using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FlightEffects
{
    /// <summary>
    /// The two private game fields the mod reads, via reflection (the project does not publicize Assembly-CSharp, so
    /// every other access is checked by the compiler). Missing fields degrade gracefully.
    /// </summary>
    internal static class GameAccess
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo Accelerating = typeof(SanctuaryPet).GetField("mIsAccelerating", Any);
        private static readonly FieldInfo RendererMap = typeof(SanctuaryPet).GetField("mRendererMap", Any);

        /// <summary>SanctuaryPet.mIsAccelerating (speeding up in flight); false when the field is missing.</summary>
        public static bool IsAccelerating(SanctuaryPet pet) =>
            Accelerating != null && Accelerating.GetValue(pet) is bool b && b;

        /// <summary>The dragon's own skinned meshes (SanctuaryPet.mRendererMap, filled at load); null when unknown.</summary>
        public static ICollection<SkinnedMeshRenderer> OwnRenderers(SanctuaryPet pet) =>
            RendererMap != null && RendererMap.GetValue(pet) is Dictionary<string, SkinnedMeshRenderer> map && map.Count > 0
                ? map.Values
                : null;

        public static string Describe() =>
            $"mIsAccelerating {(Accelerating != null ? "ok" : "missing")}, mRendererMap {(RendererMap != null ? "ok" : "missing")}";
    }
}
