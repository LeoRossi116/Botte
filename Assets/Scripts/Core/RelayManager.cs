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

    private Coroutine _errorCoroutine;
    private UnityEngine.UI.Button _startGameButton;

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

        if (_startGameButton != null)
        {
            _startGameButton.gameObject.SetActive(isHost);
        }

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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
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
            UpdateAndBroadcastPlayerList();
        }
    }

    private void OnPlayerDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            _playerNames.Remove(clientId);
            UpdateAndBroadcastPlayerList();
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

        // Dynamically find and bind StartGameButton
        if (lobbyPanel != null)
        {
            _startGameButton = lobbyPanel.transform.Find("StartGameButton")?.GetComponent<UnityEngine.UI.Button>();
            if (_startGameButton != null)
            {
                _startGameButton.onClick.RemoveAllListeners();
                _startGameButton.onClick.AddListener(OnStartGameButtonClicked);
                _startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsServer);
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
}