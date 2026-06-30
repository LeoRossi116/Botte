public enum SpellEffectType
{
    DirectDamage,           // deal damageValue damage to opponent
    BuffStrengthSelf,       // add modifier to self: +amount Strength, given duration
    BuffDamageNextAttack,   // add modifier to self: +amount DamageBonus, UntilNextAttack
    PreventNextDamage,      // grant a "Shield" that fully blocks the next attack (see Task 4)
    DrainHP,                // deal damageValue damage to opponent AND heal self by healValue
    DebuffStrengthOpponent, // add modifier to opponent: -amount Strength, given duration
    DebuffAgilityOpponent,  // add modifier to opponent: -amount Agility, given duration
    GainStamina,            // add amount to self currentStamina, clamped to max
    GainMana                // add amount to self currentMana, clamped to max
}
