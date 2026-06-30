using UnityEngine;

[System.Serializable]
public class GameState
{
    public HeroState player1;
    public HeroState player2;
    public int currentTurn;
    public HeroState activePlayer;
    public GamePhase phase;

    public GameState(HeroState p1, HeroState p2)
    {
        player1 = p1;
        player2 = p2;
        currentTurn = 1;
        activePlayer = p1;
        phase = GamePhase.ResourceRecovery;
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
