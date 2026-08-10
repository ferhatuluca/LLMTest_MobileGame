using System;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using UnityEngine;
using UnityEngine.AI;

namespace MonstersVsZombies.Units.Special
{
    public readonly struct DeathSpawnCompletedEvent
    {
        public SpawnId SourceSpawnId { get; }
        public int SpawnedCount { get; }
        public int FailedPositionCount { get; }
        public int OtherFailedCount { get; }
        public int FailedCount => FailedPositionCount + OtherFailedCount;

        public DeathSpawnCompletedEvent(
            SpawnId sourceSpawnId,
            int spawnedCount,
            int failedPositionCount,
            int otherFailedCount)
        {
            SourceSpawnId = sourceSpawnId;
            SpawnedCount = spawnedCount;
            FailedPositionCount = failedPositionCount;
            OtherFailedCount = otherFailedCount;
        }
    }

    /// <summary>
    /// Implements Divisible's ordered death effect: spawn exactly three children
    /// through SpawnManager, then request the parent's pool return.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitController))]
    [RequireComponent(typeof(UnitLifecycleController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SpawnUnitsOnDeath : MonoBehaviour, IPoolable
    {
        private readonly Vector3[] _primaryPositions =
            new Vector3[MiniDivisibleSpawnFormation.ChildCount];
        private readonly Vector3[] _retryPositions =
            new Vector3[MiniDivisibleSpawnFormation.ChildCount];
        private readonly UnitController[] _lastSpawnedChildren =
            new UnitController[MiniDivisibleSpawnFormation.ChildCount];

        [field: SerializeField] public AIUnitDefinition MiniDivisibleDefinition { get; private set; }

        private UnitController _unitController;
        private UnitLifecycleController _lifecycleController;
        private NavMeshAgent _agent;
        private SpawnManager _spawnManager;
        private InteractionSystem _interactionSystem;
        private NavMeshSpawnPositionValidator _positionValidator;

        public event Action<DeathSpawnCompletedEvent> DeathSpawnCompleted;

        public bool HasFiredForCurrentSpawn { get; private set; }
        public int LastSpawnedCount { get; private set; }
        public int LastFailedCount { get; private set; }
        public int LastFailedPositionCount { get; private set; }
        public int LastOtherFailedCount { get; private set; }
        public int DeathSpawnRequestCount { get; private set; }

        private void Awake()
        {
            CacheAndSubscribe();
        }

        private void OnDestroy()
        {
            if (_lifecycleController != null)
            {
                _lifecycleController.Dying -= HandleDying;
            }
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheAndSubscribe();
            if (_unitController == null || _lifecycleController == null ||
                _agent == null || MiniDivisibleDefinition == null ||
                !MiniDivisibleDefinition.Validate().IsValid ||
                MiniDivisibleDefinition.UnitId !=
                    new UnitId("EnemyMiniDivisible") ||
                MiniDivisibleDefinition.Faction != UnitFaction.Enemy ||
                _unitController.Definition == null ||
                _unitController.Definition.UnitId !=
                    new UnitId("EnemyDivisible"))
            {
                failureMessage =
                    "SpawnUnitsOnDeath belongs only to EnemyDivisible and must point to EnemyMiniDivisible.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool ConfigureRuntimeServices(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem)
        {
            CacheAndSubscribe();
            if (spawnManager == null || interactionSystem == null ||
                _agent == null || _agent.radius <= 0f ||
                !ValidateConfiguration(out _))
            {
                return false;
            }

            _spawnManager = spawnManager;
            _interactionSystem = interactionSystem;
            _positionValidator = new NavMeshSpawnPositionValidator(
                _agent.radius,
                NavMesh.AllAreas);
            return true;
        }

        public bool PrepareForSpawn()
        {
            CacheAndSubscribe();
            ResetTransientState();
            return ValidateConfiguration(out _);
        }

        public bool CompleteSpawn()
        {
            return gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
            ResetTransientState();
            _spawnManager = null;
            _interactionSystem = null;
            _positionValidator = null;
        }

        public UnitController GetLastSpawnedChild(int index)
        {
            if (index < 0 || index >= LastSpawnedCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _lastSpawnedChildren[index];
        }

        private void HandleDying(UnitLifecycleChangedEvent lifecycleEvent)
        {
            if (HasFiredForCurrentSpawn ||
                lifecycleEvent.Unit != _unitController)
            {
                return;
            }

            HasFiredForCurrentSpawn = true;
            DeathSpawnRequestCount++;
            SpawnId sourceSpawnId = lifecycleEvent.SpawnId;
            Vector3 center = transform.position;
            float primaryDistance = _agent == null
                ? 0f
                : _agent.radius * 2f;
            if (_spawnManager == null || _interactionSystem == null ||
                _positionValidator == null || primaryDistance <= 0f)
            {
                LastOtherFailedCount =
                    MiniDivisibleSpawnFormation.ChildCount;
                LastFailedCount = LastOtherFailedCount;
                CompleteDeathSpawn(sourceSpawnId);
                return;
            }

            MiniDivisibleSpawnFormation.FillRadialPositions(
                center,
                transform.forward,
                primaryDistance,
                _primaryPositions);
            MiniDivisibleSpawnFormation.FillRadialPositions(
                center,
                transform.forward,
                primaryDistance * 0.5f,
                _retryPositions);
            for (int index = 0;
                 index < MiniDivisibleSpawnFormation.ChildCount;
                 index++)
            {
                SpawnResult<UnitController> spawnResult = TrySpawnChild(
                    _primaryPositions[index],
                    sourceSpawnId);
                if (!spawnResult.IsSuccess &&
                    spawnResult.FailureReason ==
                        SpawnFailureReason.InvalidPosition)
                {
                    spawnResult = TrySpawnChild(
                        _retryPositions[index],
                        sourceSpawnId);
                }

                if (!spawnResult.IsSuccess)
                {
                    if (spawnResult.FailureReason ==
                        SpawnFailureReason.InvalidPosition)
                    {
                        LastFailedPositionCount++;
                    }
                    else
                    {
                        LastOtherFailedCount++;
                    }

                    LastFailedCount++;
                    continue;
                }

                AIUnitBrain childBrain =
                    spawnResult.Entity.GetComponent<AIUnitBrain>();
                if (childBrain == null ||
                    !childBrain.ConfigureRuntimeServices(
                        _spawnManager,
                        _interactionSystem))
                {
                    _spawnManager.ReturnUnit(spawnResult.Entity);
                    LastOtherFailedCount++;
                    LastFailedCount++;
                    continue;
                }

                _lastSpawnedChildren[LastSpawnedCount] =
                    spawnResult.Entity;
                LastSpawnedCount++;
            }

            CompleteDeathSpawn(sourceSpawnId);
        }

        private SpawnResult<UnitController> TrySpawnChild(
            Vector3 position,
            SpawnId sourceSpawnId)
        {
            return _spawnManager.SpawnDeathUnit(
                MiniDivisibleDefinition,
                new Pose(position, transform.rotation),
                sourceSpawnId,
                _positionValidator);
        }

        private void CompleteDeathSpawn(SpawnId sourceSpawnId)
        {
            DeathSpawnCompleted?.Invoke(new DeathSpawnCompletedEvent(
                sourceSpawnId,
                LastSpawnedCount,
                LastFailedPositionCount,
                LastOtherFailedCount));
            _lifecycleController.RequestPoolReturn();
        }

        private void ResetTransientState()
        {
            HasFiredForCurrentSpawn = false;
            LastSpawnedCount = 0;
            LastFailedCount = 0;
            LastFailedPositionCount = 0;
            LastOtherFailedCount = 0;
            DeathSpawnRequestCount = 0;
            Array.Clear(
                _lastSpawnedChildren,
                0,
                _lastSpawnedChildren.Length);
        }

        private void CacheAndSubscribe()
        {
            _unitController = GetComponent<UnitController>();
            _agent = GetComponent<NavMeshAgent>();
            UnitLifecycleController lifecycleController =
                GetComponent<UnitLifecycleController>();
            if (_lifecycleController == lifecycleController)
            {
                return;
            }

            if (_lifecycleController != null)
            {
                _lifecycleController.Dying -= HandleDying;
            }

            _lifecycleController = lifecycleController;
            if (_lifecycleController != null)
            {
                _lifecycleController.Dying += HandleDying;
            }
        }
    }
}
