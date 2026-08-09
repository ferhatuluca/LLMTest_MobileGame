using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Units.Player
{
    [Serializable]
    public sealed class PlayerWeaponSlot
    {
        [field: SerializeField] public WeaponDefinition Definition { get; private set; }
        [field: SerializeField] public GameObject VisualInstance { get; private set; }

        public PlayerWeaponSlot(
            WeaponDefinition definition,
            GameObject visualInstance)
        {
            Definition = definition;
            VisualInstance = visualInstance;
        }
    }

    public readonly struct PlayerWeaponChangedEvent
    {
        public UnitController Player { get; }
        public WeaponDefinition PreviousWeapon { get; }
        public WeaponDefinition CurrentWeapon { get; }
        public int CurrentIndex { get; }

        public PlayerWeaponChangedEvent(
            UnitController player,
            WeaponDefinition previousWeapon,
            WeaponDefinition currentWeapon,
            int currentIndex)
        {
            Player = player;
            PreviousWeapon = previousWeapon;
            CurrentWeapon = currentWeapon;
            CurrentIndex = currentIndex;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(AttackController))]
    public sealed class PlayerWeaponController : MonoBehaviour, IPoolable
    {
        [SerializeField] private PlayerWeaponSlot[] _weaponSlots =
            Array.Empty<PlayerWeaponSlot>();

        private PlayerInputReader _inputReader;
        private AttackController _attackController;
        private UnitController _unitController;
        private bool _hasSubscribed;
        private bool _isPreparedForSpawn;

        public event Action<PlayerWeaponChangedEvent> WeaponChanged;

        public int WeaponCount => _weaponSlots?.Length ?? 0;
        public int CurrentWeaponIndex { get; private set; }
        public WeaponDefinition CurrentWeapon => WeaponCount == 0
            ? null
            : _weaponSlots[CurrentWeaponIndex].Definition;

        private void Awake()
        {
            CacheSiblingComponents();
            EnsureSubscriptions();
        }

        private void OnDestroy()
        {
            ReleaseSubscriptions();
        }

        public PlayerWeaponSlot GetWeaponSlot(int index)
        {
            return _weaponSlots[index];
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            failureMessage = string.Empty;
            CacheSiblingComponents();
            if (_inputReader == null || _attackController == null ||
                _unitController == null || WeaponCount == 0)
            {
                failureMessage =
                    "PlayerWeaponController requires input, attack, unit, and weapon-slot configuration.";
                return false;
            }

            HashSet<WeaponId> weaponIds = new HashSet<WeaponId>();
            foreach (PlayerWeaponSlot weaponSlot in _weaponSlots)
            {
                if (weaponSlot?.Definition == null ||
                    weaponSlot.VisualInstance == null ||
                    !weaponSlot.Definition.Validate().IsValid ||
                    !weaponIds.Add(weaponSlot.Definition.WeaponId) ||
                    !_attackController.ValidateExecutorForDefinition(
                        weaponSlot.Definition.AttackDefinition,
                        out failureMessage))
                {
                    if (string.IsNullOrWhiteSpace(failureMessage))
                    {
                        failureMessage =
                            "Player weapon slots require unique valid definitions, nested visuals, and one compatible executor.";
                    }

                    return false;
                }
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool SelectNextWeapon()
        {
            if (WeaponCount == 0)
            {
                return false;
            }

            return SelectWeapon(WeaponIndexCycle.GetNextIndex(
                CurrentWeaponIndex,
                WeaponCount));
        }

        public bool SelectPreviousWeapon()
        {
            if (WeaponCount == 0)
            {
                return false;
            }

            return SelectWeapon(WeaponIndexCycle.GetPreviousIndex(
                CurrentWeaponIndex,
                WeaponCount));
        }

        public bool SelectWeapon(int index)
        {
            if (!_isPreparedForSpawn || WeaponCount == 0 ||
                index < 0 || index >= WeaponCount)
            {
                return false;
            }

            return ApplySelection(index, true);
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            EnsureSubscriptions();
            _isPreparedForSpawn = false;
            if (!(_unitController?.Definition is PlayerUnitDefinition) ||
                !ValidateConfiguration(out _))
            {
                return false;
            }

            CurrentWeaponIndex = 0;
            _isPreparedForSpawn = ApplySelection(0, false);
            return _isPreparedForSpawn;
        }

        public bool CompleteSpawn()
        {
            return _isPreparedForSpawn && gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
            if (WeaponCount > 0)
            {
                CurrentWeaponIndex = 0;
                SetVisualSelection(0);
            }

            _isPreparedForSpawn = false;
        }

        internal void Configure(PlayerWeaponSlot[] weaponSlots)
        {
            _weaponSlots = weaponSlots == null
                ? Array.Empty<PlayerWeaponSlot>()
                : (PlayerWeaponSlot[])weaponSlots.Clone();
        }

        private bool ApplySelection(int index, bool publishChange)
        {
            WeaponDefinition previousWeapon = CurrentWeapon;
            PlayerWeaponSlot selectedSlot = _weaponSlots[index];
            if (!_attackController.SetAttackDefinition(
                    selectedSlot.Definition.AttackDefinition))
            {
                return false;
            }

            CurrentWeaponIndex = index;
            SetVisualSelection(index);
            if (publishChange && previousWeapon != selectedSlot.Definition)
            {
                WeaponChanged?.Invoke(new PlayerWeaponChangedEvent(
                    _unitController,
                    previousWeapon,
                    selectedSlot.Definition,
                    CurrentWeaponIndex));
            }

            return true;
        }

        private void SetVisualSelection(int selectedIndex)
        {
            for (int slotIndex = 0;
                 slotIndex < WeaponCount;
                 slotIndex++)
            {
                GameObject visualInstance = _weaponSlots[slotIndex].VisualInstance;
                if (visualInstance != null)
                {
                    visualInstance.SetActive(slotIndex == selectedIndex);
                }
            }
        }

        private void HandlePreviousWeapon()
        {
            if (_unitController != null && _unitController.IsActive)
            {
                SelectPreviousWeapon();
            }
        }

        private void HandleNextWeapon()
        {
            if (_unitController != null && _unitController.IsActive)
            {
                SelectNextWeapon();
            }
        }

        private void CacheSiblingComponents()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _attackController = GetComponent<AttackController>();
            _unitController = GetComponent<UnitController>();
        }

        private void EnsureSubscriptions()
        {
            if (_hasSubscribed || _inputReader == null)
            {
                return;
            }

            _inputReader.PreviousWeaponRequested += HandlePreviousWeapon;
            _inputReader.NextWeaponRequested += HandleNextWeapon;
            _hasSubscribed = true;
        }

        private void ReleaseSubscriptions()
        {
            if (!_hasSubscribed || _inputReader == null)
            {
                return;
            }

            _inputReader.PreviousWeaponRequested -= HandlePreviousWeapon;
            _inputReader.NextWeaponRequested -= HandleNextWeapon;
            _hasSubscribed = false;
        }
    }
}
