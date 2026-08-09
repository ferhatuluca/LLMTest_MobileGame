using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
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
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepTwelveRegularUnitSetup
    {
        private const string k_AllyBasePath =
            "Assets/Prefabs/Units/PF_Unit_Ally_Base.prefab";
        private const string k_EnemyBasePath =
            "Assets/Prefabs/Units/PF_Unit_Enemy_Base.prefab";
        private const string k_UnitFolder = "Assets/Data/Units";
        private const string k_AttackFolder = "Assets/Data/Attacks";
        private const string k_UnitPrefabFolder = "Assets/Prefabs/Units";
        private const string k_VisualFolder =
            "Assets/Prefabs/Visuals/Units";
        private const string k_PoolCatalogPath =
            "Assets/Data/Catalogs/PC_ProjectilePools.asset";
        private const string k_UnitCatalogPath =
            "Assets/Data/Catalogs/UC_CombatSandbox.asset";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";

        private static readonly RegularUnitSpec[] s_Specifications =
        {
            new RegularUnitSpec(
                "AllyClassicMelee",
                "Ally Classic Melee",
                UnitFaction.Ally,
                60f,
                3.5f,
                540f,
                12f,
                "AD_BasicMelee.asset",
                "UD_Ally_ClassicMelee.asset",
                "PF_Ally_ClassicMelee.prefab",
                RegularVisualKind.ClassicMelee,
                string.Empty),
            new RegularUnitSpec(
                "EnemyClassicMelee",
                "Enemy Classic Melee",
                UnitFaction.Enemy,
                60f,
                3.5f,
                540f,
                12f,
                "AD_BasicMelee.asset",
                "UD_Enemy_ClassicMelee.asset",
                "PF_Enemy_ClassicMelee.prefab",
                RegularVisualKind.ClassicMelee,
                string.Empty),
            new RegularUnitSpec(
                "AllyClassicRange",
                "Ally Classic Range",
                UnitFaction.Ally,
                50f,
                3.2f,
                540f,
                14f,
                "AD_BasicBullet.asset",
                "UD_Ally_ClassicRange.asset",
                "PF_Ally_ClassicRange.prefab",
                RegularVisualKind.ClassicRange,
                "MuzzleSocket"),
            new RegularUnitSpec(
                "EnemyClassicRange",
                "Enemy Classic Range",
                UnitFaction.Enemy,
                50f,
                3.2f,
                540f,
                14f,
                "AD_BasicBullet.asset",
                "UD_Enemy_ClassicRange.asset",
                "PF_Enemy_ClassicRange.prefab",
                RegularVisualKind.ClassicRange,
                "MuzzleSocket"),
            new RegularUnitSpec(
                "AllyDragon",
                "Ally Dragon",
                UnitFaction.Ally,
                80f,
                3f,
                480f,
                16f,
                "AD_DragonFireball.asset",
                "UD_Ally_Dragon.asset",
                "PF_Ally_Dragon.prefab",
                RegularVisualKind.Dragon,
                "MouthSocket"),
            new RegularUnitSpec(
                "EnemyDragon",
                "Enemy Dragon",
                UnitFaction.Enemy,
                80f,
                3f,
                480f,
                16f,
                "AD_DragonFireball.asset",
                "UD_Enemy_Dragon.asset",
                "PF_Enemy_Dragon.prefab",
                RegularVisualKind.Dragon,
                "MouthSocket"),
            new RegularUnitSpec(
                "AllyDoubleHead",
                "Ally DoubleHead",
                UnitFaction.Ally,
                110f,
                3.2f,
                480f,
                12f,
                "AD_DoubleHeadMelee.asset",
                "UD_Ally_DoubleHead.asset",
                "PF_Ally_DoubleHead.prefab",
                RegularVisualKind.DoubleHead,
                "WristAttackSocket")
        };

        [MenuItem(
            "Tools/Monsters vs Zombies/Step 12/Create and Verify Regular Units")]
        public static void CreateAndVerifyRegularUnits()
        {
            EnsureFolder(k_UnitFolder);
            EnsureFolder(k_UnitPrefabFolder);
            EnsureFolder(k_VisualFolder);

            Dictionary<RegularVisualKind, GameObject> visuals =
                CreateVisualPrefabs();
            Dictionary<string, AttackDefinition> attacks =
                LoadAndCreateAttackDefinitions();
            Dictionary<string, AIUnitDefinition> definitions =
                CreateUnitDefinitions(attacks);
            Dictionary<string, GameObject> prefabs =
                CreateConcretePrefabs(definitions, attacks, visuals);
            PoolCatalog poolCatalog = UpdatePoolCatalog(prefabs);
            UnitCatalog unitCatalog = UpdateUnitCatalog(definitions);
            UpdateCombatSandbox(definitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyAssets(
                definitions,
                attacks,
                visuals,
                prefabs,
                poolCatalog,
                unitCatalog);
            Debug.Log(
                "[StepTwelveRegularUnitSetup] Created and verified all seven regular concrete units, nested visuals, definitions, pools, catalogs, and direct sandbox spawns.");
        }

        private static Dictionary<RegularVisualKind, GameObject>
            CreateVisualPrefabs()
        {
            Dictionary<RegularVisualKind, GameObject> visuals =
                new Dictionary<RegularVisualKind, GameObject>();
            foreach (RegularVisualKind kind in
                     Enum.GetValues(typeof(RegularVisualKind)))
            {
                string path = GetVisualPath(kind);
                GameObject root = new GameObject($"PF_Visual_{kind}");
                BuildVisual(root.transform, kind);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    path);
                UnityEngine.Object.DestroyImmediate(root);
                visuals.Add(kind, prefab);
            }

            return visuals;
        }

        private static void BuildVisual(
            Transform root,
            RegularVisualKind kind)
        {
            switch (kind)
            {
                case RegularVisualKind.ClassicMelee:
                    AddPrimitive(
                        root,
                        PrimitiveType.Capsule,
                        "Body",
                        new Vector3(0f, 0.9f, 0f),
                        new Vector3(0.8f, 0.9f, 0.8f));
                    AddPrimitive(
                        root,
                        PrimitiveType.Sphere,
                        "LeftHand",
                        new Vector3(-0.55f, 0.9f, 0.2f),
                        Vector3.one * 0.3f);
                    AddPrimitive(
                        root,
                        PrimitiveType.Sphere,
                        "RightHand",
                        new Vector3(0.55f, 0.9f, 0.2f),
                        Vector3.one * 0.3f);
                    break;

                case RegularVisualKind.ClassicRange:
                    AddPrimitive(
                        root,
                        PrimitiveType.Capsule,
                        "Body",
                        new Vector3(0f, 0.9f, 0f),
                        new Vector3(0.75f, 0.9f, 0.75f));
                    Transform weapon = AddPrimitive(
                        root,
                        PrimitiveType.Cube,
                        "SimpleRangedWeapon",
                        new Vector3(0.45f, 1f, 0.45f),
                        new Vector3(0.18f, 0.18f, 0.8f));
                    AddSocket(
                        weapon,
                        "MuzzleSocket",
                        new Vector3(0f, 0f, 0.6f));
                    break;

                case RegularVisualKind.Dragon:
                    AddPrimitive(
                        root,
                        PrimitiveType.Capsule,
                        "Body",
                        new Vector3(0f, 0.75f, 0f),
                        new Vector3(1f, 0.75f, 1.25f));
                    AddPrimitive(
                        root,
                        PrimitiveType.Sphere,
                        "Head",
                        new Vector3(0f, 1.25f, 0.65f),
                        new Vector3(0.8f, 0.7f, 1f));
                    AddPrimitive(
                        root,
                        PrimitiveType.Cube,
                        "LeftWing",
                        new Vector3(-0.75f, 1f, -0.1f),
                        new Vector3(0.7f, 0.08f, 1f));
                    AddPrimitive(
                        root,
                        PrimitiveType.Cube,
                        "RightWing",
                        new Vector3(0.75f, 1f, -0.1f),
                        new Vector3(0.7f, 0.08f, 1f));
                    AddSocket(
                        root,
                        "MouthSocket",
                        new Vector3(0f, 1.25f, 1.15f));
                    break;

                case RegularVisualKind.DoubleHead:
                    AddPrimitive(
                        root,
                        PrimitiveType.Capsule,
                        "Body",
                        new Vector3(0f, 1f, 0f),
                        new Vector3(1.15f, 1.05f, 1.15f));
                    AddPrimitive(
                        root,
                        PrimitiveType.Sphere,
                        "LeftHead",
                        new Vector3(-0.35f, 1.9f, 0f),
                        Vector3.one * 0.65f);
                    AddPrimitive(
                        root,
                        PrimitiveType.Sphere,
                        "RightHead",
                        new Vector3(0.35f, 1.9f, 0f),
                        Vector3.one * 0.65f);
                    Transform wrist = AddPrimitive(
                        root,
                        PrimitiveType.Sphere,
                        "AttackWrist",
                        new Vector3(0.8f, 1f, 0.35f),
                        Vector3.one * 0.35f);
                    AddSocket(wrist, "WristAttackSocket", Vector3.zero);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static Transform AddPrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            UnityEngine.Object.DestroyImmediate(
                primitive.GetComponent<Collider>());
            return primitive.transform;
        }

        private static Transform AddSocket(
            Transform parent,
            string socketName,
            Vector3 localPosition)
        {
            GameObject socket = new GameObject(socketName);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
            return socket.transform;
        }

        private static Dictionary<string, AttackDefinition>
            LoadAndCreateAttackDefinitions()
        {
            Dictionary<string, AttackDefinition> attacks =
                new Dictionary<string, AttackDefinition>();
            AddExistingAttack(attacks, "AD_BasicMelee.asset");
            AddExistingAttack(attacks, "AD_BasicBullet.asset");
            AddExistingAttack(attacks, "AD_DragonFireball.asset");

            AttackDefinition doubleHead =
                LoadOrCreateAsset<AttackDefinition>(
                    $"{k_AttackFolder}/AD_DoubleHeadMelee.asset");
            SetAutoProperty(
                doubleHead,
                nameof(AttackDefinition.AttackId),
                new AttackId("DoubleHeadMelee"));
            SetAutoProperty(doubleHead, nameof(AttackDefinition.Damage), 18f);
            SetAutoProperty(
                doubleHead,
                nameof(AttackDefinition.AttackRange),
                2f);
            SetAutoProperty(
                doubleHead,
                nameof(AttackDefinition.CooldownDuration),
                1.3f);
            SetAutoProperty(
                doubleHead,
                nameof(AttackDefinition.WindupDuration),
                0.35f);
            SetAutoProperty(
                doubleHead,
                nameof(AttackDefinition.RecoveryDuration),
                0.3f);
            SetAutoProperty(
                doubleHead,
                nameof(AttackDefinition.DeliveryType),
                AttackDeliveryType.Melee);
            SetAutoProperty<ProjectileDefinition>(
                doubleHead,
                nameof(AttackDefinition.ProjectileDefinition),
                null);
            SetAutoProperty(
                doubleHead,
                nameof(AttackDefinition.DamageCategoryId),
                new DamageCategoryId("Direct"));
            EditorUtility.SetDirty(doubleHead);
            attacks.Add("AD_DoubleHeadMelee.asset", doubleHead);
            return attacks;
        }

        private static void AddExistingAttack(
            IDictionary<string, AttackDefinition> attacks,
            string fileName)
        {
            AttackDefinition attack = LoadRequiredAsset<AttackDefinition>(
                $"{k_AttackFolder}/{fileName}");
            if (!attack.Validate().IsValid)
            {
                throw new InvalidOperationException(
                    $"The existing {fileName} attack is invalid.");
            }

            attacks.Add(fileName, attack);
        }

        private static Dictionary<string, AIUnitDefinition>
            CreateUnitDefinitions(
                IReadOnlyDictionary<string, AttackDefinition> attacks)
        {
            Dictionary<string, AIUnitDefinition> definitions =
                new Dictionary<string, AIUnitDefinition>();
            foreach (RegularUnitSpec spec in s_Specifications)
            {
                AIUnitDefinition definition =
                    LoadOrCreateAsset<AIUnitDefinition>(
                        $"{k_UnitFolder}/{spec.DefinitionFileName}");
                SetAutoProperty(
                    definition,
                    nameof(UnitDefinition.UnitId),
                    new UnitId(spec.Id));
                SetAutoProperty(
                    definition,
                    nameof(UnitDefinition.DisplayName),
                    spec.DisplayName);
                SetAutoProperty(
                    definition,
                    nameof(UnitDefinition.Faction),
                    spec.Faction);
                SetAutoProperty(
                    definition,
                    nameof(UnitDefinition.MaximumHealth),
                    spec.Health);
                SetAutoProperty(
                    definition,
                    nameof(UnitDefinition.MoveSpeed),
                    spec.MoveSpeed);
                SetAutoProperty(
                    definition,
                    nameof(UnitDefinition.TurnSpeed),
                    spec.TurnSpeed);
                SetAutoProperty(
                    definition,
                    nameof(UnitDefinition.PoolId),
                    new PoolId(spec.Id));
                SetAutoProperty(
                    definition,
                    nameof(AIUnitDefinition.ChaseRange),
                    spec.ChaseRange);
                SetAutoProperty(
                    definition,
                    nameof(AIUnitDefinition.DefaultAttackDefinition),
                    attacks[spec.AttackFileName]);
                EditorUtility.SetDirty(definition);
                definitions.Add(spec.Id, definition);
            }

            return definitions;
        }

        private static Dictionary<string, GameObject> CreateConcretePrefabs(
            IReadOnlyDictionary<string, AIUnitDefinition> definitions,
            IReadOnlyDictionary<string, AttackDefinition> attacks,
            IReadOnlyDictionary<RegularVisualKind, GameObject> visuals)
        {
            GameObject allyBase =
                LoadRequiredAsset<GameObject>(k_AllyBasePath);
            GameObject enemyBase =
                LoadRequiredAsset<GameObject>(k_EnemyBasePath);
            Dictionary<string, GameObject> prefabs =
                new Dictionary<string, GameObject>();
            foreach (RegularUnitSpec spec in s_Specifications)
            {
                GameObject basePrefab = spec.Faction == UnitFaction.Ally
                    ? allyBase
                    : enemyBase;
                GameObject instance = InstantiatePrefab(basePrefab);
                instance.name =
                    spec.PrefabFileName.Substring(
                        0,
                        spec.PrefabFileName.Length - ".prefab".Length);
                instance.SetActive(false);

                UnitController unitController =
                    instance.GetComponent<UnitController>();
                AttackController attackController =
                    instance.GetComponent<AttackController>();
                AttackDefinition attack = attacks[spec.AttackFileName];
                SetAutoProperty(
                    unitController,
                    nameof(UnitController.Definition),
                    definitions[spec.Id]);
                SetAutoProperty(
                    attackController,
                    nameof(AttackController.AttackDefinition),
                    attack);

                Transform visualRoot = RequireChild(instance, "VisualRoot");
                GameObject nestedVisual = (GameObject)PrefabUtility
                    .InstantiatePrefab(visuals[spec.VisualKind]);
                nestedVisual.name = visuals[spec.VisualKind].name;
                nestedVisual.transform.SetParent(visualRoot, false);

                MonoBehaviour executor;
                if (attack.DeliveryType == AttackDeliveryType.Melee)
                {
                    executor = instance.AddComponent<MeleeAttackExecutor>();
                }
                else
                {
                    ProjectileAttackExecutor projectileExecutor =
                        instance.AddComponent<ProjectileAttackExecutor>();
                    Transform attackOrigin = FindDeepChild(
                        nestedVisual.transform,
                        spec.AttackSocketName);
                    if (attackOrigin == null)
                    {
                        throw new InvalidOperationException(
                            $"{spec.DisplayName} requires {spec.AttackSocketName}.");
                    }

                    SetAutoProperty(
                        projectileExecutor,
                        nameof(ProjectileAttackExecutor.AttackOrigin),
                        attackOrigin);
                    executor = projectileExecutor;
                }

                SetField(
                    attackController,
                    "_executorBindings",
                    new[]
                    {
                        new AttackExecutorBinding(
                            attack.DeliveryType,
                            executor)
                    });
                instance.AddComponent<ImmediateDeathPoolReturn>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    $"{k_UnitPrefabFolder}/{spec.PrefabFileName}");
                UnityEngine.Object.DestroyImmediate(instance);
                prefabs.Add(spec.Id, prefab);
            }

            return prefabs;
        }

        private static PoolCatalog UpdatePoolCatalog(
            IReadOnlyDictionary<string, GameObject> prefabs)
        {
            PoolCatalog catalog =
                LoadRequiredAsset<PoolCatalog>(k_PoolCatalogPath);
            HashSet<PoolId> ownedPoolIds = new HashSet<PoolId>();
            foreach (RegularUnitSpec spec in s_Specifications)
            {
                ownedPoolIds.Add(new PoolId(spec.Id));
            }

            List<PoolCatalogEntry> entries = new List<PoolCatalogEntry>();
            for (int index = 0; index < catalog.Count; index++)
            {
                PoolCatalogEntry entry = catalog.GetEntry(index);
                if (entry != null && !ownedPoolIds.Contains(entry.PoolId))
                {
                    entries.Add(entry);
                }
            }

            foreach (RegularUnitSpec spec in s_Specifications)
            {
                PoolCatalogEntry entry = new PoolCatalogEntry();
                SetAutoProperty(
                    entry,
                    nameof(PoolCatalogEntry.PoolId),
                    new PoolId(spec.Id));
                SetAutoProperty(
                    entry,
                    nameof(PoolCatalogEntry.Prefab),
                    prefabs[spec.Id]);
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
                entries.Add(entry);
            }

            SetField(catalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static UnitCatalog UpdateUnitCatalog(
            IReadOnlyDictionary<string, AIUnitDefinition> definitions)
        {
            UnitCatalog catalog =
                LoadRequiredAsset<UnitCatalog>(k_UnitCatalogPath);
            HashSet<UnitId> ownedUnitIds = new HashSet<UnitId>();
            foreach (RegularUnitSpec spec in s_Specifications)
            {
                ownedUnitIds.Add(new UnitId(spec.Id));
            }

            List<UnitCatalogEntry> entries = new List<UnitCatalogEntry>();
            for (int index = 0; index < catalog.Count; index++)
            {
                UnitCatalogEntry entry = catalog.GetEntry(index);
                if (entry != null && !ownedUnitIds.Contains(entry.UnitId))
                {
                    entries.Add(entry);
                }
            }

            foreach (RegularUnitSpec spec in s_Specifications)
            {
                UnitCatalogEntry entry = new UnitCatalogEntry();
                SetAutoProperty(
                    entry,
                    nameof(UnitCatalogEntry.Definition),
                    definitions[spec.Id]);
                entries.Add(entry);
            }

            SetField(catalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void UpdateCombatSandbox(
            IReadOnlyDictionary<string, AIUnitDefinition> definitions)
        {
            Scene scene = EditorSceneManager.OpenScene(
                k_ScenePath,
                OpenSceneMode.Single);
            GameObject systemsRoot = RequireRoot(scene, "__Systems");
            GameObject spawnRoot = RequireRoot(scene, "SpawnPoints");
            RemoveChild(systemsRoot.transform, "RegularUnitSandboxScenario");
            RemoveChild(spawnRoot.transform, "RegularAllySpawnPoints");
            RemoveChild(spawnRoot.transform, "RegularEnemySpawnPoints");

            SpawnPointGroup allyPoints = CreateSpawnPointGroup(
                spawnRoot.transform,
                "RegularAllySpawnPoints",
                new[]
                {
                    new Vector3(-5f, 0f, -6f),
                    new Vector3(-5f, 0f, -2f),
                    new Vector3(-5f, 0f, 2f),
                    new Vector3(-5f, 0f, 6f)
                });
            SpawnPointGroup enemyPoints = CreateSpawnPointGroup(
                spawnRoot.transform,
                "RegularEnemySpawnPoints",
                new[]
                {
                    new Vector3(5f, 0f, -4f),
                    new Vector3(5f, 0f, 0f),
                    new Vector3(5f, 0f, 4f)
                });

            GameObject scenarioObject = new GameObject(
                "RegularUnitSandboxScenario");
            scenarioObject.transform.SetParent(systemsRoot.transform, false);
            RegularUnitSandboxScenarioController scenario =
                scenarioObject.AddComponent<
                    RegularUnitSandboxScenarioController>();
            CombatSandboxBootstrap bootstrap =
                RequireChildComponent<CombatSandboxBootstrap>(
                    systemsRoot,
                    "CombatSandboxBootstrap");
            SetAutoProperty(
                scenario,
                nameof(RegularUnitSandboxScenarioController.Bootstrap),
                bootstrap);
            SetAutoProperty(
                scenario,
                nameof(RegularUnitSandboxScenarioController.InitialSandboxSpawner),
                RequireChildComponent<InitialSandboxSpawner>(
                    systemsRoot,
                    "InitialSandboxSpawner"));
            SetAutoProperty(
                scenario,
                nameof(RegularUnitSandboxScenarioController.SpawnManager),
                RequireChildComponent<SpawnManager>(
                    systemsRoot,
                    "SpawnManager"));
            SetAutoProperty(
                scenario,
                nameof(RegularUnitSandboxScenarioController.InteractionSystem),
                RequireChildComponent<
                    MonstersVsZombies.Combat.Interaction.InteractionSystem>(
                    systemsRoot,
                    "InteractionSystem"));
            SetAutoProperty(
                scenario,
                nameof(RegularUnitSandboxScenarioController.AllySpawnPoints),
                allyPoints);
            SetAutoProperty(
                scenario,
                nameof(RegularUnitSandboxScenarioController.EnemySpawnPoints),
                enemyPoints);
            SetField(
                scenario,
                "_allyDefinitions",
                GetDefinitions(
                    definitions,
                    UnitFaction.Ally));
            SetField(
                scenario,
                "_enemyDefinitions",
                GetDefinitions(
                    definitions,
                    UnitFaction.Enemy));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, k_ScenePath);
        }

        private static AIUnitDefinition[] GetDefinitions(
            IReadOnlyDictionary<string, AIUnitDefinition> definitions,
            UnitFaction faction)
        {
            List<AIUnitDefinition> matches = new List<AIUnitDefinition>();
            foreach (RegularUnitSpec spec in s_Specifications)
            {
                if (spec.Faction == faction)
                {
                    matches.Add(definitions[spec.Id]);
                }
            }

            return matches.ToArray();
        }

        private static SpawnPointGroup CreateSpawnPointGroup(
            Transform parent,
            string name,
            IReadOnlyList<Vector3> positions)
        {
            GameObject groupObject = new GameObject(name);
            groupObject.transform.SetParent(parent, false);
            SpawnPointGroup group =
                groupObject.AddComponent<SpawnPointGroup>();
            Transform[] points = new Transform[positions.Count];
            for (int index = 0; index < positions.Count; index++)
            {
                GameObject point = new GameObject($"Point_{index + 1:00}");
                point.transform.SetParent(groupObject.transform, false);
                point.transform.localPosition = positions[index];
                points[index] = point.transform;
            }

            SetField(group, "_spawnPoints", points);
            return group;
        }

        private static void VerifyAssets(
            IReadOnlyDictionary<string, AIUnitDefinition> definitions,
            IReadOnlyDictionary<string, AttackDefinition> attacks,
            IReadOnlyDictionary<RegularVisualKind, GameObject> visuals,
            IReadOnlyDictionary<string, GameObject> prefabs,
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog)
        {
            if (!poolCatalog.Validate().IsValid ||
                !unitCatalog.Validate().IsValid)
            {
                throw new InvalidOperationException(
                    "The updated Step 12 catalogs are invalid.");
            }

            foreach (RegularUnitSpec spec in s_Specifications)
            {
                AIUnitDefinition definition = definitions[spec.Id];
                AttackDefinition attack = attacks[spec.AttackFileName];
                GameObject prefab = prefabs[spec.Id];
                if (!definition.Validate().IsValid ||
                    definition.DefaultAttackDefinition != attack ||
                    !unitCatalog.TryGetDefinition(
                        definition.UnitId,
                        out UnitDefinition catalogDefinition) ||
                    catalogDefinition != definition ||
                    !poolCatalog.TryGetEntry(
                        definition.PoolId,
                        out PoolCatalogEntry poolEntry) ||
                    poolEntry.Prefab != prefab ||
                    poolEntry.InitialPrewarmCount != 10 ||
                    poolEntry.MaximumInactiveRetainedCount != 100 ||
                    poolEntry.CapacityPolicy !=
                        PoolCapacityPolicy.Expandable)
                {
                    throw new InvalidOperationException(
                        $"{spec.DisplayName} registration is invalid.");
                }

                UnitController unitController =
                    prefab.GetComponent<UnitController>();
                AttackController attackController =
                    prefab.GetComponent<AttackController>();
                if (unitController == null ||
                    unitController.Definition != definition ||
                    attackController == null ||
                    attackController.AttackDefinition != attack ||
                    prefab.GetComponent<AIUnitBrain>() == null ||
                    prefab.GetComponent<ImmediateDeathPoolReturn>() == null ||
                    PrefabUtility.GetCorrespondingObjectFromSource(prefab) ==
                        null)
                {
                    throw new InvalidOperationException(
                        $"{spec.DisplayName} prefab composition is invalid.");
                }

                ProjectileAttackExecutor projectileExecutor =
                    prefab.GetComponent<ProjectileAttackExecutor>();
                MeleeAttackExecutor meleeExecutor =
                    prefab.GetComponent<MeleeAttackExecutor>();
                if (attack.DeliveryType == AttackDeliveryType.Projectile)
                {
                    if (projectileExecutor == null ||
                        meleeExecutor != null ||
                        projectileExecutor.AttackOrigin == null ||
                        projectileExecutor.AttackOrigin.name !=
                            spec.AttackSocketName)
                    {
                        throw new InvalidOperationException(
                            $"{spec.DisplayName} projectile socket is invalid.");
                    }
                }
                else if (meleeExecutor == null || projectileExecutor != null)
                {
                    throw new InvalidOperationException(
                        $"{spec.DisplayName} melee binding is invalid.");
                }

                Transform visualRoot = RequireChild(prefab, "VisualRoot");
                if (visualRoot.childCount != 1 ||
                    PrefabUtility.GetCorrespondingObjectFromSource(
                        visualRoot.GetChild(0).gameObject) !=
                    visuals[spec.VisualKind])
                {
                    throw new InvalidOperationException(
                        $"{spec.DisplayName} does not use its nested visual prefab.");
                }
            }

            GameObject doubleHead = prefabs["AllyDoubleHead"];
            if (FindDeepChild(
                    doubleHead.transform,
                    "WristAttackSocket") == null)
            {
                throw new InvalidOperationException(
                    "Ally DoubleHead requires its wrist attack socket.");
            }
        }

        private static string GetVisualPath(RegularVisualKind kind)
        {
            return $"{k_VisualFolder}/PF_Visual_{kind}.prefab";
        }

        private static GameObject InstantiatePrefab(GameObject prefab)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate {prefab.name}.");
            }

            return instance;
        }

        private static Transform RequireChild(
            GameObject root,
            string path)
        {
            Transform child = root.transform.Find(path);
            if (child == null)
            {
                throw new InvalidOperationException(
                    $"{root.name} is missing '{path}'.");
            }

            return child;
        }

        private static Transform FindDeepChild(
            Transform root,
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

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
            GameObject root,
            string childName)
            where T : Component
        {
            Transform child = root.transform.Find(childName);
            T component = child == null ? null : child.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"{root.name}/{childName} is missing {typeof(T).Name}.");
            }

            return component;
        }

        private static void RemoveChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset '{path}' is missing.");
            }

            return asset;
        }

        private static T LoadOrCreateAsset<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string nextPath = $"{currentPath}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
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

        private enum RegularVisualKind
        {
            ClassicMelee,
            ClassicRange,
            Dragon,
            DoubleHead
        }

        private sealed class RegularUnitSpec
        {
            public string Id { get; }
            public string DisplayName { get; }
            public UnitFaction Faction { get; }
            public float Health { get; }
            public float MoveSpeed { get; }
            public float TurnSpeed { get; }
            public float ChaseRange { get; }
            public string AttackFileName { get; }
            public string DefinitionFileName { get; }
            public string PrefabFileName { get; }
            public RegularVisualKind VisualKind { get; }
            public string AttackSocketName { get; }

            public RegularUnitSpec(
                string id,
                string displayName,
                UnitFaction faction,
                float health,
                float moveSpeed,
                float turnSpeed,
                float chaseRange,
                string attackFileName,
                string definitionFileName,
                string prefabFileName,
                RegularVisualKind visualKind,
                string attackSocketName)
            {
                Id = id;
                DisplayName = displayName;
                Faction = faction;
                Health = health;
                MoveSpeed = moveSpeed;
                TurnSpeed = turnSpeed;
                ChaseRange = chaseRange;
                AttackFileName = attackFileName;
                DefinitionFileName = definitionFileName;
                PrefabFileName = prefabFileName;
                VisualKind = visualKind;
                AttackSocketName = attackSocketName;
            }
        }
    }
}
