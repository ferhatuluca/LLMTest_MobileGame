using System;
using MonstersVsZombies.Combat;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Units
{
    public enum TargetingMode
    {
        Disabled,
        AIChaseRange,
        PlayerAttackRange
    }

    /// <summary>
    /// Periodically selects the nearest hostile unit in range.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetingController : MonoBehaviour, IPoolable
    {
        private const string k_UnitTargetLayerName = "UnitTarget";

        private UnitController _unitController;
        private Collider[] _colliders;
        private DamageTargetProxy _currentTargetProxy;
        private float _scanTimeRemaining;
        private float _queryRange;
        private bool _isPreparedForSpawn;

        [field: SerializeField] public int QueryCapacity { get; private set; }
        [field: SerializeField] public float ScanInterval { get; private set; }
        [field: SerializeField] public float InitialScanDelay { get; private set; }

        public UnitController CurrentTarget { get; private set; }
        public Vector3 CurrentTargetPoint =>
            _currentTargetProxy != null &&
            _currentTargetProxy.TargetCollider != null
                ? _currentTargetProxy.TargetCollider.bounds.center
                : CurrentTarget == null
                    ? transform.position
                    : CurrentTarget.transform.position;
        public TargetingMode Mode { get; private set; }
        public float QueryRange => _queryRange;

        private void Awake()
        {
            CacheComponents();
            EnsureColliderBuffer();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        public bool InitializeScanning(
            int queryCapacity,
            float scanInterval,
            float initialScanDelay)
        {
            if (queryCapacity <= 0 || !IsPositiveFinite(scanInterval) ||
                initialScanDelay < 0f || initialScanDelay > scanInterval)
            {
                return false;
            }

            QueryCapacity = queryCapacity;
            ScanInterval = scanInterval;
            InitialScanDelay = initialScanDelay;
            _colliders = new Collider[queryCapacity];
            _scanTimeRemaining = initialScanDelay;
            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheComponents();
            EnsureColliderBuffer();
            bool isValid = _unitController != null && _colliders != null &&
                           IsPositiveFinite(ScanInterval) &&
                           InitialScanDelay >= 0f &&
                           InitialScanDelay <= ScanInterval;
            failureMessage = isValid
                ? string.Empty
                : "TargetingController requires a UnitController and a valid scan schedule.";
            return isValid;
        }

        public bool SetPlayerAttackRange(float attackRange)
        {
            CacheComponents();
            if (!(_unitController?.Definition is PlayerUnitDefinition) ||
                !IsPositiveFinite(attackRange))
            {
                return false;
            }

            Mode = TargetingMode.PlayerAttackRange;
            _queryRange = attackRange;
            ClearInvalidTarget();
            return true;
        }

        public bool PrepareForSpawn()
        {
            CacheComponents();
            EnsureColliderBuffer();
            ClearCurrentTarget();
            _scanTimeRemaining = InitialScanDelay;
            Mode = TargetingMode.Disabled;
            _queryRange = 0f;
            _isPreparedForSpawn = false;

            if (_unitController?.Definition == null || _colliders == null)
            {
                return false;
            }

            if (_unitController.Definition is AIUnitDefinition aiDefinition)
            {
                if (!IsPositiveFinite(aiDefinition.ChaseRange))
                {
                    return false;
                }

                Mode = TargetingMode.AIChaseRange;
                _queryRange = aiDefinition.ChaseRange;
            }

            _isPreparedForSpawn = true;
            return true;
        }

        public bool CompleteSpawn()
        {
            return _isPreparedForSpawn && gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
            ClearCurrentTarget();
            _scanTimeRemaining = InitialScanDelay;
            Mode = TargetingMode.Disabled;
            _queryRange = 0f;
            _isPreparedForSpawn = false;
        }

        internal void AdvanceTime(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (_unitController == null || !_unitController.IsActive ||
                Mode == TargetingMode.Disabled)
            {
                return;
            }

            ClearInvalidTarget();
            _scanTimeRemaining -= deltaTime;
            if (_scanTimeRemaining <= 0f)
            {
                ForceScan();
                _scanTimeRemaining = ScanInterval;
            }
        }

        internal bool ForceScan()
        {
            if (_unitController == null || !_unitController.IsActive ||
                Mode == TargetingMode.Disabled || _colliders == null ||
                !IsPositiveFinite(_queryRange))
            {
                return false;
            }

            int layer = LayerMask.NameToLayer(k_UnitTargetLayerName);
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _queryRange,
                _colliders,
                1 << layer,
                QueryTriggerInteraction.Collide);

            UnitController nearest = null;
            DamageTargetProxy nearestProxy = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                Collider candidateCollider = _colliders[index];
                _colliders[index] = null;
                if (candidateCollider == null ||
                    !candidateCollider.TryGetComponent(
                        out DamageTargetProxy targetProxy))
                {
                    continue;
                }

                UnitController candidate = targetProxy.UnitController;
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                float distance = CombatRangeRules.GetSquaredPlanarDistance(
                    transform.position,
                    candidate.transform.position);
                if (nearest == null || distance < nearestDistance ||
                    (distance == nearestDistance &&
                     candidate.SpawnId.CompareTo(nearest.SpawnId) < 0))
                {
                    nearest = candidate;
                    nearestProxy = targetProxy;
                    nearestDistance = distance;
                }
            }

            CurrentTarget = nearest;
            _currentTargetProxy = nearestProxy;
            return CurrentTarget != null;
        }

        internal bool IsCurrentTargetWithinRange(float range)
        {
            return IsPositiveFinite(range) && IsValidTarget(CurrentTarget) &&
                   CombatRangeRules.IsWithinRange(
                       transform.position,
                       CurrentTarget.transform.position,
                       range);
        }

        internal void ClearCurrentTarget()
        {
            CurrentTarget = null;
            _currentTargetProxy = null;
        }

        private bool IsValidTarget(UnitController candidate)
        {
            return candidate != null && candidate != _unitController &&
                   candidate.IsActive && candidate.SpawnId.IsValid &&
                   candidate.HealthController != null &&
                   candidate.HealthController.IsAlive &&
                   FactionRules.AreHostile(
                       _unitController.Faction,
                       candidate.Faction) &&
                   CombatRangeRules.IsWithinRange(
                       transform.position,
                       candidate.transform.position,
                       _queryRange);
        }

        private void ClearInvalidTarget()
        {
            if (CurrentTarget != null && !IsValidTarget(CurrentTarget))
            {
                ClearCurrentTarget();
            }
        }

        private void CacheComponents()
        {
            _unitController = GetComponent<UnitController>();
        }

        private void EnsureColliderBuffer()
        {
            if (QueryCapacity > 0 &&
                (_colliders == null || _colliders.Length != QueryCapacity))
            {
                _colliders = new Collider[QueryCapacity];
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
