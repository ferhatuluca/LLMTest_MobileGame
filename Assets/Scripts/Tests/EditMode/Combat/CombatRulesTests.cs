using MonstersVsZombies.Combat;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Player;
using MonstersVsZombies.Units.Special;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode.Combat
{
    public sealed class CombatRulesTests
    {
        [TestCase(UnitFaction.Player, UnitFaction.Enemy, true)]
        [TestCase(UnitFaction.Ally, UnitFaction.Enemy, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Player, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Ally, true)]
        [TestCase(UnitFaction.Player, UnitFaction.Ally, false)]
        [TestCase(UnitFaction.Ally, UnitFaction.Player, false)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Enemy, false)]
        public void AreHostile_ReturnsExpectedFactionRelationship(
            UnitFaction attacker,
            UnitFaction target,
            bool expected)
        {
            Assert.That(FactionRules.AreHostile(attacker, target), Is.EqualTo(expected));
        }

        [Test]
        public void IsWithinRange_UsesPlanarDistanceAndIncludesBoundary()
        {
            Vector3 source = new Vector3(0f, 100f, 0f);
            Vector3 target = new Vector3(3f, -100f, 4f);

            Assert.That(CombatRangeRules.IsWithinRange(source, target, 5f), Is.True);
            Assert.That(CombatRangeRules.IsWithinRange(source, target, 4.99f), Is.False);
        }

        [Test]
        public void GetWeaponIndex_WrapsInBothDirections()
        {
            Assert.That(WeaponIndexCycle.GetNextIndex(2, 3), Is.Zero);
            Assert.That(WeaponIndexCycle.GetPreviousIndex(0, 3), Is.EqualTo(2));
        }

        [Test]
        public void RecordInteraction_StunsOnFirstAndEveryThirdLaterSuccess()
        {
            StunnerHitSchedule schedule = new StunnerHitSchedule();
            InteractionResult applied = CreateAppliedInteraction();

            Assert.That(schedule.ShouldStunNextSuccessfulHit, Is.True);
            schedule.RecordInteraction(applied);
            Assert.That(schedule.ShouldStunNextSuccessfulHit, Is.False);
            schedule.RecordInteraction(applied);
            schedule.RecordInteraction(applied);
            Assert.That(schedule.ShouldStunNextSuccessfulHit, Is.True);
        }

        [Test]
        public void RecordInteraction_IgnoresRejectedHits()
        {
            StunnerHitSchedule schedule = new StunnerHitSchedule();
            InteractionResult rejected = InteractionResult.CreateRejected(
                InteractionOutcome.InvalidFaction,
                new AttackKey(new SpawnId(1), new AttackSequenceId(1)),
                new SpawnId(2));

            schedule.RecordInteraction(rejected);

            Assert.That(schedule.SuccessfulHitCount, Is.Zero);
            Assert.That(schedule.ShouldStunNextSuccessfulHit, Is.True);
        }

        [Test]
        public void FillRadialPositions_CreatesThreeEvenlySpacedChildren()
        {
            Vector3[] positions = new Vector3[MiniDivisibleSpawnFormation.ChildCount];

            MiniDivisibleSpawnFormation.FillRadialPositions(
                Vector3.zero,
                Vector3.forward,
                2f,
                positions);

            Assert.That(positions, Has.Length.EqualTo(3));
            foreach (Vector3 position in positions)
            {
                Assert.That(position.y, Is.Zero.Within(0.001f));
                Assert.That(position.magnitude, Is.EqualTo(2f).Within(0.001f));
            }

            Assert.That(Vector3.Distance(positions[0], positions[1]),
                Is.EqualTo(Vector3.Distance(positions[1], positions[2])).Within(0.001f));
        }

        private static InteractionResult CreateAppliedInteraction()
        {
            AttackKey attackKey = new AttackKey(
                new SpawnId(1),
                new AttackSequenceId(1));
            return InteractionResult.CreateApplied(
                attackKey,
                new SpawnId(2),
                DamageResult.CreateApplied(1f, false));
        }
    }
}
