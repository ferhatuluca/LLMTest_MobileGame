using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units.Movement;
using UnityEngine;

namespace MonstersVsZombies.Units
{
    /// <summary>
    /// Acts as a unit's composition root. It owns spawn identity and cached
    /// sibling capabilities, while health, damage, movement, targeting, attack,
    /// status, and lifecycle behavior remain in their dedicated controllers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitController : MonoBehaviour
    {
        private Renderer[] _factionRenderers;
        private MaterialPropertyBlock _factionPropertyBlock;

        [field: SerializeField] public UnitDefinition Definition { get; private set; }

        public UnitFaction Faction { get; private set; }
        public SpawnId SpawnId { get; private set; }
        public bool IsActive { get; private set; }
        public HealthController HealthController { get; private set; }
        public DamageController DamageController { get; private set; }
        public StatusEffectController StatusEffectController { get; private set; }
        public UnitLifecycleController LifecycleController { get; private set; }
        public TargetingController TargetingController { get; private set; }
        public AttackController AttackController { get; private set; }
        public IUnitMotor UnitMotor { get; private set; }

        private void Awake()
        {
            CacheSiblingComponents();
            CacheFactionVisuals();
        }

        private void OnValidate()
        {
            CacheSiblingComponents();
            CacheFactionVisuals();
        }

        public bool ValidateCoreComponents(out string failureMessage)
        {
            CacheSiblingComponents();
            if (HealthController == null)
            {
                failureMessage = $"{name} requires a {nameof(HealthController)} sibling.";
                return false;
            }

            if (DamageController == null)
            {
                failureMessage = $"{name} requires a {nameof(DamageController)} sibling.";
                return false;
            }

            if (StatusEffectController == null)
            {
                failureMessage = $"{name} requires a {nameof(StatusEffectController)} sibling.";
                return false;
            }

            if (LifecycleController == null)
            {
                failureMessage = $"{name} requires a {nameof(UnitLifecycleController)} sibling.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool ValidateGameplayComponents(out string failureMessage)
        {
            CacheSiblingComponents();
            if (!ValidateCoreComponents(out failureMessage))
            {
                return false;
            }

            if (TargetingController == null)
            {
                failureMessage = $"{name} requires a {nameof(TargetingController)} sibling.";
                return false;
            }

            if (AttackController == null)
            {
                failureMessage = $"{name} requires an {nameof(AttackController)} sibling.";
                return false;
            }

            if (!TargetingController.ValidateConfiguration(out failureMessage))
            {
                return false;
            }

            return AttackController.ValidateConfiguration(out failureMessage);
        }

        internal bool ConfigureSpawn(UnitDefinition definition, SpawnId spawnId)
        {
            if (definition == null || !definition.Validate().IsValid || !spawnId.IsValid || IsActive)
            {
                return false;
            }

            Definition = definition;
            Faction = definition.Faction;
            SpawnId = spawnId;
            ApplyFactionVisuals();
            return true;
        }

        internal void MarkActive()
        {
            if (Definition == null || !SpawnId.IsValid)
            {
                throw new System.InvalidOperationException(
                    "A unit must have a definition and spawn ID before becoming active.");
            }

            IsActive = true;
        }

        internal void MarkInactive()
        {
            IsActive = false;
        }

        internal void ClearSpawnIdentity()
        {
            IsActive = false;
            Definition = null;
            Faction = default;
            SpawnId = default;
        }

        internal void CacheSiblingComponents()
        {
            HealthController = GetComponent<HealthController>();
            DamageController = GetComponent<DamageController>();
            StatusEffectController = GetComponent<StatusEffectController>();
            LifecycleController = GetComponent<UnitLifecycleController>();
            TargetingController = GetComponent<TargetingController>();
            AttackController = GetComponent<AttackController>();
            UnitMotor = null;

            MonoBehaviour[] siblingBehaviours = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour siblingBehaviour in siblingBehaviours)
            {
                if (siblingBehaviour is IUnitMotor unitMotor)
                {
                    UnitMotor = unitMotor;
                    break;
                }
            }
        }

        private void CacheFactionVisuals()
        {
            _factionRenderers = GetComponentsInChildren<Renderer>(true);
            _factionPropertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyFactionVisuals()
        {
            if (_factionRenderers == null || _factionPropertyBlock == null)
            {
                CacheFactionVisuals();
            }

            FactionVisuals.Apply(
                _factionRenderers,
                Faction,
                _factionPropertyBlock);
        }
    }
}
