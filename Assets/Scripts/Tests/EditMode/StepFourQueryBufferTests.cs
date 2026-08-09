using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepFourQueryBufferTests
    {
        private StepFourTestFactory _factory;

        [SetUp]
        public void CreateFixture()
        {
            _factory = new StepFourTestFactory();
        }

        [TearDown]
        public void DestroyFixture()
        {
            _factory.Dispose();
        }

        [Test]
        public void DamageTargetProxy_ChildHurtboxCachesOwnerAndCurrentIdentity()
        {
            StepFourUnitFixture fixture =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            DamageTargetProxy proxy =
                _factory.AddHurtbox(fixture, "Hurtbox", Vector3.zero);

            Assert.That(proxy.ValidateReferences(out string failureMessage), Is.True, failureMessage);
            Assert.That(proxy.UnitController, Is.SameAs(fixture.Unit));
            Assert.That(proxy.DamageController, Is.SameAs(fixture.Damage));
            Assert.That(proxy.TargetCollider, Is.Not.Null);
            Assert.That(proxy.SpawnId, Is.EqualTo(new SpawnId(10)));

            fixture.Lifecycle.PrepareForReturn();
            fixture.GameObject.SetActive(false);
            _factory.Respawn(fixture, 11, UnitFaction.Enemy);
            Assert.That(proxy.SpawnId, Is.EqualTo(new SpawnId(11)));
        }

        [Test]
        public void DamageTargetProxy_FullInactiveHierarchyConfiguresThroughAwakeOnly()
        {
            GameObject root = _factory.CreateGameObject("InactiveUnitHierarchy");
            root.SetActive(false);
            root.AddComponent<HealthController>();
            root.AddComponent<StatusEffectController>();
            DamageController damageController = root.AddComponent<DamageController>();
            root.AddComponent<UnitLifecycleController>();
            UnitController unitController = root.AddComponent<UnitController>();
            GameObject hurtbox = _factory.CreateGameObject("InactiveHurtbox");
            hurtbox.transform.SetParent(root.transform, false);
            hurtbox.layer = LayerMask.NameToLayer("UnitTarget");
            hurtbox.AddComponent<SphereCollider>().isTrigger = true;
            DamageTargetProxy proxy = hurtbox.AddComponent<DamageTargetProxy>();

            root.SetActive(true);

            Assert.That(proxy.IsConfigured, Is.True);
            Assert.That(proxy.UnitController, Is.SameAs(unitController));
            Assert.That(proxy.DamageController, Is.SameAs(damageController));
            Assert.That(proxy.TargetCollider, Is.Not.Null);
        }

        [Test]
        public void DamageTargetProxy_MissingOwnerReportsClearFailure()
        {
            GameObject hurtbox = _factory.CreateGameObject("OrphanHurtbox");
            hurtbox.AddComponent<SphereCollider>().isTrigger = true;
            DamageTargetProxy proxy = hurtbox.AddComponent<DamageTargetProxy>();

            Assert.That(proxy.ValidateReferences(out string failureMessage), Is.False);
            Assert.That(failureMessage, Does.Contain(nameof(UnitController)));
        }

        [Test]
        public void Query_DeduplicatesHurtboxesAndForcesTriggerCollision()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            DamageTargetProxy first =
                _factory.AddHurtbox(target, "HurtboxA", new Vector3(-0.1f, 0f, 0f));
            _factory.AddHurtbox(target, "HurtboxB", new Vector3(0.1f, 0f, 0f));
            Physics.SyncTransforms();
            bool previousQueriesHitTriggers = Physics.queriesHitTriggers;
            Physics.queriesHitTriggers = false;
            try
            {
                TargetQueryBuffer queryBuffer = new TargetQueryBuffer(8);

                int targetCount = queryBuffer.Query(
                    Vector3.zero,
                    2f,
                    new SpawnId(1),
                    UnitFaction.Player);

                Assert.That(targetCount, Is.EqualTo(1));
                Assert.That(queryBuffer.ColliderCount, Is.EqualTo(2));
                Assert.That(queryBuffer.GetTarget(0).SpawnId, Is.EqualTo(new SpawnId(10)));
                Assert.That(
                    queryBuffer.GetTarget(0) == first ||
                    queryBuffer.GetTarget(0).UnitController == first.UnitController,
                    Is.True);
                Assert.That(
                    queryBuffer.UnitTargetLayerMask,
                    Is.EqualTo(1 << LayerMask.NameToLayer("UnitTarget")));
            }
            finally
            {
                Physics.queriesHitTriggers = previousQueriesHitTriggers;
            }
        }

        [Test]
        public void Query_FiltersWrongLayerSelfFriendlyInactiveDeadAndPooledTargets()
        {
            StepFourUnitFixture valid = CreateTarget(10, UnitFaction.Enemy, Vector3.zero);
            StepFourUnitFixture wrongLayer =
                CreateTarget(11, UnitFaction.Enemy, new Vector3(0.5f, 0f, 0f));
            wrongLayer.GameObject.transform.GetChild(0).gameObject.layer =
                LayerMask.NameToLayer("Default");
            CreateTarget(12, UnitFaction.Player, new Vector3(1f, 0f, 0f));
            CreateTarget(1, UnitFaction.Enemy, new Vector3(1.5f, 0f, 0f));

            StepFourUnitFixture inactive =
                _factory.CreatePreparedUnit(13, UnitFaction.Enemy);
            inactive.GameObject.transform.position = new Vector3(2f, 0f, 0f);
            _factory.AddHurtbox(inactive, "InactiveHurtbox", Vector3.zero);
            inactive.GameObject.SetActive(true);
            Assert.That(inactive.Lifecycle.CompleteSpawn(), Is.True);
            Assert.That(inactive.Unit.IsActive, Is.False);

            StepFourUnitFixture dead =
                CreateTarget(14, UnitFaction.Enemy, new Vector3(2.5f, 0f, 0f));
            dead.Damage.ApplyDamage(CreateDirectHit(dead, 100f));

            StepFourUnitFixture pooled =
                CreateTarget(15, UnitFaction.Enemy, new Vector3(3f, 0f, 0f));
            pooled.Lifecycle.PrepareForReturn();
            Physics.SyncTransforms();
            TargetQueryBuffer queryBuffer = new TargetQueryBuffer(16);

            int count = queryBuffer.Query(
                Vector3.zero,
                5f,
                new SpawnId(1),
                UnitFaction.Player);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(queryBuffer.GetTarget(0).UnitController, Is.SameAs(valid.Unit));
        }

        [Test]
        public void Query_SaturationAndReuseExposeNoStaleTargets()
        {
            StepFourUnitFixture first =
                CreateTarget(10, UnitFaction.Enemy, Vector3.zero);
            StepFourUnitFixture second =
                CreateTarget(11, UnitFaction.Enemy, new Vector3(0.5f, 0f, 0f));
            Physics.SyncTransforms();
            AreaQueryBuffer queryBuffer = new AreaQueryBuffer(1);

            int firstCount = queryBuffer.Query(
                Vector3.zero,
                2f,
                new SpawnId(1),
                UnitFaction.Player);

            Assert.That(firstCount, Is.EqualTo(1));
            Assert.That(queryBuffer.ColliderCount, Is.EqualTo(1));
            Assert.That(queryBuffer.WasSaturated, Is.True);
            Assert.That(queryBuffer.Capacity, Is.EqualTo(1));

            first.GameObject.transform.position = new Vector3(100f, 0f, 0f);
            second.GameObject.transform.position = new Vector3(100f, 0f, 0f);
            Physics.SyncTransforms();
            int emptyCount = queryBuffer.Query(
                Vector3.zero,
                2f,
                new SpawnId(1),
                UnitFaction.Player);

            Assert.That(emptyCount, Is.Zero);
            Assert.That(queryBuffer.ColliderCount, Is.Zero);
            Assert.That(queryBuffer.UniqueTargetCount, Is.Zero);
            Assert.That(queryBuffer.WasSaturated, Is.False);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => queryBuffer.GetTarget(0));
        }

        [Test]
        public void SeparateTargetAndAreaBuffersDoNotShareResults()
        {
            StepFourUnitFixture target =
                CreateTarget(10, UnitFaction.Enemy, Vector3.zero);
            Physics.SyncTransforms();
            TargetQueryBuffer targetBuffer = new TargetQueryBuffer(4);
            AreaQueryBuffer areaBuffer = new AreaQueryBuffer(4);

            Assert.That(
                targetBuffer.Query(Vector3.zero, 2f, new SpawnId(1), UnitFaction.Player),
                Is.EqualTo(1));
            target.GameObject.transform.position = new Vector3(100f, 0f, 0f);
            Physics.SyncTransforms();
            Assert.That(
                areaBuffer.Query(Vector3.zero, 2f, new SpawnId(1), UnitFaction.Player),
                Is.Zero);

            Assert.That(targetBuffer.UniqueTargetCount, Is.EqualTo(1));
            Assert.That(targetBuffer.GetTarget(0).SpawnId, Is.EqualTo(new SpawnId(10)));
            Assert.That(areaBuffer.UniqueTargetCount, Is.Zero);
        }

        [Test]
        public void NearestTargetRules_CloserWinsAndEqualDistanceUsesLowestSpawnId()
        {
            Assert.That(
                NearestTargetRules.IsCandidatePreferred(
                    4f,
                    new SpawnId(20),
                    9f,
                    new SpawnId(10)),
                Is.True);
            Assert.That(
                NearestTargetRules.IsCandidatePreferred(
                    9f,
                    new SpawnId(10),
                    9f,
                    new SpawnId(20)),
                Is.True);
            Assert.That(
                NearestTargetRules.IsCandidatePreferred(
                    9f,
                    new SpawnId(20),
                    9f,
                    new SpawnId(10)),
                Is.False);
            Assert.That(
                NearestTargetRules.IsCandidatePreferred(
                    100f,
                    new SpawnId(20),
                    1f,
                    default),
                Is.True);
        }

        [Test]
        public void QueryAndNearestRules_RejectInvalidArguments()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new TargetQueryBuffer(0));
            TargetQueryBuffer queryBuffer = new TargetQueryBuffer(1);
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => queryBuffer.Query(
                    Vector3.zero,
                    float.NaN,
                    new SpawnId(1),
                    UnitFaction.Player));
            Assert.Throws<System.ArgumentException>(
                () => queryBuffer.Query(
                    Vector3.zero,
                    1f,
                    default,
                    UnitFaction.Player));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => NearestTargetRules.IsCandidatePreferred(
                    -1f,
                    new SpawnId(1),
                    1f,
                    new SpawnId(2)));
        }

        private StepFourUnitFixture CreateTarget(
            long spawnId,
            UnitFaction faction,
            Vector3 position)
        {
            StepFourUnitFixture fixture =
                _factory.CreateActiveUnit(spawnId, faction);
            fixture.GameObject.transform.position = position;
            _factory.AddHurtbox(fixture, $"Hurtbox_{spawnId}", Vector3.zero);
            return fixture;
        }

        private static HitContext CreateDirectHit(
            StepFourUnitFixture target,
            float damage)
        {
            DamagePayload payload = new DamagePayload(
                new SpawnId(99),
                UnitFaction.Player,
                new AttackSequenceId(1),
                damage,
                default);
            return new HitContext(
                payload,
                target.Damage,
                target.GameObject.transform.position,
                Vector3.up,
                HitType.Direct,
                "StepFourQueryTest");
        }
    }
}
