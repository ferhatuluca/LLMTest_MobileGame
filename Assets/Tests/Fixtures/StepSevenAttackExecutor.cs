using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSevenAttackExecutor : MonoBehaviour, IAttackExecutor
    {
        [field: SerializeField] public AttackDeliveryType DeliveryType { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }

        public int ExecutionCount { get; private set; }
        public AttackExecutionContext LastExecutionContext { get; private set; }

        public InteractionResult ExecuteImpact(
            AttackExecutionContext executionContext)
        {
            ExecutionCount++;
            LastExecutionContext = executionContext;
            if (InteractionSystem == null ||
                executionContext.Source == null ||
                executionContext.Target == null ||
                executionContext.Definition == null)
            {
                return InteractionResult.CreateRejected(
                    InteractionOutcome.InvalidPayload,
                    executionContext.AttackKey,
                    executionContext.Target == null
                        ? default
                        : executionContext.Target.SpawnId);
            }

            DamagePayload damagePayload = new DamagePayload(
                executionContext.Source.SpawnId,
                executionContext.Source.Faction,
                executionContext.AttackKey.SequenceId,
                executionContext.Definition.Damage,
                executionContext.Definition.DamageCategoryId);
            HitContext hitContext = new HitContext(
                damagePayload,
                executionContext.Target.DamageController,
                executionContext.Target.transform.position,
                Vector3.up,
                HitType.Direct,
                "StepSevenTestExecutor");
            return InteractionSystem.ResolveHit(
                hitContext,
                executionContext.HitLedger);
        }
    }
}
