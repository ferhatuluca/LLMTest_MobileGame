using MonstersVsZombies.Core;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    /// <summary>
    /// Links a Player-selectable weapon identity to its visible prefab, socket,
    /// and AttackDefinition.
    /// </summary>
    [CreateAssetMenu(menuName = "Monsters vs Zombies/Combat/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [field: SerializeField] public WeaponId WeaponId { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public AttackDefinition AttackDefinition { get; private set; }
        [field: SerializeField] public GameObject WeaponVisualPrefab { get; private set; }
        [field: SerializeField] public string MuzzleSocketName { get; private set; }

        public ValidationResult Validate()
        {
            ValidationResult result = new ValidationResult();
            if (!WeaponId.IsValid)
            {
                result.AddError(ValidationCode.MissingId, $"{name} requires a stable weapon ID.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                result.AddError(ValidationCode.MissingDisplayName, $"{name} requires a display name.");
            }

            if (AttackDefinition == null)
            {
                result.AddError(
                    ValidationCode.MissingReference,
                    $"{name} requires an attack definition.");
            }
            else
            {
                result.Merge(AttackDefinition.Validate());
            }

            if (WeaponVisualPrefab == null)
            {
                result.AddError(
                    ValidationCode.MissingReference,
                    $"{name} requires a nested weapon visual prefab.");
            }

            if (string.IsNullOrWhiteSpace(MuzzleSocketName))
            {
                result.AddError(
                    ValidationCode.MissingSocketName,
                    $"{name} requires a muzzle socket name.");
            }

            return result;
        }

        private void OnValidate()
        {
            ValidationReporter.Report(this, Validate());
        }
    }
}
