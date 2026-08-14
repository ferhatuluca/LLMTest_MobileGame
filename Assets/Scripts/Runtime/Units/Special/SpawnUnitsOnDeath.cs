using System;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units.AI;
using UnityEngine;
using UnityEngine.AI;

namespace MonstersVsZombies.Units.Special
{
    public readonly struct DeathSpawnCompletedEvent
    {
        public SpawnId SourceSpawnId { get; }
        public int SpawnedCount { get; }

        public DeathSpawnCompletedEvent(
            SpawnId sourceSpawnId,
            int spawnedCount)
        {
            SourceSpawnId = sourceSpawnId;
            SpawnedCount = spawnedCount;
        }
    }

    /// <summary>
    /// Spawns three MiniDivisible units when a Divisible dies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitController))]
    [RequireComponent(typeof(UnitLifecycleController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SpawnUnitsOnDeath : MonoBehaviour, IPoolable
    {
        private readonly Vector3[] _spawnPositions =
            new Vector3[MiniDivisibleSpawnFormation.ChildCount];

        [field: SerializeField] public AIUnitDefinition MiniDivisibleDefinition { get; private set; }

        private UnitController _unitController;
        private UnitLifecycleController _lifecycleController;
        private NavMeshAgent _agent;
        private SpawnManager _spawnManager;
        private InteractionSystem _interactionSystem;
        private NavMeshSpawnPositionValidator _positionValidator;
        private bool _hasSpawnedChildren;

        public event Action<DeathSpawnCompletedEvent> DeathSpawnCompleted;

        private void Awake()
        {
            CacheComponents();
            if (_lifecycleController != null)
            {
                _lifecycleController.Dying += HandleDying;
            }
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
            CacheComponents();
            bool isValid = _unitController != null &&
                           _lifecycleController != null &&
                           _agent != null &&
                           MiniDivisibleDefinition != null &&
                           MiniDivisibleDefinition.Validate().IsValid &&
                           MiniDivisibleDefinition.UnitId ==
                               new UnitId("EnemyMiniDivisible") &&
                           _unitController.Definition != null &&
                           _unitController.Definition.UnitId ==
                               new UnitId("EnemyDivisible");
            failureMessage = isValid
                ? string.Empty
                : "SpawnUnitsOnDeath requires Divisible and MiniDivisible definitions.";
            return isValid;
        }

        public bool ConfigureRuntimeServices(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem)
        {
            CacheComponents();
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
            _hasSpawnedChildren = false;
            return ValidateConfiguration(out _);
        }

        public bool CompleteSpawn()
        {
            return gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
            _hasSpawnedChildren = false;
            _spawnManager = null;
            _interactionSystem = null;
            _positionValidator = null;
        }

        private void HandleDying(UnitLifecycleChangedEvent lifecycleEvent)
        {
            if (_hasSpawnedChildren || lifecycleEvent.Unit != _unitController)
            {
                return;
            }

            _hasSpawnedChildren = true;
            int spawnedCount = 0;
            if (_spawnManager != null && _interactionSystem != null &&
                _positionValidator != null)
            {
                MiniDivisibleSpawnFormation.FillRadialPositions(
                    transform.position,
                    transform.forward,
                    _agent.radius * 2f,
                    _spawnPositions);
                foreach (Vector3 position in _spawnPositions)
                {
                    SpawnResult<UnitController> result =
                        _spawnManager.SpawnDeathUnit(
                            MiniDivisibleDefinition,
                            new Pose(position, transform.rotation),
                            lifecycleEvent.SpawnId,
                            _positionValidator);
                    AIUnitBrain childBrain = result.IsSuccess
                        ? result.Entity.GetComponent<AIUnitBrain>()
                        : null;
                    if (childBrain != null &&
                        childBrain.ConfigureRuntimeServices(
                            _spawnManager,
                            _interactionSystem))
                    {
                        spawnedCount++;
                    }
                    else if (result.IsSuccess)
                    {
                        _spawnManager.ReturnUnit(result.Entity);
                    }
                }
            }

            DeathSpawnCompleted?.Invoke(new DeathSpawnCompletedEvent(
                lifecycleEvent.SpawnId,
                spawnedCount));
            _lifecycleController.RequestPoolReturn();
        }

        private void CacheComponents()
        {
            _unitController = GetComponent<UnitController>();
            _lifecycleController = GetComponent<UnitLifecycleController>();
            _agent = GetComponent<NavMeshAgent>();
        }
    }
}
