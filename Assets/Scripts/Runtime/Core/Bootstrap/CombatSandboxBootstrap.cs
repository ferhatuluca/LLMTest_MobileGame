using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Core.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class CombatSandboxBootstrap : MonoBehaviour
    {
        [field: SerializeField] public PoolCatalog PoolCatalog { get; private set; }
        [field: SerializeField] public UnitCatalog UnitCatalog { get; private set; }
        [field: SerializeField] public PlayerUnitDefinition StationaryFixtureDefinition { get; private set; }
        [field: SerializeField] public PoolManager PoolManager { get; private set; }
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }
        [field: SerializeField] public InitialSandboxSpawner InitialSandboxSpawner { get; private set; }
        [field: SerializeField] public SpawnPointGroup PlayerSpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup AllySpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup EnemySpawnPoints { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsGameplayEnabled { get; private set; }
        public string LastFailureMessage { get; private set; } = string.Empty;
        public UnitController InitialUnit { get; private set; }

        private void Awake()
        {
            if (!InitializeServices())
            {
                DisableSandbox(LastFailureMessage);
            }
        }

        private void Start()
        {
            if (IsInitialized && !SpawnInitialFixture())
            {
                DisableSandbox(LastFailureMessage);
                return;
            }

            if (IsInitialized && InitialUnit != null)
            {
                Debug.Log(
                    "[CombatSandboxBootstrap] Services initialized and the initial stationary fixture spawned.",
                    this);
            }
        }

        public bool InitializeServices()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (!ValidateReferences(out string failureMessage))
            {
                LastFailureMessage = failureMessage;
                return false;
            }

            if (!PoolManager.Initialize(PoolCatalog, out failureMessage))
            {
                LastFailureMessage = failureMessage;
                return false;
            }

            if (!SpawnManager.Initialize(
                    PoolManager,
                    UnitRegistry,
                    out failureMessage) ||
                !InitialSandboxSpawner.Configure(SpawnManager))
            {
                LastFailureMessage = string.IsNullOrWhiteSpace(failureMessage)
                    ? "InitialSandboxSpawner configuration failed."
                    : failureMessage;
                return false;
            }

            IsInitialized = true;
            IsGameplayEnabled = true;
            LastFailureMessage = string.Empty;
            return true;
        }

        public bool SpawnInitialFixture()
        {
            if (!IsInitialized || !PlayerSpawnPoints.TryGetNext(out Pose spawnPose))
            {
                LastFailureMessage =
                    "The initial stationary fixture requires an initialized sandbox and Player spawn point.";
                return false;
            }

            SpawnResult<UnitController> spawnResult =
                InitialSandboxSpawner.Spawn(
                    StationaryFixtureDefinition,
                    spawnPose);
            if (!spawnResult.IsSuccess)
            {
                LastFailureMessage =
                    $"Initial stationary fixture spawn failed: {spawnResult.FailureReason}.";
                return false;
            }

            InitialUnit = spawnResult.Entity;
            LastFailureMessage = string.Empty;
            return true;
        }

        internal void Configure(
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog,
            PlayerUnitDefinition stationaryFixtureDefinition,
            PoolManager poolManager,
            SpawnManager spawnManager,
            InteractionSystem interactionSystem,
            UnitRegistry unitRegistry,
            InitialSandboxSpawner initialSandboxSpawner,
            SpawnPointGroup playerSpawnPoints,
            SpawnPointGroup allySpawnPoints,
            SpawnPointGroup enemySpawnPoints)
        {
            PoolCatalog = poolCatalog;
            UnitCatalog = unitCatalog;
            StationaryFixtureDefinition = stationaryFixtureDefinition;
            PoolManager = poolManager;
            SpawnManager = spawnManager;
            InteractionSystem = interactionSystem;
            UnitRegistry = unitRegistry;
            InitialSandboxSpawner = initialSandboxSpawner;
            PlayerSpawnPoints = playerSpawnPoints;
            AllySpawnPoints = allySpawnPoints;
            EnemySpawnPoints = enemySpawnPoints;
        }

        private bool ValidateReferences(out string failureMessage)
        {
            if (PoolCatalog == null || !PoolCatalog.Validate().IsValid ||
                UnitCatalog == null || !UnitCatalog.Validate().IsValid ||
                StationaryFixtureDefinition == null ||
                !StationaryFixtureDefinition.Validate().IsValid ||
                PoolManager == null || SpawnManager == null ||
                InteractionSystem == null || UnitRegistry == null ||
                InitialSandboxSpawner == null || PlayerSpawnPoints == null ||
                AllySpawnPoints == null || EnemySpawnPoints == null)
            {
                failureMessage =
                    "CombatSandboxBootstrap has missing or invalid catalogs, definitions, services, or spawn groups.";
                return false;
            }

            if (PlayerSpawnPoints.Count == 0 || AllySpawnPoints.Count == 0 ||
                EnemySpawnPoints.Count == 0)
            {
                failureMessage =
                    "CombatSandboxBootstrap requires Player, Ally, and Enemy spawn points.";
                return false;
            }

            if (!UnitCatalog.TryGetDefinition(
                    StationaryFixtureDefinition.UnitId,
                    out UnitDefinition catalogDefinition) ||
                catalogDefinition != StationaryFixtureDefinition)
            {
                failureMessage =
                    "The stationary fixture definition must be present in UnitCatalog.";
                return false;
            }

            string[] requiredLayers =
            {
                "World",
                "UnitBody",
                "UnitTarget",
                "Projectile"
            };
            foreach (string requiredLayer in requiredLayers)
            {
                if (LayerMask.NameToLayer(requiredLayer) < 0)
                {
                    failureMessage =
                        $"Required physics layer '{requiredLayer}' is missing.";
                    return false;
                }
            }

            failureMessage = string.Empty;
            return true;
        }

        private void DisableSandbox(string failureMessage)
        {
            IsGameplayEnabled = false;
            enabled = false;
            Debug.LogError(
                $"[CombatSandboxBootstrap] {failureMessage}",
                this);
        }
    }
}
