using System;
using System.Globalization;

namespace FlightEffects.Core
{
    /// <summary>RGBA colour, components 0..1 (Unity-free stand-in for UnityEngine.Color).</summary>
    public struct Rgba : IEquatable<Rgba>
    {
        public float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r; G = g; B = b; A = a;
        }

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);

        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object obj) => obj is Rgba o && Equals(o);
        public override int GetHashCode() => (R, G, B, A).GetHashCode();
        public override string ToString() => ColorMath.ToHex(this);
    }

    /// <summary>Colour helpers without UnityEngine (tested in xUnit).</summary>
    public static class ColorMath
    {
        /// <summary>HSV (all 0..1, hue wraps) to RGB.</summary>
        public static Rgba HsvToRgb(float h, float s, float v, float a = 1f)
        {
            h -= (float)Math.Floor(h);
            s = Clamp01(s);
            v = Clamp01(v);
            float h6 = h * 6f;
            int i = (int)Math.Floor(h6) % 6;
            float f = h6 - (float)Math.Floor(h6);
            float p = v * (1f - s), q = v * (1f - f * s), t = v * (1f - (1f - f) * s);
            switch (i)
            {
                case 0: return new Rgba(v, t, p, a);
                case 1: return new Rgba(q, v, p, a);
                case 2: return new Rgba(p, v, t, a);
                case 3: return new Rgba(p, q, v, a);
                case 4: return new Rgba(t, p, v, a);
                default: return new Rgba(v, p, q, a);
            }
        }

        public static void RgbToHsv(Rgba c, out float h, out float s, out float v)
        {
            float max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B)), d = max - min;
            v = max;
            s = max <= 0f ? 0f : d / max;
            if (d <= 0f)
            {
                h = 0f;
                return;
            }
            if (max == c.R) h = (c.G - c.B) / d;
            else if (max == c.G) h = (c.B - c.R) / d + 2f;
            else h = (c.R - c.G) / d + 4f;
            h /= 6f;
            if (h < 0f) h += 1f;
        }

        public static string ToHex(Rgba c) =>
            "#" + Byte(c.R).ToString("X2") + Byte(c.G).ToString("X2") + Byte(c.B).ToString("X2") + Byte(c.A).ToString("X2");

        /// <summary>Parses #RRGGBB or #RRGGBBAA (the # is optional).</summary>
        public static bool TryParseHex(string text, out Rgba c)
        {
            c = default;
            if (text == null) return false;
            string s = text.Trim().TrimStart('#');
            if (s.Length != 6 && s.Length != 8) return false;
            var v = new float[4] { 0f, 0f, 0f, 1f };
            for (int i = 0; i < s.Length / 2; i++)
            {
                if (!int.TryParse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
                    return false;
                v[i] = b / 255f;
            }
            c = new Rgba(v[0], v[1], v[2], v[3]);
            return true;
        }

        public static Rgba Lerp(Rgba a, Rgba b, float t) =>
            new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        private static int Byte(float x) => (int)Math.Round(Clamp01(x) * 255f);
    }
}
