using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Interaction
{
    public abstract class UnitQueryBuffer
    {
        private const string k_UnitTargetLayerName = "UnitTarget";

        private readonly Collider[] _colliders;
        private readonly DamageTargetProxy[] _uniqueTargets;
        private readonly HashSet<SpawnId> _uniqueSpawnIds;
        private readonly int _unitTargetLayerMask;

        public int Capacity => _colliders.Length;
        public int UnitTargetLayerMask => _unitTargetLayerMask;
        public int ColliderCount { get; private set; }
        public int UniqueTargetCount { get; private set; }
        public bool WasSaturated { get; private set; }

        protected UnitQueryBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity),
                    "A query buffer requires a positive capacity.");
            }

            _colliders = new Collider[capacity];
            _uniqueTargets = new DamageTargetProxy[capacity];
            _uniqueSpawnIds = new HashSet<SpawnId>(capacity);
            int unitTargetLayer = LayerMask.NameToLayer(k_UnitTargetLayerName);
            if (unitTargetLayer < 0)
            {
                throw new InvalidOperationException(
                    $"The required {k_UnitTargetLayerName} physics layer is not configured.");
            }

            _unitTargetLayerMask = 1 << unitTargetLayer;
        }

        public int Query(
            Vector3 center,
            float radius,
            SpawnId sourceSpawnId,
            UnitFaction sourceFaction)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    "A query radius must be non-negative and finite.");
            }

            if (!sourceSpawnId.IsValid ||
                !Enum.IsDefined(typeof(UnitFaction), sourceFaction))
            {
                throw new ArgumentException(
                    "A unit query requires a valid captured source identity and faction.");
            }

            Reset();
            ColliderCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _colliders,
                _unitTargetLayerMask,
                QueryTriggerInteraction.Collide);
            WasSaturated = ColliderCount >= Capacity;
            BuildUniqueTargets(sourceSpawnId, sourceFaction);
            return UniqueTargetCount;
        }

        public DamageTargetProxy GetTarget(int index)
        {
            if (index < 0 || index >= UniqueTargetCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _uniqueTargets[index];
        }

        public void Reset()
        {
            for (int colliderIndex = 0; colliderIndex < ColliderCount; colliderIndex++)
            {
                _colliders[colliderIndex] = null;
            }

            for (int targetIndex = 0; targetIndex < UniqueTargetCount; targetIndex++)
            {
                _uniqueTargets[targetIndex] = null;
            }

            ColliderCount = 0;
            UniqueTargetCount = 0;
            WasSaturated = false;
            _uniqueSpawnIds.Clear();
        }

        internal bool TryAddTarget(
            DamageTargetProxy targetProxy,
            SpawnId sourceSpawnId,
            UnitFaction sourceFaction)
        {
            if (targetProxy == null || !targetProxy.IsConfigured)
            {
                return false;
            }

            SpawnId targetSpawnId = targetProxy.SpawnId;
            DamageController damageController = targetProxy.DamageController;
            if (!targetSpawnId.IsValid ||
                targetSpawnId == sourceSpawnId ||
                !FactionRules.AreHostile(sourceFaction, damageController.Faction) ||
                !damageController.IsAlive ||
                !damageController.IsTargetActive ||
                UniqueTargetCount >= Capacity ||
                !_uniqueSpawnIds.Add(targetSpawnId))
            {
                return false;
            }

            _uniqueTargets[UniqueTargetCount] = targetProxy;
            UniqueTargetCount++;
            return true;
        }

        private void BuildUniqueTargets(
            SpawnId sourceSpawnId,
            UnitFaction sourceFaction)
        {
            for (int colliderIndex = 0; colliderIndex < ColliderCount; colliderIndex++)
            {
                Collider collider = _colliders[colliderIndex];
                if (collider != null &&
                    collider.TryGetComponent(out DamageTargetProxy targetProxy))
                {
                    TryAddTarget(targetProxy, sourceSpawnId, sourceFaction);
                }
            }
        }
    }

    public sealed class TargetQueryBuffer : UnitQueryBuffer
    {
        public TargetQueryBuffer(int capacity) : base(capacity)
        {
        }
    }

    public sealed class AreaQueryBuffer : UnitQueryBuffer
    {
        public AreaQueryBuffer(int capacity) : base(capacity)
        {
        }
    }

    public static class NearestTargetRules
    {
        public static bool IsCandidatePreferred(
            float candidateSquaredDistance,
            SpawnId candidateSpawnId,
            float currentSquaredDistance,
            SpawnId currentSpawnId)
        {
            ValidateDistance(candidateSquaredDistance, nameof(candidateSquaredDistance));
            ValidateDistance(currentSquaredDistance, nameof(currentSquaredDistance));
            if (!candidateSpawnId.IsValid)
            {
                throw new ArgumentException(
                    "A nearest-target candidate requires a valid spawn ID.",
                    nameof(candidateSpawnId));
            }

            if (!currentSpawnId.IsValid)
            {
                return true;
            }

            if (candidateSquaredDistance < currentSquaredDistance)
            {
                return true;
            }

            return candidateSquaredDistance == currentSquaredDistance &&
                   candidateSpawnId.CompareTo(currentSpawnId) < 0;
        }

        private static void ValidateDistance(float squaredDistance, string parameterName)
        {
            if (float.IsNaN(squaredDistance) ||
                float.IsInfinity(squaredDistance) ||
                squaredDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "A squared target distance must be non-negative and finite.");
            }
        }
    }
}
