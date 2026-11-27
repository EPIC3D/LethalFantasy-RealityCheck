using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LFModMenu
{
    /// <summary>
    /// Clones the first settings tab (button + page), empties the page and fills it with
    /// rows built from the game's own header/toggle/slider/dropdown prefabs, so the tab
    /// looks exactly like the built-in ones.
    /// </summary>
    [HarmonyPatch(typeof(global::Settings), "Start")]
    internal static class ModsTab
    {
        private const string Marker = "LFModMenu_Tab";

        [HarmonyPostfix]
        private static void Postfix(global::Settings __instance)
        {
            try
            {
                Build(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not build the MODS tab: " + e);
            }
        }

        private static void Build(global::Settings s)
        {
            var tabs = s.tabs;
            if (tabs == null || tabs.Length == 0) return;
            if (tabs.Any(t => t.tabGameObject != null && t.tabGameObject.name == Marker)) return;

            var src = tabs[0];
            var last = tabs[tabs.Length - 1];

            // --- Tab button -------------------------------------------------------------
            var srcButtonGo = src.button.gameObject;
            var buttonGo = UnityEngine.Object.Instantiate(srcButtonGo, srcButtonGo.transform.parent);
            buttonGo.name = Marker + "_Button";
            buttonGo.transform.SetAsLastSibling();
            PlaceAfter(buttonGo, tabs);
            SetLabel(buttonGo, Plugin.TabTitle.Value);

            var button = buttonGo.GetComponentInChildren<UIPointerPulseButton>(true);
            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonGo);
                throw new Exception("tab button component not found");
            }
            var background = Corresponding(srcButtonGo.transform, buttonGo.transform, src.backgroundImage)
                             ?? buttonGo.GetComponentInChildren<Image>(true);

            // --- Page ---------------------------------------------------------------
            var srcPage = src.tabGameObject;
            var page = UnityEngine.Object.Instantiate(srcPage, srcPage.transform.parent);
            page.name = Marker;
            var content = Corresponding(srcPage.transform, page.transform, src.contentTransform);
            if (content == null) throw new Exception("content transform not found in the cloned page");
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }

            var tab = new global::Settings.Tab
            {
                button = button,
                backgroundImage = background,
                tabGameObject = page,
                contentTransform = content,
            };
            s.tabs = tabs.Concat(new[] { tab }).ToArray();
            tab.Setup();
            tab.SetTabState(false);

            new RowBuilder(s, content).BuildAll();
            Plugin.Log.LogDebug("MODS tab added to the settings window");
        }

        /// <summary>Put the new tab button one "step" right of the last one, unless a layout group does it.</summary>
        private static void PlaceAfter(GameObject buttonGo, global::Settings.Tab[] tabs)
        {
            var parent = buttonGo.transform.parent;
            if (parent != null && parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null) return;
            if (tabs.Length < 2) return;
            var rt = buttonGo.GetComponent<RectTransform>();
            var a = tabs[tabs.Length - 2].button.GetComponent<RectTransform>();
            var b = tabs[tabs.Length - 1].button.GetComponent<RectTransform>();
            if (rt == null || a == null || b == null) return;
            rt.anchoredPosition = b.anchoredPosition + (b.anchoredPosition - a.anchoredPosition);
        }

        /// <summary>Finds the clone's counterpart of <paramref name="part"/> (a component under <paramref name="srcRoot"/>).</summary>
        private static T Corresponding<T>(Transform srcRoot, Transform cloneRoot, T part) where T : Component
        {
            if (part == null) return null;
            var path = new List<int>();
            var t = part.transform;
            while (t != null && t != srcRoot)
            {
                path.Add(t.GetSiblingIndex());
                t = t.parent;
            }
            if (t == null) return null; // not under srcRoot
            var c = cloneRoot;
            for (int i = path.Count - 1; i >= 0; i--) c = c.GetChild(path[i]);
            return c.GetComponent<T>();
        }

        /// <summary>Replace a localized label with a fixed text.</summary>
        internal static void SetLabel(GameObject go, string text)
        {
            var injector = go.GetComponentInChildren<LocalizationTextInjector>(true);
            TMP_Text label = injector != null ? injector.GetComponent<TMP_Text>() : go.GetComponentInChildren<TMP_Text>(true);
            if (injector != null) UnityEngine.Object.DestroyImmediate(injector);
            if (label != null) label.text = text;
        }

        internal static string Pretty(string key)
        {
            // "HudWeightUnits" -> "HUD WEIGHT UNITS"
            var sb = new StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c == '_' || c == '.') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && (char.IsLower(key[i - 1]) ||
                    (i + 1 < key.Length && char.IsLower(key[i + 1]) && char.IsUpper(key[i - 1]))))
                    sb.Append(' ');
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }

    internal sealed class RowBuilder
    {
        private readonly global::Settings s;
        private readonly Transform content;

        public RowBuilder(global::Settings settings, Transform content)
        {
            s = settings;
            this.content = content;
        }

        private GameObject Copy(GameObject prefab)
        {
            var go = UnityEngine.Object.Instantiate(prefab, content);
            go.SetActive(true);
            go.transform.SetAsLastSibling();
            // Tooltips read localization keys – we have none, so drop the (i) icon.
            foreach (var hover in go.GetComponentsInChildren<SimpleHoverRenderer>(true))
                UnityEngine.Object.Destroy(hover.gameObject);
            return go;
        }

        public void BuildAll()
        {
            int shown = 0;
            foreach (var info in Plugin.VisiblePlugins())
            {
                var entries = info.Instance.Config
                    .Select(kv => kv.Value)
                    .Where(IsSupported)
                    .GroupBy(e => e.Definition.Section)
                    .ToList();
                if (entries.Count == 0) continue;
                // The mod's name once; its sections as smaller sub-headings (only if it has several).
                Header(info.Metadata.Name.ToUpperInvariant());
                foreach (var section in entries)
                {
                    if (entries.Count > 1) Header(ModsTab.Pretty(section.Key), 0.6f);
                    foreach (var e in section)
                    {
                        try { Row(e); shown++; }
                        catch (Exception ex) { Plugin.Log.LogWarning($"MODS row {e.Definition.Key}: {ex.Message}"); }
                    }
                }
            }
            if (shown == 0) Header("NO MOD SETTINGS");
        }

        private static bool IsSupported(ConfigEntryBase e)
        {
            var t = e.SettingType;
            if (t == typeof(bool) || t.IsEnum) return true;
            return IsNumber(t) && e.Description?.AcceptableValues is AcceptableValueBase av && Range(av, out _, out _);
        }

        private static bool IsNumber(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(double);

        private static bool Range(AcceptableValueBase av, out double min, out double max)
        {
            min = max = 0;
            var type = av.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueRange<>)) return false;
            min = Convert.ToDouble(type.GetProperty("MinValue").GetValue(av));
            max = Convert.ToDouble(type.GetProperty("MaxValue").GetValue(av));
            return true;
        }

        private void Header(string text, float scale = 1f)
        {
            if (s.headerPrefab == null) return;
            var go = Copy(s.headerPrefab);
            ModsTab.SetLabel(go, text);
            if (scale != 1f)
            {
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    t.enableAutoSizing = false;
                    t.fontSize *= scale;
                }
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.y * scale);
                var le = go.GetComponent<LayoutElement>();
                if (le != null && le.preferredHeight > 0) le.preferredHeight *= scale;
            }
        }

        private void Row(ConfigEntryBase e)
        {
            var t = e.SettingType;
            string label = ModsTab.Pretty(e.Definition.Key);
            if (t == typeof(bool)) Toggle(e, label);
            else if (t.IsEnum) Dropdown(e, label);
            else Slider(e, label);
        }

        private void Toggle(ConfigEntryBase e, string label)
        {
            var go = Copy(s.togglePrefab);
            ModsTab.SetLabel(go, label);
            var toggle = go.GetComponentInChildren<Toggle>(true) ?? throw new Exception("no toggle");
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = (bool)e.BoxedValue;
            toggle.onValueChanged.AddListener(v => e.BoxedValue = v);
        }

        private void Dropdown(ConfigEntryBase e, string label)
        {
            var go = Copy(s.dropdownPrefab);
            ModsTab.SetLabel(go, label);
            var dd = go.GetComponentInChildren<TMP_Dropdown>(true) ?? throw new Exception("no dropdown");
            var names = Enum.GetNames(e.SettingType);
            dd.onValueChanged.RemoveAllListeners();
            dd.options = names.Select(n => new TMP_Dropdown.OptionData(ModsTab.Pretty(n))).ToList();
            dd.value = Math.Max(0, Array.IndexOf(names, e.BoxedValue.ToString()));
            dd.RefreshShownValue();
            dd.onValueChanged.AddListener(i => e.BoxedValue = Enum.Parse(e.SettingType, names[i]));
        }

        private void Slider(ConfigEntryBase e, string label)
        {
            Range(e.Description.AcceptableValues, out double min, out double max);
            var go = Copy(s.sliderPrefab);
            ModsTab.SetLabel(go, label);
            var slider = go.GetComponentInChildren<Slider>(true) ?? throw new Exception("no slider");
            var tr = go.transform;
            var valueText = tr.GetChild(tr.childCount - 1).GetComponentInChildren<TextMeshProUGUI>(true);
            bool whole = e.SettingType == typeof(int);
            slider.onValueChanged.RemoveAllListeners();
            slider.wholeNumbers = whole;
            slider.minValue = (float)min;
            slider.maxValue = (float)max;
            slider.value = Convert.ToSingle(e.BoxedValue);
            void Show(float v)
            {
                if (valueText != null)
                    valueText.text = whole ? ((int)v).ToString(CultureInfo.InvariantCulture)
                        : v.ToString(max - min >= 100 ? "0" : max - min >= 10 ? "0.0" : "0.00", CultureInfo.InvariantCulture);
            }
            Show(slider.value);
            slider.onValueChanged.AddListener(v =>
            {
                if (!whole && max - min < 10) v = Mathf.Round(v * 100f) / 100f;
                var nv = Convert.ChangeType(v, e.SettingType, CultureInfo.InvariantCulture);
                if (!nv.Equals(e.BoxedValue)) e.BoxedValue = nv;   // save only real changes while dragging
                Show(v);
            });
        }
    }
}
