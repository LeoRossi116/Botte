using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Botte.Core
{
    public class BattleManager : MonoBehaviour
    {
        public GameManager gameManager;
        public Botte.UI.BattleUI battleUI;
        public Button attackButton;
        public Button endTurnButton;

        private GameState gameState;

        private void Start()
        {
            if (gameManager != null)
            {
                gameState = gameManager.gameState;
            }

            if (gameState != null && battleUI != null)
            {
                battleUI.RefreshHero(gameState.player1, true);
                battleUI.RefreshHero(gameState.player2, false);
                battleUI.AddLog("Battle started! Turn 1 — Player 1 attacks first.");
            }
        }

        public void OnAttackPressed()
        {
            if (gameState == null || battleUI == null) return;

            HeroState attacker = gameState.activePlayer;
            HeroState defender = (attacker == gameState.player1) ? gameState.player2 : gameState.player1;

            int damage = 1 + Mathf.FloorToInt(attacker.data.strength / 3);
            defender.currentHP = Mathf.Max(0, defender.currentHP - damage);

            battleUI.AddLog($"{attacker.data.heroName} hits {defender.data.heroName} for {damage} damage. ({defender.data.heroName} HP: {defender.currentHP}/{defender.data.maxHP})");
            battleUI.RefreshHero(defender, defender == gameState.player1);

            if (defender.currentHP <= 0)
            {
                EndBattle(attacker);
            }
        }

        public void OnEndTurnPressed()
        {
            if (gameState == null || battleUI == null) return;

            HeroState oldActive = gameState.activePlayer;
            
            oldActive.currentStamina = Mathf.Min(oldActive.currentStamina + 3, oldActive.data.agility);
            oldActive.currentMana = Mathf.Min(oldActive.currentMana + 2, oldActive.data.intelligence);

            gameState.activePlayer = (oldActive == gameState.player1) ? gameState.player2 : gameState.player1;

            if (gameState.activePlayer == gameState.player1)
            {
                gameState.currentTurn++;
            }

            battleUI.turnText.text = $"Turn {gameState.currentTurn} — {gameState.activePlayer.data.heroName}'s turn";
            battleUI.phaseText.text = $"Phase: {gameState.phase}";

            battleUI.AddLog($"--- Turn {gameState.currentTurn}: {gameState.activePlayer.data.heroName}'s turn ---");
            battleUI.RefreshHero(gameState.player1, true);
            battleUI.RefreshHero(gameState.player2, false);
        }

        private void EndBattle(HeroState winner)
        {
            if (attackButton != null) attackButton.interactable = false;
            if (endTurnButton != null) endTurnButton.interactable = false;

            if (battleUI != null)
            {
                battleUI.winnerOverlay.SetActive(true);
                battleUI.winnerText.text = $"{winner.data.heroName} wins!";
                battleUI.AddLog($"{winner.data.heroName} wins! Battle over.");
            }
        }
    }
}
