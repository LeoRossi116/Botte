using UnityEngine;

namespace Botte.UI
{
    /// <summary>
    /// Generates (and caches) a soft "border glow" sprite used to highlight cards.
    /// The sprite is white with a rounded-rectangle outline whose alpha falls off both
    /// inward and outward, leaving the center transparent. Because the center is clear,
    /// the card art shows through and only a soft halo appears around the card edges.
    /// Tint the halo by setting the Image.color on the object that uses this sprite.
    /// </summary>
    public static class GlowTextureFactory
    {
        private static Sprite _cardGlow;

        // Texture size = card size (100x140) + GlowPad on every side, so the halo can
        // spill outside the card. Keep this in sync with CardUI.GlowPad.
        public const int GlowPad = 14;
        private const int TexW = 100 + GlowPad * 2; // 128
        private const int TexH = 140 + GlowPad * 2; // 168

        public static Sprite GetCardGlow()
        {
            if (_cardGlow != null) return _cardGlow;

            var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CardGlowTex"
            };

            float cx = TexW * 0.5f;
            float cy = TexH * 0.5f;
            // The outline is placed exactly on the card's edge (card is centered in the texture).
            Vector2 halfExtents = new Vector2(50f, 70f); // 100x140 card => half size
            float radius = 12f;
            float softness = 12f; // falloff distance in pixels on each side of the outline

            var pixels = new Color[TexW * TexH];
            for (int y = 0; y < TexH; y++)
            {
                for (int x = 0; x < TexW; x++)
                {
                    float px = x + 0.5f - cx;
                    float py = y + 0.5f - cy;

                    // Signed distance to a rounded rectangle outline.
                    float bx = halfExtents.x - radius;
                    float by = halfExtents.y - radius;
                    float dx = Mathf.Abs(px) - bx;
                    float dy = Mathf.Abs(py) - by;
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                                               Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                    float sdf = outside + inside - radius;

                    // Gaussian falloff centered on the outline (sdf == 0).
                    float t = sdf / softness;
                    float alpha = Mathf.Exp(-t * t);

                    pixels[y * TexW + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cardGlow = Sprite.Create(tex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), 100f);
            _cardGlow.name = "CardGlowSprite";
            return _cardGlow;
        }
    }
}
