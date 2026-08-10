using MonstersVsZombies.Combat.Health;
using UnityEngine;
using UnityEngine.UI;

namespace MonstersVsZombies.Units.Player
{
    /// <summary>
    /// Observes the active Player's health and weapon selection and updates the
    /// lightweight sandbox HUD without owning gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHudController : MonoBehaviour
    {
        [field: SerializeField] public Text HealthText { get; private set; }
        [field: SerializeField] public Text WeaponText { get; private set; }

        private PlayerWeaponController _weaponController;

        public UnitController Player { get; private set; }

        private void OnDestroy()
        {
            Unbind();
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (HealthText == null || WeaponText == null)
            {
                failureMessage =
                    "PlayerHudController requires health and weapon Text references.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool Bind(UnitController player)
        {
            Unbind();
            if (!ValidateConfiguration(out _) || player == null ||
                player.HealthController == null)
            {
                return false;
            }

            PlayerWeaponController weaponController =
                player.GetComponent<PlayerWeaponController>();
            if (weaponController == null)
            {
                return false;
            }

            Player = player;
            _weaponController = weaponController;
            Player.HealthController.HealthChanged += HandleHealthChanged;
            _weaponController.WeaponChanged += HandleWeaponChanged;
            RefreshHealth();
            RefreshWeapon();
            return true;
        }

        public void Unbind()
        {
            if (Player?.HealthController != null)
            {
                Player.HealthController.HealthChanged -= HandleHealthChanged;
            }

            if (_weaponController != null)
            {
                _weaponController.WeaponChanged -= HandleWeaponChanged;
            }

            Player = null;
            _weaponController = null;
        }

        private void HandleHealthChanged(HealthChangedEvent healthChangedEvent)
        {
            RefreshHealth();
        }

        private void HandleWeaponChanged(PlayerWeaponChangedEvent weaponChangedEvent)
        {
            RefreshWeapon();
        }

        private void RefreshHealth()
        {
            if (Player?.HealthController != null)
            {
                HealthText.text =
                    $"Health: {Player.HealthController.CurrentHealth:0}/{Player.HealthController.MaximumHealth:0}";
            }
        }

        private void RefreshWeapon()
        {
            if (_weaponController?.CurrentWeapon != null)
            {
                WeaponText.text =
                    $"Weapon: {_weaponController.CurrentWeapon.DisplayName}";
            }
        }
    }
}
