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
    AuraBlockFirstAttack,         // aura: blocks the first attack received each turn while active
    // --- Item-oriented effects ---
    RemoveAllPoison,              // removes all poison stacks from the caster (antidoto)
    LoseMana,                     // value = mana the caster spends (validated, travaso)
    GainMaxManaThisTurn,          // value = temporary +Intelligence this turn AND +value current mana (lucidita)
    DrawChosenDeck,               // value = cards drawn from a player-chosen deck (pescata) [deferred]
    PeekChosenDeck,               // value = peek top card of a chosen deck, keep or discard (manipolazione) [deferred]
    StealOpponentDiscard,         // value = cards grabbed from opponent discard into own spellbook (saccheggio) [deferred]
    EquipmentStub,                // logs an equipment-dependent effect that is not yet implemented
    // --- Appended item effects (order matters: serialized by index) ---
    DrainManaOpponent,            // value = mana removed from opponent (dispersione, vuoto di mana)
    LoseStamina,                  // value = stamina the caster pays (validated, travaso inverso)
    GainMaxStrengthThisTurn,      // value = temporary +Strength this turn (forza bruta)
    GainMaxAgilityThisTurn        // value = temporary +Agility (max stamina) this turn (scatto felino)
}

[System.Serializable]
public class SpellEffect
{
    public SpellEffectType type;
    public int value;       // primary magnitude (damage, heal, stacks, etc.)
    public int duration;    // 0 = EndOfThisTurn, 1 = UntilNextOpponentTurn (used by debuffs)
}

// Result of attempting to cast a spell or use an item. Effects that require deck/discard/UI
// manipulation are deferred to the BattleManager via these counters.
public class CastResult
{
    public bool success;
    public int drawSpellCount;          // legacy: draw from own spell deck (Prontezza)
    public int drawFromDiscardCount;    // draw from own discard pile (Banchetto)
    public int drawChosenDeckCount;     // draw 1 from a player-chosen deck (pescata)
    public int peekChosenDeckCount;     // peek top of a player-chosen deck (manipolazione)
    public int stealOpponentDiscardCount; // steal from opponent discard into spellbook (saccheggio)
}
