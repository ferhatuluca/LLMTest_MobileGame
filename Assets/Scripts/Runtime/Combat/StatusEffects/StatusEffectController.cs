using System;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.StatusEffects
{
    public readonly struct StatusEffectChangedEvent
    {
        public UnitController Unit { get; }
        public StatusEffectType EffectType { get; }
        public bool IsActive { get; }
        public float RemainingDuration { get; }

        public StatusEffectChangedEvent(
            UnitController unit,
            StatusEffectType effectType,
            bool isActive,
            float remainingDuration)
        {
            Unit = unit;
            EffectType = effectType;
            IsActive = isActive;
            RemainingDuration = remainingDuration;
        }
    }

    /// <summary>
    /// Owns active timed status effects. Stun refreshes to the longer remaining
    /// duration and exposes movement, chase, and attack permission gates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StatusEffectController : MonoBehaviour
    {
        private UnitController _unitController;
        private HealthController _healthController;

        public event Action<StatusEffectChangedEvent> StatusEffectChanged;

        public float RemainingStunDuration { get; private set; }
        public bool IsStunned => RemainingStunDuration > 0f;
        public bool IsMovementBlocked => IsStunned;
        public bool IsChaseBlocked => IsStunned;
        public bool IsAttackBlocked => IsStunned;

        private void Awake()
        {
            CacheSiblingComponents();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        internal bool ApplyAcceptedEffect(StatusEffectPayload effectPayload)
        {
            CacheSiblingComponents();
            if (!effectPayload.IsValid || effectPayload.Type != StatusEffectType.Stun ||
                _unitController == null || !_unitController.IsActive ||
                _healthController == null || !_healthController.IsAlive)
            {
                return false;
            }

            RemainingStunDuration = Mathf.Max(RemainingStunDuration, effectPayload.Duration);
            StatusEffectChanged?.Invoke(new StatusEffectChangedEvent(
                _unitController,
                StatusEffectType.Stun,
                true,
                RemainingStunDuration));
            return true;
        }

        internal void AdvanceTime(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    "Status-effect time must be non-negative and finite.");
            }

            if (!IsStunned || deltaTime <= 0f)
            {
                return;
            }

            RemainingStunDuration = Mathf.Max(0f, RemainingStunDuration - deltaTime);
            if (!IsStunned)
            {
                StatusEffectChanged?.Invoke(new StatusEffectChangedEvent(
                    _unitController,
                    StatusEffectType.Stun,
                    false,
                    0f));
            }
        }

        internal void ResetForSpawn()
        {
            RemainingStunDuration = 0f;
            CacheSiblingComponents();
        }

        internal void ClearForDeath()
        {
            ClearEffects(true);
        }

        internal void ClearForReturn()
        {
            ClearEffects(false);
        }

        private void ClearEffects(bool publishChange)
        {
            bool wasStunned = IsStunned;
            RemainingStunDuration = 0f;
            if (wasStunned && publishChange)
            {
                StatusEffectChanged?.Invoke(new StatusEffectChangedEvent(
                    _unitController,
                    StatusEffectType.Stun,
                    false,
                    0f));
            }
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
            _healthController = GetComponent<HealthController>();
        }
    }
}
