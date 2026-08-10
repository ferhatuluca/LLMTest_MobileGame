using System;
using MonstersVsZombies.Core.Pooling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonstersVsZombies.Units.Player
{
    /// <summary>
    /// Owns Player Input System subscriptions and exposes normalized movement,
    /// aim, and weapon-cycle intent as gameplay-facing state/events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, IPoolable
    {
        [field: SerializeField] public InputActionReference MoveAction { get; private set; }
        [field: SerializeField] public InputActionReference PreviousWeaponAction { get; private set; }
        [field: SerializeField] public InputActionReference NextWeaponAction { get; private set; }
        [field: SerializeField] public InputActionReference DebugAttackAction { get; private set; }

        private bool _hasSubscribed;
        private bool _isPreparedForSpawn;

        public event Action PreviousWeaponRequested;
        public event Action NextWeaponRequested;
        public event Action DebugAttackRequested;

        public bool IsInputEnabled { get; private set; }
        public Vector2 MoveInput => IsInputEnabled && MoveAction?.action != null
            ? MoveAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        private void Awake()
        {
            EnsureSubscriptions();
        }

        private void OnDestroy()
        {
            DisableInput();
            ReleaseSubscriptions();
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (!IsActionValid(
                    MoveAction,
                    InputActionType.Value,
                    "Vector2") ||
                !IsActionValid(
                    PreviousWeaponAction,
                    InputActionType.Button,
                    "Button") ||
                !IsActionValid(
                    NextWeaponAction,
                    InputActionType.Button,
                    "Button") ||
                (DebugAttackAction != null &&
                 !IsActionValid(
                     DebugAttackAction,
                     InputActionType.Button,
                     "Button")))
            {
                failureMessage =
                    "PlayerInputReader requires Move, PreviousWeapon, and NextWeapon action references with compatible control types.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool PrepareForSpawn()
        {
            DisableInput();
            EnsureSubscriptions();
            _isPreparedForSpawn = ValidateConfiguration(out _);
            return _isPreparedForSpawn;
        }

        public bool CompleteSpawn()
        {
            if (!_isPreparedForSpawn || !gameObject.activeInHierarchy)
            {
                return false;
            }

            MoveAction.action.Enable();
            PreviousWeaponAction.action.Enable();
            NextWeaponAction.action.Enable();
            DebugAttackAction?.action?.Enable();
            IsInputEnabled = true;
            return true;
        }

        public void PrepareForReturn()
        {
            DisableInput();
            _isPreparedForSpawn = false;
        }

        internal void Configure(
            InputActionReference moveAction,
            InputActionReference previousWeaponAction,
            InputActionReference nextWeaponAction,
            InputActionReference debugAttackAction = null)
        {
            ReleaseSubscriptions();
            MoveAction = moveAction;
            PreviousWeaponAction = previousWeaponAction;
            NextWeaponAction = nextWeaponAction;
            DebugAttackAction = debugAttackAction;
            EnsureSubscriptions();
        }

        private void EnsureSubscriptions()
        {
            if (_hasSubscribed || !ValidateConfiguration(out _))
            {
                return;
            }

            PreviousWeaponAction.action.performed += HandlePreviousWeapon;
            NextWeaponAction.action.performed += HandleNextWeapon;
            if (DebugAttackAction?.action != null)
            {
                DebugAttackAction.action.performed += HandleDebugAttack;
            }

            _hasSubscribed = true;
        }

        private void ReleaseSubscriptions()
        {
            if (!_hasSubscribed)
            {
                return;
            }

            PreviousWeaponAction.action.performed -= HandlePreviousWeapon;
            NextWeaponAction.action.performed -= HandleNextWeapon;
            if (DebugAttackAction?.action != null)
            {
                DebugAttackAction.action.performed -= HandleDebugAttack;
            }

            _hasSubscribed = false;
        }

        private void DisableInput()
        {
            IsInputEnabled = false;
            MoveAction?.action?.Disable();
            PreviousWeaponAction?.action?.Disable();
            NextWeaponAction?.action?.Disable();
            DebugAttackAction?.action?.Disable();
        }

        private void HandlePreviousWeapon(InputAction.CallbackContext context)
        {
            if (IsInputEnabled)
            {
                PreviousWeaponRequested?.Invoke();
            }
        }

        private void HandleNextWeapon(InputAction.CallbackContext context)
        {
            if (IsInputEnabled)
            {
                NextWeaponRequested?.Invoke();
            }
        }

        private void HandleDebugAttack(InputAction.CallbackContext context)
        {
            if (IsInputEnabled)
            {
                DebugAttackRequested?.Invoke();
            }
        }

        private static bool IsActionValid(
            InputActionReference actionReference,
            InputActionType expectedType,
            string expectedControlType)
        {
            InputAction action = actionReference?.action;
            return action != null && action.type == expectedType &&
                   string.Equals(
                       action.expectedControlType,
                       expectedControlType,
                       StringComparison.Ordinal);
        }
    }
}
