using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace Botte.UI
{
    // A single equipped-slot square. Shows the equipped item's name/durability and
    // lets the player inspect its full stats on hover.
    public class EquipSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public bool isPlayer1;
        public BattleUI battleUI;
        public TMP_Text label;
        public string placeholder = "";
        public CardData current;

        // The specific physical EquipmentSlot index this slot UI represents.
        public EquipmentSlot equipmentSlot;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (current != null && battleUI != null) battleUI.ShowCardDescription(isPlayer1, current);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (battleUI != null) battleUI.HideCardDescription(isPlayer1);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                var bm = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
                if (bm != null && current != null)
                {
                    bm.OnEquipSlotRightClicked(isPlayer1, equipmentSlot);
                }
            }
        }
    }
}
