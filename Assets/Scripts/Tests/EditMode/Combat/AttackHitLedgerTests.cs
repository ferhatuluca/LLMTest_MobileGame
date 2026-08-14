using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using NUnit.Framework;

namespace MonstersVsZombies.Tests.EditMode.Combat
{
    public sealed class AttackHitLedgerTests
    {
        private readonly AttackKey _firstAttack = new AttackKey(
            new SpawnId(10),
            new AttackSequenceId(1));

        [Test]
        public void RecordAcceptedHit_AllowsOneHitPerTargetAndAttack()
        {
            AttackHitLedger ledger = new AttackHitLedger();
            SpawnId target = new SpawnId(20);
            ledger.BeginAttack(_firstAttack);

            Assert.That(ledger.RecordAcceptedHit(_firstAttack, target), Is.True);
            Assert.That(ledger.RecordAcceptedHit(_firstAttack, target), Is.False);
            Assert.That(ledger.HasAcceptedHit(_firstAttack, target), Is.True);
        }

        [Test]
        public void BeginAttack_ClearsHitsFromPreviousAttack()
        {
            AttackHitLedger ledger = new AttackHitLedger();
            SpawnId target = new SpawnId(20);
            ledger.BeginAttack(_firstAttack);
            ledger.RecordAcceptedHit(_firstAttack, target);
            AttackKey nextAttack = new AttackKey(
                new SpawnId(10),
                new AttackSequenceId(2));

            ledger.BeginAttack(nextAttack);

            Assert.That(ledger.HasAcceptedHit(nextAttack, target), Is.False);
            Assert.That(ledger.AcceptedTargetCount, Is.Zero);
        }

        [Test]
        public void Reset_DeactivatesTheLedger()
        {
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(_firstAttack);

            ledger.Reset();

            Assert.That(ledger.IsActive, Is.False);
            Assert.That(ledger.AcceptedTargetCount, Is.Zero);
        }
    }
}
