using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EquipAttributeMod
{
    public EquipAttribute attr;
    public int value;
}

[System.Serializable]
public class EquipRequirement
{
    public RequirementStat stat;   // which hero stat is required
    public int value;              // minimum amount the hero must have to equip
}

[CreateAssetMenu(fileName = "NewEquipmentData", menuName = "Botte/EquipmentData")]
public class EquipmentData : CardData
{
    public EquipmentType equipType;      // Armor, Utility, Weapon
    public EquipSlotType slotType;       // Torso, Helmet, Boots, Gloves, OneHandWeapon, TwoHandWeapon

    public int damageValue;              // weapons only
    public int defenseValue;             // armor (helmet/torso) reduces incoming damage

    public List<EquipAttributeMod> attributeMods = new List<EquipAttributeMod>();

    // Stat requirements the hero must meet to equip this piece (empty = no requirement).
    public List<EquipRequirement> requirements = new List<EquipRequirement>();

    public EquipEffect specialEffect = EquipEffect.None;
    public int effectValue;              // primary magnitude for the special effect
    public int effectValue2;             // secondary magnitude (e.g. hp cost, chance %)

    public int maxDurability;            // 0 = indestructible (no durability)

    public bool IsWeapon => equipType == EquipmentType.Weapon;
    public bool IsTwoHanded => slotType == EquipSlotType.TwoHandWeapon;

    // Maps the declared card slot to the physical equipped-slot index used by HeroState.
    public EquipmentSlot PhysicalSlot()
    {
        switch (slotType)
        {
            case EquipSlotType.Torso: return EquipmentSlot.Torso;
            case EquipSlotType.Helmet: return EquipmentSlot.Head;
            case EquipSlotType.Boots: return EquipmentSlot.Feet;
            case EquipSlotType.Gloves: return EquipmentSlot.Hands;
            case EquipSlotType.OneHandWeapon: return EquipmentSlot.WeaponMain;
            case EquipSlotType.TwoHandWeapon: return EquipmentSlot.WeaponMain;
        }
        return EquipmentSlot.WeaponMain;
    }
}
