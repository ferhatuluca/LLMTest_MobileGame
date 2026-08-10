using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Lifecycle;
using MonstersVsZombies.Units.Movement;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepNinePrefabAndSceneTests
    {
        private const string k_BasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Base.prefab";
        private const string k_FixturePrefabPath =
            "Assets/Tests/Fixtures/StepNine/PF_Test_StationaryUnit.prefab";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";

        [Test]
        public void BasePrefab_HasCompleteCommonCapabilityMatrix()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BasePrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<UnitController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Combat.Health.HealthController>(),
                Is.Not.Null);
            Assert.That(prefab.GetComponent<DamageController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Combat.StatusEffects.StatusEffectController>(),
                Is.Not.Null);
            Assert.That(prefab.GetComponent<TargetingController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<AttackController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UnitLifecycleController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PooledEntity>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<AttackController>().AttackDefinition,
                Is.Null);

            UnitController unitController = prefab.GetComponent<UnitController>();
            Assert.That(
                unitController.ValidateGameplayComponents(out string failure),
                Is.True,
                failure);
            Assert.That(unitController.UnitMotor, Is.Null);
            foreach (MonoBehaviour behaviour in prefab.GetComponents<MonoBehaviour>())
            {
                Assert.That(behaviour is IAttackExecutor, Is.False);
                Assert.That(behaviour is IUnitMotor, Is.False);
            }
        }

        [Test]
        public void BasePrefab_HasRequiredHierarchyHurtboxAndSockets()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BasePrefabPath);
            string[] requiredPaths =
            {
                "Hurtbox",
                "VisualRoot",
                "Sockets",
                "Sockets/AttackOrigin",
                "Sockets/WeaponSocket",
                "Sockets/RightHandSocket",
                "Sockets/MouthSocket",
                "UIAnchor",
                "DebugVisuals"
            };
            foreach (string requiredPath in requiredPaths)
            {
                Assert.That(prefab.transform.Find(requiredPath),
                    Is.Not.Null,
                    requiredPath);
            }

            Transform hurtbox = prefab.transform.Find("Hurtbox");
            Collider collider = hurtbox.GetComponent<Collider>();
            DamageTargetProxy proxy = hurtbox.GetComponent<DamageTargetProxy>();
            Assert.That(hurtbox.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("UnitTarget")));
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
            Assert.That(proxy.ValidateReferences(out string failure),
                Is.True,
                failure);
            Assert.That(prefab.transform.Find("DebugVisuals").tag,
                Is.EqualTo("EditorOnly"));
        }

        [Test]
        public void BasePrefab_TargetingSchedulePersistsExplicitConfiguration()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BasePrefabPath);
            TargetingController targeting =
                prefab.GetComponent<TargetingController>();

            Assert.That(targeting.QueryCapacity, Is.EqualTo(256));
            Assert.That(targeting.ScanInterval, Is.EqualTo(0.25f));
            Assert.That(targeting.InitialScanDelay, Is.Zero);
            Assert.That(targeting.ValidateConfiguration(out string failure),
                Is.True,
                failure);
        }

        [Test]
        public void StationaryFixture_IsTestOnlyPrefabVariantAndNonAttacking()
        {
            GameObject fixture = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_FixturePrefabPath);

            Assert.That(fixture, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetType(fixture),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(fixture.GetComponent<ImmediateDeathPoolReturn>(),
                Is.Not.Null);
            Assert.That(fixture.GetComponent<AttackController>().AttackDefinition,
                Is.Null);
            Assert.That(fixture.GetComponent<UnitController>().UnitMotor, Is.Null);
        }

        [Test]
        public void FixtureCatalogEntries_UseExplicitSinglePlayerBaseline()
        {
            PoolCatalog poolCatalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");
            UnitCatalog unitCatalog = AssetDatabase.LoadAssetAtPath<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");
            PlayerUnitDefinition definition =
                AssetDatabase.LoadAssetAtPath<PlayerUnitDefinition>(
                    "Assets/Tests/Fixtures/StepNine/UD_Test_StationaryPlayer.asset");

            Assert.That(definition.Validate().IsValid, Is.True);
            Assert.That(poolCatalog.TryGetEntry(
                new PoolId("StationaryTestUnit"),
                out PoolCatalogEntry poolEntry), Is.True);
            Assert.That(poolEntry.InitialPrewarmCount, Is.EqualTo(1));
            Assert.That(poolEntry.MaximumInactiveRetainedCount, Is.EqualTo(1));
            Assert.That(poolEntry.CapacityPolicy,
                Is.EqualTo(PoolCapacityPolicy.Expandable));
            Assert.That(unitCatalog.TryGetDefinition(
                definition.UnitId,
                out UnitDefinition catalogDefinition), Is.True);
            Assert.That(catalogDefinition, Is.SameAs(definition));
        }

        [Test]
        public void CombatSandbox_HasRequiredRootsServicesWorldAndBakedNavMesh()
        {
            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(
                    k_ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                string[] requiredRoots =
                {
                    "__Systems",
                    "Environment",
                    "SpawnPoints",
                    "CameraRig",
                    "UI",
                    "Lighting"
                };
                foreach (string rootName in requiredRoots)
                {
                    Assert.That(FindRoot(scene, rootName), Is.Not.Null, rootName);
                }

                GameObject systems = FindRoot(scene, "__Systems");
                CombatSandboxBootstrap bootstrap = systems
                    .GetComponentInChildren<CombatSandboxBootstrap>(true);
                Assert.That(bootstrap, Is.Not.Null);
                Assert.That(bootstrap.PoolManager, Is.Not.Null);
                Assert.That(bootstrap.SpawnManager, Is.Not.Null);
                Assert.That(bootstrap.InteractionSystem, Is.Not.Null);
                Assert.That(bootstrap.UnitRegistry, Is.Not.Null);
                Assert.That(bootstrap.InitialSandboxSpawner, Is.Not.Null);
                Assert.That(bootstrap.PlayerSpawnPoints.Count, Is.GreaterThan(0));
                Assert.That(bootstrap.AllySpawnPoints.Count, Is.GreaterThan(0));
                Assert.That(bootstrap.EnemySpawnPoints.Count, Is.GreaterThan(0));

                GameObject environment = FindRoot(scene, "Environment");
                NavMeshSurface navMeshSurface = environment
                    .GetComponentInChildren<NavMeshSurface>(true);
                Assert.That(navMeshSurface, Is.Not.Null);
                Assert.That(navMeshSurface.navMeshData, Is.Not.Null);
                Transform ground = environment.transform.Find("Ground");
                Assert.That(ground, Is.Not.Null);
                Assert.That(ground.gameObject.layer,
                    Is.EqualTo(LayerMask.NameToLayer("World")));
                Transform obstacles = environment.transform.Find("Obstacles");
                Assert.That(obstacles.childCount, Is.GreaterThan(0));
                for (int childIndex = 0;
                     childIndex < obstacles.childCount;
                     childIndex++)
                {
                    Assert.That(obstacles.GetChild(childIndex).gameObject.layer,
                        Is.EqualTo(LayerMask.NameToLayer("World")));
                }
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root;
                }
            }

            return null;
        }
    }

    public sealed class StepNineProductionLifecycleTests
    {
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
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

            _createdObjects.Clear();
        }

        [Test]
        public void ProductionServices_SpawnDamageReturnAndRespawnFixtureCleanly()
        {
            PoolCatalog poolCatalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");
            UnitCatalog unitCatalog = AssetDatabase.LoadAssetAtPath<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");
            PlayerUnitDefinition definition =
                AssetDatabase.LoadAssetAtPath<PlayerUnitDefinition>(
                    "Assets/Tests/Fixtures/StepNine/UD_Test_StationaryPlayer.asset");
            PoolManager poolManager = CreateComponent<PoolManager>("PoolManager");
            UnitRegistry unitRegistry =
                CreateComponent<UnitRegistry>("UnitRegistry");
            InteractionSystem interactionSystem =
                CreateComponent<InteractionSystem>("InteractionSystem");
            SpawnManager spawnManager =
                CreateComponent<SpawnManager>("SpawnManager");
            InitialSandboxSpawner initialSpawner =
                CreateComponent<InitialSandboxSpawner>("InitialSpawner");
            SpawnPointGroup playerSpawns = CreateSpawnGroup("PlayerSpawns");
            SpawnPointGroup allySpawns = CreateSpawnGroup("AllySpawns");
            SpawnPointGroup enemySpawns = CreateSpawnGroup("EnemySpawns");
            Assert.That(poolManager.Initialize(poolCatalog, out string poolFailure),
                Is.True,
                poolFailure);
            Assert.That(
                spawnManager.Initialize(
                    poolManager,
                    unitRegistry,
                    out string spawnFailure),
                Is.True,
                spawnFailure);
            Assert.That(initialSpawner.Configure(spawnManager), Is.True);
            Assert.That(poolManager.IsInitialized, Is.True);
            Assert.That(spawnManager.IsInitialized, Is.True);
            Assert.That(playerSpawns.TryGetNext(out Pose firstPose), Is.True);
            SpawnResult<UnitController> firstSpawn =
                initialSpawner.Spawn(definition, firstPose);
            Assert.That(firstSpawn.IsSuccess,
                Is.True,
                firstSpawn.FailureReason.ToString());
            UnitController firstUnit = firstSpawn.Entity;
            SpawnId firstSpawnId = firstUnit.SpawnId;
            Assert.That(firstUnit.IsActive, Is.True);
            Assert.That(firstUnit.HealthController.CurrentHealth, Is.EqualTo(100f));
            Assert.That(firstUnit.AttackController.AttackDefinition, Is.Null);
            Assert.That(unitRegistry.Count, Is.EqualTo(1));

            AttackKey attackKey = new AttackKey(
                new SpawnId(900),
                new AttackSequenceId(1));
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(attackKey);
            DamagePayload payload = new DamagePayload(
                attackKey.SourceSpawnId,
                UnitFaction.Enemy,
                attackKey.SequenceId,
                100f,
                new DamageCategoryId("StepNineLifecycle"));
            InteractionResult lethalResult = interactionSystem.ResolveHit(
                new HitContext(
                    payload,
                    firstUnit.DamageController,
                    firstUnit.transform.position,
                    Vector3.up,
                    HitType.Direct,
                    "StepNineLifecycle"),
                ledger);

            Assert.That(lethalResult.IsApplied, Is.True);
            Assert.That(lethalResult.DamageResult.TargetDied, Is.True);
            Assert.That(firstUnit.gameObject.activeSelf, Is.False);
            Assert.That(unitRegistry.Count, Is.Zero);

            Assert.That(playerSpawns.TryGetNext(out Pose secondPose), Is.True);
            SpawnResult<UnitController> secondSpawn =
                initialSpawner.Spawn(definition, secondPose);
            Assert.That(secondSpawn.IsSuccess,
                Is.True,
                secondSpawn.FailureReason.ToString());
            UnitController secondUnit = secondSpawn.Entity;
            Assert.That(secondUnit, Is.SameAs(firstUnit));
            Assert.That(secondUnit.SpawnId, Is.Not.EqualTo(firstSpawnId));
            Assert.That(secondUnit.IsActive, Is.True);
            Assert.That(secondUnit.HealthController.CurrentHealth, Is.EqualTo(100f));
            Assert.That(secondUnit.HealthController.IsAlive, Is.True);
            Assert.That(secondUnit.StatusEffectController.IsStunned, Is.False);
            Assert.That(secondUnit.TargetingController.CurrentTarget, Is.Null);
            Assert.That(secondUnit.AttackController.State,
                Is.EqualTo(AttackTimingState.Idle));
            Assert.That(unitRegistry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Bootstrap_MissingReferenceFailsAsOneDisabledSandbox()
        {
            CombatSandboxBootstrap bootstrap =
                CreateComponent<CombatSandboxBootstrap>("InvalidBootstrap");

            Assert.That(bootstrap.InitializeServices(), Is.False);
            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.IsGameplayEnabled, Is.False);
            Assert.That(bootstrap.LastFailureMessage, Is.Not.Empty);
        }

        private T CreateComponent<T>(string objectName) where T : Component
        {
            GameObject gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private SpawnPointGroup CreateSpawnGroup(string objectName)
        {
            SpawnPointGroup spawnPointGroup =
                CreateComponent<SpawnPointGroup>(objectName);
            GameObject point = new GameObject($"{objectName}Point");
            _createdObjects.Add(point);
            point.transform.SetParent(spawnPointGroup.transform, false);
            spawnPointGroup.Configure(new[] { point.transform });
            return spawnPointGroup;
        }
    }
}
