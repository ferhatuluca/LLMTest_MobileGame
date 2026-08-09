using System;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using MonstersVsZombies.Units.Lifecycle;
using MonstersVsZombies.Units.Special;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepThirteenSpecialUnitFeatureTests
    {
        [Test]
        public void StunnerPolicy_AddsStunOnlyOnHitsOneFourAndSeven()
        {
            GameObject instance = InstantiatePrefab("EnemyStunner");
            try
            {
                StunnerHitPolicy policy =
                    instance.GetComponent<StunnerHitPolicy>();
                UnitController source = instance.GetComponent<UnitController>();
                AttackDefinition attack = source.Definition is
                    AIUnitDefinition definition
                        ? definition.DefaultAttackDefinition
                        : null;
                AttackKey attackKey = new AttackKey(
                    new SpawnId(1),
                    new AttackSequenceId(1));
                AttackExecutionContext context = new AttackExecutionContext(
                    source,
                    null,
                    Vector3.zero,
                    attack,
                    attackKey,
                    new AttackHitLedger());
                DamagePayload basePayload = new DamagePayload(
                    attackKey.SourceSpawnId,
                    UnitFaction.Enemy,
                    attackKey.SequenceId,
                    attack.Damage,
                    attack.DamageCategoryId,
                    new StatusEffectPayload(StatusEffectType.Stun, 2f));

                Assert.That(policy.PrepareForSpawn(), Is.True);
                for (int hitNumber = 1; hitNumber <= 7; hitNumber++)
                {
                    DamagePayload payload = policy.ModifyPayload(
                        context,
                        basePayload);
                    bool expectedStun = hitNumber == 1 ||
                                        hitNumber == 4 ||
                                        hitNumber == 7;
                    Assert.That(
                        payload.StatusEffectCount,
                        Is.EqualTo(expectedStun ? 1 : 0),
                        $"Hit {hitNumber}");
                    if (expectedStun)
                    {
                        Assert.That(
                            payload.GetStatusEffect(0).Type,
                            Is.EqualTo(StatusEffectType.Stun));
                        Assert.That(
                            payload.GetStatusEffect(0).Duration,
                            Is.EqualTo(2f));
                    }

                    policy.HandleSuccessfulInteraction(
                        context,
                        CreateAppliedResult(attackKey));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void StunnerPolicy_RejectionsMissesAndPoolResetDoNotAdvance()
        {
            GameObject instance = InstantiatePrefab("EnemyStunner");
            try
            {
                StunnerHitPolicy policy =
                    instance.GetComponent<StunnerHitPolicy>();
                AttackKey attackKey = new AttackKey(
                    new SpawnId(1),
                    new AttackSequenceId(1));
                AttackExecutionContext context = new AttackExecutionContext(
                    instance.GetComponent<UnitController>(),
                    null,
                    LoadDefinition("EnemyStunner")
                        .DefaultAttackDefinition,
                    attackKey,
                    new AttackHitLedger());
                policy.HandleSuccessfulInteraction(
                    context,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.InvalidFaction,
                        attackKey,
                        new SpawnId(2)));
                policy.HandleSuccessfulInteraction(
                    context,
                    InteractionResult.CreateRejected(
                        InteractionOutcome.OutOfRange,
                        attackKey,
                        new SpawnId(2)));
                Assert.That(policy.SuccessfulHitCount, Is.Zero);
                Assert.That(policy.ShouldStunNextSuccessfulHit, Is.True);

                policy.HandleSuccessfulInteraction(
                    context,
                    CreateAppliedResult(attackKey));
                Assert.That(policy.SuccessfulHitCount, Is.EqualTo(1));
                policy.PrepareForReturn();
                Assert.That(policy.SuccessfulHitCount, Is.Zero);
                Assert.That(policy.ShouldStunNextSuccessfulHit, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CapturedAttackPayload_PreservesPolicySelectedEffect()
        {
            GameObject instance = InstantiatePrefab("EnemyStunner");
            try
            {
                UnitController source = instance.GetComponent<UnitController>();
                AttackKey attackKey = new AttackKey(
                    new SpawnId(3),
                    new AttackSequenceId(4));
                DamagePayload payload = new DamagePayload(
                    attackKey.SourceSpawnId,
                    UnitFaction.Enemy,
                    attackKey.SequenceId,
                    15f,
                    new DamageCategoryId("Direct"),
                    new StatusEffectPayload(StatusEffectType.Stun, 2f));
                AttackExecutionContext context = new AttackExecutionContext(
                    source,
                    null,
                    Vector3.zero,
                    LoadDefinition("EnemyStunner")
                        .DefaultAttackDefinition,
                    attackKey,
                    new AttackHitLedger(),
                    payload);

                DamagePayload rebuilt = AttackPayloadFactory.Create(context);
                Assert.That(rebuilt.AttackKey, Is.EqualTo(payload.AttackKey));
                Assert.That(rebuilt.StatusEffectCount, Is.EqualTo(1));
                Assert.That(rebuilt.GetStatusEffect(0).Duration,
                    Is.EqualTo(2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void MiniDivisibleFormation_CreatesThreeDistinctRadialPositions()
        {
            Vector3 center = new Vector3(2f, 3f, 4f);
            Vector3[] positions = new Vector3[3];
            MiniDivisibleSpawnFormation.FillRadialPositions(
                center,
                Vector3.forward,
                2f,
                positions);

            for (int index = 0; index < positions.Length; index++)
            {
                Vector3 offset = positions[index] - center;
                Assert.That(offset.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(offset.magnitude,
                    Is.EqualTo(2f).Within(0.0001f));
                for (int other = index + 1;
                     other < positions.Length;
                     other++)
                {
                    Assert.That(positions[index],
                        Is.Not.EqualTo(positions[other]));
                }
            }

            Vector3 first = (positions[0] - center).normalized;
            Vector3 second = (positions[1] - center).normalized;
            Vector3 third = (positions[2] - center).normalized;
            Assert.That(Vector3.Dot(first, second),
                Is.EqualTo(-0.5f).Within(0.0001f));
            Assert.That(Vector3.Dot(second, third),
                Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [Test]
        public void DeathSpawnResult_SeparatesPositionAndOtherFailures()
        {
            DeathSpawnCompletedEvent result =
                new DeathSpawnCompletedEvent(
                    new SpawnId(10),
                    1,
                    1,
                    1);

            Assert.That(result.SourceSpawnId,
                Is.EqualTo(new SpawnId(10)));
            Assert.That(result.SpawnedCount, Is.EqualTo(1));
            Assert.That(result.FailedPositionCount, Is.EqualTo(1));
            Assert.That(result.OtherFailedCount, Is.EqualTo(1));
            Assert.That(result.FailedCount, Is.EqualTo(2));
        }

        [Test]
        public void SpecialDefinitionsAndAttacks_MatchAuthoritativeTable()
        {
            AssertSpecialDefinition(
                "EnemyStunner", 120f, 2.8f, 420f, 12f,
                15f, 2f, 1.5f, 0.45f, 0.35f);
            AssertSpecialDefinition(
                "EnemyMiniDivisible", 30f, 4f, 600f, 10f,
                6f, 1.4f, 0.8f, 0.2f, 0.2f);
            AssertSpecialDefinition(
                "EnemyDivisible", 100f, 2.7f, 420f, 12f,
                12f, 1.9f, 1.2f, 0.3f, 0.25f);

            AttackDefinition stunner = LoadDefinition("EnemyStunner")
                .DefaultAttackDefinition;
            Assert.That(stunner.AcceptedHitEffect.EffectType,
                Is.EqualTo(StatusEffectType.Stun));
            Assert.That(stunner.AcceptedHitEffect.Duration, Is.EqualTo(2f));
            Assert.That(LoadDefinition("EnemyMiniDivisible")
                .DefaultAttackDefinition.AcceptedHitEffect.EffectType,
                Is.EqualTo(StatusEffectType.None));
            Assert.That(LoadDefinition("EnemyDivisible")
                .DefaultAttackDefinition.AcceptedHitEffect.EffectType,
                Is.EqualTo(StatusEffectType.None));
        }

        [Test]
        public void StunnerPrefab_HasPolicyAndNestedRightHandHammer()
        {
            GameObject prefab = LoadPrefab("EnemyStunner");
            Assert.That(prefab.GetComponent<StunnerHitPolicy>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ImmediateDeathPoolReturn>(),
                Is.Not.Null);
            Transform rightHand = FindDeepChild(
                prefab.transform,
                "RightHandSocket");
            Assert.That(rightHand, Is.Not.Null);
            Assert.That(rightHand.childCount, Is.EqualTo(1));
            GameObject hammerSource = PrefabUtility
                .GetCorrespondingObjectFromOriginalSource(
                    rightHand.GetChild(0).gameObject);
            Assert.That(
                AssetDatabase.GetAssetPath(hammerSource),
                Is.EqualTo(
                    "Assets/Prefabs/Visuals/Units/PF_Visual_StunnerHammer.prefab"));
        }

        [Test]
        public void MiniDivisible_IsIndependentEnemyWithoutDeathSpawner()
        {
            GameObject mini = LoadPrefab("EnemyMiniDivisible");
            GameObject divisible = LoadPrefab("EnemyDivisible");
            Assert.That(mini.GetComponent<SpawnUnitsOnDeath>(), Is.Null);
            Assert.That(mini.GetComponent<ImmediateDeathPoolReturn>(),
                Is.Not.Null);
            Assert.That(divisible.GetComponent<SpawnUnitsOnDeath>(),
                Is.Not.Null);
            Assert.That(divisible.GetComponent<ImmediateDeathPoolReturn>(),
                Is.Null);
            Assert.That(
                mini.transform.Find("VisualRoot").GetChild(0).localScale,
                Is.EqualTo(Vector3.one * 0.5f));
            Assert.That(mini.GetComponent<NavMeshAgent>().radius,
                Is.EqualTo(divisible.GetComponent<NavMeshAgent>().radius *
                    0.5f));
            Assert.That(
                mini.transform.Find("Hurtbox")
                    .GetComponent<SphereCollider>().radius,
                Is.EqualTo(
                    divisible.transform.Find("Hurtbox")
                        .GetComponent<SphereCollider>().radius * 0.5f));

            GameObject miniVisualSource = PrefabUtility
                .GetCorrespondingObjectFromOriginalSource(
                    mini.transform.Find("VisualRoot").GetChild(0).gameObject);
            GameObject divisibleVisualSource = PrefabUtility
                .GetCorrespondingObjectFromOriginalSource(
                    divisible.transform.Find("VisualRoot").GetChild(0)
                        .gameObject);
            Assert.That(miniVisualSource, Is.EqualTo(divisibleVisualSource));
        }

        [Test]
        public void Divisible_PointsOnlyToMiniDivisibleDefinition()
        {
            SpawnUnitsOnDeath spawnUnits = LoadPrefab("EnemyDivisible")
                .GetComponent<SpawnUnitsOnDeath>();
            Assert.That(spawnUnits, Is.Not.Null);
            Assert.That(spawnUnits.MiniDivisibleDefinition,
                Is.SameAs(LoadDefinition("EnemyMiniDivisible")));
            Assert.That(
                LoadPrefab("EnemyMiniDivisible")
                    .GetComponent<SpawnUnitsOnDeath>(),
                Is.Null);
        }

        [Test]
        public void SpecialPoolsAndCatalogs_UseExactBaselines()
        {
            PoolCatalog pools = Load<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");
            UnitCatalog units = Load<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");
            AssertSpecialRegistration(
                pools, units, "EnemyStunner", 10, 100);
            AssertSpecialRegistration(
                pools, units, "EnemyMiniDivisible", 30, 150);
            AssertSpecialRegistration(
                pools, units, "EnemyDivisible", 10, 100);
        }

        [Test]
        public void CombatSandbox_HasAllThreeSpecialDirectSpawnPaths()
        {
            const string scenePath = "Assets/Scenes/CombatSandbox.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject systems = Array.Find(
                    scene.GetRootGameObjects(),
                    root => root.name == "__Systems");
                SpecialUnitSandboxScenarioController scenario = systems
                    .transform.Find("SpecialUnitSandboxScenario")
                    .GetComponent<SpecialUnitSandboxScenarioController>();
                Assert.That(scenario.Definitions.Count, Is.EqualTo(3));
                Assert.That(scenario.Definitions[0].UnitId,
                    Is.EqualTo(new UnitId("EnemyStunner")));
                Assert.That(scenario.Definitions[1].UnitId,
                    Is.EqualTo(new UnitId("EnemyMiniDivisible")));
                Assert.That(scenario.Definitions[2].UnitId,
                    Is.EqualTo(new UnitId("EnemyDivisible")));
                Assert.That(scenario.SpawnPoints.Count, Is.EqualTo(3));
                Assert.That(scenario.SpawnManager, Is.Not.Null);
                Assert.That(scenario.InteractionSystem, Is.Not.Null);
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static InteractionResult CreateAppliedResult(
            AttackKey attackKey)
        {
            return InteractionResult.CreateApplied(
                attackKey,
                new SpawnId(2),
                DamageResult.CreateApplied(15f, false));
        }

        private static void AssertSpecialDefinition(
            string id,
            float health,
            float moveSpeed,
            float turnSpeed,
            float chaseRange,
            float damage,
            float attackRange,
            float cooldown,
            float windup,
            float recovery)
        {
            AIUnitDefinition definition = LoadDefinition(id);
            AttackDefinition attack = definition.DefaultAttackDefinition;
            Assert.That(definition.Validate().IsValid, Is.True);
            Assert.That(definition.Faction, Is.EqualTo(UnitFaction.Enemy));
            Assert.That(definition.MaximumHealth, Is.EqualTo(health));
            Assert.That(definition.MoveSpeed, Is.EqualTo(moveSpeed));
            Assert.That(definition.TurnSpeed, Is.EqualTo(turnSpeed));
            Assert.That(definition.ChaseRange, Is.EqualTo(chaseRange));
            Assert.That(attack.Damage, Is.EqualTo(damage));
            Assert.That(attack.AttackRange, Is.EqualTo(attackRange));
            Assert.That(attack.CooldownDuration, Is.EqualTo(cooldown));
            Assert.That(attack.WindupDuration, Is.EqualTo(windup));
            Assert.That(attack.RecoveryDuration, Is.EqualTo(recovery));
            Assert.That(attack.DeliveryType,
                Is.EqualTo(AttackDeliveryType.Melee));
        }

        private static void AssertSpecialRegistration(
            PoolCatalog pools,
            UnitCatalog units,
            string id,
            int prewarm,
            int retained)
        {
            AIUnitDefinition definition = LoadDefinition(id);
            Assert.That(
                pools.TryGetEntry(
                    definition.PoolId,
                    out PoolCatalogEntry poolEntry),
                Is.True);
            Assert.That(poolEntry.Prefab, Is.EqualTo(LoadPrefab(id)));
            Assert.That(poolEntry.InitialPrewarmCount, Is.EqualTo(prewarm));
            Assert.That(poolEntry.MaximumInactiveRetainedCount,
                Is.EqualTo(retained));
            Assert.That(poolEntry.CapacityPolicy,
                Is.EqualTo(PoolCapacityPolicy.Expandable));
            Assert.That(
                units.TryGetDefinition(
                    definition.UnitId,
                    out UnitDefinition catalogDefinition),
                Is.True);
            Assert.That(catalogDefinition, Is.SameAs(definition));
        }

        private static GameObject InstantiatePrefab(string id)
        {
            return UnityEngine.Object.Instantiate(LoadPrefab(id));
        }

        private static GameObject LoadPrefab(string id)
        {
            string suffix = id.StartsWith("Enemy", StringComparison.Ordinal)
                ? id.Substring("Enemy".Length)
                : id;
            return Load<GameObject>(
                $"Assets/Prefabs/Units/PF_Enemy_{suffix}.prefab");
        }

        private static AIUnitDefinition LoadDefinition(string id)
        {
            string suffix = id.StartsWith("Enemy", StringComparison.Ordinal)
                ? id.Substring("Enemy".Length)
                : id;
            return Load<AIUnitDefinition>(
                $"Assets/Data/Units/UD_Enemy_{suffix}.asset");
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform match = FindDeepChild(root.GetChild(index), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T Load<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}
