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

        private GameState gameState;
        private TurnManager turnManager;
        private int prepDrawsThisPhase;

        private void Start()
        {
            if (gameManager != null)
            {
                gameState = gameManager.gameState;
                turnManager = new TurnManager(gameState);
            }

            if (gameState != null && battleUI != null)
            {
                // Kick off first turn
                OnPhaseEntered();
            }
        }

        private void OnPhaseEntered()
        {
            if (gameState == null || battleUI == null) return;

            HeroState active = gameState.activePlayer;
            HeroState opponent = (active == gameState.player1) ? gameState.player2 : gameState.player1;

            if (gameState.phase == GamePhase.ResourceRecovery)
            {
                turnManager.RunResourceRecoveryPhase(active, opponent);
                gameState.AdvancePhase(); // Now Preparation Phase
            }

            // Update UI Texts
            battleUI.turnText.text = $"Turn {gameState.currentTurn} — {active.data.heroName}'s turn";
            battleUI.phaseText.text = $"Phase: {gameState.phase}";

            // Toggle Button Containers
            if (battleUI.prepButtonsContainer != null) battleUI.prepButtonsContainer.SetActive(gameState.phase == GamePhase.Preparation);
            if (battleUI.combatButtonsContainer != null) battleUI.combatButtonsContainer.SetActive(gameState.phase == GamePhase.Combat);
            if (battleUI.endButtonsContainer != null) battleUI.endButtonsContainer.SetActive(gameState.phase == GamePhase.EndPhase);

            if (gameState.phase == GamePhase.Preparation)
            {
                prepDrawsThisPhase = 0;
                if (drawPrepButton != null) drawPrepButton.interactable = true;
                battleUI.AddLog($"=== {active.data.heroName}'s Preparation Phase ===");
            }
            else if (gameState.phase == GamePhase.Combat)
            {
                battleUI.AddLog($"=== {active.data.heroName}'s Combat Phase ===");
            }
            else if (gameState.phase == GamePhase.EndPhase)
            {
                battleUI.AddLog($"=== {active.data.heroName}'s End Phase ===");
            }

            // Refresh Hero Panels and Hands
            battleUI.RefreshHero(gameState.player1, true);
            battleUI.RefreshHero(gameState.player2, false);
            battleUI.RefreshHand(gameState.player1, true);
            battleUI.RefreshHand(gameState.player2, false);
        }

        public void OnDrawPrepPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Preparation) return;
            if (prepDrawsThisPhase >= 2) return;

            DrawCard(gameState.activePlayer);
            prepDrawsThisPhase++;

            if (prepDrawsThisPhase >= 2)
            {
                if (drawPrepButton != null) drawPrepButton.interactable = false;
            }

            battleUI.RefreshHero(gameState.activePlayer, gameState.activePlayer == gameState.player1);
            battleUI.RefreshHand(gameState.activePlayer, gameState.activePlayer == gameState.player1);
        }

        public void OnEquipPrepPressed()
        {
            battleUI.AddLog($"{gameState.activePlayer.data.heroName} equipaggia un oggetto (logica di equipaggiamento da implementare).");
        }

        public void OnFinishPrepPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Preparation) return;
            gameState.AdvancePhase(); // Moves to Combat
            OnPhaseEntered();
        }

        public void OnAttackPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Combat) return;

            HeroState attacker = gameState.activePlayer;
            HeroState defender = (attacker == gameState.player1) ? gameState.player2 : gameState.player1;

            bool success = CombatActions.TryWeaponAttack(attacker, defender, staminaCost: 2, weaponDamage: 4);
            if (success)
            {
                battleUI.RefreshHero(defender, defender == gameState.player1);
                battleUI.RefreshHero(attacker, attacker == gameState.player1);

                if (defender.currentHP <= 0)
                {
                    EndBattle(attacker);
                }
            }
        }

        public void OnEndTurnPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.Combat) return;
            gameState.AdvancePhase(); // Moves to EndPhase
            OnPhaseEntered();
        }

        public void OnSleepPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.EndPhase) return;

            turnManager.RunEndPhase(gameState.activePlayer, EndPhaseChoice.Rest);
            gameState.AdvancePhase(); // Wraps to ResourceRecovery for next turn
            OnPhaseEntered();
        }

        public void OnDrawExtraPressed()
        {
            if (gameState == null || gameState.phase != GamePhase.EndPhase) return;

            DrawCard(gameState.activePlayer);
            turnManager.RunEndPhase(gameState.activePlayer, EndPhaseChoice.DrawExtraCard);
            gameState.AdvancePhase(); // Wraps to ResourceRecovery for next turn
            OnPhaseEntered();
        }

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

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;

            bool castSuccess = SpellActions.TryCastSpell(owner, opponent, spell);
            if (castSuccess)
            {
                owner.hand.Remove(spell);
                owner.discardPile.Add(spell);

                battleUI.RefreshHero(gameState.player1, true);
                battleUI.RefreshHero(gameState.player2, false);
                battleUI.RefreshHand(owner, owner == gameState.player1);

                if (opponent.currentHP <= 0)
                {
                    EndBattle(owner);
                }
            }
        }

        private void DrawCard(HeroState hero)
        {
            if (hero.magicDeck == null || hero.magicDeck.Count == 0)
            {
                battleUI.AddLog($"{hero.data.heroName} non ha più carte nel deck!");
                return;
            }

            int index = Random.Range(0, hero.magicDeck.Count);
            CardData card = hero.magicDeck[index];
            hero.magicDeck.RemoveAt(index);
            hero.hand.Add(card);

            battleUI.AddLog($"{hero.data.heroName} pesca {card.cardName}.");
        }

        private void EndBattle(HeroState winner)
        {
            if (attackButton != null) attackButton.interactable = false;
            if (endTurnButton != null) endTurnButton.interactable = false;
            if (drawPrepButton != null) drawPrepButton.interactable = false;
            if (finishPrepButton != null) finishPrepButton.interactable = false;
            if (sleepButton != null) sleepButton.interactable = false;
            if (drawExtraButton != null) drawExtraButton.interactable = false;

            if (battleUI != null)
            {
                battleUI.winnerOverlay.SetActive(true);
                battleUI.winnerText.text = $"{winner.data.heroName} wins!";
                battleUI.AddLog($"{winner.data.heroName} wins! Battle over.");
            }
        }
    }
}
