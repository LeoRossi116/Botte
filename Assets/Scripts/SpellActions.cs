using UnityEngine;

public static class SpellActions
{
    private static ModifierDuration GetDuration(int turns)
    {
        if (turns == 0) return ModifierDuration.EndOfThisTurn;
        return ModifierDuration.UntilNextOpponentTurn;
    }

    public static bool TryCastSpell(HeroState caster, HeroState opponent, MagicData spell)
    {
        if (spell == null) return false;

        // Class-lock validation
        if (spell.cardClass != CardClass.Shared && spell.cardClass.ToString() != caster.data.heroClass.ToString())
        {
            Debug.Log($"[Combat] {caster.data.heroName} non può lanciare {spell.cardName}: non è della sua classe.");
            return false;
        }

        // Dedicated check for "Patto di sangue"
        if (spell.cardName == "Patto di sangue")
        {
            if (caster.currentHP - 2 < 1)
            {
                Debug.Log($"[Combat] {caster.data.heroName} non ha abbastanza HP per lanciare Patto di sangue (richiesti: 2, disponibili: {caster.currentHP}).");
                return false;
            }

            // Pay costs (0 mana/stamina for Patto di sangue, but check just in case)
            if (caster.currentMana < spell.manaCost || caster.currentStamina < spell.staminaCost)
            {
                Debug.Log($"[Combat] {caster.data.heroName} non ha abbastanza risorse per lanciare Patto di sangue (Mana: {caster.currentMana}/{spell.manaCost}, Stamina: {caster.currentStamina}/{spell.staminaCost}).");
                return false;
            }

            caster.currentMana = Mathf.Max(0, caster.currentMana - spell.manaCost);
            caster.currentStamina = Mathf.Max(0, caster.currentStamina - spell.staminaCost);

            caster.currentHP -= 2;
            int maxMana = caster.GetModifiedIntelligence();
            caster.currentMana = Mathf.Min(maxMana, caster.currentMana + 3);

            Debug.Log($"[Combat] {caster.data.heroName} paga 2 HP e guadagna 3 Mana (HP: {caster.currentHP}/{caster.data.maxHP}, Mana: {caster.currentMana}/{maxMana}).");
            return true;
        }

        // Generic resource check
        if (caster.currentMana < spell.manaCost || caster.currentStamina < spell.staminaCost)
        {
            Debug.Log($"[Combat] {caster.data.heroName} non ha abbastanza risorse per lanciare {spell.cardName} (Mana: {caster.currentMana}/{spell.manaCost}, Stamina: {caster.currentStamina}/{spell.staminaCost}).");
            return false;
        }

        // Subtract costs
        caster.currentMana = Mathf.Max(0, caster.currentMana - spell.manaCost);
        caster.currentStamina = Mathf.Max(0, caster.currentStamina - spell.staminaCost);

        ModifierDuration modDuration = GetDuration(spell.durationTurns);

        switch (spell.effectType)
        {
            case SpellEffectType.DirectDamage:
                {
                    int damage = spell.damageValue;
                    int defense = CombatActions.GetTotalDefense(opponent);
                    damage = Mathf.Max(0, damage - defense);

                    if (opponent.hasShield)
                    {
                        opponent.hasShield = false;
                        Debug.Log($"[Combat] {opponent.data.heroName} blocca completamente l'attacco grazie allo scudo.");
                    }
                    else
                    {
                        opponent.currentHP = Mathf.Max(0, opponent.currentHP - damage);
                        Debug.Log($"[Combat] {caster.data.heroName} lancia {spell.cardName}: -{spell.manaCost} Mana, infligge {damage} danni a {opponent.data.heroName} (HP rimanenti: {opponent.currentHP}).");
                    }
                }
                break;

            case SpellEffectType.BuffStrengthSelf:
                {
                    StatModifier mod = new StatModifier(spell.cardName, ModifierStat.Strength, spell.secondaryValue, modDuration);
                    caster.AddModifier(mod);
                }
                break;

            case SpellEffectType.BuffDamageNextAttack:
                {
                    StatModifier mod = new StatModifier(spell.cardName, ModifierStat.DamageBonus, spell.secondaryValue, ModifierDuration.UntilNextAttack);
                    caster.AddModifier(mod);
                }
                break;

            case SpellEffectType.PreventNextDamage:
                {
                    caster.hasShield = true;
                    Debug.Log($"[Combat] {caster.data.heroName} ottiene uno scudo: il prossimo attacco subito non infligge danno.");
                }
                break;

            case SpellEffectType.DrainHP:
                {
                    int damage = spell.damageValue;
                    int defense = CombatActions.GetTotalDefense(opponent);
                    damage = Mathf.Max(0, damage - defense);

                    if (opponent.hasShield)
                    {
                        opponent.hasShield = false;
                        Debug.Log($"[Combat] {opponent.data.heroName} blocca completamente l'attacco grazie allo scudo.");
                    }
                    else
                    {
                        opponent.currentHP = Mathf.Max(0, opponent.currentHP - damage);
                        caster.currentHP = Mathf.Min(caster.data.maxHP, caster.currentHP + spell.secondaryValue);
                        Debug.Log($"[Combat] {caster.data.heroName} lancia {spell.cardName}: -{spell.manaCost} Mana, infligge {damage} danni a {opponent.data.heroName} e recupera {spell.secondaryValue} HP (HP attuali: {caster.currentHP}/{caster.data.maxHP}).");
                    }
                }
                break;

            case SpellEffectType.DebuffStrengthOpponent:
                {
                    StatModifier mod = new StatModifier(spell.cardName, ModifierStat.Strength, -spell.secondaryValue, modDuration);
                    opponent.AddModifier(mod);
                }
                break;

            case SpellEffectType.DebuffAgilityOpponent:
                {
                    StatModifier mod = new StatModifier(spell.cardName, ModifierStat.Agility, -spell.secondaryValue, modDuration);
                    opponent.AddModifier(mod);
                }
                break;

            case SpellEffectType.GainStamina:
                {
                    int maxStamina = caster.GetModifiedAgility();
                    caster.currentStamina = Mathf.Min(maxStamina, caster.currentStamina + spell.secondaryValue);
                    Debug.Log($"[Combat] {caster.data.heroName} guadagna {spell.secondaryValue} Stamina ({caster.currentStamina}/{maxStamina}).");
                }
                break;

            case SpellEffectType.GainMana:
                {
                    int maxMana = caster.GetModifiedIntelligence();
                    caster.currentMana = Mathf.Min(maxMana, caster.currentMana + spell.secondaryValue);
                    Debug.Log($"[Combat] {caster.data.heroName} guadagna {spell.secondaryValue} Mana ({caster.currentMana}/{maxMana}).");
                }
                break;
        }

        return true;
    }
}
