using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Core;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    /// <summary>
    /// Authors pooled projectile identity and delivery-specific motion, lifetime,
    /// gravity, fuse, and area-query configuration.
    /// </summary>
    [CreateAssetMenu(menuName = "Monsters vs Zombies/Combat/Projectile Definition")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        [field: SerializeField] public PoolId PoolId { get; private set; }
        [field: SerializeField] public AttackDeliveryType CompatibleDeliveryType { get; private set; }
        [field: SerializeField] public float Speed { get; private set; }
        [field: SerializeField] public float MaximumLifetime { get; private set; }
        [field: SerializeField] public float CollisionRadius { get; private set; }
        [field: SerializeField] public float GravityScale { get; private set; }
        [field: SerializeField] public float ExplosionRadius { get; private set; }
        [field: SerializeField] public float FuseDuration { get; private set; }

        public ValidationResult Validate()
        {
            ValidationResult result = new ValidationResult();
            if (!PoolId.IsValid)
            {
                result.AddError(ValidationCode.MissingId, $"{name} requires a projectile pool ID.");
            }

            bool isCompatibleDelivery = CompatibleDeliveryType == AttackDeliveryType.Projectile ||
                                        CompatibleDeliveryType == AttackDeliveryType.Grenade;
            if (!isCompatibleDelivery)
            {
                result.AddError(
                    ValidationCode.InvalidDeliveryType,
                    $"{name} must use Projectile or Grenade delivery.");
            }

            if (!NumericValidation.IsPositiveFinite(Speed))
            {
                result.AddError(
                    ValidationCode.InvalidPositiveValue,
                    $"{name}.{nameof(Speed)} must be positive and finite.");
            }

            if (!NumericValidation.IsPositiveFinite(CollisionRadius))
            {
                result.AddError(
                    ValidationCode.InvalidPositiveValue,
                    $"{name}.{nameof(CollisionRadius)} must be positive and finite.");
            }

            AddNonNegativeValueError(result, GravityScale, nameof(GravityScale));
            AddNonNegativeValueError(result, ExplosionRadius, nameof(ExplosionRadius));
            AddNonNegativeValueError(result, MaximumLifetime, nameof(MaximumLifetime));
            AddNonNegativeValueError(result, FuseDuration, nameof(FuseDuration));

            if (CompatibleDeliveryType == AttackDeliveryType.Projectile)
            {
                AddRequiredPositiveValueError(result, MaximumLifetime, nameof(MaximumLifetime));
                if (FuseDuration != 0f)
                {
                    result.AddError(
                        ValidationCode.IncompatibleDeliveryType,
                        $"{name}.{nameof(FuseDuration)} must be zero for projectile delivery.");
                }
            }
            else if (CompatibleDeliveryType == AttackDeliveryType.Grenade)
            {
                AddRequiredPositiveValueError(result, FuseDuration, nameof(FuseDuration));
                AddRequiredPositiveValueError(result, ExplosionRadius, nameof(ExplosionRadius));
                if (MaximumLifetime != 0f)
                {
                    result.AddError(
                        ValidationCode.IncompatibleDeliveryType,
                        $"{name}.{nameof(MaximumLifetime)} must be zero when the grenade fuse owns expiry.");
                }
            }

            return result;
        }

        private void OnValidate()
        {
            ValidationReporter.Report(this, Validate());
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

        private void AddRequiredPositiveValueError(
            ValidationResult result,
            float value,
            string fieldName)
        {
            if (!NumericValidation.IsPositiveFinite(value))
            {
                result.AddError(
                    ValidationCode.InvalidPositiveValue,
                    $"{name}.{fieldName} must be positive and finite for {CompatibleDeliveryType} delivery.");
            }
        }
    }
}
