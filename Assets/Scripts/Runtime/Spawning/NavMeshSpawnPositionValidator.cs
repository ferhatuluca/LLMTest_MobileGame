using System;
using UnityEngine;
using UnityEngine.AI;

namespace MonstersVsZombies.Spawning
{
    /// <summary>
    /// Validates or samples requested unit positions against the NavMesh before
    /// a pooled unit is activated.
    /// </summary>
    public interface ISpawnPositionValidator
    {
        bool TryResolvePosition(
            Vector3 requestedPosition,
            out Vector3 resolvedPosition);
    }

    public sealed class NavMeshSpawnPositionValidator : ISpawnPositionValidator
    {
        public float MaximumSampleDistance { get; }
        public int AreaMask { get; }

        public NavMeshSpawnPositionValidator(
            float maximumSampleDistance,
            int areaMask)
        {
            if (maximumSampleDistance <= 0f ||
                float.IsNaN(maximumSampleDistance) ||
                float.IsInfinity(maximumSampleDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSampleDistance),
                    "NavMesh sample distance must be positive and finite.");
            }

            if (areaMask == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(areaMask),
                    "NavMesh area mask must include at least one area.");
            }

            MaximumSampleDistance = maximumSampleDistance;
            AreaMask = areaMask;
        }

        public bool TryResolvePosition(
            Vector3 requestedPosition,
            out Vector3 resolvedPosition)
        {
            if (!SpawnRequestValidation.IsPoseValid(
                    requestedPosition,
                    Quaternion.identity) ||
                !NavMesh.SamplePosition(
                    requestedPosition,
                    out NavMeshHit navMeshHit,
                    MaximumSampleDistance,
                    AreaMask))
            {
                resolvedPosition = default;
                return false;
            }

            resolvedPosition = navMeshHit.position;
            return true;
        }
    }
}
