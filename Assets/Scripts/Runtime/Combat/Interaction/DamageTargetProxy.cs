using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Interaction
{
    /// <summary>
    /// Maps a hurtbox collider back to its owning unit identity and concrete
    /// DamageController, allowing multi-collider units to deduplicate correctly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageTargetProxy : MonoBehaviour, IPoolable
    {
        public UnitController UnitController { get; private set; }
        public DamageController DamageController { get; private set; }
        public Collider TargetCollider { get; private set; }
        public SpawnId SpawnId => UnitController == null ? default : UnitController.SpawnId;
        public bool IsConfigured => UnitController != null &&
                                    DamageController != null &&
                                    TargetCollider != null;

        private void Awake()
        {
            CacheOwnerReferences();
        }

        private void OnValidate()
        {
            CacheOwnerReferences();
        }

        public bool ValidateReferences(out string failureMessage)
        {
            CacheOwnerReferences();
            if (UnitController == null)
            {
                failureMessage = $"{name} requires an owning {nameof(UnitController)} in its parent chain.";
                return false;
            }

            if (DamageController == null)
            {
                failureMessage = $"{name}'s owner requires a {nameof(DamageController)} sibling.";
                return false;
            }

            if (TargetCollider == null)
            {
                failureMessage = $"{name} requires a hurtbox Collider on the same GameObject.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool PrepareForSpawn()
        {
            return ValidateReferences(out _);
        }

        public bool CompleteSpawn()
        {
            return IsConfigured && gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
        }

        internal void CacheOwnerReferences()
        {
            TargetCollider = GetComponent<Collider>();
            UnitController = GetComponentInParent<UnitController>(true);
            UnitController?.CacheSiblingComponents();
            DamageController = UnitController == null
                ? null
                : UnitController.DamageController;
        }
    }
}
