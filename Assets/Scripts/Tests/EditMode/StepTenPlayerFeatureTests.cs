using System.Collections.Generic;
using System.Linq;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.Projectiles;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepTenPlayerAssetTests
    {
        private const string k_InputAssetPath =
            "Assets/InputSystem_Actions.inputactions";
        private const string k_PlayerPrefabPath =
            "Assets/Prefabs/Units/PF_Player.prefab";
        private const string k_PlayerBasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Player_Base.prefab";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";

        [Test]
        public void PlayerInputMap_HasOnlyPurposeBuiltActionsAndBindings()
        {
            InputActionAsset inputAsset =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_InputAssetPath);
            InputActionMap playerMap = inputAsset.FindActionMap("Player", true);

            CollectionAssert.AreEquivalent(
                new[] { "Move", "PreviousWeapon", "NextWeapon" },
                playerMap.actions.Select(action => action.name).ToArray());
            InputAction moveAction = playerMap.FindAction("Move", true);
            InputAction previousAction =
                playerMap.FindAction("PreviousWeapon", true);
            InputAction nextAction = playerMap.FindAction("NextWeapon", true);
            string[] movePaths = moveAction.bindings
                .Select(binding => binding.path)
                .ToArray();

            CollectionAssert.Contains(movePaths, "<Gamepad>/leftStick");
            CollectionAssert.Contains(movePaths, "<Keyboard>/w");
            CollectionAssert.Contains(movePaths, "<Keyboard>/a");
            CollectionAssert.Contains(movePaths, "<Keyboard>/s");
            CollectionAssert.Contains(movePaths, "<Keyboard>/d");
            CollectionAssert.Contains(movePaths, "<Keyboard>/upArrow");
            CollectionAssert.Contains(movePaths, "<Keyboard>/downArrow");
            CollectionAssert.Contains(movePaths, "<Keyboard>/leftArrow");
            CollectionAssert.Contains(movePaths, "<Keyboard>/rightArrow");
            Assert.That(previousAction.bindings.Count, Is.EqualTo(1));
            Assert.That(previousAction.bindings[0].path,
                Is.EqualTo("<Keyboard>/q"));
            Assert.That(nextAction.bindings.Count, Is.EqualTo(1));
            Assert.That(nextAction.bindings[0].path,
                Is.EqualTo("<Keyboard>/e"));
            Assert.That(playerMap.FindAction("Interact", false), Is.Null);
            Assert.That(playerMap.FindAction("Previous", false), Is.Null);
            Assert.That(playerMap.FindAction("Next", false), Is.Null);
        }

        [Test]
        public void PlayerDefinitions_UseExactTemporarySandboxTuning()
        {
            PlayerUnitDefinition player =
                Load<PlayerUnitDefinition>("Assets/Data/Units/UD_Player.asset");
            WeaponDefinition pistol =
                Load<WeaponDefinition>("Assets/Data/Weapons/WD_Pistol.asset");
            WeaponDefinition grenade =
                Load<WeaponDefinition>("Assets/Data/Weapons/WD_GrenadeGun.asset");
            WeaponDefinition spaceGun =
                Load<WeaponDefinition>("Assets/Data/Weapons/WD_SpaceGun.asset");

            Assert.That(player.UnitId, Is.EqualTo(new UnitId("Player")));
            Assert.That(player.Faction, Is.EqualTo(UnitFaction.Player));
            Assert.That(player.MaximumHealth, Is.EqualTo(100f));
            Assert.That(player.MoveSpeed, Is.EqualTo(6f));
            Assert.That(player.TurnSpeed, Is.EqualTo(720f));
            Assert.That(player.PoolId, Is.EqualTo(new PoolId("Player")));
            AssertWeapon(
                pistol,
                "Pistol",
                10f,
                10f,
                0.5f,
                0.05f,
                0.05f,
                AttackDeliveryType.Projectile);
            AssertWeapon(
                grenade,
                "GrenadeGun",
                25f,
                9f,
                1.8f,
                0.25f,
                0.3f,
                AttackDeliveryType.Grenade);
            AssertWeapon(
                spaceGun,
                "SpaceGun",
                18f,
                12f,
                1f,
                0.1f,
                0.15f,
                AttackDeliveryType.Hitscan);
            Assert.That(
                player.GetType().GetProperty("ChaseRange"),
                Is.Null);
            Assert.That(player.Validate().IsValid, Is.True);
        }

        [Test]
        public void PlayerPrefabChain_HasOneOfEachRequiredCapabilityAndNoAi()
        {
            GameObject playerBase = Load<GameObject>(k_PlayerBasePrefabPath);
            GameObject player = Load<GameObject>(k_PlayerPrefabPath);

            Assert.That(
                PrefabUtility.GetPrefabAssetType(playerBase),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(
                PrefabUtility.GetPrefabAssetType(player),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(player.GetComponents<CharacterController>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponents<PlayerInputReader>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponents<PlayerMotor>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponents<PlayerWeaponController>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponents<PlayerCombatController>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponents<ProjectileAttackExecutor>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponents<GrenadeAttackExecutor>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponents<HitscanAttackExecutor>().Length,
                Is.EqualTo(1));
            Assert.That(player.GetComponent<NavMeshAgent>(), Is.Null);
            Transform attackOrigin = player.transform.Find(
                "Sockets/AttackOrigin");
            Assert.That(attackOrigin, Is.Not.Null);
            Assert.That(attackOrigin.localPosition,
                Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
            Assert.That(
                player.transform.Find("Hurtbox").localPosition,
                Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
            PlayerWeaponController weaponController =
                player.GetComponent<PlayerWeaponController>();
            Assert.That(weaponController.ValidateConfiguration(out string failure),
                Is.True,
                failure);
            Assert.That(weaponController.WeaponCount, Is.EqualTo(3));
            Assert.That(
                weaponController.GetWeaponSlot(0).Definition.WeaponId,
                Is.EqualTo(new WeaponId("Pistol")));
            Assert.That(
                weaponController.GetWeaponSlot(1).Definition.WeaponId,
                Is.EqualTo(new WeaponId("GrenadeGun")));
            Assert.That(
                weaponController.GetWeaponSlot(2).Definition.WeaponId,
                Is.EqualTo(new WeaponId("SpaceGun")));
        }

        [Test]
        public void CombatSandbox_BindsPlayerCameraHudAndSharedOnScreenStick()
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
                CombatSandboxBootstrap bootstrap = scene
                    .GetRootGameObjects()
                    .Single(root => root.name == "__Systems")
                    .GetComponentInChildren<CombatSandboxBootstrap>(true);
                GameObject uiRoot = scene.GetRootGameObjects()
                    .Single(root => root.name == "UI");
                OnScreenStick stick =
                    uiRoot.GetComponentInChildren<OnScreenStick>(true);

                Assert.That(bootstrap.PlayerDefinition.UnitId,
                    Is.EqualTo(new UnitId("Player")));
                Assert.That(bootstrap.StationaryEnemyDefinition.Faction,
                    Is.EqualTo(UnitFaction.Enemy));
                Assert.That(bootstrap.CameraFollowController, Is.Not.Null);
                Assert.That(bootstrap.PlayerHudController, Is.Not.Null);
                Assert.That(
                    bootstrap.PlayerHudController.ValidateConfiguration(
                        out string failure),
                    Is.True,
                    failure);
                Assert.That(stick, Is.Not.Null);
                Assert.That(stick.controlPath,
                    Is.EqualTo("<Gamepad>/leftStick"));
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AssertWeapon(
            WeaponDefinition weapon,
            string expectedId,
            float expectedDamage,
            float expectedRange,
            float expectedCooldown,
            float expectedWindup,
            float expectedRecovery,
            AttackDeliveryType expectedDelivery)
        {
            Assert.That(weapon.WeaponId,
                Is.EqualTo(new WeaponId(expectedId)));
            Assert.That(weapon.Validate().IsValid, Is.True);
            Assert.That(weapon.AttackDefinition.Damage,
                Is.EqualTo(expectedDamage));
            Assert.That(weapon.AttackDefinition.AttackRange,
                Is.EqualTo(expectedRange));
            Assert.That(weapon.AttackDefinition.CooldownDuration,
                Is.EqualTo(expectedCooldown));
            Assert.That(weapon.AttackDefinition.WindupDuration,
                Is.EqualTo(expectedWindup));
            Assert.That(weapon.AttackDefinition.RecoveryDuration,
                Is.EqualTo(expectedRecovery));
            Assert.That(weapon.AttackDefinition.DeliveryType,
                Is.EqualTo(expectedDelivery));
            Assert.That(weapon.WeaponVisualPrefab, Is.Not.Null);
            Assert.That(weapon.MuzzleSocketName,
                Is.EqualTo("AttackOrigin"));
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Missing asset: {path}");
            return asset;
        }
    }

    public sealed class StepTenPlayerRuntimeTests : InputTestFixture
    {
        private readonly List<GameObject> _createdObjects =
            new List<GameObject>();
        private readonly List<InputDevice> _createdDevices =
            new List<InputDevice>();

        [TearDown]
        public override void TearDown()
        {
            try
            {
                foreach (InputDevice inputDevice in _createdDevices)
                {
                    if (inputDevice != null && inputDevice.added)
                    {
                        InputSystem.RemoveDevice(inputDevice);
                    }
                }

                _createdDevices.Clear();

                for (int objectIndex = _createdObjects.Count - 1;
                     objectIndex >= 0;
                     objectIndex--)
                {
                    if (_createdObjects[objectIndex] != null)
                    {
                        Object.DestroyImmediate(_createdObjects[objectIndex]);
                    }
                }

                _createdObjects.Clear();
            }
            finally
            {
                base.TearDown();
            }
        }

        [Test]
        public void InputSystem_QAndEWrapWeaponsBothDirections()
        {
            RuntimeFixture fixture = CreateRuntimeFixture();
            UnitController player = fixture.SpawnPlayer(Vector3.zero);
            PlayerWeaponController weapons =
                player.GetComponent<PlayerWeaponController>();
            PlayerInputReader input = player.GetComponent<PlayerInputReader>();
            Keyboard keyboard = AddDevice<Keyboard>();

            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);
            Assert.That(input.IsInputEnabled, Is.True);
            Assert.That(input.PreviousWeaponAction.action.enabled, Is.True);
            PressAndRelease(keyboard.qKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(2));
            Assert.That(weapons.CurrentWeapon.WeaponId,
                Is.EqualTo(new WeaponId("SpaceGun")));
            PressAndRelease(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);
            PressAndRelease(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(1));
            PressAndRelease(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.EqualTo(2));
            PressAndRelease(keyboard.eKey);
            Assert.That(weapons.CurrentWeaponIndex, Is.Zero);
        }

        [Test]
        public void EachWeaponUpdatesRangeAndKeepsExactlyOneExecutorComponent()
        {
            RuntimeFixture fixture = CreateRuntimeFixture();
            UnitController player = fixture.SpawnPlayer(Vector3.zero);
            PlayerWeaponController weapons =
                player.GetComponent<PlayerWeaponController>();
            AttackController attacks = player.AttackController;
            TargetingController targeting = player.TargetingController;
            ProjectileAttackExecutor projectile =
                player.GetComponent<ProjectileAttackExecutor>();
            GrenadeAttackExecutor grenade =
                player.GetComponent<GrenadeAttackExecutor>();
            HitscanAttackExecutor hitscan =
                player.GetComponent<HitscanAttackExecutor>();

            for (int weaponIndex = 0;
                 weaponIndex < weapons.WeaponCount;
                 weaponIndex++)
            {
                Assert.That(weapons.SelectWeapon(weaponIndex), Is.True);
                WeaponDefinition weapon = weapons.CurrentWeapon;
                Assert.That(attacks.AttackDefinition,
                    Is.SameAs(weapon.AttackDefinition));
                Assert.That(targeting.Mode,
                    Is.EqualTo(TargetingMode.PlayerAttackRange));
                Assert.That(targeting.QueryRange,
                    Is.EqualTo(weapon.AttackDefinition.AttackRange));
                Assert.That(
                    attacks.ValidateExecutorForDefinition(
                        weapon.AttackDefinition,
                        out string failure),
                    Is.True,
                    failure);
                Assert.That(player.GetComponents<ProjectileAttackExecutor>(),
                    Has.Exactly(1).SameAs(projectile));
                Assert.That(player.GetComponents<GrenadeAttackExecutor>(),
                    Has.Exactly(1).SameAs(grenade));
                Assert.That(player.GetComponents<HitscanAttackExecutor>(),
                    Has.Exactly(1).SameAs(hitscan));
            }
        }

        [Test]
        public void PlayerTargetingAcquiresEnemyWithoutIssuingMovement()
        {
            RuntimeFixture fixture = CreateRuntimeFixture();
            UnitController player = fixture.SpawnPlayer(Vector3.zero);
            UnitController enemy = fixture.SpawnStationaryEnemy(
                new Vector3(0f, 0f, 2.5f));
            Vector3 originalPosition = player.transform.position;
            Physics.SyncTransforms();

            DamageTargetProxy enemyProxy =
                enemy.GetComponentInChildren<DamageTargetProxy>(true);
            Assert.That(enemy.IsActive, Is.True);
            Assert.That(enemy.HealthController.IsAlive, Is.True);
            Assert.That(enemyProxy.IsConfigured, Is.True);
            Assert.That(enemyProxy.gameObject.activeInHierarchy, Is.True);
            Assert.That(enemyProxy.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("UnitTarget")));
            Assert.That(
                Physics.OverlapSphere(
                    player.transform.position,
                    player.TargetingController.QueryRange,
                    1 << LayerMask.NameToLayer("UnitTarget"),
                    QueryTriggerInteraction.Collide),
                Has.Some.SameAs(enemyProxy.TargetCollider));
            Assert.That(
                player.TargetingController.ForceScan(),
                Is.True,
                $"Mode={player.TargetingController.Mode}, Range={player.TargetingController.QueryRange}, Unique={player.TargetingController.LastUniqueCandidateCount}");
            Assert.That(player.TargetingController.CurrentTarget,
                Is.SameAs(enemy));
            Assert.That(player.GetComponent<PlayerMotor>(), Is.Not.Null);
            Assert.That(player.GetComponent<NavMeshAgent>(), Is.Null);
            Assert.That(player.transform.position, Is.EqualTo(originalPosition));
        }

        [Test]
        public void KeyboardAndLeftStickFeedSameCameraRelativeMoveAction()
        {
            RuntimeFixture fixture = CreateRuntimeFixture();
            UnitController player = fixture.SpawnPlayer(Vector3.zero);
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            Keyboard keyboard = AddDevice<Keyboard>();
            Gamepad gamepad = AddDevice<Gamepad>();
            Vector3 cameraForward = Vector3.ProjectOnPlane(
                fixture.Camera.transform.forward,
                Vector3.up).normalized;
            Vector3 cameraRight = Vector3.ProjectOnPlane(
                fixture.Camera.transform.right,
                Vector3.up).normalized;

            Press(keyboard.wKey);
            Assert.That(keyboard.wKey.isPressed, Is.True);
            Assert.That(
                player.GetComponent<PlayerInputReader>()
                    .MoveAction.action.controls,
                Has.Some.SameAs(keyboard.wKey));
            Assert.That(
                player.GetComponent<PlayerInputReader>().MoveInput.y,
                Is.GreaterThan(0.9f));
            motor.AdvanceMovement(0.1f);
            Vector3 keyboardDelta = Vector3.ProjectOnPlane(
                player.transform.position,
                Vector3.up);
            Assert.That(
                Vector3.Dot(keyboardDelta.normalized, cameraForward),
                Is.GreaterThan(0.99f));
            Release(keyboard.wKey);

            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = Vector3.zero;
            player.GetComponent<CharacterController>().enabled = true;
            Set(gamepad.leftStick, Vector2.right);
            motor.AdvanceMovement(0.1f);
            Vector3 stickDelta = Vector3.ProjectOnPlane(
                player.transform.position,
                Vector3.up);
            Assert.That(
                Vector3.Dot(stickDelta.normalized, cameraRight),
                Is.GreaterThan(0.99f));
        }

        [Test]
        public void AllThreeWeaponsDamageStationaryEnemyThroughExistingDeliveries()
        {
            RuntimeFixture fixture = CreateRuntimeFixture();
            UnitController player = fixture.SpawnPlayer(Vector3.zero);
            UnitController enemy = fixture.SpawnStationaryEnemy(
                new Vector3(0f, 0f, 2.5f));
            PlayerWeaponController weapons =
                player.GetComponent<PlayerWeaponController>();
            PlayerCombatController combat =
                player.GetComponent<PlayerCombatController>();
            Physics.SyncTransforms();
            Assert.That(player.TargetingController.ForceScan(), Is.True);
            Assert.That(player.TargetingController.CurrentTargetPoint.y,
                Is.EqualTo(0.5f).Within(0.001f));

            Assert.That(weapons.SelectWeapon(0), Is.True);
            Assert.That(combat.TickAutoAttack(), Is.True);
            player.AttackController.AdvanceTime(0.05f);
            ProjectileController bullet = fixture.FindActiveProjectile(
                new PoolId("Bullet"));
            bullet.AdvanceTime(0.2f);
            Assert.That(enemy.HealthController.CurrentHealth, Is.EqualTo(50f));

            player.AttackController.AdvanceTime(0.5f);
            Assert.That(weapons.SelectWeapon(1), Is.True);
            Assert.That(combat.TickAutoAttack(), Is.True);
            player.AttackController.AdvanceTime(0.25f);
            ProjectileController grenade = fixture.FindActiveProjectile(
                new PoolId("Grenade"));
            grenade.AdvanceTime(2f);
            Assert.That(enemy.HealthController.CurrentHealth, Is.EqualTo(25f));

            player.AttackController.AdvanceTime(1.8f);
            Assert.That(weapons.SelectWeapon(2), Is.True);
            Assert.That(combat.TickAutoAttack(), Is.True);
            player.AttackController.AdvanceTime(0.1f);
            Assert.That(enemy.HealthController.CurrentHealth, Is.EqualTo(7f));
            Assert.That(enemy.HealthController.IsAlive, Is.True);
        }

        [Test]
        public void GrenadeLaunch_UsesReachableLowBallisticArc()
        {
            Assert.That(
                BallisticLaunchRules.TryGetLowArcDirection(
                    new Vector3(0f, 0.5f, 0f),
                    new Vector3(6f, 0.5f, 0f),
                    12f,
                    Physics.gravity,
                    out Vector3 direction),
                Is.True);
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(direction.y, Is.GreaterThan(0f));
            Assert.That(
                BallisticLaunchRules.TryGetLowArcDirection(
                    Vector3.zero,
                    new Vector3(100f, 0f, 0f),
                    1f,
                    Physics.gravity,
                    out _),
                Is.False);
        }

        [Test]
        public void PlayerDeathAndReuseRestoreInputWeaponAndTransientState()
        {
            RuntimeFixture fixture = CreateRuntimeFixture();
            UnitController player = fixture.SpawnPlayer(Vector3.zero);
            SpawnId firstSpawnId = player.SpawnId;
            PlayerWeaponController weapons =
                player.GetComponent<PlayerWeaponController>();
            PlayerInputReader input = player.GetComponent<PlayerInputReader>();
            Assert.That(weapons.SelectWeapon(2), Is.True);
            Assert.That(
                player.StatusEffectController.ApplyAcceptedEffect(
                    new StatusEffectPayload(StatusEffectType.Stun, 2f)),
                Is.True);

            AttackKey attackKey = new AttackKey(
                new SpawnId(4000),
                new AttackSequenceId(1));
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(attackKey);
            DamagePayload payload = new DamagePayload(
                attackKey.SourceSpawnId,
                UnitFaction.Enemy,
                attackKey.SequenceId,
                100f,
                new DamageCategoryId("StepTenReset"));
            InteractionResult result = fixture.InteractionSystem.ResolveHit(
                new HitContext(
                    payload,
                    player.DamageController,
                    player.transform.position,
                    Vector3.up,
                    HitType.Direct,
                    "StepTenReset"),
                ledger);

            Assert.That(result.DamageResult.TargetDied, Is.True);
            Assert.That(player.gameObject.activeSelf, Is.False);
            Assert.That(input.IsInputEnabled, Is.False);

            UnitController reusedPlayer = fixture.SpawnPlayer(Vector3.zero);
            Assert.That(reusedPlayer, Is.SameAs(player));
            Assert.That(reusedPlayer.SpawnId, Is.Not.EqualTo(firstSpawnId));
            Assert.That(reusedPlayer.HealthController.CurrentHealth,
                Is.EqualTo(100f));
            Assert.That(reusedPlayer.StatusEffectController.IsStunned, Is.False);
            Assert.That(
                reusedPlayer.GetComponent<PlayerWeaponController>()
                    .CurrentWeaponIndex,
                Is.Zero);
            Assert.That(
                reusedPlayer.GetComponent<PlayerInputReader>().IsInputEnabled,
                Is.True);
        }

        private RuntimeFixture CreateRuntimeFixture()
        {
            PoolCatalog poolCatalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");
            PlayerUnitDefinition playerDefinition =
                AssetDatabase.LoadAssetAtPath<PlayerUnitDefinition>(
                    "Assets/Data/Units/UD_Player.asset");
            AIUnitDefinition enemyDefinition =
                AssetDatabase.LoadAssetAtPath<AIUnitDefinition>(
                    "Assets/Tests/Fixtures/StepTen/UD_Test_StationaryEnemy.asset");
            PoolManager poolManager = CreateComponent<PoolManager>("PoolManager");
            UnitRegistry registry = CreateComponent<UnitRegistry>("UnitRegistry");
            InteractionSystem interaction =
                CreateComponent<InteractionSystem>("InteractionSystem");
            SpawnManager spawnManager =
                CreateComponent<SpawnManager>("SpawnManager");
            Camera camera = CreateComponent<Camera>("Camera");
            camera.transform.position = new Vector3(0f, 15f, -10f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            Assert.That(poolManager.Initialize(poolCatalog, out string poolFailure),
                Is.True,
                poolFailure);
            Assert.That(
                spawnManager.Initialize(
                    poolManager,
                    registry,
                    out string spawnFailure),
                Is.True,
                spawnFailure);
            return new RuntimeFixture(
                poolManager,
                spawnManager,
                interaction,
                camera,
                playerDefinition,
                enemyDefinition);
        }

        private T CreateComponent<T>(string objectName) where T : Component
        {
            GameObject gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private T AddDevice<T>() where T : InputDevice
        {
            T device = InputSystem.AddDevice<T>();
            _createdDevices.Add(device);
            return device;
        }

        private sealed class RuntimeFixture
        {
            private readonly PlayerUnitDefinition _playerDefinition;
            private readonly AIUnitDefinition _enemyDefinition;

            public PoolManager PoolManager { get; }
            public SpawnManager SpawnManager { get; }
            public InteractionSystem InteractionSystem { get; }
            public Camera Camera { get; }

            public RuntimeFixture(
                PoolManager poolManager,
                SpawnManager spawnManager,
                InteractionSystem interactionSystem,
                Camera camera,
                PlayerUnitDefinition playerDefinition,
                AIUnitDefinition enemyDefinition)
            {
                PoolManager = poolManager;
                SpawnManager = spawnManager;
                InteractionSystem = interactionSystem;
                Camera = camera;
                _playerDefinition = playerDefinition;
                _enemyDefinition = enemyDefinition;
            }

            public UnitController SpawnPlayer(Vector3 position)
            {
                SpawnResult<UnitController> spawnResult = SpawnManager.SpawnUnit(
                    new UnitSpawnRequest(
                        _playerDefinition,
                        position,
                        Quaternion.identity,
                        default,
                        SpawnReason.Initial));
                Assert.That(spawnResult.IsSuccess,
                    Is.True,
                    spawnResult.FailureReason.ToString());
                UnitController player = spawnResult.Entity;
                Assert.That(
                    player.GetComponent<PlayerMotor>().BindCamera(Camera.transform),
                    Is.True);
                Assert.That(
                    player.GetComponent<PlayerCombatController>()
                        .ConfigureRuntimeServices(
                            SpawnManager,
                            InteractionSystem,
                            PoolManager),
                    Is.True);
                return player;
            }

            public UnitController SpawnStationaryEnemy(Vector3 position)
            {
                SpawnResult<UnitController> spawnResult = SpawnManager.SpawnUnit(
                    new UnitSpawnRequest(
                        _enemyDefinition,
                        position,
                        Quaternion.identity,
                        default,
                        SpawnReason.Initial));
                Assert.That(spawnResult.IsSuccess,
                    Is.True,
                    spawnResult.FailureReason.ToString());
                return spawnResult.Entity;
            }

            public ProjectileController FindActiveProjectile(PoolId poolId)
            {
                ProjectileController projectile = PoolManager
                    .GetComponentsInChildren<ProjectileController>(false)
                    .Single(controller =>
                        controller.GetComponent<PooledEntity>().PoolId == poolId);
                Assert.That(projectile.IsRunning, Is.True);
                return projectile;
            }
        }
    }
}
