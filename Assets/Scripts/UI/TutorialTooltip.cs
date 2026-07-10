using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    /// <summary>
    /// A single reusable tooltip window that is positioned just above a target button.
    /// The root stays active so <see cref="Instance"/> is always available; only the
    /// visible <see cref="box"/> is toggled.
    /// </summary>
    public class TutorialTooltip : MonoBehaviour
    {
        public static TutorialTooltip Instance { get; private set; }

        [Tooltip("The visible tooltip window (background + text). Toggled by ShowFor/Hide.")]
        [SerializeField] private RectTransform box;
        [SerializeField] private TMP_Text label;
        [Tooltip("The Canvas this tooltip lives under. Auto-resolved if left empty.")]
        [SerializeField] private Canvas canvas;
        [Tooltip("Vertical gap (in canvas units) between the button top and the tooltip bottom.")]
        [SerializeField] private float gap = 10f;

        private void Awake()
        {
            Instance = this;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Shows the tooltip with the given text, centered above <paramref name="target"/>.</summary>
        public void ShowFor(RectTransform target, string text)
        {
            if (box == null || label == null || target == null) return;

            label.text = text;
            box.gameObject.SetActive(true);
            transform.SetAsLastSibling();

            // Make sure the box has its final size before we position it.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(box);

            RectTransform parent = box.parent as RectTransform;
            if (parent == null) return;

            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners); // 0=BL, 1=TL, 2=TR, 3=BR
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, topCenter);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, cam, out Vector2 local);

            // Pivot at bottom-center so the box sits ON TOP of the button.
            box.pivot = new Vector2(0.5f, 0f);
            box.anchoredPosition = local + new Vector2(0f, gap);
        }

        /// <summary>Hides the tooltip.</summary>
        public void Hide()
        {
            if (box != null) box.gameObject.SetActive(false);
        }
    }
}
