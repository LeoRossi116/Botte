using System.Collections.Generic;
using UnityEngine;

public static class EquipmentSystem
{
    // The hero's current (equipment-modified) value for a given requirement stat.
    // Strength -> modified Strength, Intelligence -> modified max mana, Speed -> modified max stamina.
    public static int GetStatValue(HeroState hero, RequirementStat stat)
    {
        switch (stat)
        {
            case RequirementStat.Strength: return hero.GetModifiedStrength();
            case RequirementStat.Intelligence: return hero.GetModifiedIntelligence();
            case RequirementStat.Speed: return hero.GetModifiedAgility();
        }
        return 0;
    }

    // Localized label for an equipment attribute modifier (matches hero-select terminology).
    public static string AttributeLabel(EquipAttribute attr)
    {
        switch (attr)
        {
            case EquipAttribute.MaxHP: return "HP max";
            case EquipAttribute.MaxMana: return "Intelligenza";
            case EquipAttribute.MaxStamina: return "Velocità";
            case EquipAttribute.Strength: return "Forza";
            case EquipAttribute.Damage: return "Danno";
        }
        return attr.ToString();
    }

    // Localized label for a requirement stat (matches the hero-select terminology).
    public static string StatLabel(RequirementStat stat)
    {
        switch (stat)
        {
            case RequirementStat.Strength: return "Forza";
            case RequirementStat.Intelligence: return "Intelligenza";
            case RequirementStat.Speed: return "Velocità";
        }
        return stat.ToString();
    }

    // Returns true if the hero meets every stat requirement of the equipment.
    // When false, 'message' explains which requirement failed.
    public static bool MeetsRequirements(HeroState hero, EquipmentData eq, out string message)
    {
        message = null;
        if (eq == null || eq.requirements == null) return true;
        foreach (var req in eq.requirements)
        {
            int have = GetStatValue(hero, req.stat);
            if (have < req.value)
            {
                message = $"{hero.data.heroName} non può equipaggiare {eq.cardName}: richiede {StatLabel(req.stat)} {req.value} (attuale: {have}).";
                return false;
            }
        }
        return true;
    }

    // Apply an equipment's permanent attribute modifiers, then clamp current resources.
    public static void ApplyAttributes(HeroState hero, EquipmentData eq)
    {
        if (eq == null) return;
        foreach (var mod in eq.attributeMods)
        {
            switch (mod.attr)
            {
                case EquipAttribute.MaxHP:
                    hero.activeModifiers.Add(new StatModifier(eq.cardName, ModifierStat.MaxHP, mod.value, ModifierDuration.Permanent)); break;
                case EquipAttribute.MaxMana:
                    hero.activeModifiers.Add(new StatModifier(eq.cardName, ModifierStat.Intelligence, mod.value, ModifierDuration.Permanent)); break;
                case EquipAttribute.MaxStamina:
                    hero.activeModifiers.Add(new StatModifier(eq.cardName, ModifierStat.Agility, mod.value, ModifierDuration.Permanent)); break;
                case EquipAttribute.Strength:
                    hero.activeModifiers.Add(new StatModifier(eq.cardName, ModifierStat.Strength, mod.value, ModifierDuration.Permanent)); break;
                // Damage attribute is queried per attack, not stored as a modifier.
            }
        }
        ClampResources(hero);
    }

    public static void RemoveAttributes(HeroState hero, EquipmentData eq)
    {
        if (eq == null) return;
        hero.RemovePermanentFromSource(eq.cardName);
        ClampResources(hero);
    }

    public static void ClampResources(HeroState hero)
    {
        hero.currentHP = Mathf.Clamp(hero.currentHP, 0, hero.GetModifiedMaxHP());
        hero.currentMana = Mathf.Clamp(hero.currentMana, 0, hero.GetModifiedIntelligence());
        hero.currentStamina = Mathf.Clamp(hero.currentStamina, 0, hero.GetModifiedAgility());
    }

    // Flat damage bonus from equipment "Damage" attributes (e.g. corpetto borchiato +2).
    public static int SumDamageAttribute(HeroState hero)
    {
        int total = 0;
        foreach (var eq in hero.AllEquipped())
            foreach (var mod in eq.attributeMods)
                if (mod.attr == EquipAttribute.Damage) total += mod.value;
        return total;
    }

    // The damage the hero's usable (main-hand) weapon deals, or the unarmed value.
    public static int GetWeaponDamage(HeroState hero, int unarmed)
    {
        var main = hero.MainWeapon;
        if (main != null && main.IsWeapon) return main.damageValue;
        return unarmed;
    }

    private static void SetSlot(HeroState hero, EquipmentSlot slot, EquipmentData eq, List<EquipmentData> displaced)
    {
        var existing = hero.equippedItems[(int)slot];
        if (existing != null) { RemoveAttributes(hero, existing); displaced.Add(existing); hero.durability.Remove(slot); }
        hero.equippedItems[(int)slot] = eq;
        if (eq != null)
        {
            ApplyAttributes(hero, eq);
            if (eq.maxDurability > 0) hero.durability[slot] = eq.maxDurability;
        }
    }

    // Equips a piece. Returns any equipment displaced from its slot(s) (to be discarded).
    public static List<EquipmentData> Equip(HeroState hero, EquipmentData eq)
    {
        var displaced = new List<EquipmentData>();
        if (eq == null) return displaced;

        if (eq.IsWeapon)
        {
            bool wieldOneHand = hero.HasEquipEffect(EquipEffect.WieldTwoHandInOneHand);

            // Clear an existing two-handed weapon first (it occupies both hands).
            if (hero.weaponTwoHandedEquipped)
            {
                SetSlot(hero, EquipmentSlot.WeaponMain, null, displaced);
                hero.weaponTwoHandedEquipped = false;
            }

            if (eq.IsTwoHanded)
            {
                SetSlot(hero, EquipmentSlot.WeaponMain, null, displaced);
                if (!wieldOneHand) SetSlot(hero, EquipmentSlot.WeaponOff, null, displaced);
                hero.equippedItems[(int)EquipmentSlot.WeaponMain] = eq;
                ApplyAttributes(hero, eq);
                if (eq.maxDurability > 0) hero.durability[EquipmentSlot.WeaponMain] = eq.maxDurability;
                hero.weaponTwoHandedEquipped = !wieldOneHand;
            }
            else // one-handed
            {
                if (hero.MainWeapon == null) SetSlot(hero, EquipmentSlot.WeaponMain, eq, displaced);
                else if (hero.OffWeapon == null) SetSlot(hero, EquipmentSlot.WeaponOff, eq, displaced);
                else SetSlot(hero, EquipmentSlot.WeaponMain, eq, displaced); // replace main
            }
        }
        else
        {
            SetSlot(hero, eq.PhysicalSlot(), eq, displaced);
        }
        return displaced;
    }
}