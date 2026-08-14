using System;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Units
{
    public enum UnitLifecycleState
    {
        Inactive,
        Active,
        Dying,
        PoolReturn
    }

    public readonly struct UnitLifecycleChangedEvent
    {
        public UnitController Unit { get; }
        public SpawnId SpawnId { get; }
        public UnitLifecycleState PreviousState { get; }
        public UnitLifecycleState CurrentState { get; }

        public UnitLifecycleChangedEvent(
            UnitController unit,
            SpawnId spawnId,
            UnitLifecycleState previousState,
            UnitLifecycleState currentState)
        {
            Unit = unit;
            SpawnId = spawnId;
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }

    public readonly struct UnitPoolReturnRequest
    {
        public UnitController Unit { get; }
        public SpawnId SpawnId { get; }

        public UnitPoolReturnRequest(UnitController unit, SpawnId spawnId)
        {
            Unit = unit;
            SpawnId = spawnId;
        }
    }

    /// <summary>
    /// Activates units, publishes death, and resets them for pool reuse.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitLifecycleController : MonoBehaviour, IPoolable
    {
        private UnitController _unitController;
        private HealthController _healthController;
        private StatusEffectController _statusEffectController;
        private DamageController _damageController;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;
        private bool _isPublishingDeath;
        private bool _returnRequested;

        public event Action<UnitLifecycleChangedEvent> Dying;
        public event Action<UnitLifecycleChangedEvent> Despawned;
        public event Action<UnitPoolReturnRequest> PoolReturnRequested;

        public UnitLifecycleState State { get; private set; } =
            UnitLifecycleState.Inactive;

        private void Awake()
        {
            CacheComponents();
            if (_healthController != null)
            {
                _healthController.Died += HandleUnitDied;
            }
        }

        private void OnDestroy()
        {
            if (_healthController != null)
            {
                _healthController.Died -= HandleUnitDied;
            }
        }

        internal bool ConfigureSpawn(UnitDefinition definition, SpawnId spawnId)
        {
            CacheComponents();
            return State == UnitLifecycleState.Inactive &&
                   !_isPreparedForSpawn &&
                   _unitController != null &&
                   _unitController.ConfigureSpawn(definition, spawnId);
        }

        public bool PrepareForSpawn()
        {
            CacheComponents();
            _isPreparedForSpawn = false;
            if (gameObject.activeInHierarchy || _unitController == null ||
                !_unitController.ValidateCoreComponents(out _) ||
                _unitController.Definition == null ||
                !_unitController.SpawnId.IsValid ||
                !_healthController.InitializeForSpawn(
                    _unitController.Definition))
            {
                return false;
            }

            _unitController.MarkInactive();
            _statusEffectController.ResetForSpawn();
            _damageController.ResetForSpawn();
            _unitController.UnitMotor?.Stop();
            State = UnitLifecycleState.Inactive;
            _isPreparedForSpawn = true;
            _isActivationComplete = false;
            _returnRequested = false;
            return true;
        }

        public bool CompleteSpawn()
        {
            _isActivationComplete =
                _isPreparedForSpawn && gameObject.activeInHierarchy;
            return _isActivationComplete;
        }

        internal bool ActivateSpawn()
        {
            if (!_isPreparedForSpawn || !_isActivationComplete ||
                State != UnitLifecycleState.Inactive ||
                !_healthController.IsAlive)
            {
                return false;
            }

            _unitController.MarkActive();
            State = UnitLifecycleState.Active;
            _unitController.UnitMotor?.Resume();
            return true;
        }

        public void PrepareForReturn()
        {
            CacheComponents();
            SpawnId spawnId = _unitController == null
                ? default
                : _unitController.SpawnId;
            bool shouldPublishDespawn =
                State == UnitLifecycleState.Active && spawnId.IsValid;

            _unitController?.MarkInactive();
            if (shouldPublishDespawn)
            {
                UnitLifecycleState previousState = State;
                State = UnitLifecycleState.PoolReturn;
                Despawned?.Invoke(new UnitLifecycleChangedEvent(
                    _unitController,
                    spawnId,
                    previousState,
                    State));
            }

            _unitController?.UnitMotor?.Stop();
            _statusEffectController?.ClearForReturn();
            _damageController?.ResetForSpawn();
            State = UnitLifecycleState.Inactive;
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
            _returnRequested = false;
            _unitController?.ClearSpawnIdentity();
        }

        public void RequestPoolReturn()
        {
            if (State != UnitLifecycleState.Active &&
                State != UnitLifecycleState.Dying)
            {
                return;
            }

            _returnRequested = true;
            if (!_isPublishingDeath)
            {
                PublishReturnRequest();
            }
        }

        private void HandleUnitDied(UnitDeathEvent deathEvent)
        {
            if (State != UnitLifecycleState.Active ||
                deathEvent.SpawnId != _unitController.SpawnId)
            {
                return;
            }

            SpawnId spawnId = _unitController.SpawnId;
            UnitLifecycleState previousState = State;
            State = UnitLifecycleState.Dying;
            _unitController.MarkInactive();
            _unitController.UnitMotor?.Stop();
            _statusEffectController.ClearForDeath();
            UnitLifecycleChangedEvent lifecycleEvent =
                new UnitLifecycleChangedEvent(
                    _unitController,
                    spawnId,
                    previousState,
                    State);

            _isPublishingDeath = true;
            try
            {
                Dying?.Invoke(lifecycleEvent);
                Despawned?.Invoke(lifecycleEvent);
            }
            finally
            {
                _isPublishingDeath = false;
            }

            _returnRequested = true;
            PublishReturnRequest();
        }

        private void PublishReturnRequest()
        {
            if (!_returnRequested)
            {
                return;
            }

            _returnRequested = false;
            PoolReturnRequested?.Invoke(new UnitPoolReturnRequest(
                _unitController,
                _unitController == null ? default : _unitController.SpawnId));
        }

        private void CacheComponents()
        {
            _unitController = GetComponent<UnitController>();
            _healthController = GetComponent<HealthController>();
            _statusEffectController = GetComponent<StatusEffectController>();
            _damageController = GetComponent<DamageController>();
            _unitController?.CacheSiblingComponents();
        }
    }
}
