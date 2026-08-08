using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units.Movement;
using UnityEngine;

namespace MonstersVsZombies.Units
{
    [DisallowMultipleComponent]
    public sealed class UnitController : MonoBehaviour
    {
        [field: SerializeField] public UnitDefinition Definition { get; private set; }

        public UnitFaction Faction { get; private set; }
        public SpawnId SpawnId { get; private set; }
        public bool IsActive { get; private set; }
        public HealthController HealthController { get; private set; }
        public DamageController DamageController { get; private set; }
        public StatusEffectController StatusEffectController { get; private set; }
        public UnitLifecycleController LifecycleController { get; private set; }
        public IUnitMotor UnitMotor { get; private set; }

        private void Awake()
        {
            CacheSiblingComponents();
        }

        private void OnValidate()
        {
            CacheSiblingComponents();
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

        internal bool ConfigureSpawn(UnitDefinition definition, SpawnId spawnId)
        {
            if (definition == null || !definition.Validate().IsValid || !spawnId.IsValid || IsActive)
            {
                return false;
            }

            Definition = definition;
            Faction = definition.Faction;
            SpawnId = spawnId;
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
    }
}
