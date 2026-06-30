using UnityEngine;

public class GameManager : MonoBehaviour
{
    [System.NonSerialized] public GameState gameState;

    private void Awake()
    {
        HeroData p1Data = ScriptableObject.CreateInstance<HeroData>();
        p1Data.heroName = "Garrik";
        p1Data.heroClass = HeroClass.Warrior;
        p1Data.maxHP = 24;
        p1Data.strength = 6; // corrected
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

        // Build spell decks
        player1.magicDeck = DeckBuilder.BuildSpellDeck(player1.data.heroClass);
        player2.magicDeck = DeckBuilder.BuildSpellDeck(player2.data.heroClass);

        gameState = new GameState(player1, player2);

        Debug.Log("Game initialized.");
        Debug.Log($"Player 1: {player1.data.heroName} ({player1.data.heroClass}) — HP: {player1.currentHP}/{player1.data.maxHP}, Mana: {player1.currentMana}/{player1.data.intelligence}, Stamina: {player1.currentStamina}/{player1.data.agility}");
        Debug.Log($"Player 2: {player2.data.heroName} ({player2.data.heroClass}) — HP: {player2.currentHP}/{player2.data.maxHP}, Mana: {player2.currentMana}/{player2.data.intelligence}, Stamina: {player2.currentStamina}/{player2.data.agility}");
        Debug.Log($"Phase: {gameState.phase}");
    }

    private void Start()
    {
        // RunTestSequence();
    }

    private void RunTestSequence()
    {
        // 1. Create a TurnManager with the GameState.
        TurnManager turnManager = new TurnManager(gameState);

        // Adjust starting stamina of Garrik to 2 so the recovery shows 3 Stamina being recovered (up to 5/6)
        gameState.player1.currentStamina = 2;

        HeroState actingHero = gameState.activePlayer;
        HeroState opponent = (actingHero == gameState.player1) ? gameState.player2 : gameState.player1;

        // 2. Call RunResourceRecoveryPhase on the active player, advance phase.
        turnManager.RunResourceRecoveryPhase(actingHero, opponent);
        gameState.AdvancePhase();

        // 3. Call RunPreparationPhase twice on the active player: once with DrawCard, once with EquipItem. Advance phase.
        turnManager.RunPreparationPhase(actingHero, PreparationChoice.DrawCard);
        turnManager.RunPreparationPhase(actingHero, PreparationChoice.EquipItem);
        gameState.AdvancePhase();

        // 4. Call RunCombatPhase(). Then directly call CombatActions.TryWeaponAttack once with placeholder values (e.g. staminaCost 2, weaponDamage 4) to demonstrate it working. Advance phase.
        turnManager.RunCombatPhase();
        CombatActions.TryWeaponAttack(actingHero, opponent, 2, 4);

        // Spell cast demonstration: casts the first spell in that hero's deck.
        if (actingHero.magicDeck != null && actingHero.magicDeck.Count > 0)
        {
            SpellActions.TryCastSpell(actingHero, opponent, (MagicData)actingHero.magicDeck[0]);
        }

        gameState.AdvancePhase();

        // 5. Call RunEndPhase on the active player with choice Rest. Advance phase (this should wrap to ResourceRecovery and increment currentTurn).
        turnManager.RunEndPhase(actingHero, EndPhaseChoice.Rest);
        gameState.AdvancePhase();

        // 6. Log the final currentTurn value to confirm the wrap worked.
        Debug.Log($"Turno avanzato: {gameState.currentTurn}");
    }
}
