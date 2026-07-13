using UnityEngine;
using UnityEngine.EventSystems;
using Botte.UI;
using TMPro;

namespace Botte.UI
{
    /// <summary>
    /// Added to character select buttons to show hero stats in a pop-up tooltip when hovered.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HeroCardTooltipHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string tooltipText;
        private RectTransform _rect;

        private void Awake()
        {
            _rect = transform as RectTransform;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TutorialTooltip.Instance != null && !string.IsNullOrEmpty(tooltipText))
            {
                TutorialTooltip.Instance.ShowFor(_rect, tooltipText);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TutorialTooltip.Instance != null)
            {
                TutorialTooltip.Instance.Hide();
            }
        }

        private void OnDisable()
        {
            if (TutorialTooltip.Instance != null)
            {
                TutorialTooltip.Instance.Hide();
            }
        }
    }
}
