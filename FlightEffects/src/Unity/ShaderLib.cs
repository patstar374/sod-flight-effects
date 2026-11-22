using UnityEngine;

namespace FlightEffects
{
    using FlightEffects.Core;

    /// <summary>
    /// Particle shaders that ship in the game's resources.assets (always reachable via Shader.Find). Standard/PBR shaders
    /// render white in SoD scenes, so only legacy particle and unlit shaders are used.
    /// </summary>
    internal static class ShaderLib
    {
        private static readonly string[] AdditiveNames =
            { "Legacy Shaders/Particles/Additive (Soft)", "Unlit/Transparent Colored", "UI/Default" };
        private static readonly string[] AlphaNames =
            { "Legacy Shaders/Particles/Alpha Blended", "Unlit/Transparent Colored", "UI/Default" };
        private static Shader _additive, _alpha;

        public static Shader Additive => _additive != null ? _additive : _additive = Find(AdditiveNames);

        public static Shader Alpha => _alpha != null ? _alpha : _alpha = Find(AlphaNames);

        public static string Describe() =>
            $"additive={(Additive != null ? Additive.name : "none")}, alpha={(Alpha != null ? Alpha.name : "none")}";

        public static Material NewMaterial(Blend blend, Texture texture)
        {
            Shader shader = blend == Blend.Additive ? Additive : Alpha;
            if (shader == null)
                return null;
            var m = new Material(shader) { name = "FlightFx_" + blend, mainTexture = texture };
            if (m.HasProperty("_TintColor"))
                m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // legacy alpha blended doubles: neutral
            if (m.HasProperty("_InvFade"))
                m.SetFloat("_InvFade", 1f);
            return m;
        }

        private static Shader Find(string[] names)
        {
            foreach (string n in names)
            {
                Shader s = Shader.Find(n);
                if (s != null && s.isSupported)
                    return s;
            }
            return null;
        }
    }
}
