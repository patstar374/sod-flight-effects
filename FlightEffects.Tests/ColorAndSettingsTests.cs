using System;
using FlightEffects.Core;
using Xunit;

namespace FlightEffects.Tests
{
    public class ColorMathTests
    {
        internal static void Near(float expected, float actual, float tol = 1e-4f) =>
            Assert.InRange(actual, expected - tol, expected + tol);

        [Fact]
        public void HsvToRgb_PrimaryHues()
        {
            Rgba r = ColorMath.HsvToRgb(0f, 1f, 1f);
            Near(1f, r.R); Near(0f, r.G); Near(0f, r.B); Near(1f, r.A);
            Rgba g = ColorMath.HsvToRgb(1f / 3f, 1f, 1f);
            Near(0f, g.R); Near(1f, g.G); Near(0f, g.B);
            Rgba b = ColorMath.HsvToRgb(2f / 3f, 1f, 1f);
            Near(0f, b.R); Near(0f, b.G); Near(1f, b.B);
        }

        [Theory]
        [InlineData(1f, 0.4f, 0.8f)]
        [InlineData(0.2f, 0.6f, 0.1f)]
        [InlineData(0.5f, 0.5f, 0.5f)]
        [InlineData(0f, 0f, 0f)]
        [InlineData(0.9f, 0.85f, 0.3f)]
        public void RgbToHsv_RoundTrips(float r, float g, float b)
        {
            ColorMath.RgbToHsv(new Rgba(r, g, b), out float h, out float s, out float v);
            Rgba back = ColorMath.HsvToRgb(h, s, v);
            Near(r, back.R); Near(g, back.G); Near(b, back.B);
        }

        [Fact]
        public void Hex_RoundTripsAndRejectsGarbage()
        {
            Assert.Equal("#FF66CCFF", ColorMath.ToHex(new Rgba(1f, 0.4f, 0.8f, 1f)));
            Assert.True(ColorMath.TryParseHex("#FF66CC", out Rgba c));
            Near(1f, c.A); Near(0.4f, c.G, 1f / 255f);
            Assert.True(ColorMath.TryParseHex("#FF66CC80", out Rgba d));
            Near(128f / 255f, d.A, 1e-3f);
            Assert.False(ColorMath.TryParseHex("zzz", out _));
            Assert.False(ColorMath.TryParseHex(null, out _));
        }

        [Fact]
        public void Lerp_Midpoint()
        {
            Rgba m = ColorMath.Lerp(new Rgba(0f, 0f, 0f, 0f), new Rgba(1f, 1f, 1f, 1f), 0.5f);
            Near(0.5f, m.R); Near(0.5f, m.A);
        }
    }

    public class EffectSettingsTests
    {
        [Fact]
        public void Defaults_FireIsWarm()
        {
            EffectSettings s = EffectSettings.Defaults(EffectType.Fire);
            Assert.Equal(EffectType.Fire, s.Type);
            Assert.True(s.Color2.R > 0.8f && s.Color2.G < 0.4f, $"fire colour 2 {s.Color2}");
        }

        [Fact]
        public void Defaults_EveryTypeKeepsTheCommonDefaults()
        {
            foreach (EffectType t in Enum.GetValues(typeof(EffectType)))
            {
                EffectSettings s = EffectSettings.Defaults(t);
                Assert.Equal(1.2f, s.Length);
                Assert.Equal(1f, s.Width);
                Assert.Equal(0.9f, s.Glow);
                Assert.Equal(1f, s.Particles);
                Assert.Equal(EmitMode.WhileFlying, s.Mode);
                Assert.True(s.Enabled && s.Wingtips && !s.Tail && !s.PreviewOnGround);
            }
        }

        [Fact]
        public void Clamp_LimitsRanges()
        {
            var s = new EffectSettings { Length = 10f, Width = -1f, Glow = 5f, Particles = -3f };
            s.Clamp();
            Assert.Equal(4f, s.Length);
            Assert.Equal(0.1f, s.Width);
            Assert.Equal(1f, s.Glow);
            Assert.Equal(0f, s.Particles);
            s.Length = float.NaN;
            s.Clamp();
            Assert.Equal(1.2f, s.Length);
        }

        [Fact]
        public void Clamp_KeepsOneEmitterLocation()
        {
            var s = new EffectSettings { Wingtips = false, Tail = false };
            s.Clamp();
            Assert.True(s.Wingtips);
        }

        [Fact]
        public void Clone_IsIndependent()
        {
            EffectSettings s = EffectSettings.Defaults(EffectType.Hearts);
            EffectSettings c = s.Clone();
            c.Length = 3f;
            c.Color1 = new Rgba(0f, 0f, 0f);
            Assert.Equal(1.2f, s.Length);
            Assert.NotEqual(c.Color1, s.Color1);
        }
    }
}
