using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Botte.Audio; // Usiamo il namespace dell'audio manager

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

        [Header("Audio Settings")]
        [Tooltip("Slider per regolare il volume della musica di sottofondo.")]
        [SerializeField] private Slider volumeSlider;

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

            // Inizializza lo slider del volume
            if (volumeSlider != null)
            {
                // Imposta i limiti dello slider (da 0 a 1)
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;

                // Recupera il valore salvato (oppure usa il default di BattleAudioManager se non ancora istanziato)
                float savedVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
                volumeSlider.SetValueWithoutNotify(savedVolume);

                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
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
            
            // Sincronizza lo slider all'apertura del pannello
            if (volumeSlider != null)
            {
                float currentVol = BattleAudioManager.Instance != null 
                    ? BattleAudioManager.Instance.MusicVolume 
                    : PlayerPrefs.GetFloat("MusicVolume", 0.5f);
                volumeSlider.SetValueWithoutNotify(currentVol);
            }

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

        private void OnVolumeChanged(float value)
        {
            // Aggiorna il volume nel manager se è presente in scena
            if (BattleAudioManager.Instance != null)
            {
                BattleAudioManager.Instance.MusicVolume = value;
            }
            else
            {
                // Se l'audio manager non è ancora attivo, salviamo comunque la preferenza su disco
                PlayerPrefs.SetFloat("MusicVolume", value);
                PlayerPrefs.Save();
            }
        }
    }
}