using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    /// <summary>
    /// Resolves a captured melee impact directly against the selected target
    /// through InteractionSystem and the attack's hit ledger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeAttackExecutor : MonoBehaviour, IAttackExecutor
    {
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }

        public AttackDeliveryType DeliveryType => AttackDeliveryType.Melee;

        public InteractionResult ExecuteImpact(
            AttackExecutionContext executionContext)
        {
            if (InteractionSystem == null ||
                executionContext.Source == null ||
                executionContext.Target == null ||
                executionContext.Target.DamageController == null ||
                executionContext.Definition == null ||
                executionContext.HitLedger == null)
            {
                return CreateRejectedResult(executionContext);
            }

            Vector3 impactPosition = executionContext.TargetPosition;
            Vector3 impactNormal =
                executionContext.Source.transform.position - impactPosition;
            if (impactNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                impactNormal = Vector3.up;
            }
            else
            {
                impactNormal.Normalize();
            }

            HitContext hitContext = new HitContext(
                AttackPayloadFactory.Create(executionContext),
                executionContext.Target.DamageController,
                impactPosition,
                impactNormal,
                HitType.Direct,
                $"Melee:{executionContext.Definition.AttackId}");
            return InteractionSystem.ResolveHit(
                hitContext,
                executionContext.HitLedger);
        }

        internal bool Configure(InteractionSystem interactionSystem)
        {
            if (interactionSystem == null)
            {
                return false;
            }

            InteractionSystem = interactionSystem;
            return true;
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
