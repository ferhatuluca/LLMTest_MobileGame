using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Core;

namespace MonstersVsZombies.Combat.Interaction
{
    public sealed class AttackHitLedger
    {
        private readonly HashSet<SpawnId> _acceptedTargetSpawnIds =
            new HashSet<SpawnId>();

        public AttackKey AttackKey { get; private set; }
        public int AcceptedTargetCount => _acceptedTargetSpawnIds.Count;
        public bool IsActive => AttackKey.IsValid;

        public void BeginAttack(AttackKey attackKey)
        {
            if (!attackKey.IsValid)
            {
                throw new ArgumentException(
                    "A hit ledger requires a valid attack key.",
                    nameof(attackKey));
            }

            AttackKey = attackKey;
            _acceptedTargetSpawnIds.Clear();
        }

        public void Reset()
        {
            AttackKey = default;
            _acceptedTargetSpawnIds.Clear();
        }

        public bool HasAcceptedHit(AttackKey attackKey, SpawnId targetSpawnId)
        {
            return attackKey.IsValid &&
                   attackKey == AttackKey &&
                   targetSpawnId.IsValid &&
                   _acceptedTargetSpawnIds.Contains(targetSpawnId);
        }

        internal bool RecordAcceptedHit(AttackKey attackKey, SpawnId targetSpawnId)
        {
            if (attackKey != AttackKey || !attackKey.IsValid || !targetSpawnId.IsValid)
            {
                return false;
            }

            return _acceptedTargetSpawnIds.Add(targetSpawnId);
        }

        internal bool RemoveAcceptedHit(AttackKey attackKey, SpawnId targetSpawnId)
        {
            return attackKey == AttackKey &&
                   attackKey.IsValid &&
                   targetSpawnId.IsValid &&
                   _acceptedTargetSpawnIds.Remove(targetSpawnId);
        }
    }
}
