using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MonstersVsZombies.Editor.ProjectSetup
{
    public static class StepZeroProjectSetup
    {
        private const string k_ExpectedUnityVersion = "6000.5.5f1";
        private const string k_ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string k_TagManagerPath = "ProjectSettings/TagManager.asset";
        private const int k_FirstUserLayerIndex = 8;
        private const int k_NewInputSystemOnly = 1;
        private const int k_BothInputSystems = 2;

        private static readonly string[] s_RequiredLayers =
        {
            "World",
            "UnitBody",
            "UnitTarget",
            "Projectile"
        };

        private static readonly IReadOnlyDictionary<string, string> s_RequiredPackages =
            new Dictionary<string, string>
            {
                { "com.unity.inputsystem", "1.19.0" },
                { "com.unity.ai.navigation", "2.0.14" },
                { "com.unity.test-framework", "1.7.0" },
                { "com.unity.render-pipelines.universal", string.Empty }
            };

        [MenuItem("Tools/Monsters vs Zombies/Step 0/Apply and Verify")]
        public static void ApplyAndVerify()
        {
            EnsureRequiredLayers();
            VerifyUnityVersion();
            VerifyRequiredPackages();
            VerifyNewInputSystem();
            VerifyRuntimeAssemblyReference();

            Debug.Log("[Step 0] Project layers and baseline configuration verified successfully.");
        }

        private static void EnsureRequiredLayers()
        {
            UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(k_TagManagerPath);
            if (tagManagerAssets.Length == 0)
            {
                throw new InvalidOperationException($"Unable to load {k_TagManagerPath}.");
            }

            SerializedObject serializedTagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = serializedTagManager.FindProperty("layers");
            if (layers == null || !layers.isArray)
            {
                throw new InvalidOperationException("Unable to read the project layer collection.");
            }

            bool didChangeLayers = false;
            foreach (string requiredLayer in s_RequiredLayers)
            {
                if (FindLayerIndex(layers, requiredLayer) >= 0)
                {
                    continue;
                }

                int availableLayerIndex = FindAvailableLayerIndex(layers);
                if (availableLayerIndex < 0)
                {
                    throw new InvalidOperationException($"No user layer slot is available for '{requiredLayer}'.");
                }

                layers.GetArrayElementAtIndex(availableLayerIndex).stringValue = requiredLayer;
                didChangeLayers = true;
                Debug.Log($"[Step 0] Added layer '{requiredLayer}' at index {availableLayerIndex}.");
            }

            if (didChangeLayers && serializedTagManager.ApplyModifiedProperties())
            {
                AssetDatabase.SaveAssetIfDirty(tagManagerAssets[0]);
            }
        }

        private static int FindLayerIndex(SerializedProperty layers, string layerName)
        {
            for (int layerIndex = 0; layerIndex < layers.arraySize; layerIndex++)
            {
                if (layers.GetArrayElementAtIndex(layerIndex).stringValue == layerName)
                {
                    return layerIndex;
                }
            }

            return -1;
        }

        private static int FindAvailableLayerIndex(SerializedProperty layers)
        {
            for (int layerIndex = k_FirstUserLayerIndex; layerIndex < layers.arraySize; layerIndex++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(layerIndex).stringValue))
                {
                    return layerIndex;
                }
            }

            return -1;
        }

        private static void VerifyUnityVersion()
        {
            if (Application.unityVersion != k_ExpectedUnityVersion)
            {
                throw new InvalidOperationException(
                    $"Expected Unity {k_ExpectedUnityVersion}, but the project is running in {Application.unityVersion}.");
            }
        }

        private static void VerifyRequiredPackages()
        {
            PackageInfo[] registeredPackages = PackageInfo.GetAllRegisteredPackages();
            foreach (KeyValuePair<string, string> requiredPackage in s_RequiredPackages)
            {
                PackageInfo resolvedPackage = FindPackage(registeredPackages, requiredPackage.Key);
                if (resolvedPackage == null)
                {
                    throw new InvalidOperationException($"Required package '{requiredPackage.Key}' is not resolved.");
                }

                if (!string.IsNullOrEmpty(requiredPackage.Value) && resolvedPackage.version != requiredPackage.Value)
                {
                    throw new InvalidOperationException(
                        $"Package '{requiredPackage.Key}' must resolve to {requiredPackage.Value}, " +
                        $"but {resolvedPackage.version} is registered.");
                }
            }
        }

        private static PackageInfo FindPackage(PackageInfo[] packages, string packageName)
        {
            foreach (PackageInfo package in packages)
            {
                if (package.name == packageName)
                {
                    return package;
                }
            }

            return null;
        }

        private static void VerifyNewInputSystem()
        {
            UnityEngine.Object[] projectSettingsAssets = AssetDatabase.LoadAllAssetsAtPath(k_ProjectSettingsPath);
            if (projectSettingsAssets.Length == 0)
            {
                throw new InvalidOperationException($"Unable to load {k_ProjectSettingsPath}.");
            }

            SerializedObject serializedProjectSettings = new SerializedObject(projectSettingsAssets[0]);
            SerializedProperty activeInputHandler = serializedProjectSettings.FindProperty("activeInputHandler");
            if (activeInputHandler == null)
            {
                throw new InvalidOperationException("Unable to read the Active Input Handling project setting.");
            }

            bool usesNewInputSystem = activeInputHandler.intValue == k_NewInputSystemOnly ||
                                      activeInputHandler.intValue == k_BothInputSystems;
            if (!usesNewInputSystem)
            {
                throw new InvalidOperationException("Active Input Handling does not enable the new Input System.");
            }
        }

        private static void VerifyRuntimeAssemblyReference()
        {
            string runtimeAssemblyName = typeof(RuntimeAssemblyMarker).Assembly.GetName().Name;
            if (runtimeAssemblyName != "MonstersVsZombies.Runtime")
            {
                throw new InvalidOperationException(
                    $"The Editor assembly resolved RuntimeAssemblyMarker from '{runtimeAssemblyName}'.");
            }
        }
    }
}
