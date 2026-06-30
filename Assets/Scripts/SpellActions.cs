using UnityEngine;

public static class SpellActions
{
    public static CastResult TryCastSpell(HeroState caster, HeroState opponent, MagicData spell)
    {
        CastResult result = new CastResult();
        if (spell == null) return result;

        // Class-lock validation.
        if (spell.cardClass != CardClass.Shared && spell.cardClass.ToString() != caster.data.heroClass.ToString())
        {
            Debug.Log($"[Combat] {caster.data.heroName} non può lanciare {spell.cardName}: non è della sua classe.");
            return result;
        }

        // Sum HP cost (LoseHP effects) — the caster may never reduce itself below 1 HP.
        int hpCost = 0;
        foreach (SpellEffect e in spell.effects)
        {
            if (e.type == SpellEffectType.LoseHP) hpCost += e.value;
        }
        if (hpCost > 0 && caster.currentHP - hpCost < 1)
        {
            Debug.Log($"[Combat] {caster.data.heroName} non ha abbastanza HP per lanciare {spell.cardName} (richiesti: {hpCost}, disponibili: {caster.currentHP}).");
            return result;
        }

        // Resource check.
        if (caster.currentMana < spell.manaCost || caster.currentStamina < spell.staminaCost)
        {
            Debug.Log($"[Combat] {caster.data.heroName} non ha abbastanza risorse per lanciare {spell.cardName} (Mana: {caster.currentMana}/{spell.manaCost}, Stamina: {caster.currentStamina}/{spell.staminaCost}).");
            return result;
        }

        // Pay costs.
        caster.currentMana = Mathf.Max(0, caster.currentMana - spell.manaCost);
        caster.currentStamina = Mathf.Max(0, caster.currentStamina - spell.staminaCost);

        Debug.Log($"[Combat] {caster.data.heroName} lancia {spell.cardName} ({spell.magicType}): -{spell.manaCost} Mana, -{spell.staminaCost} Stamina.");

        foreach (SpellEffect e in spell.effects)
        {
            ApplyEffect(caster, opponent, spell, e, result);
        }

        result.success = true;
        return result;
    }

    private static void ApplyEffect(HeroState caster, HeroState opponent, MagicData spell, SpellEffect e, CastResult result)
    {
        switch (e.type)
        {
            case SpellEffectType.DirectDamage:
            {
                int dealt = CombatActions.DealDamage(caster, opponent, e.value, false, false);
                Debug.Log($"[Combat] {spell.cardName}: {dealt} danni a {opponent.data.heroName} (HP rimanenti: {opponent.currentHP}).");
                break;
            }
            case SpellEffectType.Heal:
            {
                caster.currentHP = Mathf.Min(caster.data.maxHP, caster.currentHP + e.value);
                Debug.Log($"[Combat] {caster.data.heroName} recupera {e.value} HP ({caster.currentHP}/{caster.data.maxHP}).");
                break;
            }
            case SpellEffectType.LoseHP:
            {
                caster.currentHP = Mathf.Max(1, caster.currentHP - e.value);
                Debug.Log($"[Combat] {caster.data.heroName} paga {e.value} HP ({caster.currentHP}/{caster.data.maxHP}).");
                break;
            }
            case SpellEffectType.GainMana:
            {
                int maxMana = caster.GetModifiedIntelligence();
                caster.currentMana = Mathf.Min(maxMana, caster.currentMana + e.value);
                Debug.Log($"[Combat] {caster.data.heroName} guadagna {e.value} Mana ({caster.currentMana}/{maxMana}).");
                break;
            }
            case SpellEffectType.GainStamina:
            {
                int maxStamina = caster.GetModifiedAgility();
                caster.currentStamina = Mathf.Min(maxStamina, caster.currentStamina + e.value);
                Debug.Log($"[Combat] {caster.data.heroName} guadagna {e.value} Stamina ({caster.currentStamina}/{maxStamina}).");
                break;
            }
            case SpellEffectType.DrainStaminaOpponent:
            {
                opponent.currentStamina = Mathf.Max(0, opponent.currentStamina - e.value);
                Debug.Log($"[Combat] {opponent.data.heroName} perde {e.value} Stamina ({opponent.currentStamina}/{opponent.GetModifiedAgility()}).");
                break;
            }
            case SpellEffectType.BuffDamageNextAttack:
            {
                caster.AddModifier(new StatModifier(spell.cardName, ModifierStat.DamageBonus, e.value, ModifierDuration.UntilNextAttack));
                break;
            }
            case SpellEffectType.BuffDamageThisTurn:
            {
                caster.AddModifier(new StatModifier(spell.cardName, ModifierStat.DamageBonus, e.value, ModifierDuration.EndOfThisTurn));
                break;
            }
            case SpellEffectType.NextAttackUnblockable:
            {
                caster.nextAttackUnblockable = true;
                Debug.Log($"[Combat] Il prossimo attacco di {caster.data.heroName} non può essere bloccato.");
                break;
            }
            case SpellEffectType.Shield:
            {
                caster.shieldAmount += e.value;
                Debug.Log($"[Combat] {caster.data.heroName} ottiene {e.value} scudo (totale: {caster.shieldAmount}).");
                break;
            }
            case SpellEffectType.PreventNextDamage:
            {
                caster.hasShield = true;
                Debug.Log($"[Combat] {caster.data.heroName} bloccherà completamente il prossimo attacco subito.");
                break;
            }
            case SpellEffectType.ApplyPoison:
            {
                opponent.poisonStacks += e.value;
                Debug.Log($"[Combat] {opponent.data.heroName} subisce {e.value} Veleno (stack totali: {opponent.poisonStacks}).");
                break;
            }
            case SpellEffectType.DebuffDamageOpponentNextTurn:
            {
                opponent.AddModifier(new StatModifier(spell.cardName, ModifierStat.DamageBonus, -e.value, ModifierDuration.UntilNextOpponentTurn));
                break;
            }
            case SpellEffectType.DrawSpellCard:
            {
                result.drawSpellCount += e.value;
                break;
            }
            case SpellEffectType.DrawFromDiscard:
            {
                result.drawFromDiscardCount += e.value;
                break;
            }
            case SpellEffectType.AuraWeakenOpponent:
            {
                caster.auraWeakenOpponent += e.value;
                Debug.Log($"[Combat] Aura attiva: l'avversario di {caster.data.heroName} infligge {e.value} danni in meno per attacco.");
                break;
            }
            case SpellEffectType.AuraBlockFirstAttack:
            {
                caster.auraBlockFirstAttack = true;
                Debug.Log($"[Combat] Aura attiva: {caster.data.heroName} bloccherà il primo attacco di ogni turno.");
                break;
            }
        }
    }
}
