using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    [DisallowMultipleComponent]
    public sealed class GrenadeAttackExecutor : MonoBehaviour, IAttackExecutor
    {
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public Transform AttackOrigin { get; private set; }

        public AttackDeliveryType DeliveryType => AttackDeliveryType.Grenade;

        public InteractionResult ExecuteImpact(
            AttackExecutionContext executionContext)
        {
            if (!CanExecute(executionContext))
            {
                return CreateRejectedResult(executionContext);
            }

            ProjectileDefinition projectileDefinition =
                executionContext.Definition.ProjectileDefinition;
            if (!BallisticLaunchRules.TryGetLowArcDirection(
                    AttackOrigin.position,
                    executionContext.TargetPosition,
                    projectileDefinition.Speed,
                    Physics.gravity * projectileDefinition.GravityScale,
                    out Vector3 direction))
            {
                return CreateRejectedResult(executionContext);
            }

            ProjectileSpawnRequest spawnRequest = new ProjectileSpawnRequest(
                projectileDefinition,
                AttackPayloadFactory.Create(executionContext),
                AttackOrigin.position,
                Quaternion.LookRotation(direction.normalized, Vector3.up));
            SpawnResult<PooledEntity> spawnResult =
                SpawnManager.SpawnProjectile(spawnRequest, InteractionSystem);
            return spawnResult.IsSuccess
                ? default
                : CreateRejectedResult(executionContext);
        }

        internal bool Configure(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem,
            Transform attackOrigin)
        {
            if (spawnManager == null || interactionSystem == null ||
                attackOrigin == null)
            {
                return false;
            }

            SpawnManager = spawnManager;
            InteractionSystem = interactionSystem;
            AttackOrigin = attackOrigin;
            return true;
        }

        private bool CanExecute(AttackExecutionContext executionContext)
        {
            return SpawnManager != null && InteractionSystem != null &&
                   AttackOrigin != null && executionContext.Source != null &&
                   executionContext.Target != null &&
                   executionContext.Definition != null &&
                   executionContext.Definition.DeliveryType == DeliveryType &&
                   executionContext.Definition.ProjectileDefinition != null;
        }

        private static InteractionResult CreateRejectedResult(
            AttackExecutionContext executionContext)
        {
            return InteractionResult.CreateRejected(
                InteractionOutcome.InvalidPayload,
                executionContext.AttackKey,
                executionContext.Target == null
                    ? default
                    : executionContext.Target.SpawnId);
        }
    }

    internal static class BallisticLaunchRules
    {
        public static bool TryGetLowArcDirection(
            Vector3 origin,
            Vector3 target,
            float speed,
            Vector3 gravity,
            out Vector3 direction)
        {
            direction = default;
            if (speed <= 0f || float.IsNaN(speed) ||
                float.IsInfinity(speed) || gravity.sqrMagnitude <=
                Mathf.Epsilon)
            {
                return false;
            }

            float gravityMagnitude = gravity.magnitude;
            Vector3 up = -gravity / gravityMagnitude;
            Vector3 displacement = target - origin;
            float verticalDistance = Vector3.Dot(displacement, up);
            Vector3 horizontalDisplacement =
                displacement - (up * verticalDistance);
            float horizontalDistance = horizontalDisplacement.magnitude;
            if (horizontalDistance <= Mathf.Epsilon)
            {
                direction = displacement.sqrMagnitude <= Mathf.Epsilon
                    ? up
                    : displacement.normalized;
                return true;
            }

            float speedSquared = speed * speed;
            float discriminant = (speedSquared * speedSquared) -
                (gravityMagnitude *
                 ((gravityMagnitude * horizontalDistance *
                   horizontalDistance) +
                  (2f * verticalDistance * speedSquared)));
            if (discriminant < 0f || float.IsNaN(discriminant) ||
                float.IsInfinity(discriminant))
            {
                return false;
            }

            float tangent = (speedSquared - Mathf.Sqrt(discriminant)) /
                            (gravityMagnitude * horizontalDistance);
            float cosine = 1f / Mathf.Sqrt(1f + (tangent * tangent));
            float sine = tangent * cosine;
            direction =
                (horizontalDisplacement / horizontalDistance * cosine) +
                (up * sine);
            return direction.sqrMagnitude > Mathf.Epsilon;
        }
    }
}
