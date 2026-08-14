using System;
using MonstersVsZombies.Core;
using MonstersVsZombies.Spawning;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonstersVsZombies.Diagnostics
{
    /// <summary>
    /// Binds the development-only SandboxDebug Input System actions to panel,
    /// spawning, clear, and reset commands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxDebugInputController : MonoBehaviour
    {
        private static readonly UnitId[] s_spawnUnitIds =
        {
            new UnitId("EnemyClassicMelee"),
            new UnitId("EnemyClassicRange"),
            new UnitId("EnemyDragon"),
            new UnitId("EnemyStunner"),
            new UnitId("EnemyDivisible"),
            new UnitId("AllyClassicMelee"),
            new UnitId("AllyClassicRange"),
            new UnitId("AllyDragon"),
            new UnitId("AllyDoubleHead"),
            new UnitId("EnemyMiniDivisible")
        };

        [field: SerializeField] public InputActionAsset InputActions { get; private set; }
        [field: SerializeField] public DebugUnitSpawner DebugUnitSpawner { get; private set; }
        [field: SerializeField] public GameObject PanelRoot { get; private set; }

        public bool IsInputEnabled { get; private set; }

        private InputActionMap _debugActionMap;

        private void OnEnable()
        {
            if (!SandboxDebugRuntime.IsAvailable)
            {
                if (PanelRoot != null)
                {
                    PanelRoot.SetActive(false);
                }

                enabled = false;
                return;
            }

            EnableInput();
        }

        private void OnDisable()
        {
            DisableInput();
        }

        private void OnDestroy()
        {
            SandboxDebugRuntime.SetAIDecisionsPaused(false);
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            InputActionMap actionMap = InputActions?.FindActionMap(
                "SandboxDebug",
                false);
            if (actionMap == null || DebugUnitSpawner == null || PanelRoot == null)
            {
                failureMessage =
                    "SandboxDebugInputController requires the SandboxDebug action map, DebugUnitSpawner, and panel root.";
                return false;
            }

            string[] requiredActions =
            {
                "TogglePanel",
                "Spawn1",
                "Spawn2",
                "Spawn3",
                "Spawn4",
                "Spawn5",
                "Spawn6",
                "Spawn7",
                "Spawn8",
                "Spawn9",
                "Spawn0",
                "Clear"
            };
            foreach (string actionName in requiredActions)
            {
                if (actionMap.FindAction(actionName, false) == null)
                {
                    failureMessage =
                        $"SandboxDebug action map is missing '{actionName}'.";
                    return false;
                }
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool Configure(
            InputActionAsset inputActions,
            DebugUnitSpawner debugUnitSpawner,
            GameObject panelRoot)
        {
            DisableInput();
            InputActions = inputActions;
            DebugUnitSpawner = debugUnitSpawner;
            PanelRoot = panelRoot;
            if (isActiveAndEnabled && SandboxDebugRuntime.IsAvailable)
            {
                EnableInput();
            }

            return ValidateConfiguration(out _);
        }

        private void EnableInput()
        {
            if (IsInputEnabled || !ValidateConfiguration(out _))
            {
                return;
            }

            _debugActionMap = InputActions.FindActionMap("SandboxDebug", true);
            foreach (InputAction action in _debugActionMap.actions)
            {
                action.performed += HandleDebugAction;
            }

            _debugActionMap.Enable();
            IsInputEnabled = true;
        }

        private void DisableInput()
        {
            if (_debugActionMap != null)
            {
                foreach (InputAction action in _debugActionMap.actions)
                {
                    action.performed -= HandleDebugAction;
                }

                _debugActionMap.Disable();
                _debugActionMap = null;
            }

            IsInputEnabled = false;
        }

        private void HandleDebugAction(InputAction.CallbackContext context)
        {
            switch (context.action.name)
            {
                case "TogglePanel":
                    PanelRoot.SetActive(!PanelRoot.activeSelf);
                    break;
                case "Clear":
                    DebugUnitSpawner.ClearNonPlayerUnitsAndProjectiles();
                    break;
                default:
                    if (TryGetMappedUnitId(
                            context.action.name,
                            out UnitId unitId))
                    {
                        DebugUnitSpawner.Spawn(unitId);
                    }

                    break;
            }
        }

        private static int ParseSpawnIndex(string actionName)
        {
            if (actionName == "Spawn0")
            {
                return 9;
            }

            if (actionName != null && actionName.Length == 6 &&
                actionName.StartsWith("Spawn", StringComparison.Ordinal) &&
                actionName[5] >= '1' && actionName[5] <= '9')
            {
                return actionName[5] - '1';
            }

            return -1;
        }

        public static bool TryGetMappedUnitId(
            string actionName,
            out UnitId unitId)
        {
            int spawnIndex = ParseSpawnIndex(actionName);
            if (spawnIndex < 0)
            {
                unitId = default;
                return false;
            }

            unitId = s_spawnUnitIds[spawnIndex];
            return true;
        }
    }
}
