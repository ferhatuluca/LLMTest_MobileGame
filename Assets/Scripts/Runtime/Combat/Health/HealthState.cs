using System;

namespace MonstersVsZombies.Combat.Health
{
    public enum HealthChangeOutcome
    {
        None,
        Applied,
        InvalidAmount,
        NotInitialized,
        AlreadyDead,
        AlreadyFull
    }

    public readonly struct HealthChangeResult
    {
        public HealthChangeOutcome Outcome { get; }
        public float PreviousHealth { get; }
        public float CurrentHealth { get; }
        public float AppliedAmount { get; }
        public bool BecameDead { get; }
        public bool IsApplied => Outcome == HealthChangeOutcome.Applied;

        public HealthChangeResult(
            HealthChangeOutcome outcome,
            float previousHealth,
            float currentHealth,
            float appliedAmount,
            bool becameDead)
        {
            Outcome = outcome;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            AppliedAmount = appliedAmount;
            BecameDead = becameDead;
        }
    }

    /// <summary>
    /// Contains deterministic health math with clamping, one-shot death, and
    /// rejection of changes after death, independent of Unity callbacks.
    /// </summary>
    public sealed class HealthState
    {
        public float CurrentHealth { get; private set; }
        public float MaximumHealth { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsInitialized { get; private set; }

        public void Initialize(float maximumHealth)
        {
            if (!IsPositiveFinite(maximumHealth))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth), "Maximum health must be positive.");
            }

            MaximumHealth = maximumHealth;
            CurrentHealth = maximumHealth;
            IsAlive = true;
            IsInitialized = true;
        }

        public HealthChangeResult ApplyDamage(float amount)
        {
            if (!IsInitialized)
            {
                return CreateRejectedResult(HealthChangeOutcome.NotInitialized);
            }

            if (!IsAlive)
            {
                return CreateRejectedResult(HealthChangeOutcome.AlreadyDead);
            }

            if (!IsPositiveFinite(amount))
            {
                return CreateRejectedResult(HealthChangeOutcome.InvalidAmount);
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Math.Max(0f, CurrentHealth - amount);
            float appliedAmount = previousHealth - CurrentHealth;
            bool becameDead = CurrentHealth <= 0f;
            if (becameDead)
            {
                IsAlive = false;
            }

            return new HealthChangeResult(
                HealthChangeOutcome.Applied,
                previousHealth,
                CurrentHealth,
                appliedAmount,
                becameDead);
        }

        public HealthChangeResult ApplyHealing(float amount)
        {
            if (!IsInitialized)
            {
                return CreateRejectedResult(HealthChangeOutcome.NotInitialized);
            }

            if (!IsAlive)
            {
                return CreateRejectedResult(HealthChangeOutcome.AlreadyDead);
            }

            if (!IsPositiveFinite(amount))
            {
                return CreateRejectedResult(HealthChangeOutcome.InvalidAmount);
            }

            if (CurrentHealth >= MaximumHealth)
            {
                return CreateRejectedResult(HealthChangeOutcome.AlreadyFull);
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Math.Min(MaximumHealth, CurrentHealth + amount);
            return new HealthChangeResult(
                HealthChangeOutcome.Applied,
                previousHealth,
                CurrentHealth,
                CurrentHealth - previousHealth,
                false);
        }

        public void Reset()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Health must be initialized before it can be reset.");
            }

            CurrentHealth = MaximumHealth;
            IsAlive = true;
        }

        private HealthChangeResult CreateRejectedResult(HealthChangeOutcome outcome)
        {
            return new HealthChangeResult(outcome, CurrentHealth, CurrentHealth, 0f, false);
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
