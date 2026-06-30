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

        private GameState gameState;
        private TurnManager turnManager;
        private int prepDrawsThisPhase;

        private HeroClass? selectedP1;
        private HeroClass? selectedP2;

        private HeroState pendingDrawHero;
        private int pendingDrawCount;

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
            if (drawChoiceSpellButton != null) drawChoiceSpellButton.onClick.AddListener(() => OnDrawChoice("Spell"));
            if (drawChoiceEquipButton != null) drawChoiceEquipButton.onClick.AddListener(() => OnDrawChoice("Equip"));
            if (drawChoiceItemButton != null) drawChoiceItemButton.onClick.AddListener(() => OnDrawChoice("Item"));
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

            DrawCardFromDeck(gameState.activePlayer);
            prepDrawsThisPhase++;
            if (prepDrawsThisPhase >= 2 && drawPrepButton != null) drawPrepButton.interactable = false;
            RefreshAll();
        }

        public void OnEquipPrepPressed()
        {
            battleUI.AddLog($"{gameState.activePlayer.data.heroName} equipaggia un oggetto (sistema equipaggiamento da implementare).");
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

            if (CombatActions.TryWeaponAttack(attacker, defender, 2, 4))
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
            DrawCardFromDeck(gameState.activePlayer);
            turnManager.RunEndPhase(gameState.activePlayer, EndPhaseChoice.DrawExtraCard);
            EndRoundForActive();
        }

        // Discards the active hero's repeatable cards (round end), then advances to the next hero.
        private void EndRoundForActive()
        {
            HeroState active = gameState.activePlayer;
            for (int i = active.hand.Count - 1; i >= 0; i--)
            {
                if (active.hand[i] is MagicData m && m.magicType == MagicType.Repeatable)
                {
                    active.hand.RemoveAt(i);
                    active.discardPile.Add(m);
                }
            }
            gameState.AdvancePhase(); // wraps to next hero ResourceRecovery
            OnPhaseEntered();
        }

        // ---------- Card usage ----------
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

            // Resolve deferred draws.
            for (int i = 0; i < result.drawFromDiscardCount; i++) DrawCardFromDiscard(owner);
            if (result.drawSpellCount > 0)
            {
                pendingDrawHero = owner;
                pendingDrawCount = result.drawSpellCount;
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(true);
            }

            // Apply per-magic-type bookkeeping.
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
                    // stays in hand; discarded at round end
                    break;
            }

            RefreshAll();
            CheckForWinner();
        }

        // ---------- Draw choice (Prontezza) ----------
        private void OnDrawChoice(string choice)
        {
            if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);
            if (pendingDrawHero == null) return;

            if (choice == "Spell")
            {
                for (int i = 0; i < pendingDrawCount; i++) DrawCardFromDeck(pendingDrawHero);
            }
            else
            {
                battleUI.AddLog($"Pescaggio {choice}: non ancora implementato (solo Spell disponibili al momento).");
            }
            pendingDrawHero = null;
            pendingDrawCount = 0;
            RefreshAll();
        }

        // ---------- Deck / discard ----------
        private void DrawCardFromDeck(HeroState hero)
        {
            if (hero.magicDeck.Count == 0)
            {
                // Reshuffle the discard pile back into the deck (no duplicates exist).
                if (hero.discardPile.Count > 0)
                {
                    hero.magicDeck.AddRange(hero.discardPile);
                    hero.discardPile.Clear();
                    battleUI.AddLog($"{hero.data.heroName} rimescola gli scarti nel mazzo.");
                }
                else
                {
                    battleUI.AddLog($"{hero.data.heroName} non ha carte da pescare!");
                    return;
                }
            }
            int index = Random.Range(0, hero.magicDeck.Count);
            CardData card = hero.magicDeck[index];
            hero.magicDeck.RemoveAt(index);
            hero.hand.Add(card);
            battleUI.AddLog($"{hero.data.heroName} pesca {card.cardName}.");
        }

        private void DrawCardFromDiscard(HeroState hero)
        {
            if (hero.discardPile.Count == 0)
            {
                battleUI.AddLog($"{hero.data.heroName} non ha carte negli scarti.");
                return;
            }
            int index = Random.Range(0, hero.discardPile.Count);
            CardData card = hero.discardPile[index];
            hero.discardPile.RemoveAt(index);
            hero.hand.Add(card);
            battleUI.AddLog($"{hero.data.heroName} recupera {card.cardName} dagli scarti.");
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
            battleUI.RefreshHand(gameState.player1, true);
            battleUI.RefreshHand(gameState.player2, false);
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
