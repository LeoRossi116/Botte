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
    [Tooltip("The title/landing page (Title + PLAY + OPTION + EXIT). This is the 'home' screen.")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("The connect page reached via PLAY (Host / Join / code input).")]
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TextMeshProUGUI errorStatusText;
    [SerializeField] private TextMeshProUGUI generatedCodeText;
    [SerializeField] private TextMeshProUGUI playerListText;

    [Header("Nickname Fields")]
    [SerializeField] private UnityEngine.UI.Button insertNameButton;
    [SerializeField] private TMP_InputField nicknameInputField;
    [Tooltip("Checkmark button that confirms the typed nickname.")]
    [SerializeField] private UnityEngine.UI.Button confirmNameButton;
    [Tooltip("Button that clears the nickname input field.")]
    [SerializeField] private UnityEngine.UI.Button clearNameButton;

    public static string LocalNickname { get; private set; } = "";

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

        // Force any text typed (or pasted) into the code field to UPPERCASE so it
        // matches the code shown to the host.
        if (joinCodeInputField != null)
        {
            joinCodeInputField.onValidateInput = (string text, int charIndex, char addedChar) => char.ToUpper(addedChar);
        }

        if (nicknameInputField != null)
        {
            nicknameInputField.gameObject.SetActive(false);
            nicknameInputField.onValueChanged.AddListener((val) => LocalNickname = val.Trim());
        }

        if (insertNameButton != null)
        {
            insertNameButton.onClick.AddListener(() =>
            {
                if (nicknameInputField != null)
                {
                    nicknameInputField.gameObject.SetActive(true);
                    nicknameInputField.ActivateInputField();
                }
                SetNicknameControlsActive(true);
            });
        }

        // The confirm/clear controls start hidden and appear together with the field.
        if (confirmNameButton != null)
        {
            confirmNameButton.onClick.AddListener(ClickedConfirmName);
            confirmNameButton.gameObject.SetActive(false);
        }
        if (clearNameButton != null)
        {
            clearNameButton.onClick.AddListener(ClickedClearName);
            clearNameButton.gameObject.SetActive(false);
        }

        // Initial menu state: show the title/landing page, hide everything else.
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (playPanel != null) playPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);

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

    // Title page -> connect (play) page.
    public void ClickedPlay()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (playPanel != null) playPanel.SetActive(true);
        if (errorStatusText != null) errorStatusText.text = "";

        if (nicknameInputField != null)
        {
            nicknameInputField.text = "";
            nicknameInputField.gameObject.SetActive(false);
            LocalNickname = "";
        }
        SetNicknameControlsActive(false);

        // The code field is visible from the start so a wrong/empty code can be reported
        // the very first time Join is pressed (no two-step "press Join again" reveal).
        if (joinCodeInputField != null)
        {
            joinCodeInputField.text = "";
            joinCodeInputField.gameObject.SetActive(true);
        }
    }

    // Shows/hides the confirm + clear buttons that accompany the nickname field.
    private void SetNicknameControlsActive(bool active)
    {
        if (confirmNameButton != null) confirmNameButton.gameObject.SetActive(active);
        if (clearNameButton != null) clearNameButton.gameObject.SetActive(active);
    }

    // Checkmark button: commits the typed nickname. Only complains about an empty
    // name here (never on merely revealing the field).
    public void ClickedConfirmName()
    {
        if (nicknameInputField == null) return;

        LocalNickname = nicknameInputField.text.Trim();
        if (string.IsNullOrEmpty(LocalNickname))
        {
            ShowError("insert your name before joining or hosting a game");
            nicknameInputField.ActivateInputField();
            return;
        }

        if (errorStatusText != null) errorStatusText.text = "";
        nicknameInputField.DeactivateInputField();
        Debug.Log($"Nickname confirmed: {LocalNickname}");
    }

    // Clears the nickname input field and the stored nickname.
    public void ClickedClearName()
    {
        if (nicknameInputField != null)
        {
            nicknameInputField.text = "";
            nicknameInputField.ActivateInputField();
        }
        LocalNickname = "";
        if (errorStatusText != null) errorStatusText.text = "";
    }

    // Connect (play) page -> back to title page.
    public void ClickedBackToMenu()
    {
        SafeShutdown();
        if (playPanel != null) playPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (errorStatusText != null) errorStatusText.text = "";
    }

    // Options button on the title page (not implemented yet, here for show).
    public void ClickedOptions()
    {
        // Intentionally left blank for now.
    }

    // Exit button on the title page: quits the application.
    public void ClickedExitGame()
    {
        SafeShutdown();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public async void ClickedHost()
    {
        if (_isBusy) return;
        _isBusy = true;

        if (nicknameInputField == null || !nicknameInputField.gameObject.activeSelf || string.IsNullOrEmpty(LocalNickname))
        {
            ShowError("insert your name before joining or hosting a game");
            _isBusy = false;
            return;
        }

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
            if (playPanel != null) playPanel.SetActive(false);
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

        if (nicknameInputField == null || !nicknameInputField.gameObject.activeSelf || string.IsNullOrEmpty(LocalNickname))
        {
            ShowError("insert your name before joining or hosting a game");
            _isBusy = false;
            return;
        }

        try
        {
            await InitializeServicesAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ShowError("Not signed in to online services.");
                _isBusy = false;
                return;
            }

            // The code box is always visible, so validate the entered code directly. An
            // empty field reports an error immediately (even on the very first press).
            if (joinCodeInputField != null && !joinCodeInputField.gameObject.activeSelf)
                joinCodeInputField.gameObject.SetActive(true);

            string joinCode = joinCodeInputField != null ? joinCodeInputField.text.Trim().ToUpper() : "";
            if (string.IsNullOrEmpty(joinCode))
            {
                if (joinCodeInputField != null) joinCodeInputField.ActivateInputField();
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
            if (playPanel != null) playPanel.SetActive(false);
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
            if (playPanel != null) playPanel.SetActive(false);
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