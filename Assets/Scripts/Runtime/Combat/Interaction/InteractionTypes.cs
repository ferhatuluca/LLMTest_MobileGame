using System;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;

namespace MonstersVsZombies.Combat.Interaction
{
    public enum InteractionOutcome
    {
        None,
        Applied,
        InvalidPayload,
        InvalidTarget,
        InvalidFaction,
        SourceEqualsTarget,
        TargetInactive,
        TargetDead,
        Invulnerable,
        AlreadyHit,
        OutOfRange
    }

    public readonly struct InteractionResult
    {
        public InteractionOutcome Outcome { get; }
        public AttackKey AttackKey { get; }
        public SpawnId TargetSpawnId { get; }
        public DamageResult DamageResult { get; }
        public bool IsApplied => Outcome == InteractionOutcome.Applied && DamageResult.IsApplied;

        private InteractionResult(
            InteractionOutcome outcome,
            AttackKey attackKey,
            SpawnId targetSpawnId,
            DamageResult damageResult)
        {
            Outcome = outcome;
            AttackKey = attackKey;
            TargetSpawnId = targetSpawnId;
            DamageResult = damageResult;
        }

        public static InteractionResult CreateApplied(
            AttackKey attackKey,
            SpawnId targetSpawnId,
            DamageResult damageResult)
        {
            if (!damageResult.IsApplied)
            {
                throw new ArgumentException(
                    "An applied interaction requires an applied damage result.",
                    nameof(damageResult));
            }

            if (!attackKey.IsValid)
            {
                throw new ArgumentException("An applied interaction requires a valid attack key.", nameof(attackKey));
            }

            if (!targetSpawnId.IsValid)
            {
                throw new ArgumentException(
                    "An applied interaction requires a valid target spawn ID.",
                    nameof(targetSpawnId));
            }

            return new InteractionResult(
                InteractionOutcome.Applied,
                attackKey,
                targetSpawnId,
                damageResult);
        }

        public static InteractionResult CreateRejected(
            InteractionOutcome outcome,
            AttackKey attackKey,
            SpawnId targetSpawnId)
        {
            if (!Enum.IsDefined(typeof(InteractionOutcome), outcome) ||
                outcome == InteractionOutcome.None ||
                outcome == InteractionOutcome.Applied)
            {
                throw new ArgumentException("An applied interaction cannot be created as a rejection.", nameof(outcome));
            }

            return new InteractionResult(
                outcome,
                attackKey,
                targetSpawnId,
                default);
        }
    }
}
