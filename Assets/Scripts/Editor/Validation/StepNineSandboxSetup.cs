using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Lifecycle;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepNineSandboxSetup
    {
        private const string k_BasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Base.prefab";
        private const string k_FixtureFolder = "Assets/Tests/Fixtures/StepNine";
        private const string k_FixturePrefabPath =
            k_FixtureFolder + "/PF_Test_StationaryUnit.prefab";
        private const string k_FixtureDefinitionPath =
            k_FixtureFolder + "/UD_Test_StationaryPlayer.asset";
        private const string k_PoolCatalogPath =
            "Assets/Data/Catalogs/PC_ProjectilePools.asset";
        private const string k_UnitCatalogPath =
            "Assets/Data/Catalogs/UC_CombatSandbox.asset";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";
        private const int k_TargetQueryCapacity = 32;
        private const float k_TargetScanInterval = 0.25f;
        private const float k_TargetInitialDelay = 0f;

        private static readonly PoolId s_stationaryPoolId =
            new PoolId("StationaryTestUnit");

        [MenuItem("Tools/Monsters vs Zombies/Step 9/Create and Verify Sandbox")]
        public static void CreateAndVerifySandbox()
        {
            EnsureFolder(k_FixtureFolder);
            GameObject basePrefab = CreateBaseUnitPrefab();
            GameObject fixturePrefab = CreateStationaryFixturePrefab(basePrefab);
            PlayerUnitDefinition fixtureDefinition =
                CreateFixtureDefinition();
            PoolCatalog poolCatalog = UpdatePoolCatalog(fixturePrefab);
            UnitCatalog unitCatalog = UpdateUnitCatalog(fixtureDefinition);
            CreateCombatSandboxScene(
                poolCatalog,
                unitCatalog,
                fixtureDefinition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyAssets(
                basePrefab,
                fixturePrefab,
                poolCatalog,
                unitCatalog,
                fixtureDefinition);
            Debug.Log(
                "[StepNineSandboxSetup] Created and verified PF_Unit_Base, stationary fixture, catalogs, CombatSandbox, and baked NavMesh.");
        }

        private static GameObject CreateBaseUnitPrefab()
        {
            GameObject root = new GameObject("PF_Unit_Base");
            root.SetActive(false);
            root.AddComponent<UnitController>();
            root.AddComponent<HealthController>();
            root.AddComponent<DamageController>();
            root.AddComponent<StatusEffectController>();
            TargetingController targetingController =
                root.AddComponent<TargetingController>();
            if (!targetingController.InitializeScanning(
                    k_TargetQueryCapacity,
                    k_TargetScanInterval,
                    k_TargetInitialDelay))
            {
                throw new InvalidOperationException(
                    "Could not configure PF_Unit_Base targeting schedule.");
            }

            root.AddComponent<AttackController>();
            root.AddComponent<UnitLifecycleController>();
            root.AddComponent<PooledEntity>();

            GameObject hurtbox = CreateChild(root.transform, "Hurtbox");
            hurtbox.layer = LayerMask.NameToLayer("UnitTarget");
            SphereCollider hurtboxCollider =
                hurtbox.AddComponent<SphereCollider>();
            hurtboxCollider.isTrigger = true;
            hurtboxCollider.radius = 0.5f;
            hurtbox.AddComponent<DamageTargetProxy>();

            CreateChild(root.transform, "VisualRoot");
            GameObject sockets = CreateChild(root.transform, "Sockets");
            CreateChild(sockets.transform, "AttackOrigin");
            CreateChild(sockets.transform, "WeaponSocket");
            CreateChild(sockets.transform, "RightHandSocket");
            CreateChild(sockets.transform, "MouthSocket");
            CreateChild(root.transform, "UIAnchor");
            GameObject debugVisuals = CreateChild(root.transform, "DebugVisuals");
            debugVisuals.tag = "EditorOnly";

            root.SetActive(true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                k_BasePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateStationaryFixturePrefab(
            GameObject basePrefab)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(basePrefab)
                as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate PF_Unit_Base for the stationary fixture.");
            }

            instance.name = "PF_Test_StationaryUnit";
            instance.SetActive(false);
            instance.AddComponent<ImmediateDeathPoolReturn>();
            Transform visualRoot = instance.transform.Find("VisualRoot");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "StationaryPlaceholder";
            visual.transform.SetParent(visualRoot, false);
            visual.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            instance.SetActive(true);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                k_FixturePrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static PlayerUnitDefinition CreateFixtureDefinition()
        {
            PlayerUnitDefinition definition =
                LoadOrCreateAsset<PlayerUnitDefinition>(k_FixtureDefinitionPath);
            definition.name = "UD_Test_StationaryPlayer";
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.UnitId),
                new UnitId("StationaryTestPlayer"));
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.DisplayName),
                "Stationary Test Player");
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.Faction),
                UnitFaction.Player);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.MaximumHealth),
                100f);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.MoveSpeed),
                6f);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.TurnSpeed),
                720f);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.PoolId),
                s_stationaryPoolId);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static PoolCatalog UpdatePoolCatalog(GameObject fixturePrefab)
        {
            PoolCatalog poolCatalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(
                k_PoolCatalogPath);
            if (poolCatalog == null)
            {
                throw new InvalidOperationException(
                    "Step 9 requires the Step 8 projectile pool catalog.");
            }

            List<PoolCatalogEntry> entries = new List<PoolCatalogEntry>();
            for (int entryIndex = 0;
                 entryIndex < poolCatalog.Count;
                 entryIndex++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(entryIndex);
                if (entry != null && entry.PoolId != s_stationaryPoolId)
                {
                    entries.Add(entry);
                }
            }

            PoolCatalogEntry fixtureEntry = new PoolCatalogEntry();
            SetAutoProperty(
                fixtureEntry,
                nameof(PoolCatalogEntry.PoolId),
                s_stationaryPoolId);
            SetAutoProperty(
                fixtureEntry,
                nameof(PoolCatalogEntry.Prefab),
                fixturePrefab);
            SetAutoProperty(
                fixtureEntry,
                nameof(PoolCatalogEntry.InitialPrewarmCount),
                1);
            SetAutoProperty(
                fixtureEntry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                1);
            SetAutoProperty(
                fixtureEntry,
                nameof(PoolCatalogEntry.CapacityPolicy),
                PoolCapacityPolicy.Expandable);
            SetAutoProperty(
                fixtureEntry,
                nameof(PoolCatalogEntry.MaximumActiveCount),
                0);
            SetAutoProperty(
                fixtureEntry,
                nameof(PoolCatalogEntry.EnableCollectionChecks),
                true);
            entries.Add(fixtureEntry);
            SetField(poolCatalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(poolCatalog);
            return poolCatalog;
        }

        private static UnitCatalog UpdateUnitCatalog(
            PlayerUnitDefinition fixtureDefinition)
        {
            UnitCatalog unitCatalog =
                LoadOrCreateAsset<UnitCatalog>(k_UnitCatalogPath);
            List<UnitCatalogEntry> entries = new List<UnitCatalogEntry>();
            for (int entryIndex = 0;
                 entryIndex < unitCatalog.Count;
                 entryIndex++)
            {
                UnitCatalogEntry entry = unitCatalog.GetEntry(entryIndex);
                if (entry != null && entry.UnitId != fixtureDefinition.UnitId)
                {
                    entries.Add(entry);
                }
            }

            UnitCatalogEntry fixtureEntry = new UnitCatalogEntry();
            SetAutoProperty(
                fixtureEntry,
                nameof(UnitCatalogEntry.Definition),
                fixtureDefinition);
            entries.Add(fixtureEntry);
            SetField(unitCatalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(unitCatalog);
            return unitCatalog;
        }

        private static void CreateCombatSandboxScene(
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog,
            PlayerUnitDefinition fixtureDefinition)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "CombatSandbox";

            GameObject systemsRoot = new GameObject("__Systems");
            GameObject environmentRoot = new GameObject("Environment");
            GameObject spawnPointsRoot = new GameObject("SpawnPoints");
            GameObject cameraRigRoot = new GameObject("CameraRig");
            new GameObject("UI");
            GameObject lightingRoot = new GameObject("Lighting");

            PoolManager poolManager = CreateNamedComponent<PoolManager>(
                systemsRoot.transform,
                "PoolManager");
            UnitRegistry unitRegistry = CreateNamedComponent<UnitRegistry>(
                systemsRoot.transform,
                "UnitRegistry");
            InteractionSystem interactionSystem =
                CreateNamedComponent<InteractionSystem>(
                    systemsRoot.transform,
                    "InteractionSystem");
            SpawnManager spawnManager = CreateNamedComponent<SpawnManager>(
                systemsRoot.transform,
                "SpawnManager");
            InitialSandboxSpawner initialSpawner =
                CreateNamedComponent<InitialSandboxSpawner>(
                    systemsRoot.transform,
                    "InitialSandboxSpawner");
            SetAutoProperty(
                initialSpawner,
                nameof(InitialSandboxSpawner.SpawnManager),
                spawnManager);
            DebugUnitSpawner debugSpawner =
                CreateNamedComponent<DebugUnitSpawner>(
                    systemsRoot.transform,
                    "DebugUnitSpawner");
            SetAutoProperty(
                debugSpawner,
                nameof(DebugUnitSpawner.SpawnManager),
                spawnManager);

            SpawnPointGroup playerSpawnPoints = CreateSpawnGroup(
                spawnPointsRoot.transform,
                "PlayerSpawn",
                new[] { Vector3.zero });
            SpawnPointGroup allySpawnPoints = CreateSpawnGroup(
                spawnPointsRoot.transform,
                "AllySpawnPoints",
                new[]
                {
                    new Vector3(-6f, 0f, 3f),
                    new Vector3(-6f, 0f, -3f)
                });
            SpawnPointGroup enemySpawnPoints = CreateSpawnGroup(
                spawnPointsRoot.transform,
                "EnemySpawnPoints",
                new[]
                {
                    new Vector3(6f, 0f, 3f),
                    new Vector3(6f, 0f, -3f)
                });

            CombatSandboxBootstrap bootstrap =
                CreateNamedComponent<CombatSandboxBootstrap>(
                    systemsRoot.transform,
                    "CombatSandboxBootstrap");
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.PoolCatalog),
                poolCatalog);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.UnitCatalog),
                unitCatalog);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.PlayerDefinition),
                fixtureDefinition);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.PoolManager),
                poolManager);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.SpawnManager),
                spawnManager);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.InteractionSystem),
                interactionSystem);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.UnitRegistry),
                unitRegistry);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.InitialSandboxSpawner),
                initialSpawner);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.PlayerSpawnPoints),
                playerSpawnPoints);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.AllySpawnPoints),
                allySpawnPoints);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.EnemySpawnPoints),
                enemySpawnPoints);

            CreateEnvironment(environmentRoot.transform);
            CreateCamera(cameraRigRoot.transform);
            CreateLighting(lightingRoot.transform);

            NavMeshSurface navMeshSurface = environmentRoot
                .GetComponentInChildren<NavMeshSurface>();
            navMeshSurface.BuildNavMesh();
            if (navMeshSurface.navMeshData == null)
            {
                throw new InvalidOperationException(
                    "CombatSandbox NavMesh bake did not produce data.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, k_ScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save CombatSandbox scene.");
            }
        }

        private static void CreateEnvironment(Transform environmentRoot)
        {
            int worldLayer = LayerMask.NameToLayer("World");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.layer = worldLayer;
            ground.transform.SetParent(environmentRoot, false);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);

            GameObject obstacles = CreateChild(environmentRoot, "Obstacles");
            Vector3[] obstaclePositions =
            {
                new Vector3(-4f, 1f, 0f),
                new Vector3(4f, 1f, 0f),
                new Vector3(0f, 1f, 5f)
            };
            for (int obstacleIndex = 0;
                 obstacleIndex < obstaclePositions.Length;
                 obstacleIndex++)
            {
                GameObject obstacle =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = $"Obstacle_{obstacleIndex + 1}";
                obstacle.layer = worldLayer;
                obstacle.transform.SetParent(obstacles.transform, false);
                obstacle.transform.position = obstaclePositions[obstacleIndex];
                obstacle.transform.localScale = new Vector3(2f, 2f, 2f);
            }

            GameObject navMeshObject =
                CreateChild(environmentRoot, "NavMeshSurface");
            NavMeshSurface navMeshSurface =
                navMeshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.All;
            navMeshSurface.layerMask = 1 << worldLayer;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        }

        private static void CreateCamera(Transform cameraRigRoot)
        {
            GameObject cameraObject = CreateChild(cameraRigRoot, "MainCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 15f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        private static void CreateLighting(Transform lightingRoot)
        {
            GameObject lightObject = CreateChild(
                lightingRoot,
                "Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static SpawnPointGroup CreateSpawnGroup(
            Transform parent,
            string groupName,
            Vector3[] positions)
        {
            GameObject groupObject = CreateChild(parent, groupName);
            SpawnPointGroup spawnPointGroup =
                groupObject.AddComponent<SpawnPointGroup>();
            Transform[] spawnPoints = new Transform[positions.Length];
            for (int pointIndex = 0;
                 pointIndex < positions.Length;
                 pointIndex++)
            {
                GameObject point = CreateChild(
                    groupObject.transform,
                    $"Point_{pointIndex + 1}");
                point.transform.position = positions[pointIndex];
                spawnPoints[pointIndex] = point.transform;
            }

            SetField(spawnPointGroup, "_spawnPoints", spawnPoints);
            return spawnPointGroup;
        }

        private static void VerifyAssets(
            GameObject basePrefab,
            GameObject fixturePrefab,
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog,
            PlayerUnitDefinition fixtureDefinition)
        {
            if (basePrefab == null || fixturePrefab == null ||
                PrefabUtility.GetPrefabAssetType(fixturePrefab) !=
                    PrefabAssetType.Variant)
            {
                throw new InvalidOperationException(
                    "PF_Unit_Base or its stationary test variant is invalid.");
            }

            UnitController unitController =
                basePrefab.GetComponent<UnitController>();
            DamageTargetProxy damageTargetProxy =
                basePrefab.GetComponentInChildren<DamageTargetProxy>(true);
            string failure = string.Empty;
            if (unitController == null ||
                !unitController.ValidateGameplayComponents(out failure) ||
                damageTargetProxy == null ||
                !damageTargetProxy.ValidateReferences(out failure))
            {
                throw new InvalidOperationException(
                    $"PF_Unit_Base validation failed: {failure}");
            }

            if (!poolCatalog.TryGetEntry(s_stationaryPoolId, out _) ||
                !unitCatalog.TryGetDefinition(
                    fixtureDefinition.UnitId,
                    out UnitDefinition catalogDefinition) ||
                catalogDefinition != fixtureDefinition)
            {
                throw new InvalidOperationException(
                    "Stationary fixture catalog entries are invalid.");
            }

            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "CombatSandbox scene is not loaded after creation.");
            }
        }

        private static T CreateNamedComponent<T>(
            Transform parent,
            string objectName) where T : Component
        {
            GameObject gameObject = CreateChild(parent, objectName);
            return gameObject.AddComponent<T>();
        }

        private static GameObject CreateChild(
            Transform parent,
            string objectName)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentFolder = System.IO.Path.GetDirectoryName(folderPath)
                .Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(folderPath);
            EnsureFolder(parentFolder);
            AssetDatabase.CreateFolder(parentFolder, folderName);
        }

        private static T LoadOrCreateAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            Type currentType = target.GetType();
            while (currentType != null)
            {
                FieldInfo field = currentType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new MissingFieldException(
                target.GetType().FullName,
                fieldName);
        }
    }
}
