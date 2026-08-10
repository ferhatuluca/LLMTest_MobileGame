using System;
using System.IO;
using System.Reflection;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Diagnostics;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepSixteenProfilingSetup
    {
        private const string k_ScenePath = "Assets/Scenes/CombatSandbox.unity";
        private const string k_BaseUnitPrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Base.prefab";
        private const int k_ProfiledTargetQueryCapacity = 256;

        private static readonly string[] s_allyDefinitionPaths =
        {
            "Assets/Data/Units/UD_Ally_ClassicMelee.asset",
            "Assets/Data/Units/UD_Ally_ClassicRange.asset",
            "Assets/Data/Units/UD_Ally_Dragon.asset",
            "Assets/Data/Units/UD_Ally_DoubleHead.asset"
        };

        private static readonly string[] s_enemyDefinitionPaths =
        {
            "Assets/Data/Units/UD_Enemy_ClassicMelee.asset",
            "Assets/Data/Units/UD_Enemy_ClassicRange.asset",
            "Assets/Data/Units/UD_Enemy_Dragon.asset",
            "Assets/Data/Units/UD_Enemy_Stunner.asset"
        };

        private static readonly string[] s_projectileDefinitionPaths =
        {
            "Assets/Data/Projectiles/PD_Bullet.asset",
            "Assets/Data/Projectiles/PD_Fireball.asset"
        };

        [MenuItem(
            "Tools/Monsters vs Zombies/Step 16/Create and Verify Profiling Presets")]
        public static void CreateAndVerifyProfilingPresets()
        {
            UpdateTargetQueryCapacity();
            Scene scene = EditorSceneManager.OpenScene(
                k_ScenePath,
                OpenSceneMode.Single);
            Transform systems = RequireRoot(scene, "__Systems");
            Transform ui = RequireRoot(scene, "UI");
            DebugUnitSpawner debugSpawner =
                systems.GetComponentInChildren<DebugUnitSpawner>(true);
            PoolManager poolManager =
                systems.GetComponentInChildren<PoolManager>(true);
            UnitRegistry unitRegistry =
                systems.GetComponentInChildren<UnitRegistry>(true);
            SandboxDebugPanelController panel =
                ui.GetComponentInChildren<SandboxDebugPanelController>(true);
            if (debugSpawner == null || poolManager == null ||
                unitRegistry == null || panel == null)
            {
                throw new InvalidOperationException(
                    "CombatSandbox requires the Step 14 diagnostics composition before Step 16 profiling setup.");
            }

            Transform diagnostics = systems.Find("SandboxDiagnostics");
            if (diagnostics == null)
            {
                throw new InvalidOperationException(
                    "CombatSandbox requires SandboxDiagnostics.");
            }

            SandboxStressPresetController previousController =
                diagnostics.GetComponent<SandboxStressPresetController>();
            if (previousController != null)
            {
                UnityEngine.Object.DestroyImmediate(previousController);
            }

            SandboxStressPresetController stressController =
                diagnostics.gameObject.AddComponent<
                    SandboxStressPresetController>();
            if (!stressController.Configure(
                    debugSpawner,
                    poolManager,
                    unitRegistry,
                    LoadAssets<AIUnitDefinition>(s_allyDefinitionPaths),
                    LoadAssets<AIUnitDefinition>(s_enemyDefinitionPaths),
                    LoadAssets<ProjectileDefinition>(
                        s_projectileDefinitionPaths)))
            {
                throw new InvalidOperationException(
                    "Could not configure the Step 16 mixed stress preset controller.");
            }

            CreatePresetControls(panel, stressController);
            EditorUtility.SetDirty(stressController);
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, k_ScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save the Step 16 profiling presets.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyProfilingPresets();
            Debug.Log(
                "[StepSixteenProfilingSetup] Created and verified 10v10, 50v50, and 100v100 mixed profiling presets with explicit Bullet and Fireball prewarming.");
        }

        [MenuItem(
            "Tools/Monsters vs Zombies/Step 16/Verify Profiling Presets")]
        public static void VerifyProfilingPresets()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BaseUnitPrefabPath);
            TargetingController baseTargeting = basePrefab == null
                ? null
                : basePrefab.GetComponent<TargetingController>();
            if (baseTargeting == null ||
                baseTargeting.QueryCapacity != k_ProfiledTargetQueryCapacity)
            {
                throw new InvalidOperationException(
                    "PF_Unit_Base does not use the profiled 256-slot target-query capacity.");
            }

            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            bool openedForVerification = !scene.IsValid() || !scene.isLoaded;
            if (openedForVerification)
            {
                scene = EditorSceneManager.OpenScene(
                    k_ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                Transform systems = RequireRoot(scene, "__Systems");
                Transform ui = RequireRoot(scene, "UI");
                SandboxStressPresetController stressController =
                    systems.GetComponentInChildren<
                        SandboxStressPresetController>(true);
                SandboxDebugPanelController panel =
                    ui.GetComponentInChildren<
                        SandboxDebugPanelController>(true);
                string stressFailure = string.Empty;
                string panelFailure = string.Empty;
                if (stressController == null || panel == null ||
                    !stressController.ValidateConfiguration(
                        out stressFailure) ||
                    !panel.ValidateConfiguration(out panelFailure) ||
                    !panel.HasStressPresetControls ||
                    panel.StressPresetController != stressController)
                {
                    throw new InvalidOperationException(
                        $"Step 16 profiling preset verification failed: {stressFailure} {panelFailure}");
                }
            }
            finally
            {
                if (openedForVerification)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem(
            "Tools/Monsters vs Zombies/Step 16/Build Windows Development Player")]
        public static void BuildWindowsDevelopmentPlayer()
        {
            string outputPath = Path.GetFullPath(
                "Builds/Step16/Windows/MonstersVsZombies.exe");
            BuildDevelopmentPlayer(
                BuildTarget.StandaloneWindows64,
                outputPath);
        }

        [MenuItem(
            "Tools/Monsters vs Zombies/Step 16/Build Android Development Player")]
        public static void BuildAndroidDevelopmentPlayer()
        {
            string outputPath = Path.GetFullPath(
                "Builds/Step16/Android/MonstersVsZombies-Step16.apk");
            BuildDevelopmentPlayer(BuildTarget.Android, outputPath);
        }

        private static void CreatePresetControls(
            SandboxDebugPanelController panel,
            SandboxStressPresetController stressController)
        {
            Transform parent = panel.transform;
            DestroyChildIfPresent(parent, "Stress10Button");
            DestroyChildIfPresent(parent, "Stress50Button");
            DestroyChildIfPresent(parent, "Stress100Button");
            DestroyChildIfPresent(parent, "StressStatus");
            Font font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            Button stressTen = CreateButton(
                parent,
                "Stress10Button",
                "Preset 10 v 10",
                font,
                new Vector2(180f, 42f),
                new Vector2(24f, -908f));
            Button stressFifty = CreateButton(
                parent,
                "Stress50Button",
                "Preset 50 v 50",
                font,
                new Vector2(180f, 42f),
                new Vector2(214f, -908f));
            Button stressHundred = CreateButton(
                parent,
                "Stress100Button",
                "Preset 100 v 100",
                font,
                new Vector2(190f, 42f),
                new Vector2(404f, -908f));
            Text stressStatus = CreateText(
                parent,
                "StressStatus",
                "Stress preset: inactive",
                font,
                new Vector2(700f, 42f),
                new Vector2(620f, -908f));

            SetAutoProperty(
                panel,
                nameof(panel.StressPresetController),
                stressController);
            SetAutoProperty(panel, nameof(panel.StressTenButton), stressTen);
            SetAutoProperty(
                panel,
                nameof(panel.StressFiftyButton),
                stressFifty);
            SetAutoProperty(
                panel,
                nameof(panel.StressHundredButton),
                stressHundred);
            SetAutoProperty(
                panel,
                nameof(panel.StressStatusText),
                stressStatus);
        }

        private static void BuildDevelopmentPlayer(
            BuildTarget buildTarget,
            string outputPath)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { k_ScenePath },
                locationPathName = outputPath,
                target = buildTarget,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{buildTarget} Development build failed: " +
                    $"{report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log(
                $"[StepSixteenDevelopmentBuild] Target={buildTarget}; " +
                $"Output={outputPath}; Bytes={report.summary.totalSize}; " +
                $"Warnings={report.summary.totalWarnings}.");
        }

        private static void UpdateTargetQueryCapacity()
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(
                k_BaseUnitPrefabPath);
            try
            {
                TargetingController targeting =
                    prefabContents.GetComponent<TargetingController>();
                if (targeting == null)
                {
                    throw new InvalidOperationException(
                        "PF_Unit_Base requires TargetingController.");
                }

                SetAutoProperty(
                    targeting,
                    nameof(targeting.QueryCapacity),
                    k_ProfiledTargetQueryCapacity);
                PrefabUtility.SaveAsPrefabAsset(
                    prefabContents,
                    k_BaseUnitPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
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
                size,
                Vector2.zero);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            string value,
            Font font,
            Vector2 size,
            Vector2 position)
        {
            GameObject textObject = CreateUIObject(
                parent,
                objectName,
                size,
                position);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 14;
            text.color = Color.white;
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static GameObject CreateUIObject(
            Transform parent,
            string objectName,
            Vector2 size,
            Vector2 position)
        {
            GameObject gameObject = new GameObject(
                objectName,
                typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rectTransform =
                (RectTransform)gameObject.transform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
            return gameObject;
        }

        private static T[] LoadAssets<T>(string[] paths)
            where T : UnityEngine.Object
        {
            T[] assets = new T[paths.Length];
            for (int assetIndex = 0;
                 assetIndex < paths.Length;
                 assetIndex++)
            {
                assets[assetIndex] =
                    AssetDatabase.LoadAssetAtPath<T>(paths[assetIndex]);
                if (assets[assetIndex] == null)
                {
                    throw new InvalidOperationException(
                        $"Missing profiling asset '{paths[assetIndex]}'.");
                }
            }

            return assets;
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

        private static void DestroyChildIfPresent(
            Transform parent,
            string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            }

            field.SetValue(target, value);
            EditorUtility.SetDirty((UnityEngine.Object)target);
        }
    }
}
