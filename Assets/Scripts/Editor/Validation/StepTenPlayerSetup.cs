using System;
using System.Collections.Generic;
using System.IO;
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
using MonstersVsZombies.Units.Lifecycle;
using MonstersVsZombies.Units.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepTenPlayerSetup
    {
        private const string k_InputAssetPath =
            "Assets/InputSystem_Actions.inputactions";
        private const string k_InputReferenceFolder = "Assets/Data/Input";
        private const string k_AttackFolder = "Assets/Data/Attacks";
        private const string k_WeaponFolder = "Assets/Data/Weapons";
        private const string k_UnitFolder = "Assets/Data/Units";
        private const string k_WeaponPrefabFolder = "Assets/Prefabs/Weapons";
        private const string k_UnitPrefabFolder = "Assets/Prefabs/Units";
        private const string k_TestFixtureFolder =
            "Assets/Tests/Fixtures/StepTen";
        private const string k_BaseUnitPrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Base.prefab";
        private const string k_PlayerBasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Player_Base.prefab";
        private const string k_PlayerPrefabPath =
            "Assets/Prefabs/Units/PF_Player.prefab";
        private const string k_StationaryEnemyPrefabPath =
            "Assets/Tests/Fixtures/StepTen/PF_Test_StationaryEnemy.prefab";
        private const string k_PoolCatalogPath =
            "Assets/Data/Catalogs/PC_ProjectilePools.asset";
        private const string k_UnitCatalogPath =
            "Assets/Data/Catalogs/UC_CombatSandbox.asset";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";
        private const int k_HitscanCastCapacity = 32;

        private static readonly PoolId s_playerPoolId = new PoolId("Player");
        private static readonly PoolId s_stationaryEnemyPoolId =
            new PoolId("StationaryEnemyTarget");
        private static readonly PoolId s_beamPoolId = new PoolId("LaserBeam");

        [MenuItem("Tools/Monsters vs Zombies/Step 10/Create and Verify Player")]
        public static void CreateAndVerifyPlayer()
        {
            EnsureFolder(k_InputReferenceFolder);
            EnsureFolder(k_AttackFolder);
            EnsureFolder(k_WeaponFolder);
            EnsureFolder(k_UnitFolder);
            EnsureFolder(k_WeaponPrefabFolder);
            EnsureFolder(k_UnitPrefabFolder);
            EnsureFolder(k_TestFixtureFolder);

            PlayerInputAssets inputAssets = ConfigureInputAsset();
            PlayerDefinitionAssets definitionAssets =
                CreateDefinitionAssets();
            ConfigureCommonTargetGeometry();
            PlayerPrefabAssets prefabAssets = CreatePlayerPrefabs(
                inputAssets,
                definitionAssets);
            PoolCatalog poolCatalog = UpdatePoolCatalog(prefabAssets);
            UnitCatalog unitCatalog = UpdateUnitCatalog(definitionAssets);
            UpdateCombatSandbox(
                poolCatalog,
                unitCatalog,
                definitionAssets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyAssets(
                inputAssets,
                definitionAssets,
                prefabAssets,
                poolCatalog,
                unitCatalog);
            Debug.Log(
                "[StepTenPlayerSetup] Created and verified Player input, movement, weapons, prefabs, catalogs, HUD, on-screen stick, and CombatSandbox bindings.");
        }

        private static void ConfigureCommonTargetGeometry()
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(
                k_BaseUnitPrefabPath);
            try
            {
                RequireChild(instance, "Hurtbox").localPosition =
                    new Vector3(0f, 0.5f, 0f);
                RequireChild(instance, "Sockets/AttackOrigin").localPosition =
                    new Vector3(0f, 0.5f, 0f);
                PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    k_BaseUnitPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static PlayerInputAssets ConfigureInputAsset()
        {
            InputActionAsset inputAsset =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_InputAssetPath);
            if (inputAsset == null)
            {
                throw new InvalidOperationException(
                    "The existing InputSystem_Actions asset is missing.");
            }

            InputActionMap existingPlayerMap =
                inputAsset.FindActionMap("Player", false);
            if (existingPlayerMap != null)
            {
                inputAsset.RemoveActionMap(existingPlayerMap);
            }

            InputActionMap playerMap = new InputActionMap("Player");
            InputAction moveAction = playerMap.AddAction(
                "Move",
                InputActionType.Value);
            moveAction.expectedControlType = "Vector2";
            moveAction.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", groups: "Keyboard&Mouse")
                .With("Down", "<Keyboard>/s", groups: "Keyboard&Mouse")
                .With("Left", "<Keyboard>/a", groups: "Keyboard&Mouse")
                .With("Right", "<Keyboard>/d", groups: "Keyboard&Mouse");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", groups: "Keyboard&Mouse")
                .With("Down", "<Keyboard>/downArrow", groups: "Keyboard&Mouse")
                .With("Left", "<Keyboard>/leftArrow", groups: "Keyboard&Mouse")
                .With("Right", "<Keyboard>/rightArrow", groups: "Keyboard&Mouse");

            InputAction previousWeaponAction = playerMap.AddAction(
                "PreviousWeapon",
                InputActionType.Button);
            previousWeaponAction.expectedControlType = "Button";
            previousWeaponAction.AddBinding(
                "<Keyboard>/q",
                groups: "Keyboard&Mouse");
            InputAction nextWeaponAction = playerMap.AddAction(
                "NextWeapon",
                InputActionType.Button);
            nextWeaponAction.expectedControlType = "Button";
            nextWeaponAction.AddBinding(
                "<Keyboard>/e",
                groups: "Keyboard&Mouse");
            inputAsset.AddActionMap(playerMap);
            string inputAssetJson = inputAsset.ToJson();
            File.WriteAllText(
                Path.GetFullPath(k_InputAssetPath),
                inputAssetJson);
            AssetDatabase.ImportAsset(
                k_InputAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                k_InputAssetPath);
            playerMap = inputAsset.FindActionMap("Player", true);
            moveAction = playerMap.FindAction("Move", true);
            previousWeaponAction = playerMap.FindAction(
                "PreviousWeapon",
                true);
            nextWeaponAction = playerMap.FindAction("NextWeapon", true);

            InputActionReference moveReference = CreateActionReference(
                $"{k_InputReferenceFolder}/IAR_Move.asset",
                moveAction);
            InputActionReference previousReference = CreateActionReference(
                $"{k_InputReferenceFolder}/IAR_PreviousWeapon.asset",
                previousWeaponAction);
            InputActionReference nextReference = CreateActionReference(
                $"{k_InputReferenceFolder}/IAR_NextWeapon.asset",
                nextWeaponAction);
            return new PlayerInputAssets(
                inputAsset,
                moveReference,
                previousReference,
                nextReference);
        }

        private static InputActionReference CreateActionReference(
            string assetPath,
            InputAction action)
        {
            InputActionReference actionReference =
                AssetDatabase.LoadAssetAtPath<InputActionReference>(assetPath);
            if (actionReference == null)
            {
                actionReference = ScriptableObject.CreateInstance<InputActionReference>();
                AssetDatabase.CreateAsset(actionReference, assetPath);
            }

            actionReference.Set(action);
            actionReference.name = Path.GetFileNameWithoutExtension(assetPath);
            EditorUtility.SetDirty(actionReference);
            return actionReference;
        }

        private static PlayerDefinitionAssets CreateDefinitionAssets()
        {
            ProjectileDefinition bullet =
                LoadRequiredAsset<ProjectileDefinition>(
                    "Assets/Data/Projectiles/PD_Bullet.asset");
            ProjectileDefinition grenade =
                LoadRequiredAsset<ProjectileDefinition>(
                    "Assets/Data/Projectiles/PD_Grenade.asset");
            AttackDefinition basicMelee =
                LoadRequiredAsset<AttackDefinition>(
                    "Assets/Data/Attacks/AD_BasicMelee.asset");
            AttackDefinition pistolAttack = CreateAttackDefinition(
                $"{k_AttackFolder}/AD_PlayerPistol.asset",
                "PlayerPistol",
                10f,
                10f,
                0.5f,
                0.05f,
                0.05f,
                AttackDeliveryType.Projectile,
                bullet,
                "Direct");
            AttackDefinition grenadeAttack =
                LoadRequiredAsset<AttackDefinition>(
                    "Assets/Data/Attacks/AD_PlayerGrenadeGun.asset");
            AttackDefinition spaceGunAttack =
                LoadRequiredAsset<AttackDefinition>(
                    "Assets/Data/Attacks/AD_PlayerSpaceGun.asset");

            GameObject pistolVisual = CreateWeaponVisual(
                $"{k_WeaponPrefabFolder}/PF_Weapon_Pistol.prefab",
                "PF_Weapon_Pistol",
                new Vector3(0.65f, 0.22f, 0.18f));
            GameObject grenadeVisual = CreateWeaponVisual(
                $"{k_WeaponPrefabFolder}/PF_Weapon_GrenadeGun.prefab",
                "PF_Weapon_GrenadeGun",
                new Vector3(0.8f, 0.32f, 0.32f));
            GameObject spaceGunVisual = CreateWeaponVisual(
                $"{k_WeaponPrefabFolder}/PF_Weapon_SpaceGun.prefab",
                "PF_Weapon_SpaceGun",
                new Vector3(0.9f, 0.2f, 0.25f));

            WeaponDefinition pistolWeapon = CreateWeaponDefinition(
                $"{k_WeaponFolder}/WD_Pistol.asset",
                "Pistol",
                "Pistol",
                pistolAttack,
                pistolVisual);
            WeaponDefinition grenadeWeapon = CreateWeaponDefinition(
                $"{k_WeaponFolder}/WD_GrenadeGun.asset",
                "GrenadeGun",
                "GrenadeGun",
                grenadeAttack,
                grenadeVisual);
            WeaponDefinition spaceGunWeapon = CreateWeaponDefinition(
                $"{k_WeaponFolder}/WD_SpaceGun.asset",
                "SpaceGun",
                "SpaceGun",
                spaceGunAttack,
                spaceGunVisual);

            PlayerUnitDefinition playerDefinition =
                LoadOrCreateAsset<PlayerUnitDefinition>(
                    $"{k_UnitFolder}/UD_Player.asset");
            ConfigureUnitDefinition(
                playerDefinition,
                "Player",
                "Player",
                UnitFaction.Player,
                100f,
                6f,
                720f,
                s_playerPoolId);

            AIUnitDefinition stationaryEnemyDefinition =
                LoadOrCreateAsset<AIUnitDefinition>(
                    $"{k_TestFixtureFolder}/UD_Test_StationaryEnemy.asset");
            ConfigureUnitDefinition(
                stationaryEnemyDefinition,
                "StationaryEnemyTarget",
                "Stationary Enemy Target",
                UnitFaction.Enemy,
                60f,
                3.5f,
                540f,
                s_stationaryEnemyPoolId);
            SetAutoProperty(
                stationaryEnemyDefinition,
                nameof(AIUnitDefinition.ChaseRange),
                12f);
            SetAutoProperty(
                stationaryEnemyDefinition,
                nameof(AIUnitDefinition.DefaultAttackDefinition),
                basicMelee);
            EditorUtility.SetDirty(stationaryEnemyDefinition);

            return new PlayerDefinitionAssets(
                playerDefinition,
                stationaryEnemyDefinition,
                pistolWeapon,
                grenadeWeapon,
                spaceGunWeapon,
                basicMelee);
        }

        private static AttackDefinition CreateAttackDefinition(
            string assetPath,
            string attackId,
            float damage,
            float attackRange,
            float cooldown,
            float windup,
            float recovery,
            AttackDeliveryType deliveryType,
            ProjectileDefinition projectileDefinition,
            string damageCategory)
        {
            AttackDefinition definition =
                LoadOrCreateAsset<AttackDefinition>(assetPath);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AttackId),
                new AttackId(attackId));
            SetAutoProperty(definition, nameof(AttackDefinition.Damage), damage);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AttackRange),
                attackRange);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.CooldownDuration),
                cooldown);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.WindupDuration),
                windup);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.RecoveryDuration),
                recovery);
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
                new AcceptedHitEffectConfiguration(
                    StatusEffectType.None,
                    0f));
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.DamageCategoryId),
                new DamageCategoryId(damageCategory));
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static WeaponDefinition CreateWeaponDefinition(
            string assetPath,
            string weaponId,
            string displayName,
            AttackDefinition attackDefinition,
            GameObject weaponVisual)
        {
            WeaponDefinition definition =
                LoadOrCreateAsset<WeaponDefinition>(assetPath);
            SetAutoProperty(
                definition,
                nameof(WeaponDefinition.WeaponId),
                new WeaponId(weaponId));
            SetAutoProperty(
                definition,
                nameof(WeaponDefinition.DisplayName),
                displayName);
            SetAutoProperty(
                definition,
                nameof(WeaponDefinition.AttackDefinition),
                attackDefinition);
            SetAutoProperty(
                definition,
                nameof(WeaponDefinition.WeaponVisualPrefab),
                weaponVisual);
            SetAutoProperty(
                definition,
                nameof(WeaponDefinition.MuzzleSocketName),
                "AttackOrigin");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureUnitDefinition(
            UnitDefinition definition,
            string unitId,
            string displayName,
            UnitFaction faction,
            float maximumHealth,
            float moveSpeed,
            float turnSpeed,
            PoolId poolId)
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
                maximumHealth);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.MoveSpeed),
                moveSpeed);
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.TurnSpeed),
                turnSpeed);
            SetAutoProperty(definition, nameof(UnitDefinition.PoolId), poolId);
            EditorUtility.SetDirty(definition);
        }

        private static GameObject CreateWeaponVisual(
            string assetPath,
            string objectName,
            Vector3 scale)
        {
            GameObject root = new GameObject(objectName);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static PlayerPrefabAssets CreatePlayerPrefabs(
            PlayerInputAssets inputAssets,
            PlayerDefinitionAssets definitionAssets)
        {
            GameObject basePrefab =
                LoadRequiredAsset<GameObject>(k_BaseUnitPrefabPath);
            GameObject playerBasePrefab = CreatePlayerBasePrefab(
                basePrefab,
                inputAssets);
            GameObject playerPrefab = CreateConcretePlayerPrefab(
                playerBasePrefab,
                definitionAssets);
            GameObject stationaryEnemyPrefab = CreateStationaryEnemyPrefab(
                basePrefab,
                definitionAssets);
            return new PlayerPrefabAssets(
                playerBasePrefab,
                playerPrefab,
                stationaryEnemyPrefab);
        }

        private static GameObject CreatePlayerBasePrefab(
            GameObject basePrefab,
            PlayerInputAssets inputAssets)
        {
            GameObject instance = InstantiatePrefab(basePrefab);
            instance.name = "PF_Unit_Player_Base";
            instance.SetActive(false);
            instance.layer = LayerMask.NameToLayer("UnitBody");

            CharacterController characterController =
                instance.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.height = 2f;
            characterController.radius = 0.5f;
            PlayerInputReader inputReader =
                instance.AddComponent<PlayerInputReader>();
            SetAutoProperty(
                inputReader,
                nameof(PlayerInputReader.MoveAction),
                inputAssets.MoveReference);
            SetAutoProperty(
                inputReader,
                nameof(PlayerInputReader.PreviousWeaponAction),
                inputAssets.PreviousWeaponReference);
            SetAutoProperty(
                inputReader,
                nameof(PlayerInputReader.NextWeaponAction),
                inputAssets.NextWeaponReference);
            instance.AddComponent<PlayerMotor>();
            PlayerWeaponController weaponController =
                instance.AddComponent<PlayerWeaponController>();
            ProjectileAttackExecutor projectileExecutor =
                instance.AddComponent<ProjectileAttackExecutor>();
            GrenadeAttackExecutor grenadeExecutor =
                instance.AddComponent<GrenadeAttackExecutor>();
            HitscanAttackExecutor hitscanExecutor =
                instance.AddComponent<HitscanAttackExecutor>();
            if (!hitscanExecutor.InitializeCastCapacity(k_HitscanCastCapacity))
            {
                throw new InvalidOperationException(
                    "Could not configure Player hitscan capacity.");
            }

            Transform attackOrigin = RequireChild(instance, "Sockets/AttackOrigin");
            attackOrigin.localPosition = new Vector3(0f, 0.5f, 0f);
            PlayerCombatController combatController =
                instance.AddComponent<PlayerCombatController>();
            SetAutoProperty(
                combatController,
                nameof(PlayerCombatController.ProjectileExecutor),
                projectileExecutor);
            SetAutoProperty(
                combatController,
                nameof(PlayerCombatController.GrenadeExecutor),
                grenadeExecutor);
            SetAutoProperty(
                combatController,
                nameof(PlayerCombatController.HitscanExecutor),
                hitscanExecutor);
            SetAutoProperty(
                combatController,
                nameof(PlayerCombatController.AttackOrigin),
                attackOrigin);
            SetAutoProperty(
                combatController,
                nameof(PlayerCombatController.BeamPoolId),
                s_beamPoolId);
            SetAutoProperty(
                combatController,
                nameof(PlayerCombatController.HitscanCastCapacity),
                k_HitscanCastCapacity);
            instance.AddComponent<ImmediateDeathPoolReturn>();

            AttackController attackController =
                instance.GetComponent<AttackController>();
            SetField(
                attackController,
                "_executorBindings",
                new[]
                {
                    new AttackExecutorBinding(
                        AttackDeliveryType.Projectile,
                        projectileExecutor),
                    new AttackExecutorBinding(
                        AttackDeliveryType.Grenade,
                        grenadeExecutor),
                    new AttackExecutorBinding(
                        AttackDeliveryType.Hitscan,
                        hitscanExecutor)
                });
            SetField(
                weaponController,
                "_weaponSlots",
                Array.Empty<PlayerWeaponSlot>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                k_PlayerBasePrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateConcretePlayerPrefab(
            GameObject playerBasePrefab,
            PlayerDefinitionAssets definitionAssets)
        {
            GameObject instance = InstantiatePrefab(playerBasePrefab);
            instance.name = "PF_Player";
            instance.SetActive(false);
            SetAutoProperty(
                instance.GetComponent<UnitController>(),
                nameof(UnitController.Definition),
                definitionAssets.PlayerDefinition);
            SetAutoProperty(
                instance.GetComponent<AttackController>(),
                nameof(AttackController.AttackDefinition),
                definitionAssets.PistolWeapon.AttackDefinition);

            Transform visualRoot = RequireChild(instance, "VisualRoot");
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "PlayerPlaceholder";
            body.transform.SetParent(visualRoot, false);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            Transform weaponSocket = RequireChild(instance, "Sockets/WeaponSocket");
            GameObject pistolVisual = InstantiateNestedWeapon(
                definitionAssets.PistolWeapon.WeaponVisualPrefab,
                weaponSocket,
                "PistolVisual");
            GameObject grenadeVisual = InstantiateNestedWeapon(
                definitionAssets.GrenadeWeapon.WeaponVisualPrefab,
                weaponSocket,
                "GrenadeGunVisual");
            GameObject spaceGunVisual = InstantiateNestedWeapon(
                definitionAssets.SpaceGunWeapon.WeaponVisualPrefab,
                weaponSocket,
                "SpaceGunVisual");
            SetField(
                instance.GetComponent<PlayerWeaponController>(),
                "_weaponSlots",
                new[]
                {
                    new PlayerWeaponSlot(
                        definitionAssets.PistolWeapon,
                        pistolVisual),
                    new PlayerWeaponSlot(
                        definitionAssets.GrenadeWeapon,
                        grenadeVisual),
                    new PlayerWeaponSlot(
                        definitionAssets.SpaceGunWeapon,
                        spaceGunVisual)
                });
            pistolVisual.SetActive(true);
            grenadeVisual.SetActive(false);
            spaceGunVisual.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                k_PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateStationaryEnemyPrefab(
            GameObject basePrefab,
            PlayerDefinitionAssets definitionAssets)
        {
            GameObject instance = InstantiatePrefab(basePrefab);
            instance.name = "PF_Test_StationaryEnemy";
            instance.SetActive(false);
            SetAutoProperty(
                instance.GetComponent<UnitController>(),
                nameof(UnitController.Definition),
                definitionAssets.StationaryEnemyDefinition);
            AttackController attackController =
                instance.GetComponent<AttackController>();
            SetAutoProperty(
                attackController,
                nameof(AttackController.AttackDefinition),
                definitionAssets.BasicMeleeAttack);
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
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "StationaryEnemyPlaceholder";
            body.transform.SetParent(visualRoot, false);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                k_StationaryEnemyPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject InstantiateNestedWeapon(
            GameObject weaponPrefab,
            Transform parent,
            string objectName)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(
                weaponPrefab,
                parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not nest weapon visual '{weaponPrefab.name}'.");
            }

            instance.name = objectName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        private static PoolCatalog UpdatePoolCatalog(
            PlayerPrefabAssets prefabAssets)
        {
            PoolCatalog poolCatalog =
                LoadRequiredAsset<PoolCatalog>(k_PoolCatalogPath);
            List<PoolCatalogEntry> entries = new List<PoolCatalogEntry>();
            for (int entryIndex = 0;
                 entryIndex < poolCatalog.Count;
                 entryIndex++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(entryIndex);
                if (entry != null && entry.PoolId != s_playerPoolId &&
                    entry.PoolId != s_stationaryEnemyPoolId)
                {
                    entries.Add(entry);
                }
            }

            entries.Add(CreatePoolEntry(
                s_playerPoolId,
                prefabAssets.PlayerPrefab,
                1,
                1));
            entries.Add(CreatePoolEntry(
                s_stationaryEnemyPoolId,
                prefabAssets.StationaryEnemyPrefab,
                1,
                1));
            SetField(poolCatalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(poolCatalog);
            return poolCatalog;
        }

        private static PoolCatalogEntry CreatePoolEntry(
            PoolId poolId,
            GameObject prefab,
            int initialPrewarmCount,
            int maximumInactiveRetainedCount)
        {
            PoolCatalogEntry entry = new PoolCatalogEntry();
            SetAutoProperty(entry, nameof(PoolCatalogEntry.PoolId), poolId);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.Prefab), prefab);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.InitialPrewarmCount),
                initialPrewarmCount);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                maximumInactiveRetainedCount);
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
            PlayerDefinitionAssets definitionAssets)
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
                    entry.UnitId != definitionAssets.PlayerDefinition.UnitId &&
                    entry.UnitId !=
                        definitionAssets.StationaryEnemyDefinition.UnitId)
                {
                    entries.Add(entry);
                }
            }

            entries.Add(CreateUnitCatalogEntry(
                definitionAssets.PlayerDefinition));
            entries.Add(CreateUnitCatalogEntry(
                definitionAssets.StationaryEnemyDefinition));
            SetField(unitCatalog, "_entries", entries.ToArray());
            EditorUtility.SetDirty(unitCatalog);
            return unitCatalog;
        }

        private static UnitCatalogEntry CreateUnitCatalogEntry(
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
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog,
            PlayerDefinitionAssets definitionAssets)
        {
            Scene scene = EditorSceneManager.OpenScene(
                k_ScenePath,
                OpenSceneMode.Single);
            GameObject systemsRoot = RequireRoot(scene, "__Systems");
            GameObject spawnPointsRoot = RequireRoot(scene, "SpawnPoints");
            GameObject cameraRigRoot = RequireRoot(scene, "CameraRig");
            GameObject uiRoot = RequireRoot(scene, "UI");

            PoolManager poolManager = RequireChildComponent<PoolManager>(
                systemsRoot,
                "PoolManager");
            SpawnManager spawnManager = RequireChildComponent<SpawnManager>(
                systemsRoot,
                "SpawnManager");
            InteractionSystem interactionSystem =
                RequireChildComponent<InteractionSystem>(
                    systemsRoot,
                    "InteractionSystem");
            UnitRegistry unitRegistry = RequireChildComponent<UnitRegistry>(
                systemsRoot,
                "UnitRegistry");
            InitialSandboxSpawner initialSpawner =
                RequireChildComponent<InitialSandboxSpawner>(
                    systemsRoot,
                    "InitialSandboxSpawner");
            CombatSandboxBootstrap bootstrap =
                RequireChildComponent<CombatSandboxBootstrap>(
                    systemsRoot,
                    "CombatSandboxBootstrap");
            SpawnPointGroup playerSpawns = RequireChildComponent<SpawnPointGroup>(
                spawnPointsRoot,
                "PlayerSpawn");
            SpawnPointGroup allySpawns = RequireChildComponent<SpawnPointGroup>(
                spawnPointsRoot,
                "AllySpawnPoints");
            SpawnPointGroup enemySpawns = RequireChildComponent<SpawnPointGroup>(
                spawnPointsRoot,
                "EnemySpawnPoints");

            Camera camera = RequireChildComponent<Camera>(
                cameraRigRoot,
                "MainCamera");
            CameraFollowController cameraFollow =
                GetOrAddComponent<CameraFollowController>(camera.gameObject);
            if (!playerSpawns.TryGetPoint(0, out Pose playerPose))
            {
                throw new InvalidOperationException(
                    "CombatSandbox requires its authored Player spawn point.");
            }

            SetAutoProperty(
                cameraFollow,
                nameof(CameraFollowController.Offset),
                camera.transform.position - playerPose.position);
            PlayerHudController hudController = CreateGameplayUi(uiRoot);

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
                definitionAssets.PlayerDefinition);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.StationaryEnemyDefinition),
                definitionAssets.StationaryEnemyDefinition);
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
                playerSpawns);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.AllySpawnPoints),
                allySpawns);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.EnemySpawnPoints),
                enemySpawns);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.CameraFollowController),
                cameraFollow);
            SetAutoProperty(
                bootstrap,
                nameof(CombatSandboxBootstrap.PlayerHudController),
                hudController);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, k_ScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save the updated CombatSandbox scene.");
            }
        }

        private static PlayerHudController CreateGameplayUi(GameObject uiRoot)
        {
            for (int childIndex = uiRoot.transform.childCount - 1;
                 childIndex >= 0;
                 childIndex--)
            {
                UnityEngine.Object.DestroyImmediate(
                    uiRoot.transform.GetChild(childIndex).gameObject);
            }

            GameObject canvasObject = new GameObject(
                "GameplayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(uiRoot.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Text healthText = CreateText(
                canvasObject.transform,
                "PlayerHealth",
                new Vector2(20f, -20f),
                TextAnchor.UpperLeft);
            Text weaponText = CreateText(
                canvasObject.transform,
                "CurrentWeapon",
                new Vector2(20f, -60f),
                TextAnchor.UpperLeft);
            CreateOnScreenStick(canvasObject.transform);

            PlayerHudController hudController =
                canvasObject.AddComponent<PlayerHudController>();
            SetAutoProperty(
                hudController,
                nameof(PlayerHudController.HealthText),
                healthText);
            SetAutoProperty(
                hudController,
                nameof(PlayerHudController.WeaponText),
                weaponText);

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(uiRoot.transform, false);
            eventSystemObject.GetComponent<InputSystemUIInputModule>()
                .AssignDefaultActions();
            return hudController;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            Vector2 anchoredPosition,
            TextAnchor alignment)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rectTransform =
                textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(480f, 40f);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.black;
            text.alignment = alignment;
            text.text = objectName == "PlayerHealth"
                ? "Health: --"
                : "Weapon: --";
            return text;
        }

        private static void CreateOnScreenStick(Transform parent)
        {
            GameObject backgroundObject = new GameObject(
                "OnScreenStick",
                typeof(RectTransform),
                typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            RectTransform backgroundRect =
                backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.zero;
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(150f, 150f);
            backgroundRect.sizeDelta = new Vector2(180f, 180f);
            backgroundObject.GetComponent<Image>().color =
                new Color(0f, 0f, 0f, 0.25f);

            GameObject handleObject = new GameObject(
                "Handle",
                typeof(RectTransform),
                typeof(Image),
                typeof(OnScreenStick));
            handleObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform handleRect =
                handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(80f, 80f);
            handleObject.GetComponent<Image>().color =
                new Color(1f, 1f, 1f, 0.75f);
            OnScreenStick onScreenStick =
                handleObject.GetComponent<OnScreenStick>();
            onScreenStick.controlPath = "<Gamepad>/leftStick";
            onScreenStick.movementRange = 50f;
        }

        private static void VerifyAssets(
            PlayerInputAssets inputAssets,
            PlayerDefinitionAssets definitionAssets,
            PlayerPrefabAssets prefabAssets,
            PoolCatalog poolCatalog,
            UnitCatalog unitCatalog)
        {
            InputActionMap playerMap =
                inputAssets.InputAsset.FindActionMap("Player", true);
            string[] actionNames =
            {
                "Move",
                "PreviousWeapon",
                "NextWeapon"
            };
            if (playerMap.actions.Count != actionNames.Length)
            {
                throw new InvalidOperationException(
                    "The Player input map contains template or missing actions.");
            }

            foreach (string actionName in actionNames)
            {
                if (playerMap.FindAction(actionName, false) == null)
                {
                    throw new InvalidOperationException(
                        $"The Player input map is missing '{actionName}'.");
                }
            }

            if (PrefabUtility.GetPrefabAssetType(prefabAssets.PlayerBasePrefab) !=
                    PrefabAssetType.Variant ||
                PrefabUtility.GetPrefabAssetType(prefabAssets.PlayerPrefab) !=
                    PrefabAssetType.Variant ||
                prefabAssets.PlayerPrefab.GetComponent<CharacterController>() ==
                    null ||
                prefabAssets.PlayerPrefab.GetComponent<PlayerInputReader>() ==
                    null ||
                prefabAssets.PlayerPrefab.GetComponent<PlayerMotor>() == null ||
                prefabAssets.PlayerPrefab.GetComponent<PlayerWeaponController>() ==
                    null ||
                prefabAssets.PlayerPrefab.GetComponent<PlayerCombatController>() ==
                    null ||
                prefabAssets.PlayerPrefab.GetComponent<ProjectileAttackExecutor>() ==
                    null ||
                prefabAssets.PlayerPrefab.GetComponent<GrenadeAttackExecutor>() ==
                    null ||
                prefabAssets.PlayerPrefab.GetComponent<HitscanAttackExecutor>() ==
                    null)
            {
                throw new InvalidOperationException(
                    "The Player prefab variant chain or capability matrix is invalid.");
            }

            PlayerWeaponController weaponController =
                prefabAssets.PlayerPrefab.GetComponent<PlayerWeaponController>();
            PlayerCombatController combatController =
                prefabAssets.PlayerPrefab.GetComponent<PlayerCombatController>();
            string weaponFailure = string.Empty;
            string combatFailure = string.Empty;
            if (weaponController.WeaponCount != 3 ||
                !weaponController.ValidateConfiguration(out weaponFailure) ||
                !combatController.ValidateConfiguration(out combatFailure))
            {
                throw new InvalidOperationException(
                    $"Player weapon/combat validation failed: {weaponFailure} {combatFailure}");
            }

            if (!definitionAssets.PlayerDefinition.Validate().IsValid ||
                !definitionAssets.StationaryEnemyDefinition.Validate().IsValid ||
                !poolCatalog.TryGetEntry(s_playerPoolId, out _) ||
                !poolCatalog.TryGetEntry(s_stationaryEnemyPoolId, out _) ||
                !unitCatalog.TryGetDefinition(
                    definitionAssets.PlayerDefinition.UnitId,
                    out UnitDefinition playerDefinition) ||
                playerDefinition != definitionAssets.PlayerDefinition ||
                !unitCatalog.TryGetDefinition(
                    definitionAssets.StationaryEnemyDefinition.UnitId,
                    out UnitDefinition enemyDefinition) ||
                enemyDefinition != definitionAssets.StationaryEnemyDefinition)
            {
                throw new InvalidOperationException(
                    "Player or stationary target definitions and catalog entries are invalid.");
            }

            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            CombatSandboxBootstrap bootstrap = RequireChildComponent<CombatSandboxBootstrap>(
                RequireRoot(scene, "__Systems"),
                "CombatSandboxBootstrap");
            OnScreenStick onScreenStick =
                RequireRoot(scene, "UI")
                    .GetComponentInChildren<OnScreenStick>(true);
            if (bootstrap.PlayerDefinition !=
                    definitionAssets.PlayerDefinition ||
                bootstrap.StationaryEnemyDefinition !=
                    definitionAssets.StationaryEnemyDefinition ||
                bootstrap.CameraFollowController == null ||
                bootstrap.PlayerHudController == null ||
                onScreenStick == null ||
                onScreenStick.controlPath != "<Gamepad>/leftStick")
            {
                throw new InvalidOperationException(
                    "CombatSandbox Player bootstrap, camera, HUD, or on-screen stick binding is invalid.");
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

        private static Transform RequireChild(GameObject root, string path)
        {
            Transform child = root.transform.Find(path);
            if (child == null)
            {
                throw new InvalidOperationException(
                    $"'{root.name}' is missing child '{path}'.");
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

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component == null
                ? gameObject.AddComponent<T>()
                : component;
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

        private readonly struct PlayerInputAssets
        {
            public InputActionAsset InputAsset { get; }
            public InputActionReference MoveReference { get; }
            public InputActionReference PreviousWeaponReference { get; }
            public InputActionReference NextWeaponReference { get; }

            public PlayerInputAssets(
                InputActionAsset inputAsset,
                InputActionReference moveReference,
                InputActionReference previousWeaponReference,
                InputActionReference nextWeaponReference)
            {
                InputAsset = inputAsset;
                MoveReference = moveReference;
                PreviousWeaponReference = previousWeaponReference;
                NextWeaponReference = nextWeaponReference;
            }
        }

        private readonly struct PlayerDefinitionAssets
        {
            public PlayerUnitDefinition PlayerDefinition { get; }
            public AIUnitDefinition StationaryEnemyDefinition { get; }
            public WeaponDefinition PistolWeapon { get; }
            public WeaponDefinition GrenadeWeapon { get; }
            public WeaponDefinition SpaceGunWeapon { get; }
            public AttackDefinition BasicMeleeAttack { get; }

            public PlayerDefinitionAssets(
                PlayerUnitDefinition playerDefinition,
                AIUnitDefinition stationaryEnemyDefinition,
                WeaponDefinition pistolWeapon,
                WeaponDefinition grenadeWeapon,
                WeaponDefinition spaceGunWeapon,
                AttackDefinition basicMeleeAttack)
            {
                PlayerDefinition = playerDefinition;
                StationaryEnemyDefinition = stationaryEnemyDefinition;
                PistolWeapon = pistolWeapon;
                GrenadeWeapon = grenadeWeapon;
                SpaceGunWeapon = spaceGunWeapon;
                BasicMeleeAttack = basicMeleeAttack;
            }
        }

        private readonly struct PlayerPrefabAssets
        {
            public GameObject PlayerBasePrefab { get; }
            public GameObject PlayerPrefab { get; }
            public GameObject StationaryEnemyPrefab { get; }

            public PlayerPrefabAssets(
                GameObject playerBasePrefab,
                GameObject playerPrefab,
                GameObject stationaryEnemyPrefab)
            {
                PlayerBasePrefab = playerBasePrefab;
                PlayerPrefab = playerPrefab;
                StationaryEnemyPrefab = stationaryEnemyPrefab;
            }
        }
    }
}
