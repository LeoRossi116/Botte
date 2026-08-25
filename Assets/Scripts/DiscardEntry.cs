/// <summary>
/// Wraps a card with ownership information for the shared discard pile.
/// Cards are shared ScriptableObjects, so ownership cannot live on CardData itself.
/// </summary>
[System.Serializable]
public class DiscardEntry
{
    public CardData card;
    /// <summary>1 = player1, 2 = player2.</summary>
    public int playerIndex;

    public DiscardEntry(CardData c, int p)
    {
        card = c;
        playerIndex = p;
    }
}
