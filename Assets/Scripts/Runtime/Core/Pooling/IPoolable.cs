namespace MonstersVsZombies.Core.Pooling
{
    /// <summary>
    /// Defines the two-phase spawn reset and synchronous return cleanup contract
    /// used by PooledEntity to make reuse deterministic.
    /// </summary>
    public interface IPoolable
    {
        bool PrepareForSpawn();
        bool CompleteSpawn();
        void PrepareForReturn();
    }
}
