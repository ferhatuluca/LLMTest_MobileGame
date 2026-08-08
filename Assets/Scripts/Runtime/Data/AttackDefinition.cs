using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using System;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    [Serializable]
    public struct AcceptedHitEffectConfiguration
    {
        [field: SerializeField] public StatusEffectType EffectType { get; private set; }
        [field: SerializeField] public float Duration { get; private set; }

        public AcceptedHitEffectConfiguration(StatusEffectType effectType, float duration)
        {
            EffectType = effectType;
            Duration = duration;
        }

        public ValidationResult Validate(string contextName)
        {
            ValidationResult result = new ValidationResult();
            if (!Enum.IsDefined(typeof(StatusEffectType), EffectType))
            {
                result.AddError(
                    ValidationCode.InvalidStatusEffect,
                    $"{contextName} has an undefined accepted-hit effect type.");
                return result;
            }

            if (EffectType == StatusEffectType.None)
            {
                if (Duration != 0f)
                {
                    result.AddError(
                        ValidationCode.InvalidStatusEffect,
                        $"{contextName} duration must be zero when no effect is selected.");
                }

                return result;
            }

            if (!NumericValidation.IsPositiveFinite(Duration))
            {
                result.AddError(
                    ValidationCode.InvalidPositiveValue,
                    $"{contextName} duration must be positive and finite when an effect is selected.");
            }

            return result;
        }
    }

    [CreateAssetMenu(menuName = "Monsters vs Zombies/Combat/Attack Definition")]
    public sealed class AttackDefinition : ScriptableObject
    {
        [field: SerializeField] public AttackId AttackId { get; private set; }
        [field: SerializeField] public float Damage { get; private set; }
        [field: SerializeField] public float AttackRange { get; private set; }
        [field: SerializeField] public float CooldownDuration { get; private set; }
        [field: SerializeField] public float WindupDuration { get; private set; }
        [field: SerializeField] public float RecoveryDuration { get; private set; }
        [field: SerializeField] public AttackDeliveryType DeliveryType { get; private set; }
        [field: SerializeField] public ProjectileDefinition ProjectileDefinition { get; private set; }
        [field: SerializeField] public AcceptedHitEffectConfiguration AcceptedHitEffect { get; private set; }
        [field: SerializeField] public DamageCategoryId DamageCategoryId { get; private set; }

        public ValidationResult Validate()
        {
            ValidationResult result = new ValidationResult();

            if (!AttackId.IsValid)
            {
                result.AddError(ValidationCode.MissingId, $"{name} requires a stable attack ID.");
            }

            AddPositiveValueError(result, Damage, nameof(Damage));
            AddPositiveValueError(result, AttackRange, nameof(AttackRange));
            AddPositiveValueError(result, CooldownDuration, nameof(CooldownDuration));
            AddNonNegativeValueError(result, WindupDuration, nameof(WindupDuration));
            AddNonNegativeValueError(result, RecoveryDuration, nameof(RecoveryDuration));
            result.Merge(AcceptedHitEffect.Validate($"{name}.{nameof(AcceptedHitEffect)}"));

            bool hasDefinedDelivery = System.Enum.IsDefined(typeof(AttackDeliveryType), DeliveryType) &&
                                      DeliveryType != AttackDeliveryType.Unspecified;
            if (!hasDefinedDelivery)
            {
                result.AddError(
                    ValidationCode.InvalidDeliveryType,
                    $"{name} requires an explicit attack delivery type.");
            }
            else if (DeliveryType == AttackDeliveryType.Projectile ||
                     DeliveryType == AttackDeliveryType.Grenade)
            {
                if (ProjectileDefinition == null)
                {
                    result.AddError(
                        ValidationCode.MissingProjectileDefinition,
                        $"{name} requires a projectile definition for {DeliveryType} delivery.");
                }
                else
                {
                    result.Merge(ProjectileDefinition.Validate());
                    if (ProjectileDefinition.CompatibleDeliveryType != DeliveryType)
                    {
                        result.AddError(
                            ValidationCode.IncompatibleDeliveryType,
                            $"{name}'s projectile definition is incompatible with {DeliveryType} delivery.");
                    }
                }
            }
            else if (ProjectileDefinition != null)
            {
                result.AddError(
                    ValidationCode.IncompatibleDeliveryType,
                    $"{name} cannot assign a projectile definition to {DeliveryType} delivery.");
            }

            return result;
        }

        private void OnValidate()
        {
            ValidationReporter.Report(this, Validate());
        }

        private void AddPositiveValueError(ValidationResult result, float value, string fieldName)
        {
            if (!NumericValidation.IsPositiveFinite(value))
            {
                result.AddError(
                    ValidationCode.InvalidPositiveValue,
                    $"{name}.{fieldName} must be a positive finite value.");
            }
        }

        private void AddNonNegativeValueError(ValidationResult result, float value, string fieldName)
        {
            if (!NumericValidation.IsNonNegativeFinite(value))
            {
                result.AddError(
                    ValidationCode.InvalidNonNegativeValue,
                    $"{name}.{fieldName} cannot be negative or non-finite.");
            }
        }
    }
}
