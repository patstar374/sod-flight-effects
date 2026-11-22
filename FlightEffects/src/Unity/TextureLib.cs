using UnityEngine;

namespace FlightEffects
{
    using FlightEffects.Core;

    /// <summary>Procedural sprite textures (TextureMath), created once and kept across scene loads.</summary>
    internal static class TextureLib
    {
        private static Texture2D _streak, _dot, _star, _heart;

        public static Texture2D Streak => _streak != null ? _streak : _streak = Make("FlightFx_Streak", TextureMath.Streak(128, 32), 128, 32);

        public static Texture2D Get(SpriteKind kind)
        {
            switch (kind)
            {
                case SpriteKind.Star:
                    return _star != null ? _star : _star = Make("FlightFx_Star", TextureMath.Star(64, 4), 64, 64);
                case SpriteKind.Heart:
                    return _heart != null ? _heart : _heart = Make("FlightFx_Heart", TextureMath.Heart(64), 64, 64);
                default:
                    return _dot != null ? _dot : _dot = Make("FlightFx_Dot", TextureMath.SoftDot(64), 64, 64);
            }
        }

        private static Texture2D Make(string name, float[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var colors = new Color[w * h];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color(px[i * 4], px[i * 4 + 1], px[i * 4 + 2], px[i * 4 + 3]);
            t.SetPixels(colors);
            t.Apply(true, true);
            return t;
        }
    }
}
