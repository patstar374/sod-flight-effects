using System;

namespace FlightEffects.Core
{
    /// <summary>
    /// Procedural sprite pixels: white RGBA, the shape lives in alpha. Layout as Unity's Texture2D.SetPixels: rows from
    /// the bottom, x first, index (y * width + x) * 4.
    /// </summary>
    public static class TextureMath
    {
        public static float[] SoftDot(int size)
        {
            var px = new float[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Radius(x, y, size);
                    float a = r >= 1f ? 0f : (1f - r * r) * (1f - r * r);
                    Set(px, size, x, y, a);
                }
            return px;
        }

        /// <summary>Sparkle star: small glowing core plus sharp rays (points = number of rays).</summary>
        public static float[] Star(int size, int points)
        {
            var px = new float[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = (float)Math.Sqrt(u * u + v * v);
                    double theta = Math.Atan2(v, u);
                    double core = Math.Exp(-(r / 0.12) * (r / 0.12));
                    double ray = Math.Exp(-(r / 0.85) * (r / 0.85)) * Math.Pow(Math.Abs(Math.Cos(points * theta / 2)), 60);
                    float a = r >= 1f ? 0f : (float)Math.Min(1.0, core + ray);
                    Set(px, size, x, y, a);
                }
            return px;
        }

        /// <summary>Heart (lobes up), 4 x 4 supersampled from (x^2 + y^2 - 1)^3 - x^2 y^3 &lt;= 0.</summary>
        public static float[] Heart(int size)
        {
            var px = new float[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            double hx = ((x + (sx + 0.5) / 4) / size * 2 - 1) * 1.25;
                            double hy = ((y + (sy + 0.5) / 4) / size * 2 - 1) * 1.25 + 0.115;
                            double q = hx * hx + hy * hy - 1;
                            if (q * q * q - hx * hx * hy * hy * hy <= 0)
                                inside++;
                        }
                    Set(px, size, x, y, inside / 16f);
                }
            return px;
        }

        /// <summary>Trail strip: u runs along the trail, v across it (soft edges), with faint lengthwise streaks.</summary>
        public static float[] Streak(int width, int height)
        {
            var px = new float[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
                    double across = Math.Exp(-((v - 0.5) / 0.2) * ((v - 0.5) / 0.2));
                    double along = 0.85 + 0.15 * (0.5 + 0.5 * Math.Sin(u * 2 * Math.PI * 3));
                    Set(px, width, x, y, (float)(across * along));
                }
            return px;
        }

        private static float Radius(int x, int y, int size)
        {
            float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
            return (float)Math.Sqrt(u * u + v * v);
        }

        private static void Set(float[] px, int width, int x, int y, float alpha)
        {
            int i = (y * width + x) * 4;
            px[i] = px[i + 1] = px[i + 2] = 1f;
            px[i + 3] = alpha;
        }
    }
}
