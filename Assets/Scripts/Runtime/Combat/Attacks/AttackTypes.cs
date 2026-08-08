using System;
using MonstersVsZombies.Core;

namespace MonstersVsZombies.Combat.Attacks
{
    public enum AttackDeliveryType
    {
        Unspecified,
        Melee,
        Projectile,
        Grenade,
        Hitscan
    }

    public readonly struct AttackKey : IEquatable<AttackKey>
    {
        public SpawnId SourceSpawnId { get; }
        public AttackSequenceId SequenceId { get; }
        public bool IsValid => SourceSpawnId.IsValid && SequenceId.IsValid;

        public AttackKey(SpawnId sourceSpawnId, AttackSequenceId sequenceId)
        {
            SourceSpawnId = sourceSpawnId;
            SequenceId = sequenceId;
        }

        public bool Equals(AttackKey other)
        {
            return SourceSpawnId == other.SourceSpawnId && SequenceId == other.SequenceId;
        }

        public override bool Equals(object obj)
        {
            return obj is AttackKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SourceSpawnId.GetHashCode() * 397) ^ SequenceId.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{SourceSpawnId}:{SequenceId}";
        }

        public static bool operator ==(AttackKey left, AttackKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttackKey left, AttackKey right)
        {
            return !left.Equals(right);
        }
    }
}
