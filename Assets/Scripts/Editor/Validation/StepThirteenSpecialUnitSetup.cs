using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using MonstersVsZombies.Units.Lifecycle;
using MonstersVsZombies.Units.Special;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepThirteenSpecialUnitSetup
    {
        private const string k_EnemyBasePath =
            "Assets/Prefabs/Units/PF_Unit_Enemy_Base.prefab";
        private const string k_AttackFolder = "Assets/Data/Attacks";
        private const string k_UnitFolder = "Assets/Data/Units";
        private const string k_PrefabFolder = "Assets/Prefabs/Units";
        private const string k_VisualFolder =
            "Assets/Prefabs/Visuals/Units";
        private const string k_PoolCatalogPath =
            "Assets/Data/Catalogs/PC_ProjectilePools.asset";
        private const string k_UnitCatalogPath =
            "Assets/Data/Catalogs/UC_CombatSandbox.asset";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";

        private static readonly SpecialUnitSpec[] s_Specifications =
        {
            new SpecialUnitSpec(
                "EnemyStunner",
                "Enemy Stunner",
                "StunnerMelee",
                "AD_StunnerMelee.asset",
                "UD_Enemy_Stunner.asset",
                "PF_Enemy_Stunner.prefab",
                120f,
                2.8f,
                420f,
                12f,
                15f,
                2f,
                1.5f,
                0.45f,
                0.35f),
            new SpecialUnitSpec(
                "EnemyMiniDivisible",
                "Enemy MiniDivisible",
                "MiniDivisibleMelee",
                "AD_MiniDivisibleMelee.asset",
                "UD_Enemy_MiniDivisible.asset",
                "PF_Enemy_MiniDivisible.prefab",
                30f,
                4f,
                600f,
                10f,
                6f,
                1.4f,
                0.8f,
                0.2f,
                0.2f),
            new SpecialUnitSpec(
                "EnemyDivisible",
                "Enemy Divisible",
                "DivisibleMelee",
                "AD_DivisibleMelee.asset",
                "UD_Enemy_Divisible.asset",
                "PF_Enemy_Divisible.prefab",
                100f,
                2.7f,
                420f,
                12f,
                12f,
                1.9f,
                1.2f,
                0.3f,
                0.25f)
        };

        [MenuItem(
            "Tools/Monsters vs Zombies/Step 13/Create and Verify Special Units")]
        public static void CreateAndVerifySpecialUnits()
        {
            EnsureFolder(k_AttackFolder);
            EnsureFolder(k_UnitFolder);
            EnsureFolder(k_PrefabFolder);
            EnsureFolder(k_VisualFolder);

            Dictionary<string, AttackDefinition> attacks = CreateAttacks();
            Dictionary<string, AIUnitDefinition> definitions =
                CreateDefinitions(attacks);
            SpecialVisualAssets visuals = CreateVisuals();
            Dictionary<string, GameObject> prefabs = CreatePrefabs(
                definitions,
                attacks,
                visuals);
            PoolCatalog pools = UpdatePoolCatalog(prefabs);
            UnitCatalog units = UpdateUnitCatalog(definitions);
            UpdateCombatSandbox(definitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyAssets(
                definitions,
                attacks,
                visuals,
                prefabs,
                pools,
                units);
            Debug.Log(
                "[StepThirteenSpecialUnitSetup] Created and verified Stunner, MiniDivisible, Divisible, their special policies, pools, catalogs, nested visuals, and sandbox spawns.");
        }

        private static Dictionary<string, AttackDefinition> CreateAttacks()
        {
            Dictionary<string, AttackDefinition> attacks =
                new Dictionary<string, AttackDefinition>();
            foreach (SpecialUnitSpec spec in s_Specifications)
            {
                AttackDefinition attack =
                    LoadOrCreateAsset<AttackDefinition>(
                        $"{k_AttackFolder}/{spec.AttackFileName}");
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.AttackId),
                    new AttackId(spec.AttackId));
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.Damage),
                    spec.Damage);
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.AttackRange),
                    spec.AttackRange);
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.CooldownDuration),
                    spec.Cooldown);
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.WindupDuration),
                    spec.Windup);
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.RecoveryDuration),
                    spec.Recovery);
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.DeliveryType),
                    AttackDeliveryType.Melee);
                SetAutoProperty<ProjectileDefinition>(
                    attack,
                    nameof(AttackDefinition.ProjectileDefinition),
                    null);
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.AcceptedHitEffect),
                    spec.Id == "EnemyStunner"
                        ? new AcceptedHitEffectConfiguration(
                            StatusEffectType.Stun,
                            2f)
                        : new AcceptedHitEffectConfiguration(
                            StatusEffectType.None,
                            0f));
                SetAutoProperty(
                    attack,
                    nameof(AttackDefinition.DamageCategoryId),
                    new DamageCategoryId("Direct"));
                EditorUtility.SetDirty(attack);
                attacks.Add(spec.Id, attack);
            }

            return attacks;
        }

        private static Dictionary<string, AIUnitDefinition>
            CreateDefinitions(
                IReadOnlyDictionary<string, AttackDefinition> attacks)
        {
            Dictionary<string, AIUnitDefinition> definitions =
                new Dictionary<string, AIUnitDefinition>();
            foreach (SpecialUnitSpec spec in s_Specifications)
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
                    UnitFaction.Enemy);
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
                    attacks[spec.Id]);
                EditorUtility.SetDirty(definition);
                definitions.Add(spec.Id, definition);
            }

            return definitions;
        }

        private static SpecialVisualAssets CreateVisuals()
        {
            GameObject hammer = CreateHammerVisual();
            GameObject stunner = CreateStunnerVisual(hammer);
            GameObject divisible = CreateDivisibleVisual();
            return new SpecialVisualAssets(hammer, stunner, divisible);
        }

        private static GameObject CreateHammerVisual()
        {
            GameObject root = new GameObject("PF_Visual_StunnerHammer");
            AddPrimitive(
                root.transform,
                PrimitiveType.Cylinder,
                "Handle",
                new Vector3(0f, 0f, 0.45f),
                new Vector3(0.08f, 0.45f, 0.08f),
                new Vector3(90f, 0f, 0f));
            AddPrimitive(
                root.transform,
                PrimitiveType.Cube,
                "HammerHead",
                new Vector3(0f, 0f, 0.9f),
                new Vector3(0.65f, 0.35f, 0.3f),
                Vector3.zero);
            return SaveVisual(
                root,
                $"{k_VisualFolder}/PF_Visual_StunnerHammer.prefab");
        }

        private static GameObject CreateStunnerVisual(GameObject hammer)
        {
            GameObject root = new GameObject("PF_Visual_Stunner");
            AddPrimitive(
                root.transform,
                PrimitiveType.Capsule,
                "Body",
                new Vector3(0f, 1.05f, 0f),
                new Vector3(1.2f, 1.1f, 1.2f),
                Vector3.zero);
            GameObject socketObject = new GameObject("RightHandSocket");
            socketObject.transform.SetParent(root.transform, false);
            socketObject.transform.localPosition =
                new Vector3(0.8f, 1.1f, 0.2f);
            GameObject nestedHammer = (GameObject)PrefabUtility
                .InstantiatePrefab(hammer);
            nestedHammer.name = hammer.name;
            nestedHammer.transform.SetParent(socketObject.transform, false);
            return SaveVisual(
                root,
                $"{k_VisualFolder}/PF_Visual_Stunner.prefab");
        }

        private static GameObject CreateDivisibleVisual()
        {
            GameObject root = new GameObject("PF_Visual_Divisible");
            AddPrimitive(
                root.transform,
                PrimitiveType.Sphere,
                "ThickBody",
                new Vector3(0f, 1f, 0f),
                new Vector3(1.45f, 1.25f, 1.45f),
                Vector3.zero);
            AddPrimitive(
                root.transform,
                PrimitiveType.Sphere,
                "Head",
                new Vector3(0f, 1.9f, 0f),
                Vector3.one * 0.8f,
                Vector3.zero);
            return SaveVisual(
                root,
                $"{k_VisualFolder}/PF_Visual_Divisible.prefab");
        }

        private static Transform AddPrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.transform.localEulerAngles = localEulerAngles;
            UnityEngine.Object.DestroyImmediate(
                primitive.GetComponent<Collider>());
            return primitive.transform;
        }

        private static GameObject SaveVisual(
            GameObject root,
            string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Dictionary<string, GameObject> CreatePrefabs(
            IReadOnlyDictionary<string, AIUnitDefinition> definitions,
            IReadOnlyDictionary<string, AttackDefinition> attacks,
            SpecialVisualAssets visuals)
        {
            GameObject enemyBase =
                LoadRequiredAsset<GameObject>(k_EnemyBasePath);
            Dictionary<string, GameObject> prefabs =
                new Dictionary<string, GameObject>();
            foreach (SpecialUnitSpec spec in s_Specifications)
            {
                GameObject instance = (GameObject)PrefabUtility
                    .InstantiatePrefab(enemyBase);
                instance.name = spec.PrefabFileName.Substring(
                    0,
                    spec.PrefabFileName.Length - ".prefab".Length);
                instance.SetActive(false);

                UnitController unitController =
                    instance.GetComponent<UnitController>();
                AttackController attackController =
                    instance.GetComponent<AttackController>();
                SetAutoProperty(
                    unitController,
                    nameof(UnitController.Definition),
                    definitions[spec.Id]);
                SetAutoProperty(
                    attackController,
                    nameof(AttackController.AttackDefinition),
                    attacks[spec.Id]);
                MeleeAttackExecutor executor =
                    instance.AddComponent<MeleeAttackExecutor>();
                SetField(
                    attackController,
                    "_executorBindings",
                    new[]
                    {
                        new AttackExecutorBinding(
                            AttackDeliveryType.Melee,
                            executor)
                    });

                Transform visualRoot = instance.transform.Find("VisualRoot");
                GameObject visualPrefab = spec.Id == "EnemyStunner"
                    ? visuals.Stunner
                    : visuals.Divisible;
                GameObject nestedVisual = (GameObject)PrefabUtility
                    .InstantiatePrefab(visualPrefab);
                nestedVisual.name = visualPrefab.name;
                nestedVisual.transform.SetParent(visualRoot, false);

                if (spec.Id == "EnemyStunner")
                {
                    instance.AddComponent<StunnerHitPolicy>();
                    instance.AddComponent<ImmediateDeathPoolReturn>();
                }
                else if (spec.Id == "EnemyMiniDivisible")
                {
                    nestedVisual.transform.localScale = Vector3.one * 0.5f;
                    NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
                    agent.radius *= 0.5f;
                    agent.height *= 0.5f;
                    Transform hurtbox = instance.transform.Find("Hurtbox");
                    SphereCollider hurtboxCollider =
                        hurtbox.GetComponent<SphereCollider>();
                    hurtboxCollider.radius *= 0.5f;
                    instance.AddComponent<ImmediateDeathPoolReturn>();
                }
                else
                {
                    SpawnUnitsOnDeath deathSpawner =
                        instance.AddComponent<SpawnUnitsOnDeath>();
                    SetAutoProperty(
                        deathSpawner,
                        nameof(SpawnUnitsOnDeath.MiniDivisibleDefinition),
                        definitions["EnemyMiniDivisible"]);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    $"{k_PrefabFolder}/{spec.PrefabFileName}");
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
            HashSet<PoolId> ownedIds = new HashSet<PoolId>();
            foreach (SpecialUnitSpec spec in s_Specifications)
            {
                ownedIds.Add(new PoolId(spec.Id));
            }

            List<PoolCatalogEntry> entries = new List<PoolCatalogEntry>();
            for (int index = 0; index < catalog.Count; index++)
            {
                PoolCatalogEntry entry = catalog.GetEntry(index);
                if (entry != null && !ownedIds.Contains(entry.PoolId))
                {
                    entries.Add(entry);
                }
            }

            foreach (SpecialUnitSpec spec in s_Specifications)
            {
                bool isMini = spec.Id == "EnemyMiniDivisible";
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
                    isMini ? 30 : 10);
                SetAutoProperty(
                    entry,
                    nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                    isMini ? 150 : 100);
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
            HashSet<UnitId> ownedIds = new HashSet<UnitId>();
            foreach (SpecialUnitSpec spec in s_Specifications)
            {
                ownedIds.Add(new UnitId(spec.Id));
            }

            List<UnitCatalogEntry> entries = new List<UnitCatalogEntry>();
            for (int index = 0; index < catalog.Count; index++)
            {
                UnitCatalogEntry entry = catalog.GetEntry(index);
                if (entry != null && !ownedIds.Contains(entry.UnitId))
                {
                    entries.Add(entry);
                }
            }

            foreach (SpecialUnitSpec spec in s_Specifications)
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
            GameObject systems = RequireRoot(scene, "__Systems");
            GameObject spawnPoints = RequireRoot(scene, "SpawnPoints");
            RemoveChild(systems.transform, "SpecialUnitSandboxScenario");
            RemoveChild(spawnPoints.transform, "SpecialEnemySpawnPoints");

            GameObject groupObject = new GameObject(
                "SpecialEnemySpawnPoints");
            groupObject.transform.SetParent(spawnPoints.transform, false);
            SpawnPointGroup group =
                groupObject.AddComponent<SpawnPointGroup>();
            Vector3[] positions =
            {
                new Vector3(3f, 0f, -6f),
                new Vector3(3f, 0f, 0f),
                new Vector3(3f, 0f, 6f)
            };
            Transform[] pointTransforms = new Transform[positions.Length];
            for (int index = 0; index < positions.Length; index++)
            {
                GameObject point = new GameObject($"Point_{index + 1:00}");
                point.transform.SetParent(groupObject.transform, false);
                point.transform.localPosition = positions[index];
                pointTransforms[index] = point.transform;
            }

            SetField(group, "_spawnPoints", pointTransforms);

            GameObject scenarioObject = new GameObject(
                "SpecialUnitSandboxScenario");
            scenarioObject.transform.SetParent(systems.transform, false);
            SpecialUnitSandboxScenarioController scenario =
                scenarioObject.AddComponent<
                    SpecialUnitSandboxScenarioController>();
            SetAutoProperty(
                scenario,
                nameof(SpecialUnitSandboxScenarioController.Bootstrap),
                RequireChildComponent<CombatSandboxBootstrap>(
                    systems,
                    "CombatSandboxBootstrap"));
            SetAutoProperty(
                scenario,
                nameof(SpecialUnitSandboxScenarioController.InitialSandboxSpawner),
                RequireChildComponent<InitialSandboxSpawner>(
                    systems,
                    "InitialSandboxSpawner"));
            SetAutoProperty(
                scenario,
                nameof(SpecialUnitSandboxScenarioController.SpawnManager),
                RequireChildComponent<SpawnManager>(
                    systems,
                    "SpawnManager"));
            SetAutoProperty(
                scenario,
                nameof(SpecialUnitSandboxScenarioController.InteractionSystem),
                RequireChildComponent<
                    MonstersVsZombies.Combat.Interaction.InteractionSystem>(
                    systems,
                    "InteractionSystem"));
            SetAutoProperty(
                scenario,
                nameof(SpecialUnitSandboxScenarioController.SpawnPoints),
                group);
            SetField(
                scenario,
                "_definitions",
                new[]
                {
                    definitions["EnemyStunner"],
                    definitions["EnemyMiniDivisible"],
                    definitions["EnemyDivisible"]
                });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, k_ScenePath);
        }

        private static void VerifyAssets(
            IReadOnlyDictionary<string, AIUnitDefinition> definitions,
            IReadOnlyDictionary<string, AttackDefinition> attacks,
            SpecialVisualAssets visuals,
            IReadOnlyDictionary<string, GameObject> prefabs,
            PoolCatalog pools,
            UnitCatalog units)
        {
            if (!pools.Validate().IsValid || !units.Validate().IsValid)
            {
                throw new InvalidOperationException(
                    "The Step 13 catalogs are invalid.");
            }

            foreach (SpecialUnitSpec spec in s_Specifications)
            {
                AIUnitDefinition definition = definitions[spec.Id];
                GameObject prefab = prefabs[spec.Id];
                if (!definition.Validate().IsValid ||
                    definition.DefaultAttackDefinition != attacks[spec.Id] ||
                    prefab.GetComponent<UnitController>().Definition !=
                        definition ||
                    prefab.GetComponent<MeleeAttackExecutor>() == null ||
                    PrefabUtility.GetPrefabAssetType(prefab) !=
                        PrefabAssetType.Variant ||
                    !pools.TryGetEntry(
                        definition.PoolId,
                        out PoolCatalogEntry poolEntry) ||
                    poolEntry.Prefab != prefab ||
                    poolEntry.CapacityPolicy !=
                        PoolCapacityPolicy.Expandable ||
                    !units.TryGetDefinition(
                        definition.UnitId,
                        out UnitDefinition catalogDefinition) ||
                    catalogDefinition != definition)
                {
                    throw new InvalidOperationException(
                        $"{spec.DisplayName} content is invalid.");
                }
            }

            GameObject stunner = prefabs["EnemyStunner"];
            StunnerHitPolicy stunnerPolicy =
                stunner.GetComponent<StunnerHitPolicy>();
            Transform rightHand = FindDeepChild(
                stunner.transform,
                "RightHandSocket");
            Transform nestedHammer = rightHand == null ||
                                     rightHand.childCount == 0
                ? null
                : rightHand.GetChild(0);
            if (stunnerPolicy == null ||
                !stunnerPolicy.ValidateConfiguration(out _) ||
                stunner.GetComponent<ImmediateDeathPoolReturn>() == null ||
                nestedHammer == null ||
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                    nestedHammer.gameObject) != visuals.Hammer)
            {
                throw new InvalidOperationException(
                    "Stunner policy or nested hammer composition is invalid.");
            }

            GameObject mini = prefabs["EnemyMiniDivisible"];
            GameObject divisible = prefabs["EnemyDivisible"];
            Transform miniVisual = mini.transform.Find("VisualRoot").GetChild(0);
            Transform divisibleVisual = divisible.transform.Find("VisualRoot")
                .GetChild(0);
            SpawnUnitsOnDeath deathSpawner =
                divisible.GetComponent<SpawnUnitsOnDeath>();
            if (mini.GetComponent<SpawnUnitsOnDeath>() != null ||
                mini.GetComponent<ImmediateDeathPoolReturn>() == null ||
                miniVisual.localScale != Vector3.one * 0.5f ||
                PrefabUtility.GetCorrespondingObjectFromSource(
                    miniVisual.gameObject) != visuals.Divisible ||
                PrefabUtility.GetCorrespondingObjectFromSource(
                    divisibleVisual.gameObject) != visuals.Divisible ||
                deathSpawner == null ||
                deathSpawner.MiniDivisibleDefinition !=
                    definitions["EnemyMiniDivisible"] ||
                divisible.GetComponent<ImmediateDeathPoolReturn>() != null)
            {
                throw new InvalidOperationException(
                    "Divisible/MiniDivisible inheritance or behavior separation is invalid.");
            }

            pools.TryGetEntry(
                definitions["EnemyMiniDivisible"].PoolId,
                out PoolCatalogEntry miniPool);
            if (miniPool.InitialPrewarmCount != 30 ||
                miniPool.MaximumInactiveRetainedCount != 150)
            {
                throw new InvalidOperationException(
                    "MiniDivisible requires the 30/150 pool baseline.");
            }
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

        private static GameObject RequireRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            throw new InvalidOperationException(
                $"CombatSandbox is missing root '{name}'.");
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
            Transform child = parent.Find(childName);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
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

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
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

        private readonly struct SpecialVisualAssets
        {
            public GameObject Hammer { get; }
            public GameObject Stunner { get; }
            public GameObject Divisible { get; }

            public SpecialVisualAssets(
                GameObject hammer,
                GameObject stunner,
                GameObject divisible)
            {
                Hammer = hammer;
                Stunner = stunner;
                Divisible = divisible;
            }
        }

        private sealed class SpecialUnitSpec
        {
            public string Id { get; }
            public string DisplayName { get; }
            public string AttackId { get; }
            public string AttackFileName { get; }
            public string DefinitionFileName { get; }
            public string PrefabFileName { get; }
            public float Health { get; }
            public float MoveSpeed { get; }
            public float TurnSpeed { get; }
            public float ChaseRange { get; }
            public float Damage { get; }
            public float AttackRange { get; }
            public float Cooldown { get; }
            public float Windup { get; }
            public float Recovery { get; }

            public SpecialUnitSpec(
                string id,
                string displayName,
                string attackId,
                string attackFileName,
                string definitionFileName,
                string prefabFileName,
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
                Id = id;
                DisplayName = displayName;
                AttackId = attackId;
                AttackFileName = attackFileName;
                DefinitionFileName = definitionFileName;
                PrefabFileName = prefabFileName;
                Health = health;
                MoveSpeed = moveSpeed;
                TurnSpeed = turnSpeed;
                ChaseRange = chaseRange;
                Damage = damage;
                AttackRange = attackRange;
                Cooldown = cooldown;
                Windup = windup;
                Recovery = recovery;
            }
        }
    }
}
