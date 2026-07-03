using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using TMPro;

public class SceneUIManager : MonoBehaviour
{
    [Header("References to the Network Prefab")]
    [SerializeField] private NetworkObject relayManagerPrefab;

    [Header("Scene UI Layout References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TextMeshProUGUI errorStatusText;
    [SerializeField] private TextMeshProUGUI generatedCodeText;
    [SerializeField] private TextMeshProUGUI playerListText;

    private RelayManager activeRelayManager;
    private UnityTransport _transport;
    private Coroutine _errorCoroutine;
    private bool _isBusy;

    private async void Start()
    {
        if (errorStatusText != null) errorStatusText.text = "";

        if (NetworkManager.Singleton != null)
        {
            _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }
        else
        {
            Debug.LogError("SceneUIManager: NetworkManager Singleton was not found in the scene.");
        }

        // Initialize Unity Gaming Services up front so hosting/joining is instant.
        await InitializeServicesAsync();
    }

    private async Task InitializeServicesAsync()
    {
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
            ShowError($"Online services failed: {e.Message}");
        }
    }

    // --- BUTTON ACTIONS ---

    public async void ClickedHost()
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            await InitializeServicesAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ShowError("Not signed in to online services.");
                _isBusy = false;
                return;
            }

            // 1. Reserve a Relay server (works across the internet / different networks)
            //    BEFORE starting the host, so Netcode binds to the Relay transport.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            _transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));

            // 2. Start the host on top of the configured Relay transport.
            if (!NetworkManager.Singleton.StartHost())
            {
                ShowError("Failed to start host.");
                SafeShutdown();
                _isBusy = false;
                return;
            }

            // 3. Spawn the networked RelayManager and hand it the UI references.
            NetworkObject netObject = Instantiate(relayManagerPrefab);
            netObject.Spawn();

            activeRelayManager = netObject.GetComponent<RelayManager>();
            activeRelayManager.AssignUIReferences(
                mainMenuPanel,
                lobbyPanel,
                joinCodeInputField,
                errorStatusText,
                generatedCodeText,
                playerListText
            );
            activeRelayManager.ShowLobby(joinCode, true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Hosting failed: {e}");
            ShowError("Failed to create a room.");
            SafeShutdown();
        }

        _isBusy = false;
    }

    public async void ClickedJoin()
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            await InitializeServicesAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ShowError("Not signed in to online services.");
                _isBusy = false;
                return;
            }

            string joinCode = joinCodeInputField != null ? joinCodeInputField.text.Trim() : "";
            if (string.IsNullOrEmpty(joinCode))
            {
                ShowError("Enter a room code first.");
                _isBusy = false;
                return;
            }

            // 1. Join the Relay allocation BEFORE starting the client.
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            _transport.SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));

            // 2. Start the client on the Relay transport.
            if (!NetworkManager.Singleton.StartClient())
            {
                ShowError("Failed to start client.");
                SafeShutdown();
                _isBusy = false;
                return;
            }

            StartCoroutine(WaitForClientConnection(joinCode));
        }
        catch (Exception e)
        {
            Debug.LogError($"Joining failed: {e}");
            ShowError("Lobby not found! Check your code.");
            SafeShutdown();
        }

        _isBusy = false;
    }

    private IEnumerator WaitForClientConnection(string joinCode)
    {
        // Wait until we are connected to the session (with a timeout).
        float timeout = 12f;
        while (!NetworkManager.Singleton.IsConnectedClient && timeout > 0f && NetworkManager.Singleton.IsListening)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            ShowError("Connection timed out.");
            SafeShutdown();
            yield break;
        }

        // Wait for the host-spawned RelayManager to replicate to this client.
        float findTimeout = 5f;
        while (activeRelayManager == null && findTimeout > 0f)
        {
            activeRelayManager = UnityEngine.Object.FindFirstObjectByType<RelayManager>();
            findTimeout -= Time.deltaTime;
            yield return null;
        }

        if (activeRelayManager != null)
        {
            activeRelayManager.AssignUIReferences(
                mainMenuPanel,
                lobbyPanel,
                joinCodeInputField,
                errorStatusText,
                generatedCodeText,
                playerListText
            );
            activeRelayManager.ShowLobby(joinCode, false);
        }
        else
        {
            ShowError("Could not sync with host.");
            SafeShutdown();
        }
    }

    public void ClickedBack()
    {
        if (activeRelayManager != null)
        {
            activeRelayManager.ToMainMenu();
        }
        else
        {
            SafeShutdown();
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }
    }

    private void SafeShutdown()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void ShowError(string message)
    {
        if (errorStatusText == null) return;
        if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
        _errorCoroutine = StartCoroutine(ErrorRoutine(message));
    }

    private IEnumerator ErrorRoutine(string message)
    {
        errorStatusText.text = $"<color=red>Error: {message}</color>";
        yield return new WaitForSeconds(3f);
        if (errorStatusText != null) errorStatusText.text = "";
    }
}