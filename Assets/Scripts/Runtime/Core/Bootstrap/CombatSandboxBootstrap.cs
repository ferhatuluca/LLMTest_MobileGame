using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Player;
using UnityEngine;

namespace MonstersVsZombies.Core.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class CombatSandboxBootstrap : MonoBehaviour
    {
        [field: SerializeField] public PoolCatalog PoolCatalog { get; private set; }
        [field: SerializeField] public UnitCatalog UnitCatalog { get; private set; }
        [field: SerializeField] public PlayerUnitDefinition PlayerDefinition { get; private set; }
        [field: SerializeField] public AIUnitDefinition StationaryEnemyDefinition { get; private set; }
        [field: SerializeField] public PoolManager PoolManager { get; private set; }
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }
        [field: SerializeField] public InitialSandboxSpawner InitialSandboxSpawner { get; private set; }
        [field: SerializeField] public SpawnPointGroup PlayerSpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup AllySpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup EnemySpawnPoints { get; private set; }
        [field: SerializeField] public CameraFollowController CameraFollowController { get; private set; }
        [field: SerializeField] public PlayerHudController PlayerHudController { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsGameplayEnabled { get; private set; }
        public string LastFailureMessage { get; private set; } = string.Empty;
        public UnitController InitialPlayer { get; private set; }
        public UnitController InitialStationaryEnemy { get; private set; }

        private void Awake()
        {
            if (!InitializeServices())
            {
                DisableSandbox(LastFailureMessage);
            }
        }

        private void Start()
        {
            if (IsInitialized &&
                (!SpawnInitialPlayer() || !SpawnStationaryEnemyTarget()))
            {
                DisableSandbox(LastFailureMessage);
                return;
            }

            if (IsInitialized && InitialPlayer != null &&
                InitialStationaryEnemy != null)
            {
                Debug.Log(
                    "[CombatSandboxBootstrap] Services initialized, the Player spawned and bound, and the stationary Enemy target spawned.",
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

        public bool SpawnInitialPlayer()
        {
            if (!IsInitialized || !PlayerSpawnPoints.TryGetNext(out Pose spawnPose))
            {
                LastFailureMessage =
                    "The initial Player requires an initialized sandbox and Player spawn point.";
                return false;
            }

            SpawnResult<UnitController> spawnResult =
                InitialSandboxSpawner.Spawn(
                    PlayerDefinition,
                    spawnPose);
            if (!spawnResult.IsSuccess)
            {
                LastFailureMessage =
                    $"Initial Player spawn failed: {spawnResult.FailureReason}.";
                return false;
            }

            UnitController player = spawnResult.Entity;
            PlayerMotor playerMotor = player.GetComponent<PlayerMotor>();
            PlayerCombatController playerCombat =
                player.GetComponent<PlayerCombatController>();
            if (playerMotor == null || playerCombat == null ||
                !playerMotor.BindCamera(CameraFollowController.transform) ||
                !playerCombat.ConfigureRuntimeServices(
                    SpawnManager,
                    InteractionSystem,
                    PoolManager) ||
                !CameraFollowController.Bind(player) ||
                !PlayerHudController.Bind(player))
            {
                PlayerHudController.Unbind();
                CameraFollowController.Clear();
                SpawnManager.ReturnUnit(player);
                LastFailureMessage =
                    "The initial Player could not bind camera, HUD, input, or combat runtime services.";
                return false;
            }

            InitialPlayer = player;
            LastFailureMessage = string.Empty;
            return true;
        }

        public bool SpawnStationaryEnemyTarget()
        {
            if (!IsInitialized || !EnemySpawnPoints.TryGetNext(out Pose spawnPose))
            {
                LastFailureMessage =
                    "The stationary Enemy target requires an initialized sandbox and Enemy spawn point.";
                return false;
            }

            SpawnResult<UnitController> spawnResult =
                InitialSandboxSpawner.Spawn(
                    StationaryEnemyDefinition,
                    spawnPose);
            if (!spawnResult.IsSuccess)
            {
                LastFailureMessage =
                    $"Stationary Enemy target spawn failed: {spawnResult.FailureReason}.";
                return false;
            }

            InitialStationaryEnemy = spawnResult.Entity;
            LastFailureMessage = string.Empty;
            return true;
        }

        internal void Configure(
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog,
            PlayerUnitDefinition playerDefinition,
            AIUnitDefinition stationaryEnemyDefinition,
            PoolManager poolManager,
            SpawnManager spawnManager,
            InteractionSystem interactionSystem,
            UnitRegistry unitRegistry,
            InitialSandboxSpawner initialSandboxSpawner,
            SpawnPointGroup playerSpawnPoints,
            SpawnPointGroup allySpawnPoints,
            SpawnPointGroup enemySpawnPoints,
            CameraFollowController cameraFollowController,
            PlayerHudController playerHudController)
        {
            PoolCatalog = poolCatalog;
            UnitCatalog = unitCatalog;
            PlayerDefinition = playerDefinition;
            StationaryEnemyDefinition = stationaryEnemyDefinition;
            PoolManager = poolManager;
            SpawnManager = spawnManager;
            InteractionSystem = interactionSystem;
            UnitRegistry = unitRegistry;
            InitialSandboxSpawner = initialSandboxSpawner;
            PlayerSpawnPoints = playerSpawnPoints;
            AllySpawnPoints = allySpawnPoints;
            EnemySpawnPoints = enemySpawnPoints;
            CameraFollowController = cameraFollowController;
            PlayerHudController = playerHudController;
        }

        private bool ValidateReferences(out string failureMessage)
        {
            if (PoolCatalog == null || !PoolCatalog.Validate().IsValid ||
                UnitCatalog == null || !UnitCatalog.Validate().IsValid ||
                PlayerDefinition == null ||
                !PlayerDefinition.Validate().IsValid ||
                StationaryEnemyDefinition == null ||
                !StationaryEnemyDefinition.Validate().IsValid ||
                PoolManager == null || SpawnManager == null ||
                InteractionSystem == null || UnitRegistry == null ||
                InitialSandboxSpawner == null || PlayerSpawnPoints == null ||
                AllySpawnPoints == null || EnemySpawnPoints == null ||
                CameraFollowController == null ||
                PlayerHudController == null ||
                !PlayerHudController.ValidateConfiguration(out _))
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
                    PlayerDefinition.UnitId,
                    out UnitDefinition playerCatalogDefinition) ||
                playerCatalogDefinition != PlayerDefinition ||
                !UnitCatalog.TryGetDefinition(
                    StationaryEnemyDefinition.UnitId,
                    out UnitDefinition enemyCatalogDefinition) ||
                enemyCatalogDefinition != StationaryEnemyDefinition)
            {
                failureMessage =
                    "The Player and stationary Enemy definitions must be present in UnitCatalog.";
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
            ReleaseInitialGameplay();
            IsGameplayEnabled = false;
            enabled = false;
            Debug.LogError(
                $"[CombatSandboxBootstrap] {failureMessage}",
                this);
        }

        private void ReleaseInitialGameplay()
        {
            PlayerHudController?.Unbind();
            CameraFollowController?.Clear();

            if (SpawnManager == null)
            {
                InitialPlayer = null;
                InitialStationaryEnemy = null;
                return;
            }

            if (InitialStationaryEnemy != null)
            {
                SpawnManager.ReturnUnit(InitialStationaryEnemy);
                InitialStationaryEnemy = null;
            }

            if (InitialPlayer != null)
            {
                SpawnManager.ReturnUnit(InitialPlayer);
                InitialPlayer = null;
            }
        }
    }
}
