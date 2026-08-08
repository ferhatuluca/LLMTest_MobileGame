using System;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Damage
{
    public enum DamageOutcome
    {
        None,
        Applied,
        InvalidAmount,
        TargetInactive,
        TargetDead,
        Invulnerable
    }

    public enum HitType
    {
        Direct,
        Area
    }

    public readonly struct DamagePayload
    {
        private readonly StatusEffectPayload[] _statusEffects;

        public SpawnId SourceSpawnId { get; }
        public UnitFaction SourceFaction { get; }
        public AttackSequenceId AttackSequenceId { get; }
        public float BaseDamage { get; }
        public DamageCategoryId DamageCategory { get; }
        public AttackKey AttackKey => new AttackKey(SourceSpawnId, AttackSequenceId);
        public int StatusEffectCount => _statusEffects == null ? 0 : _statusEffects.Length;
        public bool IsValid
        {
            get
            {
                if (!AttackKey.IsValid ||
                    !Enum.IsDefined(typeof(UnitFaction), SourceFaction) ||
                    !IsPositiveFinite(BaseDamage))
                {
                    return false;
                }

                for (int effectIndex = 0; effectIndex < StatusEffectCount; effectIndex++)
                {
                    if (!GetStatusEffect(effectIndex).IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public DamagePayload(
            SpawnId sourceSpawnId,
            UnitFaction sourceFaction,
            AttackSequenceId attackSequenceId,
            float baseDamage,
            DamageCategoryId damageCategory,
            params StatusEffectPayload[] statusEffects)
        {
            SourceSpawnId = sourceSpawnId;
            SourceFaction = sourceFaction;
            AttackSequenceId = attackSequenceId;
            BaseDamage = baseDamage;
            DamageCategory = damageCategory;
            _statusEffects = statusEffects == null || statusEffects.Length == 0
                ? Array.Empty<StatusEffectPayload>()
                : (StatusEffectPayload[])statusEffects.Clone();
        }

        public StatusEffectPayload GetStatusEffect(int index)
        {
            if (_statusEffects == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _statusEffects[index];
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct HitContext
    {
        public DamagePayload Payload { get; }
        public DamageController Target { get; }
        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public HitType HitType { get; }
        public string DeliveryIdentifier { get; }
        public bool IsValid =>
            Payload.IsValid &&
            Target != null &&
            Enum.IsDefined(typeof(HitType), HitType) &&
            !string.IsNullOrWhiteSpace(DeliveryIdentifier);

        public HitContext(
            DamagePayload payload,
            DamageController target,
            Vector3 position,
            Vector3 normal,
            HitType hitType,
            string deliveryIdentifier)
        {
            Payload = payload;
            Target = target;
            Position = position;
            Normal = normal;
            HitType = hitType;
            DeliveryIdentifier = deliveryIdentifier ?? string.Empty;
        }
    }

    public readonly struct DamageResult
    {
        private readonly StatusEffectPayload[] _acceptedStatusEffects;

        public DamageOutcome Outcome { get; }
        public float AppliedAmount { get; }
        public bool TargetDied { get; }
        public int AcceptedStatusEffectCount =>
            _acceptedStatusEffects == null ? 0 : _acceptedStatusEffects.Length;
        public bool IsApplied => Outcome == DamageOutcome.Applied;

        private DamageResult(
            DamageOutcome outcome,
            float appliedAmount,
            bool targetDied,
            params StatusEffectPayload[] acceptedStatusEffects)
        {
            Outcome = outcome;
            AppliedAmount = appliedAmount;
            TargetDied = targetDied;
            _acceptedStatusEffects = acceptedStatusEffects == null || acceptedStatusEffects.Length == 0
                ? Array.Empty<StatusEffectPayload>()
                : (StatusEffectPayload[])acceptedStatusEffects.Clone();
        }

        public static DamageResult CreateApplied(
            float appliedAmount,
            bool targetDied,
            params StatusEffectPayload[] acceptedStatusEffects)
        {
            if (!IsPositiveFinite(appliedAmount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(appliedAmount),
                    "Applied damage must be a positive finite value.");
            }

            if (targetDied && acceptedStatusEffects != null && acceptedStatusEffects.Length > 0)
            {
                throw new ArgumentException(
                    "A lethal hit cannot accept status effects after death.",
                    nameof(acceptedStatusEffects));
            }

            if (acceptedStatusEffects != null)
            {
                foreach (StatusEffectPayload acceptedStatusEffect in acceptedStatusEffects)
                {
                    if (!acceptedStatusEffect.IsValid)
                    {
                        throw new ArgumentException(
                            "Accepted status effects must be valid.",
                            nameof(acceptedStatusEffects));
                    }
                }
            }

            return new DamageResult(
                DamageOutcome.Applied,
                appliedAmount,
                targetDied,
                acceptedStatusEffects);
        }

        public StatusEffectPayload GetAcceptedStatusEffect(int index)
        {
            if (_acceptedStatusEffects == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _acceptedStatusEffects[index];
        }

        public static DamageResult CreateRejected(DamageOutcome outcome)
        {
            if (!Enum.IsDefined(typeof(DamageOutcome), outcome) ||
                outcome == DamageOutcome.None ||
                outcome == DamageOutcome.Applied)
            {
                throw new ArgumentException("An applied result cannot be created as a rejection.", nameof(outcome));
            }

            return new DamageResult(outcome, 0f, false);
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
