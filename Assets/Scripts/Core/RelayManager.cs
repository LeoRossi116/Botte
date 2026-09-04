using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using TMPro;

public class RelayManager : NetworkBehaviour
{
    public static RelayManager Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Main Menu Elements")]
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TextMeshProUGUI errorStatusText;

    [Header("Lobby Panel Elements")]
    [SerializeField] private TextMeshProUGUI generatedCodeText;
    [SerializeField] private TextMeshProUGUI playerListText;
    [SerializeField] private TextMeshProUGUI _lobbyConfigText; // read-only lobby options display; assignable in Inspector, auto-created if null

    private Coroutine _errorCoroutine;
    private UnityEngine.UI.Button _startGameButton;
    // --- READY SYSTEM & CONFIG DISPLAY FIELDS ---
    private TextMeshProUGUI _startButtonText;  // TMP text child of _startGameButton
    private bool _clientReady;                 // server: whether the connected client is ready
    private bool _localClientReady;            // client: own ready toggle state

    private readonly System.Collections.Generic.Dictionary<ulong, string> _playerNames = new System.Collections.Generic.Dictionary<ulong, string>();

    // The two players' display names, replicated to EVERY peer (only the server owns the
    // full _playerNames dictionary, so these mirror it to clients for in-battle name labels).
    private string _hostName = "";
    private string _clientName = "";

    // Local player's display name (the nickname shown on the LEFT side of the battle screen).
    public string LocalPlayerName
    {
        get
        {
            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            string n = isServer ? _hostName : _clientName;
            return string.IsNullOrEmpty(n) ? SceneUIManager.LocalNickname : n;
        }
    }

    // Opponent's display name (the nickname shown on the RIGHT side of the battle screen).
    public string OpponentPlayerName
    {
        get
        {
            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            return isServer ? _clientName : _hostName;
        }
    }

    // True while WE deliberately leave (leave lobby / normal game end). Used to
    // suppress the misleading "disconnected" error that Shutdown would otherwise raise.
    private bool _leavingIntentionally;

    // Raw (uncolored) room code, kept so it can be copied to the clipboard on click.
    private string _currentJoinCode = "";
    private UnityEngine.UI.Button _copyCodeButton;
    private Coroutine _copyFeedbackCoroutine;

    // --- LOBBY CHAT ---
    [Header("Lobby Chat Elements")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private TextMeshProUGUI chatDisplayText;
    private readonly System.Collections.Generic.Queue<string> _chatHistory = new System.Collections.Generic.Queue<string>();
    private const int MaxChatMessages = 8;

    public static bool IsMultiplayer
    {
        get
        {
            return NetworkManager.Singleton != null && 
                   NetworkManager.Singleton.IsListening && 
                   NetworkManager.Singleton.ConnectedClientsIds.Count >= 2;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    // Subscribe to network events only when the network actively starts
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnected;

            _playerNames.Clear();

            // If we are a client joining, the host will update us. 
            // If we are the host, we update the list now.
            if (NetworkManager.Singleton.IsServer)
            {
                _playerNames[NetworkManager.ServerClientId] = SceneUIManager.LocalNickname;
                UpdateAndBroadcastPlayerList();
            }
            else
            {
                RegisterPlayerNameServerRpc(SceneUIManager.LocalNickname);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnPlayerConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnPlayerDisconnected;
        }
    }

    // --- LOBBY UI ENTRY ---
    // Relay allocation and NetworkManager start are handled by SceneUIManager
    // BEFORE this networked object is spawned. This only drives the lobby UI.
    public void ShowLobby(string joinCode, bool isHost)
    {
        // Fresh lobby session: an incoming Shutdown from now on is unexpected unless we set this.
        _leavingIntentionally = false;
        _currentJoinCode = joinCode;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (generatedCodeText != null) generatedCodeText.text = $"Codice Stanza: <color=yellow>{joinCode}</color>";

        // The start/ready button is shown to both host and client; only its role differs.
        if (_startGameButton != null)
        {
            _startGameButton.gameObject.SetActive(true);
            if (isHost)
            {
                // Reset server-side client-ready whenever the lobby is (re)opened.
                _clientReady = false;
                UpdateStartButtonInteractable();
            }
            else
            {
                // Client entering / re-entering lobby: reset local ready state.
                _localClientReady = false;
                if (_startButtonText != null) _startButtonText.text = "READY";
                _startGameButton.interactable = true;
            }
        }

        // Populate (or refresh) the read-only settings display for this peer.
        RefreshLobbyConfigDisplay();

        // Reset chat for a fresh lobby session
        _chatHistory.Clear();
        if (chatDisplayText != null) chatDisplayText.text = "";
        if (chatInputField != null) chatInputField.text = "";

        // Now that UI references are assigned, apply any cached list and refresh.
        if (playerListText != null) playerListText.text = _lastPlayerList;

        if (isHost)
        {
            UpdateAndBroadcastPlayerList();
        }
        else
        {
            // Ask the server to (re)broadcast the current player list now that
            // this client's UI is ready to display it.
            RequestPlayerListRefreshServerRpc();
        }
    }

    // --- LEAVE / DISCONNECT LOGIC ---
    public void ToMainMenu()
    {
        // We are leaving on purpose; don't let the resulting Shutdown raise a
        // "disconnected" error over a victory / normal message.
        _leavingIntentionally = true;

        // Route through the managed session layer when a session is active; the session
        // SDK tears down the NGO relay connection automatically. Fall back to a direct
        // Shutdown only when there is no managed session (e.g. local solo game).
        if (SceneUIManager.CurrentSession != null)
        {
            _ = SceneUIManager.LeaveCurrentSessionAsync();
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (joinCodeInputField != null) joinCodeInputField.text = "";

        lobbyPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // --- CONNECTION HANDLERS ---
    private void OnPlayerConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // Limit player count to 2
            if (NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
            {
                NetworkManager.Singleton.DisconnectClient(clientId, "Lobby is full!");
                return;
            }
            // A new player in the lobby means ready state must be rechecked.
            _clientReady = false;
            UpdateAndBroadcastPlayerList();
            UpdateStartButtonInteractable();
        }
    }

    private void OnPlayerDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            _playerNames.Remove(clientId);
            // Disconnected client is no longer ready; re-gate the Start button.
            _clientReady = false;
            UpdateAndBroadcastPlayerList();
            UpdateStartButtonInteractable();
            return;
        }

        // --- Client side ---

        // If WE chose to leave (left the lobby, or the game ended normally), do NOT
        // show a disconnect error. This preserves the victory / normal message and
        // avoids the false "disconnection" text after a game finishes.
        if (_leavingIntentionally)
        {
            return;
        }

        // Genuine unexpected disconnect. Pick a short, friendly message and never
        // surface the raw (often very long) transport DisconnectReason string.
        string reason = NetworkManager.Singleton.DisconnectReason;
        string message;
        if (!string.IsNullOrEmpty(reason) && reason.Length < 60)
        {
            // Short, human-authored reasons (e.g. "Lobby is full!") are worth showing.
            message = reason;
        }
        else if (lobbyPanel != null && lobbyPanel.activeSelf)
        {
            message = "The host closed the lobby.";
        }
        else
        {
            message = "Connection to the host was lost.";
        }

        ShowTimedError(message);
        ToMainMenu();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterPlayerNameServerRpc(string nickname, ServerRpcParams serverRpcParams = default)
    {
        ulong senderId = serverRpcParams.Receive.SenderClientId;
        _playerNames[senderId] = nickname;
        UpdateAndBroadcastPlayerList();
        // Belt-and-suspenders: sync the host's lobby config to the newly registered client.
        BroadcastLobbyConfig();
    }

    // --- NETWORK STRING BUILDER & SYNC ENGINE ---
    private void UpdateAndBroadcastPlayerList()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        string listBuilder = "Lista Giocatori:\n";

        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            string name = "Player";
            if (_playerNames.TryGetValue(id, out string nickname) && !string.IsNullOrEmpty(nickname))
            {
                name = nickname;
            }
            else
            {
                name = (id == NetworkManager.ServerClientId) ? "Host" : "Client";
            }
            // Append ready status for non-host clients so both peers can see it.
            if (id != NetworkManager.ServerClientId)
                name += _clientReady
                    ? " <color=#2ECC71>(Ready)</color>"
                    : " <color=#FF6B6B>(Not Ready)</color>";

            listBuilder += $"    - {name}\n";
        }

        UpdatePlayerListClientRpc(listBuilder);

        // Also replicate the resolved host/client display names to every peer so the
        // battle screen can label each side with the correct player name.
        string hostName = ResolveName(NetworkManager.ServerClientId);
        string clientName = "";
        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (id != NetworkManager.ServerClientId) { clientName = ResolveName(id); break; }
        }
        SyncPlayerNamesClientRpc(hostName, clientName);
    }

    // Resolves a connected player's display name, falling back to Host/Client labels.
    private string ResolveName(ulong id)
    {
        if (_playerNames.TryGetValue(id, out string n) && !string.IsNullOrEmpty(n)) return n;
        return id == NetworkManager.ServerClientId ? "Host" : "Client";
    }

    [ClientRpc]
    private void SyncPlayerNamesClientRpc(string hostName, string clientName)
    {
        _hostName = hostName;
        _clientName = clientName;
    }

    private string _lastPlayerList = "";

    [ClientRpc]
    private void UpdatePlayerListClientRpc(string fullListText)
    {
        _lastPlayerList = fullListText;
        // UI references may not be assigned yet (e.g. RPC arriving during spawn),
        // so guard against a null target.
        if (playerListText != null)
        {
            playerListText.text = fullListText;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerListRefreshServerRpc()
    {
        UpdateAndBroadcastPlayerList();
        // Belt-and-suspenders: ensure the client has the host's lobby config.
        BroadcastLobbyConfig();
    }

    // --- LOBBY TEXT CHAT ---
    // Called by the Send button and by pressing Enter in the chat input field.
    public void OnChatSubmit()
    {
        if (chatInputField == null) return;

        string message = chatInputField.text.Trim();
        chatInputField.text = "";
        chatInputField.ActivateInputField();

        if (string.IsNullOrEmpty(message)) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        string senderName = !string.IsNullOrEmpty(SceneUIManager.LocalNickname) ? SceneUIManager.LocalNickname : (NetworkManager.Singleton.IsServer ? "Host" : "Guest");
        SubmitChatMessageServerRpc(senderName, message);
    }

    private void OnChatSubmitString(string _)
    {
        OnChatSubmit();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitChatMessageServerRpc(string sender, string message)
    {
        BroadcastChatMessageClientRpc(sender, message);
    }

    [ClientRpc]
    private void BroadcastChatMessageClientRpc(string sender, string message)
    {
        _chatHistory.Enqueue($"<b><color=#FFD54A>{sender}:</color></b> {message}");
        while (_chatHistory.Count > MaxChatMessages) _chatHistory.Dequeue();

        if (chatDisplayText != null)
        {
            chatDisplayText.text = string.Join("\n", _chatHistory);
        }
    }

    // --- CLIPBOARD COPY (room code) ---
    public void CopyCodeToClipboard()
    {
        if (string.IsNullOrEmpty(_currentJoinCode)) return;
        GUIUtility.systemCopyBuffer = _currentJoinCode;

        if (_copyFeedbackCoroutine != null) StopCoroutine(_copyFeedbackCoroutine);
        _copyFeedbackCoroutine = StartCoroutine(CopyFeedbackRoutine());
    }

    private IEnumerator CopyFeedbackRoutine()
    {
        if (generatedCodeText != null)
        {
            generatedCodeText.text = "<color=#2ECC71>Codice Copiato!</color>";
            yield return new WaitForSeconds(1.0f);
            if (generatedCodeText != null)
                generatedCodeText.text = $"Codice Stanza: <color=yellow>{_currentJoinCode}</color>";
        }
    }

    // --- DISAPPEARING ERROR HANDLING ---
    public void ShowTimedError(string message)
    {
        if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
        _errorCoroutine = StartCoroutine(ErrorTimerTextRoutine(message));
    }

    private IEnumerator ErrorTimerTextRoutine(string errorMessage)
    {
        if (errorStatusText != null)
        {
            if (errorMessage.StartsWith("Game Finished", StringComparison.OrdinalIgnoreCase))
            {
                errorStatusText.text = $"<color=yellow>{errorMessage}</color>";
            }
            else if (errorMessage.StartsWith("The host closed the lobby", StringComparison.OrdinalIgnoreCase)
                  || errorMessage.StartsWith("Connection to the host was lost", StringComparison.OrdinalIgnoreCase)
                  || errorMessage.StartsWith("You left the game", StringComparison.OrdinalIgnoreCase))
            {
                // Friendly, non-alarming notice rather than a red "Error:".
                errorStatusText.text = $"<color=#FFD54A>{errorMessage}</color>";
            }
            else if (errorMessage.StartsWith("The opponent has left the game", StringComparison.OrdinalIgnoreCase))
            {
                // Victory-by-forfeit notice.
                errorStatusText.text = $"<color=#2ECC71>{errorMessage}</color>";
            }
            else
            {
                errorStatusText.text = $"<color=red>Error: {errorMessage}</color>";
            }
        }
        yield return new WaitForSeconds(3.0f);
        if (errorStatusText != null)
        {
            errorStatusText.text = "";
        }
    }

    public void AssignUIReferences(
        GameObject mainPanel, 
        GameObject lobPanel, 
        TMP_InputField inputField, 
        TextMeshProUGUI errorText, 
        TextMeshProUGUI codeText, 
        TextMeshProUGUI listText)
    {
        mainMenuPanel = mainPanel;
        lobbyPanel = lobPanel;
        joinCodeInputField = inputField;
        errorStatusText = errorText;
        generatedCodeText = codeText;
        playerListText = listText;

        // Make the room-code label clickable so any player can copy the code.
        if (generatedCodeText != null)
        {
            // The label's TMP text had raycastTarget disabled, so the copy Button never
            // received clicks. Enable it so clicking the code copies it to the clipboard.
            generatedCodeText.raycastTarget = true;

            _copyCodeButton = generatedCodeText.GetComponent<UnityEngine.UI.Button>();
            if (_copyCodeButton == null)
            {
                _copyCodeButton = generatedCodeText.gameObject.AddComponent<UnityEngine.UI.Button>();
                _copyCodeButton.transition = UnityEngine.UI.Selectable.Transition.None;
            }
            _copyCodeButton.onClick.RemoveAllListeners();
            _copyCodeButton.onClick.AddListener(CopyCodeToClipboard);
        }

        // Dynamically find and bind StartGameButton.
        // The button is shown on BOTH host (as "Start") and client (as "READY").
        if (lobbyPanel != null)
        {
            _startGameButton = lobbyPanel.transform.Find("StartGameButton")?.GetComponent<UnityEngine.UI.Button>();
            if (_startGameButton != null)
            {
                _startButtonText = _startGameButton.GetComponentInChildren<TextMeshProUGUI>();
                _startGameButton.onClick.RemoveAllListeners();
                _startGameButton.gameObject.SetActive(true); // visible to both host and client

                if (NetworkManager.Singleton.IsServer)
                {
                    // Host: wire to start game; initially not interactable until client is ready.
                    _startGameButton.onClick.AddListener(OnStartGameButtonClicked);
                    _startGameButton.interactable = false;
                }
                else
                {
                    // Client: relabel to READY and wire the ready toggle.
                    if (_startButtonText != null) _startButtonText.text = "READY";
                    _startGameButton.onClick.AddListener(OnReadyButtonClicked);
                    _startGameButton.interactable = true;
                }
            }

            // Dynamically find and bind the lobby chat UI (children of "ChatPanel")
            Transform chatPanel = lobbyPanel.transform.Find("ChatPanel");
            if (chatPanel != null)
            {
                chatInputField = chatPanel.Find("ChatInput")?.GetComponent<TMP_InputField>();
                chatDisplayText = chatPanel.Find("ChatDisplay")?.GetComponent<TextMeshProUGUI>();

                var sendButton = chatPanel.Find("ChatSendButton")?.GetComponent<UnityEngine.UI.Button>();
                if (sendButton != null)
                {
                    sendButton.onClick.RemoveAllListeners();
                    sendButton.onClick.AddListener(OnChatSubmit);
                }

                if (chatInputField != null)
                {
                    chatInputField.onSubmit.RemoveListener(OnChatSubmitString);
                    chatInputField.onSubmit.AddListener(OnChatSubmitString);
                }
            }
        }
    }

    private void OnStartGameButtonClicked()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (playerCount == 1)
        {
            // Only 1 player (Host) -> local game!
            NetworkManager.Singleton.Shutdown();
            lobbyPanel.SetActive(false);
            var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            if (bm != null)
            {
                bm.OnPlayMenuPressed();
            }
        }
        else if (playerCount == 2)
        {
            // Lock the session so it leaves the public browse list once the match begins.
            _ = SceneUIManager.LockCurrentSessionAsync();
            // 2 players -> start multiplayer character select!
            StartMultiplayerCharacterSelectClientRpc();
        }
    }

    [ClientRpc]
    private void StartMultiplayerCharacterSelectClientRpc()
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }
        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null)
        {
            bm.OnPlayMenuPressed();
        }
    }

    // --- GAMEPLAY SYNCHRONIZATION RPCS ---

    public void SendHeroSelection(int player, int classIdx)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SelectHeroClientRpc(player, classIdx);
        }
        else
        {
            SelectHeroServerRpc(player, classIdx);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SelectHeroServerRpc(int player, int classIdx)
    {
        SelectHeroClientRpc(player, classIdx);
    }

    [ClientRpc]
    private void SelectHeroClientRpc(int player, int classIdx)
    {
        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null)
        {
            bm.SelectClassLocal(player, classIdx);
        }
    }

    public void SendStartBattle(int seed)
    {
        StartBattleClientRpc(seed);
    }

    [ClientRpc]
    private void StartBattleClientRpc(int seed)
    {
        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null)
        {
            UnityEngine.Random.InitState(seed);
            bm.OnStartBattlePressedLocal();
        }
    }

    public void SendGameplayAction(Botte.Core.GameplayActionType actionType, int arg1, int arg2)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            GameplayActionClientRpc(actionType, arg1, arg2);
        }
        else
        {
            GameplayActionServerRpc(actionType, arg1, arg2);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void GameplayActionServerRpc(Botte.Core.GameplayActionType actionType, int arg1, int arg2)
    {
        GameplayActionClientRpc(actionType, arg1, arg2);
    }

    [ClientRpc]
    private void GameplayActionClientRpc(Botte.Core.GameplayActionType actionType, int arg1, int arg2)
    {
        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null)
        {
            bm.ExecuteActionLocal(actionType, arg1, arg2);
        }
    }

    public void SendTimerUpdate(int secondsLeft)
    {
        UpdateTimerClientRpc(secondsLeft);
    }

    [ClientRpc]
    private void UpdateTimerClientRpc(int secondsLeft)
    {
        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null)
        {
            bm.UpdateTimerText(secondsLeft);
        }
    }

    // Resolves the winner's display name (the player's nickname), never the raw "Host"/"Client"
    // labels. Called on the server, which is the only peer that knows every player's nickname.
    public string GetWinnerDisplayName(bool hostWon)
    {
        ulong id = NetworkManager.ServerClientId;
        if (!hostWon)
        {
            foreach (ulong cid in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (cid != NetworkManager.ServerClientId) { id = cid; break; }
            }
        }
        if (_playerNames.TryGetValue(id, out string n) && !string.IsNullOrEmpty(n)) return n;
        return hostWon ? "Host" : "Client";
    }

    // Server-only: tells both peers to show the winner window (still on the battle screen)
    // announcing the given winner name. The network session is intentionally kept alive so
    // players can choose to return to the same lobby.
    public void BroadcastWinner(string winnerName)
    {
        if (!IsServer) return;
        ShowWinnerClientRpc(winnerName);
    }

    [ClientRpc]
    private void ShowWinnerClientRpc(string winnerName)
    {
        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null) bm.ShowNetworkWinner(winnerName);
    }

    // Returns to the shared lobby without tearing the network session down, so both players
    // who choose LOBBY reconnect to the same room. The battle view is hidden by BattleManager.
    public void ReturnToLobby()
    {
        if (string.IsNullOrEmpty(_currentJoinCode)) return;
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        ShowLobby(_currentJoinCode, isHost);
    }

    // Winner-screen "ESCI" pressed by a player after a match ends.
    // - HOST: closes the shared lobby for everyone; every peer tears the battle view down
    //   and returns to the main menu, so both players end up on the menu together.
    // - CLIENT: leaves on its own (disconnects); the host keeps the lobby alive and stays host.
    // This preserves the host/client roles for players who choose LOBBY instead.
    public void LeaveMatchToMenu()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            ToMainMenu();
            return;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            // Host closes the lobby for all peers (host included, via the ClientRpc body).
            CloseLobbyClientRpc();
        }
        else
        {
            // Only this client leaves; the host remains in the lobby.
            ToMainMenu();
        }
    }

    // Runs on the host and every remaining client when the host closes the lobby. Each peer
    // hides the battle/winner UI, then shuts its own session down and returns to the menu.
    [ClientRpc]
    private void CloseLobbyClientRpc()
    {
        // We are leaving on purpose: suppress the misleading "disconnected" error.
        _leavingIntentionally = true;

        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null) bm.ForceReturnToMainMenu();

        ToMainMenu();
    }

    public void EndMultiplayerGame(string message)
    {
        EndGameClientRpc(message);
    }

    [ClientRpc]
    private void EndGameClientRpc(string message)
    {
        ShowTimedError(message);
        ToMainMenu();
    }

    // --- MID-MATCH QUIT ---
    // Called by the player who presses the in-game Quit button. The other (remaining)
    // player is told the opponent left and is declared the winner; both return to the
    // main menu. Falls back to a plain local return if we are not actually networked.
    public void QuitMatchToMenu()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            var bmLocal = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            if (bmLocal != null) bmLocal.ForceReturnToMainMenu();
            return;
        }

        ulong quitterId = NetworkManager.Singleton.LocalClientId;
        if (NetworkManager.Singleton.IsServer)
        {
            QuitMatchClientRpc(quitterId);
        }
        else
        {
            QuitMatchServerRpc(quitterId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void QuitMatchServerRpc(ulong quitterId)
    {
        QuitMatchClientRpc(quitterId);
    }

    [ClientRpc]
    private void QuitMatchClientRpc(ulong quitterId)
    {
        bool iQuit = NetworkManager.Singleton.LocalClientId == quitterId;
        string message = iQuit
            ? "You left the game."
            : "The opponent has left the game. You win!";

        ShowTimedError(message);

        // Reset battle UI locally, then tear down the network session and show the menu.
        var bm = UnityEngine.Object.FindFirstObjectByType<Botte.Core.BattleManager>();
        if (bm != null) bm.ForceReturnToMainMenu();

        ToMainMenu();
    }

    // --- READY SYSTEM ---

    // Called on the client when the player clicks the READY button (which reuses StartGameButton).
    private void OnReadyButtonClicked()
    {
        _localClientReady = !_localClientReady;
        if (_startButtonText != null)
            _startButtonText.text = _localClientReady ? "READY \u2713" : "READY";
        SetClientReadyServerRpc(_localClientReady);
    }

    // Client → Server: update the ready state and refresh gating + player list on both peers.
    [ServerRpc(RequireOwnership = false)]
    private void SetClientReadyServerRpc(bool ready)
    {
        _clientReady = ready;
        UpdateAndBroadcastPlayerList();
        UpdateStartButtonInteractable();
    }

    // Server-only: recompute whether the host's Start button should be interactable.
    // Rules: 1 player (solo) → always startable; 2 players → require client ready.
    private void UpdateStartButtonInteractable()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (_startGameButton == null) return;
        int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        bool canStart = (playerCount == 1) || (playerCount >= 2 && _clientReady);
        _startGameButton.interactable = canStart;
    }

    // --- HOST → CLIENT CONFIG SYNC ---

    // Server helper: reads ActiveLobby.Config and broadcasts it to all clients via ClientRpc.
    private void BroadcastLobbyConfig()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        LobbyConfig cfg = ActiveLobby.Config;
        if (cfg == null) return;
        SyncLobbyConfigClientRpc(
            cfg.lobbyName     ?? "",
            cfg.isPublic,
            cfg.hostName      ?? "",
            cfg.prepSeconds,
            cfg.combatSeconds,
            cfg.endSeconds,
            cfg.bestOf);
    }

    // Runs on every peer; only clients act on it (server already owns the canonical config).
    [ClientRpc]
    private void SyncLobbyConfigClientRpc(
        string lobbyName, bool isPublic, string hostName,
        int prep, int combat, int end, int bestOf)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;

        ActiveLobby.Config = new LobbyConfig
        {
            lobbyName     = lobbyName,
            isPublic      = isPublic,
            hostName      = hostName,
            prepSeconds   = prep,
            combatSeconds = combat,
            endSeconds    = end,
            bestOf        = bestOf,
        };
        RefreshLobbyConfigDisplay();
    }

    // --- READ-ONLY LOBBY CONFIG DISPLAY ---

    // Creates (lazily) and refreshes a TMP text showing lobby settings under the lobby panel.
    // Called on the host from ShowLobby and on clients from SyncLobbyConfigClientRpc.
    private void RefreshLobbyConfigDisplay()
    {
        if (lobbyPanel == null) return;

        if (_lobbyConfigText == null)
        {
            var go = new GameObject("LobbyConfigText", typeof(RectTransform));
            go.transform.SetParent(lobbyPanel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 1f);
            rt.anchorMax        = new Vector2(0.5f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = new Vector2(500f, 80f);
            rt.anchoredPosition = new Vector2(0f, -90f);
            _lobbyConfigText = go.AddComponent<TextMeshProUGUI>();
            _lobbyConfigText.fontSize      = 14f;
            _lobbyConfigText.alignment     = TextAlignmentOptions.Center;
            _lobbyConfigText.color         = new Color(0.75f, 0.85f, 1f, 1f);
            _lobbyConfigText.raycastTarget = false;
        }

        LobbyConfig cfg = ActiveLobby.Config;
        if (cfg == null || string.IsNullOrEmpty(cfg.lobbyName))
        {
            _lobbyConfigText.text = "";
            return;
        }

        string bestOfStr = cfg.bestOf >= 3 ? "Best of 3" : "Best of 1";
        _lobbyConfigText.text =
            $"<b>{cfg.lobbyName}</b>\n" +
            $"Prep <color=#FFD54A>{cfg.prepSeconds}s</color>  " +
            $"Combat <color=#FFD54A>{cfg.combatSeconds}s</color>  " +
            $"End <color=#FFD54A>{cfg.endSeconds}s</color>  |  {bestOfStr}";
    }
}