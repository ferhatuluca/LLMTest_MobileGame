using System;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Combat.Projectiles
{
    /// <summary>
    /// Advances Bullet and Fireball motion with non-allocating casts, classifies
    /// the nearest contact, resolves hostile hits, and terminates on world impact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KinematicProjectileMovement : MonoBehaviour,
        IProjectileMotion
    {
        private RaycastHit[] _sweepHits;
        private ProjectileController _projectileController;
        private ProjectileDefinition _projectileDefinition;
        private int _deliveryLayerMask;

        [field: SerializeField] public int SweepCapacity { get; private set; }

        public AttackDeliveryType DeliveryType => AttackDeliveryType.Projectile;
        public bool IsInitialized => _sweepHits != null;
        public bool WasLastSweepSaturated { get; private set; }

        private void Awake()
        {
            EnsureSweepBuffer();
        }

        private void OnValidate()
        {
            EnsureSweepBuffer();
        }

        public bool InitializeSweepCapacity(int sweepCapacity)
        {
            if (sweepCapacity <= 0 ||
                (SweepCapacity > 0 && SweepCapacity != sweepCapacity))
            {
                return false;
            }

            SweepCapacity = sweepCapacity;
            _sweepHits = new RaycastHit[sweepCapacity];
            _deliveryLayerMask = DeliveryCollisionRules.CreateDeliveryLayerMask();
            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            EnsureSweepBuffer();
            if (!IsInitialized)
            {
                failureMessage =
                    "KinematicProjectileMovement requires an explicit positive sweep capacity.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        bool IProjectileMotion.PrepareMotion(
            ProjectileController projectileController,
            ProjectileDefinition projectileDefinition)
        {
            if (projectileController == null || projectileDefinition == null ||
                projectileDefinition.CompatibleDeliveryType != DeliveryType ||
                !projectileDefinition.Validate().IsValid || !IsInitialized)
            {
                return false;
            }

            _projectileController = projectileController;
            _projectileDefinition = projectileDefinition;
            WasLastSweepSaturated = false;
            return true;
        }

        bool IProjectileMotion.StartMotion()
        {
            return _projectileController != null &&
                   _projectileDefinition != null;
        }

        void IProjectileMotion.AdvanceTime(float deltaTime)
        {
            if (_projectileController == null || deltaTime <= 0f)
            {
                return;
            }

            Vector3 direction = transform.forward;
            float distance = _projectileDefinition.Speed * deltaTime;
            int hitCount = Physics.SphereCastNonAlloc(
                transform.position,
                _projectileDefinition.CollisionRadius,
                direction,
                _sweepHits,
                distance,
                _deliveryLayerMask,
                QueryTriggerInteraction.Collide);
            WasLastSweepSaturated = hitCount >= _sweepHits.Length;

            int selectedHitIndex = -1;
            DeliveryContactType selectedContactType = DeliveryContactType.Ignore;
            DamageTargetProxy selectedTarget = null;
            float selectedDistance = float.PositiveInfinity;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit sweepHit = _sweepHits[hitIndex];
                DeliveryContactType contactType = DeliveryCollisionRules.Classify(
                    sweepHit.collider,
                    _projectileController.DamagePayload,
                    out DamageTargetProxy targetProxy);
                if (contactType == DeliveryContactType.Ignore ||
                    sweepHit.distance > selectedDistance ||
                    (sweepHit.distance == selectedDistance &&
                     selectedContactType == DeliveryContactType.World))
                {
                    continue;
                }

                selectedHitIndex = hitIndex;
                selectedContactType = contactType;
                selectedTarget = targetProxy;
                selectedDistance = sweepHit.distance;
            }

            if (selectedHitIndex < 0)
            {
                transform.position += direction * distance;
                ClearSweepHits(hitCount);
                return;
            }

            RaycastHit selectedHit = _sweepHits[selectedHitIndex];
            transform.position += direction * selectedHit.distance;
            ClearSweepHits(hitCount);
            if (selectedContactType == DeliveryContactType.World)
            {
                _projectileController.Terminate(
                    ProjectileTerminationReason.WorldImpact);
                return;
            }

            _projectileController.ResolveDirectHit(
                selectedTarget,
                selectedHit.point,
                selectedHit.normal,
                $"Projectile:{_projectileDefinition.PoolId}");
            _projectileController.Terminate(
                ProjectileTerminationReason.HostileHit);
        }

        void IProjectileMotion.HandleTimerExpired()
        {
            _projectileController?.Terminate(
                ProjectileTerminationReason.LifetimeExpired);
        }

        void IProjectileMotion.ResetMotion()
        {
            if (_sweepHits != null)
            {
                Array.Clear(_sweepHits, 0, _sweepHits.Length);
            }

            _projectileController = null;
            _projectileDefinition = null;
            WasLastSweepSaturated = false;
        }

        private void ClearSweepHits(int hitCount)
        {
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                _sweepHits[hitIndex] = default;
            }
        }

        private void EnsureSweepBuffer()
        {
            if (SweepCapacity <= 0 ||
                (_sweepHits != null && _sweepHits.Length == SweepCapacity))
            {
                return;
            }

            _sweepHits = new RaycastHit[SweepCapacity];
            _deliveryLayerMask = DeliveryCollisionRules.CreateDeliveryLayerMask();
        }
    }
}
