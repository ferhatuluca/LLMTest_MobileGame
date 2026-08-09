using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Diagnostics;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepFourteenDeveloperControlsSetup
    {
        private const string k_InputAssetPath =
            "Assets/InputSystem_Actions.inputactions";
        private const string k_UnitCatalogPath =
            "Assets/Data/Catalogs/UC_CombatSandbox.asset";
        private const string k_PoolCatalogPath =
            "Assets/Data/Catalogs/PC_ProjectilePools.asset";
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";

        private static readonly string[] s_definitionPaths =
        {
            "Assets/Data/Units/UD_Enemy_ClassicMelee.asset",
            "Assets/Data/Units/UD_Enemy_ClassicRange.asset",
            "Assets/Data/Units/UD_Enemy_Dragon.asset",
            "Assets/Data/Units/UD_Enemy_Stunner.asset",
            "Assets/Data/Units/UD_Enemy_Divisible.asset",
            "Assets/Data/Units/UD_Ally_ClassicMelee.asset",
            "Assets/Data/Units/UD_Ally_ClassicRange.asset",
            "Assets/Data/Units/UD_Ally_Dragon.asset",
            "Assets/Data/Units/UD_Ally_DoubleHead.asset",
            "Assets/Data/Units/UD_Enemy_MiniDivisible.asset"
        };

        [MenuItem(
            "Tools/Monsters vs Zombies/Step 14/Create and Verify Developer Controls")]
        public static void CreateAndVerifyDeveloperControls()
        {
            InputActionAsset inputActions = ConfigureInputAsset();
            AIUnitDefinition[] definitions = LoadConcreteDefinitions();
            UnitCatalog unitCatalog = LoadRequiredAsset<UnitCatalog>(
                k_UnitCatalogPath);
            PoolCatalog poolCatalog = LoadRequiredAsset<PoolCatalog>(
                k_PoolCatalogPath);
            UpdateCombatSandbox(
                inputActions,
                definitions,
                unitCatalog,
                poolCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateConcreteDefinitionsAndPrefabs();
            VerifyDeveloperControls();
            Debug.Log(
                "[StepFourteenDeveloperControlsSetup] Created and verified the SandboxDebug input map, all spawn controls, pooled clear/reset, diagnostics panel, and gizmo service.");
        }

        [MenuItem(
            "Tools/Monsters vs Zombies/Validation/Validate Concrete Definitions and Prefabs")]
        public static void ValidateConcreteDefinitionsAndPrefabs()
        {
            AIUnitDefinition[] definitions = LoadConcreteDefinitions();
            UnitCatalog unitCatalog = LoadRequiredAsset<UnitCatalog>(
                k_UnitCatalogPath);
            PoolCatalog poolCatalog = LoadRequiredAsset<PoolCatalog>(
                k_PoolCatalogPath);
            foreach (AIUnitDefinition definition in definitions)
            {
                if (!definition.Validate().IsValid ||
                    !unitCatalog.TryGetDefinition(
                        definition.UnitId,
                        out UnitDefinition catalogDefinition) ||
                    catalogDefinition != definition ||
                    !poolCatalog.TryGetEntry(
                        definition.PoolId,
                        out PoolCatalogEntry poolEntry) ||
                    poolEntry.Prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Concrete definition '{definition.name}' is invalid or missing from a catalog.");
                }

                UnitController unit = poolEntry.Prefab.GetComponent<UnitController>();
                AIUnitBrain brain = poolEntry.Prefab.GetComponent<AIUnitBrain>();
                string failureMessage = string.Empty;
                if (unit == null || unit.Definition != definition ||
                    brain == null ||
                    !unit.ValidateGameplayComponents(out failureMessage) ||
                    !brain.ValidateConfiguration(out failureMessage))
                {
                    throw new InvalidOperationException(
                        $"Concrete prefab '{poolEntry.Prefab.name}' failed validation: {failureMessage}");
                }
            }

            Debug.Log(
                $"[StepFourteenConcreteValidation] Verified {definitions.Length} concrete AI definitions and their catalog-backed prefabs.");
        }

        private static InputActionAsset ConfigureInputAsset()
        {
            InputActionAsset inputAsset =
                LoadRequiredAsset<InputActionAsset>(k_InputAssetPath);
            InputActionMap previousMap = inputAsset.FindActionMap(
                "SandboxDebug",
                false);
            if (previousMap != null)
            {
                inputAsset.RemoveActionMap(previousMap);
            }

            InputActionMap debugMap = new InputActionMap("SandboxDebug");
            AddButtonAction(debugMap, "TogglePanel", "<Keyboard>/f1");
            for (int keyNumber = 1; keyNumber <= 9; keyNumber++)
            {
                AddButtonAction(
                    debugMap,
                    $"Spawn{keyNumber}",
                    $"<Keyboard>/digit{keyNumber}");
            }

            AddButtonAction(debugMap, "Spawn0", "<Keyboard>/digit0");
            AddButtonAction(debugMap, "Clear", "<Keyboard>/backspace");
            inputAsset.AddActionMap(debugMap);

            File.WriteAllText(
                Path.GetFullPath(k_InputAssetPath),
                inputAsset.ToJson());
            AssetDatabase.ImportAsset(
                k_InputAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            return LoadRequiredAsset<InputActionAsset>(k_InputAssetPath);
        }

        private static void AddButtonAction(
            InputActionMap actionMap,
            string actionName,
            string bindingPath)
        {
            InputAction action = actionMap.AddAction(
                actionName,
                InputActionType.Button);
            action.expectedControlType = "Button";
            action.AddBinding(bindingPath, groups: "Keyboard&Mouse");
        }

        private static AIUnitDefinition[] LoadConcreteDefinitions()
        {
            AIUnitDefinition[] definitions =
                new AIUnitDefinition[s_definitionPaths.Length];
            for (int definitionIndex = 0;
                 definitionIndex < s_definitionPaths.Length;
                 definitionIndex++)
            {
                definitions[definitionIndex] =
                    LoadRequiredAsset<AIUnitDefinition>(
                        s_definitionPaths[definitionIndex]);
            }

            return definitions;
        }

        private static void UpdateCombatSandbox(
            InputActionAsset inputActions,
            AIUnitDefinition[] definitions,
            UnitCatalog unitCatalog,
            PoolCatalog poolCatalog)
        {
            Scene scene = EditorSceneManager.OpenScene(
                k_ScenePath,
                OpenSceneMode.Single);
            Transform systems = RequireRoot(scene, "__Systems");
            Transform ui = RequireRoot(scene, "UI");
            Transform spawnPoints = RequireRoot(scene, "SpawnPoints");

            PoolManager poolManager = RequireComponentInChildren<PoolManager>(
                systems,
                "PoolManager");
            SpawnManager spawnManager = RequireComponentInChildren<SpawnManager>(
                systems,
                "SpawnManager");
            UnitRegistry unitRegistry = RequireComponentInChildren<UnitRegistry>(
                systems,
                "UnitRegistry");
            InteractionSystem interactionSystem =
                RequireComponentInChildren<InteractionSystem>(
                    systems,
                    "InteractionSystem");
            CombatSandboxBootstrap bootstrap =
                RequireComponentInChildren<CombatSandboxBootstrap>(
                    systems,
                    "CombatSandboxBootstrap");
            DebugUnitSpawner debugSpawner =
                RequireComponentInChildren<DebugUnitSpawner>(
                    systems,
                    "DebugUnitSpawner");
            SpawnPointGroup playerPoints =
                RequireSpawnPointGroup(spawnPoints, "PlayerSpawn");
            SpawnPointGroup allyPoints =
                RequireSpawnPointGroup(spawnPoints, "AllySpawnPoints");
            SpawnPointGroup enemyPoints =
                RequireSpawnPointGroup(spawnPoints, "EnemySpawnPoints");

            debugSpawner.Configure(
                spawnManager,
                poolManager,
                unitRegistry,
                unitCatalog,
                interactionSystem,
                allyPoints,
                enemyPoints,
                bootstrap);
            EditorUtility.SetDirty(debugSpawner);

            DestroyChildIfPresent(systems, "SandboxDiagnostics");
            GameObject diagnosticsObject = CreateChild(
                systems,
                "SandboxDiagnostics");
            SandboxGizmoController gizmos =
                diagnosticsObject.AddComponent<SandboxGizmoController>();
            gizmos.Configure(
                unitRegistry,
                playerPoints,
                allyPoints,
                enemyPoints);

            DestroyChildIfPresent(ui, "CombatSandboxPanelCanvas");
            PanelAssets panelAssets = CreatePanel(
                ui,
                definitions,
                debugSpawner,
                poolManager,
                poolCatalog,
                unitRegistry,
                interactionSystem,
                bootstrap,
                gizmos);

            SandboxDebugInputController debugInput =
                diagnosticsObject.AddComponent<SandboxDebugInputController>();
            if (!debugInput.Configure(
                    inputActions,
                    debugSpawner,
                    panelAssets.PanelRoot))
            {
                throw new InvalidOperationException(
                    "Could not configure SandboxDebugInputController.");
            }

            panelAssets.PanelRoot.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, k_ScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save CombatSandbox developer controls.");
            }
        }

        private static PanelAssets CreatePanel(
            Transform ui,
            AIUnitDefinition[] definitions,
            DebugUnitSpawner debugSpawner,
            PoolManager poolManager,
            PoolCatalog poolCatalog,
            UnitRegistry unitRegistry,
            InteractionSystem interactionSystem,
            CombatSandboxBootstrap bootstrap,
            SandboxGizmoController gizmos)
        {
            GameObject canvasObject = CreateChild(
                ui,
                "CombatSandboxPanelCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panel = CreateUIObject(
                canvasObject.transform,
                "CombatSandboxPanel",
                new Vector2(1760f, 970f),
                new Vector2(0f, 1f),
                new Vector2(40f, -40f));
            Image background = panel.AddComponent<Image>();
            background.color = new Color(0.035f, 0.045f, 0.065f, 0.96f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text title = CreateText(
                panel.transform,
                "Title",
                "COMBAT SANDBOX  |  F1 hide",
                font,
                24,
                FontStyle.Bold,
                new Vector2(1690f, 34f),
                new Vector2(24f, -18f));
            title.color = new Color(0.55f, 0.9f, 1f);
            Text playerText = CreateText(
                panel.transform,
                "PlayerStatus",
                "Player",
                font,
                18,
                FontStyle.Bold,
                new Vector2(800f, 30f),
                new Vector2(24f, -60f));
            Text factionText = CreateText(
                panel.transform,
                "FactionCounts",
                "Active units",
                font,
                18,
                FontStyle.Normal,
                new Vector2(800f, 30f),
                new Vector2(24f, -94f));
            Text interactionText = CreateText(
                panel.transform,
                "LastInteraction",
                "Last interaction: none",
                font,
                16,
                FontStyle.Normal,
                new Vector2(800f, 54f),
                new Vector2(24f, -128f));

            SandboxSpawnButtonBinding[] bindings =
                new SandboxSpawnButtonBinding[definitions.Length];
            for (int definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                float y = -205f - definitionIndex * 52f;
                Text label = CreateText(
                    panel.transform,
                    $"UnitLabel_{definitionIndex}",
                    GetDocumentedShortcut(definitionIndex) + "  " +
                    definitions[definitionIndex].DisplayName,
                    font,
                    16,
                    FontStyle.Normal,
                    new Vector2(310f, 38f),
                    new Vector2(24f, y));
                label.alignment = TextAnchor.MiddleLeft;
                Button spawnOne = CreateButton(
                    panel.transform,
                    $"SpawnOne_{definitions[definitionIndex].UnitId}",
                    "Spawn 1",
                    font,
                    new Vector2(130f, 38f),
                    new Vector2(340f, y));
                Button spawnTen = CreateButton(
                    panel.transform,
                    $"SpawnTen_{definitions[definitionIndex].UnitId}",
                    "Spawn 10",
                    font,
                    new Vector2(140f, 38f),
                    new Vector2(480f, y));
                bindings[definitionIndex] = new SandboxSpawnButtonBinding(
                    definitions[definitionIndex],
                    spawnOne,
                    spawnTen);
            }

            Button clearButton = CreateButton(
                panel.transform,
                "ClearButton",
                "Clear non-Player + projectiles  [Backspace]",
                font,
                new Vector2(596f, 42f),
                new Vector2(24f, -742f));
            Button resetButton = CreateButton(
                panel.transform,
                "ResetPlayerButton",
                "Reset Player",
                font,
                new Vector2(190f, 42f),
                new Vector2(24f, -794f));
            Button pauseButton = CreateButton(
                panel.transform,
                "PauseAIButton",
                "Pause AI decisions",
                font,
                new Vector2(250f, 42f),
                new Vector2(224f, -794f));
            Text pauseText = pauseButton.GetComponentInChildren<Text>();

            Toggle chaseToggle = CreateToggle(
                panel.transform,
                "ChaseRangeToggle",
                "Chase range (yellow)",
                font,
                new Vector2(24f, -852f));
            Toggle attackToggle = CreateToggle(
                panel.transform,
                "AttackRangeToggle",
                "Attack range (red)",
                font,
                new Vector2(280f, -852f));
            Toggle targetToggle = CreateToggle(
                panel.transform,
                "TargetLineToggle",
                "Current target (cyan)",
                font,
                new Vector2(536f, -852f));
            Toggle spawnToggle = CreateToggle(
                panel.transform,
                "SpawnPointToggle",
                "Spawn/faction markers",
                font,
                new Vector2(792f, -852f));

            Text poolText = CreateText(
                panel.transform,
                "PoolCounts",
                "Pool diagnostics",
                font,
                13,
                FontStyle.Normal,
                new Vector2(1080f, 760f),
                new Vector2(650f, -70f));
            poolText.alignment = TextAnchor.UpperLeft;
            poolText.horizontalOverflow = HorizontalWrapMode.Overflow;
            poolText.verticalOverflow = VerticalWrapMode.Overflow;

            SandboxDebugPanelController controller =
                panel.AddComponent<SandboxDebugPanelController>();
            SetAutoProperty(controller, nameof(controller.DebugUnitSpawner), debugSpawner);
            SetAutoProperty(controller, nameof(controller.PoolManager), poolManager);
            SetAutoProperty(controller, nameof(controller.PoolCatalog), poolCatalog);
            SetAutoProperty(controller, nameof(controller.UnitRegistry), unitRegistry);
            SetAutoProperty(controller, nameof(controller.InteractionSystem), interactionSystem);
            SetAutoProperty(controller, nameof(controller.Bootstrap), bootstrap);
            SetAutoProperty(controller, nameof(controller.GizmoController), gizmos);
            SetAutoProperty(controller, nameof(controller.PlayerText), playerText);
            SetAutoProperty(controller, nameof(controller.FactionCountsText), factionText);
            SetAutoProperty(controller, nameof(controller.PoolCountsText), poolText);
            SetAutoProperty(controller, nameof(controller.LastInteractionText), interactionText);
            SetAutoProperty(controller, nameof(controller.ClearButton), clearButton);
            SetAutoProperty(controller, nameof(controller.ResetPlayerButton), resetButton);
            SetAutoProperty(controller, nameof(controller.PauseAIButton), pauseButton);
            SetAutoProperty(controller, nameof(controller.PauseAIButtonText), pauseText);
            SetAutoProperty(controller, nameof(controller.ChaseRangeToggle), chaseToggle);
            SetAutoProperty(controller, nameof(controller.AttackRangeToggle), attackToggle);
            SetAutoProperty(controller, nameof(controller.TargetLineToggle), targetToggle);
            SetAutoProperty(controller, nameof(controller.SpawnPointToggle), spawnToggle);
            SetAutoProperty(controller, nameof(controller.SpawnButtons), bindings);
            if (!controller.ValidateConfiguration(out string failureMessage))
            {
                throw new InvalidOperationException(failureMessage);
            }

            EnsureEventSystem(ui);
            return new PanelAssets(canvasObject, panel);
        }

        private static string GetDocumentedShortcut(int index)
        {
            return index == 9 ? "[0]" : $"[{index + 1}]";
        }

        private static void EnsureEventSystem(Transform ui)
        {
            EventSystem eventSystem =
                ui.GetComponentInChildren<EventSystem>(true);
            if (eventSystem != null)
            {
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                StandaloneInputModule legacy =
                    eventSystem.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacy);
                }

                return;
            }

            GameObject eventObject = CreateChild(ui, "EventSystem");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<InputSystemUIInputModule>();
        }

        private static Button CreateButton(
            Transform parent,
            string objectName,
            string label,
            Font font,
            Vector2 size,
            Vector2 position)
        {
            GameObject buttonObject = CreateUIObject(
                parent,
                objectName,
                size,
                new Vector2(0f, 1f),
                position);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.25f, 0.36f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = CreateText(
                buttonObject.transform,
                "Label",
                label,
                font,
                14,
                FontStyle.Bold,
                size,
                Vector2.zero);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Toggle CreateToggle(
            Transform parent,
            string objectName,
            string label,
            Font font,
            Vector2 position)
        {
            GameObject toggleObject = CreateUIObject(
                parent,
                objectName,
                new Vector2(250f, 36f),
                new Vector2(0f, 1f),
                position);
            Toggle toggle = toggleObject.AddComponent<Toggle>();
            GameObject backgroundObject = CreateUIObject(
                toggleObject.transform,
                "Background",
                new Vector2(26f, 26f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f));
            Image background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0.15f, 0.2f, 0.25f, 1f);
            GameObject checkObject = CreateUIObject(
                backgroundObject.transform,
                "Checkmark",
                new Vector2(18f, 18f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            Image checkmark = checkObject.AddComponent<Image>();
            checkmark.color = new Color(0.25f, 0.9f, 1f, 1f);
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;
            Text text = CreateText(
                toggleObject.transform,
                "Label",
                label,
                font,
                14,
                FontStyle.Normal,
                new Vector2(215f, 32f),
                new Vector2(34f, 0f));
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.alignment = TextAnchor.MiddleLeft;
            return toggle;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            string value,
            Font font,
            int fontSize,
            FontStyle fontStyle,
            Vector2 size,
            Vector2 position)
        {
            GameObject textObject = CreateUIObject(
                parent,
                objectName,
                size,
                new Vector2(0f, 1f),
                position);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.text = value;
            text.alignment = TextAnchor.UpperLeft;
            return text;
        }

        private static GameObject CreateUIObject(
            Transform parent,
            string objectName,
            Vector2 size,
            Vector2 anchor,
            Vector2 position)
        {
            GameObject gameObject = new GameObject(
                objectName,
                typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rectTransform =
                (RectTransform)gameObject.transform;
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
            return gameObject;
        }

        private static void VerifyDeveloperControls()
        {
            InputActionAsset inputActions =
                LoadRequiredAsset<InputActionAsset>(k_InputAssetPath);
            InputActionMap debugMap = inputActions.FindActionMap(
                "SandboxDebug",
                true);
            if (debugMap.actions.Count != 12 ||
                debugMap.FindAction("TogglePanel", true).bindings[0].path !=
                    "<Keyboard>/f1" ||
                debugMap.FindAction("Clear", true).bindings[0].path !=
                    "<Keyboard>/backspace")
            {
                throw new InvalidOperationException(
                    "SandboxDebug input actions do not match the documented controls.");
            }

            InputActionMap playerMap = inputActions.FindActionMap("Player", true);
            if (playerMap.FindAction("PreviousWeapon", true).bindings[0].path !=
                    "<Keyboard>/q" ||
                playerMap.FindAction("NextWeapon", true).bindings[0].path !=
                    "<Keyboard>/e")
            {
                throw new InvalidOperationException(
                    "Q and E must remain owned by Player weapon switching.");
            }

            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            Transform systems = RequireRoot(scene, "__Systems");
            Transform ui = RequireRoot(scene, "UI");
            SandboxDebugInputController inputController =
                RequireComponentInChildren<SandboxDebugInputController>(
                    systems,
                    "SandboxDiagnostics");
            SandboxDebugPanelController panel =
                RequireComponentInChildren<SandboxDebugPanelController>(
                    ui,
                    "CombatSandboxPanelCanvas");
            if (!inputController.ValidateConfiguration(out string failureMessage) ||
                !panel.ValidateConfiguration(out failureMessage) ||
                panel.gameObject.activeSelf)
            {
                throw new InvalidOperationException(
                    $"CombatSandbox developer controls failed verification: {failureMessage}");
            }
        }

        private static Transform RequireRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root.transform;
                }
            }

            throw new InvalidOperationException(
                $"CombatSandbox requires root '{rootName}'.");
        }

        private static T RequireComponentInChildren<T>(
            Transform parent,
            string childName) where T : Component
        {
            Transform child = parent.Find(childName);
            T component = child == null
                ? null
                : child.GetComponentInChildren<T>(true);
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}/{childName}' requires {typeof(T).Name}.");
            }

            return component;
        }

        private static SpawnPointGroup RequireSpawnPointGroup(
            Transform parent,
            string childName)
        {
            return RequireComponentInChildren<SpawnPointGroup>(
                parent,
                childName);
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

        private static GameObject CreateChild(
            Transform parent,
            string objectName)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void DestroyChildIfPresent(
            Transform parent,
            string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
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

        private readonly struct PanelAssets
        {
            public GameObject CanvasRoot { get; }
            public GameObject PanelRoot { get; }

            public PanelAssets(GameObject canvasRoot, GameObject panelRoot)
            {
                CanvasRoot = canvasRoot;
                PanelRoot = panelRoot;
            }
        }
    }
}
