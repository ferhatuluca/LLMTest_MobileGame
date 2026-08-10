using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Spawning;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    /// <summary>
    /// Converts an attack snapshot into a pooled straight-projectile spawn with
    /// captured damage, source identity, origin, and launch direction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileAttackExecutor : MonoBehaviour, IAttackExecutor
    {
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public Transform AttackOrigin { get; private set; }

        public AttackDeliveryType DeliveryType => AttackDeliveryType.Projectile;

        public InteractionResult ExecuteImpact(
            AttackExecutionContext executionContext)
        {
            if (!CanExecute(executionContext))
            {
                return CreateRejectedResult(executionContext);
            }

            Vector3 direction = executionContext.TargetPosition -
                                AttackOrigin.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = AttackOrigin.forward;
            }

            ProjectileSpawnRequest spawnRequest = new ProjectileSpawnRequest(
                executionContext.Definition.ProjectileDefinition,
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
}
