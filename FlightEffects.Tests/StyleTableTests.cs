using System;
using System.Collections.Generic;
using System.Linq;
using FlightEffects.Core;
using Xunit;

namespace FlightEffects.Tests
{
    public class StyleTableTests
    {
        private static IEnumerable<EffectType> AllTypes() => Enum.GetValues(typeof(EffectType)).Cast<EffectType>();

        [Fact]
        public void EveryType_HasStyleWithAtMostEightColourKeys()
        {
            foreach (EffectType t in AllTypes())
            {
                Style st = StyleTable.For(EffectSettings.Defaults(t), 0f);
                Assert.NotNull(st);
                Assert.InRange(st.TrailColors.Length, 2, 8);
                Assert.Equal(0f, st.TrailColors.First().Time);
                Assert.Equal(1f, st.TrailColors.Last().Time);
                Assert.True(st.TrailWidth > 0f && st.TrailEndWidth > 0f, $"{t} widths");
                Assert.Equal(0f, st.TrailColors.Last().Color.A, 3);
            }
        }

        [Fact]
        public void Rainbow_SpansAtLeastFiveHues()
        {
            Style st = StyleTable.For(EffectSettings.Defaults(EffectType.Rainbow), 0f);
            var hues = new List<float>();
            foreach (GradientKey k in st.TrailColors)
            {
                ColorMath.RgbToHsv(k.Color, out float h, out _, out _);
                if (hues.All(o => Math.Min(Math.Abs(o - h), 1f - Math.Abs(o - h)) > 0.08f))
                    hues.Add(h);
            }
            Assert.True(hues.Count >= 5, $"only {hues.Count} distinct hues");
        }

        [Fact]
        public void Rainbow_ShiftsOverTime()
        {
            EffectSettings s = EffectSettings.Defaults(EffectType.Rainbow);
            Assert.NotEqual(StyleTable.For(s, 0f).TrailColors[0].Color, StyleTable.For(s, 1f).TrailColors[0].Color);
        }

        [Fact]
        public void Glow_ScalesAlpha()
        {
            foreach (EffectType t in AllTypes())
            {
                EffectSettings full = EffectSettings.Defaults(t);
                full.Glow = 1f;
                EffectSettings half = full.Clone();
                half.Glow = 0.5f;
                float a1 = StyleTable.For(full, 0f).TrailColors.Max(k => k.Color.A);
                float a2 = StyleTable.For(half, 0f).TrailColors.Max(k => k.Color.A);
                Assert.True(a2 <= 0.5f * a1 + 1e-6f, $"{t}: {a2} vs {a1}");
            }
        }

        [Fact]
        public void ParticlesZero_GivesZeroRate()
        {
            EffectSettings s = EffectSettings.Defaults(EffectType.Sparkle);
            s.Particles = 0f;
            Assert.Equal(0f, StyleTable.For(s, 0f).ParticleRate);
        }

        [Fact]
        public void Hearts_UseHeartSpriteWithAlphaBlend()
        {
            Style st = StyleTable.For(EffectSettings.Defaults(EffectType.Hearts), 0f);
            Assert.Equal(SpriteKind.Heart, st.Particle);
            Assert.Equal(Blend.Alpha, st.ParticleBlend);
            Assert.True(st.ParticleRate > 0f);
        }

        [Fact]
        public void StyleTable_MaxSettings_StayWithinParticleCap()
        {
            foreach (EffectType t in AllTypes())
            {
                EffectSettings s = EffectSettings.Defaults(t);
                s.Particles = 2f;
                s.Length = 4f;
                Style st = StyleTable.For(s, 0f);
                Assert.True(st.ParticleRate * st.ParticleLifetime * StyleTable.AssumedSpeed <= st.MaxParticles + 1e-3f,
                    $"{t}: rate {st.ParticleRate} lifetime {st.ParticleLifetime}");
            }
        }
    }
}
