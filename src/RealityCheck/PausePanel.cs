using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealityCheck
{
    /// <summary>
    /// Tabbed text panel on the left of the pause menu. Tabs are TMP links in the first line;
    /// a click handler on the text switches the page.
    /// </summary>
    internal static class PausePanel
    {
        private static PauseMenu owner;
        private static GameObject root;
        private static TextMeshProUGUI text;
        private static float nextRefresh;
        internal static int Page;

        // Colours of the game's menu: blue buttons, yellow CHALLENGES, green RESTART, orange LOBBY, red QUIT.
        internal const string Good = "#2BD62B", Bad = "#FF3B3B", Warn = "#E8A317", Head = "#E6E200", Dim = "#4DB4FF";

        private static readonly string[] Tabs =
        {
            "SIZE", "BODY", "FOOD", "THROW", "STOMP", "SOUND", "AIR", "HEAT", "COOL", "TOUCH", "SKIN", "LIMITS",
        };

        public static void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        public static void Tick(PauseMenu pm)
        {
            if (owner != pm || root == null)
            {
                owner = pm;
                (root, text) = Ui.CreatePanel(Ui.FindCanvas(pm.parentGameObject.transform), pm.debugTextMesh,
                    new Vector2(0.03f, 0.17f), new Vector2(0.29f, 0.83f), "RealityCheck_PausePanel", 26, rounded: false);   // square, like the pause menu box
                BuildTabButtons(pm.debugTextMesh);
                text.enableWordWrapping = false;               // lines never wrap into the label column
                text.enableAutoSizing = true;                  // big on short pages, a bit smaller on long ones
                text.fontSizeMax = 24;
                text.fontSizeMin = 16;
                text.overflowMode = TextOverflowModes.Overflow;   // Ellipsis would cut ALL text after the first long line
                if (root.GetComponent<RectMask2D>() == null) root.AddComponent<RectMask2D>(); // the panel frame clips anything that sticks out
                HoverTip.Attach(text, Ui.FindCanvas(pm.parentGameObject.transform));
                // Right column, used only in the expanded view.
                var go2 = UnityEngine.Object.Instantiate(text.gameObject, text.transform.parent);
                go2.name = "Text2";
                var oldTip = go2.GetComponent<HoverTip>();
                if (oldTip != null) UnityEngine.Object.Destroy(oldTip);
                text2 = go2.GetComponent<TextMeshProUGUI>();
                var r2 = text2.rectTransform;
                r2.anchorMin = new Vector2(0.5f, text.rectTransform.anchorMin.y);
                r2.anchorMax = text.rectTransform.anchorMax;
                r2.offsetMin = new Vector2(16, text.rectTransform.offsetMin.y);
                r2.offsetMax = text.rectTransform.offsetMax;
                HoverTip.Attach(text2, Ui.FindCanvas(pm.parentGameObject.transform));
                go2.SetActive(false);
                Plugin.Log.LogDebug("Pause panel attached");
            }

            bool subMenu = Open(pm.glossaryGameObject) || Open(pm.creditsGameObject) || Open(pm.challengesGameObject) || Open(pm.bugsGameObject);
            bool show = Plugin.PanelEnabled.Value && pm.parentGameObject.activeInHierarchy && !subMenu && Giantesses().Any();
            // Everything is computed once when the menu opens; buttons recompute on click.
            bool opening = show && !root.activeSelf;
            if (!show && expanded) SetExpanded(false);
            if (root.activeSelf != show) root.SetActive(show);
            if (opening)
            {
                SoundPage.Snapshot();
                Refresh();
            }
        }

        // Tab buttons look like the game's settings tabs: blue, light grey frame, active = brighter.
        private static readonly Color TabIdle = new Color(0f, 113f / 255f, 192f / 255f);
        private static readonly Color TabActive = new Color(0f, 151f / 255f, 1f);
        private static readonly List<Image> tabImages = new List<Image>();
        private static readonly string[] Characters = { "Akipo", "Solana", "Selena" };
        // Akipo pink (like the UNSTUCK button), Solana red (QUIT), Selena blue.
        private static readonly Color[] CharColors =
        {
            new Color(0.95f, 0.45f, 0.82f), new Color(0.76f, 0f, 0f), new Color(0.1f, 0.45f, 0.95f),
        };
        private static readonly List<(GameObject go, Image img)> charButtons = new List<(GameObject, Image)>();
        internal static string Selected;
        private const int TabsPerRow = 6;

        // Quick view (left side) and expanded view (almost full screen, like the game's other menus).
        private static bool expanded;
        private static TextMeshProUGUI text2;
        private static TextMeshProUGUI zoomLabel;
        private static readonly Vector2 QuickMin = new Vector2(0.03f, 0.17f), QuickMax = new Vector2(0.29f, 0.83f);
        private static readonly Vector2 WideMin = new Vector2(0.04f, 0.05f), WideMax = new Vector2(0.96f, 0.95f);

        private static void SetExpanded(bool on)
        {
            expanded = on;
            if (root == null) return;
            var rt = (RectTransform)root.transform;
            rt.anchorMin = on ? WideMin : QuickMin;
            rt.anchorMax = on ? WideMax : QuickMax;
            rt.SetAsLastSibling();
            var bg = root.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = on;          // expanded: block clicks to the pause menu behind
            var trt = text.rectTransform;
            trt.anchorMax = new Vector2(on ? 0.5f : 1f, trt.anchorMax.y);
            if (text2 != null) text2.gameObject.SetActive(on);
            text.fontSizeMax = on ? 34 : 24;
            text.fontSizeMin = on ? 20 : 16;
            if (zoomLabel != null) zoomLabel.text = on ? "–" : "+";
            Refresh();
        }
        private const float CharArea = 0.07f; // top row: character picker
        private const float ZoomStart = 0.86f; // the zoom button takes the right end of the top row
        private static float TabArea => 0.065f * ((Tabs.Length + TabsPerRow - 1) / TabsPerRow); // rows of page tabs

        private static RectTransform Area(string name, float top, float bottom)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(root.transform, false);
            rt.anchorMin = new Vector2(0, 1 - bottom);
            rt.anchorMax = new Vector2(1, 1 - top);
            rt.offsetMin = new Vector2(14, 0);
            rt.offsetMax = new Vector2(-14, top == 0 ? -12 : 0);
            return rt;
        }

        private static (GameObject go, Image img) MakeButton(RectTransform parent, string label, TMP_Text fontSource,
            float x0, float x1, float y0, float y1, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);
            var img = go.GetComponent<Image>();
            img.color = TabIdle;
            Ui.Round(img, 6);   // softly rounded, no frame – like the pause menu buttons
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var lrt = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var t = lrt.gameObject.AddComponent<TextMeshProUGUI>();
            if (fontSource != null)
            {
                t.font = fontSource.font;
                t.fontSharedMaterial = fontSource.fontSharedMaterial;
            }
            t.text = label;
            t.enableAutoSizing = true;
            t.fontSizeMin = 8;
            t.fontSizeMax = 22;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            return (go, img);
        }

        private static void BuildTabButtons(TMP_Text fontSource)
        {
            tabImages.Clear();
            charButtons.Clear();

            var chars = Area("Characters", 0, CharArea);
            for (int i = 0; i < Characters.Length; i++)
            {
                string who = Characters[i];
                charButtons.Add(MakeButton(chars, who.ToUpperInvariant(), fontSource,
                    ZoomStart * i / Characters.Length, ZoomStart * (i + 1) / Characters.Length, 0, 1,
                    () => { Selected = who; Refresh(); }));
            }
            // "+": open the big view; "–": back to the quick view.
            var zoom = MakeButton(chars, "+", fontSource, ZoomStart, 1, 0, 1, () => SetExpanded(!expanded));
            zoomLabel = zoom.go.GetComponentInChildren<TextMeshProUGUI>();

            var tabs = Area("Tabs", CharArea, CharArea + TabArea);
            int rows = (Tabs.Length + TabsPerRow - 1) / TabsPerRow;
            for (int i = 0; i < Tabs.Length; i++)
            {
                int page = i, col = i % TabsPerRow, row = i / TabsPerRow;
                tabImages.Add(MakeButton(tabs, Tabs[i], fontSource,
                    (float)col / TabsPerRow, (float)(col + 1) / TabsPerRow,
                    1f - (float)(row + 1) / rows, 1f - (float)row / rows,
                    () => { Page = page; Refresh(); }).img);
            }

            // Page text below the buttons.
            var trt = text.rectTransform;
            trt.anchorMax = new Vector2(1, 1 - CharArea - TabArea);
            trt.offsetMax = new Vector2(-16, -8);
        }

        /// <summary>Show only the characters present; highlight the one on display.</summary>
        private static void UpdateCharButtons(List<string> present, string shown)
        {
            for (int i = 0; i < charButtons.Count; i++)
            {
                var (go, img) = charButtons[i];
                if (go == null) continue;
                bool here = present.Contains(Characters[i]);
                if (go.activeSelf != here) go.SetActive(here);
                var c = CharColors[i];
                img.color = Characters[i] == shown ? c : new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);
            }
        }

        private static bool Open(GameObject go) => go != null && go.activeInHierarchy;

        internal static void Refresh()
        {
            for (int i = 0; i < tabImages.Count; i++)
                if (tabImages[i] != null) tabImages[i].color = i == Page ? TabActive : TabIdle;
            nextRefresh = Time.unscaledTime + 0.25f;
            if (text == null) return;
            string all = BuildText();
            if (!expanded || text2 == null) { text.enableAutoSizing = true; text.text = all; DrawBands(text); if (text2 != null) DrawBands(text2); return; }

            // Two columns: split between sections (blank lines) near the middle.
            var blocks = all.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.None);
            // Cut only before a section title, where the taller column is shortest.
            int Lines(int from, int to) { int n = 0; for (int i = from; i < to; i++) n += blocks[i].Split('\n').Length + 1; return n; }
            int cut = blocks.Length, best = int.MaxValue;
            for (int i = 1; i < blocks.Length; i++)
            {
                if (!blocks[i].TrimStart('\n').StartsWith($"<color={Head}><b>")) continue;
                int tallest = Math.Max(Lines(0, i), Lines(i, blocks.Length) + 2);
                if (tallest < best) { best = tallest; cut = i; }
            }
            if (cut < blocks.Length && best >= Lines(0, blocks.Length)) cut = blocks.Length;   // no gain: one column
            string left = string.Join("\n\n", blocks, 0, cut);
            string right = cut < blocks.Length ? string.Join("\n\n", blocks, cut, blocks.Length - cut) : "";
            text2.fontSizeMax = text.fontSizeMax;
            text2.fontSizeMin = text.fontSizeMin;
            text.enableAutoSizing = text2.enableAutoSizing = true;
            text.text = left;
            text2.text = "\n\n" + right;   // two lines lower: her name and a gap open the left column
            text.ForceMeshUpdate();
            text2.ForceMeshUpdate();
            float size = Mathf.Min(text.fontSize, text2.fontSize);   // the same font size in both columns
            text.enableAutoSizing = text2.enableAutoSizing = false;
            text.fontSize = text2.fontSize = size;
            DrawBands(text);
            DrawBands(text2);
        }

        internal static IEnumerable<Giantess> Giantesses()
        {
            if (GiantessManager.Instance == null) yield break;
            foreach (var g in GiantessManager.Instance.giantesses)
                if (g != null && g.gameObject.activeInHierarchy) yield return g;
        }

        internal static List<(string name, SizeStats s)> Current() =>
            Giantesses()
                .Select(g => (body: Plugin.BodyOf(Plugin.CharacterOf(g)), g))
                .Select(x => (x.body.name, s: SizeStats.Compute(x.body.height, x.body.mass, Plugin.HeightOf(x.g))))
                .OrderByDescending(x => x.s.Height).ToList();

        private static string BuildText()
        {
            var list = Current();
            if (list.Count == 0) return "";
            var u = Plugin.Units.Value;
            var sb = new StringBuilder();
            HoverTip.Hints.Clear();
            striped.Clear();
            lastHint = -1;

            // Only the selected character is shown; fall back to the largest one present.
            var big = list.FirstOrDefault(x => x.name == Selected);
            if (big.name == null) big = list[0];
            UpdateCharButtons(list.Select(x => x.name).ToList(), big.name);

            sb.Append($"<b>{big.name.ToUpperInvariant()}</b>  {Fmt.LikeHeight(big.s.Height)}  <color={Dim}>{Fmt.Length(big.s.Height, u)} · {Fmt.Factor(big.s.K)}</color>\n\n");
            switch (Page)
            {
                case 0: SizePage(sb, big.name, big.s, u, false); YouPage(sb, big.name, big.s, u); break;
                case 1: BodyPage(sb, big.name, big.s, u); break;
                case 2: FoodPage(sb, big.s, u); break;
                case 3: ThrowPage(sb, big.s, u); break;
                case 4: StompSection(sb, big.s, u); break;
                case 5: SoundPage.Build(sb, big.name, big.s, u); break;
                case 6: AirPage(sb, big.s, u); break;
                case 7: HeatPage(sb, big.s, u); break;
                case 8: CoolPage(sb, big.s, u); break;
                case 9: TouchPage(sb, big.s, u); break;
                case 10: SkinPage(sb, big.s, u); break;
                default: LimitsPage(sb, big.s, u); break;
            }
            return Finish(sb.ToString());
        }

        internal static void Section(StringBuilder sb, string title)
        {
            stripe = 0;
            sb.Append($"<color={Head}><b>{title}</b></color>\n");
        }

        // Every second entry gets a faint full-width band, so the eye stays on the row.
        private static int stripe;
        private static readonly HashSet<string> striped = new HashSet<string>();
        private static readonly System.Text.RegularExpressions.Regex LinkId =
            new System.Text.RegularExpressions.Regex("^<link=\"(\\d+)\">");

        /// <summary>Remembers every second entry (by its link id); a band is drawn behind it after layout.</summary>
        private static string Stripe(string entry)
        {
            var m = LinkId.Match(entry);
            if (stripe++ % 2 == 1 && m.Success) striped.Add(m.Groups[1].Value);
            return entry;
        }

        private static readonly Dictionary<TextMeshProUGUI, (RectTransform layer, List<Image> bands)> bandLayers =
            new Dictionary<TextMeshProUGUI, (RectTransform, List<Image>)>();

        /// <summary>Faint full-width bands behind the striped entries of a laid-out text.</summary>
        private static void DrawBands(TextMeshProUGUI t)
        {
            if (t == null) return;
            if (!bandLayers.TryGetValue(t, out var L) || L.layer == null)
            {
                var go = new GameObject("Bands", typeof(RectTransform));
                L = ((RectTransform)go.transform, new List<Image>());
                L.layer.SetParent(t.transform.parent, false);
                bandLayers[t] = L;
            }
            var tr = t.rectTransform;
            L.layer.SetSiblingIndex(tr.GetSiblingIndex());   // just behind the text
            L.layer.anchorMin = tr.anchorMin; L.layer.anchorMax = tr.anchorMax;
            L.layer.offsetMin = tr.offsetMin; L.layer.offsetMax = tr.offsetMax;
            L.layer.pivot = tr.pivot;
            bool visible = t.gameObject.activeInHierarchy;
            L.layer.gameObject.SetActive(visible);
            if (!visible) return;   // a hidden text (the right column in the quick view) has no layout

            t.ForceMeshUpdate();
            var info = t.textInfo;
            if (info == null || info.characterInfo == null || info.lineInfo == null) return;
            Rect r = tr.rect;
            int used = 0;
            for (int i = 0; i < info.linkCount; i++)
            {
                var link = info.linkInfo[i];
                if (!striped.Contains(link.GetLinkID()) || link.linkTextLength <= 0) continue;
                int first = link.linkTextfirstCharacterIndex, last = first + link.linkTextLength - 1;
                if (last >= info.characterCount) continue;
                var l0 = info.lineInfo[info.characterInfo[first].lineNumber];
                var l1 = info.lineInfo[info.characterInfo[last].lineNumber];
                if (used == L.bands.Count)
                {
                    var b = new GameObject("Band", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    b.rectTransform.SetParent(L.layer, false);
                    b.rectTransform.anchorMin = b.rectTransform.anchorMax = L.layer.pivot;   // local origin
                    b.rectTransform.pivot = new Vector2(0, 1);
                    b.color = new Color(1f, 1f, 1f, 0.06f);
                    b.raycastTarget = false;
                    L.bands.Add(b);
                }
                var band = L.bands[used++];
                band.gameObject.SetActive(true);
                float top = l0.ascender + 2, bottom = l1.descender - 2;
                band.rectTransform.anchoredPosition = new Vector2(r.xMin - 6, top);
                band.rectTransform.sizeDelta = new Vector2(r.width + 12, top - bottom);
            }
            for (int i = used; i < L.bands.Count; i++) L.bands[i].gameObject.SetActive(false);
        }

        // Two columns: label on the left, value from a fixed position.
        private const string ValueColumn = "<pos=10em>";   // em: columns follow the font size, not the panel width

        private static readonly System.Text.RegularExpressions.Regex ExPart =
            new System.Text.RegularExpressions.Regex("<ex>(.*?)</ex>", System.Text.RegularExpressions.RegexOptions.Singleline);

        /// <summary>A comparison shown on the line (the plain value moves into the tooltip).</summary>
        internal static string Ex(string text, string color = null) =>
            color == null ? $"<ex>{text}</ex>" : $"<ex><color={color}>{text}</color></ex>";

        /// <summary>Real/fantasy pair: comparisons on the line, numbers in the tooltip.</summary>
        internal static string RF(string realValue, string realLike, string fanValue, string fanLike) =>
            $"{realValue} (R)  /  {fanValue} (F) " +
            Ex((realLike + fanLike).Length > 22
                ? $"{realLike} (R)\n{ValueColumn}{fanLike} (F)"
                : $"{realLike} (R) / {fanLike} (F)");

        private static readonly System.Text.RegularExpressions.Regex DimPart =
            new System.Text.RegularExpressions.Regex("<color=" + Dim + ">(.*?)</color>");

        /// <summary>
        /// A data line. The dim (blue) parts of the value – comparisons, side notes – are moved into a
        /// mouse-over tooltip, so the line itself stays short and never wraps.
        /// </summary>
        internal static void Line(StringBuilder sb, string label, string value)
        {
            // <ex>…</ex> marks an everyday comparison that may carry its own colour.
            var colored = new List<string>();
            value = ExPart.Replace(value, m => { colored.Add(m.Groups[1].Value.Trim()); return ""; });
            var hints = new List<string>();
            string main = DimPart.Replace(value, m => { hints.Add(m.Groups[1].Value.Trim()); return ""; });
            main = System.Text.RegularExpressions.Regex.Replace(main, @"\s*[=~(]\s*$", "").Trim();
            main = System.Text.RegularExpressions.Regex.Replace(main, @"(\s*·\s*)+$", "");
            main = System.Text.RegularExpressions.Regex.Replace(main, @"\s{2,}", "  ");
            // The everyday example is shown; the exact numbers go into the mouse-over tooltip.
            var examples = hints.FindAll(IsExample).ConvertAll(Simplify).FindAll(h => h.Length > 0);
            examples.InsertRange(0, colored);
            var extras = hints.FindAll(h => !IsExample(h) && h.Length > 0);
            var tip = new List<string>(extras);
            if (examples.Count > 0)
            {
                tip.Insert(0, main);
                main = string.Join(" · ", examples);
            }
            if (main.Contains("(R)") || main.Contains("(F)")) tip.Add("(R) real physics · (F) fantasy");
            string tooltip = string.Join("\n", tip);
            int id = HoverTip.Hints.Count;
            HoverTip.Hints.Add(tooltip);
            lastHint = id;
            sb.Append(Stripe($"<link=\"{id}\"><color={Dim}>{label}</color>{ValueColumn}{main}§{id}§</link>") + "\n");
        }

        private static int lastHint = -1;

        /// <summary>Everyday comparisons start with "~", "as", "long as", "heavy as" or "=".</summary>
        private static bool IsExample(string hint)
        {
            string h = hint.Trim();
            return h.StartsWith("~") || h.StartsWith("as ") || h.StartsWith("long as ") || h.StartsWith("heavy as ")
                   || h.StartsWith("= ");
        }

        /// <summary>"long as 1.78× adult" → "1.78× adult"; "~a coin" → "a coin".</summary>
        private static string Simplify(string hint)
        {
            string h = hint.Trim();
            foreach (var prefix in new[] { "long as ", "heavy as ", "as big as ", "as ", "~", "= ", "– " })
                if (h.StartsWith(prefix)) h = h.Substring(prefix.Length).Trim();
            return h;
        }

        /// <summary>Put the tooltip marker on lines that have extra text.</summary>
        private static string Finish(string text)
        {
            for (int i = 0; i < HoverTip.Hints.Count; i++)
                text = text.Replace($"§{i}§", string.IsNullOrEmpty(HoverTip.Hints[i]) ? "" : $" <color={Head}><size=70%>+</size></color>");
            return text;
        }

        internal static void Note(StringBuilder sb, string text)
        {
            // Notes go into the tooltip of the line above.
            string clean = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
            if (lastHint >= 0 && lastHint < HoverTip.Hints.Count)
                HoverTip.Hints[lastHint] = string.IsNullOrEmpty(HoverTip.Hints[lastHint]) ? clean : HoverTip.Hints[lastHint] + "\n" + clean;
        }

        internal static string RealFantasy(string real, string fantasy) =>
            $"{real} (R)  /  {fantasy} (F)";

        // --- Pages --------------------------------------------------------------------------------

        private static void SizePage(StringBuilder sb, string name, SizeStats s, WeightUnits u, bool showName)
        {
            double ph = Plugin.YouHeight, pm = Plugin.YouMass;
            Section(sb, showName ? name.ToUpperInvariant() : "GIANTESS → NORMAL");
            Line(sb, "Weight", $"{Fmt.Mass(s.Mass, u)} · {Fmt.Num(s.Mass / pm)}× you <color={Dim}>~{Fmt.LikeMass(s.Mass)}</color>");
            Line(sb, "Foot", $"{Fmt.Length(s.FootLength, u)} × {Fmt.Length(s.FootWidth, u)} <color={Dim}>~{Fmt.LikeLength(s.FootLength)}</color> <color={Dim}>{Fmt.Num(s.FootLength / ph)}× your height</color>");
            Line(sb, "Sole area", $"{Fmt.Area(s.SoleArea, u)} per foot <color={Dim}>as big as {Fmt.LikeArea(s.SoleArea)}</color>");
            Line(sb, "Pressure", $"{Fmt.Pressure(s.GroundPressurePa, u)} <color={Dim}>~{Fmt.Num(PressureRatio(s))}× yours</color>");
            Line(sb, "Step", $"{Fmt.Length(s.StepLength, u)} <color={Dim}>~{Fmt.Num(s.StepLength / (0.413 * ph))} of your steps</color>");
            Line(sb, "Can lift", RF(Fmt.Mass(s.LiftRealKg, u), Fmt.LikeMass(s.LiftRealKg), Fmt.Mass(s.LiftFantasyKg, u), Fmt.LikeMass(s.LiftFantasyKg)));
            Line(sb, "Strength ratio", $"{Fmt.Num(100 / s.K)}% <color={Dim}>of a normal woman, for her own weight (real)</color>");
            Line(sb, "Walking", RF(Fmt.Speed(s.WalkSpeedReal, u), Fmt.LikeSpeed(s.WalkSpeedReal), Fmt.Speed(s.WalkSpeedFantasy, u), Fmt.LikeSpeed(s.WalkSpeedFantasy)));
            sb.Append('\n');
        }

        private static void StompSection(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            Section(sb, "STOMP");
            double vr = s.StompSpeedReal, vf = s.StompSpeedFantasy;
            double er = s.StompEnergy(vr) / SizeStats.TntJoulePerTon, ef = s.StompEnergy(vf) / SizeStats.TntJoulePerTon;
            Line(sb, "Foot speed", RF(Fmt.Speed(vr, u), Fmt.LikeSpeed(vr), Fmt.Speed(vf, u), Fmt.LikeSpeed(vf)));
            Line(sb, "Energy", $"{Fmt.Tnt(er)} <color={Dim}>~{Fmt.LikeEnergy(er)}</color>");
            Note(sb, $"fantasy: {Fmt.Tnt(ef)} ~{Fmt.LikeEnergy(ef)}");
            double mr = SizeStats.Magnitude(s.StompEnergy(vr) * SizeStats.StompSeismicEff);
            double mf = SizeStats.Magnitude(s.StompEnergy(vf) * SizeStats.StompSeismicEff);
            Line(sb, "Quake", RF($"M {mr:0.0}, felt {Fmt.Length(SizeStats.FeltDistance(mr), u)}", Fmt.QuakeWord(mr), $"M {mf:0.0}, felt {Fmt.Length(SizeStats.FeltDistance(mf), u)}", Fmt.QuakeWord(mf)));
            Line(sb, "Peak force", $"{Fmt.Force(s.StompForce(vr), u)} <color={Dim}>~{Fmt.LikeForce(s.StompForce(vr))}</color>");
            var (depth, layer) = SizeStats.SinkDepth(s.StompPressure(vr), s.FootWidth);
            Line(sb, "Footprint", layer < 0
                ? $"<color={Bad}>punches through bedrock</color>"
                : depth <= 0 ? "no dent" : $"{Fmt.Length(depth, u)} deep <color={Dim}>({(depth >= SizeStats.BedrockDepth ? "stopped by" : "in")} {SizeStats.Ground[layer].en})</color>");
            sb.AppendLine();

            Section(sb, "JUMP & JUMP STOMP");
            double hr = s.JumpReal, hf = s.JumpFantasy;
            Line(sb, "Jump height", RF(Fmt.Length(hr, u), "over " + Fmt.LikeHeight(hr), Fmt.Length(hf, u), "over " + Fmt.LikeHeight(hf)));
            Line(sb, "Time in the air", RealFantasy(Fmt.Time(SizeStats.AirTime(hr)), Fmt.Time(SizeStats.AirTime(hf))));
            double jr = s.JumpStompEnergy(hr), jf = s.JumpStompEnergy(hf);
            Line(sb, "Landing energy", $"{Fmt.Tnt(jr / SizeStats.TntJoulePerTon)} <color={Dim}>~{Fmt.LikeEnergy(jr / SizeStats.TntJoulePerTon)}</color>");
            Note(sb, $"fantasy: {Fmt.Tnt(jf / SizeStats.TntJoulePerTon)} ~{Fmt.LikeEnergy(jf / SizeStats.TntJoulePerTon)}");
            double qr = SizeStats.Magnitude(jr * SizeStats.StompSeismicEff), qf = SizeStats.Magnitude(jf * SizeStats.StompSeismicEff);
            Line(sb, "Quake", RF($"M {qr:0.0}, felt {Fmt.Length(SizeStats.FeltDistance(qr), u)}", Fmt.QuakeWord(qr), $"M {qf:0.0}, felt {Fmt.Length(SizeStats.FeltDistance(qf), u)}", Fmt.QuakeWord(qf)));
        }

        private static void YouPage(StringBuilder sb, string name, SizeStats s, WeightUnits u)
        {
            double ph = Plugin.YouHeight, pm = Plugin.YouMass;
            double h = ph / s.K, m = pm / (s.K * s.K * s.K);
            Section(sb, $"NORMAL → GIANTESS ({name})");
            Line(sb, "You to her", $"{Fmt.Length(h, u)} <color={Dim}>~{Fmt.LikeLength(h)}</color>");
            Line(sb, "Your weight", $"{Fmt.Mass(m, u)} <color={Dim}>~{Fmt.LikeMass(m)}</color>");
            Line(sb, "Her finger", $"{Fmt.Length(s.FingerWidth, u)} <color={Dim}>~{Fmt.LikeLength(s.FingerWidth)}</color>");
            Line(sb, "Fits under sole", $"{Fmt.Round(s.SoleArea / (0.45 * 0.3))} people");
            // Spotting you: a person notices something ~3 arcmin big. Her eye is k× bigger with cells like ours,
            // so it sees up to k× sharper – but air shimmer blurs everything below ~1 arcsec (so ~0.05 arcmin to notice).
            double? eyeD = SoundPage.EyeDistance(name);
            if (eyeD.HasValue)
            {
                double arcmin = Plugin.YouHeight / eyeD.Value * 3437.75;
                double need = Math.Max(3 / s.K, 0.05);
                bool seen = arcmin >= need;
                Line(sb, "Can she spot you?", (seen ? $"<color={Good}>yes</color>" : $"<color={Bad}>barely / no</color>") +
                    $" <color={Dim}>you look {Fmt.Num(arcmin)} arcmin from {Fmt.Length(eyeD.Value, u)}; she needs {Fmt.Num(need)} arcmin – her bigger eye sees {Fmt.Num(Math.Min(s.K, 60))}× sharper than ours, until air shimmer limits it</color>");
            }
            Line(sb, "Her blow", RF(Fmt.Speed(s.BlowWindReal, u), Fmt.Wind(s.BlowWindReal), Fmt.Speed(s.BlowWindFantasy, u), Fmt.Wind(s.BlowWindFantasy)));
            Note(sb, $"from arm's length – fantasy feels like {Fmt.Wind(s.BlowWindFantasy)}, real like {Fmt.Wind(s.BlowWindReal)}");
            Line(sb, "Step felt", $"M {s.StepMagnitude:0.0}, felt {Fmt.Length(s.StepFeltDistance, u)} " + Ex(Fmt.QuakeWord(s.StepMagnitude)));
        }

        private static void BodyPage(StringBuilder sb, string name, SizeStats s, WeightUnits u)
        {
            Section(sb, $"BODY PARTS ({name})");
            Row(sb, "", $"<color={Dim}>SIZE</color>", $"<color={Dim}>WEIGHT</color>");
            for (int i = 0; i < SizeStats.Parts.Length; i++)
            {
                var p = SizeStats.Parts[i];
                Pair(sb, p.en, Fmt.LikeLength(s.PartLength(i)), Fmt.LikeMass(s.PartMass(i)), $"{Fmt.Length(s.PartLength(i), u)} · {Fmt.Mass(s.PartMass(i), u)}");
            }
            double bust = Plugin.BustOf(name) * s.K * s.K * s.K;
            Pair(sb, "Breast (each)", Fmt.LikeVolume(bust / 950), Fmt.LikeMass(bust), $"{Fmt.Volume(bust / 950, u)} · {Fmt.Mass(bust, u)}");
            // Hips: width ~22% of height, circumference ~59%; pelvis region ~12.5% of body mass.
            double hipW = 0.22 * s.Height, hipC = 0.59 * s.Height, hipKg = 0.125 * s.Mass;
            Pair(sb, "Hips", Fmt.LikeLength(hipW), Fmt.LikeMass(hipKg), $"{Fmt.Length(hipW, u)} wide · {Fmt.Length(hipC, u)} around · {Fmt.Mass(hipKg, u)}");
            // Anime proportions: big eyes (~5 cm wide on a 1.62 m girl), small mouth (~4 cm).
            double eyeW = 0.031 * s.Height, eyeKg = 0.0075 * s.K * s.K * s.K, mouthW = 0.025 * s.Height;
            Pair(sb, "Eye (each)", Fmt.LikeLength(eyeW), Fmt.LikeMass(eyeKg), $"{Fmt.Length(eyeW, u)} wide · {Fmt.Mass(eyeKg, u)}");
            Line(sb, "Mouth", $"{Fmt.Length(mouthW, u)} wide <color={Dim}>~{Fmt.LikeLength(mouthW)}</color>");
            double tear = 3e-8 * s.K * s.K * s.K; // one teardrop ~30 µL
            Line(sb, "Teardrop", $"{Fmt.Volume(tear, u)} <color={Dim}>~{Fmt.Lake(tear)}</color>");
            Line(sb, "Finger width", $"{Fmt.Length(s.FingerWidth, u)} <color={Dim}>~{Fmt.LikeLength(s.FingerWidth)}</color>");
            double tip = 1.5e-4 * s.K * s.K; // fingertip pad ~1.5 cm² at natural size
            Line(sb, "Fingertip", $"{Fmt.Area(tip, u)} <color={Dim}>~{Fmt.LikeArea(tip)}</color>");
            Line(sb, "Skin area", $"{Fmt.Area(s.SurfaceArea, u)} <color={Dim}>as big as {Fmt.LikeArea(s.SurfaceArea)}</color>");
            sb.Append('\n');

            Section(sb, "VOLUME & BREATH");
            Line(sb, "Body volume", $"{Fmt.Volume(s.Volume, u)} <color={Dim}>~{Fmt.LikeVolume(s.Volume)}</color>");
            Line(sb, "Floating in sea", $"displaces {Fmt.Volume(s.SeaFloatDisplacedM3, u)} <color={Dim}>= {Fmt.Lake(s.SeaFloatDisplacedM3)}</color>");
            Note(sb, $"sea water is denser than her body – {Fmt.Num(100 * s.SeaFreeboard)}% of her stays above the surface");
            Line(sb, "Under the sea", $"displaces {Fmt.Volume(s.WaterDisplacedM3, u)} <color={Dim}>= {Fmt.Lake(s.WaterDisplacedM3)}</color>");
            Line(sb, "Lungs", $"{Fmt.Volume(s.LungVolumeM3, u)} <color={Dim}>~{Fmt.LikeVolume(s.LungVolumeM3)}</color>");
            Line(sb, "One breath", $"{Fmt.Volume(s.BreathM3, u)} <color={Dim}>~{Fmt.LikeVolume(s.BreathM3)}</color>");
            Line(sb, "Breaths / min", RealFantasy(Fmt.Num(s.BreathRateReal), Fmt.Num(SizeStats.BreathsPerMin)));
        }

        private static void FoodPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            // (R) real: metabolism ∝ mass^0.75 as in nature (Kleiber); (F) fantasy: ∝ mass, every cell like ours.
            double r = s.KcalWhale, f = s.KcalReal;
            Section(sb, "FOOD PER DAY");
            Line(sb, "Big Macs", RF($"{Fmt.Num(r)} kcal", Fmt.Round(r / SizeStats.BigMacKcal), $"{Fmt.Num(f)} kcal", Fmt.Round(f / SizeStats.BigMacKcal)));
            Line(sb, "Humans", RF("", Fmt.Round(r / SizeStats.HumanBodyKcal), "", Fmt.Round(f / SizeStats.HumanBodyKcal)));
            Line(sb, "Blue whales", RF("", Fmt.Round(r / SizeStats.BlueWhaleKcal), "", Fmt.Round(f / SizeStats.BlueWhaleKcal)));
            double fr = r / SizeStats.FoodKcalPerKg, ff = f / SizeStats.FoodKcalPerKg;
            Line(sb, "Food weight", RF(Fmt.Mass(fr, u), Fmt.LikeMass(fr), Fmt.Mass(ff, u), Fmt.LikeMass(ff)));
            // One bite ~15 cm³ (15 g); the throat passes a ~2.5 cm bolus – both scale with her.
            double biteKg = 0.015 * s.K * s.K * s.K, throat = 0.025 * s.K;
            Line(sb, "1 bite", $"{Fmt.Mass(biteKg, u)} <color={Dim}>~{Fmt.LikeMass(biteKg)}</color>");
            Line(sb, "Swallow whole", $"{Fmt.Length(throat, u)} wide <color={Dim}>~{Fmt.LikeLength(throat)}</color>" +
                (throat >= 0.3 ? " " + Ex("you fit", Warn) : " " + Ex("not you", Good)));
            sb.AppendLine();

            Section(sb, "DRINKING");
            double wr = s.WaterPerDayRealM3, wf = s.WaterPerDayM3(s.HeatRealW);
            Line(sb, "Water per day", RF(Fmt.Volume(wr, u), Fmt.Lake(wr), Fmt.Volume(wf, u), Fmt.Lake(wf)));
            Line(sb, "In one go", $"{Fmt.Volume(s.DrinkM3, u)} <color={Dim}>~{Fmt.Lake(s.DrinkM3)}</color>");
        }

        /// <summary>Label + two example columns; the exact numbers go into the tooltip.</summary>
        internal static void Pair(StringBuilder sb, string label, string a, string b, string exact)
        {
            int id = HoverTip.Hints.Count;
            HoverTip.Hints.Add(exact);
            lastHint = id;
            sb.Append(Stripe($"<link=\"{id}\"><color={Dim}>{label}</color><pos=10em>{a}<pos=18em>{b}§{id}§</link>") + "\n");
        }

        /// <summary>Three columns: label and two values.</summary>
        internal static void Row(StringBuilder sb, string label, string a, string b) =>
            sb.Append($"<color={Dim}>{label}</color><pos=10em>{a}<pos=18em>{b}\n");

        private static void AirPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            double head = s.EyeHeight;
            EnvInfo.Build(sb, s, u); // only when the level has Rain, Shoe, Sock or Stomach
            Section(sb, "AIR AT HER HEAD");
            Line(sb, "Oxygen", $"{OxygenText(s)}  <color={Dim}>({Fmt.Num(s.HeadO2KPa)} kPa O2)</color>");
            Line(sb, "Temperature", $"{Fmt.Temp(Climate.TempAt(head), u)} " + Ex(Fmt.TempWord(Climate.TempAt(head))) + $" <color={Dim}>(feet {Fmt.Temp(Climate.TempAt(0), u)})</color>");
            Line(sb, "Air pressure", $"{Fmt.Num(SizeStats.PressureRatio(head) * 100)}% of sea level");
            Line(sb, "Air cooling", $"{Fmt.Num(s.AirCoolingFactor * 100)}% of normal");
            sb.AppendLine();

            Section(sb, "BREATH & CLOUDS");
            double airC = SizeStats.AirTemperatureC(head);
            Line(sb, "Her breath", $"{Fmt.Temp(SizeStats.BreathC, u)}, humid, air {Fmt.Temp(airC, u)} " + Ex("steamy bathroom"));
            Line(sb, "Breath fogs", s.BreathFogs
                ? $"<color={Warn}>yes – it fogs into a small cloud</color>"
                : $"no <color={Dim}>(fogs below {Fmt.Temp(7, u)})</color>");
            Line(sb, "Own cloud", $"{Power(s.HeatRealW)} rising heat " + CloudWord(s.HeatRealW));
            Line(sb, "In the clouds", head >= SizeStats.CloudBaseM
                ? $"<color={Warn}>often – above the cloud base</color>"
                : $"no <color={Dim}>(cloud base {Fmt.Length(SizeStats.CloudBaseM, u)})</color>");
        }

        internal static string Power(double w) =>
            w < 1e3 ? Fmt.Num(w) + " W" : w < 1e6 ? Fmt.Num(w / 1e3) + " kW" : w < 1e9 ? Fmt.Num(w / 1e6) + " MW" : w < 1e12 ? Fmt.Num(w / 1e9) + " GW" : Fmt.Num(w / 1e12) + " TW";

        private static void SkinPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            Section(sb, "HER SKIN UP CLOSE");
            double k = s.K;
            double ridge = 0.0005 * k, groove = 0.0001 * k, rough = 0.00003 * k, pore = 0.00005 * k;
            Line(sb, "Fingerprint ridges", $"{Ridges(groove)} <color={Dim}>{Fmt.Length(ridge, u)} apart, {Fmt.Length(groove, u)} deep</color>");
            Line(sb, "Skin texture", $"{Texture(rough)} <color={Dim}>{Fmt.Length(rough, u)} bumps</color>");
            Line(sb, "Pores", $"{Fmt.Length(pore, u)} wide <color={Dim}>~{Fmt.LikeLength(pore)}</color>");
            Line(sb, "Fine hairs", $"{HairLike(0.00003 * k)} <color={Dim}>{Fmt.Length(0.00003 * k, u)} thick, {Fmt.Length(0.002 * k, u)} long</color>");
            // One hair from her head: 0.07 mm thick, ~60 cm long, holds ~100 g (strength ∝ cross-section, k²).
            double hairD = 0.00007 * k, hairL = 0.6 * k;
            double hairKg = 1300 * Math.PI * hairD * hairD / 4 * hairL;
            double holdsKg = 0.1 * k * k;
            Line(sb, "One hair", $"{HairLike(hairD)} <color={Dim}>{Fmt.Length(hairD, u)} thick, {Fmt.Length(hairL, u)} long, {Fmt.Mass(hairKg, u)}</color>");
            Line(sb, "It holds", $"{Fmt.Mass(holdsKg, u)} " + Ex(Fmt.LikeMass(holdsKg)) +
                (holdsKg >= Plugin.YouMass ? Ex("climbable", Good) : Ex("snaps under you", Warn)));
        }

        private static void TouchPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            double real = s.HeatRealW;
            Section(sb, "HER WARMTH ON YOU");
            // (R) she can lose her heat: skin 34 °C. (F) she overheats: feverish skin radiates more.
            double skinR = s.SkinC(s.HeatKleiberW), skinF = s.SkinC(s.HeatRealW);
            double feltR = s.FeltDistanceFor(skinR), feltF = s.FeltDistanceFor(skinF);
            Line(sb, "Felt from", RF(Fmt.Length(feltR, u), Fmt.LikeLength(feltR), Fmt.Length(feltF, u), Fmt.LikeLength(feltF)));
            double armR = s.RadiantWm2(0.4 * s.Height, skinR), armF = s.RadiantWm2(0.4 * s.Height, skinF);
            double nearR = s.RadiantWm2(0, skinR), nearF = s.RadiantWm2(0, skinF);
            Line(sb, "At arm's length", RF($"{Fmt.Num(armR)} W/m²", Warmth(armR), $"{Fmt.Num(armF)} W/m²", Warmth(armF)));
            Line(sb, "Right next to her", RF($"{Fmt.Num(nearR)} W/m²", Warmth(nearR), $"{Fmt.Num(nearF)} W/m²", Warmth(nearF)));
            sb.AppendLine();

            Section(sb, "TOUCH & HUG");
            double ph = Plugin.YouHeight;
            Line(sb, "Her palm", $"covers {Fmt.Num(100 * s.PalmCoverage(ph))}% of you <color={Dim}>{Fmt.Area(s.PalmArea, u)}</color>");
            Line(sb, "Her skin", RF(Fmt.Temp(skinR, u), SkinWord(skinR), Fmt.Temp(skinF, u), SkinWord(skinF)));
            Line(sb, "Her hand", RF("", HandWord(skinR), "", HandWord(skinF)));
            Line(sb, "Her foot", $"{Fmt.Temp(skinR - 5, u)} (R) / {Fmt.Temp(skinF - 5, u)} (F) " + Ex("cooler than a hand"));
            Line(sb, "On her chest", $"{Fmt.Temp(skinR + 0.5, u)}, warmest <color={Dim}>her breathing lifts you {Fmt.Length(s.ChestRiseM, u)}</color>");
            Note(sb, $"heartbeat: {Fmt.Num(s.HeartRateReal)} bpm (R) / {Fmt.Num(SizeStats.HeartRateBpm)} bpm (F) – a deep, slow thump");
            Line(sb, "In her lap", $"{Fmt.Temp(skinR - 1.5, u)}, soft <color={Dim}>thigh {Fmt.Length(s.PartLength(3) * 0.5, u)} long, soft and firm</color>");
            Line(sb, "Under her sole", $"{Fmt.Temp(skinR - 5, u)}, dark <color={Dim}>humid, almost airless</color>");
            Line(sb, "A hug", RF("", HugWord(skinR, s.PalmCoverage(ph) >= 1), "", HugWord(skinF, s.PalmCoverage(ph) >= 1)));
            sb.AppendLine();


        }

        /// <summary>What her fingerprint grooves feel like to your hand.</summary>
        private static string Ridges(double depth) =>
            depth < 0.0005 ? "too fine to feel" :
            depth < 0.001 ? "vinyl grooves" :
            depth < 0.002 ? "a grippy phone case" :
            depth < 0.004 ? "corduroy" :
            depth < 0.008 ? "a tyre tread" :
            depth < 0.015 ? "a boot sole" :
            depth < 0.03 ? "a tractor tyre" :
            depth < 0.06 ? "ploughed furrows" :
            depth < 0.12 ? "a gutter" :
            depth < 0.25 ? "a ditch" :
            depth < 0.5 ? "a trench" :
            depth < 1 ? "a dry stream bed" :
            "a valley";

        /// <summary>Skin roughness compared with surfaces you know.</summary>
        private static string Texture(double bumps) =>
            bumps < 0.00005 ? "smooth paper" :
            bumps < 0.0001 ? "fine sandpaper" :
            bumps < 0.0003 ? "medium sandpaper" :
            bumps < 0.0005 ? "coarse sandpaper" :
            bumps < 0.001 ? "a nail file" :
            bumps < 0.002 ? "a doormat" :
            bumps < 0.004 ? "asphalt" :
            bumps < 0.008 ? "a gravel path" :
            bumps < 0.015 ? "coarse gravel" :
            bumps < 0.03 ? "cobblestones" :
            bumps < 0.06 ? "a pebble beach" :
            bumps < 0.12 ? "a stony riverbed" :
            bumps < 0.25 ? "a rock field" :
            bumps < 0.5 ? "a boulder field" :
            "a rockslide";

        private static string HairLike(double thickness) =>
            thickness < 0.0001 ? "a fine thread" :
            thickness < 0.0003 ? "sewing thread" :
            thickness < 0.0006 ? "fishing line" :
            thickness < 0.001 ? "a guitar string" :
            thickness < 0.002 ? "a shoelace" :
            thickness < 0.004 ? "spaghetti" :
            thickness < 0.008 ? "a pencil" :
            thickness < 0.012 ? "a climbing rope" :
            thickness < 0.025 ? "a broom handle" :
            thickness < 0.05 ? "a ship's rope" :
            thickness < 0.1 ? "a mooring line" :
            thickness < 0.2 ? "a lamppost" :
            thickness < 0.4 ? "a tree trunk" :
            "a pillar";

        private static void CoolPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            // Cooling is always real physics; her heat is (R) whale-like metabolism or (F) every cell like ours.
            double rRest = s.NetHeat(s.HeatKleiberW * SizeStats.RestShare), fRest = s.NetHeat(s.HeatRealW * SizeStats.RestShare);
            double rSwim = s.NetHeat(s.HeatKleiberW * SizeStats.SwimShare), fSwim = s.NetHeat(s.HeatRealW * SizeStats.SwimShare);
            Section(sb, $"IN THE SEA ({Fmt.Temp(SizeStats.SeaC, u)})");
            CoolLine(sb, "Floating", s.SeaCoolingW(false), rRest, fRest);
            CoolLine(sb, "Swimming", s.SeaCoolingW(true), rSwim, fSwim);
            sb.AppendLine();

            Section(sb, $"UNDER A WATERFALL ({Fmt.Temp(SizeStats.RiverC, u)} river)");
            foreach (var f in SizeStats.Waterfalls)
                CoolLine(sb, f.name, s.WaterfallCoolingW(f.flow, f.width), rRest, fRest);
            sb.AppendLine();

            Section(sb, $"AT THE SOUTH POLE ({Fmt.Temp(SizeStats.PoleC, u)}, windy)");
            CoolLine(sb, "Standing", s.PoleCoolingW, rRest, fRest);
            double rWalk = s.NetHeat(s.HeatKleiberW), fWalk = s.NetHeat(s.HeatRealW);
            CoolLine(sb, "Walking", s.PoleCoolingW, rWalk, fWalk);
        }

        /// <summary>One cooling place: verdict for (R) and (F); the powers go into the tooltip.</summary>
        private static void CoolLine(StringBuilder sb, string label, double coolW, double rHeat, double fHeat)
        {
            string V(double heat) =>
                heat > coolW ? $"<color={Bad}>overheats</color>" :
                heat < coolW * 0.3 ? $"<color={Warn}>gets cold</color>" :
                $"<color={Good}>just right</color>";
            Line(sb, label, $"cools {Power(coolW)}; heat {Power(rHeat)} (R) / {Power(fHeat)} (F) " + Ex($"{V(rHeat)} (R) / {V(fHeat)} (F)"));
        }

        private static void HeatPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            double r = s.HeatKleiberW, f = s.HeatRealW;   // (R) whale-like metabolism, (F) every cell like ours
            Section(sb, "BODY HEAT (walking)");
            Line(sb, "Heat output", RF(Power(r), Fmt.LikePower(r), Power(f), Fmt.LikePower(f)));
            Note(sb, $"continuous (per second); per day {Fmt.Tnt(r * 86400 / SizeStats.TntJoulePerTon)} (R) / {Fmt.Tnt(f * 86400 / SizeStats.TntJoulePerTon)} (F)");
            // Air flowing past her (light 3 m/s breeze) carries the heat away: ΔT = P / (ρ·c·v·A), A = her front.
            double front = 0.25 * s.Height * s.Height, flow = SizeStats.Density(s.Height / 2) * 1005 * 3 * front;
            Line(sb, "Air around her", RF($"+{Fmt.Num(r / flow)} °C", HeatWord(r / flow), $"+{Fmt.Num(f / flow)} °C", HeatWord(f / flow)));
            Line(sb, "Dry cooling", $"{Power(s.NonEvapW)} " + Ex(Fmt.LikePower(s.NonEvapW)));
            // How she feels in her surroundings: the map air at her mid-height (thin, maybe cold) vs. her heat.
            double midAir = Climate.TempAt(s.Height / 2);
            double dryHere = SizeStats.NonEvapWm2 * s.SurfaceArea * Math.Sqrt(SizeStats.PressureRatio(s.Height / 2)) * Math.Max(0, SizeStats.NormalSkinC - midAir) / (SizeStats.NormalSkinC - 15);
            Line(sb, "She feels", $"air {Fmt.Temp(midAir, u)} at her middle " + Ex($"{Comfort(s.NetHeat(r), dryHere, s.MaxEvapW)} (R) / {Comfort(s.NetHeat(f), dryHere, s.MaxEvapW)} (F)"));
            sb.AppendLine();

            Section(sb, "SWEAT");
            double nr = s.SweatLph(r), nf = s.SweatLph(f), max = s.MaxSweatLph;
            Line(sb, "Sweat needed", RF($"{Fmt.Volume(nr / 1000, u)}/h", Fmt.Lake(nr / 1000) + "/h", $"{Fmt.Volume(nf / 1000, u)}/h", Fmt.Lake(nf / 1000) + "/h"));
            string Enough(double need) => need <= max ? $"<color={Good}>enough</color>" : $"<color={Bad}>too little</color>";
            Line(sb, "Can sweat", $"{Fmt.Volume(max / 1000, u)}/h " + Ex($"{Enough(nr)} (R) / {Enough(nf)} (F)"));
            string Stroke(double heat, double dT) =>
                s.ExcessHeatW(heat) > 0 ? $"<color={(dT > 3 ? Bad : Warn)}>{Duration(s.OverheatSeconds(heat, dT))}</color>" : $"<color={Good}>never</color>";
            Line(sb, "Heatstroke", $"walking, 40 °C " + Ex($"{Stroke(r, 3)} (R) / {Stroke(f, 3)} (F)"));
            Line(sb, "Deadly", $"walking, 42 °C " + Ex($"{Stroke(r, 5)} (R) / {Stroke(f, 5)} (F)"));
            double vaporM3h = Math.Min(nf, max) / 1000;
            Line(sb, "Sweat vapour (F)", $"{Fmt.Volume(vaporM3h, u)}/h · 1 mm/h over {Fmt.Area(vaporM3h * 1000, u)} " + Ex("1 mm/h rain on " + Fmt.LikeArea(vaporM3h * 1000)));
            sb.AppendLine();

            bool hot = s.ExcessHeatW(f) > 0;
            sb.Append($"<color={Head}><b>CONCLUSION:</b></color> " + (hot
                ? $"<color=#FF5FB0><b>Giantesses are HOT!</b></color> <color={Dim}>(F)</color>"
                : $"<color={Good}><b>She keeps her cool</b></color>"));
            sb.AppendLine();
        }

        /// <summary>Nerve delay told as game lag: frames at 60 FPS and what that ping feels like online.</summary>
        internal static string Lag(double seconds)
        {
            double frames = seconds * 60;
            string feel =
                seconds < 0.017 ? "less than one frame – instant" :
                seconds < 0.05 ? "a great ping" :
                seconds < 0.1 ? "a playable ping" :
                seconds < 0.2 ? "noticeable input lag" :
                seconds < 0.5 ? "a laggy server" :
                seconds < 2 ? "rubber-banding" :
                seconds < 10 ? "frozen – 'connection unstable'" :
                "timed out – disconnected";
            return $"= {Fmt.Num(frames)} frames at 60 FPS, like {feel}";
        }

        internal static string Duration(double s) =>
            double.IsInfinity(s) ? "never" :
            s < 120 ? $"{Fmt.Num(s)} s" : s < 7200 ? $"{Fmt.Num(s / 60)} min" : s < 172800 ? $"{Fmt.Num(s / 3600)} h" : $"{Fmt.Num(s / 86400)} days";

        /// <summary>Throw distance as an example, or the orbit text for very fast throws.</summary>
        /// <summary>Distance with air as an example, or where it goes once out of the air.</summary>
        private static string DragLike((double range, double exit) r) =>
            double.IsInfinity(r.range) ? (SizeStats.Fate(r.exit) ?? "space, falls back") : Fmt.LikeLength(r.range);

        private static string ThrowLike(double v, double range) =>
            SizeStats.Fate(v) ?? Fmt.LikeLength(range);

        /// <summary>What her rising body heat does to the sky (a big wildfire, ~1 GW, builds its own cumulus).</summary>
        private static string CloudWord(double heatW) =>
            heatW < 1e5 ? Ex("none", Good) :
            heatW < 1e7 ? Ex("heat shimmer") :
            heatW < 1e9 ? Ex("wisp of cloud") :
            heatW < 1e11 ? Ex("own cumulus", Warn) :
            heatW < 1e13 ? Ex("thunderhead", Warn) :
            Ex("storm system", Bad);

        /// <summary>How much warmer the air around her gets, as something you know.</summary>
        private static string HeatWord(double dT) =>
            dT < 0.5 ? "unnoticed" : dT < 3 ? "a mild breath" : dT < 10 ? "a city heat island" :
            dT < 25 ? "a heatwave" : dT < 60 ? "a desert noon" : dT < 120 ? "a sauna" :
            dT < 300 ? "an oven" : dT < 800 ? "a wildfire" : "molten rock";

        internal static string GText(double g) =>
            $"<color={(g < 5 ? Good : g < 20 ? Warn : Bad)}>{Fmt.Num(g)} g</color>";

        /// <summary>Smallest height at which the (F) heat can no longer be lost (same model as the HEAT page).</summary>
        private static double OverheatHeight(SizeStats s)
        {
            double lo = s.BaseHeight, hi = s.BaseHeight * 100_000;
            for (int i = 0; i < 60; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                var t = SizeStats.Compute(s.BaseHeight, s.BaseMass, mid);
                if (t.ExcessHeatW(t.HeatRealW) > 0) hi = mid; else lo = mid;
            }
            return hi;
        }

        /// <summary>Her comfort: heat made vs. what the air takes without sweat, and what sweat can add.</summary>
        private static string Comfort(double heatW, double dryW, double sweatW) =>
            heatW > dryW + sweatW ? $"<color={Bad}>overheating</color>" :
            heatW > dryW + 0.3 * sweatW ? $"<color={Warn}>hot, sweating</color>" :
            heatW > dryW ? $"<color={Warn}>warm</color>" :
            heatW > 0.5 * dryW ? $"<color={Good}>cosy</color>" :
            heatW > 0.25 * dryW ? "<color=#4DB4FF>chilly</color>" :
            "<color=#4DB4FF>cold</color>";

        private static string SkinWord(double c) =>
            c < 37 ? "normal skin" : c < 42 ? "feverish" : "burning hot";

        private static string HandWord(double skinC) =>
            skinC < 37 ? "pleasantly warm" : skinC < 42 ? "feverish, like a hot-water bottle" : "too hot to hold";

        private static string HugWord(double skinC, bool wraps) =>
            skinC < 37 ? (wraps ? "a warm duvet" : "a warm hug") : skinC < 42 ? "a hot bath" : "a sauna – unbearable";

        internal static string Warmth(double wm2) =>
            wm2 < 10 ? "not noticeable" :
            wm2 < 25 ? "faint, like a sunny wall" :
            wm2 < 45 ? "like a warm stove" :
            "like a radiator";

        private static void ThrowPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            Section(sb, "BASEBALL SCALED TO HER SIZE");
            Line(sb, "Ball", $"{Fmt.Length(s.BallSize, u)}, {Fmt.Mass(s.BallKg, u)} <color={Dim}>~{Fmt.LikeMass(s.BallKg)}</color>");
            Line(sb, "Throw (no air)", RF(SizeStats.ThrowResult(s.ThrowSpeedReal, s.ThrowRangeReal, u), ThrowLike(s.ThrowSpeedReal, s.ThrowRangeReal), SizeStats.ThrowResult(s.ThrowSpeedFantasy, s.ThrowRangeFantasy, u), ThrowLike(s.ThrowSpeedFantasy, s.ThrowRangeFantasy)));
            var dr = SizeStats.ThrowWithDrag(s.ThrowSpeedReal, s.BallSize, s.BallKg, s.ReleaseHeight);
            var df = SizeStats.ThrowWithDrag(s.ThrowSpeedFantasy, s.BallSize, s.BallKg, s.ReleaseHeight);
            Line(sb, "Throw (with air)", RF(SizeStats.DragResult(dr, u), DragLike(dr), SizeStats.DragResult(df, u), DragLike(df)));
            Line(sb, "Impact", RF(HudPanel.Tnt(s.ImpactRealTnt), Fmt.LikeEnergy(s.ImpactRealTnt), HudPanel.Tnt(s.ImpactFantasyTnt), Fmt.LikeEnergy(s.ImpactFantasyTnt)));
            sb.Append('\n');

            Section(sb, "IF SHE THREW YOU (you stay normal size)");
            var real = s.ThrowPlayer(s.ThrowSpeedReal);
            var fan = s.ThrowPlayer(s.ThrowSpeedFantasy);
            Line(sb, "Release speed", RF(Fmt.Speed(s.ThrowSpeedReal, u), Fmt.LikeSpeed(s.ThrowSpeedReal), Fmt.Speed(s.ThrowSpeedFantasy, u), Fmt.LikeSpeed(s.ThrowSpeedFantasy)));
            Line(sb, "You'd fly", RF(SizeStats.DragResult((real.range, real.impact), u), DragLike((real.range, real.impact)), SizeStats.DragResult((fan.range, fan.impact), u), DragLike((fan.range, fan.impact))));
            Note(sb, $"to her that's {Fmt.Length(real.range / s.K, u)} – like tossing {Fmt.LikeMass(Plugin.YouMass / (s.K * s.K * s.K))}");
            Line(sb, "G-force", $"{GText(s.ThrowG(s.ThrowSpeedReal))} (R)  /  {GText(s.ThrowG(s.ThrowSpeedFantasy))} (F)");
            if (!double.IsInfinity(fan.range))
                Line(sb, "Landing speed", RF(Fmt.Speed(real.impact, u), Fmt.LikeSpeed(real.impact), Fmt.Speed(fan.impact, u), Fmt.LikeSpeed(fan.impact)));
            else
                Line(sb, "Landing speed", $"{Fmt.Speed(real.impact, u)} (R)  /  never lands (F)");
            Note(sb, $"(R) you land like falling from {Fmt.Length(real.impact * real.impact / (2 * SizeStats.G), u)}; air caps a tumbling human near {Fmt.Speed(SizeStats.HumanTerminal, u)}");
            sb.Append('\n');

            Section(sb, "AIR DRAG");
            double realLoss = 1 - s.ThrowRangeRealDrag / Math.Max(1e-9, s.ThrowRangeReal);
            double fanLoss = double.IsInfinity(df.range) || s.ThrowSpeedFantasy >= SizeStats.OrbitalSpeed ? 0 : 1 - df.range / Math.Max(1e-9, s.ThrowRangeFantasy);
            double youLoss = 1 - SizeStats.PlayerThrowRangeDrag / SizeStats.PlayerThrowRange;
            Line(sb, "Air drag takes", RealFantasy($"{Fmt.Num(realLoss * 100)}%", $"{Fmt.Num(fanLoss * 100)}%"));
            Note(sb, $"of its range to air drag – yours loses {Fmt.Num(youLoss * 100)}%");
            if (s.ThrowSpeedFantasy > SizeStats.SpeedOfSound)
                Note(sb, $"fantasy throw: Mach {Fmt.Num(s.ThrowSpeedFantasy / SizeStats.SpeedOfSound)} – the ball would glow");
            Line(sb, "Hand speed", RF(Fmt.Speed(s.ThrowSpeedReal, u), Fmt.LikeSpeed(s.ThrowSpeedReal), Fmt.Speed(s.ThrowSpeedFantasy, u), Fmt.LikeSpeed(s.ThrowSpeedFantasy)));
        }

        internal static string OxygenText(SizeStats s)
        {
            int lvl = SizeStats.OxygenLevel(s.EyeHeight);
            string pct = Fmt.Num(SizeStats.PressureRatio(s.EyeHeight) * 100) + "%";
            (string en, string color) st = lvl switch
            {
                0 => ("normal", Good),
                1 => ("thin air", Warn),
                2 => ("hypoxia", Warn),
                3 => ("death zone", Bad),
                4 => ("blood would boil", Bad),
                _ => ("space", Bad),
            };
            return $"<color={st.color}>{pct} · {st.en}</color>";
        }

        private static string Verdict(bool ok, double failHeight, WeightUnits u)
        {
            if (ok) return $"<color={Good}>OK</color>";
            return $"<color={Bad}>FAILS</color> <color={Dim}>(above {Fmt.Length(failHeight, u)})</color>";
        }

        /// <summary>"3 m – up to her ankles, stopped by dense gravel".</summary>
        internal static string Sinking(SizeStats s, WeightUnits u)
        {
            var (depth, layer) = s.SinkDepth();
            if (layer < 0) return $"<color={Bad}>through bedrock</color>";
            var g = SizeStats.Ground[layer];
            if (depth <= 0) return $"<color={Good}>not at all</color> <color={Dim}>{g.en} holds</color>";
            string body = depth < s.AnkleHeight ? "below her ankles"
                : depth < s.KneeHeight ? "up to her shins"
                : "above her knees";
            string where = depth >= SizeStats.BedrockDepth ? $"stopped by {g.en}" : $"in {g.en}";
            string color = depth < 0.05 ? Good : depth < s.AnkleHeight ? Warn : Bad;
            return $"<color={color}>{Fmt.Length(depth, u)}</color> <color={Dim}>{body}, {where}</color>";
        }

        internal static double PressureRatio(SizeStats s)
        {
            double ph = Plugin.YouHeight, pm = Plugin.YouMass;
            return s.GroundPressurePa / SizeStats.Compute(ph, pm, ph).GroundPressurePa;
        }

        private static void LimitsPage(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            Section(sb, "REALITY CHECK");
            double b = s.BaseHeight;

            Line(sb, "Bones",
                Verdict(s.BoneStressMPa < SizeStats.BoneStrengthMPa, b * SizeStats.BoneStrengthMPa / SizeStats.WalkingBoneStressMPa, u) +
                $" <color={Dim}>{Fmt.Num(s.BoneStressMPa)} of {SizeStats.BoneStrengthMPa} MPa</color>");

            double baseP = s.GroundPressurePa / s.K;
            Line(sb, "Sinks in", Sinking(s, u));
            Note(sb, "even granite is crushed above " + Fmt.Length(b * SizeStats.GraniteStrengthMPa * 1e6 / baseP, u));

            double bp = s.BloodPressureMmHg;
            Line(sb, "Heart → brain",
                Verdict(bp < 300, b * (300 - SizeStats.BaseDiastolicDriveMmHg) / (bp - SizeStats.BaseDiastolicDriveMmHg) * s.K, u) +
                $" <color={Dim}>needs {Fmt.Num(bp)} mmHg (human 120, giraffe 300)</color>");

            double cooling = SizeStats.MaxCoolingWm2 * Math.Max(0.05, s.AirCoolingFactor);
            Line(sb, "Body heat",
                Verdict(s.ExcessHeatW(s.HeatRealW) <= 0, OverheatHeight(s), u) +
                $" <color={Dim}>(F) every cell like ours; same model as the HEAT page</color>");

            Line(sb, "Breathing", OxygenText(s));
            Line(sb, "Nerve delay",
                $"{Fmt.Time(s.NerveDelay)} <color={Dim}>(human {Fmt.Time(0.9 * b / SizeStats.NerveSpeed)})</color>");
            Note(sb, Lag(s.NerveDelay));
            Line(sb, "Voice",
                (s.VoiceHz < 20 ? $"<color={Bad}>infrasound</color> <color={Dim}>{Fmt.Num(s.VoiceHz)} Hz</color>" : $"{Fmt.Num(s.VoiceHz)} Hz"));
            Line(sb, "Step tremor", $"M {s.StepMagnitude:0.0}, felt {Fmt.Length(s.StepFeltDistance, u)} " + Ex(Fmt.QuakeWord(s.StepMagnitude)));
            Line(sb, "Real motion", $"{Fmt.Num(s.SlowMotion)}× slower");
            sb.AppendLine();
            // Bones, ground and heat all give out near ×6–7 of her natural height.
            double maxH = MaxWorkingHeight(s.BaseHeight);
            Line(sb, "Largest possible", $"~{Fmt.Length(maxH, u)} " + Ex("limping, at the edge") +
                $" <color={Dim}>bones, ground and heat all fail just above; not sustainable for long in this body shape</color>");
        }

        /// <summary>The largest height a human-shaped body could just about work at (~90% of the bone limit).</summary>
        internal static double MaxWorkingHeight(double baseHeight) =>
            0.9 * baseHeight * SizeStats.BoneStrengthMPa / SizeStats.WalkingBoneStressMPa;
    }
}
