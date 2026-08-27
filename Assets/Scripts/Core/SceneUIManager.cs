using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using TMPro;

public class SceneUIManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Serialized scene references
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Network Prefab")]
    [SerializeField] private NetworkObject relayManagerPrefab;

    [Header("Scene UI Panels")]
    [Tooltip("Title / landing page (PLAY · OPTION · EXIT).")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("Connect page – Host / Join buttons.")]
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject lobbyPanel;
    [Tooltip("Options modal (opened from title screen).")]
    [SerializeField] private GameObject optionsPanel;

    [Header("Lobby UI References")]
    [Tooltip("Legacy play-panel code field – hidden at startup; kept for RelayManager.AssignUIReferences.")]
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TextMeshProUGUI errorStatusText;
    [SerializeField] private TextMeshProUGUI generatedCodeText;
    [SerializeField] private TextMeshProUGUI playerListText;

    [Header("Nickname Fields (obsolete – kept for scene backward-compat)")]
    [SerializeField] private Button insertNameButton;
    [SerializeField] private TMP_InputField nicknameInputField;
    [Tooltip("Checkmark button that confirms the typed nickname.")]
    [SerializeField] private Button confirmNameButton;
    [Tooltip("Button that clears the nickname input field.")]
    [SerializeField] private Button clearNameButton;

    // ─────────────────────────────────────────────────────────────────────────
    // Public session API (used by RelayManager, BattleManager, and others)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The active multiplayer session. Non-null while a session exists.</summary>
    public static ISession CurrentSession { get; private set; }

    /// <summary>True when the local player is the host of the current session.</summary>
    public static bool IsHost => CurrentSession != null && CurrentSession.IsHost;

    /// <summary>
    /// Host-only: locks the session so it disappears from the public browse list and marks
    /// it as started. Safe to call when CurrentSession is null or if not host.
    /// </summary>
    public static async Task LockCurrentSessionAsync()
    {
        if (CurrentSession == null || !CurrentSession.IsHost) return;
        try
        {
            IHostSession host = CurrentSession.AsHost();
            host.IsLocked = true;
            host.SetProperty(LobbyConfig.KeyStarted,
                new SessionProperty("true", VisibilityPropertyOptions.Public));
            await host.SavePropertiesAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SceneUIManager] LockCurrentSessionAsync failed: {e.Message}");
        }
    }

    /// <summary>
    /// Leaves the current session (the session SDK also tears down NGO relay).
    /// Resets ActiveLobby. Safe to call multiple times or when CurrentSession is null.
    /// Does NOT call NetworkManager.Shutdown – that is handled by the session SDK / RelayManager.
    /// </summary>
    public static async Task LeaveCurrentSessionAsync()
    {
        if (CurrentSession == null) return;
        ISession session = CurrentSession;
        CurrentSession = null;
        ActiveLobby.Reset();
        try
        {
            await session.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SceneUIManager] LeaveCurrentSessionAsync: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal state
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The player's display name, sourced from their saved profile.</summary>
    public static string LocalNickname => PlayerProfileManager.Current.profileName;

    private RelayManager _activeRelayManager;
    private Coroutine _errorCoroutine;
    private bool _isBusy;

    // ── Lazily-built panels (built once under the Canvas at runtime, no scene edits) ──
    private GameObject _createLobbyPanel;
    private GameObject _joinBrowsePanel;

    // CreateLobbyPanel controls
    private TMP_InputField _lobbyNameInput;
    private Toggle         _publicToggle;
    private TMP_InputField _prepInput;
    private TMP_InputField _combatInput;
    private TMP_InputField _endInput;
    private Toggle         _bestOf3Toggle;

    // JoinBrowsePanel controls
    private Transform      _sessionListContent;
    private TMP_InputField _codeEntryInput;
    private TextMeshProUGUI _browseStatusText;

    // UI style constants (placeholder look – dark panel, blue / green buttons)
    private static readonly Color ColDarkPanel   = new Color(0.122f, 0.122f, 0.149f, 0.97f);
    private static readonly Color ColButtonBlue  = new Color(0.20f, 0.35f, 0.60f, 1f);
    private static readonly Color ColButtonGreen = new Color(0.15f, 0.50f, 0.20f, 1f);
    private static readonly Color ColButtonGrey  = new Color(0.25f, 0.25f, 0.28f, 1f);

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private async void Start()
    {
        if (errorStatusText != null) errorStatusText.text = "";

        // Hide the play-panel code field (code entry moved to JoinBrowsePanel).
        if (joinCodeInputField != null) joinCodeInputField.gameObject.SetActive(false);

        // Hide obsolete nickname controls.
        if (nicknameInputField != null) nicknameInputField.gameObject.SetActive(false);
        if (insertNameButton   != null) insertNameButton.gameObject.SetActive(false);
        if (confirmNameButton  != null) confirmNameButton.gameObject.SetActive(false);
        if (clearNameButton    != null) clearNameButton.gameObject.SetActive(false);

        // Initial panel state.
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (playPanel     != null) playPanel.SetActive(false);
        if (lobbyPanel    != null) lobbyPanel.SetActive(false);

        // Pre-warm UGS / auth so hosting / joining is instant.
        await EnsureServicesReadyAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UGS init + anonymous auth (idempotent)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task EnsureServicesReadyAsync()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[SceneUIManager] Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Online services failed: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Navigation – wired to scene buttons
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Title → play / connect page.</summary>
    public void ClickedPlay()
    {
        if (mainMenuPanel    != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel       != null) lobbyPanel.SetActive(false);
        if (_createLobbyPanel != null) _createLobbyPanel.SetActive(false);
        if (_joinBrowsePanel  != null) _joinBrowsePanel.SetActive(false);
        if (playPanel        != null) playPanel.SetActive(true);
        if (errorStatusText  != null) errorStatusText.text = "";
    }

    // Obsolete – kept so scene button references don't throw.
    public void ClickedConfirmName() { }
    public void ClickedClearName()   { }

    /// <summary>Play page → back to title.</summary>
    public void ClickedBackToMenu()
    {
        SafeShutdown();
        if (playPanel         != null) playPanel.SetActive(false);
        if (lobbyPanel        != null) lobbyPanel.SetActive(false);
        if (_createLobbyPanel != null) _createLobbyPanel.SetActive(false);
        if (_joinBrowsePanel  != null) _joinBrowsePanel.SetActive(false);
        if (mainMenuPanel     != null) mainMenuPanel.SetActive(true);
        if (errorStatusText   != null) errorStatusText.text = "";
    }

    public void ClickedOptions()
    {
        if (Botte.UI.SettingsPanelController.Instance != null)
            Botte.UI.SettingsPanelController.Instance.Open();
        else
            Debug.LogWarning("[SceneUIManager] SettingsPanelController not found.");
    }

    public void ClickedExitGame()
    {
        SafeShutdown();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Lobby panel Exit button – delegates to RelayManager when present.</summary>
    public void ClickedBack()
    {
        if (_activeRelayManager != null)
        {
            _activeRelayManager.ToMainMenu();    // handles NGO teardown
            _ = LeaveCurrentSessionAsync();       // leaves the session service side
        }
        else
        {
            _ = LeaveCurrentSessionAsync();
            SafeShutdown();
            if (lobbyPanel    != null) lobbyPanel.SetActive(false);
            if (playPanel     != null) playPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HOST button → show CreateLobbyPanel
    // ─────────────────────────────────────────────────────────────────────────

    public void ClickedHost()
    {
        if (string.IsNullOrEmpty(LocalNickname))
        {
            ShowError("Set a profile name on the Account page first.");
            return;
        }

        EnsureCreateLobbyPanel();
        if (playPanel != null) playPanel.SetActive(false);

        // Pre-fill lobby name.
        if (_lobbyNameInput != null)
            _lobbyNameInput.text = $"{LocalNickname}'s Lobby";

        _createLobbyPanel.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JOIN button → show JoinBrowsePanel
    // ─────────────────────────────────────────────────────────────────────────

    public void ClickedJoin()
    {
        if (string.IsNullOrEmpty(LocalNickname))
        {
            ShowError("Set a profile name on the Account page first.");
            return;
        }

        EnsureJoinBrowsePanel();
        if (playPanel != null) playPanel.SetActive(false);
        _joinBrowsePanel.SetActive(true);

        // Auto-refresh on open.
        _ = RefreshSessionListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CreateLobbyPanel callbacks
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCreateLobbyBack()
    {
        if (_createLobbyPanel != null) _createLobbyPanel.SetActive(false);
        if (playPanel         != null) playPanel.SetActive(true);
        if (errorStatusText   != null) errorStatusText.text = "";
    }

    private async void OnCreateLobbyConfirm()
    {
        if (_isBusy) return;
        _isBusy = true;

        // ── Read form values ─────────────────────────────────────────────────
        string lobbyName = (_lobbyNameInput != null && !string.IsNullOrWhiteSpace(_lobbyNameInput.text))
            ? _lobbyNameInput.text.Trim()
            : $"{LocalNickname}'s Lobby";

        bool isPublic  = _publicToggle  == null || _publicToggle.isOn;
        int  prepSec   = ParseDurationInput(_prepInput,   30);
        int  combatSec = ParseDurationInput(_combatInput, 60);
        int  endSec    = ParseDurationInput(_endInput,    20);
        int  bestOf    = (_bestOf3Toggle != null && _bestOf3Toggle.isOn) ? 3 : 1;

        var cfg = new LobbyConfig
        {
            lobbyName     = lobbyName,
            isPublic      = isPublic,
            hostName      = LocalNickname,
            prepSeconds   = prepSec,
            combatSeconds = combatSec,
            endSeconds    = endSec,
            bestOf        = bestOf,
        };
        ActiveLobby.Config = cfg;

        try
        {
            await EnsureServicesReadyAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ShowError(Loc.T("Non sei connesso ai servizi online."));
                _isBusy = false;
                return;
            }

            // ── Build session options with relay ─────────────────────────────
            var options = new SessionOptions
            {
                Name       = cfg.lobbyName,
                MaxPlayers = 2,
                IsPrivate  = !cfg.isPublic,
            }.WithRelayNetwork();

            // Encode LobbyConfig as public session properties so clients can read them.
            foreach (var kvp in cfg.ToProperties())
                options.SessionProperties[kvp.Key] =
                    new SessionProperty(kvp.Value, VisibilityPropertyOptions.Public);

            // ── Create session – also starts NGO host via Relay ──────────────
            CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            // ── Spawn RelayManager and show lobby ────────────────────────────
            NetworkObject netObj = Instantiate(relayManagerPrefab);
            netObj.Spawn();

            _activeRelayManager = netObj.GetComponent<RelayManager>();
            _activeRelayManager.AssignUIReferences(
                mainMenuPanel,
                lobbyPanel,
                joinCodeInputField,
                errorStatusText,
                generatedCodeText,
                playerListText);

            if (_createLobbyPanel != null) _createLobbyPanel.SetActive(false);
            _activeRelayManager.ShowLobby(CurrentSession.Code, isHost: true);
        }
        catch (SessionException se)
        {
            Debug.LogError($"[SceneUIManager] Session creation failed: {se}");
            ShowError($"Could not create lobby: {se.Message}");
            _ = LeaveCurrentSessionAsync();
            SafeShutdown();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneUIManager] Hosting failed: {e}");
            ShowError(Loc.T("Impossibile creare una stanza."));
            _ = LeaveCurrentSessionAsync();
            SafeShutdown();
        }

        _isBusy = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JoinBrowsePanel callbacks
    // ─────────────────────────────────────────────────────────────────────────

    private void OnJoinBrowseBack()
    {
        if (_joinBrowsePanel != null) _joinBrowsePanel.SetActive(false);
        if (playPanel        != null) playPanel.SetActive(true);
        if (errorStatusText  != null) errorStatusText.text = "";
    }

    /// <summary>Wrapper called by the JOIN BY CODE button.</summary>
    private void JoinByCodeButtonClicked()
    {
        if (_codeEntryInput == null) return;
        string code = _codeEntryInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code)) { ShowError("Enter a room code first."); return; }
        _ = DoJoinByCodeAsync(code);
    }

    /// <summary>Join a session using a player-visible code (works for public and private).</summary>
    private async Task DoJoinByCodeAsync(string code)
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            await EnsureServicesReadyAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ShowError(Loc.T("Non sei connesso ai servizi online."));
                _isBusy = false;
                return;
            }

            CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            if (_joinBrowsePanel != null) _joinBrowsePanel.SetActive(false);
            StartCoroutine(WaitForClientConnection(CurrentSession.Code));
        }
        catch (SessionException se)
        {
            Debug.LogError($"[SceneUIManager] Join by code failed: {se}");
            ShowError("Lobby not found! Check your code.");
            _ = LeaveCurrentSessionAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneUIManager] Joining failed: {e}");
            ShowError("Lobby not found! Check your code.");
            _ = LeaveCurrentSessionAsync();
        }

        _isBusy = false;
    }

    /// <summary>Join a session from the browse list using its service ID.</summary>
    private async Task DoJoinByIdAsync(string sessionId)
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            await EnsureServicesReadyAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ShowError(Loc.T("Non sei connesso ai servizi online."));
                _isBusy = false;
                return;
            }

            CurrentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
            if (_joinBrowsePanel != null) _joinBrowsePanel.SetActive(false);
            StartCoroutine(WaitForClientConnection(CurrentSession.Code));
        }
        catch (SessionException se)
        {
            Debug.LogError($"[SceneUIManager] Join by ID failed: {se}");
            ShowError("Failed to join lobby.");
            _ = LeaveCurrentSessionAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneUIManager] Joining failed: {e}");
            ShowError("Failed to join lobby.");
            _ = LeaveCurrentSessionAsync();
        }

        _isBusy = false;
    }

    /// <summary>Queries public open lobbies and populates the session list.</summary>
    private async Task RefreshSessionListAsync()
    {
        if (_sessionListContent == null) return;

        foreach (Transform child in _sessionListContent)
            Destroy(child.gameObject);

        if (_browseStatusText != null)
            _browseStatusText.text = "Searching for lobbies\u2026";

        try
        {
            var query = new QuerySessionsOptions
            {
                FilterOptions = new List<FilterOption>
                {
                    new FilterOption(FilterField.AvailableSlots, "0",     FilterOperation.Greater),
                    new FilterOption(FilterField.IsLocked,       "false", FilterOperation.Equal),
                }
            };

            QuerySessionsResults results =
                await MultiplayerService.Instance.QuerySessionsAsync(query);

            if (_browseStatusText != null)
                _browseStatusText.text = results.Sessions.Count == 0
                    ? "No open lobbies found. Try refreshing."
                    : "";

            foreach (ISessionInfo info in results.Sessions)
            {
                string hostDisplay = "";
                if (info.Properties != null &&
                    info.Properties.TryGetValue(LobbyConfig.KeyHostName, out SessionProperty prop))
                    hostDisplay = prop?.Value ?? "";

                AddSessionRow(info.Id, info.Name, hostDisplay);
            }
        }
        catch (SessionException se)
        {
            Debug.LogError($"[SceneUIManager] Session query failed: {se}");
            if (_browseStatusText != null) _browseStatusText.text = "Failed to load lobbies.";
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneUIManager] Query error: {e}");
            if (_browseStatusText != null) _browseStatusText.text = "Failed to load lobbies.";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NGO connection wait (client side)
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator WaitForClientConnection(string code)
    {
        // Wait for the session SDK to bring the NGO client up (it starts listening async).
        float startWait = 6f;
        while (!NetworkManager.Singleton.IsListening && startWait > 0f)
        {
            startWait -= Time.deltaTime;
            yield return null;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            ShowError(Loc.T("Connessione scaduta."));
            _ = LeaveCurrentSessionAsync();
            yield break;
        }

        // Wait for the full handshake.
        float connWait = 12f;
        while (!NetworkManager.Singleton.IsConnectedClient
               && connWait > 0f
               && NetworkManager.Singleton.IsListening)
        {
            connWait -= Time.deltaTime;
            yield return null;
        }

        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            ShowError(Loc.T("Connessione scaduta."));
            _ = LeaveCurrentSessionAsync();
            yield break;
        }

        // Wait for the host-spawned RelayManager to replicate to this client.
        float findWait = 5f;
        while (_activeRelayManager == null && findWait > 0f)
        {
            _activeRelayManager = UnityEngine.Object.FindAnyObjectByType<RelayManager>();
            findWait -= Time.deltaTime;
            yield return null;
        }

        if (_activeRelayManager != null)
        {
            _activeRelayManager.AssignUIReferences(
                mainMenuPanel,
                lobbyPanel,
                joinCodeInputField,
                errorStatusText,
                generatedCodeText,
                playerListText);

            if (playPanel != null) playPanel.SetActive(false);
            _activeRelayManager.ShowLobby(code, isHost: false);
        }
        else
        {
            ShowError("Could not sync with host.");
            _ = LeaveCurrentSessionAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lazy panel builders
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureCreateLobbyPanel()
    {
        if (_createLobbyPanel != null) return;
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[SceneUIManager] No Canvas found."); return; }
        BuildCreateLobbyPanel(canvas.transform);
    }

    private void EnsureJoinBrowsePanel()
    {
        if (_joinBrowsePanel != null) return;
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[SceneUIManager] No Canvas found."); return; }
        BuildJoinBrowsePanel(canvas.transform);
    }

    // ── CreateLobbyPanel ──────────────────────────────────────────────────────

    private void BuildCreateLobbyPanel(Transform canvas)
    {
        _createLobbyPanel = MakeFullScreenOverlay(canvas, "CreateLobbyPanel");
        Transform bt = MakeCenteredBox(_createLobbyPanel.transform, 620f, 650f).transform;

        float y = -30f;

        // Title
        MakeTopLabel(bt, "Title", "CREATE LOBBY", 30, TextAlignmentOptions.Center, 580f, 44f, ref y, 58f);

        // ── Lobby name ────────────────────────────────────────────────────────
        MakeTopLabel(bt, "LobbyNameLbl", "Lobby Name", 18, TextAlignmentOptions.Left, 580f, 26f, ref y, 32f);
        _lobbyNameInput = MakeInputField(bt, "LobbyNameInput", 580f, 44f, y);
        y -= 58f;

        // ── Visibility ────────────────────────────────────────────────────────
        MakeTopLabel(bt, "VisibilityLbl", "Visibility", 18, TextAlignmentOptions.Left, 580f, 26f, ref y, 32f);
        _publicToggle = MakeLabeledToggle(bt, "PublicToggle", "Public (visible in lobby browser)", y);
        _publicToggle.isOn = true;
        y -= 50f;

        // ── Turn segment durations ────────────────────────────────────────────
        MakeTopLabel(bt, "DurLbl", "Turn Segment Durations (seconds)",
            18, TextAlignmentOptions.Left, 580f, 26f, ref y, 36f);

        // Three labelled number inputs in a horizontal row.
        MakeDurationRow(bt, y, out _prepInput, out _combatInput, out _endInput);
        y -= 58f;

        // ── Match format ──────────────────────────────────────────────────────
        MakeTopLabel(bt, "BestOfLbl", "Match Format", 18, TextAlignmentOptions.Left, 580f, 26f, ref y, 32f);
        _bestOf3Toggle = MakeLabeledToggle(bt, "BestOf3Toggle",
            "Best of 3  (unchecked = Best of 1)", y);
        _bestOf3Toggle.isOn = false;

        // ── Buttons (bottom-anchored) ─────────────────────────────────────────
        Button createBtn = CreateButton(bt, "CreateBtn", "CREATE", ColButtonGreen, OnCreateLobbyConfirm);
        Button backBtn   = CreateButton(bt, "BackBtn",   "BACK",   ColButtonBlue,  OnCreateLobbyBack);
        AnchorBottomCenter(createBtn.GetComponent<RectTransform>(), -115f, 28f, 200f, 52f);
        AnchorBottomCenter(backBtn.GetComponent<RectTransform>(),    115f, 28f, 200f, 52f);

        _createLobbyPanel.SetActive(false);
    }

    private void MakeDurationRow(Transform parent, float anchoredY,
        out TMP_InputField prep, out TMP_InputField combat, out TMP_InputField end)
    {
        // Container with HorizontalLayoutGroup for the three field groups.
        var rowGo = new GameObject("DurRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(parent, false);
        var rowRT = rowGo.GetComponent<RectTransform>();
        rowRT.anchorMin        = new Vector2(0.5f, 1f);
        rowRT.anchorMax        = new Vector2(0.5f, 1f);
        rowRT.pivot            = new Vector2(0.5f, 1f);
        rowRT.sizeDelta        = new Vector2(560f, 44f);
        rowRT.anchoredPosition = new Vector2(0f, anchoredY);

        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleCenter;
        hlg.spacing              = 16f;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        prep   = MakeDurationGroup(rowGo.transform, "Prep",   "30");
        combat = MakeDurationGroup(rowGo.transform, "Combat", "60");
        end    = MakeDurationGroup(rowGo.transform, "End",    "20");
    }

    private TMP_InputField MakeDurationGroup(Transform parent, string labelText, string defaultVal)
    {
        var grp = new GameObject($"{labelText}Grp",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        grp.transform.SetParent(parent, false);
        grp.GetComponent<LayoutElement>().preferredWidth = 170f;

        var hlg = grp.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.spacing              = 6f;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        // Label
        var lblGo = new GameObject("Lbl", typeof(RectTransform), typeof(LayoutElement));
        lblGo.transform.SetParent(grp.transform, false);
        lblGo.GetComponent<LayoutElement>().preferredWidth = 72f;
        var lblTMP = lblGo.AddComponent<TextMeshProUGUI>();
        lblTMP.text          = labelText;
        lblTMP.fontSize      = 16;
        lblTMP.alignment     = TextAlignmentOptions.Right;
        lblTMP.color         = Color.white;
        lblTMP.raycastTarget = false;

        // Input
        var inpGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        inpGo.name = $"{labelText}Input";
        inpGo.transform.SetParent(grp.transform, false);
        var inpLE = inpGo.AddComponent<LayoutElement>();
        inpLE.preferredWidth = 90f;
        var inp = inpGo.GetComponent<TMP_InputField>();
        inp.text        = defaultVal;
        inp.contentType = TMP_InputField.ContentType.IntegerNumber;
        var bg = inpGo.GetComponent<Image>();
        if (bg != null) bg.color = new Color(1f, 1f, 1f, 0.9f);
        if (inp.textComponent != null) inp.textComponent.color = Color.black;

        return inp;
    }

    // ── JoinBrowsePanel ───────────────────────────────────────────────────────

    private void BuildJoinBrowsePanel(Transform canvas)
    {
        _joinBrowsePanel = MakeFullScreenOverlay(canvas, "JoinBrowsePanel");
        Transform bt = MakeCenteredBox(_joinBrowsePanel.transform, 700f, 730f).transform;

        float y = -28f;

        // Title
        MakeTopLabel(bt, "Title", "JOIN LOBBY", 30, TextAlignmentOptions.Center, 660f, 44f, ref y, 60f);

        // ── Public lobby list ─────────────────────────────────────────────────
        MakeTopLabel(bt, "ListTitle", "Public Open Lobbies",
            20, TextAlignmentOptions.Left, 660f, 26f, ref y, 34f);

        // Status / empty message
        _browseStatusText = MakeTopLabelAt(bt, "BrowseStatus", "Searching\u2026",
            15, TextAlignmentOptions.Center, 660f, 26f, y - 110f);

        // Scroll list
        var scrollGo = MakeScrollList(bt, "SessionList", 660f, 240f, y);
        _sessionListContent = scrollGo.transform.Find("Viewport/Content");
        y -= 258f;

        // Refresh button
        Button refreshBtn = CreateButton(bt, "RefreshBtn", "REFRESH", ColButtonGrey,
            () => _ = RefreshSessionListAsync());
        SetTopCenter(refreshBtn.GetComponent<RectTransform>(), 0f, y, 180f, 44f);
        y -= 62f;

        // ── Code entry ────────────────────────────────────────────────────────
        MakeTopLabel(bt, "CodeDivider", "\u2014  or join by code (public or private)  \u2014",
            14, TextAlignmentOptions.Center, 660f, 22f, ref y, 34f);

        _codeEntryInput = MakeInputField(bt, "CodeInput", 400f, 44f, y);
        _codeEntryInput.characterLimit = 8;
        _codeEntryInput.onValidateInput = (_, __, c) => char.ToUpper(c);
        if (_codeEntryInput.placeholder is TextMeshProUGUI ph) ph.text = "Enter room code\u2026";
        y -= 56f;

        Button joinCodeBtn = CreateButton(bt, "JoinCodeBtn", "JOIN BY CODE",
            ColButtonGreen, JoinByCodeButtonClicked);
        SetTopCenter(joinCodeBtn.GetComponent<RectTransform>(), 0f, y, 220f, 44f);

        // ── Back button (bottom-anchored) ─────────────────────────────────────
        Button backBtn = CreateButton(bt, "BackBtn", "BACK", ColButtonBlue, OnJoinBrowseBack);
        AnchorBottomCenter(backBtn.GetComponent<RectTransform>(), 0f, 28f, 200f, 52f);

        _joinBrowsePanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Session list rows
    // ─────────────────────────────────────────────────────────────────────────

    private void AddSessionRow(string sessionId, string name, string hostName)
    {
        if (_sessionListContent == null) return;

        string displayName = string.IsNullOrEmpty(name)     ? "(Unnamed Lobby)" : name;
        string displayHost = string.IsNullOrEmpty(hostName) ? "Unknown"         : hostName;

        var rowGo = new GameObject($"Row_{sessionId}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        rowGo.transform.SetParent(_sessionListContent, false);
        rowGo.GetComponent<Image>().color = new Color(0.20f, 0.20f, 0.24f, 0.90f);
        rowGo.GetComponent<LayoutElement>().preferredHeight = 54f;

        // Info label
        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(rowGo.transform, false);
        var lblRT = lblGo.GetComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0f, 0f);
        lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.offsetMin = new Vector2(10f, 0f);
        lblRT.offsetMax = new Vector2(-108f, 0f);
        var lblTMP = lblGo.AddComponent<TextMeshProUGUI>();
        lblTMP.text          = $"<b>{displayName}</b>    <color=#8899cc>Host: {displayHost}</color>";
        lblTMP.fontSize      = 15;
        lblTMP.alignment     = TextAlignmentOptions.Left;
        lblTMP.color         = Color.white;
        lblTMP.raycastTarget = false;
        lblTMP.richText      = true;

        // JOIN button
        string capturedId = sessionId;
        Button joinBtn = CreateButton(rowGo.transform, "JoinBtn", "JOIN",
            ColButtonGreen, () => _ = DoJoinByIdAsync(capturedId));
        var btnRT = joinBtn.GetComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(1f, 0.5f);
        btnRT.anchorMax        = new Vector2(1f, 0.5f);
        btnRT.pivot            = new Vector2(1f, 0.5f);
        btnRT.sizeDelta        = new Vector2(90f, 40f);
        btnRT.anchoredPosition = new Vector2(-8f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI factory helpers
    // ─────────────────────────────────────────────────────────────────────────

    private GameObject MakeFullScreenOverlay(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        return go;
    }

    private GameObject MakeCenteredBox(Transform parent, float w, float h)
    {
        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(parent, false);
        var rt = box.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = ColDarkPanel;
        return box;
    }

    /// <summary>Creates a top-anchored label and advances <paramref name="y"/> by <paramref name="step"/>.</summary>
    private TextMeshProUGUI MakeTopLabel(Transform parent, string name, string text,
        float fontSize, TextAlignmentOptions align, float w, float h, ref float y, float step)
    {
        var tmp = MakeTopLabelAt(parent, name, text, fontSize, align, w, h, y);
        y -= step;
        return tmp;
    }

    private TextMeshProUGUI MakeTopLabelAt(Transform parent, string name, string text,
        float fontSize, TextAlignmentOptions align, float w, float h, float anchoredY)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, anchoredY);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.alignment     = align;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private TMP_InputField MakeInputField(Transform parent, string name,
        float w, float h, float anchoredY)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, anchoredY);
        var inp = go.GetComponent<TMP_InputField>();
        var bg  = go.GetComponent<Image>();
        if (bg  != null) bg.color = new Color(1f, 1f, 1f, 0.9f);
        if (inp.textComponent != null) inp.textComponent.color = Color.black;
        return inp;
    }

    private Toggle MakeLabeledToggle(Transform parent, string name, string label, float anchoredY)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin        = new Vector2(0.5f, 1f);
        rootRT.anchorMax        = new Vector2(0.5f, 1f);
        rootRT.pivot            = new Vector2(0f, 1f);
        rootRT.sizeDelta        = new Vector2(500f, 34f);
        rootRT.anchoredPosition = new Vector2(-250f, anchoredY);

        // Checkbox background
        var bg = new GameObject("Background",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin        = new Vector2(0f, 0.5f);
        bgRT.anchorMax        = new Vector2(0f, 0.5f);
        bgRT.pivot            = new Vector2(0f, 0.5f);
        bgRT.sizeDelta        = new Vector2(28f, 28f);
        bgRT.anchoredPosition = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.28f, 0.28f, 0.36f, 1f);

        // Checkmark
        var check = new GameObject("Checkmark",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        check.transform.SetParent(bg.transform, false);
        var checkRT = check.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.1f, 0.1f);
        checkRT.anchorMax = new Vector2(0.9f, 0.9f);
        checkRT.offsetMin = Vector2.zero;
        checkRT.offsetMax = Vector2.zero;
        check.GetComponent<Image>().color = new Color(0.25f, 0.78f, 0.35f, 1f);

        // Label
        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(root.transform, false);
        var lblRT = lblGo.GetComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0f, 0f);
        lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.offsetMin = new Vector2(36f, 0f);
        lblRT.offsetMax = Vector2.zero;
        var lblTMP = lblGo.AddComponent<TextMeshProUGUI>();
        lblTMP.text          = label;
        lblTMP.fontSize      = 17;
        lblTMP.alignment     = TextAlignmentOptions.Left;
        lblTMP.color         = Color.white;
        lblTMP.raycastTarget = false;

        var toggle = root.AddComponent<Toggle>();
        toggle.targetGraphic = bg.GetComponent<Image>();
        toggle.graphic       = check.GetComponent<Image>();
        toggle.isOn          = false;
        return toggle;
    }

    private Button CreateButton(Transform parent, string name, string label,
        Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bgColor;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 20;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    private GameObject MakeScrollList(Transform parent, string name,
        float w, float h, float anchoredY)
    {
        // ScrollRect root
        var scrollGo = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin        = new Vector2(0.5f, 1f);
        scrollRT.anchorMax        = new Vector2(0.5f, 1f);
        scrollRT.pivot            = new Vector2(0.5f, 1f);
        scrollRT.sizeDelta        = new Vector2(w, h);
        scrollRT.anchoredPosition = new Vector2(0f, anchoredY);

        // Viewport
        var vpGo = new GameObject("Viewport",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        vpGo.transform.SetParent(scrollGo.transform, false);
        var vpRT = vpGo.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        vpGo.GetComponent<Image>().color = new Color(0.09f, 0.09f, 0.11f, 0.90f);
        vpGo.GetComponent<Mask>().showMaskGraphic = false;

        // Content (grows with children)
        var contentGo = new GameObject("Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(vpGo.transform, false);
        var contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin        = new Vector2(0f, 1f);
        contentRT.anchorMax        = new Vector2(1f, 1f);
        contentRT.pivot            = new Vector2(0.5f, 1f);
        contentRT.sizeDelta        = new Vector2(0f, 0f);
        contentRT.anchoredPosition = Vector2.zero;

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.spacing              = 5f;
        vlg.padding              = new RectOffset(6, 6, 6, 6);
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire ScrollRect
        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.viewport          = vpRT;
        sr.content           = contentRT;
        sr.horizontal        = false;
        sr.vertical          = true;
        sr.scrollSensitivity = 20f;

        return scrollGo;
    }

    private void AnchorBottomCenter(RectTransform rt, float xOffset, float yOffset, float w, float h)
    {
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(xOffset, yOffset);
    }

    private void SetTopCenter(RectTransform rt, float xOffset, float anchoredY, float w, float h)
    {
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(xOffset, anchoredY);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Misc helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static int ParseDurationInput(TMP_InputField field, int defaultVal,
        int min = 5, int max = 300)
    {
        if (field == null) return defaultVal;
        return int.TryParse(field.text, out int v) ? Mathf.Clamp(v, min, max) : defaultVal;
    }

    private void SafeShutdown()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }

    private void ShowError(string message)
    {
        if (errorStatusText == null) return;
        if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
        _errorCoroutine = StartCoroutine(ErrorRoutine(message));
    }

    private IEnumerator ErrorRoutine(string message)
    {
        errorStatusText.text = $"<color=red>{Loc.T("Errore")}: {message}</color>";
        yield return new WaitForSeconds(3f);
        if (errorStatusText != null) errorStatusText.text = "";
    }
}