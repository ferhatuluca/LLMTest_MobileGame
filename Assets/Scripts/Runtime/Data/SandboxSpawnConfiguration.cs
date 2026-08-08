using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    [Serializable]
    public sealed class InitialUnitSpawnEntry
    {
        [field: SerializeField] public AIUnitDefinition Definition { get; private set; }
        [field: SerializeField] public int Count { get; private set; }

        public ValidationResult Validate(string contextName)
        {
            ValidationResult result = new ValidationResult();
            if (Definition == null)
            {
                result.AddError(ValidationCode.MissingReference, $"{contextName} requires a unit definition.");
            }
            else
            {
                result.Merge(Definition.Validate());
            }

            if (Count < 0)
            {
                result.AddError(
                    ValidationCode.InvalidNonNegativeValue,
                    $"{contextName}.{nameof(Count)} cannot be negative.");
            }

            return result;
        }
    }

    [CreateAssetMenu(menuName = "Monsters vs Zombies/Sandbox/Spawn Configuration")]
    public sealed class SandboxSpawnConfiguration : ScriptableObject
    {
        [field: SerializeField] public PlayerUnitDefinition PlayerDefinition { get; private set; }
        [SerializeField] private InitialUnitSpawnEntry[] _initialUnits =
            Array.Empty<InitialUnitSpawnEntry>();

        public int InitialUnitCount => _initialUnits?.Length ?? 0;

        public InitialUnitSpawnEntry GetInitialUnit(int index)
        {
            return _initialUnits[index];
        }

        public ValidationResult Validate()
        {
            ValidationResult result = new ValidationResult();
            if (PlayerDefinition == null)
            {
                result.AddError(
                    ValidationCode.MissingReference,
                    $"{name} requires a Player unit definition.");
            }
            else
            {
                result.Merge(PlayerDefinition.Validate());
            }

            InitialUnitSpawnEntry[] initialUnits =
                _initialUnits ?? Array.Empty<InitialUnitSpawnEntry>();
            for (int entryIndex = 0; entryIndex < initialUnits.Length; entryIndex++)
            {
                InitialUnitSpawnEntry entry = initialUnits[entryIndex];
                if (entry == null)
                {
                    result.AddError(
                        ValidationCode.MissingReference,
                        $"{name} initial spawn entry {entryIndex} is missing.");
                    continue;
                }

                result.Merge(entry.Validate($"{name}.InitialUnits[{entryIndex}]"));
            }

            return result;
        }

        private void OnValidate()
        {
            ValidationReporter.Report(this, Validate());
        }
    }
}
