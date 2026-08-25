using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Botte.UI
{
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardData cardData;
        public HeroState owner;

        private bool isPlayer1;
        private BattleUI battleUI;

        private Image borderImage;
        private Image backgroundImage;                                   // Inner "Background" child image
        private Color backgroundBaseColor;                               // Default (placeholder) background color
        private TMP_Text cardLabel;                                      // Text label child
        private Color normalColor = new Color32(0xf5, 0xa6, 0x23, 0xff); // Amber border (spells)
        private Color itemColor = new Color32(0x3a, 0x9e, 0xd0, 0xff);   // Blue-ish border (items)
        private Color equipColor = new Color32(0x9b, 0x59, 0xb6, 0xff);  // Purple border (equipment)
        private Color activeColor = new Color32(0x2e, 0xcc, 0x71, 0xff); // Green border when aura active / used
        private bool isActiveOrUsed;
        private bool isItem;
        private bool isEquip;

        // Bring-to-front on hover uses an override-sorting sub-canvas so the card renders
        // above its siblings without changing its order inside the hand layout group.
        private const int HoverSortingOrder = 100;

        // --- Glow highlight (hover + just-drawn) ---
        private Image glowImage;                                         // Soft halo behind card art
        private bool isHovering;
        private float drawGlowEndTime = -1f;                            // unscaled time until the "just drawn" glow ends
        private static readonly Color DrawGlowColor = new Color(1f, 0.9f, 0.45f, 1f); // warm gold "new card" glow
        private const float GlowPulseMin = 0.55f;
        private const float GlowPulseMax = 1f;

        private void Awake()
        {
            borderImage = GetComponent<Image>();

            Transform bg = transform.Find("Background");
            if (bg != null)
            {
                backgroundImage = bg.GetComponent<Image>();
                if (backgroundImage != null) backgroundBaseColor = backgroundImage.color;
            }

            cardLabel = GetComponentInChildren<TMP_Text>();

            EnsureGlow();
        }

        // Lazily builds the soft halo image used for hover / just-drawn highlights.
        // It is the first child so the card art (Background/Label) renders on top of it,
        // and it is padded larger than the card so the halo spills around the edges.
        private void EnsureGlow()
        {
            if (glowImage != null) return;

            var go = new GameObject("Glow", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float pad = GlowTextureFactory.GlowPad;
            rt.offsetMin = new Vector2(-pad, -pad);
            rt.offsetMax = new Vector2(pad, pad);
            rt.SetAsFirstSibling();

            glowImage = go.AddComponent<Image>();
            glowImage.sprite = GlowTextureFactory.GetCardGlow();
            glowImage.type = Image.Type.Simple;
            glowImage.raycastTarget = false;
            glowImage.enabled = false;
        }

        private void Update()
        {
            if (glowImage == null) return;

            float now = Time.unscaledTime;
            bool drawActive = now < drawGlowEndTime;

            if (isHovering)
            {
                // Glow in the card's own (current) color so the hovered card clearly stands out.
                Color c = Brighten(BaseColor());
                c.a = PulseAlpha(now, 6f);
                glowImage.color = c;
                glowImage.enabled = true;
            }
            else if (drawActive)
            {
                Color c = DrawGlowColor;
                c.a = PulseAlpha(now, 5f);
                glowImage.color = c;
                glowImage.enabled = true;
            }
            else
            {
                glowImage.enabled = false;
            }
        }

        private static float PulseAlpha(float t, float speed)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(t * speed);
            return Mathf.Lerp(GlowPulseMin, GlowPulseMax, k);
        }

        private static Color Brighten(Color c)
        {
            return new Color(
                Mathf.Clamp01(c.r * 1.15f + 0.15f),
                Mathf.Clamp01(c.g * 1.15f + 0.15f),
                Mathf.Clamp01(c.b * 1.15f + 0.15f),
                1f);
        }

        /// <summary>
        /// Starts (or refreshes) the "just drawn" glow for the given remaining duration.
        /// BattleUI re-applies this after each hand refresh so the glow survives rebuilds.
        /// </summary>
        public void PlayDrawGlow(float remainingSeconds)
        {
            if (remainingSeconds <= 0f) return;
            EnsureGlow();
            drawGlowEndTime = Time.unscaledTime + remainingSeconds;
        }

        public void Setup(CardData data, HeroState hero, bool player1, BattleUI ui, string stateLabel)
        {
            cardData = data;
            owner = hero;
            isPlayer1 = player1;
            battleUI = ui;
            isActiveOrUsed = !string.IsNullOrEmpty(stateLabel);
            isItem = data is ItemData;
            isEquip = data is EquipmentData;

            TMP_Text label = cardLabel;
            if (label != null && data != null)
            {
                if (data is EquipmentData eq)
                {
                    string stat = eq.damageValue > 0 ? $"{Loc.T("Danno")} {eq.damageValue}" : (eq.defenseValue > 0 ? $"{Loc.T("Difesa")} {eq.defenseValue}" : eq.equipType.ToString());
                    label.text = $"{Loc.CardName(data.cardName)}\n({stat})";
                }
                else
                {
                    string suffix = isActiveOrUsed ? $"\n<{stateLabel}>" : "";
                    label.text = $"{Loc.CardName(data.cardName)}\n(M:{data.manaCost} S:{data.staminaCost}){suffix}";
                }
            }

            // When the card has custom artwork, draw it in place of the placeholder background
            // and label; otherwise fall back to the default placeholder look.
            bool hasTexture = data != null && data.cardTexture != null;
            if (backgroundImage != null)
            {
                if (hasTexture)
                {
                    backgroundImage.sprite = data.cardTexture;
                    backgroundImage.color = Color.white;
                    // Stretch the artwork to fill the whole card (inside the border frame) instead
                    // of letterboxing it, so the sprite acts as the card background.
                    backgroundImage.preserveAspect = false;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color = backgroundBaseColor;
                }
            }
            if (label != null) label.gameObject.SetActive(!hasTexture);

            if (borderImage != null)
                borderImage.color = BaseColor();
        }

        private Color BaseColor()
        {
            if (isActiveOrUsed) return activeColor;
            if (isEquip) return equipColor;
            if (isItem) return itemColor;
            return normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            SetOnTop(true);
            if (battleUI != null && cardData != null) battleUI.ShowCardDescription(isPlayer1, cardData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            SetOnTop(false);
            if (battleUI != null) battleUI.HideCardDescription(isPlayer1);
        }

        // Renders this card above its siblings while hovered, without reordering it inside the
        // hand's layout group. A sub-canvas with overrideSorting draws it on top; a
        // GraphicRaycaster keeps the card (and its children) receiving pointer events.
        private void SetOnTop(bool onTop)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (onTop)
            {
                if (canvas == null)
                {
                    canvas = gameObject.AddComponent<Canvas>();
                    gameObject.AddComponent<GraphicRaycaster>();
                }
                canvas.overrideSorting = true;
                canvas.sortingOrder = HoverSortingOrder;
            }
            else if (canvas != null)
            {
                canvas.overrideSorting = false;
                canvas.sortingOrder = 0;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var bm = Object.FindFirstObjectByType<Botte.Core.BattleManager>();
            if (bm == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right click discards the card (during the owner's turn).
                bm.OnCardRightClicked(owner, cardData);
                return;
            }

            if (cardData is MagicData spell) bm.OnCardClicked(owner, spell);
            else if (cardData is ItemData item) bm.OnItemClicked(owner, item);
            else if (cardData is EquipmentData equip) bm.OnEquipmentClicked(owner, equip);
        }
    }
}
