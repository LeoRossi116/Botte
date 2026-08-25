using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    /// <summary>
    /// Manages a vertical stack of floating damage indicators above a single hero portrait.
    /// Newest indicator appears on top; the oldest (bottom) expires first, after which the
    /// remaining indicators glide down to close the gap. Indicators never overlap.
    /// </summary>
    public class DamageIndicatorStack : MonoBehaviour
    {
        private RectTransform overlay;   // full-screen parent (same canvas as the portrait)
        private RectTransform target;    // hero portrait to float above
        private Canvas canvas;

        private const float Lifetime = 2f;     // seconds each indicator stays fully alive
        private const float FadeTime = 0.5f;   // trailing fade-out window
        private const float ItemW = 100f;
        private const float ItemH = 26f;
        private const float Gap = 4f;
        private const float BaseOffset = 12f;  // gap between portrait top and first indicator
        private const float Slide = 14f;       // lerp speed for down-shift animation

        private static readonly Color DamageColor = new Color32(0xE9, 0x45, 0x60, 0xff); // red
        private static readonly Color BlockColor = new Color32(0x3A, 0x9E, 0xD0, 0xff);  // blue

        private class Item
        {
            public RectTransform rt;
            public CanvasGroup cg;
            public float bornTime;
        }

        private readonly List<Item> items = new List<Item>();

        public void Init(RectTransform overlayParent, RectTransform portrait, Canvas rootCanvas)
        {
            overlay = overlayParent;
            target = portrait;
            canvas = rootCanvas;
        }

        public void Show(int amount, bool blocked)
        {
            if (overlay == null || target == null) return;

            var go = new GameObject("DamageIndicator", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(overlay, false);
            rt.sizeDelta = new Vector2(ItemW, ItemH);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Dark rounded pill background for readability over any portrait.
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var textGO = new GameObject("Text", typeof(RectTransform));
            var trt = textGO.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var txt = textGO.AddComponent<TextMeshProUGUI>();
            txt.raycastTarget = false;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableAutoSizing = false;
            txt.fontSize = 16f;
            txt.fontStyle = FontStyles.Bold;
            txt.enableWordWrapping = false;
            txt.overflowMode = TextOverflowModes.Overflow;

            if (blocked)
            {
                txt.text = Loc.T("Danno bloccato");
                txt.color = BlockColor;
            }
            else
            {
                txt.text = $"-{amount} HP";
                txt.color = DamageColor;
            }

            var item = new Item { rt = rt, cg = cg, bornTime = Time.unscaledTime };
            items.Add(item);

            // Place it immediately at its final (top) slot so it does not slide up from below.
            rt.anchoredPosition = ComputeSlot(items.Count - 1);
        }

        private Vector2 ComputeSlot(int index)
        {
            Vector2 anchorLocal = GetPortraitTopLocal();
            float y = BaseOffset + ItemH * 0.5f + index * (ItemH + Gap);
            return new Vector2(anchorLocal.x, anchorLocal.y + y);
        }

        // Portrait top-center converted into the overlay's local space (handles portrait flip,
        // canvas scaling, and both Overlay / Camera render modes).
        private Vector2 GetPortraitTopLocal()
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners); // 0=BL,1=TL,2=TR,3=BR
            Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;

            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, topCenterWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, cam, out Vector2 local);
            return local;
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            // Remove expired (oldest first, but iterate safely).
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (now - items[i].bornTime >= Lifetime)
                {
                    if (items[i].rt != null) Destroy(items[i].rt.gameObject);
                    items.RemoveAt(i);
                }
            }

            // Re-layout: index 0 = oldest at the bottom, newest on top. Glide toward slots.
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.rt == null) continue;

                Vector2 targetPos = ComputeSlot(i);
                it.rt.anchoredPosition = Vector2.Lerp(it.rt.anchoredPosition, targetPos,
                    1f - Mathf.Exp(-Slide * Time.unscaledDeltaTime));

                float age = now - it.bornTime;
                float fadeStart = Lifetime - FadeTime;
                it.cg.alpha = age <= fadeStart ? 1f : Mathf.Clamp01(1f - (age - fadeStart) / FadeTime);
            }
        }
    }
}
