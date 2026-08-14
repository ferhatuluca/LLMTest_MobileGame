namespace MonstersVsZombies.Core.Pooling
{
    public readonly struct PoolDiagnostics
    {
        public PoolId PoolId { get; }
        public int CreatedCount { get; }
        public int ActiveCount { get; }
        public int InactiveCount { get; }

        public PoolDiagnostics(
            PoolId poolId,
            int createdCount,
            int activeCount,
            int inactiveCount)
        {
            PoolId = poolId;
            CreatedCount = createdCount;
            ActiveCount = activeCount;
            InactiveCount = inactiveCount;
        }
    }
}
