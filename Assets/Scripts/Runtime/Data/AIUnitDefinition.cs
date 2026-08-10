using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    /// <summary>
    /// Extends UnitDefinition with AI movement, scan, chase, and attack references
    /// for Ally and Enemy units.
    /// </summary>
    [CreateAssetMenu(menuName = "Monsters vs Zombies/Units/AI Definition")]
    public sealed class AIUnitDefinition : UnitDefinition
    {
        [field: SerializeField] public float ChaseRange { get; private set; }
        [field: SerializeField] public AttackDefinition DefaultAttackDefinition { get; private set; }

        public override ValidationResult Validate()
        {
            ValidationResult result = base.Validate();

            if (Faction != UnitFaction.Ally && Faction != UnitFaction.Enemy)
            {
                result.AddError(
                    ValidationCode.InvalidFaction,
                    $"{name} must use the Ally or Enemy faction.");
            }

            if (!NumericValidation.IsPositiveFinite(ChaseRange))
            {
                result.AddError(
                    ValidationCode.InvalidPositiveValue,
                    $"{name}.{nameof(ChaseRange)} must be a positive finite value.");
            }

            if (DefaultAttackDefinition == null)
            {
                result.AddError(
                    ValidationCode.MissingReference,
                    $"{name} requires a default attack definition.");
            }
            else
            {
                result.Merge(DefaultAttackDefinition.Validate());
                if (NumericValidation.IsPositiveFinite(ChaseRange) &&
                    DefaultAttackDefinition.AttackRange > ChaseRange)
                {
                    result.AddError(
                        ValidationCode.AttackRangeExceedsChaseRange,
                        $"{name}'s default attack range cannot exceed its chase range.");
                }
            }

            return result;
        }
    }
}
