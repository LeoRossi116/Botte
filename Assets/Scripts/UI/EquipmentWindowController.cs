using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    /// <summary>
    /// Controls the equipment modal window, built the same way as the "Opzioni" (settings) modal:
    /// the root GameObject stays active so <see cref="Instance"/> is available, and only the
    /// <see cref="content"/> child (backdrop + window) is toggled. The window shows the title
    /// "&lt;player&gt; equipment", an X close button, and 6 card-style squares (one per equipment
    /// slot) each showing the card artwork with the attack/defense stats overlaid on top.
    /// Opened per-player from that player's "show equipment" button.
    /// </summary>
    public class EquipmentWindowController : MonoBehaviour
    {
        public static EquipmentWindowController Instance { get; private set; }

        [Tooltip("Backdrop + window shown when open. Toggled by Open/Close.")]
        [SerializeField] private GameObject content;
        [Tooltip("Title label, e.g. \"<name>'s equipment\".")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("The X button that closes the window.")]
        [SerializeField] private Button closeButton;
        [Tooltip("The 6 equipment squares, in order: Head, Torso, Hands, Feet, WeaponMain, WeaponOff.")]
        [SerializeField] private EquipmentSquareUI[] squares;

        // Fixed slot order shown in the window (matches BattleUI.EquipOrder).
        private static readonly EquipmentSlot[] SlotOrder =
        {
            EquipmentSlot.Head, EquipmentSlot.Torso, EquipmentSlot.Hands,
            EquipmentSlot.Feet, EquipmentSlot.WeaponMain, EquipmentSlot.WeaponOff
        };

        // Remember what's open so the window can refresh itself on a language change.
        private HeroState _current;
        private string _currentName;
        private bool _currentIsPlayer1;

        private void Awake()
        {
            Instance = this;
            if (content != null) content.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (Instance == this) Instance = null;
        }

        public bool IsOpen => content != null && content.activeSelf;

        /// <summary>Opens the modal showing the given hero's equipment.</summary>
        public void Open(HeroState hero, string displayName, bool isPlayer1)
        {
            _current = hero;
            _currentName = displayName;
            _currentIsPlayer1 = isPlayer1;

            Populate();

            if (content != null)
            {
                content.SetActive(true);
                transform.SetAsLastSibling();
            }
        }

        public void Close()
        {
            if (content != null) content.SetActive(false);
        }

        private void Populate()
        {
            if (titleText != null)
                titleText.text = string.Format(Loc.T("Equipaggiamento di {0}"), _currentName ?? "");

            if (squares == null || _current == null) return;

            bool twoHanded = _current.weaponTwoHandedEquipped;

            for (int i = 0; i < squares.Length && i < SlotOrder.Length; i++)
            {
                var sq = squares[i];
                if (sq == null) continue;
                EquipmentSlot es = SlotOrder[i];

                // A two-handed weapon occupies the main-hand slot and leaves the off-hand empty.
                EquipmentData eq = (es == EquipmentSlot.WeaponOff && twoHanded)
                    ? null
                    : _current.equippedItems[(int)es];

                sq.SetItem(eq, es, _currentIsPlayer1);
            }
        }

        private void OnLanguageChanged()
        {
            if (IsOpen) Populate();
        }
    }
}
