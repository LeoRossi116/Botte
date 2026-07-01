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

        // Reset per-turn defensive state for the hero whose turn is starting.
        hero.hasShield = false;
        hero.shieldAmount = 0;
        hero.nextAttackUnblockable = false;
        hero.blockedFirstAttackThisTurn = false;
        hero.exhaustedThisRound.Clear();

        // Per-turn attack / card-type tracking.
        hero.attackedLastTurn = hero.attackedThisTurn;
        hero.attackedThisTurn = false;
        hero.cardTypesUsedThisTurn.Clear();
        hero.manaUsedThisTurn = 0;

        // Poison tick (unless immune, e.g. passo del non-morto).
        if (hero.poisonStacks > 0)
        {
            if (hero.HasEquipEffect(EquipEffect.PoisonImmune))
            {
                hero.poisonStacks = 0;
                Debug.Log($"[ResourceRecovery] {hero.data.heroName} è immune al Veleno: stack rimossi.");
            }
            else
            {
                int poisonDamage = hero.poisonStacks;
                hero.currentHP = Mathf.Max(0, hero.currentHP - poisonDamage);
                hero.poisonStacks = Mathf.Max(0, hero.poisonStacks - 1);
                Debug.Log($"[ResourceRecovery] {hero.data.heroName} subisce {poisonDamage} danni da Veleno (HP rimanenti: {hero.currentHP}, Veleno: {hero.poisonStacks}).");
            }
        }

        // Stamina Recovery
        int maxStamina = hero.GetModifiedAgility();
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

        // Stun / pending stamina penalty (maglio colossale, ecc.).
        if (hero.pendingStaminaPenalty > 0)
        {
            hero.currentStamina = Mathf.Max(0, hero.currentStamina - hero.pendingStaminaPenalty);
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} è stordito: -{hero.pendingStaminaPenalty} Stamina ({hero.currentStamina}/{maxStamina}).");
            hero.pendingStaminaPenalty = 0;
        }

        // Mana Recovery
        int maxMana = hero.GetModifiedIntelligence();
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

        ApplyTurnStartEquipment(hero, opponent, maxMana);

        // HP Recovery
        Debug.Log($"[ResourceRecovery] {hero.data.heroName}: nessun recupero HP in questa fase.");
    }

    // Equipment effects that trigger at the start of the owner's turn.
    private void ApplyTurnStartEquipment(HeroState hero, HeroState opponent, int maxMana)
    {
        int extraMana = 0;
        if (hero.HasEquipEffect(EquipEffect.ExtraManaEachTurn))
            extraMana += hero.SumEquipEffect(EquipEffect.ExtraManaEachTurn);
        if (!hero.attackedLastTurn)
        {
            var med = hero.FindEquip(EquipEffect.ManaIfNoAttack);
            if (med != null) extraMana += med.effectValue;
        }
        if (opponent != null && hero.currentHP < opponent.currentHP)
        {
            var rit = hero.FindEquip(EquipEffect.ManaIfLowerHP);
            if (rit != null) extraMana += rit.effectValue;
        }
        if (extraMana > 0)
        {
            hero.currentMana = Mathf.Min(maxMana, hero.currentMana + extraMana);
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} guadagna {extraMana} Mana extra dall'equipaggiamento ({hero.currentMana}/{maxMana}).");
        }

        int shield = 0;
        if (hero.HasEquipEffect(EquipEffect.ShieldEachTurn))
            shield += hero.SumEquipEffect(EquipEffect.ShieldEachTurn);
        var egida = hero.FindEquip(EquipEffect.ChanceShieldEachTurn);
        if (egida != null && Random.Range(0, 100) < egida.effectValue2) shield += egida.effectValue;
        if (shield > 0)
        {
            hero.shieldAmount += shield;
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} ottiene {shield} scudo dall'equipaggiamento (totale: {hero.shieldAmount}).");
        }

        if (hero.HasEquipEffect(EquipEffect.BlockEachRound))
        {
            hero.hasShield = true;
            Debug.Log($"[ResourceRecovery] {hero.data.heroName} ottiene un blocco completo per questo turno (equipaggiamento).");
        }
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
                // Sleeping removes one extra poison stack on top of the per-round decay.
                hero.poisonStacks = Mathf.Max(0, hero.poisonStacks - 1);
            }

            Debug.Log($"[EndPhase] {hero.data.heroName} riposa: +2 HP, +1 Mana, +1 Stamina, -1 Veleno extra (se presente). Stato attuale: HP {hero.currentHP}/{hero.data.maxHP}, Mana {hero.currentMana}/{hero.data.intelligence}, Stamina {hero.currentStamina}/{hero.data.agility}.");
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

        // Silence lasts through the silenced hero's turn, then clears.
        if (hero.isSilenced)
        {
            hero.isSilenced = false;
            Debug.Log($"[EndPhase] {hero.data.heroName} non è più silenziato.");
        }
    }
}
