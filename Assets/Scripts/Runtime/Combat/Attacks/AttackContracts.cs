using System;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    public readonly struct AttackExecutionContext
    {
        public UnitController Source { get; }
        public UnitController Target { get; }
        public Vector3 TargetPosition { get; }
        public AttackDefinition Definition { get; }
        public AttackKey AttackKey { get; }
        public AttackHitLedger HitLedger { get; }

        public AttackExecutionContext(
            UnitController source,
            UnitController target,
            AttackDefinition definition,
            AttackKey attackKey,
            AttackHitLedger hitLedger)
            : this(
                source,
                target,
                target == null
                    ? default
                    : target.transform.position,
                definition,
                attackKey,
                hitLedger)
        {
        }

        public AttackExecutionContext(
            UnitController source,
            UnitController target,
            Vector3 targetPosition,
            AttackDefinition definition,
            AttackKey attackKey,
            AttackHitLedger hitLedger)
        {
            Source = source;
            Target = target;
            TargetPosition = targetPosition;
            Definition = definition;
            AttackKey = attackKey;
            HitLedger = hitLedger;
        }
    }

    public interface IAttackExecutor
    {
        AttackDeliveryType DeliveryType { get; }
        InteractionResult ExecuteImpact(AttackExecutionContext executionContext);
    }

    public interface IAttackResultPolicy
    {
        void HandleSuccessfulInteraction(
            AttackExecutionContext executionContext,
            InteractionResult interactionResult);
    }

    [Serializable]
    public sealed class AttackExecutorBinding
    {
        [field: SerializeField] public AttackDeliveryType DeliveryType { get; private set; }
        [field: SerializeField] public MonoBehaviour ExecutorComponent { get; private set; }

        public IAttackExecutor Executor => ExecutorComponent as IAttackExecutor;

        public AttackExecutorBinding(
            AttackDeliveryType deliveryType,
            MonoBehaviour executorComponent)
        {
            DeliveryType = deliveryType;
            ExecutorComponent = executorComponent;
        }
    }
}
