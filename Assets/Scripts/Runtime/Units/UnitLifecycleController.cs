using System;
using System.Collections.Generic;
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
    /// Owns the unit state machine from inactive through active, dying, return,
    /// and reuse, including permanent and per-spawn event subscriptions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitLifecycleController : MonoBehaviour, IPoolable
    {
        private readonly List<Action> _spawnUnsubscriptions = new List<Action>();
        private UnitController _unitController;
        private HealthController _healthController;
        private StatusEffectController _statusEffectController;
        private DamageController _damageController;
        private HealthController _subscribedHealthController;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;
        private bool _hasPublishedDespawn;
        private bool _isPublishingLifecycleEvents;
        private bool _isPoolReturnRequested;
        private bool _isFinalizingActivation;

        public event Action<UnitLifecycleChangedEvent> StateChanged;
        public event Action<UnitLifecycleChangedEvent> Spawned;
        public event Action<UnitLifecycleChangedEvent> Dying;
        public event Action<UnitLifecycleChangedEvent> Despawned;
        public event Action<UnitPoolReturnRequest> PoolReturnRequested;

        public UnitLifecycleState State { get; private set; } = UnitLifecycleState.Inactive;

        private void Awake()
        {
            CacheSiblingComponents();
            EnsurePermanentSubscriptions();
        }

        private void OnDestroy()
        {
            ReleaseSpawnSubscriptions();
            if (_subscribedHealthController != null)
            {
                _subscribedHealthController.Died -= HandleUnitDied;
                _subscribedHealthController = null;
            }
        }

        internal bool ConfigureSpawn(UnitDefinition definition, SpawnId spawnId)
        {
            CacheSiblingComponents();
            EnsurePermanentSubscriptions();
            return State == UnitLifecycleState.Inactive &&
                   !_isPreparedForSpawn &&
                   _unitController != null &&
                   _unitController.ConfigureSpawn(definition, spawnId);
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            EnsurePermanentSubscriptions();
            ReleaseSpawnSubscriptions();

            if (gameObject.activeInHierarchy ||
                _unitController == null ||
                !_unitController.ValidateCoreComponents(out _) ||
                _unitController.Definition == null ||
                !_unitController.SpawnId.IsValid ||
                _healthController == null ||
                !_healthController.InitializeForSpawn(_unitController.Definition))
            {
                _isPreparedForSpawn = false;
                return false;
            }

            _unitController.MarkInactive();
            _statusEffectController.ResetForSpawn();
            _damageController.ResetForSpawn();
            _unitController.UnitMotor?.Stop();
            State = UnitLifecycleState.Inactive;
            _isPreparedForSpawn = true;
            _isActivationComplete = false;
            _hasPublishedDespawn = false;
            _isPoolReturnRequested = false;
            return true;
        }

        public bool CompleteSpawn()
        {
            _isActivationComplete = _isPreparedForSpawn && gameObject.activeInHierarchy;
            return _isActivationComplete;
        }

        internal bool ActivateSpawn()
        {
            if (!_isPreparedForSpawn || !_isActivationComplete ||
                !gameObject.activeInHierarchy || State != UnitLifecycleState.Inactive ||
                _healthController == null || !_healthController.IsAlive)
            {
                return false;
            }

            _unitController.MarkActive();
            _isFinalizingActivation = true;
            _isPublishingLifecycleEvents = true;
            try
            {
                UnitLifecycleChangedEvent lifecycleEvent = TransitionTo(
                    UnitLifecycleState.Active,
                    _unitController.SpawnId);
                _unitController.UnitMotor?.Resume();
                Spawned?.Invoke(lifecycleEvent);
            }
            finally
            {
                _isPublishingLifecycleEvents = false;
                _isFinalizingActivation = false;
            }

            return State == UnitLifecycleState.Active && _unitController.IsActive;
        }

        public void PrepareForReturn()
        {
            if (_isPublishingLifecycleEvents)
            {
                throw new InvalidOperationException(
                    "Request a pool return during lifecycle events instead of starting the pool callback reentrantly.");
            }

            PerformReturn();
        }

        private void PerformReturn()
        {
            CacheSiblingComponents();
            bool wasLogicalSpawn = State == UnitLifecycleState.Active ||
                                   State == UnitLifecycleState.Dying;
            SpawnId returningSpawnId = _unitController == null
                ? default
                : _unitController.SpawnId;

            bool hasConfiguredSpawn = returningSpawnId.IsValid;
            _isPublishingLifecycleEvents = true;
            try
            {
                if (_unitController != null)
                {
                    _unitController.MarkInactive();
                }

                if (hasConfiguredSpawn)
                {
                    UnitLifecycleChangedEvent poolReturnEvent = TransitionTo(
                        UnitLifecycleState.PoolReturn,
                        returningSpawnId);
                    if (wasLogicalSpawn && !_hasPublishedDespawn)
                    {
                        _hasPublishedDespawn = true;
                        Despawned?.Invoke(poolReturnEvent);
                    }
                }
                else
                {
                    State = UnitLifecycleState.Inactive;
                }

                ReleaseSpawnSubscriptions();
                _unitController?.UnitMotor?.Stop();
                _statusEffectController?.ClearForReturn();
                _damageController?.ResetForSpawn();
                if (hasConfiguredSpawn)
                {
                    TransitionTo(UnitLifecycleState.Inactive, returningSpawnId);
                }

                _isPreparedForSpawn = false;
                _isActivationComplete = false;
                _unitController?.ClearSpawnIdentity();
            }
            finally
            {
                _isPublishingLifecycleEvents = false;
            }

            _isPoolReturnRequested = false;
        }

        public void RequestPoolReturn()
        {
            if (_isFinalizingActivation)
            {
                throw new InvalidOperationException(
                    "A pool return cannot be requested while logical spawn activation is being finalized.");
            }

            if (State != UnitLifecycleState.Active && State != UnitLifecycleState.Dying)
            {
                return;
            }

            _isPoolReturnRequested = true;
            if (!_isPublishingLifecycleEvents)
            {
                PublishPendingPoolReturnRequest();
            }
        }

        public void RegisterSpawnSubscription(Action unsubscribeAction)
        {
            if (unsubscribeAction == null)
            {
                throw new ArgumentNullException(nameof(unsubscribeAction));
            }

            if (State != UnitLifecycleState.Active)
            {
                throw new InvalidOperationException(
                    "Per-spawn subscriptions can only be registered for an active spawn.");
            }

            _spawnUnsubscriptions.Add(unsubscribeAction);
        }

        private void HandleUnitDied(UnitDeathEvent deathEvent)
        {
            if (State != UnitLifecycleState.Active ||
                _unitController == null ||
                deathEvent.SpawnId != _unitController.SpawnId)
            {
                return;
            }

            SpawnId dyingSpawnId = _unitController.SpawnId;
            _unitController.MarkInactive();
            _hasPublishedDespawn = true;
            _isPublishingLifecycleEvents = true;
            try
            {
                UnitLifecycleChangedEvent lifecycleEvent = TransitionTo(
                    UnitLifecycleState.Dying,
                    dyingSpawnId);
                _unitController.UnitMotor?.Stop();
                _statusEffectController?.ClearForDeath();
                Dying?.Invoke(lifecycleEvent);
                Despawned?.Invoke(lifecycleEvent);
            }
            finally
            {
                _isPublishingLifecycleEvents = false;
            }

            PublishPendingPoolReturnRequest();
        }

        private UnitLifecycleChangedEvent TransitionTo(
            UnitLifecycleState nextState,
            SpawnId spawnId)
        {
            UnitLifecycleState previousState = State;
            State = nextState;
            UnitLifecycleChangedEvent lifecycleEvent = new UnitLifecycleChangedEvent(
                _unitController,
                spawnId,
                previousState,
                State);
            StateChanged?.Invoke(lifecycleEvent);
            return lifecycleEvent;
        }

        private void PublishPendingPoolReturnRequest()
        {
            if (!_isPoolReturnRequested)
            {
                return;
            }

            _isPoolReturnRequested = false;
            if (State != UnitLifecycleState.Active && State != UnitLifecycleState.Dying)
            {
                return;
            }

            PoolReturnRequested?.Invoke(new UnitPoolReturnRequest(
                _unitController,
                _unitController == null ? default : _unitController.SpawnId));
        }

        private void ReleaseSpawnSubscriptions()
        {
            for (int subscriptionIndex = _spawnUnsubscriptions.Count - 1;
                 subscriptionIndex >= 0;
                 subscriptionIndex--)
            {
                _spawnUnsubscriptions[subscriptionIndex]?.Invoke();
            }

            _spawnUnsubscriptions.Clear();
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
            _healthController = GetComponent<HealthController>();
            _statusEffectController = GetComponent<StatusEffectController>();
            _damageController = GetComponent<DamageController>();
            _unitController?.CacheSiblingComponents();
        }

        private void EnsurePermanentSubscriptions()
        {
            if (_subscribedHealthController == _healthController)
            {
                return;
            }

            if (_subscribedHealthController != null)
            {
                _subscribedHealthController.Died -= HandleUnitDied;
            }

            _subscribedHealthController = _healthController;
            if (_subscribedHealthController != null)
            {
                _subscribedHealthController.Died += HandleUnitDied;
            }
        }
    }
}
