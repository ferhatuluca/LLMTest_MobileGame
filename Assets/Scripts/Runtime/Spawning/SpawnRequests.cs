using System;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Spawning
{
    public readonly struct UnitSpawnRequest
    {
        public UnitDefinition Definition { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public SpawnId SourceSpawnId { get; }
        public SpawnReason Reason { get; }

        public bool IsValid =>
            HasValidDefinition && HasValidPose && HasValidMetadata;

        internal bool HasValidDefinition =>
            Definition != null && Definition.Validate().IsValid;
        internal bool HasValidPose =>
            SpawnRequestValidation.IsPoseValid(Position, Rotation);
        internal bool HasValidMetadata =>
            (SourceSpawnId.Value == 0 || SourceSpawnId.IsValid) &&
            Enum.IsDefined(typeof(SpawnReason), Reason);

        public UnitSpawnRequest(
            UnitDefinition definition,
            Vector3 position,
            Quaternion rotation,
            SpawnId sourceSpawnId,
            SpawnReason reason)
        {
            Definition = definition;
            Position = position;
            Rotation = rotation;
            SourceSpawnId = sourceSpawnId;
            Reason = reason;
        }
    }

    public readonly struct ProjectileSpawnRequest
    {
        public ProjectileDefinition Definition { get; }
        public DamagePayload DamagePayload { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public bool IsValid =>
            HasValidDefinition && DamagePayload.IsValid && HasValidPose;

        internal bool HasValidDefinition =>
            Definition != null && Definition.Validate().IsValid;
        internal bool HasValidPose =>
            SpawnRequestValidation.IsPoseValid(Position, Rotation);

        public ProjectileSpawnRequest(
            ProjectileDefinition definition,
            DamagePayload damagePayload,
            Vector3 position,
            Quaternion rotation)
        {
            Definition = definition;
            DamagePayload = damagePayload;
            Position = position;
            Rotation = rotation;
        }
    }

    internal static class SpawnRequestValidation
    {
        public static bool IsPoseValid(Vector3 position, Quaternion rotation)
        {
            return IsFinite(position.x) &&
                   IsFinite(position.y) &&
                   IsFinite(position.z) &&
                   IsFinite(rotation.x) &&
                   IsFinite(rotation.y) &&
                   IsFinite(rotation.z) &&
                   IsFinite(rotation.w) &&
                   Quaternion.Dot(rotation, rotation) > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
