using Unity.Profiling;

namespace MonstersVsZombies.Diagnostics
{
    public enum SandboxPerformanceSubsystem
    {
        Targeting,
        AI,
        Attack,
        Projectile,
        PoolRent,
        PoolReturn
    }

    public readonly struct SandboxAllocationCounter
    {
        public long SampleCount { get; }
        public long AllocatingSampleCount { get; }
        public long AllocatedBytes { get; }
        public long MaximumSampleBytes { get; }

        public SandboxAllocationCounter(
            long sampleCount,
            long allocatingSampleCount,
            long allocatedBytes,
            long maximumSampleBytes)
        {
            SampleCount = sampleCount;
            AllocatingSampleCount = allocatingSampleCount;
            AllocatedBytes = allocatedBytes;
            MaximumSampleBytes = maximumSampleBytes;
        }
    }

    public readonly struct SandboxAllocationSnapshot
    {
        public SandboxAllocationCounter Targeting { get; }
        public SandboxAllocationCounter AI { get; }
        public SandboxAllocationCounter Attack { get; }
        public SandboxAllocationCounter Projectile { get; }
        public SandboxAllocationCounter PoolRent { get; }
        public SandboxAllocationCounter PoolReturn { get; }

        public SandboxAllocationSnapshot(
            SandboxAllocationCounter targeting,
            SandboxAllocationCounter ai,
            SandboxAllocationCounter attack,
            SandboxAllocationCounter projectile,
            SandboxAllocationCounter poolRent,
            SandboxAllocationCounter poolReturn)
        {
            Targeting = targeting;
            AI = ai;
            Attack = attack;
            Projectile = projectile;
            PoolRent = poolRent;
            PoolReturn = poolReturn;
        }

        public long GameplayAllocatedBytes =>
            Targeting.AllocatedBytes +
            AI.AllocatedBytes +
            Attack.AllocatedBytes +
            Projectile.AllocatedBytes +
            PoolRent.AllocatedBytes +
            PoolReturn.AllocatedBytes;
    }

    public static class SandboxPerformanceDiagnostics
    {
        internal static readonly ProfilerMarker TargetingMarker =
            new ProfilerMarker("MVZ.Targeting.Update");
        internal static readonly ProfilerMarker AIMarker =
            new ProfilerMarker("MVZ.AI.Update");
        internal static readonly ProfilerMarker AttackMarker =
            new ProfilerMarker("MVZ.Attack.Update");
        internal static readonly ProfilerMarker ProjectileMarker =
            new ProfilerMarker("MVZ.Projectile.Update");
        internal static readonly ProfilerMarker PoolRentMarker =
            new ProfilerMarker("MVZ.Pool.Rent");
        internal static readonly ProfilerMarker PoolReturnMarker =
            new ProfilerMarker("MVZ.Pool.Return");

        private static MutableAllocationCounter s_targeting;
        private static MutableAllocationCounter s_ai;
        private static MutableAllocationCounter s_attack;
        private static MutableAllocationCounter s_projectile;
        private static MutableAllocationCounter s_poolRent;
        private static MutableAllocationCounter s_poolReturn;

        public static void ResetAllocations()
        {
            s_targeting = default;
            s_ai = default;
            s_attack = default;
            s_projectile = default;
            s_poolRent = default;
            s_poolReturn = default;
        }

        public static SandboxAllocationSnapshot GetAllocationSnapshot()
        {
            return new SandboxAllocationSnapshot(
                s_targeting.GetSnapshot(),
                s_ai.GetSnapshot(),
                s_attack.GetSnapshot(),
                s_projectile.GetSnapshot(),
                s_poolRent.GetSnapshot(),
                s_poolReturn.GetSnapshot());
        }

        internal static void RecordAllocation(
            SandboxPerformanceSubsystem subsystem,
            long allocatedBytes)
        {
            if (allocatedBytes < 0)
            {
                return;
            }

            switch (subsystem)
            {
                case SandboxPerformanceSubsystem.Targeting:
                    s_targeting.Record(allocatedBytes);
                    break;
                case SandboxPerformanceSubsystem.AI:
                    s_ai.Record(allocatedBytes);
                    break;
                case SandboxPerformanceSubsystem.Attack:
                    s_attack.Record(allocatedBytes);
                    break;
                case SandboxPerformanceSubsystem.Projectile:
                    s_projectile.Record(allocatedBytes);
                    break;
                case SandboxPerformanceSubsystem.PoolRent:
                    s_poolRent.Record(allocatedBytes);
                    break;
                case SandboxPerformanceSubsystem.PoolReturn:
                    s_poolReturn.Record(allocatedBytes);
                    break;
            }
        }

        private struct MutableAllocationCounter
        {
            private long _sampleCount;
            private long _allocatingSampleCount;
            private long _allocatedBytes;
            private long _maximumSampleBytes;

            public void Record(long allocatedBytes)
            {
                _sampleCount++;
                if (allocatedBytes <= 0)
                {
                    return;
                }

                _allocatingSampleCount++;
                _allocatedBytes += allocatedBytes;
                if (allocatedBytes > _maximumSampleBytes)
                {
                    _maximumSampleBytes = allocatedBytes;
                }
            }

            public SandboxAllocationCounter GetSnapshot()
            {
                return new SandboxAllocationCounter(
                    _sampleCount,
                    _allocatingSampleCount,
                    _allocatedBytes,
                    _maximumSampleBytes);
            }
        }
    }
}
