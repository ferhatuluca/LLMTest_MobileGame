using System;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Health
{
    public readonly struct HealthChangedEvent
    {
        public UnitController Unit { get; }
        public float PreviousHealth { get; }
        public float CurrentHealth { get; }
        public float MaximumHealth { get; }
        public float AppliedAmount { get; }

        public HealthChangedEvent(
            UnitController unit,
            float previousHealth,
            float currentHealth,
            float maximumHealth,
            float appliedAmount)
        {
            Unit = unit;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            AppliedAmount = appliedAmount;
        }
    }

    public readonly struct UnitDeathEvent
    {
        public UnitController Unit { get; }
        public SpawnId SpawnId { get; }

        public UnitDeathEvent(UnitController unit, SpawnId spawnId)
        {
            Unit = unit;
            SpawnId = spawnId;
        }
    }

    [DisallowMultipleComponent]
    public sealed class HealthController : MonoBehaviour
    {
        private readonly HealthState _healthState = new HealthState();
        private UnitController _unitController;

        public event Action<HealthChangedEvent> HealthChanged;
        public event Action<UnitDeathEvent> Died;

        public float CurrentHealth => _healthState.CurrentHealth;
        public float MaximumHealth => _healthState.MaximumHealth;
        public bool IsAlive => _healthState.IsAlive;
        public bool IsInitialized => _healthState.IsInitialized;

        private void Awake()
        {
            CacheSiblingComponents();
        }

        internal bool InitializeForSpawn(UnitDefinition definition)
        {
            CacheSiblingComponents();
            if (_unitController == null || definition == null ||
                !NumericValidation.IsPositiveFinite(definition.MaximumHealth))
            {
                return false;
            }

            _healthState.Initialize(definition.MaximumHealth);
            return true;
        }

        internal HealthChangeResult ApplyDamage(float amount)
        {
            HealthChangeResult result = _healthState.ApplyDamage(amount);
            if (!result.IsApplied)
            {
                return result;
            }

            HealthChangedEvent healthChangedEvent = new HealthChangedEvent(
                _unitController,
                result.PreviousHealth,
                result.CurrentHealth,
                _healthState.MaximumHealth,
                result.AppliedAmount);
            HealthChanged?.Invoke(healthChangedEvent);

            if (result.BecameDead)
            {
                Died?.Invoke(new UnitDeathEvent(_unitController, _unitController.SpawnId));
            }

            return result;
        }

        internal HealthChangeResult ApplyHealing(float amount)
        {
            HealthChangeResult result = _healthState.ApplyHealing(amount);
            if (!result.IsApplied)
            {
                return result;
            }

            HealthChanged?.Invoke(new HealthChangedEvent(
                _unitController,
                result.PreviousHealth,
                result.CurrentHealth,
                _healthState.MaximumHealth,
                result.AppliedAmount));
            return result;
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
        }
    }
}
