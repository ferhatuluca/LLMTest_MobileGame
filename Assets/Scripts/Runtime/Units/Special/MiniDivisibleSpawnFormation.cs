using System;
using UnityEngine;

namespace MonstersVsZombies.Units.Special
{
    /// <summary>
    /// Computes deterministic radial candidate positions for MiniDivisible child
    /// spawns without owning NavMesh validation or spawn policy.
    /// </summary>
    public static class MiniDivisibleSpawnFormation
    {
        public const int ChildCount = 3;

        public static void FillRadialPositions(
            Vector3 center,
            Vector3 forward,
            float radialDistance,
            Vector3[] destination)
        {
            if (radialDistance <= 0f || float.IsNaN(radialDistance) ||
                float.IsInfinity(radialDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(radialDistance));
            }

            if (destination == null || destination.Length < ChildCount)
            {
                throw new ArgumentException(
                    "The formation destination requires space for three positions.",
                    nameof(destination));
            }

            Vector3 planarForward = forward;
            planarForward.y = 0f;
            if (planarForward.sqrMagnitude <= Mathf.Epsilon)
            {
                planarForward = Vector3.forward;
            }
            else
            {
                planarForward.Normalize();
            }

            for (int index = 0; index < ChildCount; index++)
            {
                Vector3 direction = Quaternion.AngleAxis(
                    index * 120f,
                    Vector3.up) * planarForward;
                destination[index] = center + direction * radialDistance;
            }
        }
    }
}
