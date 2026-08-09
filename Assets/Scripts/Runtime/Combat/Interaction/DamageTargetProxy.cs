using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Interaction
{
    [DisallowMultipleComponent]
    public sealed class DamageTargetProxy : MonoBehaviour
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
