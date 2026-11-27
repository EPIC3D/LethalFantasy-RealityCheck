using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealityCheck
{
    internal static class Ui
    {
        /// <summary>Nearest Canvas up the hierarchy – unlike GetComponentInParent this also works on inactive objects.</summary>
        public static Canvas FindCanvas(Transform t)
        {
            Canvas found = null;
            for (; t != null; t = t.parent)
            {
                var c = t.GetComponent<Canvas>();
                if (c != null) found = c; // keep going: we want the root canvas
            }
            return found;
        }

        /// <summary>Dark box with a rich-text TMP label, in the game's font, on the given canvas.</summary>
        public static (GameObject root, TextMeshProUGUI text) CreatePanel(Canvas canvas, TMP_Text fontSource,
            Vector2 anchorMin, Vector2 anchorMax, string name, float maxFont, bool background = true)
        {
            if (canvas == null) throw new Exception("canvas not found for " + name);
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)root.transform;
            rt.SetParent(canvas.transform, false);
            rt.SetAsLastSibling();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = root.GetComponent<Image>();
            bg.color = background ? new Color(0f, 0.035f, 0.07f, 0.97f) : Color.clear; // the main menu panel navy
            bg.raycastTarget = false;
            if (background)
            {
                var border = root.AddComponent<Outline>(); // light grey frame, a bit thicker than the main menu one
                border.effectColor = new Color(0.78f, 0.8f, 0.82f, 1f);
                border.effectDistance = new Vector2(5f, -5f);
            }

            var textGo = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16, 12);
            trt.offsetMax = new Vector2(-16, -12);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (fontSource != null)
            {
                text.font = fontSource.font;
                text.fontSharedMaterial = fontSource.fontSharedMaterial;
            }
            text.enableAutoSizing = true;
            text.fontSizeMin = 8;
            text.fontSizeMax = maxFont;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.richText = true;
            text.raycastTarget = false;
            text.color = Color.white;
            if (!background) Readable(text);
            root.SetActive(false);
            return (root, text);
        }

        /// <summary>Thin dark outline so text stays readable without a box (own material instance).</summary>
        public static void Readable(TMP_Text text)
        {
            var mat = new Material(text.fontSharedMaterial);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.18f);
            mat.SetColor("_OutlineColor", new Color(0, 0, 0, 0.85f));
            text.fontSharedMaterial = mat;
        }

        /// <summary>
        /// Shrinks a HUD panel to its text and gives it a 50% dark backdrop, pinned to a screen corner
        /// (anchor = corner in screen fractions, pivot = which corner of the box sits there).
        /// </summary>
        private static int menusScene = -1;
        private static GridMainUIManagerBase[] menus = new GridMainUIManagerBase[0];
        private static float nextMenuCheck;
        private static bool menuOpen;

        /// <summary>True while a crafting / cauldron / enchanting grid menu is open (checked at most twice a second).</summary>
        public static bool CraftingOpen()
        {
            if (Time.unscaledTime < nextMenuCheck) return menuOpen;
            nextMenuCheck = Time.unscaledTime + 0.5f;
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene != menusScene)
            {
                menusScene = scene;
                menus = UnityEngine.Object.FindObjectsOfType<GridMainUIManagerBase>(true);
            }
            menuOpen = false;
            foreach (var m in menus)
                if (m != null && m.gameObject.activeInHierarchy) { menuOpen = true; break; }
            return menuOpen;
        }

        /// <summary>Popup: fixed width, height from the wrapped text, pinned to the corner its anchors point at.</summary>
        public static void FitPopup(GameObject root, TMP_Text text, float width)
        {
            var rt = (RectTransform)root.transform;
            Vector2 a = rt.anchorMax;                       // PlacePopup set a box; use its outer corner
            bool right = a.x > 0.5f, top = a.y > 0.5f;
            Vector2 corner = new Vector2(right ? rt.anchorMax.x : rt.anchorMin.x, top ? rt.anchorMax.y : rt.anchorMin.y);
            Vector2 size = text.GetPreferredValues(text.text, width - 32, 0);
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = new Vector2(right ? 1 : 0, top ? 1 : 0);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, size.y + 24);
        }

        /// <summary>
        /// Where "you" are: the active camera (your eyes). In interactions (hug, lap, hand, foot) the game
        /// moves only the camera onto her body – your body stays where it was.
        /// </summary>
        public static Vector3? Eye()
        {
            var cam = Camera.main;
            return cam != null && cam.isActiveAndEnabled ? cam.transform.position : (Vector3?)null;
        }

        public static void FitToText(GameObject root, TMP_Text text, Vector2 anchor, Vector2 pivot)
        {
            var rt = (RectTransform)root.transform;
            Vector2 size = text.GetPreferredValues(text.text);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size.x + 32, size.y + 24);   // text padding is 16 × 12
            var bg = root.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0f, 0f, 0f, 0.5f);
        }

        public static void PlacePopup(RectTransform rt, TipPosition pos)
        {
            (Vector2 min, Vector2 max) a = pos switch
            {
                TipPosition.UnderSizeBar => (new Vector2(0.35f, 0.78f), new Vector2(0.65f, 0.90f)),
                TipPosition.BottomLeft => (new Vector2(0.01f, 0.32f), new Vector2(0.30f, 0.45f)),
                TipPosition.BottomRight => (new Vector2(0.70f, 0.32f), new Vector2(0.99f, 0.45f)),
                TipPosition.AboveHotbar => (new Vector2(0.30f, 0.19f), new Vector2(0.70f, 0.29f)),
                _ => (new Vector2(0.74f, 0.82f), new Vector2(0.99f, 0.97f)), // top right
            };
            rt.anchorMin = a.min;
            rt.anchorMax = a.max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void PlaceTicker(RectTransform rt, TipPosition pos)
        {
            (Vector2 min, Vector2 max) a = pos switch
            {
                TipPosition.TopRight => (new Vector2(0.70f, 0.70f), new Vector2(0.99f, 0.735f)),
                TipPosition.BottomLeft => (new Vector2(0.01f, 0.03f), new Vector2(0.33f, 0.065f)),
                TipPosition.BottomRight => (new Vector2(0.67f, 0.31f), new Vector2(0.99f, 0.345f)), // above the data panel
                TipPosition.AboveHotbar => (new Vector2(0.28f, 0.175f), new Vector2(0.72f, 0.212f)),
                _ => (new Vector2(0.345f, 0.895f), new Vector2(0.655f, 0.93f)), // under the size bar
            };
            if (rt.anchorMin == a.min && rt.anchorMax == a.max) return;
            rt.anchorMin = a.min;
            rt.anchorMax = a.max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static (GameObject band, RectTransform textRt, TextMeshProUGUI text) CreateTicker(Canvas canvas, TMP_Text fontSource)
        {
            var band = new GameObject("RealityCheck_Ticker", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var rt = (RectTransform)band.transform;
            rt.SetParent(canvas.transform, false);
            rt.SetAsLastSibling();
            rt.anchorMin = new Vector2(0.28f, 0.175f);
            rt.anchorMax = new Vector2(0.72f, 0.212f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = band.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0.35f);
            img.raycastTarget = false;

            var go = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)go.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(0, 1);
            trt.pivot = new Vector2(0, 0.5f);
            trt.sizeDelta = new Vector2(4000, 0);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (fontSource != null)
            {
                text.font = fontSource.font;
                text.fontSharedMaterial = fontSource.fontSharedMaterial;
            }
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = 24;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.richText = true;
            text.raycastTarget = false;
            text.color = Color.white;
            band.SetActive(false);
            return (band, trt, text);
        }
    }

    // The plugin's own GameObject does not survive scene changes in this game, so the panels
    // are driven from the game's own Update methods instead.
    [HarmonyPatch(typeof(PauseMenu), "Update")]
    internal static class PauseMenuTick
    {
        private static bool failed;

        [HarmonyPostfix]
        private static void Postfix(PauseMenu __instance)
        {
            if (failed) return;
            if (__instance.parentGameObject == null || !__instance.parentGameObject.activeInHierarchy) { PausePanel.Hide(); return; }
            try { PausePanel.Tick(__instance); }
            catch (Exception e) { failed = true; Plugin.Log.LogError("Pause panel disabled: " + e); }
        }
    }

    [HarmonyPatch(typeof(GameUI), nameof(GameUI.Update))]
    internal static class GameUiTick
    {
        // Each part fails on its own; a part is switched off only after repeated errors.
        private static readonly int[] errors = new int[3];
        private const int MaxErrors = 5;

        [HarmonyPostfix]
        private static void Postfix(GameUI __instance)
        {
            Run(0, "HUD panel", () => HudPanel.Tick(__instance));
            Run(1, "Feel panel", () => FeelPanel.Tick(__instance));
            Run(2, "Sweat sampling", EnvInfo.SampleSweat);
        }

        private static void Run(int i, string name, Action a)
        {
            if (errors[i] >= MaxErrors) return;
            try { a(); }
            catch (Exception e)
            {
                if (++errors[i] >= MaxErrors) Plugin.Log.LogError(name + " disabled: " + e);
                else Plugin.Log.LogWarning(name + ": " + e.Message);
            }
        }
    }
}
