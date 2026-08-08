namespace MonstersVsZombies.Core.Pooling
{
    public interface IPoolable
    {
        bool PrepareForSpawn();
        bool CompleteSpawn();
        void PrepareForReturn();
    }
}
