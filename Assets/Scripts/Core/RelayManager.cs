using System;
using System.Collections;
using System.Collections.Generic;
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

public class RelayManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Main Menu Elements")]
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TextMeshProUGUI errorStatusText;

    [Header("Lobby Panel Elements")]
    [SerializeField] private TextMeshProUGUI generatedCodeText;
    [SerializeField] private TextMeshProUGUI playerListText; // SINGLE dynamic text element

    private UnityTransport _transport;
    private Coroutine _errorCoroutine;

    private async void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            // Re-render the player list whenever someone connects or leaves
            NetworkManager.Singleton.OnClientConnectedCallback += UpdatePlayerListVisuals;
            NetworkManager.Singleton.OnClientDisconnectCallback += UpdatePlayerListVisuals;
        }
        else
        {
            Debug.LogError("RelayManager requires a NetworkManager component.");
            return;
        }

        if (errorStatusText != null) errorStatusText.text = "";

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch (Exception e)
        {
            ShowTimedError($"Services Setup Failed: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= UpdatePlayerListVisuals;
            NetworkManager.Singleton.OnClientDisconnectCallback -= UpdatePlayerListVisuals;
        }
    }

    // --- HOST LOGIC ---
    public async void StartHostSession()
    {
        if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();

        try
        {
            // Note: Increased allocation to allow more than 2 players based on your example
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(10); 
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
            _transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
            generatedCodeText.text = $"Room Code: <color=yellow>{joinCode}</color>";
            
            // Build the initial list (just the host)
            UpdatePlayerListVisuals(NetworkManager.Singleton.LocalClientId);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Hosting Failed: {e.Message}");
            ShowTimedError("Failed to create a room.");
        }
    }

    // --- JOIN LOGIC ---
    public async void StartClientSession()
    {
        if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();

        string joinCode = joinCodeInputField.text.Trim();
        
        if (string.IsNullOrEmpty(joinCode) || joinCode.Length < 4)
        {
            ShowTimedError("Please enter a valid room code.");
            return;
        }

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = joinAllocation.ToRelayServerData("dtls");
            _transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();

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

    // --- DYNAMIC PLAYER LIST RE-RENDER ---
    private void UpdatePlayerListVisuals(ulong clientId)
    {
        // If we are a client and get disconnected by the host, kick back to main menu
        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsConnectedClient)
        {
            ToMainMenu();
            return;
        }

        // Build the string dynamically
        string listBuilder = "Player List:\n";

        // Netcode keeps a collection of all connected IDs. Loop through them.
        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // ID 0 (or the Server/Host ID) is always the Host
            if (id == 0) 
            {
                listBuilder += "    - Host\n";
            }
            else 
            {
                listBuilder += "    - Client\n";
            }
        }

        // Apply the complete string block to our single text box
        playerListText.text = listBuilder;
    }

    // --- TIMED ERROR COROUTINE ---
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
}