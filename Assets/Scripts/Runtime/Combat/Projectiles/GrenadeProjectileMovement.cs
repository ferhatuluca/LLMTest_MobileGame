using System;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Combat.Projectiles
{
    /// <summary>
    /// Launches a pooled Rigidbody grenade, resolves its fuse-time area query
    /// once per target, and returns it after the explosion completes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GrenadeProjectileMovement : MonoBehaviour,
        IProjectileMotion
    {
        private Rigidbody _rigidbody;
        private AreaQueryBuffer _areaQueryBuffer;
        private ProjectileController _projectileController;
        private ProjectileDefinition _projectileDefinition;

        [field: SerializeField] public int AreaCapacity { get; private set; }

        public AttackDeliveryType DeliveryType => AttackDeliveryType.Grenade;
        public bool IsInitialized => _areaQueryBuffer != null;
        public bool WasLastExplosionSaturated { get; private set; }
        public int LastExplosionTargetCount { get; private set; }

        private void Awake()
        {
            CacheRigidbody();
            EnsureAreaBuffer();
        }

        private void OnValidate()
        {
            CacheRigidbody();
            EnsureAreaBuffer();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_projectileController == null ||
                !_projectileController.IsRunning || collision == null ||
                collision.collider == null)
            {
                return;
            }

            Vector3 collisionPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            HandleCollisionContact(collision.collider, collisionPosition);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTriggerContact(other);
        }

        public bool InitializeAreaCapacity(int areaCapacity)
        {
            if (areaCapacity <= 0 ||
                (AreaCapacity > 0 && AreaCapacity != areaCapacity))
            {
                return false;
            }

            AreaCapacity = areaCapacity;
            _areaQueryBuffer = new AreaQueryBuffer(areaCapacity);
            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheRigidbody();
            EnsureAreaBuffer();
            if (_rigidbody == null || !IsInitialized)
            {
                failureMessage =
                    "GrenadeProjectileMovement requires a Rigidbody and an explicit positive area capacity.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        internal void HandleTriggerContact(Collider other)
        {
            if (_projectileController == null ||
                !_projectileController.IsRunning)
            {
                return;
            }

            DeliveryContactType contactType = DeliveryCollisionRules.Classify(
                other,
                _projectileController.DamagePayload,
                out _);
            if (contactType == DeliveryContactType.HostileTarget ||
                contactType == DeliveryContactType.World)
            {
                Detonate(transform.position);
            }
        }

        internal void HandleCollisionContact(
            Collider other,
            Vector3 collisionPosition)
        {
            if (_projectileController == null ||
                !_projectileController.IsRunning)
            {
                return;
            }

            DeliveryContactType contactType = DeliveryCollisionRules.Classify(
                other,
                _projectileController.DamagePayload,
                out _);
            if (contactType == DeliveryContactType.World)
            {
                Detonate(collisionPosition);
            }
        }

        bool IProjectileMotion.PrepareMotion(
            ProjectileController projectileController,
            ProjectileDefinition projectileDefinition)
        {
            CacheRigidbody();
            if (projectileController == null || projectileDefinition == null ||
                projectileDefinition.CompatibleDeliveryType != DeliveryType ||
                !projectileDefinition.Validate().IsValid ||
                !ValidateConfiguration(out _))
            {
                return false;
            }

            _projectileController = projectileController;
            _projectileDefinition = projectileDefinition;
            _rigidbody.useGravity = projectileDefinition.GravityScale > 0f;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            WasLastExplosionSaturated = false;
            LastExplosionTargetCount = 0;
            return true;
        }

        bool IProjectileMotion.StartMotion()
        {
            if (_projectileController == null || _projectileDefinition == null)
            {
                return false;
            }

            _rigidbody.linearVelocity =
                transform.forward * _projectileDefinition.Speed;
            return true;
        }

        void IProjectileMotion.AdvanceTime(float deltaTime)
        {
            if (_rigidbody == null || _projectileDefinition == null ||
                _projectileDefinition.GravityScale == 0f ||
                _projectileDefinition.GravityScale == 1f)
            {
                return;
            }

            _rigidbody.linearVelocity += Physics.gravity *
                ((_projectileDefinition.GravityScale - 1f) * deltaTime);
        }

        void IProjectileMotion.HandleTimerExpired()
        {
            Detonate(transform.position);
        }

        void IProjectileMotion.ResetMotion()
        {
            CacheRigidbody();
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            _areaQueryBuffer?.Reset();
            _projectileController = null;
            _projectileDefinition = null;
        }

        private void Detonate(Vector3 explosionPosition)
        {
            if (_projectileController == null ||
                !_projectileController.IsRunning)
            {
                return;
            }

            _areaQueryBuffer.Query(
                explosionPosition,
                _projectileDefinition.ExplosionRadius,
                _projectileController.DamagePayload.SourceSpawnId,
                _projectileController.DamagePayload.SourceFaction);
            WasLastExplosionSaturated = _areaQueryBuffer.WasSaturated;
            LastExplosionTargetCount = _areaQueryBuffer.UniqueTargetCount;
            for (int targetIndex = 0;
                 targetIndex < _areaQueryBuffer.UniqueTargetCount;
                 targetIndex++)
            {
                _projectileController.ResolveAreaHit(
                    _areaQueryBuffer.GetTarget(targetIndex),
                    explosionPosition,
                    $"Grenade:{_projectileDefinition.PoolId}");
            }

            _projectileController.Terminate(
                ProjectileTerminationReason.Explosion);
        }

        private void CacheRigidbody()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
        }

        private void EnsureAreaBuffer()
        {
            if (AreaCapacity <= 0 || _areaQueryBuffer != null)
            {
                return;
            }

            _areaQueryBuffer = new AreaQueryBuffer(AreaCapacity);
        }
    }
}
