using System;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Data;

namespace MonstersVsZombies.Combat.Attacks
{
    /// <summary>
    /// Converts an immutable attack execution snapshot and authored definition
    /// into the DamagePayload consumed by every delivery implementation.
    /// </summary>
    public static class AttackPayloadFactory
    {
        public static DamagePayload Create(AttackExecutionContext executionContext)
        {
            if (executionContext.HasCapturedDamagePayload)
            {
                return executionContext.CapturedDamagePayload;
            }

            if (executionContext.Source == null ||
                executionContext.Definition == null ||
                !executionContext.AttackKey.IsValid)
            {
                throw new ArgumentException(
                    "An attack payload requires a valid source, definition, and attack key.",
                    nameof(executionContext));
            }

            AttackDefinition attackDefinition = executionContext.Definition;
            AcceptedHitEffectConfiguration acceptedHitEffect =
                attackDefinition.AcceptedHitEffect;
            if (acceptedHitEffect.EffectType == StatusEffectType.None)
            {
                return new DamagePayload(
                    executionContext.Source.SpawnId,
                    executionContext.Source.Faction,
                    executionContext.AttackKey.SequenceId,
                    attackDefinition.Damage,
                    attackDefinition.DamageCategoryId);
            }

            return new DamagePayload(
                executionContext.Source.SpawnId,
                executionContext.Source.Faction,
                executionContext.AttackKey.SequenceId,
                attackDefinition.Damage,
                attackDefinition.DamageCategoryId,
                new StatusEffectPayload(
                    acceptedHitEffect.EffectType,
                    acceptedHitEffect.Duration));
        }
    }
}
