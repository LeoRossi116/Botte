using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Botte/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public CardType cardType;
    public CardClass cardClass;
    public int staminaCost;
    public int manaCost;
    [TextArea] public string effectDescription;
}
