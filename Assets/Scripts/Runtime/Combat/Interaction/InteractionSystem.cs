using System;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Interaction
{
    public readonly struct InteractionResolvedEvent
    {
        public HitContext HitContext { get; }
        public InteractionResult Result { get; }

        public InteractionResolvedEvent(HitContext hitContext, InteractionResult result)
        {
            HitContext = hitContext;
            Result = result;
        }
    }

    /// <summary>
    /// Validates immutable hit context, faction policy, source identity, and
    /// per-attack deduplication before forwarding damage to DamageController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionSystem : MonoBehaviour
    {
        public event Action<InteractionResolvedEvent> InteractionResolved;

        public InteractionResult ResolveHit(
            HitContext hitContext,
            AttackHitLedger hitLedger)
        {
            DamagePayload payload = hitContext.Payload;
            AttackKey attackKey = payload.AttackKey;
            DamageController target = hitContext.Target;
            SpawnId targetSpawnId = target == null ? default : target.SpawnId;

            if (!payload.IsValid ||
                !Enum.IsDefined(typeof(HitType), hitContext.HitType) ||
                string.IsNullOrWhiteSpace(hitContext.DeliveryIdentifier))
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.InvalidPayload,
                        attackKey,
                        targetSpawnId));
            }

            if (hitLedger == null || !hitLedger.IsActive || hitLedger.AttackKey != attackKey)
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.InvalidPayload,
                        attackKey,
                        targetSpawnId));
            }

            if (target == null || !targetSpawnId.IsValid)
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.InvalidTarget,
                        attackKey,
                        targetSpawnId));
            }

            if (payload.SourceSpawnId == targetSpawnId)
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.SourceEqualsTarget,
                        attackKey,
                        targetSpawnId));
            }

            if (!FactionRules.AreHostile(payload.SourceFaction, target.Faction))
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.InvalidFaction,
                        attackKey,
                        targetSpawnId));
            }

            if (!target.IsAlive)
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.TargetDead,
                        attackKey,
                        targetSpawnId));
            }

            if (!target.IsTargetActive)
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.TargetInactive,
                        attackKey,
                        targetSpawnId));
            }

            if (target.IsInvulnerable)
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.Invulnerable,
                        attackKey,
                        targetSpawnId));
            }

            if (hitLedger.HasAcceptedHit(attackKey, targetSpawnId))
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.AlreadyHit,
                        attackKey,
                        targetSpawnId));
            }

            if (!hitLedger.RecordAcceptedHit(attackKey, targetSpawnId))
            {
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.AlreadyHit,
                        attackKey,
                        targetSpawnId));
            }

            DamageResult damageResult = target.ApplyDamage(hitContext);
            if (!damageResult.IsApplied)
            {
                hitLedger.RemoveAcceptedHit(attackKey, targetSpawnId);
                return PublishResult(
                    hitContext,
                    InteractionResult.CreateRejected(
                        MapDamageOutcome(damageResult.Outcome),
                        attackKey,
                        targetSpawnId));
            }

            return PublishResult(
                hitContext,
                InteractionResult.CreateApplied(attackKey, targetSpawnId, damageResult));
        }

        private InteractionResult PublishResult(
            HitContext hitContext,
            InteractionResult result)
        {
            InteractionResolved?.Invoke(new InteractionResolvedEvent(hitContext, result));
            return result;
        }

        private static InteractionOutcome MapDamageOutcome(DamageOutcome damageOutcome)
        {
            switch (damageOutcome)
            {
                case DamageOutcome.TargetInactive:
                    return InteractionOutcome.TargetInactive;
                case DamageOutcome.TargetDead:
                    return InteractionOutcome.TargetDead;
                case DamageOutcome.Invulnerable:
                    return InteractionOutcome.Invulnerable;
                default:
                    return InteractionOutcome.InvalidPayload;
            }
        }
    }
}
