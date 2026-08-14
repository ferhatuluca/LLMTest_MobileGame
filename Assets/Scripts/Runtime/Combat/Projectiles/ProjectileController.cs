using System;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Projectiles
{
    /// <summary>
    /// Owns the pooled projectile lifecycle, captured immutable damage payload,
    /// per-attack hit ledger, motion adapter, hit resolution, and pool return.
    /// A projectile is colored from the captured source faction before it is
    /// activated, so reused bullets and grenades never retain an old owner tint.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PooledEntity))]
    public sealed class ProjectileController : MonoBehaviour,
        IPoolable,
        IProjectileSpawnLifecycle,
        IProjectileSpawnRuntimeContextReceiver
    {
        private readonly AttackHitLedger _hitLedger = new AttackHitLedger();

        private PooledEntity _pooledEntity;
        private IProjectileMotion _projectileMotion;
        private SpawnManager _spawnManager;
        private InteractionSystem _interactionSystem;
        private ProjectileSpawnRequest _spawnRequest;
        private float _elapsedTime;
        private bool _isConfigured;
        private bool _isPreparedForSpawn;
        private Renderer[] _factionRenderers;
        private MaterialPropertyBlock _factionPropertyBlock;

        public event Action<ProjectileTerminationEvent> Terminated;

        public ProjectileDefinition Definition => _spawnRequest.Definition;
        public DamagePayload DamagePayload => _spawnRequest.DamagePayload;
        public AttackHitLedger HitLedger => _hitLedger;
        public bool IsRunning { get; private set; }
        public float ElapsedTime => _elapsedTime;

        private void Awake()
        {
            CacheComponents();
            CacheFactionVisuals();
        }

        private void OnValidate()
        {
            CacheComponents();
            CacheFactionVisuals();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        public bool ConfigureProjectileRuntime(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem)
        {
            if (gameObject.activeInHierarchy || spawnManager == null ||
                interactionSystem == null)
            {
                return false;
            }

            _spawnManager = spawnManager;
            _interactionSystem = interactionSystem;
            return true;
        }

        public bool ConfigureProjectileSpawn(ProjectileSpawnRequest spawnRequest)
        {
            CacheComponents();
            if (gameObject.activeInHierarchy || _spawnManager == null ||
                _interactionSystem == null || !spawnRequest.IsValid ||
                _projectileMotion == null ||
                _projectileMotion.DeliveryType !=
                    spawnRequest.Definition.CompatibleDeliveryType)
            {
                return false;
            }

            _spawnRequest = spawnRequest;
            _isConfigured = true;
            ApplyFactionVisuals(spawnRequest.DamagePayload.SourceFaction);
            return true;
        }

        public bool PrepareForSpawn()
        {
            CacheComponents();
            _hitLedger.Reset();
            _projectileMotion?.ResetMotion();
            _elapsedTime = 0f;
            IsRunning = false;
            _isPreparedForSpawn = false;
            if (!_isConfigured || _pooledEntity == null ||
                _projectileMotion == null ||
                !_projectileMotion.ValidateConfiguration(out _) ||
                !_projectileMotion.PrepareMotion(this, Definition))
            {
                return false;
            }

            _isPreparedForSpawn = true;
            return true;
        }

        public bool CompleteSpawn()
        {
            return _isPreparedForSpawn && gameObject.activeInHierarchy;
        }

        public bool StartProjectile()
        {
            if (!_isPreparedForSpawn || !gameObject.activeInHierarchy ||
                IsRunning)
            {
                _hitLedger.Reset();
                return false;
            }

            _hitLedger.BeginAttack(DamagePayload.AttackKey);
            if (!_projectileMotion.StartMotion())
            {
                _hitLedger.Reset();
                return false;
            }

            IsRunning = true;
            return true;
        }

        public void PrepareForReturn()
        {
            IsRunning = false;
            _projectileMotion?.ResetMotion();
            _hitLedger.Reset();
            _spawnRequest = default;
            _spawnManager = null;
            _interactionSystem = null;
            _elapsedTime = 0f;
            _isConfigured = false;
            _isPreparedForSpawn = false;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheComponents();
            if (_pooledEntity == null || _projectileMotion == null)
            {
                failureMessage =
                    "ProjectileController requires one PooledEntity and one projectile motion component.";
                return false;
            }

            return _projectileMotion.ValidateConfiguration(out failureMessage);
        }

        internal void AdvanceTime(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    "Projectile time must be non-negative and finite.");
            }

            if (!IsRunning)
            {
                return;
            }

            _projectileMotion.AdvanceTime(deltaTime);
            if (!IsRunning)
            {
                return;
            }

            _elapsedTime += deltaTime;
            float timerDuration = Definition.CompatibleDeliveryType ==
                AttackDeliveryType.Grenade
                ? Definition.FuseDuration
                : Definition.MaximumLifetime;
            if (_elapsedTime >= timerDuration)
            {
                _projectileMotion.HandleTimerExpired();
            }
        }

        internal InteractionResult ResolveDirectHit(
            DamageTargetProxy targetProxy,
            Vector3 position,
            Vector3 normal,
            string deliveryIdentifier)
        {
            if (!IsRunning || targetProxy == null ||
                _interactionSystem == null)
            {
                return InteractionResult.CreateRejected(
                    InteractionOutcome.InvalidTarget,
                    DamagePayload.AttackKey,
                    targetProxy == null ? default : targetProxy.SpawnId);
            }

            HitContext hitContext = new HitContext(
                DamagePayload,
                targetProxy.DamageController,
                position,
                normal,
                HitType.Direct,
                deliveryIdentifier);
            return _interactionSystem.ResolveHit(hitContext, _hitLedger);
        }

        internal InteractionResult ResolveAreaHit(
            DamageTargetProxy targetProxy,
            Vector3 explosionPosition,
            string deliveryIdentifier)
        {
            Vector3 normal = targetProxy.transform.position - explosionPosition;
            if (normal.sqrMagnitude <= Mathf.Epsilon)
            {
                normal = Vector3.up;
            }
            else
            {
                normal.Normalize();
            }

            HitContext hitContext = new HitContext(
                DamagePayload,
                targetProxy.DamageController,
                targetProxy.transform.position,
                normal,
                HitType.Area,
                deliveryIdentifier);
            return _interactionSystem.ResolveHit(hitContext, _hitLedger);
        }

        internal void Terminate(ProjectileTerminationReason reason)
        {
            if (!IsRunning || !Enum.IsDefined(
                    typeof(ProjectileTerminationReason), reason) ||
                reason == ProjectileTerminationReason.None)
            {
                return;
            }

            IsRunning = false;
            ProjectileTerminationEvent terminationEvent =
                new ProjectileTerminationEvent(
                    _pooledEntity.PoolId,
                    DamagePayload.AttackKey,
                    reason,
                    transform.position);
            Terminated?.Invoke(terminationEvent);
            _spawnManager.ReturnProjectile(_pooledEntity);
        }

        private void CacheComponents()
        {
            _pooledEntity = GetComponent<PooledEntity>();
            _projectileMotion = null;
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (!(behaviour is IProjectileMotion projectileMotion))
                {
                    continue;
                }

                if (_projectileMotion != null)
                {
                    _projectileMotion = null;
                    return;
                }

                _projectileMotion = projectileMotion;
            }
        }

        private void CacheFactionVisuals()
        {
            _factionRenderers = GetComponentsInChildren<Renderer>(true);
            _factionPropertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyFactionVisuals(UnitFaction faction)
        {
            if (_factionRenderers == null || _factionPropertyBlock == null)
            {
                CacheFactionVisuals();
            }

            FactionVisuals.Apply(
                _factionRenderers,
                faction,
                _factionPropertyBlock);
        }
    }
}
