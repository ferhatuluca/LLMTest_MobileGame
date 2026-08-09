using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Data;
using MonstersVsZombies.Diagnostics;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepFourteenAssetAndSceneTests
    {
        private const string k_InputAssetPath =
            "Assets/InputSystem_Actions.inputactions";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";

        private static readonly string[] s_expectedUnitIds =
        {
            "EnemyClassicMelee",
            "EnemyClassicRange",
            "EnemyDragon",
            "EnemyStunner",
            "EnemyDivisible",
            "AllyClassicMelee",
            "AllyClassicRange",
            "AllyDragon",
            "AllyDoubleHead",
            "EnemyMiniDivisible"
        };

        [Test]
        public void SandboxDebugMap_HasExactDocumentedKeysWithoutQOrE()
        {
            InputActionAsset inputActions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_InputAssetPath);
            Assert.That(inputActions, Is.Not.Null);
            InputActionMap debugMap = inputActions.FindActionMap(
                "SandboxDebug",
                true);
            Assert.That(debugMap.actions.Count, Is.EqualTo(12));
            AssertSingleBinding(debugMap, "TogglePanel", "<Keyboard>/f1");
            for (int keyNumber = 1; keyNumber <= 9; keyNumber++)
            {
                AssertSingleBinding(
                    debugMap,
                    $"Spawn{keyNumber}",
                    $"<Keyboard>/digit{keyNumber}");
            }

            AssertSingleBinding(debugMap, "Spawn0", "<Keyboard>/digit0");
            AssertSingleBinding(debugMap, "Clear", "<Keyboard>/backspace");
            foreach (InputAction action in debugMap.actions)
            {
                foreach (InputBinding binding in action.bindings)
                {
                    Assert.That(binding.path,
                        Is.Not.EqualTo("<Keyboard>/q"));
                    Assert.That(binding.path,
                        Is.Not.EqualTo("<Keyboard>/e"));
                }
            }

            InputActionMap playerMap = inputActions.FindActionMap("Player", true);
            AssertSingleBinding(playerMap, "PreviousWeapon", "<Keyboard>/q");
            AssertSingleBinding(playerMap, "NextWeapon", "<Keyboard>/e");
        }

        [Test]
        public void KeyboardActions_MapToDocumentedConcreteUnits()
        {
            for (int unitIndex = 0; unitIndex < s_expectedUnitIds.Length; unitIndex++)
            {
                string actionName = unitIndex == 9
                    ? "Spawn0"
                    : $"Spawn{unitIndex + 1}";
                Assert.That(
                    SandboxDebugInputController.TryGetMappedUnitId(
                        actionName,
                        out UnitId unitId),
                    Is.True);
                Assert.That(unitId.Value, Is.EqualTo(s_expectedUnitIds[unitIndex]));
            }

            Assert.That(
                SandboxDebugInputController.TryGetMappedUnitId(
                    "PreviousWeapon",
                    out _),
                Is.False);
        }

        [Test]
        public void CombatSandbox_PanelHasEveryConcreteButtonAndRequiredControls()
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
                GameObject systems = FindRoot(scene, "__Systems");
                GameObject ui = FindRoot(scene, "UI");
                Assert.That(systems, Is.Not.Null);
                Assert.That(ui, Is.Not.Null);
                SandboxDebugInputController input = systems
                    .GetComponentInChildren<SandboxDebugInputController>(true);
                SandboxGizmoController gizmos = systems
                    .GetComponentInChildren<SandboxGizmoController>(true);
                SandboxDebugPanelController panel = ui
                    .GetComponentInChildren<SandboxDebugPanelController>(true);
                DebugUnitSpawner spawner = systems
                    .GetComponentInChildren<DebugUnitSpawner>(true);

                Assert.That(input, Is.Not.Null);
                Assert.That(gizmos, Is.Not.Null);
                Assert.That(panel, Is.Not.Null);
                Assert.That(spawner, Is.Not.Null);
                Assert.That(input.ValidateConfiguration(out string inputFailure),
                    Is.True,
                    inputFailure);
                Assert.That(panel.ValidateConfiguration(out string panelFailure),
                    Is.True,
                    panelFailure);
                Assert.That(spawner.ValidateConfiguration(out string spawnFailure),
                    Is.True,
                    spawnFailure);
                Assert.That(panel.gameObject.activeSelf, Is.False);
                Assert.That(spawner.AllySpawnPoints,
                    Is.Not.SameAs(spawner.EnemySpawnPoints));
                Assert.That(panel.SpawnButtons.Length, Is.EqualTo(10));
                for (int bindingIndex = 0;
                     bindingIndex < panel.SpawnButtons.Length;
                     bindingIndex++)
                {
                    SandboxSpawnButtonBinding binding =
                        panel.SpawnButtons[bindingIndex];
                    Assert.That(binding.Definition.UnitId.Value,
                        Is.EqualTo(s_expectedUnitIds[bindingIndex]));
                    Assert.That(binding.SpawnOneButton, Is.Not.Null);
                    Assert.That(binding.SpawnTenButton, Is.Not.Null);
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

        [Test]
        public void ReleaseGuard_DisablesControlsAndCompilesOutGizmoDrawing()
        {
            string runtimeSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Runtime/Diagnostics/SandboxDebugRuntime.cs");
            string inputSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Runtime/Diagnostics/SandboxDebugInputController.cs");
            string gizmoSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Runtime/Diagnostics/SandboxGizmoController.cs");

            StringAssert.Contains(
                "Application.isEditor || Debug.isDebugBuild",
                runtimeSource);
            StringAssert.Contains(
                "if (!SandboxDebugRuntime.IsAvailable)",
                inputSource);
            StringAssert.Contains(
                "#if UNITY_EDITOR || DEVELOPMENT_BUILD",
                gizmoSource);
        }

        private static void AssertSingleBinding(
            InputActionMap actionMap,
            string actionName,
            string expectedPath)
        {
            InputAction action = actionMap.FindAction(actionName, true);
            Assert.That(action.bindings.Count, Is.EqualTo(1), actionName);
            Assert.That(action.bindings[0].path,
                Is.EqualTo(expectedPath),
                actionName);
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

    public sealed class StepFourteenRuntimeControlTests
    {
        private StepSixSpawnFactory _factory;
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _factory = new StepSixSpawnFactory();
            SandboxDebugRuntime.SetAIDecisionsPaused(false);
            SandboxDebugRuntime.SetDiagnosticsEnabled(false);
        }

        [TearDown]
        public void TearDown()
        {
            SandboxDebugRuntime.SetAIDecisionsPaused(false);
            SandboxDebugRuntime.SetDiagnosticsEnabled(false);
            _factory.Dispose();
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

        [Test]
        public void SpawnMany_UsesSpawnManagerForEveryRequest()
        {
            StepSixUnitPoolSpec enemySpec = new StepSixUnitPoolSpec(
                new PoolId("StepFourteenEnemy"),
                UnitFaction.Enemy,
                maximumInactiveRetainedCount: 12);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(enemySpec);
            SetDefinitionIdentity(enemySpec.Definition, "EnemyClassicMelee");
            DebugUnitSpawner spawner = CreateSpawner(
                environment,
                enemySpec.Definition);

            int successCount = spawner.SpawnMany(
                new UnitId("EnemyClassicMelee"),
                10);

            Assert.That(successCount, Is.EqualTo(10));
            Assert.That(environment.UnitRegistry.Count, Is.EqualTo(10));
            Assert.That(
                environment.PoolManager.TryGetDiagnostics(
                    enemySpec.PoolId,
                    out PoolDiagnostics diagnostics),
                Is.True);
            Assert.That(diagnostics.ActiveCount, Is.EqualTo(10));
            Assert.That(diagnostics.PeakActiveCount, Is.EqualTo(10));
        }

        [Test]
        public void Clear_ReturnsNonPlayerThroughPoolAndPreservesPlayer()
        {
            StepSixUnitPoolSpec playerSpec = new StepSixUnitPoolSpec(
                new PoolId("StepFourteenPlayer"),
                UnitFaction.Player);
            StepSixUnitPoolSpec enemySpec = new StepSixUnitPoolSpec(
                new PoolId("StepFourteenEnemy"),
                UnitFaction.Enemy);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(playerSpec, enemySpec);
            DebugUnitSpawner spawner = CreateSpawner(
                environment,
                playerSpec.Definition,
                enemySpec.Definition);
            UnitController player = spawner.Spawn(
                playerSpec.Definition,
                new Pose(Vector3.zero, Quaternion.identity)).Entity;
            UnitController enemy = spawner.Spawn(
                enemySpec.Definition,
                new Pose(Vector3.right, Quaternion.identity)).Entity;
            int createdBefore = GetDiagnostics(
                environment.PoolManager,
                enemySpec.PoolId).CreatedCount;

            int returnedCount =
                spawner.ClearNonPlayerUnitsAndProjectiles();

            Assert.That(returnedCount, Is.EqualTo(1));
            Assert.That(player.IsActive, Is.True);
            Assert.That(enemy.IsActive, Is.False);
            Assert.That(environment.UnitRegistry.Count, Is.EqualTo(1));
            PoolDiagnostics diagnostics = GetDiagnostics(
                environment.PoolManager,
                enemySpec.PoolId);
            Assert.That(diagnostics.ActiveCount, Is.Zero);
            Assert.That(diagnostics.InactiveCount, Is.EqualTo(1));
            Assert.That(diagnostics.CreatedCount, Is.EqualTo(createdBefore),
                "Clear must return to the pool instead of destroying the object.");
        }

        [Test]
        public void SpawnFailures_ReportRequiredDiagnosticCodes()
        {
            StepSixUnitPoolSpec knownSpec = new StepSixUnitPoolSpec(
                new PoolId("KnownPool"),
                UnitFaction.Enemy);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(knownSpec);
            DebugUnitSpawner spawner = CreateSpawner(
                environment,
                knownSpec.Definition);
            UnitDefinition unknownDefinition = _factory.CreateUnitDefinition(
                new PoolId("MissingPool"),
                UnitFaction.Enemy,
                "Unknown");

            spawner.Spawn(null, new Pose(Vector3.zero, Quaternion.identity));
            Assert.That(spawner.LastDiagnostic.Code,
                Is.EqualTo(SandboxDiagnosticCode.InvalidDefinition));
            spawner.Spawn(
                unknownDefinition,
                new Pose(Vector3.zero, Quaternion.identity));
            Assert.That(spawner.LastDiagnostic.Code,
                Is.EqualTo(SandboxDiagnosticCode.MissingPool));
            spawner.Spawn(
                knownSpec.Definition,
                new Pose(
                    new Vector3(float.NaN, 0f, 0f),
                    Quaternion.identity));
            Assert.That(spawner.LastDiagnostic.Code,
                Is.EqualTo(SandboxDiagnosticCode.InvalidSpawnPosition));
        }

        [Test]
        public void PoolDiagnosticsSnapshot_IsSortedAndContainsEveryMetric()
        {
            StepSixUnitPoolSpec zPool = new StepSixUnitPoolSpec(
                new PoolId("ZPool"),
                UnitFaction.Enemy);
            StepSixUnitPoolSpec aPool = new StepSixUnitPoolSpec(
                new PoolId("APool"),
                UnitFaction.Ally);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(zPool, aPool);
            environment.SpawnManager.SpawnUnit(new UnitSpawnRequest(
                zPool.Definition,
                Vector3.zero,
                Quaternion.identity,
                default,
                SpawnReason.Debug));
            List<PoolDiagnostics> diagnostics =
                new List<PoolDiagnostics>();

            Assert.That(environment.PoolManager.CopyDiagnostics(diagnostics),
                Is.EqualTo(2));
            Assert.That(diagnostics[0].PoolId.Value, Is.EqualTo("APool"));
            Assert.That(diagnostics[1].PoolId.Value, Is.EqualTo("ZPool"));
            Assert.That(diagnostics[1].ActiveCount, Is.EqualTo(1));
            Assert.That(diagnostics[1].CreatedCount, Is.EqualTo(1));
            Assert.That(diagnostics[1].PeakActiveCount, Is.EqualTo(1));
            Assert.That(diagnostics[1].FailedRentCount, Is.Zero);
            Assert.That(diagnostics[1].CapacityReachedCount, Is.Zero);
            Assert.That(diagnostics[1].OverflowDestroyCount, Is.Zero);
        }

        private DebugUnitSpawner CreateSpawner(
            StepSixSpawnEnvironment environment,
            params UnitDefinition[] definitions)
        {
            UnitCatalog catalog = ScriptableObject.CreateInstance<UnitCatalog>();
            _createdObjects.Add(catalog);
            UnitCatalogEntry[] entries =
                new UnitCatalogEntry[definitions.Length];
            for (int definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                entries[definitionIndex] = new UnitCatalogEntry();
                StepSixSpawnFactory.SetAutoProperty(
                    entries[definitionIndex],
                    nameof(UnitCatalogEntry.Definition),
                    definitions[definitionIndex]);
            }

            FieldInfo entriesField = typeof(UnitCatalog).GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            entriesField.SetValue(catalog, entries);

            SpawnPointGroup allyPoints = CreateSpawnPoints("AllyPoints");
            SpawnPointGroup enemyPoints = CreateSpawnPoints("EnemyPoints");
            DebugUnitSpawner spawner = _factory
                .CreateGameObject("DebugUnitSpawner")
                .AddComponent<DebugUnitSpawner>();
            StepSixSpawnFactory.SetAutoProperty(
                spawner,
                nameof(DebugUnitSpawner.SpawnManager),
                environment.SpawnManager);
            StepSixSpawnFactory.SetAutoProperty(
                spawner,
                nameof(DebugUnitSpawner.PoolManager),
                environment.PoolManager);
            StepSixSpawnFactory.SetAutoProperty(
                spawner,
                nameof(DebugUnitSpawner.UnitRegistry),
                environment.UnitRegistry);
            StepSixSpawnFactory.SetAutoProperty(
                spawner,
                nameof(DebugUnitSpawner.UnitCatalog),
                catalog);
            InteractionSystem interactionSystem = _factory
                .CreateGameObject("InteractionSystem")
                .AddComponent<InteractionSystem>();
            StepSixSpawnFactory.SetAutoProperty(
                spawner,
                nameof(DebugUnitSpawner.InteractionSystem),
                interactionSystem);
            StepSixSpawnFactory.SetAutoProperty(
                spawner,
                nameof(DebugUnitSpawner.AllySpawnPoints),
                allyPoints);
            StepSixSpawnFactory.SetAutoProperty(
                spawner,
                nameof(DebugUnitSpawner.EnemySpawnPoints),
                enemyPoints);
            return spawner;
        }

        private SpawnPointGroup CreateSpawnPoints(string objectName)
        {
            GameObject groupObject = _factory.CreateGameObject(objectName);
            GameObject pointObject = _factory.CreateGameObject(objectName + "Point");
            pointObject.transform.SetParent(groupObject.transform, false);
            SpawnPointGroup spawnPoints =
                groupObject.AddComponent<SpawnPointGroup>();
            spawnPoints.Configure(new[] { pointObject.transform });
            return spawnPoints;
        }

        private static void SetDefinitionIdentity(
            UnitDefinition definition,
            string unitId)
        {
            StepSixSpawnFactory.SetAutoProperty(
                definition,
                nameof(UnitDefinition.UnitId),
                new UnitId(unitId));
            StepSixSpawnFactory.SetAutoProperty(
                definition,
                nameof(UnitDefinition.DisplayName),
                unitId);
        }

        private static PoolDiagnostics GetDiagnostics(
            PoolManager poolManager,
            PoolId poolId)
        {
            Assert.That(poolManager.TryGetDiagnostics(
                poolId,
                out PoolDiagnostics diagnostics), Is.True);
            return diagnostics;
        }
    }

    public sealed class StepFourteenAIPauseTests
    {
        [Test]
        public void PauseAIDecisions_StopsMovementWithoutChangingTargetOrState()
        {
            using (AIRuntimeHarness harness = new AIRuntimeHarness())
            {
                UnitController ally = harness.SpawnAI(
                    harness.AllyDefinition,
                    new Vector3(-6f, 0f, 3f));
                UnitController target = harness.Spawn(
                    harness.StationaryEnemyDefinition,
                    new Vector3(-1f, 0f, 3f));
                Physics.SyncTransforms();
                Assert.That(ally.TargetingController.ForceScan(), Is.True);
                AIUnitBrain brain = ally.GetComponent<AIUnitBrain>();
                StepElevenMotorProbe motor =
                    ally.GetComponent<StepElevenMotorProbe>();
                brain.AdvanceDecision(0f);
                brain.AdvanceDecision(0f);
                Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));
                Assert.That(motor.IsStopped, Is.False);

                Assert.That(SandboxDebugRuntime.SetAIDecisionsPaused(true),
                    Is.True);
                brain.AdvanceDecision(1f);

                Assert.That(motor.IsStopped, Is.True);
                Assert.That(brain.State, Is.EqualTo(AIUnitState.Chase));
                Assert.That(ally.TargetingController.CurrentTarget,
                    Is.SameAs(target));
                SandboxDebugRuntime.SetAIDecisionsPaused(false);
                brain.AdvanceDecision(0f);
                Assert.That(motor.IsStopped, Is.False);
            }
        }
    }
}
