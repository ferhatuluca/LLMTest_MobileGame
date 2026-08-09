using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using UnityEngine;

namespace MonstersVsZombies.Core.Bootstrap
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class AISandboxScenarioController : MonoBehaviour
    {
        [field: SerializeField] public CombatSandboxBootstrap Bootstrap { get; private set; }
        [field: SerializeField] public InitialSandboxSpawner InitialSandboxSpawner { get; private set; }
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public SpawnPointGroup AllySpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup EnemySpawnPoints { get; private set; }
        [field: SerializeField] public AIUnitDefinition AllyDefinition { get; private set; }
        [field: SerializeField] public AIUnitDefinition EnemyDefinition { get; private set; }

        public UnitController InitialAlly { get; private set; }
        public UnitController InitialEnemy { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastFailureMessage { get; private set; } = string.Empty;

        private void Start()
        {
            if (!InitializeScenario())
            {
                DisableScenario(LastFailureMessage);
                return;
            }

            Debug.Log(
                "[AISandboxScenario] Spawned and configured one Ally and one Enemy NavMesh AI fixture.",
                this);
        }

        public bool InitializeScenario()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (!ValidateConfiguration(out string failureMessage) ||
                !TrySpawnAI(
                    AllyDefinition,
                    AllySpawnPoints,
                    out UnitController ally,
                    out failureMessage))
            {
                LastFailureMessage = failureMessage;
                return false;
            }

            InitialAlly = ally;
            if (!TrySpawnAI(
                    EnemyDefinition,
                    EnemySpawnPoints,
                    out UnitController enemy,
                    out failureMessage))
            {
                SpawnManager.ReturnUnit(InitialAlly);
                InitialAlly = null;
                LastFailureMessage = failureMessage;
                return false;
            }

            InitialEnemy = enemy;
            IsInitialized = true;
            LastFailureMessage = string.Empty;
            return true;
        }

        internal void Configure(
            CombatSandboxBootstrap bootstrap,
            InitialSandboxSpawner initialSandboxSpawner,
            SpawnManager spawnManager,
            InteractionSystem interactionSystem,
            SpawnPointGroup allySpawnPoints,
            SpawnPointGroup enemySpawnPoints,
            AIUnitDefinition allyDefinition,
            AIUnitDefinition enemyDefinition)
        {
            Bootstrap = bootstrap;
            InitialSandboxSpawner = initialSandboxSpawner;
            SpawnManager = spawnManager;
            InteractionSystem = interactionSystem;
            AllySpawnPoints = allySpawnPoints;
            EnemySpawnPoints = enemySpawnPoints;
            AllyDefinition = allyDefinition;
            EnemyDefinition = enemyDefinition;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (Bootstrap == null || !Bootstrap.IsInitialized ||
                InitialSandboxSpawner == null || SpawnManager == null ||
                InteractionSystem == null || AllySpawnPoints == null ||
                EnemySpawnPoints == null || AllySpawnPoints.Count == 0 ||
                EnemySpawnPoints.Count < 2 || AllyDefinition == null ||
                EnemyDefinition == null || !AllyDefinition.Validate().IsValid ||
                !EnemyDefinition.Validate().IsValid ||
                AllyDefinition.Faction != UnitFaction.Ally ||
                EnemyDefinition.Faction != UnitFaction.Enemy)
            {
                failureMessage =
                    "AISandboxScenario requires initialized services, matching AI definitions, an Ally point, and a second Enemy point.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        private bool TrySpawnAI(
            AIUnitDefinition definition,
            SpawnPointGroup spawnPointGroup,
            out UnitController unit,
            out string failureMessage)
        {
            unit = null;
            if (!spawnPointGroup.TryGetNext(out Pose spawnPose))
            {
                failureMessage =
                    $"No spawn point is available for {definition.DisplayName}.";
                return false;
            }

            SpawnResult<UnitController> spawnResult =
                InitialSandboxSpawner.Spawn(definition, spawnPose);
            if (!spawnResult.IsSuccess)
            {
                failureMessage =
                    $"{definition.DisplayName} spawn failed: {spawnResult.FailureReason}.";
                return false;
            }

            AIUnitBrain brain = spawnResult.Entity.GetComponent<AIUnitBrain>();
            if (brain == null ||
                !brain.ConfigureRuntimeServices(InteractionSystem))
            {
                SpawnManager.ReturnUnit(spawnResult.Entity);
                failureMessage =
                    $"{definition.DisplayName} could not bind AI combat services.";
                return false;
            }

            unit = spawnResult.Entity;
            failureMessage = string.Empty;
            return true;
        }

        private void DisableScenario(string failureMessage)
        {
            enabled = false;
            Debug.LogError($"[AISandboxScenario] {failureMessage}", this);
        }
    }
}
