using System;
using UnityEngine;

namespace MonstersVsZombies.Combat
{
    /// <summary>
    /// Provides shared squared-distance range checks used by targeting, AI, and
    /// attack eligibility without square-root work or divergent boundary rules.
    /// </summary>
    public static class CombatRangeRules
    {
        public static float GetSquaredPlanarDistance(
            Vector3 sourcePosition,
            Vector3 targetPosition)
        {
            ValidatePosition(sourcePosition, nameof(sourcePosition));
            ValidatePosition(targetPosition, nameof(targetPosition));
            float xDistance = targetPosition.x - sourcePosition.x;
            float zDistance = targetPosition.z - sourcePosition.z;
            return (xDistance * xDistance) + (zDistance * zDistance);
        }

        public static bool IsWithinRange(
            Vector3 sourcePosition,
            Vector3 targetPosition,
            float range)
        {
            ValidateRange(range);
            float squaredDistance = GetSquaredPlanarDistance(
                sourcePosition,
                targetPosition);
            return squaredDistance <= range * range;
        }

        private static void ValidatePosition(
            Vector3 position,
            string parameterName)
        {
            if (!IsFinite(position.x) ||
                !IsFinite(position.y) ||
                !IsFinite(position.z))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Combat positions must be finite.");
            }
        }

        private static void ValidateRange(float range)
        {
            if (range < 0f || !IsFinite(range))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(range),
                    "Combat range must be non-negative and finite.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
