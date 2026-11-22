using System;

namespace FlightEffects.Core
{
    public enum EffectType { Contrail, Rainbow, Sparkle, Fire, Stardust, Hearts }

    public enum EmitMode { WhileFlying, WhileAccelerating }

    /// <summary>What the player set in the editor (one active set, plus named presets).</summary>
    public sealed class EffectSettings
    {
        public const float MinLength = 0.2f, MaxLength = 4f, DefaultLength = 1.2f;
        public const float MinWidth = 0.1f, MaxWidth = 3f, DefaultWidth = 1f;
        public const float MinGlow = 0.1f, MaxGlow = 1f, DefaultGlow = 0.9f;
        public const float MaxParticles = 2f, DefaultParticles = 1f;

        public bool Enabled = true;
        public EffectType Type = EffectType.Contrail;
        public Rgba Color1 = new Rgba(1f, 1f, 1f);
        public Rgba Color2 = new Rgba(0.75f, 0.88f, 1f);
        /// <summary>Trail lifetime in seconds (particles live proportionally longer).</summary>
        public float Length = DefaultLength;
        /// <summary>Width factor (1 = style default, scaled with the dragon's wingspan).</summary>
        public float Width = DefaultWidth;
        /// <summary>Opacity / brightness 0.1..1.</summary>
        public float Glow = DefaultGlow;
        /// <summary>Particle amount factor 0..2.</summary>
        public float Particles = DefaultParticles;
        public EmitMode Mode = EmitMode.WhileFlying;
        public bool Wingtips = true;
        public bool Tail;
        public bool PreviewOnGround;

        public static EffectSettings Defaults(EffectType type)
        {
            var s = new EffectSettings { Type = type };
            DefaultColors(type, out s.Color1, out s.Color2);
            return s;
        }

        public static void DefaultColors(EffectType type, out Rgba c1, out Rgba c2)
        {
            switch (type)
            {
                case EffectType.Rainbow:
                    c1 = new Rgba(1f, 0.3f, 0.3f); c2 = new Rgba(0.5f, 0.4f, 1f); break;
                case EffectType.Sparkle:
                    c1 = new Rgba(1f, 0.85f, 0.4f); c2 = new Rgba(1f, 1f, 1f); break;
                case EffectType.Fire:
                    c1 = new Rgba(1f, 0.85f, 0.3f); c2 = new Rgba(1f, 0.25f, 0.05f); break;
                case EffectType.Stardust:
                    c1 = new Rgba(0.6f, 0.8f, 1f); c2 = new Rgba(1f, 0.9f, 0.5f); break;
                case EffectType.Hearts:
                    c1 = new Rgba(1f, 0.35f, 0.6f); c2 = new Rgba(1f, 0.1f, 0.25f); break;
                default:
                    c1 = new Rgba(1f, 1f, 1f); c2 = new Rgba(0.75f, 0.88f, 1f); break;
            }
        }

        public EffectSettings Clone() => (EffectSettings)MemberwiseClone();

        /// <summary>Brings every value into its range (NaN/inf -> default) and keeps at least one emitter location.</summary>
        public void Clamp()
        {
            Length = Fix(Length, MinLength, MaxLength, DefaultLength);
            Width = Fix(Width, MinWidth, MaxWidth, DefaultWidth);
            Glow = Fix(Glow, MinGlow, MaxGlow, DefaultGlow);
            Particles = Fix(Particles, 0f, MaxParticles, DefaultParticles);
            Color1 = FixColor(Color1);
            Color2 = FixColor(Color2);
            if (!Enum.IsDefined(typeof(EffectType), Type)) Type = EffectType.Contrail;
            if (!Enum.IsDefined(typeof(EmitMode), Mode)) Mode = EmitMode.WhileFlying;
            if (!Wingtips && !Tail) Wingtips = true;
        }

        private static float Fix(float v, float lo, float hi, float def) =>
            float.IsNaN(v) || float.IsInfinity(v) ? def : Math.Max(lo, Math.Min(hi, v));

        private static Rgba FixColor(Rgba c) =>
            new Rgba(Fix(c.R, 0f, 1f, 1f), Fix(c.G, 0f, 1f, 1f), Fix(c.B, 0f, 1f, 1f), Fix(c.A, 0f, 1f, 1f));
    }
}
