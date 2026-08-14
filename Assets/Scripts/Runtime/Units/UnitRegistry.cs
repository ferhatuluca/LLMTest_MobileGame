using System;
using System.Collections.Generic;
using MonstersVsZombies.Core;
using UnityEngine;

namespace MonstersVsZombies.Units
{
    /// <summary>
    /// Keeps the active units used by gameplay and developer controls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitRegistry : MonoBehaviour
    {
        private readonly Dictionary<SpawnId, UnitController> _units =
            new Dictionary<SpawnId, UnitController>();

        public int Count => _units.Count;

        private void OnDestroy()
        {
            foreach (UnitController unit in _units.Values)
            {
                if (unit?.LifecycleController != null)
                {
                    unit.LifecycleController.Despawned -=
                        HandleUnitDespawned;
                }
            }

            _units.Clear();
        }

        public bool Register(UnitController unit)
        {
            if (unit == null || !unit.IsActive || !unit.SpawnId.IsValid ||
                unit.LifecycleController == null ||
                _units.ContainsKey(unit.SpawnId) ||
                _units.ContainsValue(unit))
            {
                return false;
            }

            _units.Add(unit.SpawnId, unit);
            unit.LifecycleController.Despawned += HandleUnitDespawned;
            return true;
        }

        public bool Remove(SpawnId spawnId, UnitController expectedUnit)
        {
            if (expectedUnit == null ||
                !_units.TryGetValue(spawnId, out UnitController unit) ||
                unit != expectedUnit)
            {
                return false;
            }

            _units.Remove(spawnId);
            if (unit.LifecycleController != null)
            {
                unit.LifecycleController.Despawned -= HandleUnitDespawned;
            }

            return true;
        }

        public int GetFactionCount(UnitFaction faction)
        {
            int count = 0;
            foreach (UnitController unit in _units.Values)
            {
                if (unit != null && unit.Faction == faction)
                {
                    count++;
                }
            }

            return count;
        }

        public int CopySnapshot(List<UnitController> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            destination.AddRange(_units.Values);
            destination.Sort((left, right) =>
                left.SpawnId.CompareTo(right.SpawnId));
            return destination.Count;
        }

        private void HandleUnitDespawned(
            UnitLifecycleChangedEvent lifecycleEvent)
        {
            Remove(lifecycleEvent.SpawnId, lifecycleEvent.Unit);
        }
    }
}
