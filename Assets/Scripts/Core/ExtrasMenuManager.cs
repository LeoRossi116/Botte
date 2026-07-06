using UnityEngine;

/// <summary>
/// Handles navigation for the Extras sub-menu and the Card Editor Hub, plus the
/// card-editing entry actions (create new / load existing). Panel visibility is
/// driven by SetActive, matching the pattern used by SceneUIManager.
/// </summary>
public class ExtrasMenuManager : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The title/landing page. Owned by SceneUIManager; hidden while Extras is open.")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("The Extras options page (Card Editor / Achievements / Game Statistics / Back).")]
    [SerializeField] private GameObject extrasPanel;
    [Tooltip("The Card Editor Hub page (Create New Card / saved slots / Back).")]
    [SerializeField] private GameObject cardEditorHubPanel;

    // The card currently being edited. Held in memory until a real editor screen
    // and persistence layer are wired up.
    private CardData _currentCard;

    private void Awake()
    {
        // Sub-menus always start hidden; the main menu is the entry point.
        if (extrasPanel != null) extrasPanel.SetActive(false);
        if (cardEditorHubPanel != null) cardEditorHubPanel.SetActive(false);
    }

    // --- EXTRAS MENU ACTIONS ---

    // Main menu -> Extras options.
    public void OpenExtras()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (cardEditorHubPanel != null) cardEditorHubPanel.SetActive(false);
        if (extrasPanel != null) extrasPanel.SetActive(true);
    }

    // Extras -> Card Editor Hub.
    public void OpenCardEditor()
    {
        if (extrasPanel != null) extrasPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (cardEditorHubPanel != null) cardEditorHubPanel.SetActive(true);
    }

    // Placeholder: Achievements screen not implemented yet.
    public void OpenAchievements()
    {
        Debug.Log("[Extras] Achievements: not implemented yet.");
    }

    // Placeholder: Game Statistics screen not implemented yet.
    public void OpenGameStatistics()
    {
        Debug.Log("[Extras] Game Statistics: not implemented yet.");
    }

    // Extras -> back to the main menu.
    public void BackToMainMenu()
    {
        if (extrasPanel != null) extrasPanel.SetActive(false);
        if (cardEditorHubPanel != null) cardEditorHubPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    // --- CARD EDITOR HUB ACTIONS ---

    // Initializes a fresh, blank card structure ready to be edited.
    public void CreateNewCard()
    {
        _currentCard = ScriptableObject.CreateInstance<CardData>();
        _currentCard.cardName = "New Card";
        _currentCard.rarity = Rarity.Common;
        _currentCard.effectDescription = string.Empty;
        Debug.Log("[CardEditor] Created a new blank card structure.");
    }

    // Dynamic slot action: loads the pre-existing card data identified by cardId.
    // Each saved card slot passes its own id so a single handler serves them all.
    public void LoadCard(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("[CardEditor] LoadCard called with an empty card id.");
            return;
        }

        // Cards are expected under a Resources/Cards folder once persistence exists.
        CardData loaded = Resources.Load<CardData>($"Cards/{cardId}");
        if (loaded != null)
        {
            _currentCard = loaded;
            Debug.Log($"[CardEditor] Loaded card '{cardId}' ({loaded.cardName}).");
        }
        else
        {
            Debug.Log($"[CardEditor] No saved card found for id '{cardId}' yet.");
        }
    }

    // Card Editor Hub -> back to the Extras options.
    public void BackToExtras()
    {
        if (cardEditorHubPanel != null) cardEditorHubPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (extrasPanel != null) extrasPanel.SetActive(true);
    }
}
