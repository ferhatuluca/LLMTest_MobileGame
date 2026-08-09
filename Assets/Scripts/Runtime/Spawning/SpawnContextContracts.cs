using MonstersVsZombies.Core;
using MonstersVsZombies.Combat.Interaction;

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

    public interface IProjectileSpawnRuntimeContextReceiver
    {
        bool ConfigureProjectileRuntime(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem);
    }
}
