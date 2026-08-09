using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using MonstersVsZombies.Units.Lifecycle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepElevenAISetup
    {
        private const string k_BaseUnitPrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Base.prefab";
        private const string k_AIBasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_AI_Base.prefab";
        private const string k_AllyBasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Ally_Base.prefab";
        private const string k_EnemyBasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Enemy_Base.prefab";
        private const string k_TestFolder =
            "Assets/Tests/Fixtures/StepEleven";
        private const string k_TestAllyPrefabPath =
            "Assets/Tests/Fixtures/StepEleven/PF_Test_AI_Ally.prefab";
        private const string k_TestEnemyPrefabPath =
            "Assets/Tests/Fixtures/StepEleven/PF_Test_AI_Enemy.prefab";
        private const string k_TestAttackPath =
            "Assets/Tests/Fixtures/StepEleven/AD_Test_AI_BasicMelee.asset";
        private const string k_TestAllyDefinitionPath =
            "Assets/Tests/Fixtures/StepEleven/UD_Test_AI_Ally.asset";
        private const string k_TestEnemyDefinitionPath =
            "Assets/Tests/Fixtures/StepEleven/UD_Test_AI_Enemy.asset";
        private const string k_PoolCatalogPath =
            "Assets/Data/Catalogs/PC_ProjectilePools.asset";
        private const string k_UnitCatalogPath =
            "Assets/Data/Catalogs/UC_CombatSandbox.asset";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";
        private static readonly PoolId s_allyPoolId =
            new PoolId("TestAIAlly");
        private static readonly PoolId s_enemyPoolId =
            new PoolId("TestAIEnemy");

        [MenuItem("Tools/Monsters vs Zombies/Step 11/Create and Verify AI Bases")]
        public static void CreateAndVerifyAIBases()
        {
            EnsureFolder(k_TestFolder);
            StepElevenDefinitions definitions = CreateDefinitions();
            StepElevenPrefabs prefabs = CreatePrefabs(definitions);
            PoolCatalog poolCatalog = UpdatePoolCatalog(prefabs);
            UnitCatalog unitCatalog = UpdateUnitCatalog(definitions);
            UpdateCombatSandbox(definitions);
            EnsureCombatSandboxBuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyAssets(
                definitions,
                prefabs,
                poolCatalog,
                unitCatalog);
            Debug.Log(
                "[StepElevenAISetup] Created and verified NavMesh AI bases, faction branches, thin fixtures, catalogs, and CombatSandbox scenario.");
        }

        private static void EnsureCombatSandboxBuildScene()
        {
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(
                    EditorBuildSettings.scenes);
            for (int sceneIndex = 0;
                 sceneIndex < scenes.Count;
                 sceneIndex++)
            {
                if (scenes[sceneIndex].path != k_ScenePath)
                {
                    continue;
                }

                scenes[sceneIndex] = new EditorBuildSettingsScene(
                    k_ScenePath,
                    true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(k_ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static StepElevenDefinitions CreateDefinitions()
        {
            AttackDefinition attackDefinition =
                LoadOrCreateAsset<AttackDefinition>(k_TestAttackPath);
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.AttackId),
                new AttackId("TestAIBasicMelee"));
            SetAutoProperty(attackDefinition, nameof(AttackDefinition.Damage), 10f);
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.AttackRange),
                1.8f);
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.CooldownDuration),
                1f);
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.WindupDuration),
                0.25f);
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.RecoveryDuration),
                0.25f);
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.DeliveryType),
                AttackDeliveryType.Melee);
            SetAutoProperty<ProjectileDefinition>(
                attackDefinition,
                nameof(AttackDefinition.ProjectileDefinition),
                null);
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.AcceptedHitEffect),
                new AcceptedHitEffectConfiguration(
                    StatusEffectType.None,
                    0f));
            SetAutoProperty(
                attackDefinition,
                nameof(AttackDefinition.DamageCategoryId),
                new DamageCategoryId("Direct"));
            EditorUtility.SetDirty(attackDefinition);

            AIUnitDefinition allyDefinition =
                LoadOrCreateAsset<AIUnitDefinition>(
                    k_TestAllyDefinitionPath);
            ConfigureAIUnitDefinition(
                allyDefinition,
                "TestAIAlly",
                "Test AI Ally",
                UnitFaction.Ally,
                s_allyPoolId,
                attackDefinition);
            AIUnitDefinition enemyDefinition =
                LoadOrCreateAsset<AIUnitDefinition>(
                    k_TestEnemyDefinitionPath);
            ConfigureAIUnitDefinition(
                enemyDefinition,
                "TestAIEnemy",
                "Test AI Enemy",
                UnitFaction.Enemy,
                s_enemyPoolId,
                attackDefinition);
            return new StepElevenDefinitions(
                attackDefinition,
                allyDefinition,
                enemyDefinition);
        }

        private static void ConfigureAIUnitDefinition(
            AIUnitDefinition definition,
            string unitId,
            string displayName,
            UnitFaction faction,
            PoolId poolId,
            AttackDefinition attackDefinition)
        {
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.UnitId),
                new UnitId(unitId));
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.DisplayName),
                displayName);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.Faction),
                faction);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.MaximumHealth),
                60f);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.MoveSpeed),
                3.5f);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.TurnSpeed),
                540f);
            SetAutoProperty(definition, nameof(UnitDefinition.PoolId), poolId);
            SetAutoProperty(
                definition,
                nameof(AIUnitDefinition.ChaseRange),
                12f);
            SetAutoProperty(
                definition,
                nameof(AIUnitDefinition.DefaultAttackDefinition),
                attackDefinition);
            EditorUtility.SetDirty(definition);
        }

        private static StepElevenPrefabs CreatePrefabs(
            StepElevenDefinitions definitions)
        {
            GameObject baseUnitPrefab =
                LoadRequiredAsset<GameObject>(k_BaseUnitPrefabPath);
            GameObject aiBasePrefab = CreateAIBasePrefab(baseUnitPrefab);
            GameObject allyBasePrefab = CreateFactionBasePrefab(
                aiBasePrefab,
                k_AllyBasePrefabPath,
                "PF_Unit_Ally_Base",
                UnitFaction.Ally);
            GameObject enemyBasePrefab = CreateFactionBasePrefab(
                aiBasePrefab,
                k_EnemyBasePrefabPath,
                "PF_Unit_Enemy_Base",
                UnitFaction.Enemy);
            GameObject allyPrefab = CreateConcreteFixturePrefab(
                allyBasePrefab,
                k_TestAllyPrefabPath,
                "PF_Test_AI_Ally",
                "AllyPlaceholder",
                definitions.AllyDefinition,
                definitions.AttackDefinition);
            GameObject enemyPrefab = CreateConcreteFixturePrefab(
                enemyBasePrefab,
                k_TestEnemyPrefabPath,
                "PF_Test_AI_Enemy",
                "EnemyPlaceholder",
                definitions.EnemyDefinition,
                definitions.AttackDefinition);
            return new StepElevenPrefabs(
                aiBasePrefab,
                allyBasePrefab,
                enemyBasePrefab,
                allyPrefab,
                enemyPrefab);
        }

        private static GameObject CreateAIBasePrefab(GameObject baseUnitPrefab)
        {
            GameObject instance = InstantiatePrefab(baseUnitPrefab);
            instance.name = "PF_Unit_AI_Base";
            instance.SetActive(false);
            instance.layer = LayerMask.NameToLayer("UnitBody");
            NavMeshAgent agent = instance.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 2f;
            instance.AddComponent<NavMeshUnitMotor>();
            instance.AddComponent<AIUnitBrain>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                k_AIBasePrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateFactionBasePrefab(
            GameObject aiBasePrefab,
            string assetPath,
            string prefabName,
            UnitFaction expectedFaction)
        {
            GameObject instance = InstantiatePrefab(aiBasePrefab);
            instance.name = prefabName;
            instance.SetActive(false);
            AIFactionDefinitionGuard guard =
                instance.AddComponent<AIFactionDefinitionGuard>();
            SetAutoProperty(
                guard,
                nameof(AIFactionDefinitionGuard.ExpectedFaction),
                expectedFaction);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                assetPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateConcreteFixturePrefab(
            GameObject factionBasePrefab,
            string assetPath,
            string prefabName,
            string visualName,
            AIUnitDefinition definition,
            AttackDefinition attackDefinition)
        {
            GameObject instance = InstantiatePrefab(factionBasePrefab);
            instance.name = prefabName;
            instance.SetActive(false);
            SetAutoProperty(
                instance.GetComponent<UnitController>(),
                nameof(UnitController.Definition),
                definition);
            AttackController attackController =
                instance.GetComponent<AttackController>();
            SetAutoProperty(
                attackController,
                nameof(AttackController.AttackDefinition),
                attackDefinition);
            MeleeAttackExecutor meleeExecutor =
                instance.AddComponent<MeleeAttackExecutor>();
            SetField(
                attackController,
                "_executorBindings",
                new[]
                {
                    new AttackExecutorBinding(
                        AttackDeliveryType.Melee,
                        meleeExecutor)
                });
            instance.AddComponent<ImmediateDeathPoolReturn>();

            Transform visualRoot = RequireChild(instance, "VisualRoot");
            GameObject visual = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            visual.name = visualName;
            visual.transform.SetParent(visualRoot, false);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                assetPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static PoolCatalog UpdatePoolCatalog(
            StepElevenPrefabs prefabs)
        {
            PoolCatalog poolCatalog =
                LoadRequiredAsset<PoolCatalog>(k_PoolCatalogPath);
            List<PoolCatalogEntry> entries = new List<PoolCatalogEntry>();
            for (int entryIndex = 0;
                 entryIndex < poolCatalog.Count;
                 entryIndex++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(entryIndex);
                if (entry != null && entry.PoolId != s_allyPoolId &&
                    entry.PoolId != s_enemyPoolId)
                {
                    entries.Add(entry);
                }
            }

            entries.Add(CreatePoolEntry(s_allyPoolId, prefabs.AllyPrefab));
            entries.Add(CreatePoolEntry(s_enemyPoolId, prefabs.EnemyPrefab));
            SetField(poolCatalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(poolCatalog);
            return poolCatalog;
        }

        private static PoolCatalogEntry CreatePoolEntry(
            PoolId poolId,
            GameObject prefab)
        {
            PoolCatalogEntry entry = new PoolCatalogEntry();
            SetAutoProperty(entry, nameof(PoolCatalogEntry.PoolId), poolId);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.Prefab), prefab);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.InitialPrewarmCount),
                10);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                100);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.CapacityPolicy),
                PoolCapacityPolicy.Expandable);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumActiveCount),
                0);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.EnableCollectionChecks),
                true);
            return entry;
        }

        private static UnitCatalog UpdateUnitCatalog(
            StepElevenDefinitions definitions)
        {
            UnitCatalog unitCatalog =
                LoadRequiredAsset<UnitCatalog>(k_UnitCatalogPath);
            List<UnitCatalogEntry> entries = new List<UnitCatalogEntry>();
            for (int entryIndex = 0;
                 entryIndex < unitCatalog.Count;
                 entryIndex++)
            {
                UnitCatalogEntry entry = unitCatalog.GetEntry(entryIndex);
                if (entry != null &&
                    entry.UnitId != definitions.AllyDefinition.UnitId &&
                    entry.UnitId != definitions.EnemyDefinition.UnitId)
                {
                    entries.Add(entry);
                }
            }

            entries.Add(CreateUnitEntry(definitions.AllyDefinition));
            entries.Add(CreateUnitEntry(definitions.EnemyDefinition));
            SetField(unitCatalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(unitCatalog);
            return unitCatalog;
        }

        private static UnitCatalogEntry CreateUnitEntry(
            UnitDefinition definition)
        {
            UnitCatalogEntry entry = new UnitCatalogEntry();
            SetAutoProperty(
                entry,
                nameof(UnitCatalogEntry.Definition),
                definition);
            return entry;
        }

        private static void UpdateCombatSandbox(
            StepElevenDefinitions definitions)
        {
            Scene scene = EditorSceneManager.OpenScene(
                k_ScenePath,
                OpenSceneMode.Single);
            GameObject systemsRoot = RequireRoot(scene, "__Systems");
            GameObject spawnPointsRoot = RequireRoot(scene, "SpawnPoints");
            Transform priorScenario = systemsRoot.transform.Find(
                "AISandboxScenario");
            if (priorScenario != null)
            {
                UnityEngine.Object.DestroyImmediate(priorScenario.gameObject);
            }

            CombatSandboxBootstrap bootstrap =
                RequireChildComponent<CombatSandboxBootstrap>(
                    systemsRoot,
                    "CombatSandboxBootstrap");
            InitialSandboxSpawner initialSpawner =
                RequireChildComponent<InitialSandboxSpawner>(
                    systemsRoot,
                    "InitialSandboxSpawner");
            SpawnManager spawnManager = RequireChildComponent<SpawnManager>(
                systemsRoot,
                "SpawnManager");
            InteractionSystem interactionSystem =
                RequireChildComponent<InteractionSystem>(
                    systemsRoot,
                    "InteractionSystem");
            SpawnPointGroup allySpawnPoints =
                RequireChildComponent<SpawnPointGroup>(
                    spawnPointsRoot,
                    "AllySpawnPoints");
            SpawnPointGroup enemySpawnPoints =
                RequireChildComponent<SpawnPointGroup>(
                    spawnPointsRoot,
                    "EnemySpawnPoints");

            GameObject scenarioObject = new GameObject("AISandboxScenario");
            scenarioObject.transform.SetParent(systemsRoot.transform, false);
            AISandboxScenarioController scenario =
                scenarioObject.AddComponent<AISandboxScenarioController>();
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.Bootstrap),
                bootstrap);
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.InitialSandboxSpawner),
                initialSpawner);
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.SpawnManager),
                spawnManager);
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.InteractionSystem),
                interactionSystem);
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.AllySpawnPoints),
                allySpawnPoints);
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.EnemySpawnPoints),
                enemySpawnPoints);
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.AllyDefinition),
                definitions.AllyDefinition);
            SetAutoProperty(
                scenario,
                nameof(AISandboxScenarioController.EnemyDefinition),
                definitions.EnemyDefinition);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, k_ScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save the Step 11 CombatSandbox scenario.");
            }
        }

        private static void VerifyAssets(
            StepElevenDefinitions definitions,
            StepElevenPrefabs prefabs,
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog)
        {
            if (!definitions.AttackDefinition.Validate().IsValid ||
                !definitions.AllyDefinition.Validate().IsValid ||
                !definitions.EnemyDefinition.Validate().IsValid)
            {
                throw new InvalidOperationException(
                    "Step 11 test definitions are invalid.");
            }

            if (PrefabUtility.GetPrefabAssetType(prefabs.AIBasePrefab) !=
                    PrefabAssetType.Variant ||
                PrefabUtility.GetPrefabAssetType(prefabs.AllyBasePrefab) !=
                    PrefabAssetType.Variant ||
                PrefabUtility.GetPrefabAssetType(prefabs.EnemyBasePrefab) !=
                    PrefabAssetType.Variant ||
                PrefabUtility.GetPrefabAssetType(prefabs.AllyPrefab) !=
                    PrefabAssetType.Variant ||
                PrefabUtility.GetPrefabAssetType(prefabs.EnemyPrefab) !=
                    PrefabAssetType.Variant)
            {
                throw new InvalidOperationException(
                    "The Step 11 AI prefab chain must contain only variants.");
            }

            ValidateConcretePrefab(
                prefabs.AllyPrefab,
                UnitFaction.Ally);
            ValidateConcretePrefab(
                prefabs.EnemyPrefab,
                UnitFaction.Enemy);
            if (!poolCatalog.TryGetEntry(s_allyPoolId, out _) ||
                !poolCatalog.TryGetEntry(s_enemyPoolId, out _) ||
                !unitCatalog.TryGetDefinition(
                    definitions.AllyDefinition.UnitId,
                    out UnitDefinition catalogAlly) ||
                catalogAlly != definitions.AllyDefinition ||
                !unitCatalog.TryGetDefinition(
                    definitions.EnemyDefinition.UnitId,
                    out UnitDefinition catalogEnemy) ||
                catalogEnemy != definitions.EnemyDefinition)
            {
                throw new InvalidOperationException(
                    "Step 11 fixture catalog entries are invalid.");
            }
        }

        private static void ValidateConcretePrefab(
            GameObject prefab,
            UnitFaction expectedFaction)
        {
            UnitController unitController = prefab.GetComponent<UnitController>();
            NavMeshUnitMotor motor = prefab.GetComponent<NavMeshUnitMotor>();
            AIUnitBrain brain = prefab.GetComponent<AIUnitBrain>();
            AIFactionDefinitionGuard guard =
                prefab.GetComponent<AIFactionDefinitionGuard>();
            string failure = "A required AI component is missing.";
            if (unitController == null || motor == null || brain == null ||
                guard == null || guard.ExpectedFaction != expectedFaction ||
                !unitController.ValidateGameplayComponents(out failure) ||
                !motor.ValidateConfiguration(out failure) ||
                !brain.ValidateConfiguration(out failure) ||
                !guard.ValidateConfiguration(out failure))
            {
                throw new InvalidOperationException(
                    $"{prefab.name} validation failed: {failure}");
            }
        }

        private static GameObject InstantiatePrefab(GameObject prefab)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab)
                as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate prefab '{prefab.name}'.");
            }

            return instance;
        }

        private static Transform RequireChild(
            GameObject parent,
            string relativePath)
        {
            Transform child = parent.transform.Find(relativePath);
            if (child == null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}' is missing '{relativePath}'.");
            }

            return child;
        }

        private static GameObject RequireRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root;
                }
            }

            throw new InvalidOperationException(
                $"CombatSandbox is missing root '{rootName}'.");
        }

        private static T RequireChildComponent<T>(
            GameObject parent,
            string childName) where T : Component
        {
            Transform child = parent.transform.Find(childName);
            T component = child == null ? null : child.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}/{childName}' requires {typeof(T).Name}.");
            }

            return component;
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

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset '{assetPath}' is missing.");
            }

            return asset;
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
            asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void SetAutoProperty<TValue>(
            object target,
            string propertyName,
            TValue value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void SetField<TValue>(
            object target,
            string fieldName,
            TValue value)
        {
            Type currentType = target.GetType();
            while (currentType != null)
            {
                FieldInfo field = currentType.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(target, value);
                    if (target is UnityEngine.Object unityObject)
                    {
                        EditorUtility.SetDirty(unityObject);
                    }

                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new MissingFieldException(
                target.GetType().FullName,
                fieldName);
        }

        private readonly struct StepElevenDefinitions
        {
            public AttackDefinition AttackDefinition { get; }
            public AIUnitDefinition AllyDefinition { get; }
            public AIUnitDefinition EnemyDefinition { get; }

            public StepElevenDefinitions(
                AttackDefinition attackDefinition,
                AIUnitDefinition allyDefinition,
                AIUnitDefinition enemyDefinition)
            {
                AttackDefinition = attackDefinition;
                AllyDefinition = allyDefinition;
                EnemyDefinition = enemyDefinition;
            }
        }

        private readonly struct StepElevenPrefabs
        {
            public GameObject AIBasePrefab { get; }
            public GameObject AllyBasePrefab { get; }
            public GameObject EnemyBasePrefab { get; }
            public GameObject AllyPrefab { get; }
            public GameObject EnemyPrefab { get; }

            public StepElevenPrefabs(
                GameObject aiBasePrefab,
                GameObject allyBasePrefab,
                GameObject enemyBasePrefab,
                GameObject allyPrefab,
                GameObject enemyPrefab)
            {
                AIBasePrefab = aiBasePrefab;
                AllyBasePrefab = allyBasePrefab;
                EnemyBasePrefab = enemyBasePrefab;
                AllyPrefab = allyPrefab;
                EnemyPrefab = enemyPrefab;
            }
        }
    }
}
