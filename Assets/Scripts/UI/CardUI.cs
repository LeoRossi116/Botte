using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardData cardData;
        public HeroState owner;

        private bool isPlayer1;
        private BattleUI battleUI;

        private Image borderImage;
        private Color normalColor = new Color32(0xf5, 0xa6, 0x23, 0xff); // Amber border (spells)
        private Color itemColor = new Color32(0x3a, 0x9e, 0xd0, 0xff);   // Blue-ish border (items)
        private Color equipColor = new Color32(0x9b, 0x59, 0xb6, 0xff);  // Purple border (equipment)
        private Color hoverColor = Color.white;                          // White outline on hover
        private Color activeColor = new Color32(0x2e, 0xcc, 0x71, 0xff); // Green border when aura active / used
        private bool isActiveOrUsed;
        private bool isItem;
        private bool isEquip;

        private void Awake()
        {
            borderImage = GetComponent<Image>();
        }

        public void Setup(CardData data, HeroState hero, bool player1, BattleUI ui, string stateLabel)
        {
            cardData = data;
            owner = hero;
            isPlayer1 = player1;
            battleUI = ui;
            isActiveOrUsed = !string.IsNullOrEmpty(stateLabel);
            isItem = data is ItemData;
            isEquip = data is EquipmentData;

            TMP_Text label = GetComponentInChildren<TMP_Text>();
            if (label != null && data != null)
            {
                if (data is EquipmentData eq)
                {
                    string stat = eq.damageValue > 0 ? $"Dmg {eq.damageValue}" : (eq.defenseValue > 0 ? $"Def {eq.defenseValue}" : eq.equipType.ToString());
                    label.text = $"{data.cardName}\n({stat})";
                }
                else
                {
                    string suffix = isActiveOrUsed ? $"\n<{stateLabel}>" : "";
                    label.text = $"{data.cardName}\n(M:{data.manaCost} S:{data.staminaCost}){suffix}";
                }
            }

            if (borderImage != null)
                borderImage.color = BaseColor();
        }

        private Color BaseColor()
        {
            if (isActiveOrUsed) return activeColor;
            if (isEquip) return equipColor;
            if (isItem) return itemColor;
            return normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (borderImage != null) borderImage.color = hoverColor;
            if (battleUI != null && cardData != null) battleUI.ShowCardDescription(isPlayer1, cardData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (borderImage != null) borderImage.color = BaseColor();
            if (battleUI != null) battleUI.HideCardDescription(isPlayer1);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var bm = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            if (bm == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right click discards the card (during the owner's turn).
                bm.OnCardRightClicked(owner, cardData);
                return;
            }

            if (cardData is MagicData spell) bm.OnCardClicked(owner, spell);
            else if (cardData is ItemData item) bm.OnItemClicked(owner, item);
            else if (cardData is EquipmentData equip) bm.OnEquipmentClicked(owner, equip);
        }
    }
}
