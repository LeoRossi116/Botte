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
            ShowCharacterSelect();
        }

        // ---------- Runtime wiring ----------
        private void WireRuntimeListeners()
        {
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
            }
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
            if (battleUI != null)
            {
                if (battleUI.characterSelectPanel != null) battleUI.characterSelectPanel.SetActive(true);
                if (battleUI.battleScreen != null) battleUI.battleScreen.SetActive(false);
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);
            }
        }

        private void SelectClass(int player, int classIdx)
        {
            HeroClass chosen = (HeroClass)classIdx;
            if (player == 1) selectedP1 = chosen; else selectedP2 = chosen;
            if (battleUI != null) battleUI.UpdateSelectionHighlight(player, classIdx, selectedP1, selectedP2);
            if (startBattleButton != null) startBattleButton.interactable = selectedP1.HasValue && selectedP2.HasValue;
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

            SetAllButtonsInteractable(true);
            battleUI.AddLog($"Battaglia iniziata! {gameState.player1.data.heroName} vs {gameState.player2.data.heroName}.");
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

            battleUI.turnText.text = $"Turno {gameState.currentTurn} — tocca a {active.data.heroName}";
            battleUI.phaseText.text = $"Fase: {gameState.phase}";

            if (battleUI.prepButtonsContainer != null) battleUI.prepButtonsContainer.SetActive(gameState.phase == GamePhase.Preparation);
            if (battleUI.combatButtonsContainer != null) battleUI.combatButtonsContainer.SetActive(gameState.phase == GamePhase.Combat);
            if (battleUI.endButtonsContainer != null) battleUI.endButtonsContainer.SetActive(gameState.phase == GamePhase.EndPhase);

            if (gameState.phase == GamePhase.Preparation)
            {
                prepDrawsThisPhase = 0;
                equipsThisPhase = 0;
                if (drawPrepButton != null) drawPrepButton.interactable = true;
                battleUI.AddLog($"=== Fase di Preparazione di {active.data.heroName} ===");
            }
            else if (gameState.phase == GamePhase.Combat)
            {
                battleUI.AddLog($"=== Fase di Combattimento di {active.data.heroName} ===");
            }
            else if (gameState.phase == GamePhase.EndPhase)
            {
                battleUI.AddLog($"=== Fase Finale di {active.data.heroName} ===");
            }

            RefreshAll();
        }

        // ---------- Preparation ----------
        public void OnDrawPrepPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Preparation) return;
            if (prepDrawsThisPhase >= 2) return;

            // Each prep draw lets the player choose which deck to draw from.
            RequestDeckDraw(gameState.activePlayer, 1, () =>
            {
                prepDrawsThisPhase++;
                if (prepDrawsThisPhase >= 2 && drawPrepButton != null) drawPrepButton.interactable = false;
            });
        }

        public void OnEquipPrepPressed()
        {
            battleUI.AddLog($"{gameState.activePlayer.data.heroName}: seleziona il Libro Equipaggiamento e clicca un pezzo per equipaggiarlo (max 2 per turno).");
            if (battleUI != null) battleUI.SetSelectedBook(gameState.activePlayer == gameState.player1, BookType.Equipment);
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
            if (!owner.equipmentBook.Contains(equip)) return;

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;
            owner.equipmentBook.Remove(equip);
            var displaced = EquipmentSystem.Equip(owner, equip);
            battleUI.AddLog($"{owner.data.heroName} equipaggia {equip.cardName} ({equip.slotType}).");
            foreach (var d in displaced)
            {
                DiscardEquipment(owner, opponent, d);
                battleUI.AddLog($"{owner.data.heroName} sostituisce e scarta {d.cardName}.");
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
                battleUI.AddLog($"{equip.cardName}: {owner.data.heroName} guadagna {equip.effectValue} Mana.");
            }
            else if (equip.specialEffect == EquipEffect.OnDiscardDamage)
            {
                int dealt = CombatActions.DealDamage(owner, opponent, equip.effectValue, false, false);
                battleUI.AddLog($"{equip.cardName}: infligge {dealt} danni a {opponent.data.heroName}.");
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
                battleUI.AddLog($"{owner.data.heroName} scarta l'oggetto {item.cardName}.");
            }
            else if (card is MagicData spell && owner.hand.Contains(spell))
            {
                owner.hand.Remove(spell);
                owner.discardPile.Add(spell);
                owner.activeAuras.Remove(spell);
                battleUI.AddLog($"{owner.data.heroName} scarta {spell.cardName} dal libro incantesimi.");
            }
            else if (card is EquipmentData eq && owner.equipmentBook.Contains(eq))
            {
                HeroState opp = (owner == gameState.player1) ? gameState.player2 : gameState.player1;
                owner.equipmentBook.Remove(eq);
                DiscardEquipment(owner, opp, eq);
                battleUI.AddLog($"{owner.data.heroName} scarta l'equipaggiamento {eq.cardName}.");
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

        // ---------- Concrete draws ----------
        private void DrawOneFromDeck(HeroState hero, DeckChoice deck)
        {
            if (deck == DeckChoice.Item)
            {
                if (!EnsureItemDeck()) { battleUI.AddLog("Il mazzo oggetti è vuoto!"); return; }
                int idx = Random.Range(0, gameState.itemDeck.Count);
                CardData card = gameState.itemDeck[idx];
                gameState.itemDeck.RemoveAt(idx);
                hero.itemBook.Add(card);
                battleUI.AddLog($"{hero.data.heroName} pesca l'oggetto {card.cardName}.");
                return;
            }

            if (deck == DeckChoice.Equipment)
            {
                if (!EnsureEquipmentDeck(hero)) { battleUI.AddLog($"{hero.data.heroName} non ha equipaggiamento da pescare!"); return; }
                int eIdx = Random.Range(0, hero.equipmentDeck.Count);
                CardData card = hero.equipmentDeck[eIdx];
                hero.equipmentDeck.RemoveAt(eIdx);
                hero.equipmentBook.Add(card);
                battleUI.AddLog($"{hero.data.heroName} pesca l'equipaggiamento {card.cardName}.");
                return;
            }

            // Spell deck draw, with spellbook size limit (instants excluded).
            if (!EnsureSpellDeck(hero)) { battleUI.AddLog($"{hero.data.heroName} non ha carte incantesimo da pescare!"); return; }
            int sIdx = Random.Range(0, hero.magicDeck.Count);
            CardData spellCard = hero.magicDeck[sIdx];
            bool isInstant = spellCard is MagicData m && m.magicType == MagicType.Instant;
            if (!isInstant && hero.IsSpellbookFull())
            {
                battleUI.AddLog($"Libro incantesimi pieno ({HeroState.MAX_SPELLBOOK}): {spellCard.cardName} non viene raccolta e resta nel mazzo.");
                return; // card stays in the deck
            }
            hero.magicDeck.RemoveAt(sIdx);
            hero.hand.Add(spellCard);
            battleUI.AddLog($"{hero.data.heroName} pesca {spellCard.cardName}.");
        }

        // Refills the spell deck by pulling ONLY spell cards out of the shared discard pile.
        private bool EnsureSpellDeck(HeroState hero)
        {
            if (hero.magicDeck.Count > 0) return true;
            bool moved = false;
            for (int i = hero.discardPile.Count - 1; i >= 0; i--)
            {
                if (hero.discardPile[i] is MagicData)
                {
                    hero.magicDeck.Add(hero.discardPile[i]);
                    hero.discardPile.RemoveAt(i);
                    moved = true;
                }
            }
            if (moved) battleUI.AddLog($"{hero.data.heroName} rimescola gli incantesimi scartati nel mazzo.");
            return hero.magicDeck.Count > 0;
        }

        // Refills the equipment deck by pulling ONLY equipment cards out of the shared discard pile.
        private bool EnsureEquipmentDeck(HeroState hero)
        {
            if (hero.equipmentDeck.Count > 0) return true;
            bool moved = false;
            for (int i = hero.discardPile.Count - 1; i >= 0; i--)
            {
                if (hero.discardPile[i] is EquipmentData)
                {
                    hero.equipmentDeck.Add(hero.discardPile[i]);
                    hero.discardPile.RemoveAt(i);
                    moved = true;
                }
            }
            if (moved) battleUI.AddLog($"{hero.data.heroName} rimescola l'equipaggiamento scartato nel mazzo.");
            return hero.equipmentDeck.Count > 0;
        }

        private bool EnsureItemDeck()
        {
            if (gameState.itemDeck.Count > 0) return true;
            if (gameState.itemDiscard.Count > 0)
            {
                gameState.itemDeck.AddRange(gameState.itemDiscard);
                gameState.itemDiscard.Clear();
                battleUI.AddLog("Gli scarti oggetti vengono rimescolati nel mazzo oggetti.");
                return true;
            }
            return false;
        }

        private void DrawFromOwnDiscard(HeroState hero)
        {
            if (hero.discardPile.Count == 0) { battleUI.AddLog($"{hero.data.heroName} non ha carte negli scarti."); return; }
            int idx = Random.Range(0, hero.discardPile.Count);
            CardData card = hero.discardPile[idx];
            if (card is MagicData m && m.magicType != MagicType.Instant && hero.IsSpellbookFull())
            {
                battleUI.AddLog($"Libro incantesimi pieno: impossibile recuperare {card.cardName} dagli scarti.");
                return;
            }
            hero.discardPile.RemoveAt(idx);
            hero.hand.Add(card);
            battleUI.AddLog($"{hero.data.heroName} recupera {card.cardName} dagli scarti.");
        }

        // Saccheggio: grab a card from opponent discard into own spellbook (ignores size limit).
        private void StealFromOpponentDiscard(HeroState owner, HeroState opponent)
        {
            if (opponent.discardPile.Count == 0) { battleUI.AddLog($"{opponent.data.heroName} non ha carte negli scarti da rubare."); return; }
            int idx = Random.Range(0, opponent.discardPile.Count);
            CardData card = opponent.discardPile[idx];
            opponent.discardPile.RemoveAt(idx);
            owner.hand.Add(card);
            battleUI.AddLog($"{owner.data.heroName} ruba {card.cardName} dagli scarti di {opponent.data.heroName} (limite ignorato).");
        }

        // Manipolazione: reveal top card of a chosen deck, then keep or discard it.
        private void DoPeek(HeroState hero, DeckChoice deck)
        {
            CardData top = null;
            if (deck == DeckChoice.Item)
            {
                if (!EnsureItemDeck()) { battleUI.AddLog("Il mazzo oggetti è vuoto."); return; }
                top = gameState.itemDeck[0];
            }
            else
            {
                if (!EnsureSpellDeck(hero)) { battleUI.AddLog($"Il mazzo incantesimi di {hero.data.heroName} è vuoto."); return; }
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
                battleUI.AddLog($"{winner.data.heroName} vince! Partita conclusa.");
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
