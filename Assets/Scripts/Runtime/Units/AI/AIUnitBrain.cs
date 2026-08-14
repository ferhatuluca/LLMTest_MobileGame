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

    /// <summary>
    /// Chooses between idling, chasing, and attacking for an AI unit.
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
        private IUnitMotor _unitMotor;
        private NavMeshUnitMotor _navMeshMotor;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;
        private float _destinationRefreshRemaining;
        private Vector3 _lastDestination;

        public AIUnitState State { get; private set; } = AIUnitState.Disabled;
        public bool HasRuntimeServices { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void Update()
        {
            AdvanceDecision(Time.deltaTime);
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheComponents();
            bool isValid = _unitController != null &&
                           _targetingController != null &&
                           _attackController != null &&
                           _statusEffectController != null &&
                           _unitMotor != null &&
                           _unitController.Definition is AIUnitDefinition definition &&
                           definition.Validate().IsValid;
            failureMessage = isValid
                ? string.Empty
                : "AIUnitBrain requires a valid AI definition and its core unit components.";
            return isValid;
        }

        public bool ConfigureRuntimeServices(InteractionSystem interactionSystem)
        {
            return ConfigureRuntimeServices(null, interactionSystem);
        }

        public bool ConfigureRuntimeServices(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem)
        {
            CacheComponents();
            if (interactionSystem == null ||
                !(_unitController?.Definition is AIUnitDefinition definition))
            {
                HasRuntimeServices = false;
                return false;
            }

            AttackDeliveryType delivery =
                definition.DefaultAttackDefinition.DeliveryType;
            if (delivery == AttackDeliveryType.Melee)
            {
                MeleeAttackExecutor executor =
                    GetComponent<MeleeAttackExecutor>();
                HasRuntimeServices = executor != null &&
                                     executor.Configure(interactionSystem);
            }
            else if (delivery == AttackDeliveryType.Projectile)
            {
                ProjectileAttackExecutor executor =
                    GetComponent<ProjectileAttackExecutor>();
                HasRuntimeServices = spawnManager != null && executor != null &&
                    executor.Configure(
                        spawnManager,
                        interactionSystem,
                        executor.AttackOrigin);
            }
            else
            {
                HasRuntimeServices = false;
            }

            SpawnUnitsOnDeath deathSpawn = GetComponent<SpawnUnitsOnDeath>();
            if (HasRuntimeServices && deathSpawn != null)
            {
                HasRuntimeServices = deathSpawn.ConfigureRuntimeServices(
                    spawnManager,
                    interactionSystem);
            }

            return HasRuntimeServices;
        }

        public bool PrepareForSpawn()
        {
            CacheComponents();
            ResetState();
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
            ResetState();
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
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
                _statusEffectController.IsChaseBlocked)
            {
                EnterDisabled();
                return;
            }

            UnitController target = _targetingController.CurrentTarget;
            if (!IsTargetValid(target))
            {
                _targetingController.ClearCurrentTarget();
                _unitMotor.Stop();
                State = AIUnitState.Idle;
                return;
            }

            AIUnitDefinition definition =
                (AIUnitDefinition)_unitController.Definition;
            if (CombatRangeRules.IsWithinRange(
                    transform.position,
                    target.transform.position,
                    definition.DefaultAttackDefinition.AttackRange))
            {
                _unitMotor.Stop();
                _unitMotor.FaceTowards(target.transform.position);
                State = AIUnitState.Attack;
                if (HasRuntimeServices)
                {
                    _attackController.TryStartAttack();
                }

                return;
            }

            State = AIUnitState.Chase;
            _unitMotor.Resume();
            Vector3 targetPosition = target.transform.position;
            float refreshDistance = _navMeshMotor == null
                ? 0f
                : _navMeshMotor.DestinationRefreshDistance;
            bool targetMoved = CombatRangeRules.GetSquaredPlanarDistance(
                _lastDestination,
                targetPosition) >= refreshDistance * refreshDistance;
            if (_destinationRefreshRemaining <= 0f || targetMoved)
            {
                _unitMotor.MoveTo(targetPosition);
                _lastDestination = targetPosition;
                _destinationRefreshRemaining = _targetingController.ScanInterval;
            }
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

        private void EnterDisabled()
        {
            _targetingController?.ClearCurrentTarget();
            _unitMotor?.Stop();
            State = AIUnitState.Disabled;
        }

        private void ResetState()
        {
            _targetingController?.ClearCurrentTarget();
            _unitMotor?.Stop();
            _destinationRefreshRemaining = 0f;
            _lastDestination = transform.position;
            HasRuntimeServices = false;
            State = AIUnitState.Disabled;
        }

        private void CacheComponents()
        {
            _unitController = GetComponent<UnitController>();
            _targetingController = GetComponent<TargetingController>();
            _attackController = GetComponent<AttackController>();
            _statusEffectController = GetComponent<StatusEffectController>();
            _navMeshMotor = GetComponent<NavMeshUnitMotor>();
            _unitController?.CacheSiblingComponents();
            _unitMotor = _unitController?.UnitMotor;
        }
    }
}
