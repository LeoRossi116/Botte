using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Botte.UI
{
    /// <summary>
    /// Controls the shared "Opzioni" (settings) modal. The root GameObject stays active so
    /// <see cref="Instance"/> is available even before the panel is first opened; only the
    /// <see cref="content"/> child (backdrop + window) is toggled on/off.
    /// Opened from the main-menu OPTIONS button and from the in-game options window.
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        public static SettingsPanelController Instance { get; private set; }

        [Tooltip("The backdrop + window shown when the panel is open. Toggled by Open/Close.")]
        [SerializeField] private GameObject content;
        [Tooltip("Checkbox that drives GameSettings.TutorialEnabled.")]
        [SerializeField] private Toggle tutorialToggle;
        [Tooltip("The X button in the top-right corner that closes the panel.")]
        [SerializeField] private Button closeButton;
        [Tooltip("Dropdown to switch the UI/content language (Italiano / English).")]
        [SerializeField] private TMPro.TMP_Dropdown languageDropdown;

        private void Awake()
        {
            Instance = this;

            if (content != null) content.SetActive(false);

            if (tutorialToggle != null)
            {
                tutorialToggle.SetIsOnWithoutNotify(GameSettings.TutorialEnabled);
                tutorialToggle.onValueChanged.AddListener(OnTutorialToggleChanged);
            }

            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (languageDropdown != null)
            {
                languageDropdown.ClearOptions();
                languageDropdown.AddOptions(new List<string> { "Italiano", "English" });
                languageDropdown.SetValueWithoutNotify((int)GameSettings.CurrentLanguage);
                languageDropdown.RefreshShownValue();
                languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Shows the settings modal on top of everything else.</summary>
        public void Open()
        {
            if (tutorialToggle != null) tutorialToggle.SetIsOnWithoutNotify(GameSettings.TutorialEnabled);
            if (content != null)
            {
                content.SetActive(true);
                transform.SetAsLastSibling();
            }
        }

        /// <summary>Hides the settings modal.</summary>
        public void Close()
        {
            if (content != null) content.SetActive(false);
        }

        private void OnTutorialToggleChanged(bool value)
        {
            GameSettings.TutorialEnabled = value;
        }

        private void OnLanguageChanged(int index)
        {
            GameSettings.CurrentLanguage = (Language)index;
        }
    }
}
