using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button joinButton;

    public void Setup(string displayName, string displayHost, System.Action onJoin)
    {
        if (label != null)
            label.text = $"<b>{displayName}</b>    <color=#8899cc>Host: {displayHost}</color>";
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => onJoin?.Invoke());
        }
    }
}
