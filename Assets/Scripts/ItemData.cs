using System.Collections.Generic;
using UnityEngine;

// Item cards: always cost 0 mana / 0 stamina, usable during the Combat phase,
// discarded after use (unless an effect says otherwise). Items live in the shared item deck.
[CreateAssetMenu(fileName = "NewItemData", menuName = "Botte/ItemData")]
public class ItemData : CardData
{
    public ItemCategory category;
    public CardTarget target;
    public List<SpellEffect> effects = new List<SpellEffect>();
}
