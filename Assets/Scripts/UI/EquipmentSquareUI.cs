using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    /// <summary>
    /// A single card-style square inside the equipment modal window. Shows the equipped item's
    /// card artwork as the square background (stretched to fill), the item name, and the
    /// attack/defense stats overlaid on top of the card. Empty slots show a placeholder label.
    /// Right-clicking a filled square unequips it (during the owner's turn), matching the old
    /// equip-slot behaviour.
    /// </summary>
    public class EquipmentSquareUI : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Fills the square with the card artwork (stretched).")]
        public Image cardImage;
        [Tooltip("Overlay showing the attack/defense stats on top of the card.")]
        public TMP_Text statsText;
        [Tooltip("Item name (or the empty-slot placeholder).")]
        public TMP_Text nameText;

        // Background color used when the slot is empty (dark blue, matches the card placeholder).
        private static readonly Color EmptyColor = new Color32(0x16, 0x21, 0x3e, 0xff);

        [HideInInspector] public bool isPlayer1;
        [HideInInspector] public EquipmentSlot equipmentSlot;
        [HideInInspector] public EquipmentData current;

        /// <summary>Populates the square with an equipped item (or clears it when eq is null).</summary>
        public void SetItem(EquipmentData eq, EquipmentSlot slot, bool player1)
        {
            equipmentSlot = slot;
            current = eq;
            isPlayer1 = player1;

            bool has = eq != null;

            if (cardImage != null)
            {
                cardImage.enabled = true;
                if (has && eq.cardTexture != null)
                {
                    cardImage.sprite = eq.cardTexture;
                    cardImage.color = Color.white;
                }
                else
                {
                    cardImage.sprite = null;
                    cardImage.color = EmptyColor;
                }
                cardImage.preserveAspect = false; // stretch to fill the whole square
            }

            bool hasTexture = has && eq.cardTexture != null;
            if (nameText != null)
            {
                nameText.text = has ? Loc.CardName(eq.cardName) : Loc.T(SlotPlaceholder(slot));
                // The card artwork already has the item name baked into it, so only show our own
                // name label as a fallback (art-less items and empty slots) to avoid a duplicate.
                nameText.gameObject.SetActive(!hasTexture);
            }

            if (statsText != null)
                statsText.text = has ? StatBadge(eq) : "";
        }

        // Attack/defense badge shown on top of the card. Shows whichever stats the piece has.
        private static string StatBadge(EquipmentData eq)
        {
            string s = "";
            if (eq.damageValue > 0) s += $"{Loc.T("Danno")}: {eq.damageValue}";
            if (eq.defenseValue > 0) s += (s.Length > 0 ? "   " : "") + $"{Loc.T("Difesa")}: {eq.defenseValue}";
            return s;
        }

        private static string SlotPlaceholder(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "Testa";
                case EquipmentSlot.Torso: return "Torso";
                case EquipmentSlot.Hands: return "Mani";
                case EquipmentSlot.Feet: return "Piedi";
                case EquipmentSlot.WeaponMain: return "Arma principale";
                case EquipmentSlot.WeaponOff: return "Arma secondaria";
            }
            return "";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            if (current == null) return;
            var bm = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            if (bm != null) bm.OnEquipSlotRightClicked(isPlayer1, equipmentSlot);
        }
    }
}
