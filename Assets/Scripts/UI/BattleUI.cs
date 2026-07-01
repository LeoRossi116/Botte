using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Botte.UI
{
    public class BattleUI : MonoBehaviour
    {
        [Header("Player 1 UI")]
        public TMP_Text p1HeroNameText;
        public RectTransform p1HPBarFill;
        public TMP_Text p1HPText;
        public TMP_Text p1ManaText;
        public TMP_Text p1StaminaText;
        public TMP_Text p1StatusText;
        public RectTransform p1HandArea;

        [Header("Player 2 UI")]
        public TMP_Text p2HeroNameText;
        public RectTransform p2HPBarFill;
        public TMP_Text p2HPText;
        public TMP_Text p2ManaText;
        public TMP_Text p2StaminaText;
        public TMP_Text p2StatusText;
        public RectTransform p2HandArea;

        [Header("Center Panel")]
        public TMP_Text turnText;
        public TMP_Text phaseText;
        public RectTransform logContent;
        public ScrollRect logScrollRect;
        public GameObject winnerOverlay;
        public TMP_Text winnerText;

        [Header("Phase Containers")]
        public GameObject prepButtonsContainer;
        public GameObject combatButtonsContainer;
        public GameObject endButtonsContainer;

        [Header("Card Prefab")]
        public GameObject cardPrefab;

        [Header("Screens")]
        public GameObject characterSelectPanel;
        public GameObject battleScreen;
        public GameObject drawChoicePanel;

        [Header("Card Description Panels")]
        public GameObject p1DescPanel;
        public TMP_Text p1DescName;
        public TMP_Text p1DescCost;
        public TMP_Text p1DescEffect;
        public GameObject p2DescPanel;
        public TMP_Text p2DescName;
        public TMP_Text p2DescCost;
        public TMP_Text p2DescEffect;

        [Header("Character Select Buttons (Warrior, Mage, Rogue, Necro)")]
        public Button[] p1ClassButtons;
        public Button[] p2ClassButtons;

        [Header("Book Selectors (Spell, Equipment, Item)")]
        public Button[] p1BookButtons;
        public Button[] p2BookButtons;
        public TMP_Text p1BookLabel;
        public TMP_Text p2BookLabel;

        [Header("Peek Panel (manipolazione)")]
        public GameObject peekPanel;
        public TMP_Text peekText;

        [Header("Equipment Window (Helmet, Torso, Gloves, Boots, WeaponMain, WeaponOff)")]
        public EquipSlotUI[] p1EquipSlots;
        public EquipSlotUI[] p2EquipSlots;
        public GameObject p1WeaponConnector;
        public GameObject p2WeaponConnector;

        // Currently displayed book per player.
        public BookType p1SelectedBook = BookType.Spell;
        public BookType p2SelectedBook = BookType.Spell;

        public void RefreshHero(HeroState hero, bool isPlayer1)
        {
            TMP_Text nameText = isPlayer1 ? p1HeroNameText : p2HeroNameText;
            RectTransform hpFill = isPlayer1 ? p1HPBarFill : p2HPBarFill;
            TMP_Text hpText = isPlayer1 ? p1HPText : p2HPText;
            TMP_Text manaText = isPlayer1 ? p1ManaText : p2ManaText;
            TMP_Text staminaText = isPlayer1 ? p1StaminaText : p2StaminaText;
            TMP_Text statusText = isPlayer1 ? p1StatusText : p2StatusText;

            nameText.text = hero.data.heroName;
            hpText.text = $"HP: {hero.currentHP} / {hero.data.maxHP}";
            manaText.text = $"Mana: {hero.currentMana} / {hero.GetModifiedIntelligence()}";
            staminaText.text = $"Stamina: {hero.currentStamina} / {hero.GetModifiedAgility()}";

            float hpPct = hero.data.maxHP > 0 ? (float)hero.currentHP / hero.data.maxHP : 0f;
            hpFill.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1f);

            string statusStr = "";
            if (hero.poisonStacks > 0) statusStr += $"Veleno({hero.poisonStacks}) ";
            if (hero.shieldAmount > 0) statusStr += $"Scudo({hero.shieldAmount}) ";
            if (hero.hasShield) statusStr += "Blocco ";
            if (hero.nextAttackUnblockable) statusStr += "Perforante ";
            if (hero.auraBlockFirstAttack) statusStr += "Riflessi ";
            if (hero.auraWeakenOpponent > 0) statusStr += $"Indebol(-{hero.auraWeakenOpponent}) ";
            statusText.text = statusStr.Trim();
        }

        // Displays the contents of the currently selected book for the given player.
        public void RefreshBook(HeroState hero, bool isPlayer1)
        {
            Transform area = isPlayer1 ? p1HandArea : p2HandArea;
            if (area == null || cardPrefab == null) return;

            for (int i = area.childCount - 1; i >= 0; i--)
            {
                Transform child = area.GetChild(i);
                child.SetParent(null, false); // detach so childCount is correct this frame
                Destroy(child.gameObject);
            }

            BookType book = isPlayer1 ? p1SelectedBook : p2SelectedBook;
            UpdateBookSelectorVisuals(isPlayer1);

            if (book == BookType.Spell)
            {
                foreach (CardData card in hero.hand)
                {
                    if (card is MagicData spell)
                    {
                        string state = "";
                        if (spell.magicType == MagicType.Aura && hero.activeAuras.Contains(spell)) state = "ATTIVA";
                        else if (spell.magicType == MagicType.Exhaustion && hero.exhaustedThisRound.Contains(spell)) state = "USATA";
                        SpawnCard(area, spell, hero, isPlayer1, state);
                    }
                }
            }
            else if (book == BookType.Item)
            {
                foreach (CardData card in hero.itemBook)
                    SpawnCard(area, card, hero, isPlayer1, "");
            }
            else if (book == BookType.Equipment)
            {
                foreach (CardData card in hero.equipmentBook)
                    SpawnCard(area, card, hero, isPlayer1, "");
            }
        }

        private void SpawnCard(Transform area, CardData card, HeroState hero, bool isPlayer1, string state)
        {
            GameObject cardGO = Instantiate(cardPrefab, area);
            CardUI cardUI = cardGO.GetComponent<CardUI>();
            if (cardUI == null) cardUI = cardGO.AddComponent<CardUI>();
            cardUI.Setup(card, hero, isPlayer1, this, state);
        }

        // Backwards-compatible alias still used by some flows.
        public void RefreshHand(HeroState hero, bool isPlayer1) => RefreshBook(hero, isPlayer1);

        private static readonly EquipmentSlot[] EquipOrder =
        {
            EquipmentSlot.Head, EquipmentSlot.Torso, EquipmentSlot.Hands,
            EquipmentSlot.Feet, EquipmentSlot.WeaponMain, EquipmentSlot.WeaponOff
        };

        // Updates the equipped-slot squares (name + durability) and the 2-hand connector.
        public void RefreshEquipment(HeroState hero, bool isPlayer1)
        {
            EquipSlotUI[] slots = isPlayer1 ? p1EquipSlots : p2EquipSlots;
            GameObject connector = isPlayer1 ? p1WeaponConnector : p2WeaponConnector;
            if (slots == null) return;

            for (int i = 0; i < slots.Length && i < EquipOrder.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                EquipmentSlot es = EquipOrder[i];
                EquipmentData eq = hero.equippedItems[(int)es];

                // Off-hand square reflects a two-handed weapon occupying both hands.
                if (es == EquipmentSlot.WeaponOff && hero.weaponTwoHandedEquipped)
                {
                    slot.current = hero.MainWeapon;
                    if (slot.label != null) slot.label.text = "(2 mani)";
                    continue;
                }

                slot.current = eq;
                if (slot.label == null) continue;
                if (eq != null)
                {
                    string dur = (eq.maxDurability > 0 && hero.durability.ContainsKey(es))
                        ? $"\n[{hero.durability[es]}/{eq.maxDurability}]" : "";
                    slot.label.text = ShortName(eq.cardName) + dur;
                }
                else
                {
                    slot.label.text = slot.placeholder;
                }
            }

            if (connector != null) connector.SetActive(hero.weaponTwoHandedEquipped);
        }

        private string ShortName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            return n.Length <= 12 ? n : n.Substring(0, 11) + "…";
        }

        public void SetSelectedBook(bool isPlayer1, BookType book)
        {
            if (isPlayer1) p1SelectedBook = book; else p2SelectedBook = book;
            TMP_Text label = isPlayer1 ? p1BookLabel : p2BookLabel;
            if (label != null) label.text = BookName(book);
            UpdateBookSelectorVisuals(isPlayer1);
        }

        private string BookName(BookType b)
        {
            switch (b)
            {
                case BookType.Spell: return "Libro Incantesimi";
                case BookType.Equipment: return "Libro Equipaggiamento";
                case BookType.Item: return "Libro Oggetti";
            }
            return "";
        }

        private void UpdateBookSelectorVisuals(bool isPlayer1)
        {
            Button[] buttons = isPlayer1 ? p1BookButtons : p2BookButtons;
            BookType selected = isPlayer1 ? p1SelectedBook : p2SelectedBook;
            if (buttons == null) return;
            Color normal = new Color32(0x16, 0x21, 0x3e, 0xff);
            Color picked = new Color32(0xf5, 0xa6, 0x23, 0xff);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var img = buttons[i].GetComponent<Image>();
                if (img != null) img.color = ((int)selected == i) ? picked : normal;
            }
        }

        public void ShowCardDescription(bool isPlayer1, CardData card)
        {
            GameObject panel = isPlayer1 ? p1DescPanel : p2DescPanel;
            TMP_Text nameT = isPlayer1 ? p1DescName : p2DescName;
            TMP_Text costT = isPlayer1 ? p1DescCost : p2DescCost;
            TMP_Text effectT = isPlayer1 ? p1DescEffect : p2DescEffect;
            if (panel == null || card == null) return;

            panel.SetActive(true);
            if (nameT != null) nameT.text = card.cardName;
            if (costT != null) costT.text = $"M:{card.manaCost}  S:{card.staminaCost}";
            if (effectT != null)
            {
                if (card is EquipmentData eq)
                {
                    string stats = $"<i>{eq.equipType} · {eq.slotType}</i>\n";
                    if (eq.damageValue > 0) stats += $"Danno: {eq.damageValue}  ";
                    if (eq.defenseValue > 0) stats += $"Difesa: {eq.defenseValue}  ";
                    foreach (var m in eq.attributeMods)
                        stats += $"{(m.value >= 0 ? "+" : "")}{m.value} {m.attr}  ";
                    stats += "\n";
                    if (eq.maxDurability > 0) stats += $"Durabilità: {eq.maxDurability}\n";
                    if (!string.IsNullOrEmpty(eq.effectDescription)) stats += eq.effectDescription;
                    else if (eq.specialEffect != EquipEffect.None) stats += eq.specialEffect.ToString();
                    effectT.text = stats;
                }
                else
                {
                    string typeLine;
                    if (card is MagicData spell) typeLine = spell.magicType.ToString();
                    else if (card is ItemData item) typeLine = $"Oggetto · {item.category} · {item.target}";
                    else typeLine = card.cardType.ToString();
                    effectT.text = $"<i>{typeLine}</i>\n{card.effectDescription}";
                }
            }
        }

        public void HideCardDescription(bool isPlayer1)
        {
            GameObject panel = isPlayer1 ? p1DescPanel : p2DescPanel;
            if (panel != null) panel.SetActive(false);
        }

        public void ShowPeek(string cardName)
        {
            if (peekPanel == null) return;
            peekPanel.SetActive(true);
            if (peekText != null) peekText.text = $"Cima del mazzo:\n<b>{cardName}</b>\nTenere o scartare?";
        }

        public void HidePeek()
        {
            if (peekPanel != null) peekPanel.SetActive(false);
        }

        public void AddLog(string message)
        {
            Debug.Log(message);
            if (logContent == null) return;
            GameObject newTextGO = new GameObject("LogText");
            newTextGO.transform.SetParent(logContent, false);
            TextMeshProUGUI txt = newTextGO.AddComponent<TextMeshProUGUI>();
            txt.text = message;
            txt.fontSize = 16f;
            txt.color = Color.white;

            Canvas.ForceUpdateCanvases();
            if (logScrollRect != null) logScrollRect.verticalNormalizedPosition = 0f;
        }

        public void ClearLog()
        {
            if (logContent == null) return;
            foreach (Transform child in logContent) Destroy(child.gameObject);
        }

        public void ClearHands()
        {
            ClearChildren(p1HandArea);
            ClearChildren(p2HandArea);
            HideCardDescription(true);
            HideCardDescription(false);
        }

        private void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Transform child = t.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        // Highlights the chosen class button for each player on the character-select screen.
        public void UpdateSelectionHighlight(int player, int classIdx, HeroClass? sel1, HeroClass? sel2)
        {
            Color normal = new Color32(0xf5, 0xa6, 0x23, 0xff);
            Color picked = new Color32(0x2e, 0xcc, 0x71, 0xff);

            if (p1ClassButtons != null)
            {
                for (int i = 0; i < p1ClassButtons.Length; i++)
                {
                    if (p1ClassButtons[i] == null) continue;
                    var img = p1ClassButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (sel1.HasValue && (int)sel1.Value == i) ? picked : normal;
                }
            }
            if (p2ClassButtons != null)
            {
                for (int i = 0; i < p2ClassButtons.Length; i++)
                {
                    if (p2ClassButtons[i] == null) continue;
                    var img = p2ClassButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (sel2.HasValue && (int)sel2.Value == i) ? picked : normal;
                }
            }
        }
    }
}
