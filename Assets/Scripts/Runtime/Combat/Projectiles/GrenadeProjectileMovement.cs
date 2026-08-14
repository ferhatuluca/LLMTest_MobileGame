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
        private const string k_UnitTargetLayerName = "UnitTarget";

        private Rigidbody _rigidbody;
        private Collider[] _areaColliders;
        private ProjectileController _projectileController;
        private ProjectileDefinition _projectileDefinition;

        [field: SerializeField] public int AreaCapacity { get; private set; }

        public AttackDeliveryType DeliveryType => AttackDeliveryType.Grenade;
        public bool IsInitialized => _areaColliders != null;

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
            _areaColliders = new Collider[areaCapacity];
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

            if (_areaColliders != null)
            {
                Array.Clear(_areaColliders, 0, _areaColliders.Length);
            }
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

            int layer = LayerMask.NameToLayer(k_UnitTargetLayerName);
            int colliderCount = Physics.OverlapSphereNonAlloc(
                explosionPosition,
                _projectileDefinition.ExplosionRadius,
                _areaColliders,
                1 << layer,
                QueryTriggerInteraction.Collide);
            for (int colliderIndex = 0;
                 colliderIndex < colliderCount;
                 colliderIndex++)
            {
                Collider targetCollider = _areaColliders[colliderIndex];
                _areaColliders[colliderIndex] = null;
                if (targetCollider != null &&
                    targetCollider.TryGetComponent(
                        out DamageTargetProxy targetProxy))
                {
                    _projectileController.ResolveAreaHit(
                        targetProxy,
                        explosionPosition,
                        $"Grenade:{_projectileDefinition.PoolId}");
                }
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
            if (AreaCapacity <= 0 || _areaColliders != null)
            {
                return;
            }

            _areaColliders = new Collider[AreaCapacity];
        }
    }
}
