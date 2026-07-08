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
        private Image backgroundImage;                                   // Inner "Background" child image
        private Color backgroundBaseColor;                               // Default (placeholder) background color
        private TMP_Text cardLabel;                                      // Text label child
        private Color normalColor = new Color32(0xf5, 0xa6, 0x23, 0xff); // Amber border (spells)
        private Color itemColor = new Color32(0x3a, 0x9e, 0xd0, 0xff);   // Blue-ish border (items)
        private Color equipColor = new Color32(0x9b, 0x59, 0xb6, 0xff);  // Purple border (equipment)
        private Color hoverColor = Color.white;                          // White outline on hover
        private Color activeColor = new Color32(0x2e, 0xcc, 0x71, 0xff); // Green border when aura active / used
        private bool isActiveOrUsed;
        private bool isItem;
        private bool isEquip;

        // Bring-to-front on hover uses an override-sorting sub-canvas so the card renders
        // above its siblings without changing its order inside the hand layout group.
        private const int HoverSortingOrder = 100;

        private void Awake()
        {
            borderImage = GetComponent<Image>();

            Transform bg = transform.Find("Background");
            if (bg != null)
            {
                backgroundImage = bg.GetComponent<Image>();
                if (backgroundImage != null) backgroundBaseColor = backgroundImage.color;
            }

            cardLabel = GetComponentInChildren<TMP_Text>();
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

            TMP_Text label = cardLabel;
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

            // When the card has custom artwork, draw it in place of the placeholder background
            // and label; otherwise fall back to the default placeholder look.
            bool hasTexture = data != null && data.cardTexture != null;
            if (backgroundImage != null)
            {
                if (hasTexture)
                {
                    backgroundImage.sprite = data.cardTexture;
                    backgroundImage.color = Color.white;
                    backgroundImage.preserveAspect = true;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color = backgroundBaseColor;
                }
            }
            if (label != null) label.gameObject.SetActive(!hasTexture);

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
            SetOnTop(true);
            if (battleUI != null && cardData != null) battleUI.ShowCardDescription(isPlayer1, cardData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (borderImage != null) borderImage.color = BaseColor();
            SetOnTop(false);
            if (battleUI != null) battleUI.HideCardDescription(isPlayer1);
        }

        // Renders this card above its siblings while hovered, without reordering it inside the
        // hand's layout group. A sub-canvas with overrideSorting draws it on top; a
        // GraphicRaycaster keeps the card (and its children) receiving pointer events.
        private void SetOnTop(bool onTop)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (onTop)
            {
                if (canvas == null)
                {
                    canvas = gameObject.AddComponent<Canvas>();
                    gameObject.AddComponent<GraphicRaycaster>();
                }
                canvas.overrideSorting = true;
                canvas.sortingOrder = HoverSortingOrder;
            }
            else if (canvas != null)
            {
                canvas.overrideSorting = false;
                canvas.sortingOrder = 0;
            }
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
