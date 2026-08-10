using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using UnityEngine;

namespace MonstersVsZombies.Core.Bootstrap
{
    /// <summary>
    /// Drives the regular-unit sandbox scenario used to exercise the four common
    /// Ally and Enemy archetypes through production systems.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class RegularUnitSandboxScenarioController : MonoBehaviour
    {
        [SerializeField] private AIUnitDefinition[] _allyDefinitions =
            Array.Empty<AIUnitDefinition>();
        [SerializeField] private AIUnitDefinition[] _enemyDefinitions =
            Array.Empty<AIUnitDefinition>();

        private readonly List<UnitController> _spawnedUnits =
            new List<UnitController>();

        [field: SerializeField] public CombatSandboxBootstrap Bootstrap { get; private set; }
        [field: SerializeField] public InitialSandboxSpawner InitialSandboxSpawner { get; private set; }
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public SpawnPointGroup AllySpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup EnemySpawnPoints { get; private set; }

        public IReadOnlyList<AIUnitDefinition> AllyDefinitions =>
            _allyDefinitions;
        public IReadOnlyList<AIUnitDefinition> EnemyDefinitions =>
            _enemyDefinitions;
        public IReadOnlyList<UnitController> SpawnedUnits => _spawnedUnits;
        public bool IsInitialized { get; private set; }
        public string LastFailureMessage { get; private set; } = string.Empty;

        private void Start()
        {
            if (!SpawnAllUnits())
            {
                enabled = false;
                Debug.LogError(
                    $"[RegularUnitSandboxScenario] {LastFailureMessage}",
                    this);
                return;
            }

            Debug.Log(
                "[RegularUnitSandboxScenario] Spawned all seven regular concrete AI units.",
                this);
        }

        public bool SpawnAllUnits()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (!ValidateConfiguration(out string failureMessage))
            {
                LastFailureMessage = failureMessage;
                return false;
            }

            AllySpawnPoints.ResetRoundRobin();
            EnemySpawnPoints.ResetRoundRobin();
            if (!TrySpawnDefinitions(
                    _allyDefinitions,
                    AllySpawnPoints,
                    out failureMessage) ||
                !TrySpawnDefinitions(
                    _enemyDefinitions,
                    EnemySpawnPoints,
                    out failureMessage))
            {
                ReturnSpawnedUnits();
                LastFailureMessage = failureMessage;
                return false;
            }

            IsInitialized = true;
            LastFailureMessage = string.Empty;
            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (Bootstrap == null || !Bootstrap.IsInitialized ||
                InitialSandboxSpawner == null || SpawnManager == null ||
                InteractionSystem == null || AllySpawnPoints == null ||
                EnemySpawnPoints == null || _allyDefinitions == null ||
                _enemyDefinitions == null || _allyDefinitions.Length != 4 ||
                _enemyDefinitions.Length != 3 ||
                AllySpawnPoints.Count < _allyDefinitions.Length ||
                EnemySpawnPoints.Count < _enemyDefinitions.Length ||
                !ValidateDefinitions(
                    _allyDefinitions,
                    UnitFaction.Ally,
                    Bootstrap.UnitCatalog) ||
                !ValidateDefinitions(
                    _enemyDefinitions,
                    UnitFaction.Enemy,
                    Bootstrap.UnitCatalog))
            {
                failureMessage =
                    "RegularUnitSandboxScenario requires four cataloged Ally definitions, three cataloged Enemy definitions, matching spawn points, and initialized runtime services.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        private bool TrySpawnDefinitions(
            AIUnitDefinition[] definitions,
            SpawnPointGroup spawnPointGroup,
            out string failureMessage)
        {
            foreach (AIUnitDefinition definition in definitions)
            {
                if (!spawnPointGroup.TryGetNext(out Pose spawnPose))
                {
                    failureMessage =
                        $"No direct sandbox spawn point is available for {definition.DisplayName}.";
                    return false;
                }

                SpawnResult<UnitController> spawnResult =
                    InitialSandboxSpawner.Spawn(definition, spawnPose);
                UnitController unit = spawnResult.Entity;
                AIUnitBrain brain = unit == null
                    ? null
                    : unit.GetComponent<AIUnitBrain>();
                if (!spawnResult.IsSuccess || brain == null ||
                    !brain.ConfigureRuntimeServices(
                        SpawnManager,
                        InteractionSystem))
                {
                    if (unit != null && unit.IsActive)
                    {
                        SpawnManager.ReturnUnit(unit);
                    }

                    failureMessage =
                        $"{definition.DisplayName} could not spawn and bind its combat delivery services.";
                    return false;
                }

                _spawnedUnits.Add(unit);
            }

            failureMessage = string.Empty;
            return true;
        }

        private void ReturnSpawnedUnits()
        {
            foreach (UnitController unit in _spawnedUnits)
            {
                if (unit != null && unit.IsActive)
                {
                    SpawnManager.ReturnUnit(unit);
                }
            }

            _spawnedUnits.Clear();
        }

        private static bool ValidateDefinitions(
            AIUnitDefinition[] definitions,
            UnitFaction expectedFaction,
            UnitCatalog unitCatalog)
        {
            foreach (AIUnitDefinition definition in definitions)
            {
                if (definition == null || !definition.Validate().IsValid ||
                    definition.Faction != expectedFaction ||
                    !unitCatalog.TryGetDefinition(
                        definition.UnitId,
                        out UnitDefinition catalogDefinition) ||
                    catalogDefinition != definition)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
