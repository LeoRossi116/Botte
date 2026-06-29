using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameState gameState;

    private void Awake()
    {
        HeroData p1Data = ScriptableObject.CreateInstance<HeroData>();
        p1Data.heroName = "Garrik";
        p1Data.heroClass = HeroClass.Warrior;
        p1Data.maxHP = 24;
        p1Data.strength = 5;
        p1Data.intelligence = 3;
        p1Data.agility = 6;

        HeroData p2Data = ScriptableObject.CreateInstance<HeroData>();
        p2Data.heroName = "Lyra";
        p2Data.heroClass = HeroClass.Mage;
        p2Data.maxHP = 16;
        p2Data.strength = 1;
        p2Data.intelligence = 5;
        p2Data.agility = 5;

        HeroState player1 = new HeroState(p1Data);
        HeroState player2 = new HeroState(p2Data);

        gameState = new GameState(player1, player2);

        Debug.Log("Game initialized.");
        Debug.Log($"Player 1: {player1.data.heroName} ({player1.data.heroClass}) — HP: {player1.currentHP}/{player1.data.maxHP}, Mana: {player1.currentMana}/{player1.data.intelligence}, Stamina: {player1.currentStamina}/{player1.data.agility}");
        Debug.Log($"Player 2: {player2.data.heroName} ({player2.data.heroClass}) — HP: {player2.currentHP}/{player2.data.maxHP}, Mana: {player2.currentMana}/{player2.data.intelligence}, Stamina: {player2.currentStamina}/{player2.data.agility}");
        Debug.Log($"Phase: {gameState.phase}");
    }
}
