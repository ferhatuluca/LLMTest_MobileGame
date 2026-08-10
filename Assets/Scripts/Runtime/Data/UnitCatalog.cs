using System;
using System.Collections.Generic;
using MonstersVsZombies.Core;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    [Serializable]
    public sealed class UnitCatalogEntry
    {
        [field: SerializeField] public UnitDefinition Definition { get; private set; }

        public UnitId UnitId => Definition == null ? default : Definition.UnitId;

        public ValidationResult Validate(string contextName)
        {
            ValidationResult result = new ValidationResult();
            if (Definition == null)
            {
                result.AddError(ValidationCode.MissingReference, $"{contextName} requires a definition.");
            }
            else
            {
                result.Merge(Definition.Validate());
            }

            return result;
        }
    }

    /// <summary>
    /// Maps stable UnitIds to validated UnitDefinitions for spawning and debug tools.
    /// </summary>
    [CreateAssetMenu(menuName = "Monsters vs Zombies/Catalogs/Unit Catalog")]
    public sealed class UnitCatalog : ScriptableObject
    {
        [SerializeField] private UnitCatalogEntry[] _entries = Array.Empty<UnitCatalogEntry>();

        public int Count => _entries?.Length ?? 0;

        public UnitCatalogEntry GetEntry(int index)
        {
            return _entries[index];
        }

        public bool TryGetDefinition(UnitId unitId, out UnitDefinition definition)
        {
            UnitCatalogEntry[] entries = _entries ?? Array.Empty<UnitCatalogEntry>();
            foreach (UnitCatalogEntry entry in entries)
            {
                if (entry != null && entry.UnitId == unitId)
                {
                    definition = entry.Definition;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public ValidationResult Validate()
        {
            ValidationResult result = new ValidationResult();
            HashSet<UnitId> seenUnitIds = new HashSet<UnitId>();

            UnitCatalogEntry[] entries = _entries ?? Array.Empty<UnitCatalogEntry>();
            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                UnitCatalogEntry entry = entries[entryIndex];
                if (entry == null)
                {
                    result.AddError(
                        ValidationCode.MissingReference,
                        $"{name} unit entry {entryIndex} is missing.");
                    continue;
                }

                result.Merge(entry.Validate($"{name}.Entries[{entryIndex}]"));
                if (entry.UnitId.IsValid && !seenUnitIds.Add(entry.UnitId))
                {
                    result.AddError(
                        ValidationCode.DuplicateUnitId,
                        $"{name} contains duplicate unit ID '{entry.UnitId}'.");
                }
            }

            return result;
        }

        private void OnValidate()
        {
            ValidationReporter.Report(this, Validate());
        }
    }
}
