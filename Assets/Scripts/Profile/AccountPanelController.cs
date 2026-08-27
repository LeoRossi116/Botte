using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Builds a placeholder Account / Profile page at runtime (final art comes later). Adds a
// top-left "PROFILE" button to the main menu that opens a panel showing:
//   - the profile name (editable here),
//   - the immutable profile ID (read-only),
//   - gameplay stats (win/loss ratio, win %, per-character win %, most used character).
// All data comes from the locally-saved PlayerProfileManager.
public class AccountPanelController : MonoBehaviour
{
    [Header("Authored Scene References (Panel & Controls)")]
    [SerializeField] private GameObject accountButton;
    [SerializeField] private GameObject accountPanel;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TextMeshProUGUI idText;
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Authored Scene References (Buttons)")]
    [SerializeField] private Button accountOpenButton;
    [SerializeField] private Button backButton;

    private void Start()
    {
        // Touch the profile so it loads and generates its ID on first ever access.
        var _ = PlayerProfileManager.Current;

        if (accountPanel != null)
        {
            // Panel already exists as an authored scene object – just wire the listeners.
            if (accountOpenButton != null)
            {
                accountOpenButton.onClick.RemoveAllListeners();
                accountOpenButton.onClick.AddListener(OpenPanel);
            }
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(ClosePanel);
            }
            if (nameInput != null)
            {
                nameInput.onEndEdit.RemoveAllListeners();
                nameInput.onEndEdit.AddListener(OnNameEdited);
            }
            accountPanel.SetActive(false);
            return;
        }

        // ── Fallback: build at runtime (no serialized references assigned) ──
        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            Debug.LogWarning("[AccountPanel] No 'Canvas' found in scene; cannot build the account page.");
            return;
        }

        Transform canvas = canvasGo.transform;
        Transform mainMenu = canvas.Find("MainMenuPanel");

        BuildOpenButton(mainMenu != null ? mainMenu : canvas);
        BuildAccountPanel(canvas);
    }

    private void BuildOpenButton(Transform parent)
    {
        UnityEngine.UI.Button btn = CreateButton(parent, "AccountButton", "PROFILE", OpenPanel);
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(160f, 50f);
        rt.anchoredPosition = new Vector2(20f, -20f);
    }

    private void BuildAccountPanel(Transform canvas)
    {
        // Full-screen dimmer that also blocks clicks to the menu behind it.
        accountPanel = new GameObject("AccountPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        accountPanel.transform.SetParent(canvas, false);
        RectTransform panelRT = accountPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        accountPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // Centered content box.
        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        box.transform.SetParent(accountPanel.transform, false);
        RectTransform boxRT = box.GetComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax = new Vector2(0.5f, 0.5f);
        boxRT.pivot = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta = new Vector2(560f, 640f);
        boxRT.anchoredPosition = Vector2.zero;
        box.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.98f);

        Transform boxT = box.transform;

        TextMeshProUGUI title = CreateText(boxT, "Title", "PROFILE", 34, TextAlignmentOptions.Center);
        AnchorTop(title.rectTransform, 520f, 50f, -20f);

        TextMeshProUGUI nameLabel = CreateText(boxT, "NameLabel", "Name", 20, TextAlignmentOptions.Left);
        AnchorTop(nameLabel.rectTransform, 520f, 28f, -80f);
        nameInput = CreateInputField(boxT, "NameInput");
        AnchorTop(nameInput.GetComponent<RectTransform>(), 520f, 44f, -108f);
        nameInput.onEndEdit.AddListener(OnNameEdited);

        TextMeshProUGUI idLabel = CreateText(boxT, "IdLabel", "ID (cannot be changed)", 20, TextAlignmentOptions.Left);
        AnchorTop(idLabel.rectTransform, 520f, 28f, -170f);
        idText = CreateText(boxT, "IdValue", "-", 22, TextAlignmentOptions.Left);
        idText.color = new Color(0.7f, 0.8f, 1f, 1f);
        AnchorTop(idText.rectTransform, 520f, 32f, -198f);

        TextMeshProUGUI statsTitle = CreateText(boxT, "StatsTitle", "Stats", 24, TextAlignmentOptions.Left);
        AnchorTop(statsTitle.rectTransform, 520f, 30f, -250f);
        statsText = CreateText(boxT, "StatsBody", "", 20, TextAlignmentOptions.TopLeft);
        AnchorTop(statsText.rectTransform, 520f, 260f, -284f);

        UnityEngine.UI.Button back = CreateButton(boxT, "BackButton", "BACK", ClosePanel);
        RectTransform backRT = back.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0f);
        backRT.anchorMax = new Vector2(0.5f, 0f);
        backRT.pivot = new Vector2(0.5f, 0f);
        backRT.sizeDelta = new Vector2(200f, 54f);
        backRT.anchoredPosition = new Vector2(0f, 24f);

        accountPanel.SetActive(false);
    }

    private void AnchorTop(RectTransform rt, float width, float height, float y)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, y);
    }

    private void OpenPanel()
    {
        if (accountPanel == null) return;
        PlayerProfileData d = PlayerProfileManager.Current;
        if (nameInput != null) nameInput.text = d.profileName;
        if (idText != null) idText.text = d.profileId;
        RefreshStats();
        accountPanel.transform.SetAsLastSibling();
        accountPanel.SetActive(true);
    }

    private void ClosePanel()
    {
        if (nameInput != null) PlayerProfileManager.SetProfileName(nameInput.text);
        if (accountPanel != null) accountPanel.SetActive(false);
    }

    private void OnNameEdited(string value)
    {
        PlayerProfileManager.SetProfileName(value);
    }

    private void RefreshStats()
    {
        if (statsText == null) return;
        PlayerProfileData d = PlayerProfileManager.Current;

        var sb = new System.Text.StringBuilder();
        int total = d.totalWins + d.totalLosses;
        sb.AppendLine($"Games played: {total}");
        sb.AppendLine($"Wins: {d.totalWins}    Losses: {d.totalLosses}");
        sb.AppendLine($"Win / Loss ratio: {PlayerProfileManager.WinLossRatio:0.00}");
        sb.AppendLine($"Win percentage: {PlayerProfileManager.WinPercentage:0.#}%");
        sb.AppendLine("");
        sb.AppendLine("Win % per character:");
        foreach (HeroClass hc in System.Enum.GetValues(typeof(HeroClass)))
        {
            int games = PlayerProfileManager.GamesFor(hc);
            sb.AppendLine($"   {hc}: {PlayerProfileManager.WinPercentageFor(hc):0.#}%   ({games} games)");
        }
        sb.AppendLine("");
        int mostGames;
        string most = PlayerProfileManager.MostUsedCharacter(out mostGames);
        sb.AppendLine($"Most used character: {most} ({mostGames} games)");

        statsText.text = sb.ToString();
    }

    #if UNITY_EDITOR
        /// <summary>Editor-only: invokes the private builders so the panel hierarchy can be
        /// authored into the scene for inspector editing. Call from a RunCommand, not from gameplay code.</summary>
        public void Editor_AuthorIntoScene(Transform canvas, Transform mainMenuParent)
        {
            Transform parent = mainMenuParent ?? canvas;
            if (parent.Find("AccountButton") == null)
                BuildOpenButton(parent);
            if (canvas.Find("AccountPanel") == null)
                BuildAccountPanel(canvas);
        }
    #endif

        // ---------- Small UI builders (placeholder styling) ----------

    private TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private UnityEngine.UI.Button CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.35f, 0.6f, 1f);
        UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(onClick);

        TextMeshProUGUI txt = CreateText(go.transform, "Text", label, 22, TextAlignmentOptions.Center);
        RectTransform trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return btn;
    }

    private TMP_InputField CreateInputField(Transform parent, string name)
    {
        GameObject go = TMPro.TMP_DefaultControls.CreateInputField(new TMPro.TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        TMP_InputField input = go.GetComponent<TMP_InputField>();
        UnityEngine.UI.Image bg = go.GetComponent<UnityEngine.UI.Image>();
        if (bg != null) bg.color = new Color(1f, 1f, 1f, 0.9f);
        if (input.textComponent != null) input.textComponent.color = Color.black;
        return input;
    }
}
