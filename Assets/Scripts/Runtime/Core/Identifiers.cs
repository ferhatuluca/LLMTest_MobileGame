using System;
using UnityEngine;

namespace MonstersVsZombies.Core
{
    [Serializable]
    public struct UnitId : IEquatable<UnitId>
    {
        [field: SerializeField] public string Value { get; private set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public UnitId(string value)
        {
            Value = value;
        }

        public bool Equals(UnitId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is UnitId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(UnitId left, UnitId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UnitId left, UnitId right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct AttackId : IEquatable<AttackId>
    {
        [field: SerializeField] public string Value { get; private set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public AttackId(string value)
        {
            Value = value;
        }

        public bool Equals(AttackId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AttackId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(AttackId left, AttackId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttackId left, AttackId right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct PoolId : IEquatable<PoolId>
    {
        [field: SerializeField] public string Value { get; private set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public PoolId(string value)
        {
            Value = value;
        }

        public bool Equals(PoolId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PoolId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(PoolId left, PoolId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PoolId left, PoolId right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct SpawnId : IEquatable<SpawnId>, IComparable<SpawnId>
    {
        [field: SerializeField] public long Value { get; private set; }

        public bool IsValid => Value > 0;

        public SpawnId(long value)
        {
            Value = value;
        }

        public int CompareTo(SpawnId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(SpawnId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SpawnId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(SpawnId left, SpawnId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SpawnId left, SpawnId right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct AttackSequenceId : IEquatable<AttackSequenceId>
    {
        [field: SerializeField] public long Value { get; private set; }

        public bool IsValid => Value > 0;

        public AttackSequenceId(long value)
        {
            Value = value;
        }

        public bool Equals(AttackSequenceId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AttackSequenceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(AttackSequenceId left, AttackSequenceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttackSequenceId left, AttackSequenceId right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct DamageCategoryId : IEquatable<DamageCategoryId>
    {
        [field: SerializeField] public string Value { get; private set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public DamageCategoryId(string value)
        {
            Value = value;
        }

        public bool Equals(DamageCategoryId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DamageCategoryId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public static bool operator ==(DamageCategoryId left, DamageCategoryId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DamageCategoryId left, DamageCategoryId right)
        {
            return !left.Equals(right);
        }
    }
}
