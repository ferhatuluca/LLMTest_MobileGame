using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace MonstersVsZombies.Units.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(UnitController))]
    public sealed class NavMeshUnitMotor : MonoBehaviour, IUnitMotor,
        IDestinationRefreshPolicy, IPoolable
    {
        private const int k_AvoidancePriorityCount = 100;

        private NavMeshAgent _agent;
        private UnitController _unitController;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;

        public bool IsStopped { get; private set; } = true;
        public bool IsOnNavMesh => _agent != null && _agent.isOnNavMesh;
        public bool HasPath => IsOnNavMesh && _agent.hasPath;
        public Vector3 LastDestination { get; private set; }
        public int DestinationCommandCount { get; private set; }
        public int AvoidancePriority => _agent == null
            ? 0
            : _agent.avoidancePriority;
        public float DestinationRefreshDistance => _agent == null
            ? 0f
            : _agent.radius;

        private void Awake()
        {
            CacheSiblingComponents();
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheSiblingComponents();
            if (_agent == null || _unitController == null)
            {
                failureMessage =
                    "NavMeshUnitMotor requires NavMeshAgent and UnitController siblings.";
                return false;
            }

            if (!(_unitController.Definition is AIUnitDefinition definition) ||
                !definition.Validate().IsValid)
            {
                failureMessage =
                    "NavMeshUnitMotor accepts only a valid AIUnitDefinition.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            if (!CanControlAgent() ||
                float.IsNaN(worldPosition.x) ||
                float.IsNaN(worldPosition.y) ||
                float.IsNaN(worldPosition.z) ||
                float.IsInfinity(worldPosition.x) ||
                float.IsInfinity(worldPosition.y) ||
                float.IsInfinity(worldPosition.z))
            {
                return;
            }

            _agent.isStopped = false;
            if (_agent.SetDestination(worldPosition))
            {
                IsStopped = false;
                LastDestination = worldPosition;
                DestinationCommandCount++;
            }
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            if (!CanControlAgent() || _unitController.Definition == null)
            {
                return;
            }

            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _unitController.Definition.TurnSpeed * Time.deltaTime);
        }

        public void Stop()
        {
            IsStopped = true;
            if (_agent == null || !_agent.isActiveAndEnabled ||
                !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        public void Resume()
        {
            if (!_isPreparedForSpawn || !_isActivationComplete ||
                _agent == null || !_agent.isActiveAndEnabled ||
                !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = false;
            IsStopped = false;
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            Stop();
            LastDestination = transform.position;
            DestinationCommandCount = 0;
            _isActivationComplete = false;
            _isPreparedForSpawn = ValidateConfiguration(out _);
            if (!_isPreparedForSpawn)
            {
                return false;
            }

            AIUnitDefinition definition =
                (AIUnitDefinition)_unitController.Definition;
            _agent.speed = definition.MoveSpeed;
            _agent.angularSpeed = definition.TurnSpeed;
            _agent.stoppingDistance = Mathf.Max(
                0f,
                definition.DefaultAttackDefinition.AttackRange -
                _agent.radius);
            return true;
        }

        public bool CompleteSpawn()
        {
            if (!_isPreparedForSpawn || !gameObject.activeInHierarchy ||
                _agent == null || !_agent.isActiveAndEnabled ||
                !_unitController.SpawnId.IsValid)
            {
                return false;
            }

            bool didWarp = _agent.Warp(transform.position);
            _isActivationComplete = didWarp && _agent.isOnNavMesh;
            if (!_isActivationComplete)
            {
                Stop();
                return false;
            }

            _agent.avoidancePriority = (int)((_unitController.SpawnId.Value - 1) %
                                             k_AvoidancePriorityCount);
            Stop();
            return true;
        }

        public void PrepareForReturn()
        {
            Stop();
            LastDestination = transform.position;
            DestinationCommandCount = 0;
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
        }

        private bool CanControlAgent()
        {
            return _isPreparedForSpawn && _isActivationComplete &&
                   !IsStopped && _unitController != null &&
                   _unitController.IsActive && _agent != null &&
                   _agent.isActiveAndEnabled && _agent.isOnNavMesh;
        }

        private void CacheSiblingComponents()
        {
            _agent = GetComponent<NavMeshAgent>();
            _unitController = GetComponent<UnitController>();
            _unitController?.CacheSiblingComponents();
        }
    }
}
