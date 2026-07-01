using UnityEngine;

public static class CombatActions
{
    public const int UNARMED_DAMAGE = 1; // base damage with no main-hand weapon

    // Defense = helmet + torso defenseValue (+ temporary Defense modifiers), min 0.
    public static int GetTotalDefense(HeroState hero)
    {
        int total = 0;
        var helmet = hero.Helmet; if (helmet != null) total += helmet.defenseValue;
        var torso = hero.Torso; if (torso != null) total += torso.defenseValue;
        total += hero.GetModifiedDefenseBonus();
        return Mathf.Max(0, total);
    }

    // Applies poison respecting immunity and the owner's ExtraPoisonStack effect.
    public static void ApplyPoison(HeroState source, HeroState target, int stacks)
    {
        if (stacks <= 0) return;
        if (target.HasEquipEffect(EquipEffect.PoisonImmune))
        {
            Debug.Log($"[Combat] {target.data.heroName} è immune al Veleno.");
            return;
        }
        int extra = source != null ? source.SumEquipEffect(EquipEffect.ExtraPoisonStack) : 0;
        int total = stacks + extra;
        target.poisonStacks += total;
        Debug.Log($"[Combat] {target.data.heroName} subisce {total} Veleno (stack totali: {target.poisonStacks}).");
    }

    private static bool MainWeaponBypass(HeroState attacker)
    {
        var main = attacker.MainWeapon;
        return main != null && main.specialEffect == EquipEffect.WeaponBypassDefense;
    }

    // Fires when a physical (weapon) attack lands (not fully blocked): armor durability + reactions.
    private static void OnPhysicalHitTaken(HeroState attacker, HeroState defender)
    {
        // Armor durability decreases on each hit taken; broken pieces are discarded.
        DegradeArmor(defender, EquipmentSlot.Head);
        DegradeArmor(defender, EquipmentSlot.Torso);

        // Retaliation poison (casacca di spine, corazza spettrale).
        var retal = defender.FindEquip(EquipEffect.PoisonOnHitTaken);
        if (retal != null) ApplyPoison(defender, attacker, retal.effectValue);

        // Shield gained per physical hit taken (stivali leggieri del mago).
        var shieldPerHit = defender.FindEquip(EquipEffect.ShieldPerPhysicalHitTaken);
        if (shieldPerHit != null)
        {
            defender.shieldAmount += shieldPerHit.effectValue;
            Debug.Log($"[Combat] {defender.data.heroName} ottiene {shieldPerHit.effectValue} scudo (colpo fisico subito).");
        }
    }

    private static void DegradeArmor(HeroState hero, EquipmentSlot slot)
    {
        var piece = hero.equippedItems[(int)slot];
        if (piece == null || piece.maxDurability <= 0) return;
        if (!hero.durability.ContainsKey(slot)) return;
        hero.durability[slot]--;
        if (hero.durability[slot] <= 0)
        {
            Debug.Log($"[Combat] {piece.cardName} di {hero.data.heroName} si rompe!");
            EquipmentSystem.RemoveAttributes(hero, piece);
            hero.equippedItems[(int)slot] = null;
            hero.durability.Remove(slot);
            hero.discardPile.Add(piece);
        }
    }

    // Centralized damage resolution shared by weapon attacks and damaging spells.
    // Returns the HP actually lost by the defender.
    public static int DealDamage(HeroState attacker, HeroState defender, int rawDamage, bool unblockable, bool isWeaponAttack)
    {
        int dmg = Mathf.Max(0, rawDamage);

        if (isWeaponAttack && defender.auraWeakenOpponent > 0)
            dmg = Mathf.Max(0, dmg - defender.auraWeakenOpponent);

        bool bypass = unblockable || (isWeaponAttack && attacker != null && MainWeaponBypass(attacker));

        if (!bypass)
        {
            // Sacrifice armor: block one entire hit, then break (elmo della salvezza).
            if (isWeaponAttack)
            {
                var sac = FindSacrifice(defender);
                if (sac.piece != null)
                {
                    Debug.Log($"[Combat] {sac.piece.cardName} blocca interamente l'attacco e si distrugge!");
                    EquipmentSystem.RemoveAttributes(defender, sac.piece);
                    defender.equippedItems[(int)sac.slot] = null;
                    defender.durability.Remove(sac.slot);
                    defender.discardPile.Add(sac.piece);
                    return 0;
                }
            }

            if (defender.auraBlockFirstAttack && !defender.blockedFirstAttackThisTurn)
            {
                defender.blockedFirstAttackThisTurn = true;
                Debug.Log($"[Combat] {defender.data.heroName} blocca il primo attacco del turno (Riflessi felini).");
                return 0;
            }

            if (defender.hasShield)
            {
                defender.hasShield = false;
                Debug.Log($"[Combat] {defender.data.heroName} blocca completamente l'attacco grazie allo scudo.");
                return 0;
            }

            dmg = Mathf.Max(0, dmg - GetTotalDefense(defender));

            if (defender.shieldAmount > 0 && dmg > 0)
            {
                int absorbed = Mathf.Min(defender.shieldAmount, dmg);
                defender.shieldAmount -= absorbed;
                dmg -= absorbed;
                Debug.Log($"[Combat] Lo scudo di {defender.data.heroName} assorbe {absorbed} danni (scudo rimanente: {defender.shieldAmount}).");
            }
        }

        defender.currentHP = Mathf.Max(0, defender.currentHP - dmg);

        if (isWeaponAttack) OnPhysicalHitTaken(attacker, defender);
        return dmg;
    }

    private static (EquipmentData piece, EquipmentSlot slot) FindSacrifice(HeroState hero)
    {
        for (int i = 0; i < hero.equippedItems.Length; i++)
            if (hero.equippedItems[i] != null && hero.equippedItems[i].specialEffect == EquipEffect.SacrificeBlockThenBreak)
                return (hero.equippedItems[i], (EquipmentSlot)i);
        return (null, EquipmentSlot.WeaponMain);
    }

    // Sums conditional weapon-damage bonuses granted by equipment.
    private static int GetEquipmentAttackBonus(HeroState attacker, HeroState defender)
    {
        int bonus = EquipmentSystem.SumDamageAttribute(attacker); // e.g. corpetto borchiato +2

        foreach (var eq in attacker.AllEquipped())
        {
            switch (eq.specialEffect)
            {
                case EquipEffect.BonusDamageIfOpponentLowHP:
                    if (defender.currentHP * 2 < defender.GetModifiedMaxHP()) bonus += eq.effectValue;
                    break;
                case EquipEffect.BonusDamageIfOpponentPoisoned:
                    if (defender.poisonStacks > 0) bonus += eq.effectValue;
                    break;
                case EquipEffect.DamagePerPoisonStack:
                    bonus += eq.effectValue * defender.poisonStacks;
                    break;
                case EquipEffect.DamageAtHPCost:
                    bonus += eq.effectValue;
                    break;
                case EquipEffect.ReaperVsPoisoned:
                    if (defender.poisonStacks > 0) bonus += eq.effectValue;
                    break;
                case EquipEffect.SynergyDamage:
                    if (attacker.cardTypesUsedThisTurn.Count >= 2) bonus += eq.effectValue;
                    break;
            }
        }
        return bonus;
    }

    // Post-hit weapon effects: on-hit poison/silence/stun, life gain, hp cost, reaper heal.
    private static void OnWeaponHit(HeroState attacker, HeroState defender)
    {
        foreach (var eq in attacker.AllEquipped())
        {
            switch (eq.specialEffect)
            {
                case EquipEffect.PoisonOnHit:
                    ApplyPoison(attacker, defender, eq.effectValue);
                    break;
                case EquipEffect.SilenceOnHit:
                    defender.isSilenced = true;
                    Debug.Log($"[Combat] {defender.data.heroName} è silenziato: non potrà lanciare incantesimi nel suo prossimo turno.");
                    break;
                case EquipEffect.StunOnHit:
                    defender.pendingStaminaPenalty += eq.effectValue;
                    Debug.Log($"[Combat] {defender.data.heroName} è stordito: -{eq.effectValue} Stamina il prossimo turno.");
                    break;
                case EquipEffect.LifeOnAttack:
                    attacker.currentHP = Mathf.Min(attacker.GetModifiedMaxHP(), attacker.currentHP + eq.effectValue);
                    Debug.Log($"[Combat] {attacker.data.heroName} recupera {eq.effectValue} HP dall'arma ({attacker.currentHP}/{attacker.GetModifiedMaxHP()}).");
                    break;
                case EquipEffect.DamageAtHPCost:
                    attacker.currentHP = Mathf.Max(1, attacker.currentHP - eq.effectValue2);
                    Debug.Log($"[Combat] {attacker.data.heroName} paga {eq.effectValue2} HP per l'arma ({attacker.currentHP}/{attacker.GetModifiedMaxHP()}).");
                    break;
                case EquipEffect.ReaperVsPoisoned:
                    if (defender.poisonStacks > 0)
                    {
                        attacker.currentHP = Mathf.Min(attacker.GetModifiedMaxHP(), attacker.currentHP + eq.effectValue2);
                        Debug.Log($"[Combat] {attacker.data.heroName} drena {eq.effectValue2} HP dal nemico avvelenato.");
                    }
                    break;
            }
        }
    }

    public static bool TryWeaponAttack(HeroState attacker, HeroState defender, int staminaCost)
    {
        if (attacker.HasEquipEffect(EquipEffect.ReduceStaminaCost)) staminaCost -= 1;
        staminaCost = Mathf.Max(1, staminaCost);

        if (attacker.currentStamina < staminaCost)
        {
            Debug.Log($"[Combat] {attacker.data.heroName} non ha abbastanza Stamina per attaccare (richiesta: {staminaCost}, disponibile: {attacker.currentStamina}).");
            return false;
        }

        attacker.currentStamina = Mathf.Max(0, attacker.currentStamina - staminaCost);
        attacker.attackedThisTurn = true;

        int weaponDmg = EquipmentSystem.GetWeaponDamage(attacker, UNARMED_DAMAGE);
        int hits = attacker.HasEquipEffect(EquipEffect.AttackTwice) ? 2 : 1;
        bool unblockable = attacker.nextAttackUnblockable;

        for (int h = 0; h < hits; h++)
        {
            if (defender.currentHP <= 0) break;
            int raw = weaponDmg
                      + Mathf.FloorToInt((float)attacker.GetModifiedStrength() / 3)
                      + attacker.GetDamageBonusThisTurn()
                      + GetEquipmentAttackBonus(attacker, defender);
            int dealt = DealDamage(attacker, defender, raw, unblockable, true);
            OnWeaponHit(attacker, defender);
            Debug.Log($"[Combat] {attacker.data.heroName} attacca con l'arma (colpo {h + 1}/{hits}): {dealt} danno a {defender.data.heroName} (HP: {defender.currentHP}).");
        }

        // Consume next-attack buffs once per attack action.
        attacker.nextAttackUnblockable = false;
        for (int i = attacker.activeModifiers.Count - 1; i >= 0; i--)
        {
            if (attacker.activeModifiers[i].duration == ModifierDuration.UntilNextAttack && attacker.activeModifiers[i].stat == ModifierStat.DamageBonus)
                attacker.activeModifiers.RemoveAt(i);
        }

        return true;
    }
}
