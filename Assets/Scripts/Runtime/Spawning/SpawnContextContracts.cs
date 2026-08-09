using MonstersVsZombies.Core;

namespace MonstersVsZombies.Spawning
{
    public readonly struct UnitSpawnContext
    {
        public UnitSpawnRequest Request { get; }
        public SpawnId SpawnId { get; }

        public UnitSpawnContext(UnitSpawnRequest request, SpawnId spawnId)
        {
            Request = request;
            SpawnId = spawnId;
        }
    }

    public interface IUnitSpawnContextReceiver
    {
        bool ConfigureUnitSpawn(UnitSpawnContext spawnContext);
    }

    public interface IProjectileSpawnLifecycle
    {
        bool ConfigureProjectileSpawn(ProjectileSpawnRequest spawnRequest);
        bool StartProjectile();
    }
}
