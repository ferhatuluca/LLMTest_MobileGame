using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.Projectiles;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepEightAttackDeliveryTests
    {
        private StepEightTestEnvironment _environment;

        [SetUp]
        public void SetUp()
        {
            _environment = new StepEightTestEnvironment();
        }

        [TearDown]
        public void TearDown()
        {
            _environment.Dispose();
        }

        [Test]
        public void Melee_ResolvesOneInteractionAtImpactPoint()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right);
            MeleeAttackExecutor executor = source.GameObject
                .AddComponent<MeleeAttackExecutor>();
            Assert.That(executor.Configure(_environment.InteractionSystem), Is.True);
            AttackExecutionContext context = _environment.CreateExecutionContext(
                source,
                target,
                _environment.MeleeAttackDefinition,
                1);

            InteractionResult firstResult = executor.ExecuteImpact(context);
            InteractionResult duplicateResult = executor.ExecuteImpact(context);

            Assert.That(firstResult.IsApplied, Is.True);
            Assert.That(duplicateResult.Outcome,
                Is.EqualTo(InteractionOutcome.AlreadyHit));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
        }

        [TestCase("Bullet")]
        [TestCase("Fireball")]
        public void KinematicProjectile_HostileHitReturnsToPool(string poolName)
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                new Vector3(2f, 0f, 0f));
            ProjectileDefinition definition = _environment.GetProjectileDefinition(
                poolName);
            ProjectileController projectile = _environment.SpawnProjectile(
                definition,
                source,
                Vector3.zero,
                Vector3.right,
                1);
            Physics.SyncTransforms();

            projectile.AdvanceTime(0.25f);

            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
            Assert.That(_environment.GetPoolDiagnostics(definition.PoolId).ActiveCount,
                Is.Zero);
        }

        [TestCase("Bullet")]
        [TestCase("Fireball")]
        public void KinematicProjectile_ExpiryReturnsWithoutDamage(string poolName)
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            ProjectileDefinition definition = _environment.GetProjectileDefinition(
                poolName);
            ProjectileController projectile = _environment.SpawnProjectile(
                definition,
                source,
                new Vector3(0f, 0f, 20f),
                Vector3.right,
                2);
            ProjectileTerminationReason terminationReason =
                ProjectileTerminationReason.None;
            projectile.Terminated += terminationEvent =>
                terminationReason = terminationEvent.Reason;

            projectile.AdvanceTime(definition.MaximumLifetime);

            Assert.That(terminationReason,
                Is.EqualTo(ProjectileTerminationReason.LifetimeExpired));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
        }

        [TestCase("Bullet")]
        [TestCase("Fireball")]
        public void WorldObstruction_BlocksKinematicProjectile(string poolName)
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                new Vector3(3f, 0f, 0f));
            _environment.CreateWorldBlock(new Vector3(1.5f, 0f, 0f));
            ProjectileDefinition definition = _environment.GetProjectileDefinition(
                poolName);
            ProjectileController projectile = _environment.SpawnProjectile(
                definition,
                source,
                Vector3.zero,
                Vector3.right,
                3);
            Physics.SyncTransforms();

            projectile.AdvanceTime(0.5f);

            Assert.That(target.Health.CurrentHealth,
                Is.EqualTo(target.Health.MaximumHealth));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
        }

        [TestCase("Bullet")]
        [TestCase("Fireball")]
        public void SourceAndFriendlyContacts_DoNotConsumeKinematicProjectile(
            string poolName)
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture friendly = _environment.CreateUnit(
                2,
                UnitFaction.Player,
                new Vector3(0.8f, 0f, 0f));
            StepSevenUnitFixture hostile = _environment.CreateUnit(
                3,
                UnitFaction.Enemy,
                new Vector3(2f, 0f, 0f));
            ProjectileController projectile = _environment.SpawnProjectile(
                _environment.GetProjectileDefinition(poolName),
                source,
                Vector3.zero,
                Vector3.right,
                4);
            Physics.SyncTransforms();

            projectile.AdvanceTime(0.25f);

            Assert.That(friendly.Health.CurrentHealth,
                Is.EqualTo(friendly.Health.MaximumHealth));
            Assert.That(hostile.Health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Projectile_UsesCapturedPayloadAfterSourceDies()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                new Vector3(2f, 0f, 0f));
            ProjectileController projectile = _environment.SpawnProjectile(
                _environment.BulletDefinition,
                source,
                Vector3.zero,
                Vector3.right,
                5,
                17f);
            source.Health.ApplyDamage(source.Health.MaximumHealth);
            Physics.SyncTransforms();

            projectile.AdvanceTime(0.25f);

            Assert.That(target.Health.CurrentHealth, Is.EqualTo(83f));
        }

        [Test]
        public void Projectile_DoesNotReadRecycledSourceIdentityOrFaction()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                new Vector3(2f, 0f, 0f));
            ProjectileController projectile = _environment.SpawnProjectile(
                _environment.BulletDefinition,
                source,
                Vector3.zero,
                Vector3.right,
                6);
            DamagePayload capturedPayload = projectile.DamagePayload;
            StepSevenTestFactory.SetAutoProperty(
                source.Unit,
                nameof(UnitController.SpawnId),
                new SpawnId(999));
            StepSevenTestFactory.SetAutoProperty(
                source.Unit,
                nameof(UnitController.Faction),
                UnitFaction.Enemy);
            source.GameObject.transform.position = Vector3.left * 2f;
            Physics.SyncTransforms();

            projectile.AdvanceTime(0.25f);

            Assert.That(capturedPayload.SourceSpawnId, Is.EqualTo(new SpawnId(1)));
            Assert.That(capturedPayload.SourceFaction, Is.EqualTo(UnitFaction.Player));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void Grenade_FuseExplosionDeduplicatesMultipleHurtboxes()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                new Vector3(-2f, 0f, 0f));
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right);
            _environment.AddHurtbox(target, "SecondHurtbox", Vector3.up * 0.1f);
            ProjectileController projectile = _environment.SpawnProjectile(
                _environment.GrenadeDefinition,
                source,
                Vector3.zero,
                Vector3.forward,
                7);
            GrenadeProjectileMovement movement =
                projectile.GetComponent<GrenadeProjectileMovement>();
            Physics.SyncTransforms();

            projectile.AdvanceTime(_environment.GrenadeDefinition.FuseDuration);

            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(movement.LastExplosionTargetCount, Is.EqualTo(1));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Grenade_SourceAndFriendlyTriggersDoNotDetonateButHostileDoes()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture friendly = _environment.CreateUnit(
                2,
                UnitFaction.Player,
                Vector3.right);
            StepSevenUnitFixture hostile = _environment.CreateUnit(
                3,
                UnitFaction.Enemy,
                Vector3.right * 2f);
            ProjectileController projectile = _environment.SpawnProjectile(
                _environment.GrenadeDefinition,
                source,
                new Vector3(0f, 0f, 20f),
                Vector3.forward,
                8);
            GrenadeProjectileMovement movement =
                projectile.GetComponent<GrenadeProjectileMovement>();

            movement.HandleTriggerContact(_environment.GetHurtbox(source).TargetCollider);
            Assert.That(projectile.IsRunning, Is.True);
            movement.HandleTriggerContact(_environment.GetHurtbox(friendly).TargetCollider);
            Assert.That(projectile.IsRunning, Is.True);
            movement.HandleTriggerContact(_environment.GetHurtbox(hostile).TargetCollider);

            Assert.That(projectile.gameObject.activeSelf, Is.False);
            Assert.That(friendly.Health.CurrentHealth,
                Is.EqualTo(friendly.Health.MaximumHealth));
        }

        [Test]
        public void Grenade_ReturnResetsLinearAndAngularVelocity()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            ProjectileController projectile = _environment.SpawnProjectile(
                _environment.GrenadeDefinition,
                source,
                new Vector3(0f, 0f, 20f),
                Vector3.forward,
                9);
            Rigidbody rigidbody = projectile.GetComponent<Rigidbody>();
            rigidbody.angularVelocity = Vector3.one * 3f;
            Assert.That(rigidbody.linearVelocity.sqrMagnitude, Is.GreaterThan(0f));

            PoolReturnResult returnResult =
                _environment.SpawnManager.ReturnProjectile(
                    projectile.GetComponent<PooledEntity>());

            Assert.That(returnResult.IsSuccess, Is.True);
            Assert.That(rigidbody.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(rigidbody.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Grenade_WorldCollisionDetonatesAndReturns()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            ProjectileController projectile = _environment.SpawnProjectile(
                _environment.GrenadeDefinition,
                source,
                new Vector3(0f, 0f, 20f),
                Vector3.forward,
                16);
            GameObject worldBlock = _environment.CreateWorldBlock(
                new Vector3(0f, 0f, 21f));
            GrenadeProjectileMovement movement =
                projectile.GetComponent<GrenadeProjectileMovement>();

            movement.HandleCollisionContact(
                worldBlock.GetComponent<Collider>(),
                worldBlock.transform.position);

            Assert.That(projectile.gameObject.activeSelf, Is.False);
            Assert.That(_environment.GetPoolDiagnostics(
                StepEightTestEnvironment.GrenadePoolId).ActiveCount,
                Is.Zero);
        }

        [Test]
        public void Hitscan_IgnoresFriendlyAndAppliesOnlyFirstHostile()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture friendly = _environment.CreateUnit(
                2,
                UnitFaction.Player,
                Vector3.right);
            StepSevenUnitFixture firstHostile = _environment.CreateUnit(
                3,
                UnitFaction.Enemy,
                Vector3.right * 2f);
            StepSevenUnitFixture secondHostile = _environment.CreateUnit(
                4,
                UnitFaction.Enemy,
                Vector3.right * 3f);
            HitscanAttackExecutor executor = _environment.CreateHitscanExecutor(
                source.GameObject,
                source.GameObject.transform);
            AttackExecutionContext context = _environment.CreateExecutionContext(
                source,
                firstHostile,
                _environment.HitscanAttackDefinition,
                10);
            Physics.SyncTransforms();

            InteractionResult result = executor.ExecuteImpact(context);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(friendly.Health.CurrentHealth,
                Is.EqualTo(friendly.Health.MaximumHealth));
            Assert.That(firstHostile.Health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(secondHostile.Health.CurrentHealth,
                Is.EqualTo(secondHostile.Health.MaximumHealth));
            Assert.That(_environment.GetPoolDiagnostics(
                StepEightTestEnvironment.LaserBeamPoolId).ActiveCount,
                Is.EqualTo(1));
        }

        [Test]
        public void Hitscan_WorldObstructionBlocksHostile()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right * 3f);
            _environment.CreateWorldBlock(Vector3.right * 1.5f);
            HitscanAttackExecutor executor = _environment.CreateHitscanExecutor(
                source.GameObject,
                source.GameObject.transform);
            AttackExecutionContext context = _environment.CreateExecutionContext(
                source,
                target,
                _environment.HitscanAttackDefinition,
                11);
            Physics.SyncTransforms();

            InteractionResult result = executor.ExecuteImpact(context);

            Assert.That(result.IsApplied, Is.False);
            Assert.That(target.Health.CurrentHealth,
                Is.EqualTo(target.Health.MaximumHealth));
        }

        [Test]
        public void FriendlyFire_IsRejectedForMeleeGrenadeAndHitscan()
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture friendly = _environment.CreateUnit(
                2,
                UnitFaction.Player,
                Vector3.right);
            MeleeAttackExecutor melee = source.GameObject
                .AddComponent<MeleeAttackExecutor>();
            Assert.That(melee.Configure(_environment.InteractionSystem), Is.True);
            InteractionResult meleeResult = melee.ExecuteImpact(
                _environment.CreateExecutionContext(
                    source,
                    friendly,
                    _environment.MeleeAttackDefinition,
                    12));

            ProjectileController grenade = _environment.SpawnProjectile(
                _environment.GrenadeDefinition,
                source,
                friendly.GameObject.transform.position,
                Vector3.forward,
                13);
            grenade.AdvanceTime(_environment.GrenadeDefinition.FuseDuration);

            HitscanAttackExecutor hitscan = _environment.CreateHitscanExecutor(
                source.GameObject,
                source.GameObject.transform);
            InteractionResult hitscanResult = hitscan.ExecuteImpact(
                _environment.CreateExecutionContext(
                    source,
                    friendly,
                    _environment.HitscanAttackDefinition,
                    14));

            Assert.That(meleeResult.Outcome,
                Is.EqualTo(InteractionOutcome.InvalidFaction));
            Assert.That(hitscanResult.IsApplied, Is.False);
            Assert.That(friendly.Health.CurrentHealth,
                Is.EqualTo(friendly.Health.MaximumHealth));
        }

        [TestCase(AttackDeliveryType.Projectile)]
        [TestCase(AttackDeliveryType.Grenade)]
        public void SpawningExecutor_CapturesPayloadAtFireTime(
            AttackDeliveryType deliveryType)
        {
            StepSevenUnitFixture source = _environment.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);
            StepSevenUnitFixture target = _environment.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right * 2f);
            AttackDefinition attackDefinition = deliveryType ==
                AttackDeliveryType.Projectile
                ? _environment.ProjectileAttackDefinition
                : _environment.GrenadeAttackDefinition;
            IAttackExecutor executor = deliveryType == AttackDeliveryType.Projectile
                ? _environment.CreateProjectileExecutor(source.GameObject)
                : _environment.CreateGrenadeExecutor(source.GameObject);
            AttackExecutionContext context = _environment.CreateExecutionContext(
                source,
                target,
                attackDefinition,
                15);

            InteractionResult immediateResult = executor.ExecuteImpact(context);
            ProjectileController runningProjectile =
                _environment.FindRunningProjectile(deliveryType);

            Assert.That(immediateResult.Outcome,
                Is.EqualTo(InteractionOutcome.None));
            Assert.That(runningProjectile, Is.Not.Null);
            Assert.That(runningProjectile.DamagePayload.SourceSpawnId,
                Is.EqualTo(source.Unit.SpawnId));
            Assert.That(runningProjectile.DamagePayload.SourceFaction,
                Is.EqualTo(source.Unit.Faction));
            Assert.That(runningProjectile.DamagePayload.BaseDamage,
                Is.EqualTo(10f));
        }

        [Test]
        public void LaserBeam_ExpiresAndReturnsToPool()
        {
            PoolRentResult<PooledEntity> rentResult =
                _environment.PoolManager.Rent(
                    StepEightTestEnvironment.LaserBeamPoolId);
            Assert.That(rentResult.IsSuccess, Is.True);
            LaserBeamPresentationController beam = rentResult.Entity
                .GetComponent<LaserBeamPresentationController>();
            Assert.That(
                beam.ConfigurePresentation(
                    Vector3.zero,
                    Vector3.right * 2f,
                    _environment.PoolManager),
                Is.True);
            Assert.That(rentResult.Entity.PrepareForSpawn(), Is.True);
            rentResult.Entity.gameObject.SetActive(true);
            Assert.That(rentResult.Entity.CompleteSpawn(), Is.True);

            beam.AdvanceTime(0.12f);

            Assert.That(beam.gameObject.activeSelf, Is.False);
            Assert.That(_environment.GetPoolDiagnostics(
                StepEightTestEnvironment.LaserBeamPoolId).ActiveCount,
                Is.Zero);
        }

        [Test]
        public void DeliveryMasks_ContainOnlyWorldAndUnitTarget()
        {
            int expectedMask =
                (1 << LayerMask.NameToLayer("World")) |
                (1 << LayerMask.NameToLayer("UnitTarget"));

            Assert.That(DeliveryCollisionRules.CreateDeliveryLayerMask(),
                Is.EqualTo(expectedMask));
        }
    }

    public sealed class StepEightAssetTests
    {
        [Test]
        public void ConcreteAssets_MatchAuthoredTuningAndPoolBaselines()
        {
            ProjectileDefinition bullet = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(
                "Assets/Data/Projectiles/PD_Bullet.asset");
            ProjectileDefinition fireball = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(
                "Assets/Data/Projectiles/PD_Fireball.asset");
            ProjectileDefinition grenade = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(
                "Assets/Data/Projectiles/PD_Grenade.asset");
            PoolCatalog catalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");

            Assert.That(bullet, Is.Not.Null);
            Assert.That(bullet.Speed, Is.EqualTo(20f));
            Assert.That(bullet.MaximumLifetime, Is.EqualTo(2f));
            Assert.That(bullet.CollisionRadius, Is.EqualTo(0.10f));
            Assert.That(fireball.Speed, Is.EqualTo(12f));
            Assert.That(fireball.MaximumLifetime, Is.EqualTo(3f));
            Assert.That(fireball.CollisionRadius, Is.EqualTo(0.25f));
            Assert.That(grenade.Speed, Is.EqualTo(12f));
            Assert.That(grenade.FuseDuration, Is.EqualTo(2f));
            Assert.That(grenade.GravityScale, Is.EqualTo(1f));
            Assert.That(grenade.ExplosionRadius, Is.EqualTo(3f));
            Assert.That(catalog.Validate().IsValid, Is.True);
            AssertPoolEntry(catalog, "Bullet", 50, 200);
            AssertPoolEntry(catalog, "Fireball", 30, 100);
            AssertPoolEntry(catalog, "Grenade", 20, 60);
            AssertPoolEntry(catalog, "LaserBeam", 20, 60);
        }

        [Test]
        public void ConcretePrefabs_HaveRequiredPooledDeliveryComponents()
        {
            GameObject bullet = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Projectiles/PF_Projectile_Bullet.prefab");
            GameObject fireball = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Projectiles/PF_Projectile_Fireball.prefab");
            GameObject grenade = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Projectiles/PF_Projectile_Grenade.prefab");
            GameObject laser = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Effects/PF_Effect_LaserBeam.prefab");

            AssertKinematicPrefab(bullet);
            AssertKinematicPrefab(fireball);
            Assert.That(grenade.GetComponent<PooledEntity>(), Is.Not.Null);
            Assert.That(grenade.GetComponent<ProjectileController>(), Is.Not.Null);
            Assert.That(grenade.GetComponent<GrenadeProjectileMovement>(), Is.Not.Null);
            Assert.That(grenade.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(laser.GetComponent<PooledEntity>(), Is.Not.Null);
            LaserBeamPresentationController beam =
                laser.GetComponent<LaserBeamPresentationController>();
            Assert.That(beam, Is.Not.Null);
            Assert.That(beam.Lifetime, Is.EqualTo(0.12f));
        }

        [Test]
        public void AttackAssets_MatchTemporarySandboxTable()
        {
            AssertAttack("AD_BasicMelee", 10f, 1.8f, 1f, 0.25f, 0.25f,
                AttackDeliveryType.Melee);
            AssertAttack("AD_BasicBullet", 8f, 8f, 1.2f, 0.20f, 0.20f,
                AttackDeliveryType.Projectile);
            AssertAttack("AD_DragonFireball", 14f, 10f, 1.6f, 0.40f, 0.30f,
                AttackDeliveryType.Projectile);
            AssertAttack("AD_PlayerGrenadeGun", 25f, 9f, 1.8f, 0.25f, 0.30f,
                AttackDeliveryType.Grenade);
            AssertAttack("AD_PlayerSpaceGun", 18f, 12f, 1f, 0.10f, 0.15f,
                AttackDeliveryType.Hitscan);
        }

        private static void AssertKinematicPrefab(GameObject prefab)
        {
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<PooledEntity>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ProjectileController>(), Is.Not.Null);
            KinematicProjectileMovement movement =
                prefab.GetComponent<KinematicProjectileMovement>();
            Assert.That(movement, Is.Not.Null);
            Assert.That(movement.SweepCapacity, Is.EqualTo(32));
        }

        private static void AssertPoolEntry(
            PoolCatalog catalog,
            string poolId,
            int prewarm,
            int retained)
        {
            Assert.That(catalog.TryGetEntry(
                new PoolId(poolId),
                out PoolCatalogEntry entry), Is.True);
            Assert.That(entry.InitialPrewarmCount, Is.EqualTo(prewarm));
            Assert.That(entry.MaximumInactiveRetainedCount, Is.EqualTo(retained));
            Assert.That(entry.CapacityPolicy,
                Is.EqualTo(PoolCapacityPolicy.Expandable));
        }

        private static void AssertAttack(
            string assetName,
            float damage,
            float range,
            float cooldown,
            float windup,
            float recovery,
            AttackDeliveryType deliveryType)
        {
            AttackDefinition attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
                $"Assets/Data/Attacks/{assetName}.asset");
            Assert.That(attack, Is.Not.Null);
            Assert.That(attack.Validate().IsValid, Is.True);
            Assert.That(attack.Damage, Is.EqualTo(damage));
            Assert.That(attack.AttackRange, Is.EqualTo(range));
            Assert.That(attack.CooldownDuration, Is.EqualTo(cooldown));
            Assert.That(attack.WindupDuration, Is.EqualTo(windup));
            Assert.That(attack.RecoveryDuration, Is.EqualTo(recovery));
            Assert.That(attack.DeliveryType, Is.EqualTo(deliveryType));
        }
    }

    internal sealed class StepEightTestEnvironment : IDisposable
    {
        public static readonly PoolId BulletPoolId = new PoolId("StepEightBullet");
        public static readonly PoolId FireballPoolId = new PoolId("StepEightFireball");
        public static readonly PoolId GrenadePoolId = new PoolId("StepEightGrenade");
        public static readonly PoolId LaserBeamPoolId = new PoolId("StepEightLaser");

        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();
        private readonly StepSevenTestFactory _unitFactory =
            new StepSevenTestFactory();

        public InteractionSystem InteractionSystem { get; }
        public PoolManager PoolManager { get; }
        public SpawnManager SpawnManager { get; }
        public ProjectileDefinition BulletDefinition { get; }
        public ProjectileDefinition FireballDefinition { get; }
        public ProjectileDefinition GrenadeDefinition { get; }
        public AttackDefinition MeleeAttackDefinition { get; }
        public AttackDefinition ProjectileAttackDefinition { get; }
        public AttackDefinition GrenadeAttackDefinition { get; }
        public AttackDefinition HitscanAttackDefinition { get; }

        public StepEightTestEnvironment()
        {
            InteractionSystem = CreateGameObject("StepEightInteraction")
                .AddComponent<InteractionSystem>();
            UnitRegistry unitRegistry = CreateGameObject("StepEightRegistry")
                .AddComponent<UnitRegistry>();
            BulletDefinition = CreateProjectileDefinition(
                BulletPoolId,
                AttackDeliveryType.Projectile,
                10f,
                1f,
                0.1f,
                0f,
                0f,
                0f);
            FireballDefinition = CreateProjectileDefinition(
                FireballPoolId,
                AttackDeliveryType.Projectile,
                10f,
                1f,
                0.2f,
                0f,
                0f,
                0f);
            GrenadeDefinition = CreateProjectileDefinition(
                GrenadePoolId,
                AttackDeliveryType.Grenade,
                10f,
                0f,
                0.2f,
                1f,
                3f,
                1f);
            MeleeAttackDefinition = CreateAttackDefinition(
                "StepEightMelee",
                AttackDeliveryType.Melee,
                null);
            ProjectileAttackDefinition = CreateAttackDefinition(
                "StepEightProjectile",
                AttackDeliveryType.Projectile,
                BulletDefinition);
            GrenadeAttackDefinition = CreateAttackDefinition(
                "StepEightGrenade",
                AttackDeliveryType.Grenade,
                GrenadeDefinition);
            HitscanAttackDefinition = CreateAttackDefinition(
                "StepEightHitscan",
                AttackDeliveryType.Hitscan,
                null);

            PoolCatalogEntry[] entries =
            {
                CreatePoolEntry(BulletPoolId, CreateKinematicPrefab("BulletPrefab")),
                CreatePoolEntry(FireballPoolId, CreateKinematicPrefab("FireballPrefab")),
                CreatePoolEntry(GrenadePoolId, CreateGrenadePrefab()),
                CreatePoolEntry(LaserBeamPoolId, CreateBeamPrefab())
            };
            PoolCatalog catalog = CreateScriptableObject<PoolCatalog>();
            SetField(catalog, "_entries", entries);
            PoolManager = CreateGameObject("StepEightPoolManager")
                .AddComponent<PoolManager>();
            Assert.That(PoolManager.Initialize(catalog, out string poolFailure),
                Is.True,
                poolFailure);
            SpawnManager = CreateGameObject("StepEightSpawnManager")
                .AddComponent<SpawnManager>();
            Assert.That(
                SpawnManager.Initialize(
                    PoolManager,
                    unitRegistry,
                    out string spawnFailure),
                Is.True,
                spawnFailure);
        }

        public StepSevenUnitFixture CreateUnit(
            long spawnId,
            UnitFaction faction,
            Vector3 position)
        {
            return _unitFactory.CreateUnit(spawnId, faction, position);
        }

        public DamageTargetProxy AddHurtbox(
            StepSevenUnitFixture fixture,
            string objectName,
            Vector3 localPosition)
        {
            return _unitFactory.AddHurtbox(fixture, objectName, localPosition);
        }

        public DamageTargetProxy GetHurtbox(StepSevenUnitFixture fixture)
        {
            return fixture.GameObject.GetComponentInChildren<DamageTargetProxy>();
        }

        public ProjectileDefinition GetProjectileDefinition(string poolName)
        {
            return poolName == "Bullet" ? BulletDefinition : FireballDefinition;
        }

        public ProjectileController SpawnProjectile(
            ProjectileDefinition definition,
            StepSevenUnitFixture source,
            Vector3 position,
            Vector3 direction,
            long sequenceId,
            float damage = 10f)
        {
            DamagePayload payload = new DamagePayload(
                source.Unit.SpawnId,
                source.Unit.Faction,
                new AttackSequenceId(sequenceId),
                damage,
                new DamageCategoryId("StepEight"));
            SpawnResult<PooledEntity> result = SpawnManager.SpawnProjectile(
                new ProjectileSpawnRequest(
                    definition,
                    payload,
                    position,
                    Quaternion.LookRotation(direction, Vector3.up)),
                InteractionSystem);
            Assert.That(result.IsSuccess, Is.True, result.FailureReason.ToString());
            return result.Entity.GetComponent<ProjectileController>();
        }

        public AttackExecutionContext CreateExecutionContext(
            StepSevenUnitFixture source,
            StepSevenUnitFixture target,
            AttackDefinition attackDefinition,
            long sequenceId)
        {
            AttackKey attackKey = new AttackKey(
                source.Unit.SpawnId,
                new AttackSequenceId(sequenceId));
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(attackKey);
            return new AttackExecutionContext(
                source.Unit,
                target.Unit,
                attackDefinition,
                attackKey,
                ledger);
        }

        public GameObject CreateWorldBlock(Vector3 position)
        {
            GameObject worldBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldBlock.name = "WorldBlock";
            worldBlock.layer = LayerMask.NameToLayer("World");
            worldBlock.transform.position = position;
            worldBlock.transform.localScale = new Vector3(0.2f, 2f, 2f);
            _createdObjects.Add(worldBlock);
            return worldBlock;
        }

        public HitscanAttackExecutor CreateHitscanExecutor(
            GameObject owner,
            Transform attackOrigin)
        {
            HitscanAttackExecutor executor =
                owner.AddComponent<HitscanAttackExecutor>();
            Assert.That(
                executor.Configure(
                    InteractionSystem,
                    PoolManager,
                    attackOrigin,
                    LaserBeamPoolId,
                    32),
                Is.True);
            return executor;
        }

        public ProjectileAttackExecutor CreateProjectileExecutor(GameObject owner)
        {
            ProjectileAttackExecutor executor =
                owner.AddComponent<ProjectileAttackExecutor>();
            Assert.That(
                executor.Configure(
                    SpawnManager,
                    InteractionSystem,
                    owner.transform),
                Is.True);
            return executor;
        }

        public GrenadeAttackExecutor CreateGrenadeExecutor(GameObject owner)
        {
            GrenadeAttackExecutor executor =
                owner.AddComponent<GrenadeAttackExecutor>();
            Assert.That(
                executor.Configure(
                    SpawnManager,
                    InteractionSystem,
                    owner.transform),
                Is.True);
            return executor;
        }

        public ProjectileController FindRunningProjectile(
            AttackDeliveryType deliveryType)
        {
            ProjectileController[] projectiles =
                UnityEngine.Object.FindObjectsByType<ProjectileController>(
                    FindObjectsInactive.Include);
            foreach (ProjectileController projectile in projectiles)
            {
                if (projectile.IsRunning && projectile.Definition != null &&
                    projectile.Definition.CompatibleDeliveryType == deliveryType)
                {
                    return projectile;
                }
            }

            return null;
        }

        public PoolDiagnostics GetPoolDiagnostics(PoolId poolId)
        {
            Assert.That(PoolManager.TryGetDiagnostics(poolId, out PoolDiagnostics value),
                Is.True);
            return value;
        }

        public void Dispose()
        {
            _unitFactory.Dispose();
            for (int objectIndex = _createdObjects.Count - 1;
                 objectIndex >= 0;
                 objectIndex--)
            {
                UnityEngine.Object createdObject = _createdObjects[objectIndex];
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }
        }

        private GameObject CreateKinematicPrefab(string objectName)
        {
            GameObject prefab = CreateGameObject(objectName);
            prefab.SetActive(false);
            prefab.layer = LayerMask.NameToLayer("Projectile");
            prefab.AddComponent<SphereCollider>();
            prefab.AddComponent<PooledEntity>();
            KinematicProjectileMovement movement =
                prefab.AddComponent<KinematicProjectileMovement>();
            Assert.That(movement.InitializeSweepCapacity(32), Is.True);
            prefab.AddComponent<ProjectileController>();
            return prefab;
        }

        private GameObject CreateGrenadePrefab()
        {
            GameObject prefab = CreateGameObject("GrenadePrefab");
            prefab.SetActive(false);
            prefab.layer = LayerMask.NameToLayer("Projectile");
            prefab.AddComponent<SphereCollider>();
            prefab.AddComponent<Rigidbody>();
            prefab.AddComponent<PooledEntity>();
            GrenadeProjectileMovement movement =
                prefab.AddComponent<GrenadeProjectileMovement>();
            Assert.That(movement.InitializeAreaCapacity(64), Is.True);
            prefab.AddComponent<ProjectileController>();
            return prefab;
        }

        private GameObject CreateBeamPrefab()
        {
            GameObject prefab = CreateGameObject("BeamPrefab");
            prefab.SetActive(false);
            prefab.AddComponent<PooledEntity>();
            GameObject visual = CreateGameObject("BeamVisual");
            visual.transform.SetParent(prefab.transform, false);
            LaserBeamPresentationController beam =
                prefab.AddComponent<LaserBeamPresentationController>();
            Assert.That(beam.ConfigureAsset(visual.transform, 0.12f), Is.True);
            return prefab;
        }

        private ProjectileDefinition CreateProjectileDefinition(
            PoolId poolId,
            AttackDeliveryType deliveryType,
            float speed,
            float maximumLifetime,
            float collisionRadius,
            float gravityScale,
            float explosionRadius,
            float fuseDuration)
        {
            ProjectileDefinition definition =
                CreateScriptableObject<ProjectileDefinition>();
            definition.name = poolId.ToString();
            SetAutoProperty(definition, nameof(ProjectileDefinition.PoolId), poolId);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.CompatibleDeliveryType),
                deliveryType);
            SetAutoProperty(definition, nameof(ProjectileDefinition.Speed), speed);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.MaximumLifetime),
                maximumLifetime);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.CollisionRadius),
                collisionRadius);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.GravityScale),
                gravityScale);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.ExplosionRadius),
                explosionRadius);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.FuseDuration),
                fuseDuration);
            return definition;
        }

        private AttackDefinition CreateAttackDefinition(
            string attackName,
            AttackDeliveryType deliveryType,
            ProjectileDefinition projectileDefinition)
        {
            AttackDefinition definition = CreateScriptableObject<AttackDefinition>();
            definition.name = attackName;
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AttackId),
                new AttackId(attackName));
            SetAutoProperty(definition, nameof(AttackDefinition.Damage), 10f);
            SetAutoProperty(definition, nameof(AttackDefinition.AttackRange), 10f);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.CooldownDuration),
                1f);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.WindupDuration),
                0f);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.RecoveryDuration),
                0f);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.DeliveryType),
                deliveryType);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.ProjectileDefinition),
                projectileDefinition);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AcceptedHitEffect),
                new AcceptedHitEffectConfiguration(StatusEffectType.None, 0f));
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.DamageCategoryId),
                new DamageCategoryId("StepEight"));
            return definition;
        }

        private PoolCatalogEntry CreatePoolEntry(
            PoolId poolId,
            GameObject prefab)
        {
            PoolCatalogEntry entry = new PoolCatalogEntry();
            SetAutoProperty(entry, nameof(PoolCatalogEntry.PoolId), poolId);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.Prefab), prefab);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.InitialPrewarmCount), 0);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                8);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.CapacityPolicy),
                PoolCapacityPolicy.Expandable);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.MaximumActiveCount), 0);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.EnableCollectionChecks), true);
            return entry;
        }

        private GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            StepSevenTestFactory.SetAutoProperty(target, propertyName, value);
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
