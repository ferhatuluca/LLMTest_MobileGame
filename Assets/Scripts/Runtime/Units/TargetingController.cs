using System;
using MonstersVsZombies.Combat;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
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

    public readonly struct TargetingEvent
    {
        public UnitController Source { get; }
        public UnitController Target { get; }
        public SpawnId TargetSpawnId { get; }

        public TargetingEvent(
            UnitController source,
            UnitController target,
            SpawnId targetSpawnId)
        {
            Source = source;
            Target = target;
            TargetSpawnId = targetSpawnId;
        }
    }

    [DisallowMultipleComponent]
    public sealed class TargetingController : MonoBehaviour, IPoolable
    {
        private UnitController _unitController;
        private UnitLifecycleController _lifecycleController;
        private UnitLifecycleController _subscribedLifecycleController;
        private TargetQueryBuffer _queryBuffer;
        private SpawnId _currentTargetSpawnId;
        private float _scanInterval;
        private float _initialScanDelay;
        private float _scanTimeRemaining;
        private float _queryRange;
        private bool _isPreparedForSpawn;

        public event Action<TargetingEvent> TargetAcquired;
        public event Action<TargetingEvent> TargetLost;

        public UnitController CurrentTarget { get; private set; }
        public TargetingMode Mode { get; private set; }
        public float QueryRange => _queryRange;
        public bool IsInitialized => _queryBuffer != null;
        public bool WasLastScanSaturated =>
            _queryBuffer != null && _queryBuffer.WasSaturated;
        public int LastUniqueCandidateCount =>
            _queryBuffer == null ? 0 : _queryBuffer.UniqueTargetCount;

        private void Awake()
        {
            CacheSiblingComponents();
            EnsurePermanentSubscriptions();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        private void OnDestroy()
        {
            ReleaseCurrentTargetSubscription();
            ReleasePermanentSubscriptions();
        }

        public bool InitializeScanning(
            int queryCapacity,
            float scanInterval,
            float initialScanDelay)
        {
            if (_queryBuffer != null || queryCapacity <= 0 ||
                scanInterval <= 0f ||
                float.IsNaN(scanInterval) ||
                float.IsInfinity(scanInterval) ||
                initialScanDelay < 0f ||
                initialScanDelay > scanInterval ||
                float.IsNaN(initialScanDelay) ||
                float.IsInfinity(initialScanDelay))
            {
                return false;
            }

            _queryBuffer = new TargetQueryBuffer(queryCapacity);
            _scanInterval = scanInterval;
            _initialScanDelay = initialScanDelay;
            _scanTimeRemaining = initialScanDelay;
            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheSiblingComponents();
            if (_unitController == null || _lifecycleController == null)
            {
                failureMessage =
                    "TargetingController requires UnitController and " +
                    "UnitLifecycleController siblings.";
                return false;
            }

            if (!IsInitialized || !IsPositiveFinite(_scanInterval) ||
                _initialScanDelay < 0f ||
                _initialScanDelay > _scanInterval)
            {
                failureMessage =
                    "TargetingController requires an initialized query buffer " +
                    "and an explicit valid scan schedule.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool SetPlayerAttackRange(float attackRange)
        {
            CacheSiblingComponents();
            EnsurePermanentSubscriptions();
            if (_unitController == null ||
                !(_unitController.Definition is PlayerUnitDefinition) ||
                !IsPositiveFinite(attackRange))
            {
                return false;
            }

            Mode = TargetingMode.PlayerAttackRange;
            _queryRange = attackRange;
            if (CurrentTarget != null && !IsCurrentTargetValid())
            {
                ClearTarget();
            }

            return true;
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            ClearTarget();
            _scanTimeRemaining = _initialScanDelay;
            _isPreparedForSpawn = false;
            Mode = TargetingMode.Disabled;
            _queryRange = 0f;

            if (_unitController == null || !IsInitialized ||
                _unitController.Definition == null)
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
            ClearTarget();
            _scanTimeRemaining = _initialScanDelay;
            _queryBuffer?.Reset();
            Mode = TargetingMode.Disabled;
            _queryRange = 0f;
            _isPreparedForSpawn = false;
        }

        internal void AdvanceTime(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    "Targeting time must be non-negative and finite.");
            }

            if (_unitController == null || !_unitController.IsActive ||
                Mode == TargetingMode.Disabled || !IsInitialized)
            {
                return;
            }

            if (CurrentTarget != null && !IsCurrentTargetValid())
            {
                ClearTarget();
            }

            _scanTimeRemaining -= deltaTime;
            if (_scanTimeRemaining > 0f)
            {
                return;
            }

            ForceScan();
            _scanTimeRemaining = _scanInterval;
        }

        internal bool ForceScan()
        {
            if (_unitController == null || !_unitController.IsActive ||
                Mode == TargetingMode.Disabled || !IsInitialized ||
                !IsPositiveFinite(_queryRange))
            {
                return false;
            }

            _queryBuffer.Query(
                transform.position,
                _queryRange,
                _unitController.SpawnId,
                _unitController.Faction);

            UnitController selectedTarget = null;
            SpawnId selectedSpawnId = default;
            float selectedSquaredDistance = 0f;
            for (int targetIndex = 0;
                 targetIndex < _queryBuffer.UniqueTargetCount;
                 targetIndex++)
            {
                DamageTargetProxy targetProxy = _queryBuffer.GetTarget(targetIndex);
                UnitController candidate = targetProxy.UnitController;
                if (candidate == null ||
                    !CombatRangeRules.IsWithinRange(
                        transform.position,
                        candidate.transform.position,
                        _queryRange))
                {
                    continue;
                }

                float candidateSquaredDistance =
                    CombatRangeRules.GetSquaredPlanarDistance(
                        transform.position,
                        candidate.transform.position);
                if (selectedTarget == null ||
                    NearestTargetRules.IsCandidatePreferred(
                        candidateSquaredDistance,
                        candidate.SpawnId,
                        selectedSquaredDistance,
                        selectedSpawnId))
                {
                    selectedTarget = candidate;
                    selectedSpawnId = candidate.SpawnId;
                    selectedSquaredDistance = candidateSquaredDistance;
                }
            }

            SetTarget(selectedTarget);
            return selectedTarget != null;
        }

        internal bool IsCurrentTargetWithinRange(float range)
        {
            return IsPositiveFinite(range) &&
                   IsCurrentTargetIdentityValid() &&
                   CombatRangeRules.IsWithinRange(
                       transform.position,
                       CurrentTarget.transform.position,
                       range);
        }

        private bool IsCurrentTargetValid()
        {
            return IsCurrentTargetIdentityValid() &&
                   FactionRules.AreHostile(
                       _unitController.Faction,
                       CurrentTarget.Faction) &&
                   CurrentTarget.HealthController != null &&
                   CurrentTarget.HealthController.IsAlive &&
                   CurrentTarget.IsActive &&
                   CombatRangeRules.IsWithinRange(
                       transform.position,
                       CurrentTarget.transform.position,
                       _queryRange);
        }

        private bool IsCurrentTargetIdentityValid()
        {
            return CurrentTarget != null &&
                   _currentTargetSpawnId.IsValid &&
                   CurrentTarget.SpawnId == _currentTargetSpawnId;
        }

        private void SetTarget(UnitController target)
        {
            if (target == CurrentTarget &&
                (target == null || target.SpawnId == _currentTargetSpawnId))
            {
                return;
            }

            ClearTarget();
            if (target == null)
            {
                return;
            }

            CurrentTarget = target;
            _currentTargetSpawnId = target.SpawnId;
            if (target.LifecycleController != null)
            {
                target.LifecycleController.Despawned += HandleTargetDespawned;
            }

            TargetAcquired?.Invoke(new TargetingEvent(
                _unitController,
                CurrentTarget,
                _currentTargetSpawnId));
        }

        private void ClearTarget()
        {
            if (CurrentTarget == null)
            {
                _currentTargetSpawnId = default;
                return;
            }

            UnitController lostTarget = CurrentTarget;
            SpawnId lostSpawnId = _currentTargetSpawnId;
            ReleaseCurrentTargetSubscription();
            CurrentTarget = null;
            _currentTargetSpawnId = default;
            TargetLost?.Invoke(new TargetingEvent(
                _unitController,
                lostTarget,
                lostSpawnId));
        }

        private void HandleTargetDespawned(UnitLifecycleChangedEvent lifecycleEvent)
        {
            if (lifecycleEvent.Unit == CurrentTarget &&
                lifecycleEvent.SpawnId == _currentTargetSpawnId)
            {
                ClearTarget();
            }
        }

        private void HandleSourceDying(UnitLifecycleChangedEvent lifecycleEvent)
        {
            if (lifecycleEvent.Unit == _unitController)
            {
                ClearTarget();
            }
        }

        private void ReleaseCurrentTargetSubscription()
        {
            if (CurrentTarget != null &&
                CurrentTarget.LifecycleController != null)
            {
                CurrentTarget.LifecycleController.Despawned -=
                    HandleTargetDespawned;
            }
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
            _lifecycleController = GetComponent<UnitLifecycleController>();
        }

        private void EnsurePermanentSubscriptions()
        {
            if (_subscribedLifecycleController == _lifecycleController)
            {
                return;
            }

            ReleasePermanentSubscriptions();
            _subscribedLifecycleController = _lifecycleController;
            if (_subscribedLifecycleController != null)
            {
                _subscribedLifecycleController.Dying += HandleSourceDying;
            }
        }

        private void ReleasePermanentSubscriptions()
        {
            if (_subscribedLifecycleController != null)
            {
                _subscribedLifecycleController.Dying -= HandleSourceDying;
                _subscribedLifecycleController = null;
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
