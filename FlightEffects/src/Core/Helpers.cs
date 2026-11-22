using System;
using System.Collections.Generic;

namespace FlightEffects.Core
{
    /// <summary>German display names of the effect types (editor buttons and preset names).</summary>
    public static class EffectNames
    {
        public static string Label(EffectType type)
        {
            switch (type)
            {
                case EffectType.Rainbow: return "Regenbogen";
                case EffectType.Sparkle: return "Glitzer";
                case EffectType.Fire: return "Feuer";
                case EffectType.Stardust: return "Sternenstaub";
                case EffectType.Hearts: return "Herzen";
                default: return "Kondensstreifen";
            }
        }
    }

    /// <summary>Per-dragon rig build attempts: only failures count, with a pause between tries.</summary>
    public sealed class BuildAttempts
    {
        private readonly int _maxFailures;
        private readonly float _retrySeconds;
        private readonly Dictionary<int, KeyValuePair<int, float>> _state = new Dictionary<int, KeyValuePair<int, float>>();

        public BuildAttempts(int maxFailures = 5, float retrySeconds = 2f)
        {
            _maxFailures = maxFailures;
            _retrySeconds = retrySeconds;
        }

        public bool CanTry(int id, float now) =>
            !_state.TryGetValue(id, out KeyValuePair<int, float> s) || (s.Key < _maxFailures && now >= s.Value);

        /// <summary>Records a failure; true when this one made the dragon give up.</summary>
        public bool Failed(int id, float now)
        {
            _state.TryGetValue(id, out KeyValuePair<int, float> s);
            int failures = s.Key + 1;
            _state[id] = new KeyValuePair<int, float>(failures, now + _retrySeconds);
            return failures == _maxFailures;
        }

        public void Succeeded(int id) => _state.Remove(id);
    }

    /// <summary>Detects teleports, so a trail never draws a line across the scene.</summary>
    public static class Motion
    {
        public const float JumpSpans = 5f, MinJump = 20f;

        public static float JumpDistance(float span) => Math.Max(JumpSpans * span, MinJump);

        public static bool IsJump(Vec3 previous, Vec3 current, float span) =>
            (current - previous).Length > JumpDistance(span);
    }

    /// <summary>Editor window scale (relative to 1080p) and keeping it on screen; units are GUI units.</summary>
    public static class WindowFit
    {
        public const float ReferenceHeight = 1080f, MinScale = 0.6f, MaxScale = 3f, Margin = 10f;

        public static float Scale(float screenHeight) =>
            Math.Max(MinScale, Math.Min(MaxScale, screenHeight / ReferenceHeight));

        public static void Clamp(ref float x, ref float y, ref float w, ref float h, float screenW, float screenH, float scale)
        {
            float gw = screenW / scale, gh = screenH / scale;
            w = Math.Min(w, gw - 2f * Margin);
            h = Math.Min(h, gh - 2f * Margin);
            x = Math.Max(Margin, Math.Min(x, gw - w - Margin));
            y = Math.Max(Margin, Math.Min(y, gh - h - Margin));
        }
    }
}
