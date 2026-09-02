using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    /// <summary>
    /// UI component attached to the LobbyRow prefab representing an open lobby in the browse list.
    /// Easily editable and customizable directly in the Unity Editor.
    /// </summary>
    public class LobbyRowItem : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Text label displaying the lobby name and host nickname.")]
        [SerializeField] private TextMeshProUGUI infoText;

        [Tooltip("Button used to join this lobby.")]
        [SerializeField] private Button joinButton;

        [Tooltip("Text inside the join button.")]
        [SerializeField] private TextMeshProUGUI joinButtonText;

        public void Init(string sessionId, string lobbyName, string hostName, Action<string> onJoinClicked)
        {
            string displayName = string.IsNullOrEmpty(lobbyName) ? "(Unnamed Lobby)" : lobbyName;
            string displayHost = string.IsNullOrEmpty(hostName) ? "Unknown" : hostName;

            if (infoText != null)
            {
                infoText.text = $"<b>{displayName}</b>    <color=#8899cc>Host: {displayHost}</color>";
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(() => onJoinClicked?.Invoke(sessionId));
            }
        }
    }
}
