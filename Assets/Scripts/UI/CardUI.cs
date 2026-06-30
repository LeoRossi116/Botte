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
        
        private Image borderImage;
        private Color normalColor = new Color32(0xf5, 0xa6, 0x23, 0xff); // Amber
        private Color hoverColor = Color.white; // White outline/highlight
        
        private void Awake()
        {
            borderImage = GetComponent<Image>();
            if (borderImage != null)
            {
                borderImage.color = normalColor;
            }
        }
        
        public void Setup(MagicData data, HeroState hero)
        {
            cardData = data;
            owner = hero;
            
            TMP_Text label = GetComponentInChildren<TMP_Text>();
            if (label != null && data != null)
            {
                label.text = $"{data.cardName}\n(M:{data.manaCost} S:{data.staminaCost})";
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (borderImage != null)
            {
                borderImage.color = hoverColor;
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            if (borderImage != null)
            {
                borderImage.color = normalColor;
            }
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            var bm = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            if (bm != null)
            {
                bm.OnCardClicked(owner, cardData);
            }
        }
    }
}
