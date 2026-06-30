using UnityEngine;

[CreateAssetMenu(fileName = "NewMagicData", menuName = "Botte/MagicData")]
public class MagicData : CardData
{
    public MagicType magicType;
    public int damageValue;
    public SpellEffectType effectType;
    public int secondaryValue;
    public int durationTurns;
}
