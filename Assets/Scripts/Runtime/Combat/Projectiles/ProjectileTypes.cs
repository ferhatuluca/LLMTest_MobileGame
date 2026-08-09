using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Combat.Projectiles
{
    public enum ProjectileTerminationReason
    {
        None,
        HostileHit,
        WorldImpact,
        LifetimeExpired,
        Explosion
    }

    public readonly struct ProjectileTerminationEvent
    {
        public PoolId PoolId { get; }
        public AttackKey AttackKey { get; }
        public ProjectileTerminationReason Reason { get; }
        public Vector3 Position { get; }

        public ProjectileTerminationEvent(
            PoolId poolId,
            AttackKey attackKey,
            ProjectileTerminationReason reason,
            Vector3 position)
        {
            PoolId = poolId;
            AttackKey = attackKey;
            Reason = reason;
            Position = position;
        }
    }

    internal interface IProjectileMotion
    {
        AttackDeliveryType DeliveryType { get; }
        bool ValidateConfiguration(out string failureMessage);
        bool PrepareMotion(
            ProjectileController projectileController,
            ProjectileDefinition projectileDefinition);
        bool StartMotion();
        void AdvanceTime(float deltaTime);
        void HandleTimerExpired();
        void ResetMotion();
    }
}
