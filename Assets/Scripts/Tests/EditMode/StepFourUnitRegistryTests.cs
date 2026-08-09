using System.Collections.Generic;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepFourUnitRegistryTests
    {
        private StepFourTestFactory _factory;
        private UnitRegistry _registry;

        [SetUp]
        public void CreateFixture()
        {
            _factory = new StepFourTestFactory();
            _registry = _factory.CreateComponent<UnitRegistry>("UnitRegistry");
        }

        [TearDown]
        public void DestroyFixture()
        {
            _factory.Dispose();
        }

        [Test]
        public void Register_ActiveSpawnAddsLookupCountSnapshotAndEventOnce()
        {
            StepFourUnitFixture fixture =
                _factory.CreateActiveUnit(20, UnitFaction.Enemy);
            int eventCount = 0;
            UnitRegistryEvent observedEvent = default;
            _registry.UnitRegistered += registryEvent =>
            {
                eventCount++;
                observedEvent = registryEvent;
            };

            bool wasRegistered = _registry.Register(fixture.Unit);
            List<UnitController> snapshot = new List<UnitController>();

            Assert.That(wasRegistered, Is.True);
            Assert.That(_registry.Count, Is.EqualTo(1));
            Assert.That(_registry.GetFactionCount(UnitFaction.Enemy), Is.EqualTo(1));
            Assert.That(
                _registry.TryGetUnit(new SpawnId(20), out UnitController registeredUnit),
                Is.True);
            Assert.That(registeredUnit, Is.SameAs(fixture.Unit));
            Assert.That(_registry.CopySnapshot(snapshot), Is.EqualTo(1));
            Assert.That(snapshot[0], Is.SameAs(fixture.Unit));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(observedEvent.Unit, Is.SameAs(fixture.Unit));
            Assert.That(observedEvent.SpawnId, Is.EqualTo(new SpawnId(20)));
            Assert.That(observedEvent.Faction, Is.EqualTo(UnitFaction.Enemy));
        }

        [Test]
        public void Register_InactiveSpawnIsRejectedWithoutMutation()
        {
            StepFourUnitFixture fixture =
                _factory.CreatePreparedUnit(1, UnitFaction.Player);
            int eventCount = 0;
            _registry.UnitRegistered += _ => eventCount++;

            Assert.That(_registry.Register(fixture.Unit), Is.False);
            Assert.That(_registry.Count, Is.Zero);
            Assert.That(_registry.GetFactionCount(UnitFaction.Player), Is.Zero);
            Assert.That(eventCount, Is.Zero);
        }

        [Test]
        public void Register_SameActiveSpawnTwiceIsIdempotent()
        {
            StepFourUnitFixture fixture =
                _factory.CreateActiveUnit(1, UnitFaction.Player);
            int eventCount = 0;
            _registry.UnitRegistered += _ => eventCount++;

            Assert.That(_registry.Register(fixture.Unit), Is.True);
            Assert.That(_registry.Register(fixture.Unit), Is.False);
            Assert.That(_registry.Count, Is.EqualTo(1));
            Assert.That(_registry.GetFactionCount(UnitFaction.Player), Is.EqualTo(1));
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void Register_DuplicateSpawnIdDoesNotReplaceOriginalUnit()
        {
            StepFourUnitFixture original =
                _factory.CreateActiveUnit(1, UnitFaction.Player);
            StepFourUnitFixture duplicate =
                _factory.CreateActiveUnit(1, UnitFaction.Enemy);

            Assert.That(_registry.Register(original.Unit), Is.True);
            Assert.That(_registry.Register(duplicate.Unit), Is.False);
            Assert.That(
                _registry.TryGetUnit(new SpawnId(1), out UnitController registeredUnit),
                Is.True);
            Assert.That(registeredUnit, Is.SameAs(original.Unit));
            Assert.That(_registry.GetFactionCount(UnitFaction.Player), Is.EqualTo(1));
            Assert.That(_registry.GetFactionCount(UnitFaction.Enemy), Is.Zero);
        }

        [Test]
        public void Return_LogicalDespawnRemovesBeforeIdentityIsClearedExactlyOnce()
        {
            StepFourUnitFixture fixture =
                _factory.CreateActiveUnit(7, UnitFaction.Ally);
            Assert.That(_registry.Register(fixture.Unit), Is.True);
            int removalCount = 0;
            UnitRegistryEvent observedEvent = default;
            bool identityWasValidDuringRemoval = false;
            _registry.UnitRemoved += registryEvent =>
            {
                removalCount++;
                observedEvent = registryEvent;
                identityWasValidDuringRemoval = registryEvent.Unit.SpawnId.IsValid;
            };

            fixture.Lifecycle.PrepareForReturn();
            fixture.Lifecycle.PrepareForReturn();

            Assert.That(removalCount, Is.EqualTo(1));
            Assert.That(identityWasValidDuringRemoval, Is.True);
            Assert.That(observedEvent.SpawnId, Is.EqualTo(new SpawnId(7)));
            Assert.That(observedEvent.Faction, Is.EqualTo(UnitFaction.Ally));
            Assert.That(_registry.Count, Is.Zero);
            Assert.That(_registry.GetFactionCount(UnitFaction.Ally), Is.Zero);
            Assert.That(fixture.Unit.SpawnId.IsValid, Is.False);
        }

        [Test]
        public void DeathThenPoolReturn_PublishesOneRegistryRemoval()
        {
            StepFourUnitFixture fixture =
                _factory.CreateActiveUnit(4, UnitFaction.Enemy);
            Assert.That(_registry.Register(fixture.Unit), Is.True);
            int removalCount = 0;
            _registry.UnitRemoved += _ => removalCount++;

            DamageResult damageResult = fixture.Damage.ApplyDamage(CreateHitContext(
                fixture,
                new SpawnId(99),
                UnitFaction.Player,
                100f));
            fixture.Lifecycle.PrepareForReturn();

            Assert.That(damageResult.TargetDied, Is.True);
            Assert.That(removalCount, Is.EqualTo(1));
            Assert.That(_registry.Count, Is.Zero);
        }

        [Test]
        public void Respawn_NewIdentityAndFactionLeavesNoStaleLookupOrCount()
        {
            StepFourUnitFixture fixture =
                _factory.CreateActiveUnit(1, UnitFaction.Enemy);
            Assert.That(_registry.Register(fixture.Unit), Is.True);
            fixture.Lifecycle.PrepareForReturn();
            fixture.GameObject.SetActive(false);

            _factory.Respawn(fixture, 2, UnitFaction.Ally);
            Assert.That(_registry.Register(fixture.Unit), Is.True);

            Assert.That(_registry.Count, Is.EqualTo(1));
            Assert.That(_registry.GetFactionCount(UnitFaction.Enemy), Is.Zero);
            Assert.That(_registry.GetFactionCount(UnitFaction.Ally), Is.EqualTo(1));
            Assert.That(_registry.TryGetUnit(new SpawnId(1), out _), Is.False);
            Assert.That(_registry.TryGetUnit(new SpawnId(2), out _), Is.True);
            Assert.That(
                _registry.Remove(new SpawnId(1), fixture.Unit),
                Is.False);
        }

        [Test]
        public void CopySnapshots_AreSortedAndDestinationMutationDoesNotAffectRegistry()
        {
            StepFourUnitFixture high =
                _factory.CreateActiveUnit(30, UnitFaction.Enemy);
            StepFourUnitFixture low =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            StepFourUnitFixture middle =
                _factory.CreateActiveUnit(20, UnitFaction.Ally);
            Assert.That(_registry.Register(high.Unit), Is.True);
            Assert.That(_registry.Register(low.Unit), Is.True);
            Assert.That(_registry.Register(middle.Unit), Is.True);
            List<UnitController> snapshot = new List<UnitController>();

            Assert.That(_registry.CopySnapshot(snapshot), Is.EqualTo(3));
            Assert.That(
                snapshot.ConvertAll(unit => unit.SpawnId.Value),
                Is.EqualTo(new long[] { 10, 20, 30 }));
            snapshot.Clear();
            Assert.That(_registry.Count, Is.EqualTo(3));

            Assert.That(
                _registry.CopyFactionSnapshot(UnitFaction.Enemy, snapshot),
                Is.EqualTo(2));
            Assert.That(
                snapshot.ConvertAll(unit => unit.SpawnId.Value),
                Is.EqualTo(new long[] { 10, 30 }));
        }

        private static HitContext CreateHitContext(
            StepFourUnitFixture target,
            SpawnId sourceSpawnId,
            UnitFaction sourceFaction,
            float damage)
        {
            DamagePayload payload = new DamagePayload(
                sourceSpawnId,
                sourceFaction,
                new AttackSequenceId(1),
                damage,
                default);
            return new HitContext(
                payload,
                target.Damage,
                target.GameObject.transform.position,
                Vector3.up,
                HitType.Direct,
                "StepFourRegistryTest");
        }
    }
}
