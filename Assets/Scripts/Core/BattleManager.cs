using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Botte.Core
{
    public class BattleManager : MonoBehaviour
    {
        public GameManager gameManager;
        public Botte.UI.BattleUI battleUI;

        [Header("Combat Phase Buttons")]
        public Button attackButton;
        public Button endTurnButton;

        [Header("Preparation Phase Buttons")]
        public Button drawPrepButton;
        public Button equipPrepButton;
        public Button finishPrepButton;

        [Header("End Phase Buttons")]
        public Button sleepButton;
        public Button drawExtraButton;

        [Header("Character Select (order: Warrior, Mage, Rogue, Necro)")]
        public Button[] p1ClassButtons;
        public Button[] p2ClassButtons;
        public Button startBattleButton;

        [Header("Winner Buttons")]
        public Button restartButton;
        public Button exitButton;

        [Header("Draw Choice Buttons")]
        public Button drawChoiceSpellButton;
        public Button drawChoiceEquipButton;
        public Button drawChoiceItemButton;

        [Header("Peek Buttons (manipolazione)")]
        public Button peekKeepButton;
        public Button peekDiscardButton;

        [Header("Main Menu Buttons")]
        public Button playMenuButton;
        public Button exitMenuButton;

        private GameState gameState;
        private TurnManager turnManager;
        private int prepDrawsThisPhase;
        private int equipsThisPhase;

        private HeroClass? selectedP1;
        private HeroClass? selectedP2;

        // Pending deck-draw flow (deck choice popup).
        private HeroState pendingDrawHero;
        private int pendingDrawCount;
        private bool pendingPeek;
        private System.Action pendingAfterDraws;

        // Pending peek (manipolazione) keep/discard.
        private DeckChoice pendingPeekDeck;
        private CardData pendingPeekCard;

        private void Start()
        {
            WireRuntimeListeners();
            ShowMainMenu();
        }

        public bool IsHeroActive(HeroState hero)
        {
            return gameState != null && gameState.activePlayer == hero;
        }

        // ---------- Runtime wiring ----------
        private void WireRuntimeListeners()
        {
            if (playMenuButton != null) playMenuButton.onClick.AddListener(OnPlayMenuPressed);
            if (exitMenuButton != null) exitMenuButton.onClick.AddListener(OnExitPressed);

            if (p1ClassButtons != null)
            {
                for (int i = 0; i < p1ClassButtons.Length; i++)
                {
                    int idx = i;
                    if (p1ClassButtons[i] != null) p1ClassButtons[i].onClick.AddListener(() => SelectClass(1, idx));
                }
            }
            if (p2ClassButtons != null)
            {
                for (int i = 0; i < p2ClassButtons.Length; i++)
                {
                    int idx = i;
                    if (p2ClassButtons[i] != null) p2ClassButtons[i].onClick.AddListener(() => SelectClass(2, idx));
                }
            }
            if (startBattleButton != null) startBattleButton.onClick.AddListener(OnStartBattlePressed);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartPressed);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitPressed);
            if (drawChoiceSpellButton != null) drawChoiceSpellButton.onClick.AddListener(() => OnDeckChoice(DeckChoice.Spell));
            if (drawChoiceEquipButton != null) drawChoiceEquipButton.onClick.AddListener(() => OnDeckChoice(DeckChoice.Equipment));
            if (drawChoiceItemButton != null) drawChoiceItemButton.onClick.AddListener(() => OnDeckChoice(DeckChoice.Item));
            if (peekKeepButton != null) peekKeepButton.onClick.AddListener(OnPeekKeep);
            if (peekDiscardButton != null) peekDiscardButton.onClick.AddListener(OnPeekDiscard);

            if (battleUI != null)
            {
                if (battleUI.p1BookButtons != null)
                    for (int i = 0; i < battleUI.p1BookButtons.Length; i++)
                    {
                        int idx = i;
                        if (battleUI.p1BookButtons[i] != null) battleUI.p1BookButtons[i].onClick.AddListener(() => OnBookSelected(true, idx));
                    }
                if (battleUI.p2BookButtons != null)
                    for (int i = 0; i < battleUI.p2BookButtons.Length; i++)
                    {
                        int idx = i;
                        if (battleUI.p2BookButtons[i] != null) battleUI.p2BookButtons[i].onClick.AddListener(() => OnBookSelected(false, idx));
                    }

                if (battleUI.p1ShowEquipButton != null) battleUI.p1ShowEquipButton.onClick.AddListener(() => OnShowEquipToggle(true));
                if (battleUI.p2ShowEquipButton != null) battleUI.p2ShowEquipButton.onClick.AddListener(() => OnShowEquipToggle(false));
            }
        }

        private void ShowMainMenu()
        {
            if (battleUI != null)
            {
                if (battleUI.mainMenuPanel != null) battleUI.mainMenuPanel.SetActive(true);
                if (battleUI.characterSelectPanel != null) battleUI.characterSelectPanel.SetActive(false);
                if (battleUI.battleScreen != null) battleUI.battleScreen.SetActive(false);
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);
            }
        }

        public void OnPlayMenuPressed()
        {
            if (battleUI != null && battleUI.mainMenuPanel != null)
                battleUI.mainMenuPanel.SetActive(false);
            ShowCharacterSelect();
        }

        // Toggles the equipment-slot window for a player (button to the left of the hero stats).
        public void OnShowEquipToggle(bool isPlayer1)
        {
            if (battleUI == null) return;
            battleUI.ToggleEquipmentSlots(isPlayer1);
            if (gameState != null)
                battleUI.RefreshEquipment(isPlayer1 ? gameState.player1 : gameState.player2, isPlayer1);
        }

        public void OnBookSelected(bool isPlayer1, int bookIdx)
        {
            if (battleUI == null) return;
            battleUI.SetSelectedBook(isPlayer1, (BookType)bookIdx);
            if (gameState != null)
                battleUI.RefreshBook(isPlayer1 ? gameState.player1 : gameState.player2, isPlayer1);
        }

        // ---------- Character select ----------
        private void ShowCharacterSelect()
        {
            selectedP1 = null;
            selectedP2 = null;
            if (startBattleButton != null) startBattleButton.interactable = false;

            // Populate class-button labels dynamically so F shows the bare (unarmed) damage.
            for (int i = 0; i < 4; i++)
            {
                RefreshClassButtonLabel(p1ClassButtons, i, (HeroClass)i);
                RefreshClassButtonLabel(p2ClassButtons, i, (HeroClass)i);
            }

            if (battleUI != null)
            {
                if (battleUI.characterSelectPanel != null) battleUI.characterSelectPanel.SetActive(true);
                if (battleUI.battleScreen != null) battleUI.battleScreen.SetActive(false);
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);
            }
        }

        // Displays the hero's strength (F) directly.
        private void RefreshClassButtonLabel(Button[] buttons, int idx, HeroClass hc)
        {
            if (buttons == null || idx >= buttons.Length || buttons[idx] == null) return;
            HeroData d = GameManager.CreateHeroData(hc);
            var tmp = buttons[idx].GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                tmp.text = $"{d.heroName}\n{hc}\nHP{d.maxHP}  F{d.strength} M{d.intelligence} S{d.agility}";
        }

        private void SelectClass(int player, int classIdx)
        {
            HeroClass chosen = (HeroClass)classIdx;
            if (player == 1)
            {
                if (selectedP2.HasValue && selectedP2.Value == chosen)
                {
                    battleUI.AddLog($"Non puoi scegliere lo stesso eroe dell'altro giocatore ({chosen})!");
                    return;
                }
                selectedP1 = chosen;
            }
            else
            {
                if (selectedP1.HasValue && selectedP1.Value == chosen)
                {
                    battleUI.AddLog($"Non puoi scegliere lo stesso eroe dell'altro giocatore ({chosen})!");
                    return;
                }
                selectedP2 = chosen;
            }
            if (battleUI != null) battleUI.UpdateSelectionHighlight(player, classIdx, selectedP1, selectedP2);
            if (startBattleButton != null) startBattleButton.interactable = selectedP1.HasValue && selectedP2.HasValue && (selectedP1.Value != selectedP2.Value);
        }

        public void OnStartBattlePressed()
        {
            if (!selectedP1.HasValue || !selectedP2.HasValue) return;

            gameManager.InitGame(selectedP1.Value, selectedP2.Value);
            gameState = gameManager.gameState;
            turnManager = new TurnManager(gameState);

            if (battleUI != null)
            {
                if (battleUI.characterSelectPanel != null) battleUI.characterSelectPanel.SetActive(false);
                if (battleUI.battleScreen != null) battleUI.battleScreen.SetActive(true);
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
                battleUI.ClearLog();
            }

            // Hide the optional draw prep button as the 2 draws are now obligatory
            if (drawPrepButton != null) drawPrepButton.gameObject.SetActive(false);

            SetAllButtonsInteractable(true);
            LogAction($"Battaglia iniziata! {GetStyledName(gameState.player1)} vs {GetStyledName(gameState.player2)}.");

            // Draw starting hands (Warrior: 3E/1I, Rogue: 2E/2I, Mage/Necro: 1E/2S/1I)
            DrawStartingHand(gameState.player1);
            DrawStartingHand(gameState.player2);

            OnPhaseEntered();
        }

        // ---------- Phase flow ----------
        private void OnPhaseEntered()
        {
            if (gameState == null || battleUI == null) return;

            HeroState active = gameState.activePlayer;
            HeroState opponent = (active == gameState.player1) ? gameState.player2 : gameState.player1;

            if (gameState.phase == GamePhase.ResourceRecovery)
            {
                turnManager.RunResourceRecoveryPhase(active, opponent);
                if (CheckForWinner()) return;
                gameState.AdvancePhase(); // -> Preparation
            }

            battleUI.turnText.text = $"Turno {gameState.currentTurn} — tocca a {GetStyledName(active)}";
            battleUI.phaseText.text = $"Fase: {gameState.phase}";

            if (battleUI.prepButtonsContainer != null)
            {
                battleUI.prepButtonsContainer.SetActive(gameState.phase == GamePhase.Preparation);
                if (drawPrepButton != null) drawPrepButton.gameObject.SetActive(false);
            }
            if (battleUI.combatButtonsContainer != null) battleUI.combatButtonsContainer.SetActive(gameState.phase == GamePhase.Combat);
            if (battleUI.endButtonsContainer != null) battleUI.endButtonsContainer.SetActive(gameState.phase == GamePhase.EndPhase);

            // Equipment slots are hidden by default at the start of every phase.
            battleUI.SetEquipmentSlotsVisible(true, false);
            battleUI.SetEquipmentSlotsVisible(false, false);

            if (gameState.phase == GamePhase.Preparation)
            {
                prepDrawsThisPhase = 0;
                equipsThisPhase = 0;
                if (drawPrepButton != null) drawPrepButton.interactable = true;
                LogAction($"=== Fase di Preparazione di {GetStyledName(active)} ===");

                // Process poison damage tick during the preparation phase
                int poisonDamage = turnManager.ProcessPoisonTick(active);
                if (poisonDamage > 0)
                {
                    LogAction($"{GetStyledName(active)} subisce {poisonDamage} danni da Veleno all'inizio della Preparazione.");
                    if (CheckForWinner()) return;
                }

                // Automatically trigger the 2 obligatory prep phase draws
                TriggerPrepDraw(active, 1);
            }
            else if (gameState.phase == GamePhase.Combat)
            {
                LogAction($"=== Fase di Combattimento di {GetStyledName(active)} ===");
            }
            else if (gameState.phase == GamePhase.EndPhase)
            {
                LogAction($"=== Fase Finale di {GetStyledName(active)} ===");
            }

            RefreshAll();
        }

        private void TriggerPrepDraw(HeroState hero, int drawNumber)
        {
            pendingDrawHero = hero;
            pendingDrawCount = 1;
            pendingPeek = false;
            pendingAfterDraws = () =>
            {
                prepDrawsThisPhase++;
                if (prepDrawsThisPhase < 2)
                {
                    TriggerPrepDraw(hero, 2);
                }
            };
            string label = (drawNumber == 1) ? "1° Pescaggio Obbligatorio" : "2° Pescaggio Obbligatorio";
            battleUI.ShowDrawChoice(label);
        }

        // ---------- Preparation ----------
        public void OnDrawPrepPressed()
        {
            // Legacy button no longer active
        }

        public void OnEquipPrepPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Preparation) return;
            bool isP1 = gameState.activePlayer == gameState.player1;

            // Entering equip mode: force the equipment book AND show the equipment slots.
            battleUI.SetSelectedBook(isP1, BookType.Equipment);
            battleUI.SetEquipmentSlotsVisible(isP1, true);
            LogAction($"{GetStyledName(gameState.activePlayer)}: modalità equipaggiamento attiva. Clicca un pezzo nel Libro Equipaggiamento (max 2 per turno).");
            RefreshAll();
        }

        // Left-clicking an equipment card in the Equipment book during Preparation equips it.
        public void OnEquipmentClicked(HeroState owner, EquipmentData equip)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Non è il tuo turno!");
                return;
            }
            if (gameState.phase != GamePhase.Preparation)
            {
                battleUI.AddLog("Puoi equipaggiare solo in Preparazione (in combattimento l'equipaggiamento si può solo ispezionare).");
                return;
            }
            if (equipsThisPhase >= 2)
            {
                battleUI.AddLog($"{owner.data.heroName} ha già equipaggiato 2 pezzi questo turno.");
                return;
            }

            // Both conditions required: the equipment book must be selected AND the slots shown.
            bool isP1 = owner == gameState.player1;
            BookType selBook = isP1 ? battleUI.p1SelectedBook : battleUI.p2SelectedBook;
            if (selBook != BookType.Equipment || !battleUI.IsEquipVisible(isP1))
            {
                battleUI.AddLog("Premi il pulsante Equipaggia per attivare la modalità (serve il Libro Equipaggiamento e gli slot mostrati).");
                return;
            }
            if (!owner.equipmentBook.Contains(equip)) return;

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;
            owner.equipmentBook.Remove(equip);
            var displaced = EquipmentSystem.Equip(owner, equip);
            LogAction($"{GetStyledName(owner)} equipaggia {equip.cardName} ({equip.slotType}).");
            foreach (var d in displaced)
            {
                DiscardEquipment(owner, opponent, d);
                LogAction($"{GetStyledName(owner)} sostituisce e scarta {d.cardName}.");
            }
            equipsThisPhase++;
            RefreshAll();
        }

        // Handles OnDiscard equipment effects then moves the piece to the discard pile.
        private void DiscardEquipment(HeroState owner, HeroState opponent, EquipmentData equip)
        {
            if (equip.specialEffect == EquipEffect.OnDiscardGainMana)
            {
                owner.currentMana = Mathf.Min(owner.GetModifiedIntelligence(), owner.currentMana + equip.effectValue);
                LogAction($"{equip.cardName}: {GetStyledName(owner)} guadagna {equip.effectValue} Mana.");
            }
            else if (equip.specialEffect == EquipEffect.OnDiscardDamage)
            {
                int dealt = CombatActions.DealDamage(owner, opponent, equip.effectValue, false, false);
                LogAction($"{equip.cardName}: infligge {dealt} danni a {GetStyledName(opponent)}.");
            }
            owner.discardPile.Add(equip);
        }

        public void OnFinishPrepPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Preparation) return;
            gameState.AdvancePhase(); // -> Combat
            OnPhaseEntered();
        }

        // ---------- Combat ----------
        public void OnAttackPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Combat) return;

            HeroState attacker = gameState.activePlayer;
            HeroState defender = (attacker == gameState.player1) ? gameState.player2 : gameState.player1;

            if (CombatActions.TryWeaponAttack(attacker, defender, 2))
            {
                RefreshAll();
                CheckForWinner();
            }
        }

        public void OnEndTurnPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Combat) return;
            gameState.AdvancePhase(); // -> EndPhase
            OnPhaseEntered();
        }

        // ---------- End phase ----------
        public void OnSleepPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.EndPhase) return;
            turnManager.RunEndPhase(gameState.activePlayer, EndPhaseChoice.Rest);
            EndRoundForActive();
        }

        public void OnDrawExtraPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.EndPhase) return;
            // Draw 1 from a chosen deck, then end the round.
            HeroState active = gameState.activePlayer;
            RequestDeckDraw(active, 1, () =>
            {
                turnManager.RunEndPhase(active, EndPhaseChoice.DrawExtraCard);
                EndRoundForActive();
            });
        }

        // Repeatable cards are NO LONGER auto-discarded — they persist until the player
        // discards them by right-clicking. Round end just advances to the next hero.
        private void EndRoundForActive()
        {
            gameState.AdvancePhase(); // wraps to next hero ResourceRecovery
            OnPhaseEntered();
        }

        // ---------- Spell card usage (left click) ----------
        public void OnCardClicked(HeroState owner, MagicData spell)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Non è il tuo turno!");
                return;
            }
            if (gameState.phase != GamePhase.Combat)
            {
                battleUI.AddLog("Puoi usare le carte solo durante la fase di Combattimento!");
                return;
            }
            if (spell.magicType == MagicType.Aura && owner.activeAuras.Contains(spell))
            {
                battleUI.AddLog($"{spell.cardName} è un'aura già attiva.");
                return;
            }
            if (spell.magicType == MagicType.Exhaustion && owner.exhaustedThisRound.Contains(spell))
            {
                battleUI.AddLog($"{spell.cardName} è già stata usata questo round.");
                return;
            }

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;

            CastResult result = SpellActions.TryCastSpell(owner, opponent, spell);
            if (!result.success) return;

            owner.cardTypesUsedThisTurn.Add("Spell");
            ResolveDeferred(owner, opponent, result);

            // Per-magic-type bookkeeping.
            switch (spell.magicType)
            {
                case MagicType.Instant:
                    owner.hand.Remove(spell);
                    owner.discardPile.Add(spell);
                    break;
                case MagicType.Aura:
                    if (!owner.activeAuras.Contains(spell)) owner.activeAuras.Add(spell);
                    break;
                case MagicType.Exhaustion:
                    if (!owner.exhaustedThisRound.Contains(spell)) owner.exhaustedThisRound.Add(spell);
                    break;
                case MagicType.Repeatable:
                    // stays in the spellbook; only discarded by player choice (right-click)
                    break;
            }

            RefreshAll();
            CheckForWinner();
        }

        // ---------- Item card usage (left click) ----------
        public void OnItemClicked(HeroState owner, ItemData item)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Non è il tuo turno!");
                return;
            }
            if (gameState.phase != GamePhase.Combat)
            {
                battleUI.AddLog("Puoi usare gli oggetti solo durante la fase di Combattimento!");
                return;
            }

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;

            CastResult result = ItemActions.TryUseItem(owner, opponent, item);
            if (!result.success) return;

            owner.cardTypesUsedThisTurn.Add("Item");

            // Items are always discarded into the shared item discard pile after use.
            owner.itemBook.Remove(item);
            gameState.itemDiscard.Add(item);

            ResolveDeferred(owner, opponent, result);

            RefreshAll();
            CheckForWinner();
        }

        // ---------- Right click: discard a card from the active hero's books ----------
        public void OnCardRightClicked(HeroState owner, CardData card)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Puoi scartare le carte solo durante il tuo turno!");
                return;
            }

            if (card is ItemData item && owner.itemBook.Contains(item))
            {
                owner.itemBook.Remove(item);
                gameState.itemDiscard.Add(item);
                LogAction($"{GetStyledName(owner)} scarta l'oggetto {item.cardName}.");
            }
            else if (card is MagicData spell && owner.hand.Contains(spell))
            {
                owner.hand.Remove(spell);
                owner.discardPile.Add(spell);
                owner.activeAuras.Remove(spell);
                LogAction($"{GetStyledName(owner)} scarta {spell.cardName} dal libro incantesimi.");
            }
            else if (card is EquipmentData eq && owner.equipmentBook.Contains(eq))
            {
                HeroState opp = (owner == gameState.player1) ? gameState.player2 : gameState.player1;
                owner.equipmentBook.Remove(eq);
                DiscardEquipment(owner, opp, eq);
                LogAction($"{GetStyledName(owner)} scarta l'equipaggiamento {eq.cardName}.");
            }
            else
            {
                return;
            }
            RefreshAll();
        }

        // ---------- Deferred effect resolution ----------
        private void ResolveDeferred(HeroState owner, HeroState opponent, CastResult result)
        {
            for (int i = 0; i < result.drawFromDiscardCount; i++) DrawFromOwnDiscard(owner);
            for (int i = 0; i < result.stealOpponentDiscardCount; i++) StealFromOpponentDiscard(owner, opponent);

            int deckDraws = result.drawSpellCount + result.drawChosenDeckCount;
            if (result.peekChosenDeckCount > 0)
            {
                RequestPeek(owner);
            }
            else if (deckDraws > 0)
            {
                RequestDeckDraw(owner, deckDraws, null);
            }
        }

        // ---------- Deck choice popup flow ----------
        private void RequestDeckDraw(HeroState hero, int count, System.Action after)
        {
            pendingDrawHero = hero;
            pendingDrawCount = count;
            pendingAfterDraws = after;
            pendingPeek = false;
            if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(true);
        }

        private void RequestPeek(HeroState hero)
        {
            pendingDrawHero = hero;
            pendingPeek = true;
            if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(true);
        }

        public void OnDeckChoice(DeckChoice deck)
        {
            if (pendingDrawHero == null) { if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false); return; }

            if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);

            if (pendingPeek)
            {
                pendingPeek = false;
                DoPeek(pendingDrawHero, deck);
                RefreshAll();
                return;
            }

            DrawOneFromDeck(pendingDrawHero, deck);
            pendingDrawCount--;
            RefreshAll();

            if (pendingDrawCount > 0)
            {
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(true);
            }
            else
            {
                System.Action after = pendingAfterDraws;
                pendingAfterDraws = null;
                pendingDrawHero = null;
                if (after != null) after.Invoke();
            }
        }

        public string GetStyledName(HeroState hero)
        {
            if (hero == null) return "";
            return Botte.UI.BattleUI.GetClassColorizedName(hero.data.heroClass, hero.data.heroName);
        }

        public void LogAction(string message)
        {
            if (battleUI != null)
            {
                battleUI.AddLog(""); // empty line separator between different turn actions
                battleUI.AddLog(message);
            }
        }

        public void OnEquipSlotRightClicked(bool isPlayer1, EquipmentSlot slot)
        {
            if (gameState == null) return;
            HeroState owner = isPlayer1 ? gameState.player1 : gameState.player2;
            if (gameState.activePlayer != owner)
            {
                battleUI.AddLog("Puoi disequipaggiare/scartare equipaggiamento solo durante il tuo turno!");
                return;
            }

            EquipmentData equip = owner.equippedItems[(int)slot];
            if (equip != null)
            {
                owner.equippedItems[(int)slot] = null;
                if (equip.slotType == EquipSlotType.TwoHandWeapon)
                {
                    owner.weaponTwoHandedEquipped = false;
                }
                owner.durability.Remove(slot);
                EquipmentSystem.RemoveAttributes(owner, equip);

                HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;

                // Sacrifice ability triggered on right click
                if (equip.specialEffect == EquipEffect.SacrificeBlockThenBreak)
                {
                    owner.hasShield = true;
                    LogAction($"{GetStyledName(owner)} sacrifica {equip.cardName} e ottiene un BLOCCO completo per il prossimo attacco!");
                }

                DiscardEquipment(owner, opponent, equip);
                LogAction($"{GetStyledName(owner)} disequipaggia e scarta {equip.cardName} dallo slot attivo {slot}.");
                RefreshAll();
            }
        }

        private void DrawStartingHand(HeroState hero)
        {
            HeroClass hc = hero.data.heroClass;
            int numEquip = 0;
            int numSpells = 0;
            int numItems = 0;

            switch (hc)
            {
                case HeroClass.Warrior:
                    numEquip = 3; numItems = 1; break;
                case HeroClass.Rogue:
                    numEquip = 2; numItems = 2; break;
                case HeroClass.Mage:
                    numEquip = 1; numSpells = 2; numItems = 1; break;
                case HeroClass.Necro:
                    numEquip = 1; numSpells = 2; numItems = 1; break;
            }

            for (int i = 0; i < numEquip; i++) DrawStartingCard(hero, DeckChoice.Equipment);
            for (int i = 0; i < numSpells; i++) DrawStartingCard(hero, DeckChoice.Spell);
            for (int i = 0; i < numItems; i++) DrawStartingCard(hero, DeckChoice.Item);
        }

        private void DrawStartingCard(HeroState hero, DeckChoice deck)
        {
            if (deck == DeckChoice.Item)
            {
                if (gameState.itemDeck.Count > 0)
                {
                    int idx = Random.Range(0, gameState.itemDeck.Count);
                    CardData card = gameState.itemDeck[idx];
                    gameState.itemDeck.RemoveAt(idx);
                    if (hero.itemBook.Count < HeroState.MAX_BOOK_SIZE)
                    {
                        hero.itemBook.Add(card);
                    }
                    else
                    {
                        gameState.itemDiscard.Add(card);
                    }
                }
            }
            else if (deck == DeckChoice.Equipment)
            {
                if (hero.equipmentDeck.Count > 0)
                {
                    int idx = Random.Range(0, hero.equipmentDeck.Count);
                    CardData card = hero.equipmentDeck[idx];
                    hero.equipmentDeck.RemoveAt(idx);
                    if (hero.equipmentBook.Count < HeroState.MAX_BOOK_SIZE)
                    {
                        hero.equipmentBook.Add(card);
                    }
                    else
                    {
                        hero.discardPile.Add(card);
                    }
                }
            }
            else if (deck == DeckChoice.Spell)
            {
                if (hero.magicDeck.Count > 0)
                {
                    int idx = Random.Range(0, hero.magicDeck.Count);
                    CardData card = hero.magicDeck[idx];
                    hero.magicDeck.RemoveAt(idx);
                    if (hero.hand.Count < HeroState.MAX_BOOK_SIZE)
                    {
                        hero.hand.Add(card);
                    }
                    else
                    {
                        hero.discardPile.Add(card);
                    }
                }
            }
        }

        private void ApplyFatigue(HeroState hero)
        {
            hero.fatigueCount++;
            hero.currentHP = Mathf.Max(0, hero.currentHP - hero.fatigueCount);
            LogAction($"{GetStyledName(hero)} tenta di pescare da un mazzo vuoto e subisce <color=red>{hero.fatigueCount}</color> danni da Affaticamento! (HP rimanenti: {hero.currentHP})");
            RefreshAll();
            CheckForWinner();
        }

        // ---------- Concrete draws ----------
        private void DrawOneFromDeck(HeroState hero, DeckChoice deck)
        {
            string prepPrefix = "";
            if (gameState != null && gameState.phase == GamePhase.Preparation)
            {
                prepPrefix = (prepDrawsThisPhase == 0) ? "[1° pescaggio di preparazione] " : "[2° pescaggio di preparazione] ";
            }

            if (deck == DeckChoice.Item)
            {
                if (gameState.itemDeck.Count == 0)
                {
                    ApplyFatigue(hero);
                    return;
                }
                int idx = Random.Range(0, gameState.itemDeck.Count);
                CardData card = gameState.itemDeck[idx];
                gameState.itemDeck.RemoveAt(idx);

                if (hero.itemBook.Count >= HeroState.MAX_BOOK_SIZE)
                {
                    gameState.itemDiscard.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'oggetto {card.cardName}, ma il suo Libro Oggetti è pieno ({HeroState.MAX_BOOK_SIZE}). Viene scartato immediatamente!");
                }
                else
                {
                    hero.itemBook.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'oggetto {card.cardName}.");
                }
                return;
            }

            if (deck == DeckChoice.Equipment)
            {
                if (hero.equipmentDeck.Count == 0)
                {
                    ApplyFatigue(hero);
                    return;
                }
                int eIdx = Random.Range(0, hero.equipmentDeck.Count);
                CardData card = hero.equipmentDeck[eIdx];
                hero.equipmentDeck.RemoveAt(eIdx);

                if (hero.equipmentBook.Count >= HeroState.MAX_BOOK_SIZE)
                {
                    hero.discardPile.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'equipaggiamento {card.cardName}, ma il suo Libro Equipaggiamento è pieno ({HeroState.MAX_BOOK_SIZE}). Viene scartato immediatamente!");
                }
                else
                {
                    hero.equipmentBook.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'equipaggiamento {card.cardName}.");
                }
                return;
            }

            if (deck == DeckChoice.Spell)
            {
                if (hero.magicDeck.Count == 0)
                {
                    ApplyFatigue(hero);
                    return;
                }
                int sIdx = Random.Range(0, hero.magicDeck.Count);
                CardData spellCard = hero.magicDeck[sIdx];
                hero.magicDeck.RemoveAt(sIdx);

                bool isInstant = spellCard is MagicData m && m.magicType == MagicType.Instant;
                if (hero.hand.Count >= HeroState.MAX_BOOK_SIZE || (!isInstant && hero.IsSpellbookFull()))
                {
                    hero.discardPile.Add(spellCard);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca {spellCard.cardName}, ma il suo Libro Incantesimi è pieno ({HeroState.MAX_BOOK_SIZE}). Viene scartata immediatamente!");
                }
                else
                {
                    hero.hand.Add(spellCard);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca {spellCard.cardName}.");
                }
                return;
            }
        }

        private void DrawFromOwnDiscard(HeroState hero)
        {
            if (hero.discardPile.Count == 0) { battleUI.AddLog($"{hero.data.heroName} non ha carte negli scarti."); return; }
            int idx = Random.Range(0, hero.discardPile.Count);
            CardData card = hero.discardPile[idx];
            if (hero.hand.Count >= HeroState.MAX_BOOK_SIZE || (card is MagicData m && m.magicType != MagicType.Instant && hero.IsSpellbookFull()))
            {
                LogAction($"Libro incantesimi pieno: impossibile recuperare {card.cardName} dagli scarti.");
                return;
            }
            hero.discardPile.RemoveAt(idx);
            hero.hand.Add(card);
            LogAction($"{GetStyledName(hero)} recupera {card.cardName} dagli scarti.");
        }

        // Saccheggio: grab a card from opponent discard into own spellbook (ignores size limit).
        private void StealFromOpponentDiscard(HeroState owner, HeroState opponent)
        {
            if (opponent.discardPile.Count == 0) { battleUI.AddLog($"{opponent.data.heroName} non ha carte negli scarti da rubare."); return; }
            int idx = Random.Range(0, opponent.discardPile.Count);
            CardData card = opponent.discardPile[idx];
            opponent.discardPile.RemoveAt(idx);
            
            // Ignores spellbook size restriction (standard logic), but let's check MAX_BOOK_SIZE limit!
            // Wait, "ignore spell book size restriction" is stated in the description of Saccheggio, so we bypass it!
            owner.hand.Add(card);
            LogAction($"{GetStyledName(owner)} ruba {card.cardName} dagli scarti di {GetStyledName(opponent)} (limite ignorato).");
        }

        // Manipolazione: reveal top card of a chosen deck, then keep or discard it.
        private void DoPeek(HeroState hero, DeckChoice deck)
        {
            CardData top = null;
            if (deck == DeckChoice.Item)
            {
                if (gameState.itemDeck.Count == 0) { battleUI.AddLog("Il mazzo oggetti è vuoto."); return; }
                top = gameState.itemDeck[0];
            }
            else if (deck == DeckChoice.Equipment)
            {
                if (hero.equipmentDeck.Count == 0) { battleUI.AddLog("Il mazzo equipaggiamento è vuoto."); return; }
                top = hero.equipmentDeck[0];
            }
            else
            {
                if (hero.magicDeck.Count == 0) { battleUI.AddLog($"Il mazzo incantesimi di {hero.data.heroName} è vuoto."); return; }
                top = hero.magicDeck[0];
            }
            pendingPeekDeck = deck;
            pendingPeekCard = top;
            battleUI.ShowPeek(top.cardName);
        }

        public void OnPeekKeep()
        {
            battleUI.HidePeek();
            if (pendingPeekCard == null) return;
            HeroState hero = pendingDrawHero;
            if (pendingPeekDeck == DeckChoice.Item)
            {
                gameState.itemDeck.Remove(pendingPeekCard);
                hero.itemBook.Add(pendingPeekCard);
                battleUI.AddLog($"{hero.data.heroName} tiene l'oggetto {pendingPeekCard.cardName}.");
            }
            else
            {
                bool isInstant = pendingPeekCard is MagicData m && m.magicType == MagicType.Instant;
                if (!isInstant && hero.IsSpellbookFull())
                {
                    battleUI.AddLog($"Libro pieno: {pendingPeekCard.cardName} viene scartata invece di essere tenuta.");
                    hero.magicDeck.Remove(pendingPeekCard);
                    hero.discardPile.Add(pendingPeekCard);
                }
                else
                {
                    hero.magicDeck.Remove(pendingPeekCard);
                    hero.hand.Add(pendingPeekCard);
                    battleUI.AddLog($"{hero.data.heroName} tiene {pendingPeekCard.cardName}.");
                }
            }
            pendingPeekCard = null;
            pendingDrawHero = null;
            RefreshAll();
        }

        public void OnPeekDiscard()
        {
            battleUI.HidePeek();
            if (pendingPeekCard == null) return;
            HeroState hero = pendingDrawHero;
            if (pendingPeekDeck == DeckChoice.Item)
            {
                gameState.itemDeck.Remove(pendingPeekCard);
                gameState.itemDiscard.Add(pendingPeekCard);
            }
            else
            {
                hero.magicDeck.Remove(pendingPeekCard);
                hero.discardPile.Add(pendingPeekCard);
            }
            battleUI.AddLog($"{hero.data.heroName} scarta {pendingPeekCard.cardName} dalla cima del mazzo.");
            pendingPeekCard = null;
            pendingDrawHero = null;
            RefreshAll();
        }

        // ---------- Win / restart / exit ----------
        private bool CheckForWinner()
        {
            if (gameState == null) return false;
            if (gameState.player1.currentHP <= 0) { EndBattle(gameState.player2); return true; }
            if (gameState.player2.currentHP <= 0) { EndBattle(gameState.player1); return true; }
            return false;
        }

        private void EndBattle(HeroState winner)
        {
            SetAllButtonsInteractable(false);
            if (battleUI != null)
            {
                battleUI.winnerOverlay.SetActive(true);
                battleUI.winnerText.text = $"{winner.data.heroName} vince!";
                LogAction($"{GetStyledName(winner)} vince! Partita conclusa.");
            }
        }

        public void OnRestartPressed()
        {
            gameState = null;
            turnManager = null;
            if (battleUI != null)
            {
                battleUI.ClearHands();
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
            }
            ShowCharacterSelect();
        }

        public void OnExitPressed()
        {
            Debug.Log("Uscita dal gioco.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Helpers ----------
        private void RefreshAll()
        {
            battleUI.RefreshHero(gameState.player1, true);
            battleUI.RefreshHero(gameState.player2, false);
            battleUI.RefreshBook(gameState.player1, true);
            battleUI.RefreshBook(gameState.player2, false);
            battleUI.RefreshEquipment(gameState.player1, true);
            battleUI.RefreshEquipment(gameState.player2, false);
        }

        private void SetAllButtonsInteractable(bool value)
        {
            if (attackButton != null) attackButton.interactable = value;
            if (endTurnButton != null) endTurnButton.interactable = value;
            if (drawPrepButton != null) drawPrepButton.interactable = value;
            if (equipPrepButton != null) equipPrepButton.interactable = value;
            if (finishPrepButton != null) finishPrepButton.interactable = value;
            if (sleepButton != null) sleepButton.interactable = value;
            if (drawExtraButton != null) drawExtraButton.interactable = value;
        }
    }
}
