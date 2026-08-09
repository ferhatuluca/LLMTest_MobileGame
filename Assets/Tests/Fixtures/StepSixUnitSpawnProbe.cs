using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSixUnitSpawnProbe : MonoBehaviour,
        IPoolable,
        IUnitSpawnContextReceiver
    {
        [field: SerializeField] public bool FailContext { get; private set; }
        [field: SerializeField] public bool FailPrepare { get; private set; }
        [field: SerializeField] public bool FailComplete { get; private set; }
        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }

        public UnitDefinition CapturedDefinition { get; private set; }
        public UnitFaction CapturedFaction { get; private set; }
        public SpawnId CapturedSpawnId { get; private set; }
        public SpawnId CapturedSourceSpawnId { get; private set; }
        public SpawnReason CapturedReason { get; private set; }
        public Vector3 CapturedPosition { get; private set; }
        public Quaternion CapturedRotation { get; private set; }
        public bool ContextObservedInactive { get; private set; }
        public bool PrepareObservedInactive { get; private set; }
        public bool EnableObservedLogicalInactive { get; private set; }
        public bool CompleteObservedLogicalInactive { get; private set; }
        public bool CompleteObservedUnregistered { get; private set; }
        public int GameplayActionCount { get; private set; }

        private void OnEnable()
        {
            UnitController unitController = GetComponent<UnitController>();
            EnableObservedLogicalInactive =
                unitController != null && !unitController.IsActive;
            if (unitController != null && unitController.IsActive)
            {
                GameplayActionCount++;
            }
        }

        bool IUnitSpawnContextReceiver.ConfigureUnitSpawn(
            UnitSpawnContext spawnContext)
        {
            UnitController unitController = GetComponent<UnitController>();
            CapturedDefinition = spawnContext.Request.Definition;
            CapturedFaction = unitController == null
                ? default
                : unitController.Faction;
            CapturedSpawnId = spawnContext.SpawnId;
            CapturedSourceSpawnId = spawnContext.Request.SourceSpawnId;
            CapturedReason = spawnContext.Request.Reason;
            CapturedPosition = transform.position;
            CapturedRotation = transform.rotation;
            ContextObservedInactive = !gameObject.activeInHierarchy;
            return !FailContext &&
                   unitController != null &&
                   unitController.Definition == spawnContext.Request.Definition &&
                   unitController.SpawnId == spawnContext.SpawnId;
        }

        public bool PrepareForSpawn()
        {
            PrepareObservedInactive = !gameObject.activeInHierarchy;
            GameplayActionCount = 0;
            return !FailPrepare;
        }

        public bool CompleteSpawn()
        {
            UnitController unitController = GetComponent<UnitController>();
            CompleteObservedLogicalInactive =
                unitController != null && !unitController.IsActive;
            CompleteObservedUnregistered =
                UnitRegistry != null && UnitRegistry.Count == 0;
            if (unitController != null && unitController.IsActive)
            {
                GameplayActionCount++;
            }

            return !FailComplete;
        }

        public void PrepareForReturn()
        {
            GameplayActionCount = 0;
            FailContext = false;
            FailPrepare = false;
            FailComplete = false;
        }
    }
}
