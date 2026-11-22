using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlightEffects.Core;
using Xunit;

namespace FlightEffects.Tests
{
    public class TipFinderTests
    {
        private static BoneInfo Bone(string name, int parent, float x, float y, float z) =>
            new BoneInfo { Name = name, Parent = parent, BindPos = new Vec3(x, y, z) };

        /// <summary>Root, spine, two 4-bone wings (LWing04 at x -4), a non-wing hand further out (x -4.5), 5-bone tail.</summary>
        private static List<BoneInfo> GenericRig()
        {
            var b = new List<BoneInfo> { Bone("Root", -1, 0, 1, 0), Bone("Spine", 0, 0, 1, 1) };
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                int parent = 1;
                for (int i = 1; i <= 4; i++)
                {
                    b.Add(Bone($"{s}Wing0{i}", parent, side * i, 1.5f, 0.5f));
                    parent = b.Count - 1;
                }
            }
            b.Add(Bone("Hand_L", 1, -4.5f, 0.2f, 1f));
            int tailParent = 0;
            for (int i = 1; i <= 5; i++)
            {
                b.Add(Bone($"Tail0{i}", tailParent, 0, 1, -i));
                tailParent = b.Count - 1;
            }
            return b;
        }

        [Fact]
        public void GenericRig_UsesWingNamedBones()
        {
            List<BoneInfo> bones = GenericRig();
            TipResult r = TipFinder.Find(bones);
            Assert.Equal("LWing04", bones[r.LeftWing].Name);
            Assert.Equal("RWing04", bones[r.RightWing].Name);
            Assert.Equal(new[] { "Tail05" }, r.TailTips.Select(i => bones[i].Name));
        }

        [Fact]
        public void NoWingNames_FallsBackToMostLateralBones()
        {
            var bones = new List<BoneInfo>
            {
                Bone("Root", -1, 0, 1, 0), Bone("ArmL", 0, -3, 1, 0), Bone("ArmR", 0, 3, 1, 0), Bone("Neck", 0, 0, 2, 1),
            };
            TipResult r = TipFinder.Find(bones);
            Assert.Equal("ArmL", bones[r.LeftWing].Name);
            Assert.Equal("ArmR", bones[r.RightWing].Name);
            Assert.Empty(r.TailTips);
        }

        [Fact]
        public void TipFinder_TooFewBones_ReturnsNoWings()
        {
            Assert.False(TipFinder.Find(new List<BoneInfo> { Bone("Root", -1, 0, 1, 0) }).HasWings);
            Assert.False(TipFinder.Find(new List<BoneInfo>()).HasWings);
            Assert.False(TipFinder.Find(null).HasWings);
        }

        [Fact]
        public void TipFinder_WingsOnOneSideOnly_ReturnsNoWings()
        {
            var bones = new List<BoneInfo> { Bone("Root", -1, 0, 1, 0), Bone("WingA", 0, 1, 1, 0), Bone("WingB", 1, 3, 1, 0) };
            Assert.False(TipFinder.Find(bones).HasWings);
        }

        [Fact]
        public void Offsets_ExtendBeyondTheJoint()
        {
            TipResult r = TipFinder.Find(GenericRig());
            Assert.Equal(-TipFinder.WingExtension, r.LeftOffset.X, 4);
            Assert.Equal(0f, r.LeftOffset.Y, 4);
            Assert.Equal(TipFinder.WingExtension, r.RightOffset.X, 4);
            Assert.Equal(-TipFinder.TailExtension, r.TailOffsets.Single().Z, 4);
            Assert.Equal(8f + 2 * TipFinder.WingExtension, r.Wingspan, 3);
        }
    }

    public class EmitGateTests
    {
        private static EmitInputs In(bool flying = false, bool accel = false, bool preview = false, bool enabled = true,
            bool pet = true, EmitMode mode = EmitMode.WhileFlying) =>
            new EmitInputs { Enabled = enabled, HasPet = pet, Flying = flying, Accelerating = accel, Preview = preview, Mode = mode };

        [Fact]
        public void NotFlying_StaysOff()
        {
            var g = new EmitGate();
            for (int i = 0; i < 10; i++) g.Step(In(), 0.1f);
            Assert.Equal(0f, g.Intensity);
            Assert.False(g.Emitting);
        }

        [Fact]
        public void Flying_FadesInOverHalfASecond()
        {
            var g = new EmitGate();
            g.Step(In(flying: true), 0.25f);
            Assert.Equal(0.5f, g.Intensity, 4);
            Assert.True(g.Emitting);
            g.Step(In(flying: true), 0.25f);
            Assert.Equal(1f, g.Intensity, 4);
        }

        [Fact]
        public void Landing_FadesOut()
        {
            var g = new EmitGate();
            g.Step(In(flying: true), 1f);
            g.Step(In(), 0.25f);
            Assert.Equal(0.5f, g.Intensity, 4);
            g.Step(In(), 0.25f);
            Assert.Equal(0f, g.Intensity, 4);
            Assert.False(g.Emitting);
        }

        [Fact]
        public void AccelerateMode_NeedsAcceleration()
        {
            var g = new EmitGate();
            g.Step(In(flying: true, mode: EmitMode.WhileAccelerating), 1f);
            Assert.Equal(0f, g.Intensity);
            g.Step(In(flying: true, accel: true, mode: EmitMode.WhileAccelerating), 0.1f);
            Assert.True(g.Intensity > 0f);
        }

        [Fact]
        public void Preview_EmitsOnGround()
        {
            var g = new EmitGate();
            g.Step(In(preview: true), 1f);
            Assert.Equal(1f, g.Intensity);
        }

        [Fact]
        public void Disabled_FadesOut()
        {
            var g = new EmitGate();
            g.Step(In(flying: true), 1f);
            g.Step(In(flying: true, enabled: false), 1f);
            Assert.Equal(0f, g.Intensity);
        }

        [Fact]
        public void NoPet_DropsToZeroAtOnce()
        {
            var g = new EmitGate();
            g.Step(In(flying: true), 1f);
            g.Step(In(flying: true, pet: false), 0.01f);
            Assert.Equal(0f, g.Intensity);
        }

        [Fact]
        public void HugeDt_StaysInRange()
        {
            var g = new EmitGate();
            g.Step(In(flying: true), 10f);
            Assert.Equal(1f, g.Intensity);
            g.Step(In(), -5f);
            Assert.Equal(1f, g.Intensity);
        }
    }

    public class TextureMathTests
    {
        private static float Alpha(float[] px, int w, int x, int y) => px[(y * w + x) * 4 + 3];

        [Fact]
        public void Textures_HaveExpectedLength()
        {
            Assert.Equal(64 * 64 * 4, TextureMath.SoftDot(64).Length);
            Assert.Equal(64 * 64 * 4, TextureMath.Star(64, 4).Length);
            Assert.Equal(64 * 64 * 4, TextureMath.Heart(64).Length);
            Assert.Equal(128 * 32 * 4, TextureMath.Streak(128, 32).Length);
            float[] dot = TextureMath.SoftDot(16);
            for (int i = 0; i < dot.Length; i += 4)
                Assert.True(dot[i] == 1f && dot[i + 1] == 1f && dot[i + 2] == 1f, "texture colour must be white");
        }

        [Fact]
        public void SoftDot_BrightCentreTransparentCorners()
        {
            float[] px = TextureMath.SoftDot(64);
            Assert.True(Alpha(px, 64, 32, 32) > 0.9f);
            Assert.True(Alpha(px, 64, 0, 0) < 0.01f);
            Assert.Equal(Alpha(px, 64, 10, 32), Alpha(px, 64, 53, 32), 4);
        }

        [Fact]
        public void Star_PointsBrighterThanGaps()
        {
            float[] px = TextureMath.Star(64, 4);
            float onRay = Alpha(px, 64, 32 + 11, 32);          // along +x, r ~ 0.35
            float inGap = Alpha(px, 64, 32 + 8, 32 + 8);       // 45 degrees, same radius
            Assert.True(onRay > 0.5f && onRay > 4 * inGap, $"ray {onRay} gap {inGap}");
        }

        [Fact]
        public void Heart_MirrorSymmetricAndFilled()
        {
            const int n = 64;
            float[] px = TextureMath.Heart(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n / 2; x++)
                    Assert.Equal(Alpha(px, n, x, y), Alpha(px, n, n - 1 - x, y), 4);
            Assert.True(Alpha(px, n, n / 2, n / 2) > 0.99f, "centre filled");
            Assert.True(Enumerable.Range(0, n).All(x => Alpha(px, n, x, 0) < 0.01f), "bottom row empty");
            int top = (int)(n * 0.9f);
            float notch = Enumerable.Range(top, n - top).Max(y => Alpha(px, n, n / 2, y));
            float lobe = Enumerable.Range(top, n - top).Max(y => Alpha(px, n, n / 4 + 3, y));
            Assert.True(lobe > notch + 0.3f, $"lobe {lobe} notch {notch}");
        }

        [Fact]
        public void Streak_FadesTowardsEdges()
        {
            float[] px = TextureMath.Streak(128, 32);
            Assert.True(Alpha(px, 128, 64, 16) > 0.8f);
            Assert.True(Alpha(px, 128, 64, 0) < 0.1f && Alpha(px, 128, 64, 31) < 0.1f);
        }
    }
}
