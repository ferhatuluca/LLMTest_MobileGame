using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Projectiles
{
    public enum DeliveryContactType
    {
        Ignore,
        World,
        HostileTarget
    }

    /// <summary>
    /// Centralizes delivery layer masks and contact classification so projectile
    /// and hitscan paths share identical source/faction/world rules.
    /// </summary>
    public static class DeliveryCollisionRules
    {
        public const string WorldLayerName = "World";
        public const string UnitTargetLayerName = "UnitTarget";

        public static int CreateDeliveryLayerMask()
        {
            int worldLayer = LayerMask.NameToLayer(WorldLayerName);
            int unitTargetLayer = LayerMask.NameToLayer(UnitTargetLayerName);
            if (worldLayer < 0 || unitTargetLayer < 0)
            {
                throw new System.InvalidOperationException(
                    "World and UnitTarget physics layers must be configured.");
            }

            return (1 << worldLayer) | (1 << unitTargetLayer);
        }

        public static DeliveryContactType Classify(
            Collider collider,
            DamagePayload damagePayload,
            out DamageTargetProxy targetProxy)
        {
            targetProxy = null;
            if (collider == null || !damagePayload.IsValid)
            {
                return DeliveryContactType.Ignore;
            }

            int worldLayer = LayerMask.NameToLayer(WorldLayerName);
            if (collider.gameObject.layer == worldLayer)
            {
                return DeliveryContactType.World;
            }

            int unitTargetLayer = LayerMask.NameToLayer(UnitTargetLayerName);
            if (collider.gameObject.layer != unitTargetLayer ||
                !collider.TryGetComponent(out targetProxy) ||
                !targetProxy.IsConfigured)
            {
                targetProxy = null;
                return DeliveryContactType.Ignore;
            }

            DamageController damageController = targetProxy.DamageController;
            if (targetProxy.SpawnId == damagePayload.SourceSpawnId ||
                !FactionRules.AreHostile(
                    damagePayload.SourceFaction,
                    damageController.Faction) ||
                !damageController.IsAlive ||
                !damageController.IsTargetActive)
            {
                targetProxy = null;
                return DeliveryContactType.Ignore;
            }

            return DeliveryContactType.HostileTarget;
        }
    }
}
