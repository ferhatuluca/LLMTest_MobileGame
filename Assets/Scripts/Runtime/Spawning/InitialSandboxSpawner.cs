using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Spawning
{
    [DisallowMultipleComponent]
    public sealed class InitialSandboxSpawner : MonoBehaviour
    {
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }

        public SpawnResult<UnitController> Spawn(
            UnitDefinition definition,
            Pose spawnPose)
        {
            if (SpawnManager == null)
            {
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.RentFailed);
            }

            return SpawnManager.SpawnUnit(new UnitSpawnRequest(
                definition,
                spawnPose.position,
                spawnPose.rotation,
                default,
                SpawnReason.Initial));
        }
    }
}
