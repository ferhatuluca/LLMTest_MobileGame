using System;
using System.Collections.Generic;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    [Serializable]
    public sealed class PoolCatalogEntry
    {
        [field: SerializeField] public PoolId PoolId { get; private set; }
        [field: SerializeField] public GameObject Prefab { get; private set; }
        [field: SerializeField] public int InitialPrewarmCount { get; private set; }
        [field: SerializeField] public int MaximumInactiveRetainedCount { get; private set; }
        [field: SerializeField] public PoolCapacityPolicy CapacityPolicy { get; private set; }
        [field: SerializeField] public int MaximumActiveCount { get; private set; }
        [field: SerializeField] public bool EnableCollectionChecks { get; private set; }

        public ValidationResult Validate(string contextName)
        {
            ValidationResult result = new ValidationResult();
            if (!PoolId.IsValid)
            {
                result.AddError(ValidationCode.MissingId, $"{contextName} requires a pool ID.");
            }

            if (Prefab == null)
            {
                result.AddError(ValidationCode.MissingReference, $"{contextName} requires a prefab.");
            }

            if (InitialPrewarmCount < 0)
            {
                result.AddError(
                    ValidationCode.InvalidNonNegativeValue,
                    $"{contextName}.{nameof(InitialPrewarmCount)} cannot be negative.");
            }

            if (MaximumInactiveRetainedCount <= 0)
            {
                result.AddError(
                    ValidationCode.InvalidPositiveValue,
                    $"{contextName}.{nameof(MaximumInactiveRetainedCount)} must be positive.");
            }

            if (InitialPrewarmCount > MaximumInactiveRetainedCount)
            {
                result.AddError(
                    ValidationCode.PrewarmExceedsRetainedCount,
                    $"{contextName} prewarm cannot exceed maximum inactive retained count.");
            }

            if (!Enum.IsDefined(typeof(PoolCapacityPolicy), CapacityPolicy))
            {
                result.AddError(
                    ValidationCode.InvalidCapacityPolicy,
                    $"{contextName} has an invalid pool capacity policy.");
            }
            else if (CapacityPolicy == PoolCapacityPolicy.HardActiveLimit && MaximumActiveCount <= 0)
            {
                result.AddError(
                    ValidationCode.InvalidActiveLimit,
                    $"{contextName} requires a positive hard active limit.");
            }
            else if (CapacityPolicy == PoolCapacityPolicy.Expandable && MaximumActiveCount != 0)
            {
                result.AddError(
                    ValidationCode.InvalidActiveLimit,
                    $"{contextName}.{nameof(MaximumActiveCount)} must be zero for an expandable pool.");
            }

            return result;
        }
    }

    [CreateAssetMenu(menuName = "Monsters vs Zombies/Catalogs/Pool Catalog")]
    public sealed class PoolCatalog : ScriptableObject
    {
        [SerializeField] private PoolCatalogEntry[] _entries = Array.Empty<PoolCatalogEntry>();

        public int Count => _entries?.Length ?? 0;

        public PoolCatalogEntry GetEntry(int index)
        {
            return _entries[index];
        }

        public bool TryGetEntry(PoolId poolId, out PoolCatalogEntry matchingEntry)
        {
            PoolCatalogEntry[] entries = _entries ?? Array.Empty<PoolCatalogEntry>();
            foreach (PoolCatalogEntry entry in entries)
            {
                if (entry != null && entry.PoolId == poolId)
                {
                    matchingEntry = entry;
                    return true;
                }
            }

            matchingEntry = null;
            return false;
        }

        public ValidationResult Validate()
        {
            ValidationResult result = new ValidationResult();
            HashSet<PoolId> seenPoolIds = new HashSet<PoolId>();

            PoolCatalogEntry[] entries = _entries ?? Array.Empty<PoolCatalogEntry>();
            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                PoolCatalogEntry entry = entries[entryIndex];
                if (entry == null)
                {
                    result.AddError(
                        ValidationCode.MissingReference,
                        $"{name} pool entry {entryIndex} is missing.");
                    continue;
                }

                result.Merge(entry.Validate($"{name}.Entries[{entryIndex}]"));
                if (entry.PoolId.IsValid && !seenPoolIds.Add(entry.PoolId))
                {
                    result.AddError(
                        ValidationCode.DuplicatePoolId,
                        $"{name} contains duplicate pool ID '{entry.PoolId}'.");
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
