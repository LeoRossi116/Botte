using UnityEngine;

public enum PreparationChoice { DrawCard, EquipItem }
public enum EndPhaseChoice { Rest, DrawExtraCard }

public class TurnManager
{
    private const int STAMINA_RECOVERY = 3;
    private const int MANA_RECOVERY = 2;
    private const int HP_RECOVERY = 0;

    private GameState gameState;

    public TurnManager(GameState state)
    {
        this.gameState = state;
    }

    public void RunResourceRecoveryPhase(HeroState hero, HeroState opponent)
    {
        if (opponent != null)
        {
            opponent.ExpireModifiers(ModifierDuration.UntilNextOpponentTurn);
        }

        if (hero.poisonStacks > 0)
        {
            int poisonDamage = hero.poisonStacks;
            hero.currentHP = Mathf.Max(0, hero.currentHP - poisonDamage);
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} subisce {poisonDamage} danni da Veleno (HP rimanenti: {hero.currentHP}).");
        }

        // Stamina Recovery
        int maxStamina = hero.GetModifiedAgility(); // Agility can be modified!
        int staminaGained = Mathf.Min(STAMINA_RECOVERY, maxStamina - hero.currentStamina);
        if (staminaGained > 0)
        {
            hero.currentStamina = Mathf.Min(maxStamina, hero.currentStamina + staminaGained);
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} recupera {staminaGained} Stamina ({hero.currentStamina}/{maxStamina}).");
        }
        else
        {
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} è già al massimo di Stamina ({hero.currentStamina}/{maxStamina}), nessun recupero.");
        }

        // Mana Recovery
        int maxMana = hero.GetModifiedIntelligence(); // Intelligence can be modified!
        int manaGained = Mathf.Min(MANA_RECOVERY, maxMana - hero.currentMana);
        if (manaGained > 0)
        {
            hero.currentMana = Mathf.Min(maxMana, hero.currentMana + manaGained);
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} recupera {manaGained} Mana ({hero.currentMana}/{maxMana}).");
        }
        else
        {
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} è già al massimo di Mana ({hero.currentMana}/{maxMana}), nessun recupero.");
        }

        // HP Recovery
        Debug.Log($"[ResourceRecovery] {hero.data.heroName}: nessun recupero HP in questa fase.");
    }

    public void RunPreparationPhase(HeroState hero, PreparationChoice choice)
    {
        if (choice == PreparationChoice.DrawCard)
        {
            Debug.Log($"[Preparation] {hero.data.heroName} pesca una carta (logica di pescaggio da implementare).");
        }
        else if (choice == PreparationChoice.EquipItem)
        {
            Debug.Log($"[Preparation] {hero.data.heroName} equipaggia un oggetto (logica di equipaggiamento da implementare).");
        }
    }

    public void RunCombatPhase()
    {
        Debug.Log("[Combat] Inizio fase di Combattimento. Usa CombatActions per agire.");
    }

    public void RunEndPhase(HeroState hero, EndPhaseChoice choice)
    {
        if (choice == EndPhaseChoice.Rest)
        {
            hero.currentHP = Mathf.Min(hero.data.maxHP, hero.currentHP + 2);
            hero.currentMana = Mathf.Min(hero.GetModifiedIntelligence(), hero.currentMana + 1); // Clamp to modified stats just to be safe
            hero.currentStamina = Mathf.Min(hero.GetModifiedAgility(), hero.currentStamina + 1);
            if (hero.poisonStacks > 0)
            {
                hero.poisonStacks = Mathf.Max(0, hero.poisonStacks - 1);
            }

            Debug.Log($"[EndPhase] {hero.data.heroName} riposa: +2 HP, +1 Mana, +1 Stamina, -1 Veleno (se presente). Stato attuale: HP {hero.currentHP}/{hero.data.maxHP}, Mana {hero.currentMana}/{hero.data.intelligence}, Stamina {hero.currentStamina}/{hero.data.agility}.");
        }
        else if (choice == EndPhaseChoice.DrawExtraCard)
        {
            Debug.Log($"[EndPhase] {hero.data.heroName} pesca una carta extra a fine turno (logica di pescaggio da implementare).");
        }

        if (hero.hand != null && hero.hand.Count > 7)
        {
            Debug.Log($"[EndPhase] {hero.data.heroName} ha più di 7 carte in mano, scarto fino a 7 da implementare.");
        }

        hero.ExpireModifiers(ModifierDuration.EndOfThisTurn);
    }
}
