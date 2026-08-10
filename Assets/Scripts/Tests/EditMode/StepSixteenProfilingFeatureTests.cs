using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Diagnostics;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSixteenProfilingFeatureTests
    {
        private const string k_ScenePath =
            "Assets/Scenes/CombatSandbox.unity";
        private const string k_BasePrefabPath =
            "Assets/Prefabs/Units/PF_Unit_Base.prefab";

        private static readonly string[] s_expectedAllies =
        {
            "AllyClassicMelee",
            "AllyClassicRange",
            "AllyDragon",
            "AllyDoubleHead"
        };

        private static readonly string[] s_expectedEnemies =
        {
            "EnemyClassicMelee",
            "EnemyClassicRange",
            "EnemyDragon",
            "EnemyStunner"
        };

        [Test]
        public void CombatSandbox_HasExactStressCompositionAndThreePresets()
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
                SandboxStressPresetController stress = FindInScene<
                    SandboxStressPresetController>(scene);
                SandboxDebugPanelController panel = FindInScene<
                    SandboxDebugPanelController>(scene);
                Assert.That(stress, Is.Not.Null);
                Assert.That(panel, Is.Not.Null);
                Assert.That(
                    stress.ValidateConfiguration(out string stressFailure),
                    Is.True,
                    stressFailure);
                Assert.That(panel.HasStressPresetControls, Is.True);
                Assert.That(panel.StressPresetController, Is.SameAs(stress));
                Assert.That(panel.StressTenButton, Is.Not.Null);
                Assert.That(panel.StressFiftyButton, Is.Not.Null);
                Assert.That(panel.StressHundredButton, Is.Not.Null);
                Assert.That(panel.StressStatusText, Is.Not.Null);
                AssertDefinitions(stress.AllyDefinitions, s_expectedAllies);
                AssertDefinitions(stress.EnemyDefinitions, s_expectedEnemies);
                Assert.That(stress.ProjectileDefinitions.Length, Is.EqualTo(2));
                Assert.That(
                    stress.ProjectileDefinitions[0].PoolId,
                    Is.EqualTo(new MonstersVsZombies.Core.PoolId("Bullet")));
                Assert.That(
                    stress.ProjectileDefinitions[1].PoolId,
                    Is.EqualTo(new MonstersVsZombies.Core.PoolId("Fireball")));
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
        public void BaseUnit_UsesProfiledQueryCapacity()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BasePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            TargetingController targeting =
                prefab.GetComponent<TargetingController>();
            Assert.That(targeting, Is.Not.Null);
            Assert.That(targeting.QueryCapacity, Is.EqualTo(256));
        }

        [Test]
        public void StressResult_DefaultCannotReportSuccess()
        {
            Assert.That(default(SandboxStressPresetResult).IsSuccess, Is.False);
            SandboxStressPresetResult success =
                new SandboxStressPresetResult(
                    50,
                    50,
                    50,
                    PoolFailureReason.None);
            Assert.That(success.IsSuccess, Is.True);
            Assert.That(
                new SandboxStressPresetResult(
                    50,
                    49,
                    50,
                    PoolFailureReason.None).IsSuccess,
                Is.False);
        }

        [Test]
        public void AllocationDiagnostics_ResetEverySubsystem()
        {
            SandboxPerformanceDiagnostics.ResetAllocations();
            SandboxAllocationSnapshot snapshot =
                SandboxPerformanceDiagnostics.GetAllocationSnapshot();
            Assert.That(snapshot.GameplayAllocatedBytes, Is.Zero);
            Assert.That(snapshot.Targeting.SampleCount, Is.Zero);
            Assert.That(snapshot.AI.SampleCount, Is.Zero);
            Assert.That(snapshot.Attack.SampleCount, Is.Zero);
            Assert.That(snapshot.Projectile.SampleCount, Is.Zero);
            Assert.That(snapshot.PoolRent.SampleCount, Is.Zero);
            Assert.That(snapshot.PoolReturn.SampleCount, Is.Zero);
        }

        private static void AssertDefinitions(
            AIUnitDefinition[] definitions,
            string[] expectedIds)
        {
            Assert.That(definitions.Length, Is.EqualTo(expectedIds.Length));
            for (int definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                Assert.That(definitions[definitionIndex], Is.Not.Null);
                Assert.That(
                    definitions[definitionIndex].UnitId.Value,
                    Is.EqualTo(expectedIds[definitionIndex]));
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
