using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlightEffects
{
    using FlightEffects.Core;

    /// <summary>Trail + particle emitters on one dragon's wing tips (and tail tips), styled from EffectSettings.</summary>
    internal sealed class FxRig
    {
        private sealed class Emitter
        {
            public GameObject Go;
            public TrailRenderer Trail;
            public ParticleSystem Particles;
            public ParticleSystemRenderer ParticleRenderer;
            public bool IsTail;
            public float ParticleRate;
            public Vector3 LastPos;
            public bool HasLastPos;
        }

        private const float TailWidthFactor = 0.7f;

        private readonly SanctuaryPet _pet;
        private readonly float _span;
        private readonly List<Emitter> _emitters = new List<Emitter>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly Gradient _gradient = new Gradient();
        private float _shownIntensity = -1f;
        private bool _emitting;
        private bool _particlesOn;

        private FxRig(SanctuaryPet pet, float span)
        {
            _pet = pet;
            _span = span;
        }

        public string Description { get; private set; }

        /// <summary>Builds emitters at the wing (and tail) tips found in the bind pose; null + reason when impossible.</summary>
        public static FxRig Build(SanctuaryPet pet, out string reason)
        {
            reason = null;
            if (ShaderLib.Additive == null || ShaderLib.Alpha == null)
            {
                reason = "no usable shader (" + ShaderLib.Describe() + ")";
                return null;
            }
            // the pet's own renderers (filled at load, before a rider is parented to the dragon); never the avatar's
            IEnumerable<SkinnedMeshRenderer> candidates = GameAccess.OwnRenderers(pet)
                ?? (IEnumerable<SkinnedMeshRenderer>)pet.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Transform avatar = AvAvatar.pObject != null ? AvAvatar.pObject.transform : null;
            SkinnedMeshRenderer smr = candidates
                .Where(r => r != null && r.sharedMesh != null && r.bones != null && r.bones.Length > 0
                            && (avatar == null || !r.transform.IsChildOf(avatar)))
                .OrderByDescending(r => r.bones.Length).FirstOrDefault();
            if (smr == null)
            {
                reason = "no skinned mesh";
                return null;
            }
            Transform[] bones = smr.bones;
            Matrix4x4[] bind = smr.sharedMesh.bindposes;
            if (bind == null || bind.Length < bones.Length)
            {
                reason = $"{(bind == null ? 0 : bind.Length)} bindposes for {bones.Length} bones";
                return null;
            }
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && !indexOf.ContainsKey(bones[i]))
                    indexOf[bones[i]] = i;
            var infos = new List<BoneInfo>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                int parent = -1;
                if (bones[i] != null)
                    for (Transform p = bones[i].parent; p != null; p = p.parent)
                        if (indexOf.TryGetValue(p, out int pi))
                        {
                            parent = pi;
                            break;
                        }
                Vector3 pos = bind[i].inverse.GetColumn(3);
                infos.Add(new BoneInfo
                {
                    Name = bones[i] != null ? bones[i].name : "", Parent = parent, BindPos = new Vec3(pos.x, pos.y, pos.z),
                });
            }
            TipResult tips = TipFinder.Find(infos);
            if (!tips.HasWings && tips.TailTips.Count == 0)
            {
                reason = "no wing or tail tips";
                return null;
            }
            Vector3 s = smr.transform.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            float span = Mathf.Clamp(tips.Wingspan * (scale > 0f ? scale : 1f), 0.5f, 60f);
            var rig = new FxRig(pet, span);
            try
            {
                if (tips.HasWings)
                {
                    rig.Add(bones[tips.LeftWing], bind[tips.LeftWing], tips.LeftOffset, false);
                    rig.Add(bones[tips.RightWing], bind[tips.RightWing], tips.RightOffset, false);
                }
                for (int k = 0; k < tips.TailTips.Count; k++)
                    rig.Add(bones[tips.TailTips[k]], bind[tips.TailTips[k]], tips.TailOffsets[k], true);
                if (rig._emitters.Count == 0)
                {
                    reason = "tip bones are missing";
                    return null;
                }
                string wings = tips.HasWings ? $"{infos[tips.LeftWing].Name}/{infos[tips.RightWing].Name}" : "none";
                rig.Description = $"{pet.name}: wings {wings}, tail tips {tips.TailTips.Count}, span {span:0.0} m";
                return rig;
            }
            catch
            {
                rig.Dispose(); // never leave half a rig on the bones
                throw;
            }
        }

        public bool IsFor(SanctuaryPet pet) =>
            pet != null && ReferenceEquals(pet, _pet) && _emitters.Count > 0 && _emitters.TrueForAll(e => e.Go != null);

        /// <summary>Full reconfiguration after a settings change.</summary>
        public void Apply(EffectSettings s, Style st)
        {
            Material trailMat = MaterialFor(st.TrailBlend, SpriteKind.None);
            Material particleMat = st.Particle != SpriteKind.None && st.ParticleRate > 0f
                ? MaterialFor(st.ParticleBlend, st.Particle) : null;
            foreach (Emitter e in _emitters)
            {
                bool on = e.IsTail ? s.Tail : s.Wingtips;
                if (e.Go.activeSelf != on)
                {
                    // switching an emitter on or off never keeps old trail history (no streak from the ground later)
                    e.Trail.emitting = false;
                    e.Trail.Clear();
                    e.Particles.Clear(true);
                    e.HasLastPos = false;
                    e.Go.SetActive(on);
                }
                float factor = e.IsTail ? TailWidthFactor : 1f;
                e.Trail.time = s.Length;
                e.Trail.widthMultiplier = st.TrailWidth * s.Width * _span * factor;
                e.Trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, st.TrailEndWidth));
                e.Trail.sharedMaterial = trailMat;
                e.Trail.minVertexDistance = 0.01f * _span;
                ConfigureParticles(e, s, st, particleMat, factor);
            }
            _shownIntensity = -1f;
            _particlesOn = false;
            _emitting = false;
        }

        /// <summary>Per frame: colours (rainbow moves), fade intensity, emitting on/off.</summary>
        public void Tick(Style st, float intensity, bool emitting, bool animatedColors)
        {
            foreach (Emitter e in _emitters)
            {
                if (!e.Go.activeInHierarchy)
                {
                    e.HasLastPos = false;
                    continue;
                }
                Vector3 pos = e.Go.transform.position;
                if (e.HasLastPos && Motion.IsJump(new Vec3(e.LastPos.x, e.LastPos.y, e.LastPos.z), new Vec3(pos.x, pos.y, pos.z), _span))
                {
                    e.Trail.Clear(); // teleport: no line across the scene
                    e.Particles.Clear(true);
                }
                e.LastPos = pos;
                e.HasLastPos = true;
            }
            if (animatedColors || Mathf.Abs(intensity - _shownIntensity) > 0.004f)
            {
                SetGradient(st.TrailColors, intensity);
                foreach (Emitter e in _emitters)
                {
                    e.Trail.colorGradient = _gradient;
                    ParticleSystem.EmissionModule em = e.Particles.emission;
                    em.rateOverDistance = e.ParticleRate * intensity;
                }
                _shownIntensity = intensity;
            }
            bool particles = emitting && st.Particle != SpriteKind.None && st.ParticleRate > 0f;
            if (emitting != _emitting || particles != _particlesOn)
            {
                foreach (Emitter e in _emitters)
                {
                    if (!e.Go.activeSelf)
                        continue;
                    e.Trail.emitting = emitting;
                    if (particles)
                        e.Particles.Play(true);
                    else
                        e.Particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                _emitting = emitting;
                _particlesOn = particles;
            }
        }

        public void Dispose()
        {
            foreach (Emitter e in _emitters)
                if (e.Go != null)
                    Object.Destroy(e.Go);
            _emitters.Clear();
            foreach (Material m in _materials.Values)
                if (m != null)
                    Object.Destroy(m);
            _materials.Clear();
        }

        private void Add(Transform bone, Matrix4x4 bindpose, Vec3 offset, bool tail)
        {
            if (bone == null)
                return;
            var go = new GameObject(tail ? "FlightFx_Tail" : "FlightFx_Wing");
            go.transform.SetParent(bone, false);
            go.transform.localPosition = bindpose.MultiplyVector(new Vector3(offset.X, offset.Y, offset.Z));
            go.transform.localRotation = Quaternion.identity;
            var trail = go.AddComponent<TrailRenderer>();
            trail.emitting = false;
            trail.autodestruct = false;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;
            trail.minVertexDistance = 0.01f * _span;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.02f * _span);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f * _span;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _emitters.Add(new Emitter { Go = go, Trail = trail, Particles = ps, ParticleRenderer = renderer, IsTail = tail });
        }

        private void ConfigureParticles(Emitter e, EffectSettings s, Style st, Material mat, float factor)
        {
            ParticleSystem ps = e.Particles;
            e.ParticleRate = mat != null ? st.ParticleRate : 0f;
            if (mat == null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            float size = st.ParticleSize * s.Width * _span * factor;
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = st.ParticleLifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f * size, 1.3f * size);
            main.startRotation = new ParticleSystem.MinMaxCurve(-st.ParticleSpin, st.ParticleSpin);
            main.gravityModifier = st.ParticleGravity;
            main.maxParticles = st.MaxParticles;
            main.startColor = RandomColors(st.ParticleColors);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(st.Twinkle
                ? AlphaCurve(new[] { 0f, 0.1f, 0.35f, 0.55f, 0.8f, 1f }, new[] { 0f, 1f, 0.35f, 1f, 0.5f, 0f })
                : AlphaCurve(new[] { 0f, 0.6f, 1f }, new[] { 1f, 0.8f, 0f }));
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, st.Twinkle
                ? new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(0.5f, 0.7f), new Keyframe(1f, 0.3f))
                : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
            e.ParticleRenderer.sharedMaterial = mat;
        }

        private void SetGradient(GradientKey[] keys, float intensity)
        {
            int n = Mathf.Min(keys.Length, 8);
            var colors = new GradientColorKey[n];
            var alphas = new GradientAlphaKey[n];
            for (int i = 0; i < n; i++)
            {
                Rgba c = keys[i].Color;
                colors[i] = new GradientColorKey(new Color(c.R, c.G, c.B), keys[i].Time);
                alphas[i] = new GradientAlphaKey(c.A * intensity, keys[i].Time);
            }
            _gradient.SetKeys(colors, alphas);
        }

        private static ParticleSystem.MinMaxGradient RandomColors(Rgba[] colors)
        {
            if (colors == null || colors.Length == 0)
                return new ParticleSystem.MinMaxGradient(Color.white);
            int n = Mathf.Min(colors.Length, 8);
            var ck = new GradientColorKey[Mathf.Max(n, 2)];
            var ak = new GradientAlphaKey[ck.Length];
            for (int i = 0; i < ck.Length; i++)
            {
                Rgba c = colors[Mathf.Min(i, n - 1)];
                float t = ck.Length == 1 ? 0f : i / (float)(ck.Length - 1);
                ck[i] = new GradientColorKey(new Color(c.R, c.G, c.B), t);
                ak[i] = new GradientAlphaKey(c.A, t);
            }
            var g = new Gradient();
            g.SetKeys(ck, ak);
            return new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        }

        private static Gradient AlphaCurve(float[] times, float[] alphas)
        {
            var g = new Gradient();
            var ak = new GradientAlphaKey[times.Length];
            for (int i = 0; i < times.Length; i++)
                ak[i] = new GradientAlphaKey(alphas[i], times[i]);
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, ak);
            return g;
        }

        private Material MaterialFor(Blend blend, SpriteKind sprite)
        {
            string key = blend + "/" + sprite;
            if (_materials.TryGetValue(key, out Material m) && m != null)
                return m;
            m = ShaderLib.NewMaterial(blend, sprite == SpriteKind.None ? TextureLib.Streak : TextureLib.Get(sprite));
            _materials[key] = m;
            return m;
        }
    }
}
