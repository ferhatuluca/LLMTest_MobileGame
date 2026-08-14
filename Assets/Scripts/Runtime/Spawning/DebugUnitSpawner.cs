using System;
using System.Collections.Generic;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using UnityEngine;

namespace MonstersVsZombies.Spawning
{
    /// <summary>
    /// Provides development-only catalog-driven spawn, clear, and Player-reset
    /// commands while preserving normal SpawnManager and pooling paths.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugUnitSpawner : MonoBehaviour
    {
        private readonly List<UnitController> _unitSnapshot =
            new List<UnitController>();
        private readonly List<PooledEntity> _entitySnapshot =
            new List<PooledEntity>();

        [field: SerializeField] public SpawnManager SpawnManager { get; private set; }
        [field: SerializeField] public PoolManager PoolManager { get; private set; }
        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }
        [field: SerializeField] public UnitCatalog UnitCatalog { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public SpawnPointGroup AllySpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup EnemySpawnPoints { get; private set; }
        [field: SerializeField] public CombatSandboxBootstrap Bootstrap { get; private set; }

        public string LastMessage { get; private set; } = string.Empty;

        public event Action<string> SpawnReported;

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (SpawnManager == null || PoolManager == null ||
                UnitRegistry == null || UnitCatalog == null ||
                InteractionSystem == null ||
                !UnitCatalog.Validate().IsValid ||
                AllySpawnPoints == null || AllySpawnPoints.Count == 0 ||
                EnemySpawnPoints == null || EnemySpawnPoints.Count == 0 ||
                Bootstrap == null)
            {
                failureMessage =
                    "DebugUnitSpawner requires valid spawn, pool, registry, catalog, bootstrap, Ally point, and Enemy point references.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool Configure(
            SpawnManager spawnManager,
            PoolManager poolManager,
            UnitRegistry unitRegistry,
            UnitCatalog unitCatalog,
            InteractionSystem interactionSystem,
            SpawnPointGroup allySpawnPoints,
            SpawnPointGroup enemySpawnPoints,
            CombatSandboxBootstrap bootstrap)
        {
            SpawnManager = spawnManager;
            PoolManager = poolManager;
            UnitRegistry = unitRegistry;
            UnitCatalog = unitCatalog;
            InteractionSystem = interactionSystem;
            AllySpawnPoints = allySpawnPoints;
            EnemySpawnPoints = enemySpawnPoints;
            Bootstrap = bootstrap;
            return ValidateConfiguration(out _);
        }

        public SpawnResult<UnitController> Spawn(
            UnitDefinition definition,
            Pose spawnPose)
        {
            return SpawnInternal(definition, spawnPose);
        }

        private SpawnResult<UnitController> SpawnInternal(
            UnitDefinition definition,
            Pose spawnPose)
        {
            if (SpawnManager == null)
            {
                SpawnResult<UnitController> unavailableResult =
                    SpawnResult<UnitController>.CreateFailure(
                        SpawnFailureReason.RentFailed);
                PublishSpawnResult(definition, unavailableResult);
                return unavailableResult;
            }

            SpawnResult<UnitController> result = SpawnManager.SpawnUnit(new UnitSpawnRequest(
                definition,
                spawnPose.position,
                spawnPose.rotation,
                default,
                SpawnReason.Debug));
            if (result.IsSuccess)
            {
                AIUnitBrain brain = result.Entity.GetComponent<AIUnitBrain>();
                if (brain != null && !brain.ConfigureRuntimeServices(
                        SpawnManager,
                        InteractionSystem))
                {
                    SpawnManager.ReturnUnit(result.Entity);
                    result = SpawnResult<UnitController>.CreateFailure(
                        SpawnFailureReason.ActivationDependentInitializationFailed);
                }
            }

            PublishSpawnResult(definition, result);

            return result;
        }

        public SpawnResult<UnitController> Spawn(UnitId unitId)
        {
            if (UnitCatalog == null ||
                !UnitCatalog.TryGetDefinition(unitId, out UnitDefinition definition))
            {
                SpawnResult<UnitController> invalidResult =
                    SpawnResult<UnitController>.CreateFailure(
                        SpawnFailureReason.InvalidDefinition);
                PublishSpawnResult(null, invalidResult);
                return invalidResult;
            }

            SpawnPointGroup spawnPointGroup = definition.Faction == UnitFaction.Ally
                ? AllySpawnPoints
                : EnemySpawnPoints;
            if (spawnPointGroup == null ||
                !spawnPointGroup.TryGetNext(out Pose spawnPose))
            {
                SpawnResult<UnitController> invalidPositionResult =
                    SpawnResult<UnitController>.CreateFailure(
                        SpawnFailureReason.InvalidPosition);
                PublishSpawnResult(definition, invalidPositionResult);
                return invalidPositionResult;
            }

            return Spawn(definition, spawnPose);
        }

        public int SpawnMany(UnitId unitId, int requestedCount)
        {
            if (requestedCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedCount));
            }

            int successCount = 0;
            for (int spawnIndex = 0; spawnIndex < requestedCount; spawnIndex++)
            {
                if (Spawn(unitId).IsSuccess)
                {
                    successCount++;
                }
            }

            return successCount;
        }

        public int ClearNonPlayerUnitsAndProjectiles()
        {
            if (PoolManager == null || UnitRegistry == null)
            {
                return 0;
            }

            _unitSnapshot.Clear();
            UnitRegistry.CopySnapshot(_unitSnapshot);
            HashSet<PooledEntity> playerEntities = new HashSet<PooledEntity>();
            foreach (UnitController unit in _unitSnapshot)
            {
                if (unit != null && unit.Faction == UnitFaction.Player)
                {
                    PooledEntity playerEntity = unit.GetComponent<PooledEntity>();
                    if (playerEntity != null)
                    {
                        playerEntities.Add(playerEntity);
                    }
                }
            }

            PoolManager.CopyActiveEntities(_entitySnapshot);
            int returnedCount = 0;
            foreach (PooledEntity entity in _entitySnapshot)
            {
                if (entity != null && !playerEntities.Contains(entity) &&
                    PoolManager.Return(entity).IsSuccess)
                {
                    returnedCount++;
                }
            }

            return returnedCount;
        }

        public bool ResetPlayer()
        {
            return Bootstrap != null && Bootstrap.ResetPlayer();
        }

        private void PublishSpawnResult(
            UnitDefinition definition,
            SpawnResult<UnitController> result)
        {
            string definitionName = definition == null
                ? "<missing definition>"
                : definition.DisplayName;
            LastMessage = result.IsSuccess
                ? $"Spawned {definitionName} as {result.Entity.SpawnId}."
                : $"Could not spawn {definitionName}: {result.FailureReason}.";
            SpawnReported?.Invoke(LastMessage);
        }
    }
}
