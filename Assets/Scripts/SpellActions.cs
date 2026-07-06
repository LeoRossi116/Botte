using UnityEngine;

public static class SpellActions
{
    public static CastResult TryCastSpell(HeroState caster, HeroState opponent, MagicData spell)
    {
        CastResult result = new CastResult();
        if (spell == null) return result;

        // Silence (lama spezza-incantesimi) prevents casting.
        if (caster.isSilenced)
        {
            Debug.Log($"[Combat] {caster.data.heroName} è silenziato e non può lanciare incantesimi.");
            return result;
        }

        // Class-lock validation.
        if (spell.cardClass != CardClass.Shared && spell.cardClass.ToString() != caster.data.heroClass.ToString())
        {
            Debug.Log($"[Combat] {caster.data.heroName} non può lanciare {spell.cardName}: non è della sua classe.");
            return result;
        }

        // Stamina-cost reduction from equipment (guanti dell'abilità/agilità).
        int staminaCost = spell.staminaCost;
        if (staminaCost > 0 && caster.HasEquipEffect(EquipEffect.ReduceStaminaCost))
            staminaCost = Mathf.Max(1, staminaCost - 1);

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
        if (caster.currentMana < spell.manaCost || caster.currentStamina < staminaCost)
        {
            Debug.Log($"[Combat] {caster.data.heroName} non ha abbastanza risorse per lanciare {spell.cardName} (Mana: {caster.currentMana}/{spell.manaCost}, Stamina: {caster.currentStamina}/{staminaCost}).");
            return result;
        }

        // Pay costs.
        caster.currentMana = Mathf.Max(0, caster.currentMana - spell.manaCost);
        caster.currentStamina = Mathf.Max(0, caster.currentStamina - staminaCost);
        caster.manaUsedThisTurn += spell.manaCost;

        Debug.Log($"[Combat] {caster.data.heroName} lancia {spell.cardName} ({spell.magicType}): -{spell.manaCost} Mana, -{staminaCost} Stamina.");

        foreach (SpellEffect e in spell.effects)
        {
            ApplyEffect(caster, opponent, spell.cardName, e, result);
        }

        // Equipment: shield gained per 2 mana spent (talismano di protezione).
        if (caster.HasEquipEffect(EquipEffect.ShieldPerManaUsed) && spell.manaCost >= 2)
        {
            int gain = (spell.manaCost / 2) * 2;
            caster.shieldAmount += gain;
            Debug.Log($"[Combat] {caster.data.heroName} ottiene {gain} scudo (mana speso).");
        }

        // Equipment: every spell applies poison (totem della maledizione).
        var totem = caster.FindEquip(EquipEffect.PoisonOnSpell);
        if (totem != null) CombatActions.ApplyPoison(caster, opponent, totem.effectValue);

        result.success = true;
        return result;
    }

    // Shared effect application used by both spells and items. sourceName is used for logging.
    public static void ApplyEffect(HeroState caster, HeroState opponent, string sourceName, SpellEffect e, CastResult result)
    {
        switch (e.type)
        {
            case SpellEffectType.DirectDamage:
            {
                int amount = e.value + caster.SumEquipEffect(EquipEffect.SpellDamageBonus);
                int dealt = CombatActions.DealDamage(caster, opponent, amount, false, false);
                Debug.Log($"[Combat] {sourceName}: {dealt} danni a {opponent.data.heroName} (HP rimanenti: {opponent.currentHP}).");
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
                caster.AddModifier(new StatModifier(sourceName, ModifierStat.DamageBonus, e.value, ModifierDuration.UntilNextAttack));
                break;
            }
            case SpellEffectType.BuffDamageThisTurn:
            {
                caster.AddModifier(new StatModifier(sourceName, ModifierStat.DamageBonus, e.value, ModifierDuration.EndOfThisTurn));
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
                CombatActions.ApplyPoison(caster, opponent, e.value);
                break;
            }
            case SpellEffectType.DebuffDamageOpponentNextTurn:
            {
                opponent.AddModifier(new StatModifier(sourceName, ModifierStat.DamageBonus, -e.value, ModifierDuration.UntilNextOpponentTurn));
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
            case SpellEffectType.RemoveAllPoison:
            {
                int removed = caster.poisonStacks;
                caster.poisonStacks = 0;
                Debug.Log($"[Combat] {sourceName}: {caster.data.heroName} rimuove {removed} stack di Veleno.");
                break;
            }
            case SpellEffectType.LoseMana:
            {
                caster.currentMana = Mathf.Max(0, caster.currentMana - e.value);
                Debug.Log($"[Combat] {caster.data.heroName} perde {e.value} Mana ({caster.currentMana}/{caster.GetModifiedIntelligence()}).");
                break;
            }
            case SpellEffectType.GainMaxManaThisTurn:
            {
                caster.AddModifier(new StatModifier(sourceName, ModifierStat.Intelligence, e.value, ModifierDuration.EndOfThisTurn));
                int maxMana = caster.GetModifiedIntelligence();
                caster.currentMana = Mathf.Min(maxMana, caster.currentMana + e.value);
                Debug.Log($"[Combat] {caster.data.heroName} ottiene +{e.value} Mana max per questo turno ({caster.currentMana}/{maxMana}).");
                break;
            }
            case SpellEffectType.DrawChosenDeck:
            {
                result.drawChosenDeckCount += e.value;
                break;
            }
            case SpellEffectType.PeekChosenDeck:
            {
                result.peekChosenDeckCount += e.value;
                break;
            }
            case SpellEffectType.StealOpponentDiscard:
            {
                result.stealOpponentDiscardCount += e.value;
                break;
            }
            case SpellEffectType.EquipmentStub:
            {
                Debug.Log($"[Combat] {sourceName}: effetto legato all'equipaggiamento non ancora implementato.");
                break;
            }
            case SpellEffectType.DrainManaOpponent:
            {
                opponent.currentMana = Mathf.Max(0, opponent.currentMana - e.value);
                Debug.Log($"[Combat] {opponent.data.heroName} perde {e.value} Mana ({opponent.currentMana}/{opponent.GetModifiedIntelligence()}).");
                break;
            }
            case SpellEffectType.LoseStamina:
            {
                caster.currentStamina = Mathf.Max(0, caster.currentStamina - e.value);
                Debug.Log($"[Combat] {caster.data.heroName} perde {e.value} Stamina ({caster.currentStamina}/{caster.GetModifiedAgility()}).");
                break;
            }
            case SpellEffectType.GainMaxStrengthThisTurn:
            {
                caster.AddModifier(new StatModifier(sourceName, ModifierStat.Strength, e.value, ModifierDuration.EndOfThisTurn));
                Debug.Log($"[Combat] {caster.data.heroName} ottiene +{e.value} Forza per questo turno.");
                break;
            }
            case SpellEffectType.GainMaxAgilityThisTurn:
            {
                caster.AddModifier(new StatModifier(sourceName, ModifierStat.Agility, e.value, ModifierDuration.EndOfThisTurn));
                Debug.Log($"[Combat] {caster.data.heroName} ottiene +{e.value} Rapidità per questo turno.");
                break;
            }
        }
    }
}
