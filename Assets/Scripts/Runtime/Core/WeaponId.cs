using System;
using UnityEngine;

namespace MonstersVsZombies.Core
{
    [Serializable]
    public struct WeaponId : IEquatable<WeaponId>
    {
        [field: SerializeField] public string Value { get; private set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public WeaponId(string value)
        {
            Value = value;
        }

        public bool Equals(WeaponId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(WeaponId left, WeaponId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeaponId left, WeaponId right)
        {
            return !left.Equals(right);
        }
    }
}
