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
        return Mathf.Max(0, total);
    }

    // Centralized damage resolution shared by weapon attacks and damaging spells.
    // Returns the HP actually lost by the defender.
    public static int DealDamage(HeroState attacker, HeroState defender, int rawDamage, bool unblockable, bool isWeaponAttack)
    {
        int dmg = Mathf.Max(0, rawDamage);

        // Aura that weakens the opponent's outgoing attacks (e.g. Debolezza).
        if (isWeaponAttack && defender.auraWeakenOpponent > 0)
        {
            dmg = Mathf.Max(0, dmg - defender.auraWeakenOpponent);
        }

        if (unblockable)
        {
            defender.currentHP = Mathf.Max(0, defender.currentHP - dmg);
            return dmg;
        }

        // Flat defense reduction.
        dmg = Mathf.Max(0, dmg - GetTotalDefense(defender));

        // Aura: block the first attack received each turn (e.g. Riflessi felini).
        if (defender.auraBlockFirstAttack && !defender.blockedFirstAttackThisTurn)
        {
            defender.blockedFirstAttackThisTurn = true;
            Debug.Log($"[Combat] {defender.data.heroName} blocca il primo attacco del turno (Riflessi felini).");
            return 0;
        }

        // Full single-attack block (e.g. Elusione, Velo della morte, Passo d'ombra).
        if (defender.hasShield)
        {
            defender.hasShield = false;
            Debug.Log($"[Combat] {defender.data.heroName} blocca completamente l'attacco grazie allo scudo.");
            return 0;
        }

        // Point-based shield absorbs partial damage (e.g. Difesa imperturbabile, Scudo arcano, Teletrasporto).
        if (defender.shieldAmount > 0 && dmg > 0)
        {
            int absorbed = Mathf.Min(defender.shieldAmount, dmg);
            defender.shieldAmount -= absorbed;
            dmg -= absorbed;
            Debug.Log($"[Combat] Lo scudo di {defender.data.heroName} assorbe {absorbed} danni (scudo rimanente: {defender.shieldAmount}).");
        }

        defender.currentHP = Mathf.Max(0, defender.currentHP - dmg);
        return dmg;
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

        bool unblockable = attacker.nextAttackUnblockable;
        int raw = weaponDamage + Mathf.FloorToInt((float)attacker.GetModifiedStrength() / 3) + attacker.GetDamageBonusThisTurn();
        int dealt = DealDamage(attacker, defender, raw, unblockable, true);

        // Consume next-attack buffs.
        attacker.nextAttackUnblockable = false;
        for (int i = attacker.activeModifiers.Count - 1; i >= 0; i--)
        {
            if (attacker.activeModifiers[i].duration == ModifierDuration.UntilNextAttack && attacker.activeModifiers[i].stat == ModifierStat.DamageBonus)
            {
                attacker.activeModifiers.RemoveAt(i);
            }
        }

        Debug.Log($"[Combat] {attacker.data.heroName} attacca con l'arma: -{staminaCost} Stamina, {dealt} danno inflitto a {defender.data.heroName} (HP rimanenti: {defender.currentHP}).");
        return true;
    }
}
