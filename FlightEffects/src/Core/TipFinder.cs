using System;
using System.Collections.Generic;
using System.Linq;

namespace FlightEffects.Core
{
    public struct Vec3
    {
        public float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float f) => new Vec3(a.X * f, a.Y * f, a.Z * f);
        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }

    /// <summary>One skinned bone: name, index of the nearest bone ancestor (-1 = none), bind-pose position in mesh
    /// space (Unity axes: -X is the dragon's left, -Z is behind, the mesh origin lies on the centre line).</summary>
    public sealed class BoneInfo
    {
        public string Name;
        public int Parent = -1;
        public Vec3 BindPos;
    }

    public sealed class TipResult
    {
        public int LeftWing = -1, RightWing = -1;
        /// <summary>Mesh-space offsets from the tip joint to the emission point.</summary>
        public Vec3 LeftOffset, RightOffset;
        public List<int> TailTips = new List<int>();
        public List<Vec3> TailOffsets = new List<Vec3>();
        /// <summary>Distance between the two wing emission points (without wings: the X extent of all bones).</summary>
        public float Wingspan;

        public bool HasWings => LeftWing >= 0 && RightWing >= 0;
    }

    /// <summary>Finds wing and tail tips in a dragon's bind pose (wings spread), for any rig naming.</summary>
    public static class TipFinder
    {
        /// <summary>Emission point beyond the tip joint, as a share of the last bone (parent -> tip).</summary>
        public const float WingExtension = 0.35f;
        public const float TailExtension = 1.0f;
        /// <summary>Tail leaves count as tips when they lie this share of the model length near the rearmost one.</summary>
        public const float TailDepthFraction = 0.05f;

        public static TipResult Find(IList<BoneInfo> bones)
        {
            var r = new TipResult();
            if (bones == null || bones.Count < 2)
                return r;
            List<int> wing = Named(bones, "wing");
            if (wing.Count < 2)
                wing = Enumerable.Range(0, bones.Count).ToList();
            int left = wing.OrderBy(i => bones[i].BindPos.X).First();
            int right = wing.OrderByDescending(i => bones[i].BindPos.X).First();
            if (bones[left].BindPos.X < 0f && bones[right].BindPos.X > 0f)
            {
                r.LeftWing = left;
                r.RightWing = right;
                r.LeftOffset = Offset(bones, left, WingExtension);
                r.RightOffset = Offset(bones, right, WingExtension);
                r.Wingspan = (bones[right].BindPos + r.RightOffset - (bones[left].BindPos + r.LeftOffset)).Length;
            }
            else
            {
                r.Wingspan = bones.Max(b => b.BindPos.X) - bones.Min(b => b.BindPos.X);
            }

            List<int> tail = Named(bones, "tail");
            var parents = new HashSet<int>(tail.Select(i => bones[i].Parent).Where(p => tail.Contains(p)));
            List<int> leaves = tail.Where(i => !parents.Contains(i)).ToList();
            if (leaves.Count > 0)
            {
                float rear = leaves.Min(i => bones[i].BindPos.Z);
                float length = bones.Max(b => b.BindPos.Z) - bones.Min(b => b.BindPos.Z);
                foreach (int i in leaves.Where(i => bones[i].BindPos.Z <= rear + TailDepthFraction * length))
                {
                    r.TailTips.Add(i);
                    r.TailOffsets.Add(Offset(bones, i, TailExtension));
                }
            }
            return r;
        }

        private static List<int> Named(IList<BoneInfo> bones, string part) =>
            Enumerable.Range(0, bones.Count)
                .Where(i => (bones[i].Name ?? "").IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        private static Vec3 Offset(IList<BoneInfo> bones, int i, float extension)
        {
            int p = bones[i].Parent;
            return p >= 0 && p < bones.Count ? (bones[i].BindPos - bones[p].BindPos) * extension : default;
        }
    }
}
