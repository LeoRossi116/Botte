using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Botte.UI
{
    /// <summary>
    /// Universal keyboard navigation focus indicator for uGUI panels.
    ///
    /// Responsibilities:
    ///   1. Draws a bright-yellow 9-sliced border around the currently selected UI element.
    ///      The border sprite is generated procedurally at runtime (no imported asset needed).
    ///      Position is computed via RectTransform.GetWorldCorners, so it handles CanvasScaler
    ///      scaling and nested Layout Groups correctly.
    ///   2. Enters "keyboard mode" the first time any navigation/submit key is pressed.
    ///      While in keyboard mode, auto-selects a default element so arrow-key navigation
    ///      is always routed somewhere.  While NOT in keyboard mode the highlight is hidden
    ///      UNLESS the EventSystem already has something selected (e.g. via mouse click).
    ///   3. Submit (Enter) is NOT re-implemented here — the scene's InputSystemUIInputModule
    ///      already routes Enter → ISubmitHandler / Button.onClick on the selected object.
    ///      This script only guarantees a valid selection exists for that routing to work.
    ///   4. On Awake, any Selectable whose navigation.mode is None is upgraded to Automatic
    ///      so arrow keys can reach every interactive element.
    /// </summary>
    [DefaultExecutionOrder(100)] // run after other UI scripts
    public class UIFocusManager : MonoBehaviour
    {
        // ── Internal state ────────────────────────────────────────────────────────────

        private Canvas          _rootCanvas;
        private RectTransform   _canvasRt;
        private UnityEngine.UI.Image _highlightImage;
        private RectTransform   _highlightRt;
        private bool            _keyboardMode;

        // Reused buffer to avoid per-frame allocation
        private readonly Vector3[] _corners = new Vector3[4];

        // ── Lifecycle ─────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Locate the root Canvas (this GameObject is expected to live under it)
            _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas != null)
                _rootCanvas = _rootCanvas.rootCanvas;

            if (_rootCanvas == null)
            {
                // Fallback: search the scene (FindAnyObjectByType avoids instance-ID ordering issues)
                _rootCanvas = FindAnyObjectByType<Canvas>();
                if (_rootCanvas != null) _rootCanvas = _rootCanvas.rootCanvas;
            }

            if (_rootCanvas == null)
            {
                Debug.LogWarning("[UIFocusManager] No Canvas found in scene – focus highlight disabled.");
                return;
            }

            _canvasRt = _rootCanvas.GetComponent<RectTransform>();

            CreateHighlight();
            FixNavigationModes();
        }

        // ── Highlight creation ────────────────────────────────────────────────────────

        private void CreateHighlight()
        {
            var go = new GameObject("UIFocusHighlight");
            go.transform.SetParent(_rootCanvas.transform, false);
            go.transform.SetAsLastSibling();

            _highlightRt            = go.AddComponent<RectTransform>();
            _highlightRt.anchorMin  = Vector2.zero;
            _highlightRt.anchorMax  = Vector2.zero;
            _highlightRt.pivot      = Vector2.zero;

            _highlightImage                 = go.AddComponent<UnityEngine.UI.Image>();
            _highlightImage.sprite          = CreateBorderSprite();
            _highlightImage.type            = UnityEngine.UI.Image.Type.Sliced;
            _highlightImage.color           = new Color(1f, 0.92f, 0.1f, 1f); // bright yellow
            _highlightImage.raycastTarget   = false;

            go.SetActive(false);
        }

        /// <summary>
        /// Procedurally creates a 32×32 Texture2D with a solid yellow 5-px border and a
        /// transparent center, then wraps it in a Sprite with matching 9-slice borders so
        /// Image.Type.Sliced scales the frame without distorting the corners.
        /// Pattern follows <see cref="GlowTextureFactory"/> used by CardUI.
        /// </summary>
        private static Sprite CreateBorderSprite()
        {
            const int size     = 32;
            const int borderPx = 5;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name       = "UIFocusBorderTex"
            };

            var pixels = new Color[size * size];
            var yellow = new Color(1f, 0.92f, 0.1f, 1f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < borderPx || x >= size - borderPx ||
                                y < borderPx || y >= size - borderPx;
                pixels[y * size + x] = isBorder ? yellow : Color.clear;
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // Vector4(left, bottom, right, top) in texels — matches our 5-px solid border.
            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f, 0,
                SpriteMeshType.FullRect,
                new Vector4(borderPx, borderPx, borderPx, borderPx)
            );
        }

        // ── Navigation safety net ─────────────────────────────────────────────────────

        /// <summary>
        /// Upgrades Selectable.navigation.mode from None → Automatic (scene and inactive
        /// objects included) so arrow keys can reach every interactive element.
        /// No other navigation settings are changed.
        /// </summary>
        private static void FixNavigationModes()
        {
            var all = FindObjectsByType<UnityEngine.UI.Selectable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var sel in all)
            {
                if (sel.navigation.mode == UnityEngine.UI.Navigation.Mode.None)
                {
                    var nav = sel.navigation;
                    nav.mode = UnityEngine.UI.Navigation.Mode.Automatic;
                    sel.navigation = nav;
                }
            }
        }

        // ── Per-frame update ──────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_highlightImage == null || _canvasRt == null) return;

            DetectKeyboardMode();

            var es = EventSystem.current;
            GameObject selected = es != null ? es.currentSelectedGameObject : null;

            // Validate the current selection
            bool hasValidSelection = false;
            if (selected != null && selected.activeInHierarchy)
            {
                var sel = selected.GetComponent<UnityEngine.UI.Selectable>();
                if (sel != null && sel.IsInteractable())
                    hasValidSelection = true;
            }

            // Auto-select a sensible default when keyboard mode is active and nothing is
            // focused (or the previously focused element has been deactivated/removed).
            if (_keyboardMode && !hasValidSelection)
            {
                var defaultGo = FindBestDefault();
                if (defaultGo != null && es != null)
                {
                    es.SetSelectedGameObject(defaultGo);
                    selected          = defaultGo;
                    hasValidSelection = true;
                }
            }

            // Show highlight on ANY valid selection — keyboard mode AND mouse-click selections.
            // Hide it only when there is genuinely nothing selected.
            if (hasValidSelection && selected != null)
            {
                var targetRt = selected.GetComponent<RectTransform>()
                            ?? selected.GetComponentInChildren<RectTransform>();
                PositionHighlight(targetRt);
            }
            else
            {
                HideHighlight();
            }
        }

        // ── Keyboard mode detection ───────────────────────────────────────────────────

        private void DetectKeyboardMode()
        {
            if (_keyboardMode) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.upArrowKey.wasPressedThisFrame    || kb.downArrowKey.wasPressedThisFrame  ||
                kb.leftArrowKey.wasPressedThisFrame   || kb.rightArrowKey.wasPressedThisFrame ||
                kb.wKey.wasPressedThisFrame           || kb.aKey.wasPressedThisFrame          ||
                kb.sKey.wasPressedThisFrame           || kb.dKey.wasPressedThisFrame          ||
                kb.enterKey.wasPressedThisFrame       || kb.numpadEnterKey.wasPressedThisFrame)
            {
                _keyboardMode = true;
            }
        }

        // ── Highlight positioning ─────────────────────────────────────────────────────

        private void PositionHighlight(RectTransform targetRt)
        {
            if (targetRt == null) { HideHighlight(); return; }

            // Null camera → Screen Space Overlay; non-null → Camera/World space
            Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _rootCanvas.worldCamera;

            targetRt.GetWorldCorners(_corners);

            var min = new Vector2(float.MaxValue,  float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var corner in _corners)
            {
                Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(cam, corner);
                Vector2 localPt;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRt, screenPt, cam, out localPt);
                min = Vector2.Min(min, localPt);
                max = Vector2.Max(max, localPt);
            }

            const float pad = 4f;
            _highlightRt.anchoredPosition = min - new Vector2(pad, pad);
            _highlightRt.sizeDelta        = (max - min) + new Vector2(pad * 2f, pad * 2f);

            if (!_highlightImage.gameObject.activeSelf)
                _highlightImage.gameObject.SetActive(true);

            // Always drawn on top of all panels
            _highlightRt.SetAsLastSibling();
        }

        private void HideHighlight()
        {
            if (_highlightImage != null && _highlightImage.gameObject.activeSelf)
                _highlightImage.gameObject.SetActive(false);
        }

        // ── Default selection logic ───────────────────────────────────────────────────

        /// <summary>
        /// Finds the first interactable Selectable in the topmost active panel.
        /// "Topmost" = direct child of the root Canvas with the highest sibling index that
        /// contains at least one interactable+active Selectable.
        /// Modals call SetAsLastSibling when opened, so highest sibling index = frontmost.
        /// </summary>
        private GameObject FindBestDefault()
        {
            if (_rootCanvas == null) return null;

            var canvasTr = _rootCanvas.transform;
            int count    = canvasTr.childCount;

            for (int i = count - 1; i >= 0; i--)
            {
                var child = canvasTr.GetChild(i);

                // Skip our own highlight overlay
                if (_highlightRt != null && (Transform)_highlightRt == child) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                var first = FindFirstInteractable(child);
                if (first != null) return first.gameObject;
            }

            return null;
        }

        private static UnityEngine.UI.Selectable FindFirstInteractable(Transform root)
        {
            // GetComponentsInChildren(false) excludes inactive objects — only active hierarchy
            var selectables = root.GetComponentsInChildren<UnityEngine.UI.Selectable>(false);
            foreach (var sel in selectables)
            {
                if (sel.IsInteractable() && sel.gameObject.activeInHierarchy)
                    return sel;
            }
            return null;
        }
    }
}
