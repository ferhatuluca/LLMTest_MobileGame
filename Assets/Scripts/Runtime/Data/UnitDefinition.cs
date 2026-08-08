using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    public abstract class UnitDefinition : ScriptableObject
    {
        [field: SerializeField] public UnitId UnitId { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public UnitFaction Faction { get; private set; }
        [field: SerializeField] public float MaximumHealth { get; private set; }
        [field: SerializeField] public float MoveSpeed { get; private set; }
        [field: SerializeField] public float TurnSpeed { get; private set; }
        [field: SerializeField] public PoolId PoolId { get; private set; }

        public virtual ValidationResult Validate()
        {
            ValidationResult result = new ValidationResult();

            if (!UnitId.IsValid)
            {
                result.AddError(ValidationCode.MissingId, $"{name} requires a stable unit ID.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                result.AddError(ValidationCode.MissingDisplayName, $"{name} requires a display name.");
            }

            if (!System.Enum.IsDefined(typeof(UnitFaction), Faction))
            {
                result.AddError(ValidationCode.InvalidFaction, $"{name} has an invalid faction.");
            }

            AddPositiveValueError(result, MaximumHealth, nameof(MaximumHealth));
            AddPositiveValueError(result, MoveSpeed, nameof(MoveSpeed));
            AddPositiveValueError(result, TurnSpeed, nameof(TurnSpeed));

            if (!PoolId.IsValid)
            {
                result.AddError(ValidationCode.MissingId, $"{name} requires a pool ID.");
            }

            return result;
        }

        protected virtual void OnValidate()
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
    }
}
