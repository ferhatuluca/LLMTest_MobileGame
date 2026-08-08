using System;

namespace MonstersVsZombies.Spawning
{
    public enum SpawnReason
    {
        Initial,
        Debug,
        DeathEffect,
        Gameplay
    }

    public enum SpawnFailureReason
    {
        None,
        InvalidDefinition,
        UnknownPool,
        InvalidPosition,
        CapacityReached,
        RentFailed,
        ActivationIndependentInitializationFailed,
        ActivationDependentInitializationFailed
    }

    public readonly struct SpawnResult<T> where T : class
    {
        public T Entity { get; }
        public SpawnFailureReason FailureReason { get; }
        public bool IsSuccess => FailureReason == SpawnFailureReason.None && Entity != null;

        private SpawnResult(T entity, SpawnFailureReason failureReason)
        {
            Entity = entity;
            FailureReason = failureReason;
        }

        public static SpawnResult<T> CreateSuccess(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return new SpawnResult<T>(entity, SpawnFailureReason.None);
        }

        public static SpawnResult<T> CreateFailure(SpawnFailureReason failureReason)
        {
            if (!Enum.IsDefined(typeof(SpawnFailureReason), failureReason) ||
                failureReason == SpawnFailureReason.None)
            {
                throw new ArgumentException("A failed spawn requires a failure reason.", nameof(failureReason));
            }

            return new SpawnResult<T>(null, failureReason);
        }
    }
}
