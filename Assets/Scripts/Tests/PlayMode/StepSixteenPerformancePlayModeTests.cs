using System.Collections;
using System.Collections.Generic;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Diagnostics;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MonstersVsZombies.Tests.PlayMode
{
    public sealed class StepSixteenPerformancePlayModeTests
    {
        private const string k_CombatSandboxSceneName = "CombatSandbox";
        private const string k_SampleSceneName = "SampleScene";
        private const int k_WarmupFrames = 60;
        private const int k_SampleFrames = 120;
        private const float k_ProfileDeltaTime = 1f / 60f;

        private readonly List<UnitController> _units =
            new List<UnitController>(256);
        private readonly List<PoolDiagnostics> _poolDiagnostics =
            new List<PoolDiagnostics>();

        private CombatSandboxBootstrap _bootstrap;
        private SandboxStressPresetController _stressController;

        [UnitySetUp]
        public IEnumerator LoadCombatSandbox()
        {
            Time.captureDeltaTime = k_ProfileDeltaTime;
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                k_CombatSandboxSceneName,
                LoadSceneMode.Single);
            Assert.That(sceneLoad, Is.Not.Null);
            yield return sceneLoad;

            for (int frame = 0; frame < 120; frame++)
            {
                _bootstrap = Object.FindAnyObjectByType<
                    CombatSandboxBootstrap>();
                _stressController = Object.FindAnyObjectByType<
                    SandboxStressPresetController>();
                if (_bootstrap != null &&
                    _bootstrap.IsGameplayEnabled &&
                    _bootstrap.InitialPlayer != null &&
                    _stressController != null &&
                    _stressController.ValidateConfiguration(out _))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                "CombatSandbox did not initialize its Step 16 stress services within 120 frames.");
        }

        [UnityTearDown]
        public IEnumerator UnloadCombatSandbox()
        {
            _stressController?.StopPreset();
            Time.captureDeltaTime = 0f;
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                k_SampleSceneName,
                LoadSceneMode.Single);
            if (sceneLoad != null)
            {
                yield return sceneLoad;
            }

            _bootstrap = null;
            _stressController = null;
            _units.Clear();
            _poolDiagnostics.Clear();
        }

        [UnityTest]
        [Category("Performance")]
        public IEnumerator Profile_10AlliesVersus10Enemies()
        {
            yield return CapturePreset(10);
        }

        [UnityTest]
        [Category("Performance")]
        public IEnumerator Profile_50AlliesVersus50Enemies()
        {
            yield return CapturePreset(50);
        }

        [UnityTest]
        [Category("Performance")]
        public IEnumerator Profile_100AlliesVersus100Enemies()
        {
            yield return CapturePreset(100);
        }

        private IEnumerator CapturePreset(int perFactionCount)
        {
            SandboxStressPresetResult presetResult =
                _stressController.RunPreset(perFactionCount);
            Assert.That(presetResult.IsSuccess, Is.True,
                $"Preset {perFactionCount}v{perFactionCount} failed: " +
                $"{presetResult.PoolFailureReason}, " +
                $"Allies {presetResult.SpawnedAllies}, " +
                $"Enemies {presetResult.SpawnedEnemies}.");
            Assert.That(
                _bootstrap.UnitRegistry.GetFactionCount(UnitFaction.Ally),
                Is.EqualTo(perFactionCount));
            Assert.That(
                _bootstrap.UnitRegistry.GetFactionCount(UnitFaction.Enemy),
                Is.EqualTo(perFactionCount));

            for (int frame = 0; frame < k_WarmupFrames; frame++)
            {
                yield return null;
            }

            Dictionary<PoolId, int> createdAtStart =
                CaptureCreatedCounts();
            SandboxPerformanceDiagnostics.ResetAllocations();
            using ProfilerRecorder mainThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "Main Thread",
                k_SampleFrames);
            using ProfilerRecorder gcAllocated = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                k_SampleFrames);
            using ProfilerRecorder drawCalls = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Draw Calls Count",
                k_SampleFrames);
            using ProfilerRecorder setPassCalls = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "SetPass Calls Count",
                k_SampleFrames);

            float elapsedSeconds = 0f;
            for (int frame = 0; frame < k_SampleFrames; frame++)
            {
                elapsedSeconds += Time.deltaTime;
                yield return null;
            }

            mainThread.Stop();
            gcAllocated.Stop();
            drawCalls.Stop();
            setPassCalls.Stop();
            SandboxAllocationSnapshot allocations =
                SandboxPerformanceDiagnostics.GetAllocationSnapshot();
            QueryMetrics queryMetrics = CaptureQueryMetrics();
            int createdGrowth = GetCreatedGrowth(createdAtStart);
            Assert.That(queryMetrics.ScanCount, Is.GreaterThan(0));
            Assert.That(queryMetrics.SaturatedScanCount, Is.Zero,
                "The profiled target-query capacity still saturated.");
            Assert.That(createdGrowth, Is.Zero,
                "A prewarmed stress capture created additional pooled objects.");
            Assert.That(allocations.GameplayAllocatedBytes, Is.Zero,
                FormatAllocationFailure(allocations));

            Debug.Log(
                $"[Step16Profile] Preset={perFactionCount}v{perFactionCount}; " +
                $"Frames={k_SampleFrames}; Seconds={elapsedSeconds:0.###}; " +
                $"MainThreadAvgMs={GetAverageMilliseconds(mainThread):0.###}; " +
                $"MainThreadMaxMs={GetMaximumMilliseconds(mainThread):0.###}; " +
                $"GCFrameMaxBytes={GetMaximumValue(gcAllocated)}; " +
                $"DrawCallsAvg={GetAverageValue(drawCalls):0.###}; " +
                $"SetPassAvg={GetAverageValue(setPassCalls):0.###}; " +
                $"TargetScans={queryMetrics.ScanCount}; " +
                $"SaturatedScans={queryMetrics.SaturatedScanCount}; " +
                $"DestinationCommands={queryMetrics.DestinationCommandCount}; " +
                $"PoolCreatedGrowth={createdGrowth}; " +
                $"GameplayAllocatedBytes={allocations.GameplayAllocatedBytes}.");
        }

        private Dictionary<PoolId, int> CaptureCreatedCounts()
        {
            _bootstrap.PoolManager.CopyDiagnostics(_poolDiagnostics);
            Dictionary<PoolId, int> counts =
                new Dictionary<PoolId, int>(_poolDiagnostics.Count);
            foreach (PoolDiagnostics diagnostics in _poolDiagnostics)
            {
                counts.Add(diagnostics.PoolId, diagnostics.CreatedCount);
            }

            return counts;
        }

        private int GetCreatedGrowth(Dictionary<PoolId, int> createdAtStart)
        {
            _bootstrap.PoolManager.CopyDiagnostics(_poolDiagnostics);
            int growth = 0;
            foreach (PoolDiagnostics diagnostics in _poolDiagnostics)
            {
                if (createdAtStart.TryGetValue(
                        diagnostics.PoolId,
                        out int initialCount))
                {
                    growth += Mathf.Max(
                        0,
                        diagnostics.CreatedCount - initialCount);
                }
            }

            return growth;
        }

        private QueryMetrics CaptureQueryMetrics()
        {
            _units.Clear();
            _bootstrap.UnitRegistry.CopySnapshot(_units);
            int scanCount = 0;
            int saturatedScanCount = 0;
            int destinationCommandCount = 0;
            foreach (UnitController unit in _units)
            {
                if (unit == null)
                {
                    continue;
                }

                TargetingController targeting = unit.TargetingController;
                if (targeting != null)
                {
                    scanCount += targeting.ScanCount;
                    saturatedScanCount += targeting.SaturatedScanCount;
                }

                NavMeshUnitMotor motor =
                    unit.GetComponent<NavMeshUnitMotor>();
                if (motor != null)
                {
                    destinationCommandCount += motor.DestinationCommandCount;
                }
            }

            return new QueryMetrics(
                scanCount,
                saturatedScanCount,
                destinationCommandCount);
        }

        private static double GetAverageMilliseconds(
            ProfilerRecorder recorder)
        {
            return GetAverageValue(recorder) / 1000000d;
        }

        private static double GetMaximumMilliseconds(
            ProfilerRecorder recorder)
        {
            return GetMaximumValue(recorder) / 1000000d;
        }

        private static double GetAverageValue(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0)
            {
                return 0d;
            }

            long total = 0;
            foreach (ProfilerRecorderSample sample in recorder.ToArray())
            {
                total += sample.Value;
            }

            return (double)total / recorder.Count;
        }

        private static long GetMaximumValue(ProfilerRecorder recorder)
        {
            long maximum = 0;
            if (!recorder.Valid)
            {
                return maximum;
            }

            foreach (ProfilerRecorderSample sample in recorder.ToArray())
            {
                if (sample.Value > maximum)
                {
                    maximum = sample.Value;
                }
            }

            return maximum;
        }

        private static string FormatAllocationFailure(
            SandboxAllocationSnapshot allocations)
        {
            return "Recurring gameplay allocation detected. " +
                $"Targeting={allocations.Targeting.AllocatedBytes}, " +
                $"AI={allocations.AI.AllocatedBytes}, " +
                $"Attack={allocations.Attack.AllocatedBytes}, " +
                $"Projectile={allocations.Projectile.AllocatedBytes}, " +
                $"PoolRent={allocations.PoolRent.AllocatedBytes}, " +
                $"PoolReturn={allocations.PoolReturn.AllocatedBytes}.";
        }

        private readonly struct QueryMetrics
        {
            public int ScanCount { get; }
            public int SaturatedScanCount { get; }
            public int DestinationCommandCount { get; }

            public QueryMetrics(
                int scanCount,
                int saturatedScanCount,
                int destinationCommandCount)
            {
                ScanCount = scanCount;
                SaturatedScanCount = saturatedScanCount;
                DestinationCommandCount = destinationCommandCount;
            }
        }
    }
}
