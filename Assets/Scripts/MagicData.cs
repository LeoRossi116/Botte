using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMagicData", menuName = "Botte/MagicData")]
public class MagicData : CardData
{
    public MagicType magicType;
    public List<SpellEffect> effects = new List<SpellEffect>();
}
