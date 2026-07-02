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
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(10); 
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
            _transport.SetRelayServerData(relayServerData);

            // ❌ REMOVE THIS LINE: NetworkManager.Singleton.StartHost(); (SceneUIManager handles it now)

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
            generatedCodeText.text = $"Room Code: <color=yellow>{joinCode}</color>";
            
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
            UpdateAndBroadcastPlayerList();
        }
    }

    private void OnPlayerDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.ServerClientId)
        {
            Debug.Log("Host closed the room. Returning to main menu.");
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
    private void ShowTimedError(string message)
    {
        if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
        _errorCoroutine = StartCoroutine(ErrorTimerTextRoutine(message));
    }

    private IEnumerator ErrorTimerTextRoutine(string errorMessage)
    {
        errorStatusText.text = $"<color=red>Error: {errorMessage}</color>";
        yield return new WaitForSeconds(3.0f);
        errorStatusText.text = "";
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
    }
}