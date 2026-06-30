using UnityEngine;

public static class CombatActions
{
    public static int GetTotalDefense(HeroState hero)
    {
        int total = 0;
        if (hero.equippedItems != null)
        {
            foreach (var item in hero.equippedItems)
            {
                if (item != null)
                {
                    total += item.defenseValue;
                }
            }
        }
        total += hero.GetModifiedDefenseBonus();
        return total;
    }

    public static bool TryWeaponAttack(HeroState attacker, HeroState defender, int staminaCost, int weaponDamage)
    {
        staminaCost = Mathf.Max(1, staminaCost);

        if (attacker.currentStamina < staminaCost)
        {
            Debug.Log($"[Combat] {attacker.data.heroName} non ha abbastanza Stamina per attaccare (richiesta: {staminaCost}, disponibile: {attacker.currentStamina}).");
            return false;
        }

        attacker.currentStamina = Mathf.Max(0, attacker.currentStamina - staminaCost);
        
        int damage = weaponDamage + Mathf.FloorToInt((float)attacker.GetModifiedStrength() / 3) + attacker.GetDamageBonusThisTurn();
        int defense = GetTotalDefense(defender);
        damage = Mathf.Max(0, damage - defense);

        // Consume UntilNextAttack DamageBonus modifiers
        for (int i = attacker.activeModifiers.Count - 1; i >= 0; i--)
        {
            if (attacker.activeModifiers[i].duration == ModifierDuration.UntilNextAttack && attacker.activeModifiers[i].stat == ModifierStat.DamageBonus)
            {
                attacker.activeModifiers.RemoveAt(i);
            }
        }

        if (defender.hasShield)
        {
            damage = 0;
            defender.hasShield = false;
            Debug.Log($"[Combat] {defender.data.heroName} blocca completamente l'attacco grazie allo scudo.");
        }
        else
        {
            defender.currentHP = Mathf.Max(0, defender.currentHP - damage);
            Debug.Log($"[Combat] {attacker.data.heroName} attacca con l'arma: -{staminaCost} Stamina, {damage} danno inflitto a {defender.data.heroName} (HP rimanenti: {defender.currentHP}).");
        }

        return true;
    }

    public static bool TryMagicAttack(HeroState caster, HeroState target, MagicData spell)
    {
        if (caster.currentMana < spell.manaCost || caster.currentStamina < spell.staminaCost)
        {
            Debug.Log($"[Combat] {caster.data.heroName} non ha abbastanza risorse per lanciare {spell.cardName} (Mana richiesto: {spell.manaCost}/{caster.currentMana}, Stamina richiesta: {spell.staminaCost}/{caster.currentStamina}).");
            return false;
        }

        caster.currentMana = Mathf.Max(0, caster.currentMana - spell.manaCost);
        caster.currentStamina = Mathf.Max(0, caster.currentStamina - spell.staminaCost);

        int baseDamage = spell.damageValue > 0 ? spell.damageValue : 5;
        int defense = GetTotalDefense(target);
        int damage = Mathf.Max(0, baseDamage - defense);

        if (target.hasShield)
        {
            damage = 0;
            target.hasShield = false;
            Debug.Log($"[Combat] {target.data.heroName} blocca completamente l'attacco grazie allo scudo.");
        }
        else
        {
            target.currentHP = Mathf.Max(0, target.currentHP - damage);
            Debug.Log($"[Combat] {caster.data.heroName} lancia {spell.cardName}: -{spell.manaCost} Mana, -{spell.staminaCost} Stamina, {damage} danno inflitto a {target.data.heroName} (HP rimanenti: {target.currentHP}).");
        }

        return true;
    }
}
