namespace MonstersVsZombies.Core.Pooling
{
    public readonly struct PoolDiagnostics
    {
        public PoolId PoolId { get; }
        public int CreatedCount { get; }
        public int ActiveCount { get; }
        public int InactiveCount { get; }
        public int PeakActiveCount { get; }
        public int FailedRentCount { get; }
        public int CapacityReachedCount { get; }
        public int OverflowDestroyCount { get; }
        public bool CollectionChecksEnabled { get; }

        public PoolDiagnostics(
            PoolId poolId,
            int createdCount,
            int activeCount,
            int inactiveCount,
            int peakActiveCount,
            int failedRentCount,
            int capacityReachedCount,
            int overflowDestroyCount,
            bool collectionChecksEnabled)
        {
            PoolId = poolId;
            CreatedCount = createdCount;
            ActiveCount = activeCount;
            InactiveCount = inactiveCount;
            PeakActiveCount = peakActiveCount;
            FailedRentCount = failedRentCount;
            CapacityReachedCount = capacityReachedCount;
            OverflowDestroyCount = overflowDestroyCount;
            CollectionChecksEnabled = collectionChecksEnabled;
        }
    }
}
