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

        [Header("Hero Visuals (Left = local hero, Right = opponent)")]
        public UnityEngine.UI.Image p1HeroImage;
        public UnityEngine.UI.Image p2HeroImage;
        public HeroSpriteAnimator p1HeroAnim;
        public HeroSpriteAnimator p2HeroAnim;

        [Tooltip("Hero portrait per class, indexed by HeroClass (0=Warrior, 1=Mage, 2=Rogue, 3=Necro). " +
                 "Assign these manually. Used when a HeroData has no explicit heroTexture. " +
                 "The correct portrait is chosen automatically based on the selected hero.")]
        public Sprite[] heroClassTextures = new Sprite[4];

        [Header("Opponent Cards (top panel)")]
        [Tooltip("Card-back art per hero class, indexed by HeroClass (0=Warrior, 1=Mage, 2=Rogue, 3=Necro). " +
                 "Assign these manually, exactly like the hero portraits above. The sprite shown on the " +
                 "opponent's card slots is chosen automatically based on the opponent's selected hero.")]
        public Sprite[] oppCardTextures = new Sprite[4];
        [Tooltip("The opponent card-slot Images in the top panel (OppCard_Slot_0/1/2). " +
                 "These always represent the opponent (they are not swapped for the client).")]
        public UnityEngine.UI.Image[] oppCardSlots;

        [Header("Stat Bar Fills (Mana/Stamina; HP fills declared above)")]
        public RectTransform p1ManaBarFill;
        public RectTransform p1StaminaBarFill;
        public RectTransform p2ManaBarFill;
        public RectTransform p2StaminaBarFill;

        [Header("Hero Hover Popups")]
        public GameObject p1HeroPopup;
        public TMP_Text p1HeroPopupText;
        public GameObject p2HeroPopup;
        public TMP_Text p2HeroPopupText;

        [Header("Scoreboard (top-center)")]
        public TMP_Text scoreTurnText;
        public TMP_Text scoreWhoseTurnText;
        public TMP_Text scoreTimerText;

        [Header("Shared Inspect Box (single, bottom-center)")]
        public GameObject inspectPanel;
        public TMP_Text inspectName;
        public TMP_Text inspectCost;
        public TMP_Text inspectEffect;

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
        public GameObject mainMenuPanel;
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
        public GameObject p1EquipWindow;
        public GameObject p2EquipWindow;
        public Button p1ShowEquipButton;
        public Button p2ShowEquipButton;

        // Whether the equipment slots are currently shown (per player). Hidden by default.
        public bool p1EquipVisible;
        public bool p2EquipVisible;

        // Desc panel layout: full width when slots hidden, narrow (right of slots) when shown.
        private static readonly Vector2 DescPosFull = new Vector2(0f, 0f);
        private static readonly Vector2 DescSizeFull = new Vector2(400f, 160f);
        private static readonly Vector2 DescPosRight = new Vector2(0f, 0f);
        private static readonly Vector2 DescSizeRight = new Vector2(400f, 160f);

        // Currently displayed book per player.
        public BookType p1SelectedBook = BookType.Spell;
        public BookType p2SelectedBook = BookType.Spell;

        private bool referencesMapped = false;

        public void MapUIReferences()
        {
            if (referencesMapped) return;

            if (RelayManager.IsMultiplayer && Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                // We are Client!
                // Swap all p1 and p2 references, because P2 is the local player (left side) and P1 is the remote player (right side).
                Swap(ref p1HeroNameText, ref p2HeroNameText);
                Swap(ref p1HPBarFill, ref p2HPBarFill);
                Swap(ref p1HPText, ref p2HPText);
                Swap(ref p1ManaText, ref p2ManaText);
                Swap(ref p1StaminaText, ref p2StaminaText);
                Swap(ref p1StatusText, ref p2StatusText);
                Swap(ref p1HandArea, ref p2HandArea);
                Swap(ref p1HeroImage, ref p2HeroImage);
                Swap(ref p1HeroAnim, ref p2HeroAnim);
                Swap(ref p1ManaBarFill, ref p2ManaBarFill);
                Swap(ref p1StaminaBarFill, ref p2StaminaBarFill);
                Swap(ref p1HeroPopup, ref p2HeroPopup);
                Swap(ref p1HeroPopupText, ref p2HeroPopupText);
                Swap(ref p1DescPanel, ref p2DescPanel);
                Swap(ref p1DescName, ref p2DescName);
                Swap(ref p1DescCost, ref p2DescCost);
                Swap(ref p1DescEffect, ref p2DescEffect);
                Swap(ref p1ClassButtons, ref p2ClassButtons);
                Swap(ref p1BookButtons, ref p2BookButtons);
                Swap(ref p1BookLabel, ref p2BookLabel);
                Swap(ref p1EquipSlots, ref p2EquipSlots);
                Swap(ref p1WeaponConnector, ref p2WeaponConnector);
                Swap(ref p1EquipWindow, ref p2EquipWindow);
                Swap(ref p1ShowEquipButton, ref p2ShowEquipButton);
            }
            referencesMapped = true;
        }

        private void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        // Resolves the display name to show for a battle side: the local player's nickname for
        // the LEFT side, the opponent's nickname for the RIGHT side. In multiplayer the names
        // come from RelayManager (replicated to both peers); otherwise it falls back to the
        // locally-typed nickname for the local side and a generic label for the opponent.
        private string ResolvePlayerName(bool localSide)
        {
            if (RelayManager.IsMultiplayer && RelayManager.Instance != null)
            {
                string n = localSide ? RelayManager.Instance.LocalPlayerName
                                     : RelayManager.Instance.OpponentPlayerName;
                if (!string.IsNullOrEmpty(n)) return n;
            }

            if (localSide)
                return string.IsNullOrEmpty(SceneUIManager.LocalNickname) ? "Giocatore 1" : SceneUIManager.LocalNickname;
            return "Avversario";
        }

        // True when the given player side is the LOCAL player (shown on the left):
        // player1 for the host / single-player, player2 for a connected client.
        public bool IsLocalPlayerSide(bool isPlayer1)
        {
            bool clientSide = RelayManager.IsMultiplayer
                && Unity.Netcode.NetworkManager.Singleton != null
                && !Unity.Netcode.NetworkManager.Singleton.IsServer;
            return isPlayer1 != clientSide;
        }

        // A hand-area reference points at the ScrollRect "Content" (Content < Viewport < HandArea).
        // Returns the HandArea root GameObject so it can be shown/hidden as a whole.
        private GameObject GetHandAreaRoot(RectTransform content)
        {
            if (content == null) return null;
            if (content.parent != null && content.parent.parent != null)
                return content.parent.parent.gameObject;
            return content.gameObject;
        }

        // Shows the end-of-match winner overlay, forcing it to the top of the draw/raycast
        // order so its buttons are visible and clickable (it is authored as the first sibling).
        public void ShowWinner(string text)
        {
            if (winnerOverlay == null) return;
            winnerOverlay.transform.SetAsLastSibling();
            winnerOverlay.SetActive(true);
            if (winnerText != null) winnerText.text = text;
        }

        // Color coding for each hero class.
        public static string GetClassColor(HeroClass hc)
        {
            switch (hc)
            {
                case HeroClass.Warrior: return "#E94560"; // RED
                case HeroClass.Rogue: return "#2980B9";   // BLUE
                case HeroClass.Mage: return "#30E3CA";    // LIGHT BLUE
                case HeroClass.Necro: return "#8A39E8";   // PURPLE
            }
            return "#FFFFFF";
        }

        public static string GetClassColorizedName(HeroClass hc, string name)
        {
            return $"<color={GetClassColor(hc)}>{name}</color>";
        }

        // Resolves the portrait to use for a hero: an explicit HeroData.heroTexture wins,
        // otherwise the per-class mapping is used so each chosen hero shows a different image.
        private Sprite GetHeroTexture(HeroData data)
        {
            if (data == null) return null;
            if (data.heroTexture != null) return data.heroTexture;
            int idx = (int)data.heroClass;
            if (heroClassTextures != null && idx >= 0 && idx < heroClassTextures.Length)
                return heroClassTextures[idx];
            return null;
        }

        // Resolves the card-back sprite to use for a given hero class (manual per-class mapping).
        private Sprite GetOppCardTexture(HeroData data)
        {
            if (data == null || oppCardTextures == null) return null;
            int idx = (int)data.heroClass;
            if (idx >= 0 && idx < oppCardTextures.Length) return oppCardTextures[idx];
            return null;
        }

        // Shows the opponent's card-back art on every top-panel card slot, chosen from the
        // opponent hero's class. Mirrors the manual-sprite approach used for hero portraits:
        // if no sprite is assigned for that class, the slot keeps its placeholder appearance.
        public void RefreshOpponentCards(HeroData oppData)
        {
            if (oppCardSlots == null) return;
            Sprite art = GetOppCardTexture(oppData);
            if (art == null) return; // graceful fallback: leave placeholder untouched

            foreach (var slot in oppCardSlots)
            {
                if (slot == null) continue;
                slot.sprite = art;
                slot.color = Color.white;
                slot.preserveAspect = true;

                // Hide the "Opp Card" placeholder label once real art is shown.
                var placeholder = slot.transform.Find("Text");
                if (placeholder != null) placeholder.gameObject.SetActive(false);
            }
        }

        public void RefreshHero(HeroState hero, bool isPlayer1)
        {
            MapUIReferences();
            TMP_Text nameText = isPlayer1 ? p1HeroNameText : p2HeroNameText;
            RectTransform hpFill = isPlayer1 ? p1HPBarFill : p2HPBarFill;
            TMP_Text hpText = isPlayer1 ? p1HPText : p2HPText;
            TMP_Text statusText = isPlayer1 ? p1StatusText : p2StatusText;
            RectTransform manaFill = isPlayer1 ? p1ManaBarFill : p2ManaBarFill;
            RectTransform staminaFill = isPlayer1 ? p1StaminaBarFill : p2StaminaBarFill;
            TMP_Text manaText = isPlayer1 ? p1ManaText : p2ManaText;
            TMP_Text staminaText = isPlayer1 ? p1StaminaText : p2StaminaText;

            var bm = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            bool isActive = bm != null && bm.IsHeroActive(hero);
            // Show the PLAYER's name (local player on the left, opponent on the right) instead
            // of the hero's name, keeping the hero-class color and the active-turn arrows.
            string playerName = ResolvePlayerName(IsLocalPlayerSide(isPlayer1));
            string styledName = GetClassColorizedName(hero.data.heroClass, playerName);
            nameText.text = isActive ? $"<b>▶ {styledName} ◀</b>" : styledName;

            // Show the portrait for the chosen hero. Prefer a HeroData-level texture, otherwise
            // fall back to the per-class mapping. The frame animator (when it has real art) owns
            // the image instead, so we skip static assignment in that case.
            UnityEngine.UI.Image heroImg = isPlayer1 ? p1HeroImage : p2HeroImage;
            HeroSpriteAnimator heroAnim = isPlayer1 ? p1HeroAnim : p2HeroAnim;

            // Face direction is decided by the PHYSICAL side, not the logical player
            // index: the local hero (always shown on the LEFT) faces right (+1) and the
            // opponent (always on the RIGHT) faces left (-1). Because the client swaps the
            // p1/p2 references, using IsLocalPlayerSide keeps both host and client correct.
            float faceX = IsLocalPlayerSide(isPlayer1) ? 1f : -1f;
            if (heroImg != null)
            {
                Vector3 scale = heroImg.transform.localScale;
                scale.x = faceX;
                heroImg.transform.localScale = scale;

                // Keep the equip toggle button pinned to the bottom-right of the sprite with
                // upright text, independent of the sprite's mirror. It is moved OUT of the
                // (mirrored) sprite onto the HeroPanel so it never inherits the horizontal flip.
                Button eqBtn = isPlayer1 ? p1ShowEquipButton : p2ShowEquipButton;
                RectTransform sprtRT = heroImg.transform as RectTransform;
                if (eqBtn != null && sprtRT != null && sprtRT.parent != null)
                {
                    var brt = eqBtn.GetComponent<RectTransform>();
                    Transform heroPanel = sprtRT.parent; // "HeroPanel"
                    if (brt.parent != heroPanel) brt.SetParent(heroPanel, false);
                    brt.localScale = Vector3.one;
                    brt.anchorMin = new Vector2(0.5f, 0.5f);
                    brt.anchorMax = new Vector2(0.5f, 0.5f);
                    brt.pivot = new Vector2(0.5f, 0.5f);
                    Vector2 half = sprtRT.sizeDelta * 0.5f;
                    brt.anchoredPosition = new Vector2(
                        sprtRT.anchoredPosition.x + half.x - 20f,
                        sprtRT.anchoredPosition.y - half.y + 22f);
                    brt.SetAsLastSibling();
                }
            }

            if (heroAnim != null && hero.data != null)
            {
                heroAnim.Setup(hero.data.heroClass.ToString());
            }

            if (heroImg != null && (heroAnim == null || !heroAnim.HasArt))
            {
                Sprite portrait = GetHeroTexture(hero.data);
                if (portrait != null)
                {
                    heroImg.sprite = portrait;
                    heroImg.color = Color.white;
                    heroImg.preserveAspect = true;
                }
            }

            hpText.text = $"HP: {hero.currentHP} / {hero.GetModifiedMaxHP()}";

            float hpPct = hero.data.maxHP > 0 ? (float)hero.currentHP / hero.data.maxHP : 0f;
            hpFill.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1f);

            // Mana (blue) and Stamina (beige) are shown as bars beneath the HP bar.
            int manaMax = hero.GetModifiedIntelligence();
            float manaPct = manaMax > 0 ? (float)hero.currentMana / manaMax : 0f;
            if (manaFill != null) manaFill.anchorMax = new Vector2(Mathf.Clamp01(manaPct), 1f);
            if (manaText != null) manaText.text = $"Mana: {hero.currentMana} / {manaMax}";

            int staminaMax = hero.GetModifiedAgility();
            float staminaPct = staminaMax > 0 ? (float)hero.currentStamina / staminaMax : 0f;
            if (staminaFill != null) staminaFill.anchorMax = new Vector2(Mathf.Clamp01(staminaPct), 1f);
            if (staminaText != null) staminaText.text = $"Stam: {hero.currentStamina} / {staminaMax}";

            // The top-panel card slots always show the OPPONENT (the non-local side). Uses the
            // same manual per-class sprite mechanism as the hero portraits.
            if (!IsLocalPlayerSide(isPlayer1))
                RefreshOpponentCards(hero.data);

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
            MapUIReferences();
            RectTransform area = isPlayer1 ? p1HandArea : p2HandArea;
            TMP_Text bookLabel = isPlayer1 ? p1BookLabel : p2BookLabel;

            // The single bottom panel only ever shows the LOCAL player's book. The two hand
            // areas overlap, so the remote player's area (and its label) are hidden entirely,
            // otherwise it would cover the local player's cards.
            bool local = IsLocalPlayerSide(isPlayer1);
            GameObject areaRoot = GetHandAreaRoot(area);
            if (areaRoot != null) areaRoot.SetActive(local);
            if (bookLabel != null) bookLabel.gameObject.SetActive(local);
            if (!local) return;

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

        // Enlarged slot metrics (used when the equipment window is shown).
        private const float SLOT_SIZE = 52f;
        private const float FUSED_WEAPON_WIDTH = 112f; // two columns (52 + 8 gap + 52)

        // Updates the equipped-slot squares (name + durability). A two-handed weapon fuses the
        // two weapon squares into one wider rectangle; this reverts when the weapon is removed.
        public void RefreshEquipment(HeroState hero, bool isPlayer1)
        {
            MapUIReferences();
            EquipSlotUI[] slots = isPlayer1 ? p1EquipSlots : p2EquipSlots;
            GameObject connector = isPlayer1 ? p1WeaponConnector : p2WeaponConnector;
            if (connector != null) connector.SetActive(false); // legacy orange connector no longer used
            if (slots == null) return;

            bool twoHanded = hero.weaponTwoHandedEquipped;

            for (int i = 0; i < slots.Length && i < EquipOrder.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                EquipmentSlot es = EquipOrder[i];
                slot.equipmentSlot = es; // Assign slot enum dynamically
                EquipmentData eq = hero.equippedItems[(int)es];

                // Off-hand square: hidden entirely when a two-handed weapon is equipped (fused).
                if (es == EquipmentSlot.WeaponOff)
                {
                    slot.gameObject.SetActive(!twoHanded);
                    slot.current = twoHanded ? null : eq;
                    if (!twoHanded && slot.label != null)
                        slot.label.text = eq != null ? SlotText(hero, es, eq) : slot.placeholder;
                    continue;
                }

                // Main weapon square: widen to a fused rectangle when two-handed, else normal.
                if (es == EquipmentSlot.WeaponMain)
                {
                    var rt = slot.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.sizeDelta = twoHanded ? new Vector2(FUSED_WEAPON_WIDTH, SLOT_SIZE)
                                                 : new Vector2(SLOT_SIZE, SLOT_SIZE);
                }

                slot.current = eq;
                if (slot.label == null) continue;
                slot.label.text = eq != null ? SlotText(hero, es, eq) : slot.placeholder;
            }

            // Live hero stats (reflect equipment modifiers) shown while the equip window is open.
            GameObject window = isPlayer1 ? p1EquipWindow : p2EquipWindow;
            if (window != null) UpdateStatsReadout(window, hero);
        }

        // Lazily creates/updates a small stat panel below the equipped slots showing the hero's
        // current (equipment-modified) Strength, Intelligence (mana) and Speed (stamina).
        private void UpdateStatsReadout(GameObject window, HeroState hero)
        {
            var winRT = window.GetComponent<RectTransform>();
            // Make sure the window is tall enough to contain the readout below the 3 slot rows.
            if (winRT != null && winRT.sizeDelta.y < 238f)
                winRT.sizeDelta = new Vector2(winRT.sizeDelta.x, 238f);

            Transform t = window.transform.Find("StatsReadout");
            TMP_Text txt;
            if (t == null)
            {
                var go = new GameObject("StatsReadout", typeof(RectTransform));
                go.transform.SetParent(window.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(2f, -176f);
                rt.sizeDelta = new Vector2(108f, 58f);
                txt = go.AddComponent<TextMeshProUGUI>();
                txt.fontSize = 14f;
                txt.color = Color.white;
                txt.alignment = TextAlignmentOptions.TopLeft;
                txt.textWrappingMode = TextWrappingModes.NoWrap;
            }
            else
            {
                txt = t.GetComponent<TMP_Text>();
            }

            if (txt != null)
            {
                txt.text =
                    $"<b>Statistiche</b>\n" +
                    $"Forza: {hero.GetModifiedStrength()}\n" +
                    $"Int: {hero.GetModifiedIntelligence()}   Vel: {hero.GetModifiedAgility()}";
            }
        }

        private string SlotText(HeroState hero, EquipmentSlot es, EquipmentData eq)
        {
            string dur = (eq.maxDurability > 0 && hero.durability.ContainsKey(es))
                ? $"\n[{hero.durability[es]}/{eq.maxDurability}]" : "";
            return ShortName(eq.cardName) + dur;
        }

        // ---------- Equipment window visibility ----------
        public bool IsEquipVisible(bool isPlayer1) => isPlayer1 ? p1EquipVisible : p2EquipVisible;

        public void ToggleEquipmentSlots(bool isPlayer1)
        {
            SetEquipmentSlotsVisible(isPlayer1, !(isPlayer1 ? p1EquipVisible : p2EquipVisible));
        }

        public void SetEquipmentSlotsVisible(bool isPlayer1, bool visible)
        {
            MapUIReferences();
            if (isPlayer1) p1EquipVisible = visible; else p2EquipVisible = visible;

            GameObject window = isPlayer1 ? p1EquipWindow : p2EquipWindow;
            if (window != null) window.SetActive(visible);

            // Highlight the toggle button when active.
            Button toggle = isPlayer1 ? p1ShowEquipButton : p2ShowEquipButton;
            if (toggle != null)
            {
                var img = toggle.GetComponent<Image>();
                if (img != null) img.color = visible ? new Color32(0x2e, 0xcc, 0x71, 0xff)
                                                     : new Color32(0x16, 0x21, 0x3e, 0xff);
            }

            // Move/resize the inspect box: full width when hidden, right of the slots when shown.
            GameObject descPanel = isPlayer1 ? p1DescPanel : p2DescPanel;
            if (descPanel != null)
            {
                var rt = descPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = visible ? DescPosRight : DescPosFull;
                    rt.sizeDelta = visible ? DescSizeRight : DescSizeFull;
                }
            }
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
            // Equipment has no mana/stamina cost, so hide the cost line for it.
            if (costT != null) costT.text = (card is EquipmentData) ? "" : $"M:{card.manaCost}  S:{card.staminaCost}";
            if (effectT != null)
            {
                if (card is EquipmentData eq)
                {
                    string stats = "";
                    // Requirements shown first (directly below the cost line), if any.
                    if (eq.requirements != null && eq.requirements.Count > 0)
                    {
                        string reqStr = "";
                        foreach (var r in eq.requirements)
                            reqStr += $"{EquipmentSystem.StatLabel(r.stat)} {r.value}  ";
                        stats += $"<color=#E94560>Requisiti: {reqStr.Trim()}</color>\n";
                    }
                    stats += $"<i>{eq.equipType} · {eq.slotType}</i>\n";
                    if (eq.damageValue > 0) stats += $"Danno: {eq.damageValue}  ";
                    if (eq.defenseValue > 0) stats += $"Difesa: {eq.defenseValue}  ";
                    foreach (var m in eq.attributeMods)
                        stats += $"{(m.value >= 0 ? "+" : "")}{m.value} {EquipmentSystem.AttributeLabel(m.attr)}  ";
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

        public void ShowDrawChoice(string title)
        {
            if (drawChoicePanel != null)
            {
                drawChoicePanel.SetActive(true);
                var titleText = drawChoicePanel.transform.Find("Box/Title")?.GetComponent<TMP_Text>();
                if (titleText != null)
                {
                    titleText.text = title;
                }
            }
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
