using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlightEffects
{
    using FlightEffects.Core;

    /// <summary>
    /// Follows the player's current dragon (SanctuaryManager.pCurPetInstance): builds the emitter rig for it, rebuilds on
    /// dragon or scene changes and drives the effect while the player rides it in flight (or always with the preview).
    /// </summary>
    internal sealed class FlightFxController : MonoBehaviour
    {
        private static readonly float ClimbSin = Mathf.Sin(10f * Mathf.Deg2Rad);

        private readonly EmitGate _gate = new EmitGate();
        private readonly BuildAttempts _attempts = new BuildAttempts(5, 2f);
        private readonly HashSet<int> _broken = new HashSet<int>();
        private FxRig _rig;
        private bool _dirty = true;

        public EffectSettings Settings { get; set; } = new EffectSettings();

        /// <summary>What the rig is attached to, for the editor's info line.</summary>
        public string RigDescription => _rig != null ? _rig.Description : null;

        public void MarkDirty() => _dirty = true;

        private void Update()
        {
            SanctuaryPet pet = SanctuaryManager.pCurPetInstance;
            if (pet == null)
            {
                DropRig();
                return;
            }
            int id = pet.GetInstanceID();
            if (_broken.Contains(id))
                return;
            try
            {
                if (_rig == null || !_rig.IsFor(pet))
                {
                    DropRig();
                    if (!TryBuild(pet, id))
                        return;
                }
                var inputs = new EmitInputs
                {
                    Enabled = Settings.Enabled,
                    HasPet = true,
                    Flying = pet.pIsMounted && AvAvatar.IsFlying(),
                    Accelerating = GameAccess.IsAccelerating(pet) && pet.pCurrentSkillType == PetSpecialSkillType.FLY
                                   && pet.transform.forward.y <= ClimbSin,
                    Preview = Settings.PreviewOnGround,
                    Mode = Settings.Mode,
                };
                _gate.Step(inputs, Time.deltaTime);
                Style style = StyleTable.For(Settings, Time.time);
                if (_dirty)
                {
                    _rig.Apply(Settings, style);
                    _dirty = false;
                }
                _rig.Tick(style, _gate.Intensity, _gate.Emitting, Settings.Type == EffectType.Rainbow);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FlightEffects: disabled for {pet.name} after an error: {e}");
                _broken.Add(id);
                DropRig();
            }
        }

        private bool TryBuild(SanctuaryPet pet, int id)
        {
            if (!_attempts.CanTry(id, Time.unscaledTime))
                return false;
            _rig = FxRig.Build(pet, out string reason);
            if (_rig == null)
            {
                bool gaveUp = _attempts.Failed(id, Time.unscaledTime);
                Plugin.Log.LogWarning($"FlightEffects: no effect rig for {pet.name}: {reason}" +
                                      (gaveUp ? " - giving up for this dragon" : " - will retry"));
                return false;
            }
            _attempts.Succeeded(id);
            Plugin.Log.LogInfo($"FlightEffects: rig built for {_rig.Description}");
            _gate.Reset();
            _dirty = true;
            return true;
        }

        private void DropRig()
        {
            if (_rig == null)
                return;
            _rig.Dispose();
            _rig = null;
            _gate.Reset();
        }

        private void OnDestroy() => DropRig();
    }
}
