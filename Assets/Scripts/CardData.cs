using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Botte/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public CardType cardType;
    public CardClass cardClass;
    public int staminaCost;
    public int manaCost;
    public Rarity rarity = Rarity.Common;
    [TextArea] public string effectDescription;

    [Header("Visual")]
    [Tooltip("Optional card artwork. When set, the card is drawn using this sprite instead of the placeholder background/label.")]
    public Sprite cardTexture;

    // Number of copies of this card that a deck should contain, based on its rarity.
    public static int CountForRarity(Rarity r)
    {
        switch (r)
        {
            case Rarity.Common: return 4;
            case Rarity.Uncommon: return 3;
            case Rarity.Rare: return 2;
            case Rarity.Legendary: return 1;
        }
        return 1;
    }

    // How many copies of THIS card belong in a deck.
    public int DeckCount => CountForRarity(rarity);
}
