using UnityEngine;
using UnityEngine.EventSystems;

namespace Botte.UI
{
    /// <summary>
    /// Added to the middle-panel action buttons. When the Tutorial option is enabled and the
    /// pointer hovers the button, shows a small explanatory window above it via
    /// <see cref="TutorialTooltip"/>. Does nothing when the Tutorial option is disabled.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TutorialButtonHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum HintKind { Equip, EndTurn, Attack, Sleep, DrawExtra, Custom }

        [SerializeField] private HintKind kind = HintKind.Custom;
        [Tooltip("Used only when kind is Custom.")]
        [TextArea]
        [SerializeField] private string customText = "";

        private RectTransform _rect;
        private Botte.Core.BattleManager _battleManager;

        private void Awake()
        {
            _rect = transform as RectTransform;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!GameSettings.TutorialEnabled) return;
            if (TutorialTooltip.Instance == null) return;
            TutorialTooltip.Instance.ShowFor(_rect, ResolveText());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TutorialTooltip.Instance != null) TutorialTooltip.Instance.Hide();
        }

        private string ResolveText()
        {
            switch (kind)
            {
                case HintKind.Equip:
                    return "Utilizza carta equipaggiamento, fino a 2 volte per turno";
                case HintKind.Attack:
                    return "Infliggi danno all'Avversario";
                case HintKind.Sleep:
                    return "+2hp +1Mana +1Stamina";
                case HintKind.DrawExtra:
                    return "Pesca una carta da un mazzo a scelta";
                case HintKind.EndTurn:
                    if (_battleManager == null)
                        _battleManager = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
                    string dynamic = _battleManager != null ? _battleManager.GetEndTurnTutorialText() : null;
                    return string.IsNullOrEmpty(dynamic) ? "Passa alla fase successiva" : dynamic;
                default:
                    return customText;
            }
        }
    }
}
