using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using UnityEngine;

namespace MonstersVsZombies.Core.Bootstrap
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class SpecialUnitSandboxScenarioController : MonoBehaviour
    {
        [SerializeField] private AIUnitDefinition[] _definitions =
            Array.Empty<AIUnitDefinition>();

        private readonly List<UnitController> _spawnedUnits =
            new List<UnitController>();

        [field: SerializeField] public CombatSandboxBootstrap Bootstrap { get; private set; }
        [field: SerializeField] public InitialSandboxSpawner InitialSandboxSpawner { get; private set; }
        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public SpawnPointGroup SpawnPoints { get; private set; }

        public IReadOnlyList<AIUnitDefinition> Definitions => _definitions;
        public IReadOnlyList<UnitController> SpawnedUnits => _spawnedUnits;
        public bool IsInitialized { get; private set; }
        public string LastFailureMessage { get; private set; } = string.Empty;

        private void Start()
        {
            if (!SpawnAllUnits())
            {
                enabled = false;
                Debug.LogError(
                    $"[SpecialUnitSandboxScenario] {LastFailureMessage}",
                    this);
                return;
            }

            Debug.Log(
                "[SpecialUnitSandboxScenario] Spawned Stunner, MiniDivisible, and Divisible.",
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

            SpawnPoints.ResetRoundRobin();
            foreach (AIUnitDefinition definition in _definitions)
            {
                if (!SpawnPoints.TryGetNext(out Pose spawnPose))
                {
                    ReturnSpawnedUnits();
                    LastFailureMessage =
                        $"No special-unit spawn point is available for {definition.DisplayName}.";
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

                    ReturnSpawnedUnits();
                    LastFailureMessage =
                        $"{definition.DisplayName} could not spawn and bind its special combat services.";
                    return false;
                }

                _spawnedUnits.Add(unit);
            }

            IsInitialized = true;
            LastFailureMessage = string.Empty;
            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (Bootstrap == null || !Bootstrap.IsInitialized ||
                InitialSandboxSpawner == null || SpawnManager == null ||
                InteractionSystem == null || SpawnPoints == null ||
                _definitions == null || _definitions.Length != 3 ||
                SpawnPoints.Count < _definitions.Length)
            {
                failureMessage =
                    "SpecialUnitSandboxScenario requires three definitions, three spawn points, and initialized runtime services.";
                return false;
            }

            string[] expectedIds =
            {
                "EnemyStunner",
                "EnemyMiniDivisible",
                "EnemyDivisible"
            };
            for (int index = 0; index < expectedIds.Length; index++)
            {
                AIUnitDefinition definition = _definitions[index];
                if (definition == null || !definition.Validate().IsValid ||
                    definition.Faction != UnitFaction.Enemy ||
                    definition.UnitId != new UnitId(expectedIds[index]) ||
                    !Bootstrap.UnitCatalog.TryGetDefinition(
                        definition.UnitId,
                        out UnitDefinition catalogDefinition) ||
                    catalogDefinition != definition)
                {
                    failureMessage =
                        "Special-unit definitions must be ordered Stunner, MiniDivisible, Divisible and registered as Enemies.";
                    return false;
                }
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
    }
}
