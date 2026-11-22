using System;

namespace FlightEffects.Core
{
    public struct EmitInputs
    {
        public bool Enabled;
        public bool HasPet;
        public bool Flying;
        public bool Accelerating;
        public bool Preview;
        public EmitMode Mode;
    }

    /// <summary>Decides when the effect shows and fades it in and out softly (like the game's own dragon trails).</summary>
    public sealed class EmitGate
    {
        public const float FadeSeconds = 0.5f;

        public float Intensity { get; private set; }

        public bool Emitting => Intensity > 0.01f;

        public static bool Wanted(EmitInputs i) =>
            i.Enabled && i.HasPet && (i.Preview || (i.Flying && (i.Mode == EmitMode.WhileFlying || i.Accelerating)));

        public void Step(EmitInputs i, float dt)
        {
            if (!i.HasPet)
            {
                Intensity = 0f;
                return;
            }
            float target = Wanted(i) ? 1f : 0f;
            float step = Math.Max(0f, dt) / FadeSeconds;
            Intensity = target > Intensity ? Math.Min(target, Intensity + step) : Math.Max(target, Intensity - step);
        }

        public void Reset() => Intensity = 0f;
    }
}
