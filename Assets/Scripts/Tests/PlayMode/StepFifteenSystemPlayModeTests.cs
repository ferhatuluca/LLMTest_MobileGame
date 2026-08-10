using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.Projectiles;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using MonstersVsZombies.Units.Player;
using MonstersVsZombies.Units.Special;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MonstersVsZombies.Tests.PlayMode
{
    public sealed class StepFifteenSystemPlayModeTests
    {
        private const string k_CombatSandboxSceneName = "CombatSandbox";
        private const string k_SampleSceneName = "SampleScene";
        private static long s_attackSequence;

        private CombatSandboxBootstrap _bootstrap;
        private DebugUnitSpawner _debugSpawner;

        [UnitySetUp]
        public IEnumerator LoadCombatSandbox()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                k_CombatSandboxSceneName,
                LoadSceneMode.Single);
            Assert.That(sceneLoad, Is.Not.Null);
            yield return sceneLoad;

            for (int frame = 0; frame < 120; frame++)
            {
                _bootstrap = UnityEngine.Object
                    .FindAnyObjectByType<CombatSandboxBootstrap>();
                _debugSpawner = UnityEngine.Object
                    .FindAnyObjectByType<DebugUnitSpawner>();
                if (_bootstrap != null && _debugSpawner != null &&
                    _bootstrap.IsGameplayEnabled &&
                    _bootstrap.InitialPlayer != null &&
                    _bootstrap.InitialPlayer.IsActive)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                "CombatSandbox did not initialize and spawn its Player within 120 frames.");
        }

        [UnityTearDown]
        public IEnumerator UnloadCombatSandbox()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                k_SampleSceneName,
                LoadSceneMode.Single);
            Assert.That(sceneLoad, Is.Not.Null);
            yield return sceneLoad;
            _bootstrap = null;
            _debugSpawner = null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_InitializesServicesBeforeImmediatePlayerSpawn()
        {
            Assert.That(_bootstrap.IsInitialized, Is.True);
            Assert.That(_bootstrap.IsGameplayEnabled, Is.True);
            Assert.That(_bootstrap.InitialPlayer, Is.Not.Null);
            Assert.That(_bootstrap.InitialPlayer.IsActive, Is.True);
            Assert.That(_bootstrap.InitialPlayer.SpawnId.IsValid, Is.True);
            Assert.That(_bootstrap.UnitRegistry.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(_bootstrap.InitialPlayer.GetComponent<
                MonstersVsZombies.Units.Player.PlayerInputReader>().IsInputEnabled,
                Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllyVersusEnemy_AcceptsOnlyHostileDamage()
        {
            ClearNonPlayerEntities();
            UnitController ally = SpawnUnit(
                "AllyClassicMelee",
                new Vector3(-2f, 0f, 0f));
            UnitController enemy = SpawnUnit(
                "EnemyClassicMelee",
                new Vector3(2f, 0f, 0f));
            UnitController friendly = SpawnUnit(
                "AllyClassicRange",
                new Vector3(-3f, 0f, 0f));

            InteractionResult hostileResult = ResolveHit(
                ally,
                enemy,
                10f,
                "Step15AllyEnemy");
            InteractionResult friendlyResult = ResolveHit(
                ally,
                friendly,
                10f,
                "Step15Friendly");

            Assert.That(hostileResult.IsApplied, Is.True);
            Assert.That(enemy.HealthController.CurrentHealth,
                Is.EqualTo(enemy.HealthController.MaximumHealth - 10f));
            Assert.That(friendlyResult.Outcome,
                Is.EqualTo(InteractionOutcome.InvalidFaction));
            Assert.That(friendly.HealthController.CurrentHealth,
                Is.EqualTo(friendly.HealthController.MaximumHealth));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FriendlyFire_IsRejectedForEveryDeliveryType()
        {
            ClearNonPlayerEntities();
            UnitController source = _bootstrap.InitialPlayer;
            UnitController friendly = SpawnUnit(
                "AllyClassicMelee",
                source.transform.position + Vector3.right * 2f);
            DamageTargetProxy friendlyProxy = GetHurtbox(friendly);

            InteractionResult meleeResult = ResolveHit(
                source,
                friendly,
                10f,
                "Melee");
            Assert.That(meleeResult.Outcome,
                Is.EqualTo(InteractionOutcome.InvalidFaction));

            foreach (AttackDeliveryType deliveryType in new[]
                     {
                         AttackDeliveryType.Projectile,
                         AttackDeliveryType.Grenade,
                         AttackDeliveryType.Hitscan
                     })
            {
                DamagePayload payload = CreatePayload(
                    source,
                    10f,
                    $"Step15{deliveryType}");
                DeliveryContactType contactType =
                    DeliveryCollisionRules.Classify(
                        friendlyProxy.TargetCollider,
                        payload,
                        out DamageTargetProxy classifiedProxy);
                Assert.That(contactType, Is.EqualTo(DeliveryContactType.Ignore),
                    $"{deliveryType} must ignore friendly hurtboxes.");
                Assert.That(classifiedProxy, Is.Null);
            }

            Assert.That(friendly.HealthController.CurrentHealth,
                Is.EqualTo(friendly.HealthController.MaximumHealth));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CollisionPolicies_CoverSourceFriendlyInactiveDeadAndWorld()
        {
            ClearNonPlayerEntities();
            UnitController source = _bootstrap.InitialPlayer;
            UnitController hostile = SpawnUnit(
                "EnemyClassicMelee",
                source.transform.position + Vector3.forward * 3f);
            UnitController friendly = SpawnUnit(
                "AllyClassicMelee",
                source.transform.position + Vector3.left * 3f);
            UnitController inactive = SpawnUnit(
                "EnemyClassicRange",
                source.transform.position + Vector3.right * 3f);
            UnitController dead = SpawnUnit(
                "EnemyDragon",
                source.transform.position + Vector3.back * 3f);
            DamageTargetProxy sourceProxy = GetHurtbox(source);
            DamageTargetProxy hostileProxy = GetHurtbox(hostile);
            DamageTargetProxy friendlyProxy = GetHurtbox(friendly);
            DamageTargetProxy inactiveProxy = GetHurtbox(inactive);
            DamageTargetProxy deadProxy = GetHurtbox(dead);

            Assert.That(_bootstrap.SpawnManager.ReturnUnit(inactive).IsSuccess,
                Is.True);
            dead.HealthController.ApplyDamage(
                dead.HealthController.MaximumHealth + 1f);

            GameObject world = GameObject.CreatePrimitive(PrimitiveType.Cube);
            world.name = "Step15WorldPolicy";
            world.layer = LayerMask.NameToLayer(
                DeliveryCollisionRules.WorldLayerName);
            Collider worldCollider = world.GetComponent<Collider>();
            DamagePayload payload = CreatePayload(
                source,
                10f,
                "Step15CollisionPolicy");

            foreach (AttackDeliveryType deliveryType in new[]
                     {
                         AttackDeliveryType.Projectile,
                         AttackDeliveryType.Grenade,
                         AttackDeliveryType.Hitscan
                     })
            {
                AssertContact(sourceProxy.TargetCollider, payload,
                    DeliveryContactType.Ignore, deliveryType, "source");
                AssertContact(friendlyProxy.TargetCollider, payload,
                    DeliveryContactType.Ignore, deliveryType, "friendly");
                AssertContact(inactiveProxy.TargetCollider, payload,
                    DeliveryContactType.Ignore, deliveryType, "inactive");
                AssertContact(deadProxy.TargetCollider, payload,
                    DeliveryContactType.Ignore, deliveryType, "dead");
                AssertContact(worldCollider, payload,
                    DeliveryContactType.World, deliveryType, "World");
                AssertContact(hostileProxy.TargetCollider, payload,
                    DeliveryContactType.HostileTarget, deliveryType, "hostile");
            }

            UnityEngine.Object.Destroy(world);
            yield return null;
        }
        [UnityTest]
        public IEnumerator AI_TransitionsChaseAttackChaseAndClearsOutsideRange()
        {
            ClearNonPlayerEntities();
            MovePlayerFarAway();
            UnitController ally = SpawnAtAuthoredPoint(
                "AllyClassicMelee",
                _bootstrap.AllySpawnPoints,
                0);
            UnitController target = SpawnAtAuthoredPoint(
                "EnemyClassicMelee",
                _bootstrap.EnemySpawnPoints,
                0);
            AIUnitBrain brain = ally.GetComponent<AIUnitBrain>();
            AIUnitDefinition definition = (AIUnitDefinition)ally.Definition;
            float attackRange = ally.AttackController.AttackDefinition.AttackRange;
            Vector3 origin = ally.transform.position;

            Warp(target, origin + Vector3.forward *
                ((attackRange + definition.ChaseRange) * 0.5f));
            Physics.SyncTransforms();
            Assert.That(ally.TargetingController.ForceScan(), Is.True);
            brain.AdvanceDecision(0f);
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));

            Warp(target, origin + Vector3.forward * (attackRange * 0.75f));
            Physics.SyncTransforms();
            brain.AdvanceDecision(0.1f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Attack));

            Warp(target, origin + Vector3.forward *
                ((attackRange + definition.ChaseRange) * 0.5f));
            Physics.SyncTransforms();
            brain.AdvanceDecision(0.1f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));

            Warp(target, origin + Vector3.forward *
                (definition.ChaseRange + 1f));
            Physics.SyncTransforms();
            Assert.That(ally.TargetingController.ForceScan(), Is.False);
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Idle));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stun_StopsMovingAIThenExpiresAndResumes()
        {
            ClearNonPlayerEntities();
            MovePlayerFarAway();
            UnitController ally = SpawnAtAuthoredPoint(
                "AllyClassicMelee",
                _bootstrap.AllySpawnPoints,
                0);
            UnitController target = SpawnAtAuthoredPoint(
                "EnemyClassicMelee",
                _bootstrap.EnemySpawnPoints,
                0);
            AIUnitDefinition definition = (AIUnitDefinition)ally.Definition;
            float attackRange = ally.AttackController.AttackDefinition.AttackRange;
            Warp(target, ally.transform.position + Vector3.forward *
                ((attackRange + definition.ChaseRange) * 0.5f));
            Physics.SyncTransforms();

            AIUnitBrain brain = ally.GetComponent<AIUnitBrain>();
            NavMeshUnitMotor motor = ally.GetComponent<NavMeshUnitMotor>();
            Assert.That(ally.TargetingController.ForceScan(), Is.True);
            brain.AdvanceDecision(0f);
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));
            Assert.That(motor.IsStopped, Is.False);

            Assert.That(
                ally.StatusEffectController.ApplyAcceptedEffect(
                    new StatusEffectPayload(StatusEffectType.Stun, 1f)),
                Is.True);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Disabled));
            Assert.That(motor.IsStopped, Is.True);
            Assert.That(motor.HasPath, Is.False);
            Assert.That(ally.TargetingController.CurrentTarget, Is.Null);

            ally.StatusEffectController.AdvanceTime(1f);
            Assert.That(ally.TargetingController.ForceScan(), Is.True);
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Idle));
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));
            Assert.That(motor.IsStopped, Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator DivisibleDeath_CreatesExactlyThreeIndependentMiniDivisibles()
        {
            SpecialUnitSandboxScenarioController scenario =
                UnityEngine.Object.FindAnyObjectByType<
                    SpecialUnitSandboxScenarioController>();
            Assert.That(scenario, Is.Not.Null);
            Assert.That(scenario.IsInitialized, Is.True);
            UnitController divisible = FindScenarioUnit(
                scenario,
                "EnemyDivisible");
            int enemyCountBefore =
                _bootstrap.UnitRegistry.GetFactionCount(UnitFaction.Enemy);
            int miniCountBefore = CountActiveUnits("EnemyMiniDivisible");

            InteractionResult result = ResolvePlayerHit(
                divisible,
                divisible.HealthController.MaximumHealth + 1f,
                "Step15DivisibleDeath");
            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.DamageResult.TargetDied, Is.True);

            for (int frame = 0; frame < 60 &&
                 CountActiveUnits("EnemyMiniDivisible") <
                 miniCountBefore + MiniDivisibleSpawnFormation.ChildCount;
                 frame++)
            {
                yield return null;
            }

            Assert.That(divisible.IsActive, Is.False);
            Assert.That(divisible.gameObject.activeSelf, Is.False);
            Assert.That(CountActiveUnits("EnemyMiniDivisible"),
                Is.EqualTo(
                    miniCountBefore + MiniDivisibleSpawnFormation.ChildCount));
            Assert.That(
                _bootstrap.UnitRegistry.GetFactionCount(UnitFaction.Enemy),
                Is.EqualTo(enemyCountBefore + 2));
            AssertMiniDivisiblesAreIndependent();
        }
        [UnityTest]
        public IEnumerator PooledStunnerAndDivisible_ResetEveryTransientState()
        {
            SpecialUnitSandboxScenarioController scenario =
                UnityEngine.Object.FindAnyObjectByType<
                    SpecialUnitSandboxScenarioController>();
            DebugUnitSpawner debugSpawner =
                UnityEngine.Object.FindAnyObjectByType<DebugUnitSpawner>();
            Assert.That(scenario, Is.Not.Null);
            Assert.That(debugSpawner, Is.Not.Null);

            UnitController stunner = FindScenarioUnit(scenario, "EnemyStunner");
            SpawnId previousStunnerSpawnId = stunner.SpawnId;
            stunner.HealthController.ApplyDamage(10f);
            Assert.That(
                stunner.StatusEffectController.ApplyAcceptedEffect(
                    new StatusEffectPayload(StatusEffectType.Stun, 5f)),
                Is.True);
            StunnerHitPolicy policy = stunner.GetComponent<StunnerHitPolicy>();
            RecordSuccessfulHit(policy, stunner, _bootstrap.InitialPlayer);
            Assert.That(policy.SuccessfulHitCount, Is.EqualTo(1));

            Assert.That(
                _bootstrap.SpawnManager.ReturnUnit(stunner).IsSuccess,
                Is.True);
            SpawnResult<UnitController> stunnerRespawn =
                debugSpawner.Spawn(new UnitId("EnemyStunner"));
            Assert.That(stunnerRespawn.IsSuccess, Is.True);
            Assert.That(stunnerRespawn.Entity, Is.SameAs(stunner));
            Assert.That(stunner.SpawnId, Is.Not.EqualTo(previousStunnerSpawnId));
            Assert.That(stunner.HealthController.CurrentHealth,
                Is.EqualTo(stunner.HealthController.MaximumHealth));
            Assert.That(stunner.TargetingController.CurrentTarget, Is.Null);
            Assert.That(stunner.StatusEffectController.IsStunned, Is.False);
            Assert.That(policy.SuccessfulHitCount, Is.Zero);

            UnitController divisible = FindScenarioUnit(
                scenario,
                "EnemyDivisible");
            SpawnId previousDivisibleSpawnId = divisible.SpawnId;
            SpawnUnitsOnDeath deathSpawner =
                divisible.GetComponent<SpawnUnitsOnDeath>();
            Assert.That(
                divisible.StatusEffectController.ApplyAcceptedEffect(
                    new StatusEffectPayload(StatusEffectType.Stun, 5f)),
                Is.True);
            ResolvePlayerHit(
                divisible,
                divisible.HealthController.MaximumHealth + 1f,
                "Step15DivisibleReuse");
            for (int frame = 0; frame < 60 && divisible.gameObject.activeSelf;
                 frame++)
            {
                yield return null;
            }

            SpawnResult<UnitController> divisibleRespawn =
                debugSpawner.Spawn(new UnitId("EnemyDivisible"));
            Assert.That(divisibleRespawn.IsSuccess, Is.True);
            Assert.That(divisibleRespawn.Entity, Is.SameAs(divisible));
            Assert.That(divisible.SpawnId,
                Is.Not.EqualTo(previousDivisibleSpawnId));
            Assert.That(divisible.HealthController.CurrentHealth,
                Is.EqualTo(divisible.HealthController.MaximumHealth));
            Assert.That(divisible.TargetingController.CurrentTarget, Is.Null);
            Assert.That(divisible.StatusEffectController.IsStunned, Is.False);
            Assert.That(deathSpawner.HasFiredForCurrentSpawn, Is.False);
            Assert.That(deathSpawner.LastSpawnedCount, Is.Zero);
            Assert.That(deathSpawner.LastFailedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Projectiles_ImpactExpireAndReturnToTheirPools()
        {
            ClearNonPlayerEntities();
            UnitController source = _bootstrap.InitialPlayer;
            ProjectileDefinition bullet = source
                .GetComponent<PlayerWeaponController>()
                .GetWeaponSlot(0).Definition.AttackDefinition.ProjectileDefinition;
            ProjectileDefinition grenade = source
                .GetComponent<PlayerWeaponController>()
                .GetWeaponSlot(1).Definition.AttackDefinition.ProjectileDefinition;
            ProjectileDefinition fireball = GetDefinition("EnemyDragon")
                .DefaultAttackDefinition.ProjectileDefinition;

            foreach (ProjectileDefinition definition in new[] { bullet, fireball })
            {
                UnitController target = SpawnUnit(
                    "EnemyClassicMelee",
                    source.transform.position + Vector3.forward * 3f);
                DamageTargetProxy hurtbox = GetHurtbox(target);
                float healthBefore = target.HealthController.CurrentHealth;
                ProjectileController impactProjectile = SpawnProjectile(
                    definition,
                    source,
                    hurtbox.TargetCollider.bounds.center,
                    10f);
                impactProjectile.AdvanceTime(definition.MaximumLifetime);
                Assert.That(target.HealthController.CurrentHealth,
                    Is.EqualTo(healthBefore - 10f));
                Assert.That(impactProjectile.gameObject.activeSelf, Is.False);

                ProjectileController expiryProjectile = SpawnProjectile(
                    definition,
                    source,
                    source.transform.position + Vector3.up * 25f,
                    Vector3.up,
                    10f);
                expiryProjectile.AdvanceTime(definition.MaximumLifetime);
                Assert.That(expiryProjectile.gameObject.activeSelf, Is.False);
                Assert.That(GetPoolDiagnostics(definition.PoolId).ActiveCount,
                    Is.Zero);
                _bootstrap.SpawnManager.ReturnUnit(target);
            }

            ProjectileController grenadeProjectile = SpawnProjectile(
                grenade,
                source,
                source.transform.position + Vector3.up * 20f,
                Vector3.forward,
                10f);
            grenadeProjectile.AdvanceTime(grenade.FuseDuration);
            Assert.That(grenadeProjectile.gameObject.activeSelf, Is.False);
            Assert.That(GetPoolDiagnostics(grenade.PoolId).ActiveCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Grenade_MultipleHurtboxesApplyDamageOnlyOnce()
        {
            ClearNonPlayerEntities();
            UnitController source = _bootstrap.InitialPlayer;
            UnitController target = SpawnUnit(
                "EnemyClassicMelee",
                source.transform.position + Vector3.forward * 3f);
            DamageTargetProxy primaryHurtbox = GetHurtbox(target);
            GameObject duplicateHurtbox = new GameObject("Step15DuplicateHurtbox");
            duplicateHurtbox.layer = LayerMask.NameToLayer(
                DeliveryCollisionRules.UnitTargetLayerName);
            duplicateHurtbox.transform.SetParent(target.transform, false);
            duplicateHurtbox.transform.position =
                primaryHurtbox.TargetCollider.bounds.center;
            SphereCollider duplicateCollider =
                duplicateHurtbox.AddComponent<SphereCollider>();
            duplicateCollider.isTrigger = true;
            duplicateCollider.radius = 0.75f;
            DamageTargetProxy duplicateProxy =
                duplicateHurtbox.AddComponent<DamageTargetProxy>();
            duplicateProxy.CacheOwnerReferences();
            Assert.That(duplicateProxy.ValidateReferences(out string failure),
                Is.True,
                failure);
            Physics.SyncTransforms();

            ProjectileDefinition grenade = source
                .GetComponent<PlayerWeaponController>()
                .GetWeaponSlot(1).Definition.AttackDefinition.ProjectileDefinition;
            float healthBefore = target.HealthController.CurrentHealth;
            ProjectileController projectile = SpawnProjectile(
                grenade,
                source,
                primaryHurtbox.TargetCollider.bounds.center,
                Vector3.forward,
                10f);
            projectile.AdvanceTime(grenade.FuseDuration);

            Assert.That(target.HealthController.CurrentHealth,
                Is.EqualTo(healthBefore - 10f));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator InFlightProjectile_RemainsSafeAfterSourceDeathAndReuse()
        {
            ClearNonPlayerEntities();
            UnitController source = _bootstrap.InitialPlayer;
            SpawnId capturedSourceSpawnId = source.SpawnId;
            UnitController target = SpawnUnit(
                "EnemyClassicMelee",
                source.transform.position + Vector3.forward * 3f);
            DamageTargetProxy hurtbox = GetHurtbox(target);
            ProjectileDefinition bullet = source
                .GetComponent<PlayerWeaponController>()
                .GetWeaponSlot(0).Definition.AttackDefinition.ProjectileDefinition;
            ProjectileController projectile = SpawnProjectile(
                bullet,
                source,
                hurtbox.TargetCollider.bounds.center,
                17f);
            Assert.That(projectile.DamagePayload.SourceSpawnId,
                Is.EqualTo(capturedSourceSpawnId));

            Assert.That(_bootstrap.ResetPlayer(), Is.True);
            Assert.That(_bootstrap.InitialPlayer.SpawnId,
                Is.Not.EqualTo(capturedSourceSpawnId));
            projectile.AdvanceTime(bullet.MaximumLifetime);

            Assert.That(target.HealthController.CurrentHealth,
                Is.EqualTo(target.HealthController.MaximumHealth - 17f));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
            Assert.That(projectile.DamagePayload.SourceSpawnId.Value, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActiveAI_HasNavMeshAndRuntimeServicesBeforeDecision()
        {
            AIUnitBrain[] brains = UnityEngine.Object
                .FindObjectsByType<AIUnitBrain>(
                    FindObjectsInactive.Exclude);
            Assert.That(brains.Length, Is.GreaterThan(0));
            foreach (AIUnitBrain brain in brains)
            {
                UnitController unit = brain.GetComponent<UnitController>();
                NavMeshUnitMotor motor =
                    brain.GetComponent<NavMeshUnitMotor>();
                Assert.That(unit, Is.Not.Null);
                Assert.That(unit.IsActive, Is.True);
                Assert.That(brain.HasRuntimeServices, Is.True);
                Assert.That(motor, Is.Not.Null);
                Assert.That(motor.IsOnNavMesh, Is.True);
                Assert.That(brain.State, Is.Not.EqualTo(AIUnitState.Disabled));
            }

            yield return null;
        }

        private void ClearNonPlayerEntities()
        {
            _debugSpawner.ClearNonPlayerUnitsAndProjectiles();
            Assert.That(_bootstrap.UnitRegistry.GetFactionCount(UnitFaction.Player),
                Is.EqualTo(1));
        }

        private void MovePlayerFarAway()
        {
            _bootstrap.InitialPlayer.transform.position =
                new Vector3(100f, 0f, 100f);
            Physics.SyncTransforms();
        }

        private UnitController SpawnUnit(string unitId, Vector3 position)
        {
            UnitDefinition definition = GetDefinition(unitId);
            SpawnResult<UnitController> result = _debugSpawner.Spawn(
                definition,
                new Pose(position, Quaternion.identity));
            Assert.That(result.IsSuccess, Is.True,
                $"Could not spawn {unitId}: {result.FailureReason}.");
            return result.Entity;
        }

        private UnitController SpawnAtAuthoredPoint(
            string unitId,
            SpawnPointGroup spawnPoints,
            int index)
        {
            Assert.That(spawnPoints.TryGetPoint(index, out Pose pose), Is.True);
            UnitDefinition definition = GetDefinition(unitId);
            SpawnResult<UnitController> result = _debugSpawner.Spawn(
                definition,
                pose);
            Assert.That(result.IsSuccess, Is.True,
                $"Could not spawn {unitId}: {result.FailureReason}.");
            return result.Entity;
        }

        private AIUnitDefinition GetDefinition(string unitId)
        {
            Assert.That(
                _bootstrap.UnitCatalog.TryGetDefinition(
                    new UnitId(unitId),
                    out UnitDefinition definition),
                Is.True,
                $"UnitCatalog is missing '{unitId}'.");
            Assert.That(definition, Is.TypeOf<AIUnitDefinition>());
            return (AIUnitDefinition)definition;
        }

        private InteractionResult ResolveHit(
            UnitController source,
            UnitController target,
            float damage,
            string deliveryIdentifier)
        {
            DamagePayload payload = CreatePayload(
                source,
                damage,
                deliveryIdentifier);
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(payload.AttackKey);
            return _bootstrap.InteractionSystem.ResolveHit(
                new HitContext(
                    payload,
                    target.DamageController,
                    target.transform.position,
                    Vector3.up,
                    HitType.Direct,
                    deliveryIdentifier),
                ledger);
        }

        private static DamagePayload CreatePayload(
            UnitController source,
            float damage,
            string category)
        {
            AttackSequenceId sequenceId =
                new AttackSequenceId(++s_attackSequence);
            return new DamagePayload(
                source.SpawnId,
                source.Faction,
                sequenceId,
                damage,
                new DamageCategoryId(category));
        }

        private static DamageTargetProxy GetHurtbox(UnitController unit)
        {
            DamageTargetProxy proxy = unit.GetComponentInChildren<
                DamageTargetProxy>(true);
            Assert.That(proxy, Is.Not.Null);
            Assert.That(proxy.ValidateReferences(out string failure),
                Is.True,
                failure);
            return proxy;
        }

        private static void AssertContact(
            Collider collider,
            DamagePayload payload,
            DeliveryContactType expected,
            AttackDeliveryType deliveryType,
            string contactLabel)
        {
            DeliveryContactType actual = DeliveryCollisionRules.Classify(
                collider,
                payload,
                out _);
            Assert.That(actual, Is.EqualTo(expected),
                $"{deliveryType} {contactLabel} policy mismatch.");
        }

        private static void Warp(UnitController unit, Vector3 position)
        {
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                Assert.That(agent.Warp(position), Is.True);
            }
            else
            {
                unit.transform.position = position;
            }
        }
        private ProjectileController SpawnProjectile(
            ProjectileDefinition definition,
            UnitController source,
            Vector3 targetPosition,
            float damage)
        {
            Vector3 origin = source.transform.position + Vector3.up;
            Vector3 direction = (targetPosition - origin).normalized;
            return SpawnProjectile(
                definition,
                source,
                origin,
                direction,
                damage);
        }

        private ProjectileController SpawnProjectile(
            ProjectileDefinition definition,
            UnitController source,
            Vector3 position,
            Vector3 direction,
            float damage)
        {
            DamagePayload payload = CreatePayload(
                source,
                damage,
                $"Step15{definition.PoolId}");
            SpawnResult<PooledEntity> result =
                _bootstrap.SpawnManager.SpawnProjectile(
                    new ProjectileSpawnRequest(
                        definition,
                        payload,
                        position,
                        Quaternion.LookRotation(direction, Vector3.up)),
                    _bootstrap.InteractionSystem);
            Assert.That(result.IsSuccess, Is.True,
                $"Could not spawn projectile {definition.PoolId}: " +
                result.FailureReason);
            ProjectileController projectile =
                result.Entity.GetComponent<ProjectileController>();
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.IsRunning, Is.True);
            return projectile;
        }

        private PoolDiagnostics GetPoolDiagnostics(PoolId poolId)
        {
            Assert.That(
                _bootstrap.PoolManager.TryGetDiagnostics(
                    poolId,
                    out PoolDiagnostics diagnostics),
                Is.True,
                $"Missing diagnostics for pool {poolId}.");
            return diagnostics;
        }

        private InteractionResult ResolvePlayerHit(
            UnitController target,
            float damage,
            string deliveryIdentifier)
        {
            AttackSequenceId sequenceId =
                new AttackSequenceId(++s_attackSequence);
            AttackKey attackKey = new AttackKey(
                _bootstrap.InitialPlayer.SpawnId,
                sequenceId);
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(attackKey);
            DamagePayload payload = new DamagePayload(
                attackKey.SourceSpawnId,
                UnitFaction.Player,
                sequenceId,
                damage,
                new DamageCategoryId("Step15Integration"));
            return _bootstrap.InteractionSystem.ResolveHit(
                new HitContext(
                    payload,
                    target.DamageController,
                    target.transform.position,
                    Vector3.up,
                    HitType.Direct,
                    deliveryIdentifier),
                ledger);
        }

        private int CountActiveUnits(string unitId)
        {
            UnitId expectedId = new UnitId(unitId);
            List<UnitController> units = new List<UnitController>();
            _bootstrap.UnitRegistry.CopySnapshot(units);
            return units.Count(unit =>
                unit != null &&
                unit.IsActive &&
                unit.Definition != null &&
                unit.Definition.UnitId == expectedId);
        }

        private void AssertMiniDivisiblesAreIndependent()
        {
            UnitId miniId = new UnitId("EnemyMiniDivisible");
            List<UnitController> units = new List<UnitController>();
            _bootstrap.UnitRegistry.CopySnapshot(units);
            foreach (UnitController unit in units.Where(unit =>
                         unit != null &&
                         unit.IsActive &&
                         unit.Definition != null &&
                         unit.Definition.UnitId == miniId))
            {
                Assert.That(unit.GetComponent<SpawnUnitsOnDeath>(), Is.Null);
            }
        }

        private static UnitController FindScenarioUnit(
            SpecialUnitSandboxScenarioController scenario,
            string unitId)
        {
            UnitId expectedId = new UnitId(unitId);
            UnitController unit = scenario.SpawnedUnits.FirstOrDefault(
                candidate =>
                    candidate != null &&
                    candidate.Definition != null &&
                    candidate.Definition.UnitId == expectedId);
            Assert.That(unit, Is.Not.Null,
                $"Scenario unit '{unitId}' was not spawned.");
            return unit;
        }

        private static void RecordSuccessfulHit(
            StunnerHitPolicy policy,
            UnitController source,
            UnitController target)
        {
            AttackSequenceId sequenceId = new AttackSequenceId(5001);
            AttackKey attackKey = new AttackKey(source.SpawnId, sequenceId);
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(attackKey);
            DamagePayload payload = new DamagePayload(
                source.SpawnId,
                source.Faction,
                sequenceId,
                1f,
                new DamageCategoryId("Step15StunnerCadence"));
            AttackExecutionContext context = new AttackExecutionContext(
                source,
                target,
                target.transform.position,
                source.AttackController.AttackDefinition,
                attackKey,
                ledger,
                payload);
            policy.HandleSuccessfulInteraction(
                context,
                InteractionResult.CreateApplied(
                    attackKey,
                    target.SpawnId,
                    DamageResult.CreateApplied(1f, false)));
        }

    }

    public sealed class StepFifteenInputPlayModeTests : InputTestFixture
    {
        private const string k_CombatSandboxSceneName = "CombatSandbox";
        private const string k_SampleSceneName = "SampleScene";

        private CombatSandboxBootstrap _bootstrap;

        [UnitySetUp]
        public IEnumerator LoadCombatSandbox()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                k_CombatSandboxSceneName,
                LoadSceneMode.Single);
            Assert.That(sceneLoad, Is.Not.Null);
            yield return sceneLoad;

            for (int frame = 0; frame < 120; frame++)
            {
                _bootstrap = UnityEngine.Object
                    .FindAnyObjectByType<CombatSandboxBootstrap>();
                if (_bootstrap != null &&
                    _bootstrap.IsGameplayEnabled &&
                    _bootstrap.InitialPlayer != null &&
                    _bootstrap.InitialPlayer.IsActive)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                "CombatSandbox did not initialize its Player for the input test.");
        }

        [UnityTearDown]
        public IEnumerator UnloadCombatSandbox()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                k_SampleSceneName,
                LoadSceneMode.Single);
            if (sceneLoad != null)
            {
                yield return sceneLoad;
            }

            _bootstrap = null;
        }

        [UnityTest]
        public IEnumerator InputSystem_QAndEWrapPistolGrenadeGunAndSpaceGun()
        {
            DebugUnitSpawner debugSpawner = UnityEngine.Object
                .FindAnyObjectByType<DebugUnitSpawner>();
            Assert.That(debugSpawner, Is.Not.Null);
            debugSpawner.ClearNonPlayerUnitsAndProjectiles();
            Assert.That(_bootstrap.ResetPlayer(), Is.True);
            yield return null;

            PlayerWeaponController weapons = _bootstrap.InitialPlayer
                .GetComponent<PlayerWeaponController>();
            PlayerInputReader input = _bootstrap.InitialPlayer
                .GetComponent<PlayerInputReader>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.EnableDevice(keyboard);
            input.PreviousWeaponAction.action.Disable();
            input.NextWeaponAction.action.Disable();
            input.PreviousWeaponAction.action.Enable();
            input.NextWeaponAction.action.Enable();
            int previousActionPerformedCount = 0;
            int previousRequestCount = 0;
            input.PreviousWeaponAction.action.performed +=
                _ => previousActionPerformedCount++;
            input.PreviousWeaponRequested += () => previousRequestCount++;

            Assert.That(input.IsInputEnabled, Is.True);
            Assert.That(keyboard.enabled, Is.True);
            Assert.That(input.PreviousWeaponAction.action.controls,
                Has.Some.SameAs(keyboard.qKey));
            Assert.That(input.NextWeaponAction.action.controls,
                Has.Some.SameAs(keyboard.eKey));
            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);

            yield return PressAndReleaseOverFrames(keyboard.qKey);
            Assert.That(previousActionPerformedCount, Is.EqualTo(1),
                "Q did not perform the configured PreviousWeapon action.");
            Assert.That(previousRequestCount, Is.EqualTo(1),
                "PlayerInputReader did not publish the Q request.");
            Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(2));
            Assert.That(weapons.CurrentWeapon.WeaponId,
                Is.EqualTo(new WeaponId("SpaceGun")));

            yield return PressAndReleaseOverFrames(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);
            yield return PressAndReleaseOverFrames(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(1));
            Assert.That(weapons.CurrentWeapon.WeaponId,
                Is.EqualTo(new WeaponId("GrenadeGun")));
            yield return PressAndReleaseOverFrames(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(2));
            yield return PressAndReleaseOverFrames(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);
            Assert.That(weapons.CurrentWeapon.WeaponId,
                Is.EqualTo(new WeaponId("Pistol")));
        }

        private IEnumerator PressAndReleaseOverFrames(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            InputSystem.Update();
            yield return null;
            Release(button, queueEventOnly: true);
            InputSystem.Update();
            yield return null;
        }
    }
}
