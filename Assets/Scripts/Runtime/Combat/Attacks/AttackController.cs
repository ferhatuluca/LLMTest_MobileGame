using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    public enum AttackTimingState
    {
        Idle,
        Windup,
        Recovery
    }

    public readonly struct AttackTimingEvent
    {
        public UnitController Source { get; }
        public UnitController Target { get; }
        public AttackKey AttackKey { get; }
        public AttackTimingState State { get; }

        public AttackTimingEvent(
            UnitController source,
            UnitController target,
            AttackKey attackKey,
            AttackTimingState state)
        {
            Source = source;
            Target = target;
            AttackKey = attackKey;
            State = state;
        }
    }

    public readonly struct AttackImpactEvent
    {
        public AttackExecutionContext ExecutionContext { get; }
        public InteractionResult InteractionResult { get; }

        public AttackImpactEvent(
            AttackExecutionContext executionContext,
            InteractionResult interactionResult)
        {
            ExecutionContext = executionContext;
            InteractionResult = interactionResult;
        }
    }

    [DisallowMultipleComponent]
    public sealed class AttackController : MonoBehaviour, IPoolable
    {
        private readonly AttackHitLedger _hitLedger = new AttackHitLedger();
        private readonly List<IAttackResultPolicy> _resultPolicies =
            new List<IAttackResultPolicy>();

        [SerializeField] private AttackExecutorBinding[] _executorBindings =
            Array.Empty<AttackExecutorBinding>();
        [field: SerializeField] public AttackDefinition AttackDefinition { get; private set; }

        private UnitController _unitController;
        private TargetingController _targetingController;
        private StatusEffectController _statusEffectController;
        private UnitLifecycleController _lifecycleController;
        private IAttackExecutor _activeExecutor;
        private UnitController _attackTarget;
        private SpawnId _attackTargetSpawnId;
        private long _lastAttackSequence;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;
        private bool _hasImpacted;

        public event Action<AttackTimingEvent> AttackStarted;
        public event Action<AttackTimingEvent> AttackCancelled;
        public event Action<AttackTimingEvent> RecoveryCompleted;
        public event Action<AttackImpactEvent> ImpactResolved;

        public AttackTimingState State { get; private set; }
        public AttackKey ActiveAttackKey { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float WindupRemaining { get; private set; }
        public float RecoveryRemaining { get; private set; }
        public bool HasActiveSequence => ActiveAttackKey.IsValid;
        public AttackHitLedger HitLedger => _hitLedger;

        private void Awake()
        {
            CacheSiblingComponents();
            CacheResultPolicies();
            EnsurePermanentSubscriptions();
        }

        private void OnValidate()
        {
            CacheSiblingComponents();
            CacheResultPolicies();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        private void OnDestroy()
        {
            ReleasePermanentSubscriptions();
        }

        public bool SetAttackDefinition(AttackDefinition attackDefinition)
        {
            if (State != AttackTimingState.Idle ||
                (attackDefinition != null && !attackDefinition.Validate().IsValid))
            {
                return false;
            }

            AttackDefinition previousDefinition = AttackDefinition;
            IAttackExecutor previousExecutor = _activeExecutor;
            AttackDefinition = attackDefinition;
            if (!ResolveActiveExecutor(out _))
            {
                AttackDefinition = previousDefinition;
                _activeExecutor = previousExecutor;
                return false;
            }

            if (_unitController != null &&
                _unitController.Definition is PlayerUnitDefinition &&
                AttackDefinition != null &&
                _targetingController != null &&
                !_targetingController.SetPlayerAttackRange(
                    AttackDefinition.AttackRange))
            {
                AttackDefinition = previousDefinition;
                _activeExecutor = previousExecutor;
                return false;
            }

            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheSiblingComponents();
            if (_unitController == null ||
                _targetingController == null ||
                _statusEffectController == null ||
                _lifecycleController == null)
            {
                failureMessage =
                    "AttackController requires UnitController, TargetingController, " +
                    "StatusEffectController, and UnitLifecycleController siblings.";
                return false;
            }

            if (AttackDefinition == null)
            {
                failureMessage = string.Empty;
                return true;
            }

            if (!AttackDefinition.Validate().IsValid)
            {
                failureMessage = "AttackController has an invalid AttackDefinition.";
                return false;
            }

            HashSet<AttackDeliveryType> seenDeliveries =
                new HashSet<AttackDeliveryType>();
            int matchingExecutorCount = 0;
            AttackExecutorBinding[] bindings =
                _executorBindings ?? Array.Empty<AttackExecutorBinding>();
            foreach (AttackExecutorBinding binding in bindings)
            {
                if (binding == null ||
                    !Enum.IsDefined(
                        typeof(AttackDeliveryType),
                        binding.DeliveryType) ||
                    binding.DeliveryType == AttackDeliveryType.Unspecified ||
                    binding.Executor == null ||
                    binding.Executor.DeliveryType != binding.DeliveryType ||
                    !seenDeliveries.Add(binding.DeliveryType))
                {
                    failureMessage =
                        "AttackController has a missing, duplicate, or incompatible executor binding.";
                    return false;
                }

                if (binding.DeliveryType == AttackDefinition.DeliveryType)
                {
                    matchingExecutorCount++;
                }
            }

            if (matchingExecutorCount != 1)
            {
                failureMessage =
                    "AttackController requires exactly one executor matching its delivery type.";
                return false;
            }

            if (_unitController.Definition is AIUnitDefinition && bindings.Length != 1)
            {
                failureMessage =
                    "A fixed AI unit must bind only its configured attack executor.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool ValidateExecutorForDefinition(
            AttackDefinition attackDefinition,
            out string failureMessage)
        {
            if (attackDefinition == null || !attackDefinition.Validate().IsValid)
            {
                failureMessage = "A valid attack definition is required.";
                return false;
            }

            int matchingExecutorCount = 0;
            AttackExecutorBinding[] bindings =
                _executorBindings ?? Array.Empty<AttackExecutorBinding>();
            foreach (AttackExecutorBinding binding in bindings)
            {
                if (binding == null ||
                    !Enum.IsDefined(
                        typeof(AttackDeliveryType),
                        binding.DeliveryType) ||
                    binding.DeliveryType == AttackDeliveryType.Unspecified ||
                    binding.Executor == null ||
                    binding.Executor.DeliveryType != binding.DeliveryType)
                {
                    failureMessage =
                        "An attack executor binding is missing or incompatible.";
                    return false;
                }

                if (binding.DeliveryType == attackDefinition.DeliveryType)
                {
                    matchingExecutorCount++;
                }
            }

            if (matchingExecutorCount != 1)
            {
                failureMessage =
                    $"Attack delivery '{attackDefinition.DeliveryType}' requires exactly one compatible executor.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool TryStartAttack()
        {
            CacheSiblingComponents();
            if (!_isPreparedForSpawn || !_isActivationComplete ||
                State != AttackTimingState.Idle || CooldownRemaining > 0f ||
                AttackDefinition == null || _activeExecutor == null ||
                _unitController == null || !_unitController.IsActive ||
                _statusEffectController == null ||
                _statusEffectController.IsAttackBlocked ||
                _targetingController == null ||
                !_targetingController.IsCurrentTargetWithinRange(
                    AttackDefinition.AttackRange))
            {
                return false;
            }

            _attackTarget = _targetingController.CurrentTarget;
            _attackTargetSpawnId = _attackTarget.SpawnId;
            _lastAttackSequence = checked(_lastAttackSequence + 1);
            ActiveAttackKey = new AttackKey(
                _unitController.SpawnId,
                new AttackSequenceId(_lastAttackSequence));
            _hitLedger.BeginAttack(ActiveAttackKey);
            CooldownRemaining = AttackDefinition.CooldownDuration;
            WindupRemaining = AttackDefinition.WindupDuration;
            RecoveryRemaining = 0f;
            _hasImpacted = false;
            State = AttackTimingState.Windup;
            _unitController.UnitMotor?.FaceTowards(_attackTarget.transform.position);
            AttackStarted?.Invoke(CreateTimingEvent(State));

            if (WindupRemaining <= 0f)
            {
                RequestImpact();
            }

            return true;
        }

        public bool RequestImpact()
        {
            if (State != AttackTimingState.Windup || _hasImpacted ||
                !CanContinueWindup(false))
            {
                if (State == AttackTimingState.Windup)
                {
                    CancelActiveAttack(false);
                }

                return false;
            }

            if (AttackDefinition.DeliveryType == AttackDeliveryType.Melee &&
                !CombatRangeRules.IsWithinRange(
                    transform.position,
                    _attackTarget.transform.position,
                    AttackDefinition.AttackRange))
            {
                BeginRecovery(default);
                return false;
            }

            _hasImpacted = true;
            AttackExecutionContext executionContext =
                new AttackExecutionContext(
                    _unitController,
                    _attackTarget,
                    _targetingController.CurrentTargetPoint,
                    AttackDefinition,
                    ActiveAttackKey,
                    _hitLedger);
            InteractionResult interactionResult =
                _activeExecutor.ExecuteImpact(executionContext);
            if (interactionResult.IsApplied)
            {
                foreach (IAttackResultPolicy resultPolicy in _resultPolicies)
                {
                    resultPolicy.HandleSuccessfulInteraction(
                        executionContext,
                        interactionResult);
                }
            }

            BeginRecovery(new AttackImpactEvent(
                executionContext,
                interactionResult));
            return true;
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            CacheResultPolicies();
            EnsurePermanentSubscriptions();
            ResetAllTiming();
            _isPreparedForSpawn = false;
            _isActivationComplete = false;

            if (_unitController == null ||
                _unitController.Definition == null ||
                _targetingController == null ||
                _statusEffectController == null ||
                _lifecycleController == null)
            {
                return false;
            }

            if (_unitController.Definition is AIUnitDefinition aiDefinition)
            {
                AttackDefinition = aiDefinition.DefaultAttackDefinition;
            }

            if (!ResolveActiveExecutor(out _) ||
                !ValidateConfiguration(out _))
            {
                return false;
            }

            if (_unitController.Definition is PlayerUnitDefinition &&
                AttackDefinition != null &&
                !_targetingController.SetPlayerAttackRange(
                    AttackDefinition.AttackRange))
            {
                return false;
            }

            _isPreparedForSpawn = true;
            return true;
        }

        public bool CompleteSpawn()
        {
            _isActivationComplete =
                _isPreparedForSpawn && gameObject.activeInHierarchy;
            return _isActivationComplete;
        }

        public void PrepareForReturn()
        {
            ResetAllTiming();
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
        }

        internal void ConfigureBindings(
            AttackDefinition attackDefinition,
            AttackExecutorBinding[] executorBindings)
        {
            AttackDefinition = attackDefinition;
            _executorBindings = executorBindings == null
                ? Array.Empty<AttackExecutorBinding>()
                : (AttackExecutorBinding[])executorBindings.Clone();
            ResolveActiveExecutor(out _);
        }

        internal void AdvanceTime(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    "Attack timing delta must be non-negative and finite.");
            }

            if (_unitController == null || !_unitController.IsActive)
            {
                return;
            }

            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
            if (State == AttackTimingState.Windup)
            {
                if (!CanContinueWindup(true))
                {
                    CancelActiveAttack(false);
                    return;
                }

                WindupRemaining = Mathf.Max(0f, WindupRemaining - deltaTime);
                if (WindupRemaining <= 0f)
                {
                    RequestImpact();
                }
            }
            else if (State == AttackTimingState.Recovery)
            {
                RecoveryRemaining = Mathf.Max(0f, RecoveryRemaining - deltaTime);
                if (RecoveryRemaining <= 0f)
                {
                    CompleteRecovery();
                }
            }
        }

        private bool ResolveActiveExecutor(out string failureMessage)
        {
            _activeExecutor = null;
            if (AttackDefinition == null)
            {
                failureMessage = string.Empty;
                return true;
            }

            AttackExecutorBinding[] bindings =
                _executorBindings ?? Array.Empty<AttackExecutorBinding>();
            foreach (AttackExecutorBinding binding in bindings)
            {
                if (binding == null ||
                    binding.DeliveryType != AttackDefinition.DeliveryType)
                {
                    continue;
                }

                if (_activeExecutor != null)
                {
                    _activeExecutor = null;
                    failureMessage = "Duplicate executor binding.";
                    return false;
                }

                _activeExecutor = binding.Executor;
            }

            if (_activeExecutor == null ||
                _activeExecutor.DeliveryType != AttackDefinition.DeliveryType)
            {
                _activeExecutor = null;
                failureMessage = "Missing or incompatible executor binding.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        private bool CanContinueWindup(bool requireAttackRange)
        {
            return _unitController != null &&
                   _unitController.IsActive &&
                   _statusEffectController != null &&
                   !_statusEffectController.IsAttackBlocked &&
                   _attackTarget != null &&
                   _attackTargetSpawnId.IsValid &&
                   _attackTarget.SpawnId == _attackTargetSpawnId &&
                   _targetingController != null &&
                   _targetingController.CurrentTarget == _attackTarget &&
                   (!requireAttackRange ||
                    _targetingController.IsCurrentTargetWithinRange(
                        AttackDefinition.AttackRange));
        }

        private void BeginRecovery(AttackImpactEvent impactEvent)
        {
            _hasImpacted = true;
            WindupRemaining = 0f;
            RecoveryRemaining = AttackDefinition.RecoveryDuration;
            State = AttackTimingState.Recovery;
            if (impactEvent.ExecutionContext.AttackKey.IsValid)
            {
                ImpactResolved?.Invoke(impactEvent);
            }

            if (RecoveryRemaining <= 0f)
            {
                CompleteRecovery();
            }
        }

        private void CompleteRecovery()
        {
            AttackTimingEvent timingEvent = CreateTimingEvent(
                AttackTimingState.Idle);
            State = AttackTimingState.Idle;
            RecoveryRemaining = 0f;
            ClearActiveSequence();
            RecoveryCompleted?.Invoke(timingEvent);
        }

        private void CancelActiveAttack(bool resetCommittedTiming)
        {
            if (State != AttackTimingState.Windup)
            {
                return;
            }

            AttackTimingEvent timingEvent = CreateTimingEvent(
                AttackTimingState.Idle);
            State = AttackTimingState.Idle;
            WindupRemaining = 0f;
            RecoveryRemaining = 0f;
            if (resetCommittedTiming)
            {
                CooldownRemaining = 0f;
            }

            ClearActiveSequence();
            AttackCancelled?.Invoke(timingEvent);
        }

        private void ResetAllTiming()
        {
            State = AttackTimingState.Idle;
            CooldownRemaining = 0f;
            WindupRemaining = 0f;
            RecoveryRemaining = 0f;
            _lastAttackSequence = 0;
            ClearActiveSequence();
        }

        private void ClearActiveSequence()
        {
            _hitLedger.Reset();
            ActiveAttackKey = default;
            _attackTarget = null;
            _attackTargetSpawnId = default;
            _hasImpacted = false;
        }

        private AttackTimingEvent CreateTimingEvent(AttackTimingState state)
        {
            return new AttackTimingEvent(
                _unitController,
                _attackTarget,
                ActiveAttackKey,
                state);
        }

        private void HandleTargetLost(TargetingEvent targetingEvent)
        {
            if (State == AttackTimingState.Windup &&
                targetingEvent.Target == _attackTarget &&
                targetingEvent.TargetSpawnId == _attackTargetSpawnId)
            {
                CancelActiveAttack(false);
            }
        }

        private void HandleStatusEffectChanged(
            StatusEffectChangedEvent statusEffectEvent)
        {
            if (statusEffectEvent.EffectType == StatusEffectType.Stun &&
                statusEffectEvent.IsActive)
            {
                CancelActiveAttack(false);
            }
        }

        private void HandleDying(UnitLifecycleChangedEvent lifecycleEvent)
        {
            ResetAllTiming();
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
            _targetingController = GetComponent<TargetingController>();
            _statusEffectController = GetComponent<StatusEffectController>();
            _lifecycleController = GetComponent<UnitLifecycleController>();
        }

        private void CacheResultPolicies()
        {
            _resultPolicies.Clear();
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != this &&
                    behaviour is IAttackResultPolicy resultPolicy)
                {
                    _resultPolicies.Add(resultPolicy);
                }
            }
        }

        private void EnsurePermanentSubscriptions()
        {
            ReleasePermanentSubscriptions();
            if (_targetingController != null)
            {
                _targetingController.TargetLost += HandleTargetLost;
            }

            if (_statusEffectController != null)
            {
                _statusEffectController.StatusEffectChanged +=
                    HandleStatusEffectChanged;
            }

            if (_lifecycleController != null)
            {
                _lifecycleController.Dying += HandleDying;
            }
        }

        private void ReleasePermanentSubscriptions()
        {
            if (_targetingController != null)
            {
                _targetingController.TargetLost -= HandleTargetLost;
            }

            if (_statusEffectController != null)
            {
                _statusEffectController.StatusEffectChanged -=
                    HandleStatusEffectChanged;
            }

            if (_lifecycleController != null)
            {
                _lifecycleController.Dying -= HandleDying;
            }
        }
    }
}
