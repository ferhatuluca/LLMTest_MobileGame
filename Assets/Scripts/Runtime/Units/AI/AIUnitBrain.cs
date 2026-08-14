using System;
using MonstersVsZombies.Combat;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Diagnostics;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units.Movement;
using MonstersVsZombies.Units.Special;
using UnityEngine;

namespace MonstersVsZombies.Units.AI
{
    public enum AIUnitState
    {
        Disabled,
        Idle,
        Chase,
        Attack
    }

    public readonly struct AIUnitStateChangedEvent
    {
        public UnitController Unit { get; }
        public AIUnitState PreviousState { get; }
        public AIUnitState CurrentState { get; }

        public AIUnitStateChangedEvent(
            UnitController unit,
            AIUnitState previousState,
            AIUnitState currentState)
        {
            Unit = unit;
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }

    /// <summary>
    /// Converts target range, stun, lifecycle, and cooldown state into the small
    /// Idle/Chase/Attack decision loop for an AI-controlled unit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitController))]
    [RequireComponent(typeof(TargetingController))]
    [RequireComponent(typeof(AttackController))]
    public sealed class AIUnitBrain : MonoBehaviour, IPoolable
    {
        private UnitController _unitController;
        private TargetingController _targetingController;
        private AttackController _attackController;
        private StatusEffectController _statusEffectController;
        private UnitLifecycleController _lifecycleController;
        private IUnitMotor _unitMotor;
        private MeleeAttackExecutor _meleeAttackExecutor;
        private ProjectileAttackExecutor _projectileAttackExecutor;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;
        private float _destinationRefreshRemaining;
        private Vector3 _lastRequestedTargetPosition;
        private bool _hasRequestedDestination;

        public event Action<AIUnitStateChangedEvent> StateChanged;

        public AIUnitState State { get; private set; } = AIUnitState.Disabled;
        public bool HasRuntimeServices { get; private set; }

        private void Awake()
        {
            CacheSiblingComponents();
            EnsurePermanentSubscriptions();
        }

        private void Update()
        {
            AdvanceDecision(Time.deltaTime);
        }

        private void OnDestroy()
        {
            ReleasePermanentSubscriptions();
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheSiblingComponents();
            if (_unitController == null || _targetingController == null ||
                _attackController == null || _statusEffectController == null ||
                _lifecycleController == null ||
                _unitMotor == null ||
                !(_unitController.Definition is AIUnitDefinition definition) ||
                !definition.Validate().IsValid)
            {
                failureMessage =
                    "AIUnitBrain requires AI definition, targeting, attack, status, and motor capabilities.";
                return false;
            }

            AIFactionDefinitionGuard factionGuard =
                GetComponent<AIFactionDefinitionGuard>();
            if (factionGuard != null &&
                !factionGuard.ValidateConfiguration(out failureMessage))
            {
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool ConfigureRuntimeServices(
            InteractionSystem interactionSystem)
        {
            return ConfigureRuntimeServices(null, interactionSystem);
        }

        public bool ConfigureRuntimeServices(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem)
        {
            CacheSiblingComponents();
            if (interactionSystem == null ||
                !(_unitController?.Definition is AIUnitDefinition definition) ||
                definition.DefaultAttackDefinition == null)
            {
                HasRuntimeServices = false;
                return false;
            }

            switch (definition.DefaultAttackDefinition.DeliveryType)
            {
                case AttackDeliveryType.Melee:
                    HasRuntimeServices = _meleeAttackExecutor != null &&
                        _meleeAttackExecutor.Configure(interactionSystem);
                    break;

                case AttackDeliveryType.Projectile:
                    HasRuntimeServices = spawnManager != null &&
                        _projectileAttackExecutor != null &&
                        _projectileAttackExecutor.Configure(
                            spawnManager,
                            interactionSystem,
                            _projectileAttackExecutor.AttackOrigin);
                    break;

                default:
                    HasRuntimeServices = false;
                    break;
            }

            SpawnUnitsOnDeath spawnUnitsOnDeath =
                GetComponent<SpawnUnitsOnDeath>();
            if (HasRuntimeServices && spawnUnitsOnDeath != null)
            {
                HasRuntimeServices = spawnUnitsOnDeath
                    .ConfigureRuntimeServices(
                        spawnManager,
                        interactionSystem);
            }

            return HasRuntimeServices;
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            EnsurePermanentSubscriptions();
            _unitMotor?.Stop();
            _destinationRefreshRemaining = 0f;
            _lastRequestedTargetPosition = transform.position;
            _hasRequestedDestination = false;
            HasRuntimeServices = false;
            _isActivationComplete = false;
            SetState(AIUnitState.Disabled);
            _isPreparedForSpawn = ValidateConfiguration(out _);
            return _isPreparedForSpawn;
        }

        public bool CompleteSpawn()
        {
            _isActivationComplete =
                _isPreparedForSpawn && gameObject.activeInHierarchy;
            return _isActivationComplete;
        }

        public void PrepareForReturn()
        {
            _targetingController?.ClearCurrentTarget();
            _unitMotor?.Stop();
            _destinationRefreshRemaining = 0f;
            _hasRequestedDestination = false;
            HasRuntimeServices = false;
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
            SetState(AIUnitState.Disabled);
        }

        internal void AdvanceDecision(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (SandboxDebugRuntime.AreAIDecisionsPaused)
            {
                _unitMotor?.Stop();
                return;
            }

            _destinationRefreshRemaining = Mathf.Max(
                0f,
                _destinationRefreshRemaining - deltaTime);
            if (!_isPreparedForSpawn || !_isActivationComplete ||
                _unitController == null || !_unitController.IsActive ||
                _statusEffectController == null ||
                _statusEffectController.IsChaseBlocked ||
                _unitController.HealthController == null ||
                !_unitController.HealthController.IsAlive)
            {
                EnterDisabled(true);
                return;
            }

            if (State == AIUnitState.Disabled)
            {
                SetState(AIUnitState.Idle);
                _unitMotor.Stop();
                return;
            }

            UnitController target = _targetingController.CurrentTarget;
            if (!IsTargetValid(target))
            {
                _targetingController.ClearCurrentTarget();
                EnterIdle();
                return;
            }

            AIUnitDefinition definition =
                (AIUnitDefinition)_unitController.Definition;
            float attackRange =
                definition.DefaultAttackDefinition.AttackRange;
            if (CombatRangeRules.IsWithinRange(
                    transform.position,
                    target.transform.position,
                    attackRange))
            {
                EnterAttack(target);
                return;
            }

            EnterChase(target);
        }

        private void EnterIdle()
        {
            _unitMotor.Stop();
            _hasRequestedDestination = false;
            SetState(AIUnitState.Idle);
        }

        private void EnterChase(UnitController target)
        {
            SetState(AIUnitState.Chase);
            _unitMotor.Resume();
            Vector3 targetPosition = target.transform.position;
            float refreshDistance = Mathf.Max(
                Mathf.Epsilon,
                _unitMotor is IDestinationRefreshPolicy refreshPolicy
                    ? refreshPolicy.DestinationRefreshDistance
                    : 0f);
            bool targetMovedMeaningfully = !_hasRequestedDestination ||
                CombatRangeRules.GetSquaredPlanarDistance(
                    _lastRequestedTargetPosition,
                    targetPosition) >= refreshDistance * refreshDistance;
            if (_destinationRefreshRemaining > 0f &&
                !targetMovedMeaningfully)
            {
                return;
            }

            _unitMotor.MoveTo(targetPosition);
            _lastRequestedTargetPosition = targetPosition;
            _hasRequestedDestination = true;
            _destinationRefreshRemaining =
                _targetingController.ScanInterval;
        }

        private void EnterAttack(UnitController target)
        {
            _unitMotor.Stop();
            _unitMotor.FaceTowards(target.transform.position);
            _hasRequestedDestination = false;
            SetState(AIUnitState.Attack);
            if (HasRuntimeServices)
            {
                _attackController.TryStartAttack();
            }
        }

        private void EnterDisabled(bool clearTarget)
        {
            if (clearTarget)
            {
                _targetingController?.ClearCurrentTarget();
            }

            _unitMotor?.Stop();
            _hasRequestedDestination = false;
            SetState(AIUnitState.Disabled);
        }

        private bool IsTargetValid(UnitController target)
        {
            return target != null && target.IsActive &&
                   target.HealthController != null &&
                   target.HealthController.IsAlive &&
                   FactionRules.AreHostile(
                       _unitController.Faction,
                       target.Faction) &&
                   _unitController.Definition is AIUnitDefinition definition &&
                   CombatRangeRules.IsWithinRange(
                       transform.position,
                       target.transform.position,
                       definition.ChaseRange);
        }

        private void SetState(AIUnitState state)
        {
            if (State == state)
            {
                return;
            }

            AIUnitState previousState = State;
            State = state;
            StateChanged?.Invoke(new AIUnitStateChangedEvent(
                _unitController,
                previousState,
                State));
        }

        private void HandleStatusEffectChanged(
            StatusEffectChangedEvent statusEffectEvent)
        {
            if (statusEffectEvent.Unit == _unitController &&
                statusEffectEvent.EffectType == StatusEffectType.Stun &&
                statusEffectEvent.IsActive)
            {
                EnterDisabled(true);
            }
        }

        private void HandleTargetLost(TargetingEvent targetingEvent)
        {
            if (targetingEvent.Source == _unitController &&
                _unitController.IsActive &&
                !_statusEffectController.IsChaseBlocked)
            {
                EnterIdle();
            }
        }

        private void HandleUnitDying(
            UnitLifecycleChangedEvent lifecycleEvent)
        {
            if (lifecycleEvent.Unit == _unitController)
            {
                EnterDisabled(true);
            }
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
            _targetingController = GetComponent<TargetingController>();
            _attackController = GetComponent<AttackController>();
            _statusEffectController = GetComponent<StatusEffectController>();
            _lifecycleController = GetComponent<UnitLifecycleController>();
            _meleeAttackExecutor = GetComponent<MeleeAttackExecutor>();
            _projectileAttackExecutor =
                GetComponent<ProjectileAttackExecutor>();
            _unitController?.CacheSiblingComponents();
            _unitMotor = _unitController?.UnitMotor;
        }

        private void EnsurePermanentSubscriptions()
        {
            ReleasePermanentSubscriptions();
            if (_statusEffectController != null)
            {
                _statusEffectController.StatusEffectChanged +=
                    HandleStatusEffectChanged;
            }

            if (_targetingController != null)
            {
                _targetingController.TargetLost += HandleTargetLost;
            }

            if (_lifecycleController != null)
            {
                _lifecycleController.Dying += HandleUnitDying;
            }
        }

        private void ReleasePermanentSubscriptions()
        {
            if (_statusEffectController != null)
            {
                _statusEffectController.StatusEffectChanged -=
                    HandleStatusEffectChanged;
            }

            if (_targetingController != null)
            {
                _targetingController.TargetLost -= HandleTargetLost;
            }

            if (_lifecycleController != null)
            {
                _lifecycleController.Dying -= HandleUnitDying;
            }
        }
    }
}
