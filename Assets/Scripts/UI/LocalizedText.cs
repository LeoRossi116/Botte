using UnityEngine;
using TMPro;

namespace Botte.UI
{
    /// <summary>
    /// Keeps a <see cref="TMP_Text"/> label localized. The text authored in the editor (Italian is
    /// the authoring language) is used as the translation source key. The label then shows the
    /// Italian source when the active language is Italian, or its English translation (from
    /// <see cref="Loc"/>) when English is selected, and updates live whenever the language changes.
    ///
    /// Attach this to any button/label whose text is static (authored in the scene). Do NOT attach
    /// it to labels whose text is assigned at runtime by other code — those must localize themselves
    /// with <c>Loc.T(...)</c> at assignment time instead.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Italian source text used as the translation key. Auto-captured from the label at " +
                 "Awake when left empty.")]
        [TextArea] public string italianSource;

        private TMP_Text _label;
        private bool _captured;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            EnsureSourceCaptured();
        }

        private void OnEnable()
        {
            EnsureSourceCaptured();
            Loc.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= Apply;
        }

        private void EnsureSourceCaptured()
        {
            if (_captured) return;
            if (_label == null) _label = GetComponent<TMP_Text>();
            if (string.IsNullOrEmpty(italianSource) && _label != null)
                italianSource = _label.text;
            _captured = true;
        }

        private void Apply()
        {
            if (_label == null) _label = GetComponent<TMP_Text>();
            if (_label != null && !string.IsNullOrEmpty(italianSource))
                _label.text = Loc.T(italianSource);
        }
    }
}
