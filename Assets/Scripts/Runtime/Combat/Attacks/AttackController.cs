using System;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
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

    /// <summary>
    /// Owns attack cooldown, windup, impact, and recovery.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackController : MonoBehaviour, IPoolable
    {
        private readonly AttackHitLedger _hitLedger = new AttackHitLedger();

        [field: SerializeField] public AttackDefinition AttackDefinition { get; private set; }

        private UnitController _unitController;
        private TargetingController _targetingController;
        private IAttackExecutor _activeExecutor;
        private StunnerHitPolicy _stunnerHitPolicy;
        private UnitController _attackTarget;
        private SpawnId _attackTargetSpawnId;
        private long _lastAttackSequence;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;
        private bool _hasImpacted;

        public AttackTimingState State { get; private set; }
        public AttackKey ActiveAttackKey { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float WindupRemaining { get; private set; }
        public float RecoveryRemaining { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        public bool SetAttackDefinition(AttackDefinition attackDefinition)
        {
            if (State != AttackTimingState.Idle ||
                (attackDefinition != null &&
                 !attackDefinition.Validate().IsValid))
            {
                return false;
            }

            AttackDefinition previousDefinition = AttackDefinition;
            IAttackExecutor previousExecutor = _activeExecutor;
            AttackDefinition = attackDefinition;
            if (!ResolveExecutor(out _))
            {
                AttackDefinition = previousDefinition;
                _activeExecutor = previousExecutor;
                return false;
            }

            if (_unitController?.Definition is PlayerUnitDefinition &&
                AttackDefinition != null &&
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
            CacheComponents();
            if (_unitController == null || _targetingController == null)
            {
                failureMessage =
                    "AttackController requires UnitController and TargetingController siblings.";
                return false;
            }

            if (AttackDefinition == null)
            {
                failureMessage = string.Empty;
                return true;
            }

            return ValidateExecutorForDefinition(
                AttackDefinition,
                out failureMessage);
        }

        public bool ValidateExecutorForDefinition(
            AttackDefinition attackDefinition,
            out string failureMessage)
        {
            if (attackDefinition == null ||
                !attackDefinition.Validate().IsValid)
            {
                failureMessage = "A valid attack definition is required.";
                return false;
            }

            int matchCount = CountExecutors(attackDefinition.DeliveryType);
            failureMessage = matchCount == 1
                ? string.Empty
                : $"Attack delivery '{attackDefinition.DeliveryType}' requires exactly one executor component.";
            return matchCount == 1;
        }

        public bool TryStartAttack()
        {
            if (!_isPreparedForSpawn || !_isActivationComplete ||
                State != AttackTimingState.Idle || CooldownRemaining > 0f ||
                AttackDefinition == null || _activeExecutor == null ||
                _unitController == null || !_unitController.IsActive ||
                _unitController.StatusEffectController.IsAttackBlocked ||
                !_targetingController.IsCurrentTargetWithinRange(
                    AttackDefinition.AttackRange))
            {
                return false;
            }

            _attackTarget = _targetingController.CurrentTarget;
            _attackTargetSpawnId = _attackTarget.SpawnId;
            _lastAttackSequence++;
            ActiveAttackKey = new AttackKey(
                _unitController.SpawnId,
                new AttackSequenceId(_lastAttackSequence));
            _hitLedger.BeginAttack(ActiveAttackKey);
            CooldownRemaining = AttackDefinition.CooldownDuration;
            WindupRemaining = AttackDefinition.WindupDuration;
            RecoveryRemaining = 0f;
            _hasImpacted = false;
            State = AttackTimingState.Windup;
            _unitController.UnitMotor?.FaceTowards(
                _attackTarget.transform.position);
            if (WindupRemaining <= 0f)
            {
                RequestImpact();
            }

            return true;
        }

        public bool RequestImpact()
        {
            if (State != AttackTimingState.Windup || _hasImpacted ||
                !CanContinueAttack())
            {
                CancelAttack();
                return false;
            }

            if (AttackDefinition.DeliveryType == AttackDeliveryType.Melee &&
                !_targetingController.IsCurrentTargetWithinRange(
                    AttackDefinition.AttackRange))
            {
                BeginRecovery();
                return false;
            }

            _hasImpacted = true;
            Vector3 targetPoint = _targetingController.CurrentTargetPoint;
            AttackExecutionContext context = new AttackExecutionContext(
                _unitController,
                _attackTarget,
                targetPoint,
                AttackDefinition,
                ActiveAttackKey,
                _hitLedger);
            DamagePayload payload = AttackPayloadFactory.Create(context);
            if (_stunnerHitPolicy != null)
            {
                payload = _stunnerHitPolicy.PreparePayload(context, payload);
            }

            context = new AttackExecutionContext(
                _unitController,
                _attackTarget,
                targetPoint,
                AttackDefinition,
                ActiveAttackKey,
                _hitLedger,
                payload);
            InteractionResult result = _activeExecutor.ExecuteImpact(context);
            _stunnerHitPolicy?.RecordResult(result);
            BeginRecovery();
            return true;
        }

        public bool PrepareForSpawn()
        {
            CacheComponents();
            ResetTiming();
            _isPreparedForSpawn = false;
            _isActivationComplete = false;

            if (_unitController?.Definition is AIUnitDefinition aiDefinition)
            {
                AttackDefinition = aiDefinition.DefaultAttackDefinition;
            }

            if (!ValidateConfiguration(out _) || !ResolveExecutor(out _))
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
            ResetTiming();
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
        }

        internal void AdvanceTime(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (_unitController == null || !_unitController.IsActive)
            {
                return;
            }

            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
            if (State == AttackTimingState.Windup)
            {
                if (!CanContinueAttack())
                {
                    CancelAttack();
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
                RecoveryRemaining = Mathf.Max(
                    0f,
                    RecoveryRemaining - deltaTime);
                if (RecoveryRemaining <= 0f)
                {
                    State = AttackTimingState.Idle;
                    ClearSequence();
                }
            }
        }

        private bool ResolveExecutor(out string failureMessage)
        {
            _activeExecutor = null;
            if (AttackDefinition == null)
            {
                failureMessage = string.Empty;
                return true;
            }

            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is IAttackExecutor executor &&
                    executor.DeliveryType == AttackDefinition.DeliveryType)
                {
                    if (_activeExecutor != null)
                    {
                        failureMessage = "Duplicate attack executor component.";
                        _activeExecutor = null;
                        return false;
                    }

                    _activeExecutor = executor;
                }
            }

            failureMessage = _activeExecutor == null
                ? "Missing attack executor component."
                : string.Empty;
            return _activeExecutor != null;
        }

        private int CountExecutors(AttackDeliveryType deliveryType)
        {
            int count = 0;
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is IAttackExecutor executor &&
                    executor.DeliveryType == deliveryType)
                {
                    count++;
                }
            }

            return count;
        }

        private bool CanContinueAttack()
        {
            return _unitController != null && _unitController.IsActive &&
                   !_unitController.StatusEffectController.IsAttackBlocked &&
                   _attackTarget != null &&
                   _attackTarget.SpawnId == _attackTargetSpawnId &&
                   _targetingController.CurrentTarget == _attackTarget;
        }

        private void BeginRecovery()
        {
            WindupRemaining = 0f;
            RecoveryRemaining = AttackDefinition.RecoveryDuration;
            State = AttackTimingState.Recovery;
            if (RecoveryRemaining <= 0f)
            {
                State = AttackTimingState.Idle;
                ClearSequence();
            }
        }

        private void CancelAttack()
        {
            if (State != AttackTimingState.Windup)
            {
                return;
            }

            State = AttackTimingState.Idle;
            WindupRemaining = 0f;
            RecoveryRemaining = 0f;
            ClearSequence();
        }

        private void ResetTiming()
        {
            State = AttackTimingState.Idle;
            CooldownRemaining = 0f;
            WindupRemaining = 0f;
            RecoveryRemaining = 0f;
            _lastAttackSequence = 0;
            ClearSequence();
        }

        private void ClearSequence()
        {
            _hitLedger.Reset();
            ActiveAttackKey = default;
            _attackTarget = null;
            _attackTargetSpawnId = default;
            _hasImpacted = false;
        }

        private void CacheComponents()
        {
            _unitController = GetComponent<UnitController>();
            _targetingController = GetComponent<TargetingController>();
            _stunnerHitPolicy = GetComponent<StunnerHitPolicy>();
        }
    }
}
