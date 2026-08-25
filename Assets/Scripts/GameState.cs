using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameState
{
    public HeroState player1;
    public HeroState player2;
    public int currentTurn;
    public HeroState activePlayer;
    public GamePhase phase;

    /// <summary>
    /// ONE shared discard pile for both players. Last element = top (most recently played card).
    /// Each entry records which player played the card via playerIndex.
    /// </summary>
    public List<DiscardEntry> discardPile = new List<DiscardEntry>();

    public GameState(HeroState p1, HeroState p2)
    {
        player1 = p1;
        player2 = p2;
        currentTurn = 1;
        activePlayer = p1;
        phase = GamePhase.ResourceRecovery;

        // Wire player indices and the shared discard pile reference.
        p1.playerIndex = 1;
        p2.playerIndex = 2;
        p1.sharedDiscardPile = discardPile;
        p2.sharedDiscardPile = discardPile;
    }

    public void AdvancePhase()
    {
        if (phase == GamePhase.EndPhase)
        {
            phase = GamePhase.ResourceRecovery;
            currentTurn++;
            activePlayer = (activePlayer == player1) ? player2 : player1;
        }
        else
        {
            phase = (GamePhase)((int)phase + 1);
        }
    }
}
