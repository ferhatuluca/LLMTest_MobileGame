using System;
using System.Collections.Generic;
using MonstersVsZombies.Core;
using UnityEngine;

namespace MonstersVsZombies.Units
{
    public readonly struct UnitRegistryEvent
    {
        public UnitController Unit { get; }
        public SpawnId SpawnId { get; }
        public UnitFaction Faction { get; }

        public UnitRegistryEvent(UnitController unit, SpawnId spawnId, UnitFaction faction)
        {
            Unit = unit;
            SpawnId = spawnId;
            Faction = faction;
        }
    }

    /// <summary>
    /// Tracks only logically active units by SpawnId and faction, providing
    /// allocation-conscious snapshots for targeting and diagnostics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitRegistry : MonoBehaviour
    {
        private readonly Dictionary<SpawnId, RegistryEntry> _unitsBySpawnId =
            new Dictionary<SpawnId, RegistryEntry>();
        private readonly HashSet<UnitController> _registeredUnits =
            new HashSet<UnitController>();
        private int _playerCount;
        private int _allyCount;
        private int _enemyCount;

        public event Action<UnitRegistryEvent> UnitRegistered;
        public event Action<UnitRegistryEvent> UnitRemoved;

        public int Count => _unitsBySpawnId.Count;

        private void OnDestroy()
        {
            foreach (RegistryEntry registryEntry in _unitsBySpawnId.Values)
            {
                UnitController unitController = registryEntry.Unit;
                if (unitController != null && unitController.LifecycleController != null)
                {
                    unitController.LifecycleController.Despawned -= HandleUnitDespawned;
                }
            }

            _unitsBySpawnId.Clear();
            _registeredUnits.Clear();
            _playerCount = 0;
            _allyCount = 0;
            _enemyCount = 0;
        }

        public bool Register(UnitController unitController)
        {
            if (unitController == null || !unitController.IsActive ||
                !unitController.SpawnId.IsValid ||
                !Enum.IsDefined(typeof(UnitFaction), unitController.Faction) ||
                unitController.LifecycleController == null ||
                _registeredUnits.Contains(unitController) ||
                _unitsBySpawnId.ContainsKey(unitController.SpawnId))
            {
                return false;
            }

            SpawnId spawnId = unitController.SpawnId;
            UnitFaction faction = unitController.Faction;
            _unitsBySpawnId.Add(spawnId, new RegistryEntry(unitController, faction));
            _registeredUnits.Add(unitController);
            IncrementFactionCount(faction);

            UnitLifecycleController lifecycleController = unitController.LifecycleController;
            lifecycleController.Despawned += HandleUnitDespawned;
            lifecycleController.RegisterSpawnSubscription(
                () => lifecycleController.Despawned -= HandleUnitDespawned);

            UnitRegistered?.Invoke(new UnitRegistryEvent(unitController, spawnId, faction));
            return true;
        }

        public bool Remove(SpawnId spawnId, UnitController expectedUnit)
        {
            if (expectedUnit == null || expectedUnit.IsActive ||
                !_unitsBySpawnId.TryGetValue(spawnId, out RegistryEntry registryEntry) ||
                registryEntry.Unit != expectedUnit)
            {
                return false;
            }

            UnitController unitController = registryEntry.Unit;
            UnitFaction faction = registryEntry.Faction;
            _unitsBySpawnId.Remove(spawnId);
            if (unitController != null)
            {
                _registeredUnits.Remove(unitController);
                if (unitController.LifecycleController != null)
                {
                    unitController.LifecycleController.Despawned -= HandleUnitDespawned;
                }
            }

            DecrementFactionCount(faction);
            UnitRemoved?.Invoke(new UnitRegistryEvent(unitController, spawnId, faction));
            return true;
        }

        public bool TryGetUnit(SpawnId spawnId, out UnitController unitController)
        {
            if (_unitsBySpawnId.TryGetValue(spawnId, out RegistryEntry registryEntry))
            {
                unitController = registryEntry.Unit;
                return true;
            }

            unitController = null;
            return false;
        }

        public int GetFactionCount(UnitFaction faction)
        {
            switch (faction)
            {
                case UnitFaction.Player:
                    return _playerCount;
                case UnitFaction.Ally:
                    return _allyCount;
                case UnitFaction.Enemy:
                    return _enemyCount;
                default:
                    return 0;
            }
        }

        public int CopySnapshot(List<UnitController> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (RegistryEntry registryEntry in _unitsBySpawnId.Values)
            {
                destination.Add(registryEntry.Unit);
            }

            destination.Sort(UnitSpawnIdComparer.Instance);
            return destination.Count;
        }

        public int CopyFactionSnapshot(UnitFaction faction, List<UnitController> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (RegistryEntry registryEntry in _unitsBySpawnId.Values)
            {
                if (registryEntry.Unit != null && registryEntry.Faction == faction)
                {
                    destination.Add(registryEntry.Unit);
                }
            }

            destination.Sort(UnitSpawnIdComparer.Instance);
            return destination.Count;
        }

        private void HandleUnitDespawned(UnitLifecycleChangedEvent lifecycleEvent)
        {
            Remove(lifecycleEvent.SpawnId, lifecycleEvent.Unit);
        }

        private void IncrementFactionCount(UnitFaction faction)
        {
            switch (faction)
            {
                case UnitFaction.Player:
                    _playerCount++;
                    break;
                case UnitFaction.Ally:
                    _allyCount++;
                    break;
                case UnitFaction.Enemy:
                    _enemyCount++;
                    break;
            }
        }

        private void DecrementFactionCount(UnitFaction faction)
        {
            switch (faction)
            {
                case UnitFaction.Player:
                    _playerCount = Mathf.Max(0, _playerCount - 1);
                    break;
                case UnitFaction.Ally:
                    _allyCount = Mathf.Max(0, _allyCount - 1);
                    break;
                case UnitFaction.Enemy:
                    _enemyCount = Mathf.Max(0, _enemyCount - 1);
                    break;
            }
        }

        private sealed class UnitSpawnIdComparer : IComparer<UnitController>
        {
            public static UnitSpawnIdComparer Instance { get; } = new UnitSpawnIdComparer();

            public int Compare(UnitController left, UnitController right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                return left.SpawnId.CompareTo(right.SpawnId);
            }
        }

        private readonly struct RegistryEntry
        {
            public UnitController Unit { get; }
            public UnitFaction Faction { get; }

            public RegistryEntry(UnitController unit, UnitFaction faction)
            {
                Unit = unit;
                Faction = faction;
            }
        }
    }
}
