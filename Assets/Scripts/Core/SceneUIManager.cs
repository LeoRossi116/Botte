using UnityEngine;
using Unity.Netcode;
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

    // --- BUTTON ACTIONS ---

    public void ClickedHost()
    {
        // 1. The Host turns on the network manager first
        NetworkManager.Singleton.StartHost();

        // 2. Now that the network is live, we spawn our RelayManager across the network
        if (relayManagerPrefab != null && NetworkManager.Singleton.IsServer)
        {
            NetworkObject netObject = Instantiate(relayManagerPrefab);
            netObject.Spawn(); // Officially spawns it into Netcode!

            activeRelayManager = netObject.GetComponent<RelayManager>();
            
            // Pass UI references to the spawned object
            activeRelayManager.AssignUIReferences(
                mainMenuPanel, 
                lobbyPanel, 
                joinCodeInputField, 
                errorStatusText, 
                generatedCodeText, 
                playerListText
            );

            // Run the host session logic
            activeRelayManager.StartHostSession();
        }
    }

    public void ClickedJoin()
    {
        // For clients, they can't spawn network objects manually. 
        // We temporarily find the instance once Netcode connects them.
        NetworkManager.Singleton.StartClient();
        StartCoroutine(WaitForClientConnection());
    }

    private System.Collections.IEnumerator WaitForClientConnection()
    {
        // Wait until we are connected to the session
        while (!NetworkManager.Singleton.IsConnectedClient)
        {
            yield return null;
        }

        // Find the RelayManager that the host spawned into the scene
        activeRelayManager = Object.FindFirstObjectByType<RelayManager>();

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

            activeRelayManager.StartClientSession();
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
            // Fallback shutdown if manager isn't found
            NetworkManager.Singleton.Shutdown();
            lobbyPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }
    }
}