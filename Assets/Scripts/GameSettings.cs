using System;
using UnityEngine;

/// <summary>
/// Global, persisted game settings shared across the whole app.
/// Currently holds the "Tutorial" option that drives the in-battle button hints.
/// </summary>
public static class GameSettings
{
    private const string TutorialKey = "Botte.TutorialEnabled";
    private const string LanguageKey = "Botte.Language";

    private static bool _loaded;
    private static bool _tutorialEnabled;

    private static bool _langLoaded;
    private static Language _language;

    /// <summary>Raised whenever <see cref="TutorialEnabled"/> changes.</summary>
    public static event Action<bool> TutorialEnabledChanged;

    /// <summary>Raised whenever <see cref="CurrentLanguage"/> changes.</summary>
    public static event Action<Language> LanguageChanged;

    /// <summary>
    /// The UI/content language. Italian is the authoring/default language.
    /// Persisted via PlayerPrefs; changing it raises <see cref="LanguageChanged"/>.
    /// </summary>
    public static Language CurrentLanguage
    {
        get
        {
            if (!_langLoaded)
            {
                _language = (Language)PlayerPrefs.GetInt(LanguageKey, (int)Language.Italian);
                _langLoaded = true;
            }
            return _language;
        }
        set
        {
            if (_langLoaded && _language == value) return;
            _language = value;
            _langLoaded = true;
            PlayerPrefs.SetInt(LanguageKey, (int)value);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// When true, hovering the middle-panel action buttons during a match shows a
    /// small explanatory tooltip above the button. Defaults to ON. Persisted via PlayerPrefs.
    /// </summary>
    public static bool TutorialEnabled
    {
        get
        {
            if (!_loaded)
            {
                _tutorialEnabled = PlayerPrefs.GetInt(TutorialKey, 1) == 1;
                _loaded = true;
            }
            return _tutorialEnabled;
        }
        set
        {
            if (_loaded && _tutorialEnabled == value) return;
            _tutorialEnabled = value;
            _loaded = true;
            PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0);
            PlayerPrefs.Save();
            TutorialEnabledChanged?.Invoke(value);
        }
    }
}
