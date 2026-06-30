public enum SpellEffectType
{
    DirectDamage,                 // value = damage dealt to opponent (respects shield/block/defense)
    Heal,                         // value = HP restored to caster
    LoseHP,                       // value = HP the caster pays (cannot drop below 1)
    GainMana,                     // value = mana gained by caster (clamped)
    GainStamina,                  // value = stamina gained by caster (clamped)
    DrainStaminaOpponent,         // value = stamina removed from opponent
    BuffDamageNextAttack,         // value = bonus damage on caster's NEXT weapon attack (consumed)
    BuffDamageThisTurn,           // value = bonus damage on EVERY caster attack this turn
    NextAttackUnblockable,        // caster's next attack ignores defense AND cannot be blocked
    Shield,                       // value = damage points absorbed until caster's next turn
    PreventNextDamage,            // fully blocks the single next attack the caster receives
    ApplyPoison,                  // value = poison stacks applied to opponent
    DebuffDamageOpponentNextTurn, // value = damage reduction on opponent during their next turn
    DrawSpellCard,                // value = number of spell cards drawn (player choice of deck)
    DrawFromDiscard,              // value = number of cards drawn from the discard pile
    AuraWeakenOpponent,           // aura: opponent deals value less damage per attack while active
    AuraBlockFirstAttack          // aura: blocks the first attack received each turn while active
}

[System.Serializable]
public class SpellEffect
{
    public SpellEffectType type;
    public int value;       // primary magnitude (damage, heal, stacks, etc.)
    public int duration;    // 0 = EndOfThisTurn, 1 = UntilNextOpponentTurn (used by debuffs)
}

// Result of attempting to cast a spell. Draw effects are deferred to the BattleManager
// because they require deck/discard/UI manipulation that lives above the pure logic layer.
public class CastResult
{
    public bool success;
    public int drawSpellCount;
    public int drawFromDiscardCount;
}
