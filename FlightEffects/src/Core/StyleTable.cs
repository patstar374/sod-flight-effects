using System;

namespace FlightEffects.Core
{
    public enum Blend { Additive, Alpha }

    public enum SpriteKind { None, SoftDot, Star, Heart }

    public struct GradientKey
    {
        public float Time;
        public Rgba Color;

        public GradientKey(float time, Rgba color)
        {
            Time = time;
            Color = color;
        }
    }

    /// <summary>How one effect type looks. Widths and sizes are in wingspan units (world = value x span x Width).</summary>
    public sealed class Style
    {
        public Blend TrailBlend;
        public float TrailWidth;
        /// <summary>End width as a factor of the start width.</summary>
        public float TrailEndWidth;
        /// <summary>Colour and alpha from the emitter (0) to the end of the trail (1); at most 8 keys.</summary>
        public GradientKey[] TrailColors;
        public SpriteKind Particle;
        public Blend ParticleBlend;
        /// <summary>Particles per metre flown.</summary>
        public float ParticleRate;
        public float ParticleSize;
        public float ParticleLifetime;
        /// <summary>Gravity modifier; negative makes particles rise.</summary>
        public float ParticleGravity;
        public bool Twinkle;
        /// <summary>Largest random start rotation (radians); hearts stay upright.</summary>
        public float ParticleSpin = (float)Math.PI;
        public Rgba[] ParticleColors = new Rgba[0];
        public int MaxParticles = 300;
    }

    /// <summary>Effect type + settings -> concrete look. Values are starting points tuned in the test flight.</summary>
    public static class StyleTable
    {
        /// <summary>Flight speed (m/s) used to keep the particle count under MaxParticles.</summary>
        public const float AssumedSpeed = 40f;

        public static Style For(EffectSettings s, float time)
        {
            float g = s.Glow;
            Rgba c1 = s.Color1, c2 = s.Color2;
            var st = new Style();
            switch (s.Type)
            {
                case EffectType.Rainbow:
                    Trail(st, Blend.Additive, 0.03f, 0.3f);
                    st.TrailColors = new GradientKey[7];
                    for (int i = 0; i < 7; i++)
                    {
                        float t = i / 6f;
                        st.TrailColors[i] = new GradientKey(t, ColorMath.HsvToRgb(i / 7f + 0.25f * time, 0.9f, 1f, g * (1f - t)));
                    }
                    break;
                case EffectType.Sparkle:
                    Trail(st, Blend.Additive, 0.008f, 0.5f);
                    st.TrailColors = Keys(c1.WithAlpha(0.8f * g), c2.WithAlpha(0f));
                    Particles(st, SpriteKind.Star, Blend.Additive, 6f, 0.012f, 0.8f, 0f, true, c1, c2, new Rgba(1f, 1f, 1f));
                    break;
                case EffectType.Fire:
                    Trail(st, Blend.Additive, 0.03f, 0.2f);
                    st.TrailColors = new[]
                    {
                        new GradientKey(0f, c1.WithAlpha(g)), new GradientKey(0.4f, c2.WithAlpha(0.6f * g)),
                        new GradientKey(1f, new Rgba(0.3f, 0.02f, 0f, 0f)),
                    };
                    Particles(st, SpriteKind.SoftDot, Blend.Additive, 8f, 0.008f, 0.6f, -0.5f, false, c1, c2);
                    break;
                case EffectType.Stardust:
                    Trail(st, Blend.Additive, 0.012f, 0.5f);
                    st.TrailColors = Keys(c1.WithAlpha(0.4f * g), c2.WithAlpha(0f));
                    Particles(st, SpriteKind.Star, Blend.Additive, 4f, 0.02f, 1.2f, 0f, true, c1, c2);
                    break;
                case EffectType.Hearts:
                    Trail(st, Blend.Alpha, 0.008f, 0.5f);
                    st.TrailColors = Keys(c1.WithAlpha(0.5f * g), c2.WithAlpha(0f));
                    Particles(st, SpriteKind.Heart, Blend.Alpha, 3f, 0.025f, 1f, -0.2f, false, c1, c2);
                    st.ParticleSpin = 0.35f;
                    break;
                default:
                    Trail(st, Blend.Alpha, 0.02f, 1.6f);
                    st.TrailColors = new[]
                    {
                        new GradientKey(0f, c1.WithAlpha(0.85f * g)),
                        new GradientKey(0.5f, ColorMath.Lerp(c1, c2, 0.5f).WithAlpha(0.5f * g)),
                        new GradientKey(1f, c2.WithAlpha(0f)),
                    };
                    break;
            }
            if (st.Particle != SpriteKind.None)
            {
                st.ParticleLifetime *= s.Length / EffectSettings.DefaultLength;
                st.ParticleRate *= s.Particles;
                float cap = st.MaxParticles / (st.ParticleLifetime * AssumedSpeed);
                st.ParticleRate = Math.Min(st.ParticleRate, cap);
                for (int i = 0; i < st.ParticleColors.Length; i++)
                    st.ParticleColors[i] = st.ParticleColors[i].WithAlpha(g);
            }
            return st;
        }

        private static void Trail(Style st, Blend blend, float width, float endWidth)
        {
            st.TrailBlend = blend;
            st.TrailWidth = width;
            st.TrailEndWidth = endWidth;
        }

        private static GradientKey[] Keys(Rgba head, Rgba tail) =>
            new[] { new GradientKey(0f, head), new GradientKey(1f, tail) };

        private static void Particles(Style st, SpriteKind sprite, Blend blend, float rate, float size, float lifetime,
            float gravity, bool twinkle, params Rgba[] colors)
        {
            st.Particle = sprite;
            st.ParticleBlend = blend;
            st.ParticleRate = rate;
            st.ParticleSize = size;
            st.ParticleLifetime = lifetime;
            st.ParticleGravity = gravity;
            st.Twinkle = twinkle;
            st.ParticleColors = colors;
        }
    }
}
