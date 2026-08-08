using System;
using MonstersVsZombies.Core;

namespace MonstersVsZombies.Core.Pooling
{
    public enum PoolCapacityPolicy
    {
        Expandable,
        HardActiveLimit
    }

    public enum PoolFailureReason
    {
        None,
        UnknownPool,
        CapacityReached,
        InvalidPrefab,
        CreationFailed,
        InitializationFailed,
        AlreadyReturned,
        ForeignEntity
    }

    public readonly struct PoolRentResult<T> where T : class
    {
        public PoolId PoolId { get; }
        public T Entity { get; }
        public PoolFailureReason FailureReason { get; }
        public bool IsSuccess => PoolId.IsValid && FailureReason == PoolFailureReason.None && Entity != null;

        private PoolRentResult(PoolId poolId, T entity, PoolFailureReason failureReason)
        {
            PoolId = poolId;
            Entity = entity;
            FailureReason = failureReason;
        }

        public static PoolRentResult<T> CreateSuccess(PoolId poolId, T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!poolId.IsValid)
            {
                throw new ArgumentException("A successful rent requires a valid pool ID.", nameof(poolId));
            }

            return new PoolRentResult<T>(poolId, entity, PoolFailureReason.None);
        }

        public static PoolRentResult<T> CreateFailure(PoolId poolId, PoolFailureReason failureReason)
        {
            if (!Enum.IsDefined(typeof(PoolFailureReason), failureReason) ||
                failureReason == PoolFailureReason.None)
            {
                throw new ArgumentException("A failed rent requires a failure reason.", nameof(failureReason));
            }

            return new PoolRentResult<T>(poolId, null, failureReason);
        }
    }

    public readonly struct PoolReturnResult
    {
        public PoolId PoolId { get; }
        public PoolFailureReason FailureReason { get; }
        public bool IsSuccess => PoolId.IsValid && FailureReason == PoolFailureReason.None;

        private PoolReturnResult(PoolId poolId, PoolFailureReason failureReason)
        {
            PoolId = poolId;
            FailureReason = failureReason;
        }

        public static PoolReturnResult CreateSuccess(PoolId poolId)
        {
            if (!poolId.IsValid)
            {
                throw new ArgumentException("A successful return requires a valid pool ID.", nameof(poolId));
            }

            return new PoolReturnResult(poolId, PoolFailureReason.None);
        }

        public static PoolReturnResult CreateFailure(PoolId poolId, PoolFailureReason failureReason)
        {
            if (!Enum.IsDefined(typeof(PoolFailureReason), failureReason) ||
                failureReason == PoolFailureReason.None)
            {
                throw new ArgumentException("A failed return requires a failure reason.", nameof(failureReason));
            }

            return new PoolReturnResult(poolId, failureReason);
        }
    }
}
