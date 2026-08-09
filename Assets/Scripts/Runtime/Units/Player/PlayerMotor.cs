using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units.Movement;
using UnityEngine;

namespace MonstersVsZombies.Units.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(UnitController))]
    public sealed class PlayerMotor : MonoBehaviour, IUnitMotor, IPoolable
    {
        private CharacterController _characterController;
        private PlayerInputReader _inputReader;
        private UnitController _unitController;
        private StatusEffectController _statusEffectController;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;
        private float _verticalVelocity;

        public bool IsStopped { get; private set; } = true;
        public Transform CameraTransform { get; private set; }

        private void Awake()
        {
            CacheSiblingComponents();
        }

        private void Update()
        {
            AdvanceMovement(Time.deltaTime);
        }

        public bool BindCamera(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return false;
            }

            CameraTransform = cameraTransform;
            return true;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            if (!CanMove())
            {
                return;
            }

            Vector3 moveDirection = worldPosition - transform.position;
            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            MoveInDirection(moveDirection, Time.deltaTime);
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            Vector3 facingDirection = worldPosition - transform.position;
            facingDirection.y = 0f;
            if (facingDirection.sqrMagnitude <= Mathf.Epsilon ||
                _unitController?.Definition == null)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(
                facingDirection.normalized,
                Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                _unitController.Definition.TurnSpeed * Time.deltaTime);
        }

        public void Stop()
        {
            IsStopped = true;
        }

        public void Resume()
        {
            if (_isPreparedForSpawn && _isActivationComplete)
            {
                IsStopped = false;
            }
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            Stop();
            _verticalVelocity = 0f;
            _isActivationComplete = false;
            _isPreparedForSpawn =
                _characterController != null &&
                _inputReader != null &&
                _unitController?.Definition is PlayerUnitDefinition &&
                _statusEffectController != null;
            return _isPreparedForSpawn;
        }

        public bool CompleteSpawn()
        {
            _isActivationComplete =
                _isPreparedForSpawn && gameObject.activeInHierarchy;
            return _isActivationComplete;
        }

        public void PrepareForReturn()
        {
            Stop();
            _verticalVelocity = 0f;
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
        }

        internal void AdvanceMovement(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new System.ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (!CanMove() || CameraTransform == null)
            {
                return;
            }

            Vector2 moveInput = _inputReader.MoveInput;
            Vector3 cameraForward = Vector3.ProjectOnPlane(
                CameraTransform.forward,
                Vector3.up);
            Vector3 cameraRight = Vector3.ProjectOnPlane(
                CameraTransform.right,
                Vector3.up);
            if (cameraForward.sqrMagnitude <= Mathf.Epsilon ||
                cameraRight.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            cameraForward.Normalize();
            cameraRight.Normalize();
            Vector3 moveDirection =
                (cameraRight * moveInput.x) +
                (cameraForward * moveInput.y);
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            MoveInDirection(moveDirection, deltaTime);
        }

        private void MoveInDirection(Vector3 moveDirection, float deltaTime)
        {
            if (moveDirection.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    moveDirection,
                    Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    desiredRotation,
                    _unitController.Definition.TurnSpeed * deltaTime);
            }

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = 0f;
            }

            _verticalVelocity += Physics.gravity.y * deltaTime;
            Vector3 velocity =
                (moveDirection * _unitController.Definition.MoveSpeed) +
                (Vector3.up * _verticalVelocity);
            _characterController.Move(velocity * deltaTime);
        }

        private bool CanMove()
        {
            return _isPreparedForSpawn && _isActivationComplete &&
                   !IsStopped && _unitController != null &&
                   _unitController.IsActive &&
                   _statusEffectController != null &&
                   !_statusEffectController.IsMovementBlocked;
        }

        private void CacheSiblingComponents()
        {
            _characterController = GetComponent<CharacterController>();
            _inputReader = GetComponent<PlayerInputReader>();
            _unitController = GetComponent<UnitController>();
            _statusEffectController = GetComponent<StatusEffectController>();
            _unitController?.CacheSiblingComponents();
        }
    }
}
