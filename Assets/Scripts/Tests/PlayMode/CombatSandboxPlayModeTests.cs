using System.Collections;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Player;
using MonstersVsZombies.Units.Special;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MonstersVsZombies.Tests.PlayMode
{
    public sealed class CombatSandboxPlayModeTests
    {
        private CombatSandboxBootstrap _bootstrap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("CombatSandbox", LoadSceneMode.Single);
            float timeout = Time.realtimeSinceStartup + 10f;
            do
            {
                yield return null;
                _bootstrap = Object.FindAnyObjectByType<CombatSandboxBootstrap>();
            }
            while ((_bootstrap == null || !_bootstrap.IsInitialized ||
                    _bootstrap.InitialPlayer == null ||
                    _bootstrap.InitialStationaryEnemy == null) &&
                   Time.realtimeSinceStartup < timeout);

            Assert.That(_bootstrap, Is.Not.Null);
            Assert.That(_bootstrap.IsInitialized, Is.True, _bootstrap.LastFailureMessage);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Start_CreatesAPlayablePlayerAndEnemyTarget()
        {
            yield return null;

            UnitController player = _bootstrap.InitialPlayer;
            UnitController enemy = _bootstrap.InitialStationaryEnemy;
            Assert.That(_bootstrap.IsGameplayEnabled, Is.True);
            Assert.That(player.IsActive, Is.True);
            Assert.That(player.Faction, Is.EqualTo(UnitFaction.Player));
            Assert.That(player.GetComponent<PlayerMotor>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerWeaponController>(), Is.Not.Null);
            Assert.That(enemy.IsActive, Is.True);
            Assert.That(enemy.Faction, Is.EqualTo(UnitFaction.Enemy));
            Assert.That(_bootstrap.UnitRegistry.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ResolveHit_AppliesHostileDamageOnceAndRejectsFriendlyFire()
        {
            yield return null;

            UnitController player = _bootstrap.InitialPlayer;
            UnitController enemy = _bootstrap.InitialStationaryEnemy;
            float initialHealth = enemy.HealthController.CurrentHealth;
            DamagePayload hostilePayload = new DamagePayload(
                player.SpawnId,
                player.Faction,
                new AttackSequenceId(1),
                5f,
                default);
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(hostilePayload.AttackKey);
            HitContext hostileHit = CreateHit(hostilePayload, enemy);

            InteractionResult firstResult =
                _bootstrap.InteractionSystem.ResolveHit(hostileHit, ledger);
            InteractionResult duplicateResult =
                _bootstrap.InteractionSystem.ResolveHit(hostileHit, ledger);

            Assert.That(firstResult.Outcome, Is.EqualTo(InteractionOutcome.Applied));
            Assert.That(enemy.HealthController.CurrentHealth,
                Is.EqualTo(initialHealth - 5f));
            Assert.That(duplicateResult.Outcome,
                Is.EqualTo(InteractionOutcome.AlreadyHit));

            DamagePayload friendlyPayload = new DamagePayload(
                new SpawnId(enemy.SpawnId.Value + 1000),
                UnitFaction.Enemy,
                new AttackSequenceId(1),
                5f,
                default);
            AttackHitLedger friendlyLedger = new AttackHitLedger();
            friendlyLedger.BeginAttack(friendlyPayload.AttackKey);
            InteractionResult friendlyResult =
                _bootstrap.InteractionSystem.ResolveHit(
                    CreateHit(friendlyPayload, enemy),
                    friendlyLedger);

            Assert.That(friendlyResult.Outcome,
                Is.EqualTo(InteractionOutcome.InvalidFaction));
        }

        [UnityTest]
        public IEnumerator ReturnAndRespawn_RestoresHealthAndAssignsNewIdentity()
        {
            yield return null;

            UnitController enemy = _bootstrap.InitialStationaryEnemy;
            SpawnId previousSpawnId = enemy.SpawnId;
            float maximumHealth = enemy.HealthController.MaximumHealth;
            enemy.HealthController.ApplyDamage(10f);
            Pose spawnPose = new Pose(enemy.transform.position, enemy.transform.rotation);

            Assert.That(_bootstrap.SpawnManager.ReturnUnit(enemy).IsSuccess, Is.True);
            SpawnResult<UnitController> respawnResult =
                _bootstrap.InitialSandboxSpawner.Spawn(
                    _bootstrap.StationaryEnemyDefinition,
                    spawnPose);

            Assert.That(respawnResult.IsSuccess, Is.True);
            Assert.That(respawnResult.Entity, Is.SameAs(enemy));
            Assert.That(respawnResult.Entity.SpawnId, Is.Not.EqualTo(previousSpawnId));
            Assert.That(respawnResult.Entity.HealthController.CurrentHealth,
                Is.EqualTo(maximumHealth));
            Assert.That(respawnResult.Entity.IsActive, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spawn_AlliedAiAcquiresAnEnemyTarget()
        {
            DebugUnitSpawner debugSpawner =
                Object.FindAnyObjectByType<DebugUnitSpawner>();
            Assert.That(debugSpawner, Is.Not.Null);
            Assert.That(
                debugSpawner.UnitCatalog.TryGetDefinition(
                    new UnitId("AllyClassicMelee"),
                    out UnitDefinition allyDefinition),
                Is.True);
            UnitController enemy = _bootstrap.InitialStationaryEnemy;
            Pose spawnPose = new Pose(
                enemy.transform.position + Vector3.left * 2f,
                Quaternion.identity);
            SpawnResult<UnitController> allyResult =
                debugSpawner.Spawn(allyDefinition, spawnPose);
            Assert.That(allyResult.IsSuccess, Is.True);

            yield return null;
            Physics.SyncTransforms();
            allyResult.Entity.TargetingController.ForceScan();

            UnitController target = allyResult.Entity.TargetingController.CurrentTarget;
            Assert.That(target, Is.Not.Null);
            Assert.That(target.Faction, Is.EqualTo(UnitFaction.Enemy));
        }

        [UnityTest]
        public IEnumerator SelectWeapon_CyclesEveryConfiguredPlayerWeapon()
        {
            yield return null;

            PlayerWeaponController weapons =
                _bootstrap.InitialPlayer.GetComponent<PlayerWeaponController>();
            Assert.That(weapons.WeaponCount, Is.EqualTo(3));
            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);

            for (int expectedIndex = 1; expectedIndex < weapons.WeaponCount;
                 expectedIndex++)
            {
                Assert.That(weapons.SelectNextWeapon(), Is.True);
                Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(expectedIndex));
                Assert.That(
                    _bootstrap.InitialPlayer.AttackController.AttackDefinition,
                    Is.SameAs(weapons.CurrentWeapon.AttackDefinition));
            }

            Assert.That(weapons.SelectNextWeapon(), Is.True);
            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);
            Assert.That(weapons.SelectPreviousWeapon(), Is.True);
            Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator KillDivisible_SpawnsExactlyThreeMiniDivisibles()
        {
            DebugUnitSpawner debugSpawner =
                Object.FindAnyObjectByType<DebugUnitSpawner>();
            SpawnResult<UnitController> divisibleResult =
                debugSpawner.Spawn(new UnitId("EnemyDivisible"));
            Assert.That(divisibleResult.IsSuccess, Is.True);
            SpawnUnitsOnDeath spawnOnDeath =
                divisibleResult.Entity.GetComponent<SpawnUnitsOnDeath>();
            Assert.That(spawnOnDeath, Is.Not.Null);
            bool didComplete = false;
            DeathSpawnCompletedEvent completedEvent = default;
            spawnOnDeath.DeathSpawnCompleted += result =>
            {
                completedEvent = result;
                didComplete = true;
            };

            divisibleResult.Entity.HealthController.ApplyDamage(
                divisibleResult.Entity.HealthController.MaximumHealth);
            yield return null;

            Assert.That(didComplete, Is.True);
            Assert.That(completedEvent.SpawnedCount, Is.EqualTo(3));
            Assert.That(completedEvent.FailedCount, Is.Zero);
        }

        private static HitContext CreateHit(
            DamagePayload payload,
            UnitController target)
        {
            return new HitContext(
                payload,
                target.DamageController,
                target.transform.position,
                Vector3.up,
                HitType.Direct,
                "PlayModeTest");
        }
    }
}
