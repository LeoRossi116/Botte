using UnityEngine;

public class GameManager : MonoBehaviour
{
    [System.NonSerialized] public GameState gameState;

    // Builds a fresh HeroData for the requested class with the canonical stats.
    public static HeroData CreateHeroData(HeroClass heroClass)
    {
        HeroData d = ScriptableObject.CreateInstance<HeroData>();
        d.heroClass = heroClass;
        switch (heroClass)
        {
            case HeroClass.Warrior:
                d.heroName = "Garrik"; d.maxHP = 24; d.strength = 3; d.intelligence = 3; d.agility = 6; break;
            case HeroClass.Mage:
                d.heroName = "Lyra"; d.maxHP = 16; d.strength = 1; d.intelligence = 5; d.agility = 5; break;
            case HeroClass.Rogue:
                d.heroName = "Raven"; d.maxHP = 20; d.strength = 3; d.intelligence = 1; d.agility = 8; break;
            case HeroClass.Necro:
                d.heroName = "Mortis"; d.maxHP = 20; d.strength = 2; d.intelligence = 4; d.agility = 4; break;
        }
        return d;
    }

    // Creates a brand new game state from the two chosen classes.
    public void InitGame(HeroClass p1Class, HeroClass p2Class)
    {
        HeroState player1 = new HeroState(CreateHeroData(p1Class));
        HeroState player2 = new HeroState(CreateHeroData(p2Class));

        player1.magicDeck = DeckBuilder.BuildSpellDeck(p1Class);
        player2.magicDeck = DeckBuilder.BuildSpellDeck(p2Class);

        gameState = new GameState(player1, player2);
        gameState.itemDeck = DeckBuilder.BuildItemDeck();

        Debug.Log($"Game initialized. P1: {player1.data.heroName} ({p1Class}) vs P2: {player2.data.heroName} ({p2Class}). Item deck: {gameState.itemDeck.Count} carte.");
    }
}
