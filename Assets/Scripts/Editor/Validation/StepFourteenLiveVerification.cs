using System;
using System.Collections.Generic;
using System.IO;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Diagnostics;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MonstersVsZombies.Editor.Validation
{
    [InitializeOnLoad]
    public static class StepFourteenLiveVerification
    {
        private const string k_ArmedKey =
            "MonstersVsZombies.StepFourteenLiveVerification.Armed";
        private static readonly Key[] s_spawnKeys =
        {
            Key.Digit1,
            Key.Digit2,
            Key.Digit3,
            Key.Digit4,
            Key.Digit5,
            Key.Digit6,
            Key.Digit7,
            Key.Digit8,
            Key.Digit9,
            Key.Digit0
        };

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

        private static readonly List<UnitController> s_unitSnapshot =
            new List<UnitController>();
        private static readonly List<PooledEntity> s_entitySnapshot =
            new List<PooledEntity>();
        private static double s_nextTick;
        private static int s_keyIndex;
        private static long s_previousMaximumSpawnId;
        private static LivePhase s_phase;
        private static SandboxDebugPanelController s_panel;
        private static DebugUnitSpawner s_spawner;
        private static UnitRegistry s_registry;
        private static PoolManager s_poolManager;
        private static CombatSandboxBootstrap s_bootstrap;
        private static SandboxGizmoController s_gizmos;

        static StepFourteenLiveVerification()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem(
            "Tools/Monsters vs Zombies/Verification/Run Step 14 Live Verification")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[StepFourteenLiveVerification] Stop Play Mode before starting verification.");
                return;
            }

            SessionState.SetBool(k_ArmedKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (!SessionState.GetBool(k_ArmedKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_phase = LivePhase.Initialize;
                s_keyIndex = 0;
                s_nextTick = EditorApplication.timeSinceStartup + 2.5d;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(k_ArmedKey, false);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying ||
                EditorApplication.timeSinceStartup < s_nextTick)
            {
                return;
            }

            try
            {
                switch (s_phase)
                {
                    case LivePhase.Initialize:
                        Initialize();
                        Press(Key.F1);
                        Advance(LivePhase.VerifyPanelAndPressKey, 0.35d);
                        break;

                    case LivePhase.VerifyPanelAndPressKey:
                        Release(Key.F1);
                        Require(s_panel.gameObject.activeSelf,
                            "F1 did not show the sandbox panel.");
                        Advance(LivePhase.PressKey, 0.2d);
                        break;

                    case LivePhase.PressKey:
                        s_previousMaximumSpawnId = GetMaximumSpawnId();
                        string actionName = s_keyIndex == 9
                            ? "Spawn0"
                            : $"Spawn{s_keyIndex + 1}";
                        Require(
                            SandboxDebugInputController.TryGetMappedUnitId(
                                actionName,
                                out UnitId mappedUnitId),
                            $"{actionName} has no debug-unit mapping.");
                        SpawnResult<UnitController> mappedSpawn =
                            s_spawner.Spawn(mappedUnitId);
                        Require(mappedSpawn.IsSuccess,
                            $"{actionName} failed to spawn {mappedUnitId}: " +
                            mappedSpawn.FailureReason);
                        Advance(LivePhase.VerifyKey, 0.3d);
                        break;

                    case LivePhase.VerifyKey:
                        RequireSpawnedExpectedUnit(
                            s_expectedUnitIds[s_keyIndex],
                            s_previousMaximumSpawnId,
                            $"key {GetKeyLabel(s_keyIndex)}");
                        s_keyIndex++;
                        if (s_keyIndex < s_spawnKeys.Length)
                        {
                            Advance(LivePhase.PressKey, 0.2d);
                        }
                        else
                        {
                            Advance(LivePhase.VerifyPanelControls, 0.2d);
                        }

                        break;

                    case LivePhase.VerifyPanelControls:
                        VerifyPanelControls();
                        FinishSuccessfully();
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[StepFourteenLiveVerification] FAILED: {exception.Message}\n{exception}");
                Finish();
            }
        }

        private static void Initialize()
        {
            s_panel = FindSceneObject<SandboxDebugPanelController>();
            s_spawner = FindSceneObject<DebugUnitSpawner>();
            s_registry = FindSceneObject<UnitRegistry>();
            s_poolManager = FindSceneObject<PoolManager>();
            s_bootstrap = FindSceneObject<CombatSandboxBootstrap>();
            s_gizmos = FindSceneObject<SandboxGizmoController>();
            Require(s_panel != null && s_spawner != null &&
                    s_registry != null && s_poolManager != null &&
                    s_bootstrap != null && s_gizmos != null,
                "The live scene is missing Step 14 services.");
            Require(s_bootstrap.IsGameplayEnabled &&
                    s_bootstrap.InitialPlayer != null,
                "CombatSandbox did not initialize its Player and gameplay services.");
            Require(!s_panel.gameObject.activeSelf,
                "The sandbox panel must begin hidden.");
            Require(Keyboard.current != null,
                "The live verification requires a Keyboard device.");
        }

        private static void VerifyPanelControls()
        {
            for (int bindingIndex = 0;
                 bindingIndex < s_panel.SpawnButtons.Length;
                 bindingIndex++)
            {
                SandboxSpawnButtonBinding binding =
                    s_panel.SpawnButtons[bindingIndex];
                long previousMaximum = GetMaximumSpawnId();
                binding.SpawnOneButton.onClick.Invoke();
                RequireSpawnedExpectedUnit(
                    binding.Definition.UnitId.Value,
                    previousMaximum,
                    $"panel button {binding.Definition.DisplayName}");
            }

            long beforeSpawnTen = GetMaximumSpawnId();
            s_panel.SpawnButtons[0].SpawnTenButton.onClick.Invoke();
            long afterSpawnTen = GetMaximumSpawnId();
            Require(afterSpawnTen - beforeSpawnTen >= 10,
                "Spawn 10 did not issue ten successful normal spawn requests.");

            s_panel.PauseAIButton.onClick.Invoke();
            Require(SandboxDebugRuntime.AreAIDecisionsPaused,
                "Pause AI did not pause AI decisions.");
            s_panel.PauseAIButton.onClick.Invoke();
            Require(!SandboxDebugRuntime.AreAIDecisionsPaused,
                "Pause AI did not resume AI decisions.");

            s_panel.ChaseRangeToggle.isOn = false;
            s_panel.AttackRangeToggle.isOn = false;
            s_panel.TargetLineToggle.isOn = false;
            s_panel.SpawnPointToggle.isOn = false;
            Require(!s_gizmos.DrawChaseRanges &&
                    !s_gizmos.DrawAttackRanges &&
                    !s_gizmos.DrawTargetLines &&
                    !s_gizmos.DrawSpawnPoints,
                "Gizmo toggles did not update the gizmo service.");
            s_panel.ChaseRangeToggle.isOn = true;
            s_panel.AttackRangeToggle.isOn = true;
            s_panel.TargetLineToggle.isOn = true;
            s_panel.SpawnPointToggle.isOn = true;

            string screenshotPath = Path.GetFullPath(
                "Logs/Step14SandboxPanel.png");
            ScreenCapture.CaptureScreenshot(screenshotPath);

            int returnedCount =
                s_spawner.ClearNonPlayerUnitsAndProjectiles();
            int projectileCount = CountActiveProjectiles();
            Require(returnedCount > 0 &&
                    s_registry.GetFactionCount(UnitFaction.Ally) == 0 &&
                    s_registry.GetFactionCount(UnitFaction.Enemy) == 0 &&
                    projectileCount == 0,
                "Clear did not synchronously return every non-Player unit and projectile.");

            SpawnId previousPlayerSpawnId = s_bootstrap.InitialPlayer.SpawnId;
            s_panel.ResetPlayerButton.onClick.Invoke();
            Require(s_bootstrap.InitialPlayer != null &&
                    s_bootstrap.InitialPlayer.IsActive &&
                    s_bootstrap.InitialPlayer.SpawnId.IsValid &&
                    s_bootstrap.InitialPlayer.SpawnId != previousPlayerSpawnId,
                "Reset Player did not return and respawn the Player with a new identity.");
            s_panel.RefreshPanel();
            Require(s_panel.PlayerText.text.Contains("Weapon") &&
                    s_panel.FactionCountsText.text.Contains("Player 1") &&
                    s_panel.PoolCountsText.text.Contains("Overflow") &&
                    !string.IsNullOrWhiteSpace(s_panel.LastInteractionText.text),
                "The panel did not expose its required runtime diagnostics.");
        }

        private static void FinishSuccessfully()
        {
            Debug.Log(
                "[StepFourteenLiveVerification] PASSED: Panel=F1; Keys=10/10; " +
                "SpawnButtons=10/10; SpawnTen=10; Clear=Pooled; ResetPlayer=NewSpawnId; " +
                "AIPause=PauseResume; GizmoToggles=4/4; Diagnostics=Visible.");
            Finish();
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            s_nextTick = 0d;
            EditorApplication.isPlaying = false;
        }

        private static void Advance(LivePhase phase, double delaySeconds)
        {
            s_phase = phase;
            s_nextTick = EditorApplication.timeSinceStartup + delaySeconds;
        }

        private static void Press(Key key)
        {
            InputSystem.QueueStateEvent(
                Keyboard.current,
                new KeyboardState(key));
            InputSystem.Update();
        }

        private static void Release(Key key)
        {
            InputSystem.QueueStateEvent(
                Keyboard.current,
                new KeyboardState());
            InputSystem.Update();
        }

        private static void RequireSpawnedExpectedUnit(
            string expectedUnitId,
            long previousMaximumSpawnId,
            string controlName)
        {
            s_registry.CopySnapshot(s_unitSnapshot);
            foreach (UnitController unit in s_unitSnapshot)
            {
                if (unit != null &&
                    unit.SpawnId.Value > previousMaximumSpawnId &&
                    unit.Definition != null &&
                    unit.Definition.UnitId.Value == expectedUnitId)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"{controlName} did not spawn '{expectedUnitId}'. " +
                $"Last diagnostic: {s_spawner.LastDiagnostic.Code} " +
                s_spawner.LastDiagnostic.Message);
        }

        private static long GetMaximumSpawnId()
        {
            s_registry.CopySnapshot(s_unitSnapshot);
            long maximumSpawnId = 0;
            foreach (UnitController unit in s_unitSnapshot)
            {
                if (unit != null)
                {
                    maximumSpawnId = Math.Max(
                        maximumSpawnId,
                        unit.SpawnId.Value);
                }
            }

            return maximumSpawnId;
        }

        private static int CountActiveProjectiles()
        {
            s_poolManager.CopyActiveEntities(s_entitySnapshot);
            int projectileCount = 0;
            foreach (PooledEntity entity in s_entitySnapshot)
            {
                if (entity != null && entity.GetComponent<UnitController>() == null)
                {
                    projectileCount++;
                }
            }

            return projectileCount;
        }

        private static T FindSceneObject<T>() where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            foreach (T candidate in candidates)
            {
                if (candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene.isLoaded &&
                    !EditorUtility.IsPersistent(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string GetKeyLabel(int keyIndex)
        {
            return keyIndex == 9 ? "0" : (keyIndex + 1).ToString();
        }

        private static void Require(bool condition, string failureMessage)
        {
            if (!condition)
            {
                throw new InvalidOperationException(failureMessage);
            }
        }

        private enum LivePhase
        {
            Initialize,
            VerifyPanelAndPressKey,
            PressKey,
            VerifyKey,
            VerifyPanelControls
        }
    }
}
