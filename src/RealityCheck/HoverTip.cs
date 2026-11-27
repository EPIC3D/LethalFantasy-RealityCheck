using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealityCheck
{
    /// <summary>
    /// Mouse-over tooltip for the pause panel: lines carry a TMP link id, the extra text
    /// (comparisons, notes) is shown in a small box next to the cursor.
    /// </summary>
    internal sealed class HoverTip : MonoBehaviour
    {
        public TextMeshProUGUI source;
        public static readonly List<string> Hints = new List<string>();

        private RectTransform box;
        private TextMeshProUGUI label;
        private Canvas canvas;
        private int shown = -1;

        public static HoverTip Attach(TextMeshProUGUI source, Canvas canvas)
        {
            var tip = source.gameObject.AddComponent<HoverTip>();
            tip.source = source;
            tip.canvas = canvas;

            var go = new GameObject("RealityCheck_Tooltip", typeof(RectTransform), typeof(Image));
            tip.box = (RectTransform)go.transform;
            tip.box.SetParent(canvas.transform, false);
            tip.box.pivot = new Vector2(0, 1);
            var img = go.GetComponent<Image>();
            Ui.Round(img);
            img.color = new Color(0f, 0.035f, 0.07f, 0.97f);
            img.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.8f, 0.82f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(tip.box, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 6);
            trt.offsetMax = new Vector2(-10, -6);
            tip.label = textGo.AddComponent<TextMeshProUGUI>();
            tip.label.font = source.font;
            tip.label.fontSharedMaterial = source.fontSharedMaterial;
            tip.label.fontSize = 20;
            tip.label.color = new Color(0.3f, 0.7f, 1f);
            tip.label.richText = true;
            tip.label.raycastTarget = false;
            go.SetActive(false);
            return tip;
        }

        private void Update()
        {
            if (source == null || box == null) return;
            if (!source.gameObject.activeInHierarchy) { Hide(); return; }
            var cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 mouse = Input.mousePosition;
            int link = TMP_TextUtilities.FindIntersectingLink(source, mouse, cam);
            int id = -1;
            if (link >= 0 && int.TryParse(source.textInfo.linkInfo[link].GetLinkID(), out int parsed)
                && parsed >= 0 && parsed < Hints.Count && !string.IsNullOrEmpty(Hints[parsed]))
                id = parsed;
            if (id < 0) { Hide(); return; }

            if (id != shown)
            {
                shown = id;
                label.text = Hints[id];
                Vector2 size = label.GetPreferredValues(Hints[id], 520, 0);
                box.sizeDelta = new Vector2(Mathf.Min(size.x, 520) + 20, size.y + 12);
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, mouse, cam, out var local);
            // Keep the box on screen: flip it to the left of / above the cursor near the right / bottom edge.
            var area = ((RectTransform)canvas.transform).rect;
            Vector2 pos = local + new Vector2(18, -18), boxSize = box.sizeDelta;
            if (pos.x + boxSize.x > area.xMax - 8) pos.x = local.x - 18 - boxSize.x;
            if (pos.y - boxSize.y < area.yMin + 8) pos.y = local.y + 18 + boxSize.y;
            pos.x = Mathf.Max(area.xMin + 8, pos.x);
            pos.y = Mathf.Min(area.yMax - 8, pos.y);
            box.localPosition = pos;
            box.SetAsLastSibling();
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
        }

        private void Hide()
        {
            shown = -1;
            if (box != null && box.gameObject.activeSelf) box.gameObject.SetActive(false);
        }

        private void OnDisable() => Hide();
    }
}
