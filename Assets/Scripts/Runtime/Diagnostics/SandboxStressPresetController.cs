using System;
using System.Collections.Generic;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Diagnostics
{
    public readonly struct SandboxStressPresetResult
    {
        public int RequestedPerFaction { get; }
        public int SpawnedAllies { get; }
        public int SpawnedEnemies { get; }
        public PoolFailureReason PoolFailureReason { get; }

        public bool IsSuccess => RequestedPerFaction > 0 &&
            SpawnedAllies == RequestedPerFaction &&
            SpawnedEnemies == RequestedPerFaction &&
            PoolFailureReason == PoolFailureReason.None;

        public SandboxStressPresetResult(
            int requestedPerFaction,
            int spawnedAllies,
            int spawnedEnemies,
            PoolFailureReason poolFailureReason)
        {
            RequestedPerFaction = requestedPerFaction;
            SpawnedAllies = spawnedAllies;
            SpawnedEnemies = spawnedEnemies;
            PoolFailureReason = poolFailureReason;
        }
    }

    /// <summary>
    /// Prewarms and maintains exact 10v10, 50v50, or 100v100 diagnostic populations
    /// through the same spawning and pooling services as ordinary gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxStressPresetController : MonoBehaviour
    {
        private const float k_PopulationRefreshInterval = 0.5f;

        private readonly List<UnitController> _unitSnapshot =
            new List<UnitController>(256);
        private int[] _allyActiveCounts = Array.Empty<int>();
        private int[] _enemyActiveCounts = Array.Empty<int>();
        private int[] _allyTargetCounts = Array.Empty<int>();
        private int[] _enemyTargetCounts = Array.Empty<int>();
        private float _populationRefreshRemaining;

        [field: SerializeField] public DebugUnitSpawner DebugUnitSpawner { get; private set; }
        [field: SerializeField] public PoolManager PoolManager { get; private set; }
        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }
        [field: SerializeField] public AIUnitDefinition[] AllyDefinitions { get; private set; } =
            Array.Empty<AIUnitDefinition>();
        [field: SerializeField] public AIUnitDefinition[] EnemyDefinitions { get; private set; } =
            Array.Empty<AIUnitDefinition>();
        [field: SerializeField] public ProjectileDefinition[] ProjectileDefinitions { get; private set; } =
            Array.Empty<ProjectileDefinition>();

        public bool IsMaintainingPreset { get; private set; }
        public int RequestedPerFaction { get; private set; }
        public SandboxStressPresetResult LastResult { get; private set; }

        private void Awake()
        {
            EnsureCountBuffers();
        }

        private void Update()
        {
            if (!IsMaintainingPreset)
            {
                return;
            }

            _populationRefreshRemaining -= Time.unscaledDeltaTime;
            if (_populationRefreshRemaining <= 0f)
            {
                MaintainPopulation();
                _populationRefreshRemaining = k_PopulationRefreshInterval;
            }
        }

        public bool Configure(
            DebugUnitSpawner debugUnitSpawner,
            PoolManager poolManager,
            UnitRegistry unitRegistry,
            AIUnitDefinition[] allyDefinitions,
            AIUnitDefinition[] enemyDefinitions,
            ProjectileDefinition[] projectileDefinitions)
        {
            DebugUnitSpawner = debugUnitSpawner;
            PoolManager = poolManager;
            UnitRegistry = unitRegistry;
            AllyDefinitions = Clone(allyDefinitions);
            EnemyDefinitions = Clone(enemyDefinitions);
            ProjectileDefinitions = Clone(projectileDefinitions);
            EnsureCountBuffers();
            return ValidateConfiguration(out _);
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (DebugUnitSpawner == null || PoolManager == null ||
                UnitRegistry == null || AllyDefinitions == null ||
                AllyDefinitions.Length == 0 || EnemyDefinitions == null ||
                EnemyDefinitions.Length == 0 || ProjectileDefinitions == null ||
                ProjectileDefinitions.Length == 0 ||
                !ValidateDefinitions(AllyDefinitions, UnitFaction.Ally) ||
                !ValidateDefinitions(EnemyDefinitions, UnitFaction.Enemy))
            {
                failureMessage =
                    "Sandbox stress presets require pool/spawn/registry services, non-empty faction definition sets, and selected projectile pools.";
                return false;
            }

            foreach (ProjectileDefinition projectileDefinition in
                     ProjectileDefinitions)
            {
                if (projectileDefinition == null ||
                    !projectileDefinition.Validate().IsValid)
                {
                    failureMessage =
                        "Every selected stress projectile definition must be valid.";
                    return false;
                }
            }

            failureMessage = string.Empty;
            return true;
        }

        public SandboxStressPresetResult RunPreset(int perFactionCount)
        {
            StopPreset();
            if (perFactionCount <= 0 || !ValidateConfiguration(out _))
            {
                LastResult = new SandboxStressPresetResult(
                    perFactionCount,
                    0,
                    0,
                    PoolFailureReason.InitializationFailed);
                return LastResult;
            }

            DebugUnitSpawner.ClearNonPlayerUnitsAndProjectiles();
            DebugUnitSpawner.AllySpawnPoints.ResetRoundRobin();
            DebugUnitSpawner.EnemySpawnPoints.ResetRoundRobin();
            FillTargetCounts(
                perFactionCount,
                _allyTargetCounts,
                AllyDefinitions.Length);
            FillTargetCounts(
                perFactionCount,
                _enemyTargetCounts,
                EnemyDefinitions.Length);

            if (!PrewarmSelectedPools(perFactionCount, out PoolFailureReason failure))
            {
                LastResult = new SandboxStressPresetResult(
                    perFactionCount,
                    0,
                    0,
                    failure);
                return LastResult;
            }

            RequestedPerFaction = perFactionCount;
            int spawnedAllies = SpawnMissing(
                AllyDefinitions,
                _allyTargetCounts,
                null);
            int spawnedEnemies = SpawnMissing(
                EnemyDefinitions,
                _enemyTargetCounts,
                null);
            LastResult = new SandboxStressPresetResult(
                perFactionCount,
                spawnedAllies,
                spawnedEnemies,
                PoolFailureReason.None);
            IsMaintainingPreset = LastResult.IsSuccess;
            _populationRefreshRemaining = k_PopulationRefreshInterval;
            return LastResult;
        }

        public void StopPreset()
        {
            IsMaintainingPreset = false;
            RequestedPerFaction = 0;
            _populationRefreshRemaining = 0f;
        }

        private bool PrewarmSelectedPools(
            int perFactionCount,
            out PoolFailureReason failureReason)
        {
            for (int definitionIndex = 0;
                 definitionIndex < AllyDefinitions.Length;
                 definitionIndex++)
            {
                if (!PoolManager.TryEnsureInactiveCount(
                        AllyDefinitions[definitionIndex].PoolId,
                        _allyTargetCounts[definitionIndex],
                        out failureReason))
                {
                    return false;
                }
            }

            for (int definitionIndex = 0;
                 definitionIndex < EnemyDefinitions.Length;
                 definitionIndex++)
            {
                if (!PoolManager.TryEnsureInactiveCount(
                        EnemyDefinitions[definitionIndex].PoolId,
                        _enemyTargetCounts[definitionIndex],
                        out failureReason))
                {
                    return false;
                }
            }

            foreach (ProjectileDefinition projectileDefinition in
                     ProjectileDefinitions)
            {
                if (!PoolManager.TryEnsureInactiveCount(
                        projectileDefinition.PoolId,
                        perFactionCount,
                        out failureReason))
                {
                    return false;
                }
            }

            failureReason = PoolFailureReason.None;
            return true;
        }

        private void MaintainPopulation()
        {
            Array.Clear(_allyActiveCounts, 0, _allyActiveCounts.Length);
            Array.Clear(_enemyActiveCounts, 0, _enemyActiveCounts.Length);
            _unitSnapshot.Clear();
            UnitRegistry.CopySnapshot(_unitSnapshot);
            foreach (UnitController unit in _unitSnapshot)
            {
                if (unit == null || !unit.IsActive || unit.Definition == null)
                {
                    continue;
                }

                CountDefinition(unit.Definition, AllyDefinitions, _allyActiveCounts);
                CountDefinition(unit.Definition, EnemyDefinitions, _enemyActiveCounts);
            }

            SpawnMissing(AllyDefinitions, _allyTargetCounts, _allyActiveCounts);
            SpawnMissing(EnemyDefinitions, _enemyTargetCounts, _enemyActiveCounts);
        }

        private int SpawnMissing(
            AIUnitDefinition[] definitions,
            int[] targetCounts,
            int[] activeCounts)
        {
            int spawnedCount = 0;
            for (int definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                int activeCount = activeCounts == null
                    ? 0
                    : activeCounts[definitionIndex];
                int missingCount = Mathf.Max(
                    0,
                    targetCounts[definitionIndex] - activeCount);
                for (int spawnIndex = 0;
                     spawnIndex < missingCount;
                     spawnIndex++)
                {
                    if (DebugUnitSpawner.SpawnForStress(
                            definitions[definitionIndex]).IsSuccess)
                    {
                        spawnedCount++;
                    }
                }
            }

            return spawnedCount;
        }

        private static void CountDefinition(
            UnitDefinition definition,
            AIUnitDefinition[] definitions,
            int[] activeCounts)
        {
            for (int definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                if (definition == definitions[definitionIndex])
                {
                    activeCounts[definitionIndex]++;
                    return;
                }
            }
        }

        private void EnsureCountBuffers()
        {
            _allyActiveCounts = new int[AllyDefinitions?.Length ?? 0];
            _enemyActiveCounts = new int[EnemyDefinitions?.Length ?? 0];
            _allyTargetCounts = new int[AllyDefinitions?.Length ?? 0];
            _enemyTargetCounts = new int[EnemyDefinitions?.Length ?? 0];
        }

        private static void FillTargetCounts(
            int requestedCount,
            int[] destination,
            int definitionCount)
        {
            int baseCount = requestedCount / definitionCount;
            int remainder = requestedCount % definitionCount;
            for (int definitionIndex = 0;
                 definitionIndex < definitionCount;
                 definitionIndex++)
            {
                destination[definitionIndex] = baseCount +
                    (definitionIndex < remainder ? 1 : 0);
            }
        }

        private static bool ValidateDefinitions(
            AIUnitDefinition[] definitions,
            UnitFaction faction)
        {
            HashSet<UnitId> unitIds = new HashSet<UnitId>();
            foreach (AIUnitDefinition definition in definitions)
            {
                if (definition == null || definition.Faction != faction ||
                    !definition.Validate().IsValid ||
                    !unitIds.Add(definition.UnitId))
                {
                    return false;
                }
            }

            return true;
        }

        private static T[] Clone<T>(T[] values)
        {
            return values == null
                ? Array.Empty<T>()
                : (T[])values.Clone();
        }
    }
}
