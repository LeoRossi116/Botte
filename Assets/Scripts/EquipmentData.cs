using UnityEngine;

[CreateAssetMenu(fileName = "NewEquipmentData", menuName = "Botte/EquipmentData")]
public class EquipmentData : CardData
{
    public EquipmentSlot slot;
    public bool twoHanded;
    public int damageValue;
    public int defenseValue;
    public int durability;
    public int requiredStrength;
    public int requiredIntelligence;
    public int requiredAgility;
}
