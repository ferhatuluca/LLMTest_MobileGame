using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using MonstersVsZombies.Units.Lifecycle;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepTwelveRegularUnitFeatureTests
    {
        private const string k_UnitFolder = "Assets/Data/Units";
        private const string k_AttackFolder = "Assets/Data/Attacks";
        private const string k_PrefabFolder = "Assets/Prefabs/Units";

        private static readonly ExpectedUnit[] s_ExpectedUnits =
        {
            new ExpectedUnit(
                "AllyClassicMelee", "Ally_ClassicMelee",
                UnitFaction.Ally, 60f, 3.5f, 540f, 12f,
                "BasicMelee", AttackDeliveryType.Melee,
                string.Empty, "ClassicMelee"),
            new ExpectedUnit(
                "EnemyClassicMelee", "Enemy_ClassicMelee",
                UnitFaction.Enemy, 60f, 3.5f, 540f, 12f,
                "BasicMelee", AttackDeliveryType.Melee,
                string.Empty, "ClassicMelee"),
            new ExpectedUnit(
                "AllyClassicRange", "Ally_ClassicRange",
                UnitFaction.Ally, 50f, 3.2f, 540f, 14f,
                "BasicBullet", AttackDeliveryType.Projectile,
                "MuzzleSocket", "ClassicRange"),
            new ExpectedUnit(
                "EnemyClassicRange", "Enemy_ClassicRange",
                UnitFaction.Enemy, 50f, 3.2f, 540f, 14f,
                "BasicBullet", AttackDeliveryType.Projectile,
                "MuzzleSocket", "ClassicRange"),
            new ExpectedUnit(
                "AllyDragon", "Ally_Dragon",
                UnitFaction.Ally, 80f, 3f, 480f, 16f,
                "DragonFireball", AttackDeliveryType.Projectile,
                "MouthSocket", "Dragon"),
            new ExpectedUnit(
                "EnemyDragon", "Enemy_Dragon",
                UnitFaction.Enemy, 80f, 3f, 480f, 16f,
                "DragonFireball", AttackDeliveryType.Projectile,
                "MouthSocket", "Dragon"),
            new ExpectedUnit(
                "AllyDoubleHead", "Ally_DoubleHead",
                UnitFaction.Ally, 110f, 3.2f, 480f, 12f,
                "DoubleHeadMelee", AttackDeliveryType.Melee,
                "WristAttackSocket", "DoubleHead")
        };

        [Test]
        public void RegularDefinitions_MatchAuthoritativeSandboxTable()
        {
            foreach (ExpectedUnit expected in s_ExpectedUnits)
            {
                AIUnitDefinition definition = LoadDefinition(expected);
                Assert.That(definition.Validate().IsValid, Is.True);
                Assert.That(definition.UnitId.Value, Is.EqualTo(expected.Id));
                Assert.That(definition.Faction, Is.EqualTo(expected.Faction));
                Assert.That(definition.MaximumHealth,
                    Is.EqualTo(expected.Health));
                Assert.That(definition.MoveSpeed,
                    Is.EqualTo(expected.MoveSpeed));
                Assert.That(definition.TurnSpeed,
                    Is.EqualTo(expected.TurnSpeed));
                Assert.That(definition.ChaseRange,
                    Is.EqualTo(expected.ChaseRange));
                Assert.That(
                    definition.DefaultAttackDefinition.AttackId.Value,
                    Is.EqualTo(expected.AttackId));
                Assert.That(
                    definition.DefaultAttackDefinition.DeliveryType,
                    Is.EqualTo(expected.DeliveryType));
            }
        }

        [Test]
        public void DoubleHeadAttack_MatchesAuthoritativeSandboxTable()
        {
            AttackDefinition attack = Load<AttackDefinition>(
                $"{k_AttackFolder}/AD_DoubleHeadMelee.asset");
            Assert.That(attack.Validate().IsValid, Is.True);
            Assert.That(attack.Damage, Is.EqualTo(18f));
            Assert.That(attack.AttackRange, Is.EqualTo(2f));
            Assert.That(attack.CooldownDuration, Is.EqualTo(1.3f));
            Assert.That(attack.WindupDuration, Is.EqualTo(0.35f));
            Assert.That(attack.RecoveryDuration, Is.EqualTo(0.3f));
            Assert.That(attack.DeliveryType,
                Is.EqualTo(AttackDeliveryType.Melee));
        }

        [Test]
        public void ClassicMeleeFamily_UsesFactionBasesAndMeleeDelivery()
        {
            AssertFamilyMember(s_ExpectedUnits[0]);
            AssertFamilyMember(s_ExpectedUnits[1]);
            Assert.That(
                FactionRules.AreHostile(
                    UnitFaction.Ally,
                    UnitFaction.Ally),
                Is.False);
            Assert.That(
                FactionRules.AreHostile(
                    UnitFaction.Enemy,
                    UnitFaction.Enemy),
                Is.False);
            Assert.That(
                FactionRules.AreHostile(
                    UnitFaction.Ally,
                    UnitFaction.Enemy),
                Is.True);
        }

        [Test]
        public void ClassicRangeFamily_UsesBulletAndMuzzleSocket()
        {
            AssertFamilyMember(s_ExpectedUnits[2]);
            AssertFamilyMember(s_ExpectedUnits[3]);
            Assert.That(
                LoadDefinition(s_ExpectedUnits[2])
                    .DefaultAttackDefinition.ProjectileDefinition,
                Is.EqualTo(Load<ProjectileDefinition>(
                    "Assets/Data/Projectiles/PD_Bullet.asset")));
        }

        [Test]
        public void DragonFamily_ReusesVisualAndFiresFromMouthSocket()
        {
            AssertFamilyMember(s_ExpectedUnits[4]);
            AssertFamilyMember(s_ExpectedUnits[5]);
            GameObject ally = LoadPrefab(s_ExpectedUnits[4]);
            GameObject enemy = LoadPrefab(s_ExpectedUnits[5]);
            Assert.That(
                GetNestedVisualAssetPath(ally),
                Is.EqualTo(GetNestedVisualAssetPath(enemy)));
            Assert.That(
                GetNestedVisualAssetPath(ally),
                Is.EqualTo(
                    "Assets/Prefabs/Visuals/Units/PF_Visual_Dragon.prefab"));
            Assert.That(
                LoadDefinition(s_ExpectedUnits[4])
                    .DefaultAttackDefinition.ProjectileDefinition,
                Is.EqualTo(Load<ProjectileDefinition>(
                    "Assets/Data/Projectiles/PD_Fireball.asset")));
        }

        [Test]
        public void DoubleHead_IsAllyMeleeWithTwoHeadsAndWristSocket()
        {
            ExpectedUnit expected = s_ExpectedUnits[6];
            AssertFamilyMember(expected);
            GameObject prefab = LoadPrefab(expected);
            Assert.That(FindDeepChild(prefab.transform, "LeftHead"),
                Is.Not.Null);
            Assert.That(FindDeepChild(prefab.transform, "RightHead"),
                Is.Not.Null);
            Assert.That(FindDeepChild(prefab.transform, "WristAttackSocket"),
                Is.Not.Null);
            Assert.That(prefab.GetComponent<UnitController>()
                .Definition.Faction, Is.EqualTo(UnitFaction.Ally));
        }

        [Test]
        public void RegularUnits_AreCatalogedWithExactPoolBaselines()
        {
            PoolCatalog pools = Load<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");
            UnitCatalog units = Load<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");
            Assert.That(pools.Validate().IsValid, Is.True);
            Assert.That(units.Validate().IsValid, Is.True);
            foreach (ExpectedUnit expected in s_ExpectedUnits)
            {
                AIUnitDefinition definition = LoadDefinition(expected);
                Assert.That(
                    pools.TryGetEntry(
                        definition.PoolId,
                        out PoolCatalogEntry entry),
                    Is.True);
                Assert.That(entry.Prefab, Is.EqualTo(LoadPrefab(expected)));
                Assert.That(entry.InitialPrewarmCount, Is.EqualTo(10));
                Assert.That(entry.MaximumInactiveRetainedCount,
                    Is.EqualTo(100));
                Assert.That(entry.CapacityPolicy,
                    Is.EqualTo(PoolCapacityPolicy.Expandable));
                Assert.That(entry.MaximumActiveCount, Is.Zero);
                Assert.That(
                    units.TryGetDefinition(
                        definition.UnitId,
                        out UnitDefinition catalogDefinition),
                    Is.True);
                Assert.That(catalogDefinition, Is.SameAs(definition));
            }
        }

        [Test]
        public void RegularAIBrains_BindMeleeAndProjectileRuntimeServices()
        {
            GameObject spawnServices = new GameObject("SpawnServices");
            SpawnManager spawnManager =
                spawnServices.AddComponent<SpawnManager>();
            GameObject interactionServices = new GameObject(
                "InteractionServices");
            InteractionSystem interactionSystem =
                interactionServices.AddComponent<InteractionSystem>();
            List<GameObject> instances = new List<GameObject>();
            try
            {
                foreach (ExpectedUnit expected in s_ExpectedUnits)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(
                        LoadPrefab(expected));
                    instances.Add(instance);
                    AIUnitBrain brain = instance.GetComponent<AIUnitBrain>();
                    bool didConfigure =
                        expected.DeliveryType == AttackDeliveryType.Melee
                            ? brain.ConfigureRuntimeServices(
                                interactionSystem)
                            : brain.ConfigureRuntimeServices(
                                spawnManager,
                                interactionSystem);
                    Assert.That(didConfigure, Is.True, expected.Id);
                    Assert.That(brain.HasRuntimeServices,
                        Is.True, expected.Id);
                }
            }
            finally
            {
                foreach (GameObject instance in instances)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                UnityEngine.Object.DestroyImmediate(spawnServices);
                UnityEngine.Object.DestroyImmediate(interactionServices);
            }
        }

        [Test]
        public void CombatSandbox_HasDirectSpawnPathForEveryRegularUnit()
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
                RegularUnitSandboxScenarioController scenario = systems
                    .transform.Find("RegularUnitSandboxScenario")
                    .GetComponent<RegularUnitSandboxScenarioController>();
                Assert.That(scenario.AllyDefinitions.Count, Is.EqualTo(4));
                Assert.That(scenario.EnemyDefinitions.Count, Is.EqualTo(3));
                Assert.That(scenario.AllySpawnPoints.Count, Is.EqualTo(4));
                Assert.That(scenario.EnemySpawnPoints.Count, Is.EqualTo(3));
                Assert.That(scenario.Bootstrap, Is.Not.Null);
                Assert.That(scenario.InitialSandboxSpawner, Is.Not.Null);
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

        private static void AssertFamilyMember(ExpectedUnit expected)
        {
            GameObject prefab = LoadPrefab(expected);
            AIUnitDefinition definition = LoadDefinition(expected);
            Assert.That(PrefabUtility.GetPrefabAssetType(prefab),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(prefab.GetComponent<UnitController>().Definition,
                Is.SameAs(definition));
            Assert.That(prefab.GetComponent<AIUnitBrain>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ImmediateDeathPoolReturn>(),
                Is.Not.Null);
            Assert.That(prefab.GetComponents<MeleeAttackExecutor>().Length,
                Is.EqualTo(
                    expected.DeliveryType == AttackDeliveryType.Melee
                        ? 1
                        : 0));
            Assert.That(
                prefab.GetComponents<ProjectileAttackExecutor>().Length,
                Is.EqualTo(
                    expected.DeliveryType == AttackDeliveryType.Projectile
                        ? 1
                        : 0));
            Assert.That(
                GetNestedVisualAssetPath(prefab),
                Is.EqualTo(
                    $"Assets/Prefabs/Visuals/Units/PF_Visual_{expected.VisualName}.prefab"));
            if (!string.IsNullOrEmpty(expected.SocketName))
            {
                Assert.That(
                    FindDeepChild(prefab.transform, expected.SocketName),
                    Is.Not.Null);
            }

            if (expected.DeliveryType == AttackDeliveryType.Projectile)
            {
                Assert.That(
                    prefab.GetComponent<ProjectileAttackExecutor>()
                        .AttackOrigin.name,
                    Is.EqualTo(expected.SocketName));
            }
        }

        private static string GetNestedVisualAssetPath(GameObject prefab)
        {
            Transform visualRoot = prefab.transform.Find("VisualRoot");
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(visualRoot.childCount, Is.EqualTo(1));
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(
                visualRoot.GetChild(0).gameObject);
            Assert.That(source, Is.Not.Null);
            return AssetDatabase.GetAssetPath(source);
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

        private static AIUnitDefinition LoadDefinition(ExpectedUnit expected)
        {
            return Load<AIUnitDefinition>(
                $"{k_UnitFolder}/UD_{expected.FileStem}.asset");
        }

        private static GameObject LoadPrefab(ExpectedUnit expected)
        {
            return Load<GameObject>(
                $"{k_PrefabFolder}/PF_{expected.FileStem}.prefab");
        }

        private static T Load<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private readonly struct ExpectedUnit
        {
            public string Id { get; }
            public string FileStem { get; }
            public UnitFaction Faction { get; }
            public float Health { get; }
            public float MoveSpeed { get; }
            public float TurnSpeed { get; }
            public float ChaseRange { get; }
            public string AttackId { get; }
            public AttackDeliveryType DeliveryType { get; }
            public string SocketName { get; }
            public string VisualName { get; }

            public ExpectedUnit(
                string id,
                string fileStem,
                UnitFaction faction,
                float health,
                float moveSpeed,
                float turnSpeed,
                float chaseRange,
                string attackId,
                AttackDeliveryType deliveryType,
                string socketName,
                string visualName)
            {
                Id = id;
                FileStem = fileStem;
                Faction = faction;
                Health = health;
                MoveSpeed = moveSpeed;
                TurnSpeed = turnSpeed;
                ChaseRange = chaseRange;
                AttackId = attackId;
                DeliveryType = deliveryType;
                SocketName = socketName;
                VisualName = visualName;
            }
        }
    }
}
