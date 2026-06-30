using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public MagicData cardData;
        public HeroState owner;

        private bool isPlayer1;
        private BattleUI battleUI;

        private Image borderImage;
        private Color normalColor = new Color32(0xf5, 0xa6, 0x23, 0xff); // Amber border
        private Color hoverColor = Color.white;                          // White outline on hover
        private Color activeColor = new Color32(0x2e, 0xcc, 0x71, 0xff); // Green border when aura active
        private bool isActiveOrUsed;

        private void Awake()
        {
            borderImage = GetComponent<Image>();
        }

        public void Setup(MagicData data, HeroState hero, bool player1, BattleUI ui, string stateLabel)
        {
            cardData = data;
            owner = hero;
            isPlayer1 = player1;
            battleUI = ui;
            isActiveOrUsed = !string.IsNullOrEmpty(stateLabel);

            TMP_Text label = GetComponentInChildren<TMP_Text>();
            if (label != null && data != null)
            {
                string suffix = isActiveOrUsed ? $"\n<{stateLabel}>" : "";
                label.text = $"{data.cardName}\n(M:{data.manaCost} S:{data.staminaCost}){suffix}";
            }

            if (borderImage != null)
            {
                borderImage.color = isActiveOrUsed ? activeColor : normalColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (borderImage != null) borderImage.color = hoverColor;
            if (battleUI != null && cardData != null) battleUI.ShowCardDescription(isPlayer1, cardData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (borderImage != null) borderImage.color = isActiveOrUsed ? activeColor : normalColor;
            if (battleUI != null) battleUI.HideCardDescription(isPlayer1);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var bm = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            if (bm != null) bm.OnCardClicked(owner, cardData);
        }
    }
}
