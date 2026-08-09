using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepElevenAIAssetTests
    {
        private const string k_AIBasePath =
            "Assets/Prefabs/Units/PF_Unit_AI_Base.prefab";
        private const string k_AllyBasePath =
            "Assets/Prefabs/Units/PF_Unit_Ally_Base.prefab";
        private const string k_EnemyBasePath =
            "Assets/Prefabs/Units/PF_Unit_Enemy_Base.prefab";
        private const string k_AllyPrefabPath =
            "Assets/Tests/Fixtures/StepEleven/PF_Test_AI_Ally.prefab";
        private const string k_EnemyPrefabPath =
            "Assets/Tests/Fixtures/StepEleven/PF_Test_AI_Enemy.prefab";
        private const string k_AllyDefinitionPath =
            "Assets/Tests/Fixtures/StepEleven/UD_Test_AI_Ally.asset";
        private const string k_EnemyDefinitionPath =
            "Assets/Tests/Fixtures/StepEleven/UD_Test_AI_Enemy.asset";
        private const string k_AttackPath =
            "Assets/Tests/Fixtures/StepEleven/AD_Test_AI_BasicMelee.asset";

        [Test]
        public void AIPrefabBranches_AreThinVariantsWithExpectedCapabilities()
        {
            GameObject aiBase = Load<GameObject>(k_AIBasePath);
            GameObject allyBase = Load<GameObject>(k_AllyBasePath);
            GameObject enemyBase = Load<GameObject>(k_EnemyBasePath);
            GameObject ally = Load<GameObject>(k_AllyPrefabPath);
            GameObject enemy = Load<GameObject>(k_EnemyPrefabPath);

            AssertVariant(aiBase);
            AssertVariant(allyBase);
            AssertVariant(enemyBase);
            AssertVariant(ally);
            AssertVariant(enemy);
            Assert.That(aiBase.GetComponents<NavMeshAgent>().Length,
                Is.EqualTo(1));
            Assert.That(aiBase.GetComponents<NavMeshUnitMotor>().Length,
                Is.EqualTo(1));
            Assert.That(aiBase.GetComponents<AIUnitBrain>().Length,
                Is.EqualTo(1));
            Assert.That(aiBase.GetComponent<AIFactionDefinitionGuard>(),
                Is.Null);
            AssertFactionBase(allyBase, UnitFaction.Ally, aiBase);
            AssertFactionBase(enemyBase, UnitFaction.Enemy, aiBase);
            AssertConcreteAI(ally, UnitFaction.Ally);
            AssertConcreteAI(enemy, UnitFaction.Enemy);
        }

        [Test]
        public void TestDefinitions_UseExactClassicMeleeTableValues()
        {
            AttackDefinition attack = Load<AttackDefinition>(k_AttackPath);
            AIUnitDefinition ally = Load<AIUnitDefinition>(
                k_AllyDefinitionPath);
            AIUnitDefinition enemy = Load<AIUnitDefinition>(
                k_EnemyDefinitionPath);

            Assert.That(attack.Validate().IsValid, Is.True);
            Assert.That(attack.Damage, Is.EqualTo(10f));
            Assert.That(attack.AttackRange, Is.EqualTo(1.8f));
            Assert.That(attack.CooldownDuration, Is.EqualTo(1f));
            Assert.That(attack.WindupDuration, Is.EqualTo(0.25f));
            Assert.That(attack.RecoveryDuration, Is.EqualTo(0.25f));
            Assert.That(attack.DeliveryType,
                Is.EqualTo(AttackDeliveryType.Melee));
            AssertAIUnitDefinition(ally, UnitFaction.Ally, attack);
            AssertAIUnitDefinition(enemy, UnitFaction.Enemy, attack);
        }

        [Test]
        public void TestFixtures_AreRegisteredWithRegularUnitPoolBaselines()
        {
            PoolCatalog poolCatalog = Load<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");
            UnitCatalog unitCatalog = Load<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");
            AIUnitDefinition ally = Load<AIUnitDefinition>(
                k_AllyDefinitionPath);
            AIUnitDefinition enemy = Load<AIUnitDefinition>(
                k_EnemyDefinitionPath);

            AssertPoolEntry(poolCatalog, ally.PoolId, k_AllyPrefabPath);
            AssertPoolEntry(poolCatalog, enemy.PoolId, k_EnemyPrefabPath);
            Assert.That(
                unitCatalog.TryGetDefinition(
                    ally.UnitId,
                    out UnitDefinition catalogAlly),
                Is.True);
            Assert.That(catalogAlly, Is.SameAs(ally));
            Assert.That(
                unitCatalog.TryGetDefinition(
                    enemy.UnitId,
                    out UnitDefinition catalogEnemy),
                Is.True);
            Assert.That(catalogEnemy, Is.SameAs(enemy));
        }

        [Test]
        public void CombatSandbox_HasConfiguredAIExitScenario()
        {
            Scene scene = SceneManager.GetSceneByPath(
                "Assets/Scenes/CombatSandbox.unity");
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(
                    "Assets/Scenes/CombatSandbox.unity",
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject systems = Array.Find(
                    scene.GetRootGameObjects(),
                    root => root.name == "__Systems");
                AISandboxScenarioController scenario = systems.transform
                    .Find("AISandboxScenario")
                    .GetComponent<AISandboxScenarioController>();
                Assert.That(scenario.Bootstrap, Is.Not.Null);
                Assert.That(scenario.InitialSandboxSpawner, Is.Not.Null);
                Assert.That(scenario.SpawnManager, Is.Not.Null);
                Assert.That(scenario.InteractionSystem, Is.Not.Null);
                Assert.That(scenario.AllySpawnPoints.Count,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(scenario.EnemySpawnPoints.Count,
                    Is.GreaterThanOrEqualTo(2));
                Assert.That(scenario.AllyDefinition.Faction,
                    Is.EqualTo(UnitFaction.Ally));
                Assert.That(scenario.EnemyDefinition.Faction,
                    Is.EqualTo(UnitFaction.Enemy));
                Assert.That(
                    Array.Exists(
                        EditorBuildSettings.scenes,
                        buildScene => buildScene.enabled &&
                            buildScene.path ==
                                "Assets/Scenes/CombatSandbox.unity"),
                    Is.True,
                    "CombatSandbox must be enabled in Build Settings.");
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void FactionGuard_RejectsMismatchedAIDefinition()
        {
            GameObject instance = UnityEngine.Object.Instantiate(
                Load<GameObject>(k_AllyPrefabPath));
            instance.SetActive(false);
            try
            {
                SetAutoProperty(
                    instance.GetComponent<UnitController>(),
                    nameof(UnitController.Definition),
                    Load<AIUnitDefinition>(k_EnemyDefinitionPath));
                Assert.That(
                    instance.GetComponent<AIFactionDefinitionGuard>()
                        .ValidateConfiguration(out _),
                    Is.False);
                Assert.That(
                    instance.GetComponent<AIUnitBrain>()
                        .ValidateConfiguration(out _),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssertFactionBase(
            GameObject factionBase,
            UnitFaction expectedFaction,
            GameObject aiBase)
        {
            AIFactionDefinitionGuard guard =
                factionBase.GetComponent<AIFactionDefinitionGuard>();
            Assert.That(guard, Is.Not.Null);
            Assert.That(guard.ExpectedFaction, Is.EqualTo(expectedFaction));
            Assert.That(factionBase.GetComponent<UnitController>().Definition,
                Is.Null);
            Assert.That(
                factionBase.GetComponent<AttackController>().AttackDefinition,
                Is.Null);
            Assert.That(factionBase.GetComponent<MeleeAttackExecutor>(),
                Is.Null);
            NavMeshAgent baseAgent = aiBase.GetComponent<NavMeshAgent>();
            NavMeshAgent factionAgent =
                factionBase.GetComponent<NavMeshAgent>();
            Assert.That(factionAgent.radius, Is.EqualTo(baseAgent.radius));
            Assert.That(factionAgent.height, Is.EqualTo(baseAgent.height));
            Assert.That(factionAgent.speed, Is.EqualTo(baseAgent.speed));
            Assert.That(factionAgent.angularSpeed,
                Is.EqualTo(baseAgent.angularSpeed));
        }

        private static void AssertConcreteAI(
            GameObject prefab,
            UnitFaction expectedFaction)
        {
            Assert.That(prefab.GetComponents<NavMeshAgent>().Length,
                Is.EqualTo(1));
            Assert.That(prefab.GetComponents<NavMeshUnitMotor>().Length,
                Is.EqualTo(1));
            Assert.That(prefab.GetComponents<AIUnitBrain>().Length,
                Is.EqualTo(1));
            Assert.That(prefab.GetComponents<MeleeAttackExecutor>().Length,
                Is.EqualTo(1));
            Assert.That(
                prefab.GetComponent<AIFactionDefinitionGuard>()
                    .ExpectedFaction,
                Is.EqualTo(expectedFaction));
            Assert.That(
                prefab.GetComponent<UnitController>().Definition.Faction,
                Is.EqualTo(expectedFaction));
            Assert.That(
                prefab.transform.Find("VisualRoot").childCount,
                Is.EqualTo(1));
        }

        private static void AssertAIUnitDefinition(
            AIUnitDefinition definition,
            UnitFaction expectedFaction,
            AttackDefinition expectedAttack)
        {
            Assert.That(definition.Validate().IsValid, Is.True);
            Assert.That(definition.Faction, Is.EqualTo(expectedFaction));
            Assert.That(definition.MaximumHealth, Is.EqualTo(60f));
            Assert.That(definition.MoveSpeed, Is.EqualTo(3.5f));
            Assert.That(definition.TurnSpeed, Is.EqualTo(540f));
            Assert.That(definition.ChaseRange, Is.EqualTo(12f));
            Assert.That(definition.DefaultAttackDefinition,
                Is.SameAs(expectedAttack));
        }

        private static void AssertPoolEntry(
            PoolCatalog catalog,
            PoolId poolId,
            string expectedPrefabPath)
        {
            Assert.That(catalog.TryGetEntry(poolId, out PoolCatalogEntry entry),
                Is.True);
            Assert.That(entry.InitialPrewarmCount, Is.EqualTo(10));
            Assert.That(entry.MaximumInactiveRetainedCount, Is.EqualTo(100));
            Assert.That(entry.CapacityPolicy,
                Is.EqualTo(PoolCapacityPolicy.Expandable));
            Assert.That(AssetDatabase.GetAssetPath(entry.Prefab),
                Is.EqualTo(expectedPrefabPath));
        }

        private static void AssertVariant(GameObject prefab)
        {
            Assert.That(PrefabUtility.GetPrefabAssetType(prefab),
                Is.EqualTo(PrefabAssetType.Variant));
        }

        private static T Load<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            return asset;
        }

        private static void SetAutoProperty<TValue>(
            object target,
            string propertyName,
            TValue value)
        {
            Type currentType = target.GetType();
            string fieldName = $"<{propertyName}>k__BackingField";
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
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new MissingFieldException(
                target.GetType().FullName,
                fieldName);
        }
    }

    public sealed class StepElevenAIBehaviorTests
    {
        private AIRuntimeHarness _harness;

        [SetUp]
        public void SetUp()
        {
            _harness = new AIRuntimeHarness();
        }

        [TearDown]
        public void TearDown()
        {
            _harness.Dispose();
        }

        [Test]
        public void FactionTargeting_AllyOnlyEnemy_EnemyPlayerOrAlly()
        {
            UnitController allySource = _harness.SpawnAI(
                _harness.AllyDefinition,
                new Vector3(-6f, 0f, 3f));
            UnitController player = _harness.Spawn(
                _harness.PlayerDefinition,
                new Vector3(-5f, 0f, 3f));
            UnitController enemy = _harness.SpawnAI(
                _harness.EnemyDefinition,
                new Vector3(-1f, 0f, 3f));
            Physics.SyncTransforms();

            Assert.That(allySource.TargetingController.ForceScan(), Is.True);
            Assert.That(allySource.TargetingController.CurrentTarget,
                Is.SameAs(enemy));
            Assert.That(allySource.TargetingController.CurrentTarget,
                Is.Not.SameAs(player));

            UnitController enemySource = _harness.SpawnAI(
                _harness.EnemyDefinition,
                new Vector3(6f, 0f, -3f));
            UnitController friendlyEnemy = _harness.SpawnAI(
                _harness.EnemyDefinition,
                new Vector3(5.5f, 0f, -3f));
            UnitController nearbyPlayer = _harness.Spawn(
                _harness.PlayerDefinition,
                new Vector3(5f, 0f, -3f));
            Physics.SyncTransforms();
            Assert.That(enemySource.TargetingController.ForceScan(), Is.True);
            Assert.That(enemySource.TargetingController.CurrentTarget,
                Is.SameAs(nearbyPlayer));
            Assert.That(enemySource.TargetingController.CurrentTarget,
                Is.Not.SameAs(friendlyEnemy));
        }

        [Test]
        public void Brain_TransitionsIdleChaseAttackChaseAndTargetLoss()
        {
            UnitController ally = _harness.SpawnAI(
                _harness.AllyDefinition,
                new Vector3(-6f, 0f, 3f));
            UnitController target = _harness.Spawn(
                _harness.StationaryEnemyDefinition,
                new Vector3(-1f, 0f, 3f));
            AIUnitBrain brain = ally.GetComponent<AIUnitBrain>();
            StepElevenMotorProbe motor =
                ally.GetComponent<StepElevenMotorProbe>();
            Physics.SyncTransforms();
            Assert.That(ally.TargetingController.ForceScan(), Is.True);

            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Idle));
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));
            Assert.That(motor.IsStopped, Is.False);
            Assert.That(motor.DestinationCommandCount, Is.EqualTo(1));

            target.transform.position = new Vector3(-5f, 0f, 3f);
            Physics.SyncTransforms();
            brain.AdvanceDecision(0.1f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Attack));
            Assert.That(motor.IsStopped, Is.True);
            Assert.That(motor.HasPath, Is.False);

            target.transform.position = new Vector3(-3f, 0f, 3f);
            Physics.SyncTransforms();
            brain.AdvanceDecision(0.1f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));

            target.transform.position = new Vector3(8f, 0f, 3f);
            Physics.SyncTransforms();
            Assert.That(ally.TargetingController.ForceScan(), Is.False);
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Idle));
            Assert.That(motor.IsStopped, Is.True);
        }

        [Test]
        public void ChaseDestination_IsThrottledUnlessTargetMovesMeaningfully()
        {
            UnitController ally = _harness.SpawnAI(
                _harness.AllyDefinition,
                new Vector3(-6f, 0f, 3f));
            UnitController target = _harness.Spawn(
                _harness.StationaryEnemyDefinition,
                new Vector3(-1f, 0f, 3f));
            AIUnitBrain brain = ally.GetComponent<AIUnitBrain>();
            StepElevenMotorProbe motor =
                ally.GetComponent<StepElevenMotorProbe>();
            Physics.SyncTransforms();
            ally.TargetingController.ForceScan();
            brain.AdvanceDecision(0f);
            brain.AdvanceDecision(0f);
            Assert.That(motor.DestinationCommandCount, Is.EqualTo(1));

            target.transform.position += new Vector3(0.1f, 0f, 0f);
            Physics.SyncTransforms();
            brain.AdvanceDecision(0.1f);
            Assert.That(motor.DestinationCommandCount, Is.EqualTo(1));
            brain.AdvanceDecision(0.15f);
            Assert.That(motor.DestinationCommandCount, Is.EqualTo(2));

            target.transform.position += new Vector3(0.6f, 0f, 0f);
            Physics.SyncTransforms();
            brain.AdvanceDecision(0.01f);
            Assert.That(motor.DestinationCommandCount, Is.EqualTo(3));
        }

        [Test]
        public void Stun_DisablesAndClearsPath_ThenReturnsThroughIdle()
        {
            UnitController ally = _harness.SpawnAI(
                _harness.AllyDefinition,
                new Vector3(-6f, 0f, 3f));
            _harness.Spawn(
                _harness.StationaryEnemyDefinition,
                new Vector3(-1f, 0f, 3f));
            AIUnitBrain brain = ally.GetComponent<AIUnitBrain>();
            StepElevenMotorProbe motor =
                ally.GetComponent<StepElevenMotorProbe>();
            Physics.SyncTransforms();
            ally.TargetingController.ForceScan();
            brain.AdvanceDecision(0f);
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));

            Assert.That(
                ally.StatusEffectController.ApplyAcceptedEffect(
                    new StatusEffectPayload(StatusEffectType.Stun, 1f)),
                Is.True);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Disabled));
            Assert.That(ally.TargetingController.CurrentTarget, Is.Null);
            Assert.That(motor.IsStopped, Is.True);
            Assert.That(motor.HasPath, Is.False);

            ally.StatusEffectController.AdvanceTime(1f);
            Physics.SyncTransforms();
            Assert.That(ally.TargetingController.ForceScan(), Is.True);
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Idle));
            brain.AdvanceDecision(0f);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));
        }

        [Test]
        public void DeathAndPoolReturn_ClearAIPathTargetAndState()
        {
            Vector3 spawnPosition = new Vector3(-6f, 0f, 3f);
            UnitController ally = _harness.SpawnAI(
                _harness.AllyDefinition,
                spawnPosition);
            _harness.Spawn(
                _harness.StationaryEnemyDefinition,
                new Vector3(-1f, 0f, 3f));
            AIUnitBrain brain = ally.GetComponent<AIUnitBrain>();
            StepElevenMotorProbe motor =
                ally.GetComponent<StepElevenMotorProbe>();
            Physics.SyncTransforms();
            ally.TargetingController.ForceScan();
            brain.AdvanceDecision(0f);
            brain.AdvanceDecision(0f);
            Assert.That(motor.DestinationCommandCount, Is.EqualTo(1));

            _harness.Kill(ally, UnitFaction.Enemy);
            Assert.That(ally.IsActive, Is.False);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Disabled));
            Assert.That(motor.IsStopped, Is.True);
            Assert.That(motor.HasPath, Is.False);
            Assert.That(ally.TargetingController.CurrentTarget, Is.Null);
            _harness.Return(ally);
            Assert.That(ally.gameObject.activeSelf, Is.False);
            Assert.That(brain.State, Is.EqualTo(AIUnitState.Disabled));
            Assert.That(motor.IsStopped, Is.True);
            Assert.That(motor.HasPath, Is.False);
            Assert.That(motor.DestinationCommandCount, Is.Zero);
        }
    }

    internal sealed class AIRuntimeHarness : IDisposable
    {
        private readonly List<GameObject> _createdObjects =
            new List<GameObject>();
        private int _attackSequence;

        public AIUnitDefinition AllyDefinition { get; }
        public AIUnitDefinition EnemyDefinition { get; }
        public PlayerUnitDefinition PlayerDefinition { get; }
        public AIUnitDefinition StationaryEnemyDefinition { get; }
        public InteractionSystem InteractionSystem { get; }

        public AIRuntimeHarness()
        {
            AllyDefinition = Load<AIUnitDefinition>(
                "Assets/Tests/Fixtures/StepEleven/UD_Test_AI_Ally.asset");
            EnemyDefinition = Load<AIUnitDefinition>(
                "Assets/Tests/Fixtures/StepEleven/UD_Test_AI_Enemy.asset");
            PlayerDefinition = Load<PlayerUnitDefinition>(
                "Assets/Data/Units/UD_Player.asset");
            StationaryEnemyDefinition = Load<AIUnitDefinition>(
                "Assets/Tests/Fixtures/StepTen/UD_Test_StationaryEnemy.asset");
            GameObject interactionObject = new GameObject(
                "StepElevenInteractionSystem");
            _createdObjects.Add(interactionObject);
            InteractionSystem =
                interactionObject.AddComponent<InteractionSystem>();
        }

        public UnitController SpawnAI(
            AIUnitDefinition definition,
            Vector3 position)
        {
            UnitController unit = CreateUnit(definition, position, true);
            Assert.That(
                unit.GetComponent<AIUnitBrain>()
                    .ConfigureRuntimeServices(InteractionSystem),
                Is.True);
            return unit;
        }

        public UnitController Spawn(
            UnitDefinition definition,
            Vector3 position)
        {
            return CreateUnit(definition, position, false);
        }

        public void Kill(UnitController target, UnitFaction sourceFaction)
        {
            SpawnId sourceSpawnId = new SpawnId(9000 + _attackSequence);
            AttackSequenceId sequenceId = new AttackSequenceId(
                ++_attackSequence);
            AttackKey attackKey = new AttackKey(sourceSpawnId, sequenceId);
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(attackKey);
            DamagePayload payload = new DamagePayload(
                sourceSpawnId,
                sourceFaction,
                sequenceId,
                target.HealthController.MaximumHealth + 1f,
                new DamageCategoryId("StepElevenDeath"));
            InteractionResult result = InteractionSystem.ResolveHit(
                new HitContext(
                    payload,
                    target.DamageController,
                    target.transform.position,
                    Vector3.up,
                    HitType.Direct,
                    "StepElevenDeath"),
                ledger);
            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.DamageResult.TargetDied, Is.True);
        }

        public void Return(UnitController unit)
        {
            AIUnitBrain brain = unit.GetComponent<AIUnitBrain>();
            AIFactionDefinitionGuard guard =
                unit.GetComponent<AIFactionDefinitionGuard>();
            unit.TargetingController.PrepareForReturn();
            unit.AttackController.PrepareForReturn();
            unit.LifecycleController.PrepareForReturn();
            brain?.PrepareForReturn();
            guard?.PrepareForReturn();
            unit.GetComponent<StepElevenMotorProbe>()?.ResetForReturn();
            unit.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            for (int objectIndex = _createdObjects.Count - 1;
                 objectIndex >= 0;
                 objectIndex--)
            {
                if (_createdObjects[objectIndex] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _createdObjects[objectIndex]);
                }
            }

            _createdObjects.Clear();
        }

        private UnitController CreateUnit(
            UnitDefinition definition,
            Vector3 position,
            bool includeBrain)
        {
            GameObject root = new GameObject(
                $"Programmatic_{definition.DisplayName}");
            _createdObjects.Add(root);
            root.SetActive(false);
            root.layer = LayerMask.NameToLayer("UnitBody");
            root.transform.position = position;
            UnitController unitController =
                root.AddComponent<UnitController>();
            root.AddComponent<MonstersVsZombies.Combat.Health.HealthController>();
            root.AddComponent<DamageController>();
            root.AddComponent<StatusEffectController>();
            TargetingController targetingController =
                root.AddComponent<TargetingController>();
            Assert.That(
                targetingController.InitializeScanning(8, 0.25f, 0f),
                Is.True);
            AttackController attackController =
                root.AddComponent<AttackController>();
            UnitLifecycleController lifecycleController =
                root.AddComponent<UnitLifecycleController>();

            AIUnitDefinition aiDefinition = definition as AIUnitDefinition;
            MeleeAttackExecutor meleeExecutor = null;
            if (aiDefinition != null)
            {
                meleeExecutor = root.AddComponent<MeleeAttackExecutor>();
                attackController.ConfigureBindings(
                    aiDefinition.DefaultAttackDefinition,
                    new[]
                    {
                        new AttackExecutorBinding(
                            AttackDeliveryType.Melee,
                            meleeExecutor)
                    });
            }

            StepElevenMotorProbe motor = null;
            AIFactionDefinitionGuard guard = null;
            AIUnitBrain brain = null;
            if (includeBrain)
            {
                motor = root.AddComponent<StepElevenMotorProbe>();
                guard = root.AddComponent<AIFactionDefinitionGuard>();
                guard.Configure(definition.Faction);
                brain = root.AddComponent<AIUnitBrain>();
            }

            GameObject hurtbox = new GameObject("Hurtbox");
            hurtbox.transform.SetParent(root.transform, false);
            hurtbox.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            hurtbox.layer = LayerMask.NameToLayer("UnitTarget");
            SphereCollider targetCollider =
                hurtbox.AddComponent<SphereCollider>();
            targetCollider.isTrigger = true;
            targetCollider.radius = 0.5f;
            DamageTargetProxy targetProxy =
                hurtbox.AddComponent<DamageTargetProxy>();

            SpawnId spawnId = new SpawnId(_createdObjects.Count + 100);
            Assert.That(
                lifecycleController.ConfigureSpawn(definition, spawnId),
                Is.True);
            Assert.That(lifecycleController.PrepareForSpawn(), Is.True);
            Assert.That(targetProxy.PrepareForSpawn(), Is.True);
            Assert.That(targetingController.PrepareForSpawn(), Is.True);
            Assert.That(attackController.PrepareForSpawn(), Is.True);
            Assert.That(guard == null || guard.PrepareForSpawn(), Is.True);
            Assert.That(brain == null || brain.PrepareForSpawn(), Is.True);

            root.SetActive(true);
            Assert.That(lifecycleController.CompleteSpawn(), Is.True);
            Assert.That(targetProxy.CompleteSpawn(), Is.True);
            Assert.That(targetingController.CompleteSpawn(), Is.True);
            Assert.That(attackController.CompleteSpawn(), Is.True);
            Assert.That(guard == null || guard.CompleteSpawn(), Is.True);
            Assert.That(brain == null || brain.CompleteSpawn(), Is.True);
            Assert.That(lifecycleController.ActivateSpawn(), Is.True);
            if (meleeExecutor != null)
            {
                Assert.That(
                    meleeExecutor.Configure(InteractionSystem),
                    Is.True);
            }

            return unitController;
        }

        private static T Load<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            return asset;
        }

    }

    internal sealed class StepElevenMotorProbe : MonoBehaviour,
        MonstersVsZombies.Units.Movement.IUnitMotor,
        MonstersVsZombies.Units.Movement.IDestinationRefreshPolicy
    {
        public bool IsStopped { get; private set; } = true;
        public bool HasPath { get; private set; }
        public int DestinationCommandCount { get; private set; }
        public int FaceCommandCount { get; private set; }
        public Vector3 LastDestination { get; private set; }
        public float DestinationRefreshDistance => 0.5f;

        public void MoveTo(Vector3 worldPosition)
        {
            if (IsStopped)
            {
                return;
            }

            LastDestination = worldPosition;
            DestinationCommandCount++;
            HasPath = true;
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            FaceCommandCount++;
        }

        public void Stop()
        {
            IsStopped = true;
            HasPath = false;
        }

        public void Resume()
        {
            IsStopped = false;
        }

        public void ResetForReturn()
        {
            Stop();
            DestinationCommandCount = 0;
            FaceCommandCount = 0;
            LastDestination = transform.position;
        }
    }
}
