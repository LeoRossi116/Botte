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

    private UnityTransport _transport;
    private Coroutine _errorCoroutine;
    private UnityEngine.UI.Button _startGameButton;

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

    // FIX: Use standard Unity Start so the script wakes up immediately when the scene loads
    private async void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }
        else
        {
            Debug.LogError("RelayManager: NetworkManager Singleton was not found in the scene.");
            return;
        }

        if (errorStatusText != null) errorStatusText.text = "";

        // Initialize Unity Gaming Services right away so buttons are ready
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in anonymously. Player ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            ShowTimedError($"Services Setup Failed: {e.Message}");
        }
    }

    // Subscribe to network events only when the network actively starts
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnected;

            // If we are a client joining, the host will update us. 
            // If we are the host, we update the list now.
            if (NetworkManager.Singleton.IsServer)
            {
                UpdateAndBroadcastPlayerList();
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

    // --- HOST LOGIC ---
    public async void StartHostSession()
    {
        try
        {
            // Limit max connections to 1 (meaning 1 client + host = 2 players total)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1); 
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
            _transport.SetRelayServerData(relayServerData);

            // ❌ REMOVE THIS LINE: NetworkManager.Singleton.StartHost(); (SceneUIManager handles it now)

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
            generatedCodeText.text = $"Room Code: <color=yellow>{joinCode}</color>";
            
            // Check host startGameButton visibility
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(true);
            }

            UpdateAndBroadcastPlayerList();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Hosting Failed: {e.Message}");
            ShowTimedError("Failed to create a room.");
        }
    }

    // Update your Join function to remove the NetworkManager.Singleton.StartClient() line:
    public async void StartClientSession()
    {
        string joinCode = joinCodeInputField.text.Trim();
        
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = joinAllocation.ToRelayServerData("dtls");
            _transport.SetRelayServerData(relayServerData);

            // ❌ REMOVE THIS LINE: NetworkManager.Singleton.StartClient(); (SceneUIManager handles it now)

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
            generatedCodeText.text = $"Room Code: <color=yellow>{joinCode}</color>";

            // Client cannot start the game
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(false);
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Joining Failed: {e.Message}");
            ShowTimedError("Lobby not found! Check your code.");
        }
    }

    // --- LEAVE / DISCONNECT LOGIC ---
    public void ToMainMenu()
    {
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
        if (!NetworkManager.Singleton.IsServer)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            if (!string.IsNullOrEmpty(reason))
            {
                ShowTimedError(reason);
            }
            else
            {
                ShowTimedError("Disconnected from host.");
            }
            ToMainMenu();
            return;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            UpdateAndBroadcastPlayerList();
        }
    }

    // --- NETWORK STRING BUILDER & SYNC ENGINE ---
    private void UpdateAndBroadcastPlayerList()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        string listBuilder = "Player List:\n";

        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (id == NetworkManager.ServerClientId) 
            {
                listBuilder += "    - Host\n";
            }
            else 
            {
                listBuilder += "    - Client\n";
            }
        }

        UpdatePlayerListClientRpc(listBuilder);
    }

    [ClientRpc]
    private void UpdatePlayerListClientRpc(string fullListText)
    {
        playerListText.text = fullListText;
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
}