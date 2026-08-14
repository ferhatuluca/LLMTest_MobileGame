using System;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    /// <summary>
    /// Captures all source, target, definition, identity, and hit-ledger state an
    /// executor needs so an attack cannot observe later pooled-state changes.
    /// </summary>
    public readonly struct AttackExecutionContext
    {
        public UnitController Source { get; }
        public UnitController Target { get; }
        public Vector3 TargetPosition { get; }
        public AttackDefinition Definition { get; }
        public AttackKey AttackKey { get; }
        public AttackHitLedger HitLedger { get; }
        public DamagePayload CapturedDamagePayload { get; }
        public bool HasCapturedDamagePayload { get; }

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
            CapturedDamagePayload = default;
            HasCapturedDamagePayload = false;
        }

        public AttackExecutionContext(
            UnitController source,
            UnitController target,
            Vector3 targetPosition,
            AttackDefinition definition,
            AttackKey attackKey,
            AttackHitLedger hitLedger,
            DamagePayload capturedDamagePayload)
        {
            if (!capturedDamagePayload.IsValid)
            {
                throw new ArgumentException(
                    "A captured attack payload must be valid.",
                    nameof(capturedDamagePayload));
            }

            Source = source;
            Target = target;
            TargetPosition = targetPosition;
            Definition = definition;
            AttackKey = attackKey;
            HitLedger = hitLedger;
            CapturedDamagePayload = capturedDamagePayload;
            HasCapturedDamagePayload = true;
        }
    }

    public interface IAttackExecutor
    {
        AttackDeliveryType DeliveryType { get; }
        InteractionResult ExecuteImpact(AttackExecutionContext executionContext);
    }

}
