using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using Unity.Profiling;
using UnityEngine;

namespace MonstersVsZombies.Diagnostics
{
    public sealed class SandboxStandaloneProfileRunner : MonoBehaviour
    {
        private const string k_CountArgument = "-mvz-profile-count=";
        private const string k_DurationArgument = "-mvz-profile-duration=";
        private const string k_OutputArgument = "-mvz-profile-output=";
        private const string k_RenderArgument = "-mvz-profile-render=";
        private const double k_StartupTimeoutSeconds = 30d;
        private const double k_WarmupSeconds = 5d;

        private readonly List<PoolDiagnostics> _poolDiagnostics =
            new List<PoolDiagnostics>();
        private readonly List<UnitController> _units =
            new List<UnitController>(256);
        private readonly Dictionary<PoolId, int> _createdAtStart =
            new Dictionary<PoolId, int>();

        private int _perFactionCount;
        private double _sampleDurationSeconds;
        private string _outputPath;
        private bool _forceOffscreenRender;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            if (!TryGetArgument(k_CountArgument, out _))
            {
                return;
            }

            GameObject runnerObject = new GameObject(
                "__Step16StandaloneProfileRunner");
            DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<SandboxStandaloneProfileRunner>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            if (!TryParseArguments(out string failureMessage))
            {
                FinishFailure(failureMessage);
                return;
            }

            StartCoroutine(RunProfile());
        }

        private IEnumerator RunProfile()
        {
            double startupDeadline = Time.realtimeSinceStartupAsDouble +
                k_StartupTimeoutSeconds;
            CombatSandboxBootstrap bootstrap = null;
            SandboxStressPresetController stressController = null;
            while (Time.realtimeSinceStartupAsDouble < startupDeadline)
            {
                bootstrap = FindAnyObjectByType<CombatSandboxBootstrap>();
                stressController = FindAnyObjectByType<
                    SandboxStressPresetController>();
                if (bootstrap != null && bootstrap.IsGameplayEnabled &&
                    bootstrap.InitialPlayer != null &&
                    stressController != null &&
                    stressController.ValidateConfiguration(out _))
                {
                    break;
                }

                yield return null;
            }

            if (bootstrap == null || !bootstrap.IsGameplayEnabled ||
                stressController == null)
            {
                FinishFailure(
                    "CombatSandbox did not initialize before the standalone profile timeout.");
                yield break;
            }

            SandboxStressPresetResult presetResult =
                stressController.RunPreset(_perFactionCount);
            if (!presetResult.IsSuccess)
            {
                FinishFailure(
                    $"Stress preset failed: {presetResult.PoolFailureReason}, " +
                    $"Allies {presetResult.SpawnedAllies}, " +
                    $"Enemies {presetResult.SpawnedEnemies}.");
                yield break;
            }

            double warmupDeadline = Time.realtimeSinceStartupAsDouble +
                k_WarmupSeconds;
            while (Time.realtimeSinceStartupAsDouble < warmupDeadline)
            {
                yield return null;
            }

            CaptureCreatedCounts(bootstrap.PoolManager);
            SandboxPerformanceDiagnostics.ResetAllocations();
            using ProfilerRecorder mainThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "Main Thread");
            using ProfilerRecorder gcAllocated = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame");
            using ProfilerRecorder drawCalls = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Draw Calls Count");
            using ProfilerRecorder setPassCalls = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "SetPass Calls Count");
            using ProfilerRecorder batches = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Batches Count");
            using ProfilerRecorder triangles = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Triangles Count");
            Camera profileCamera = null;
            RenderTexture profileTarget = null;
            bool cameraWasEnabled = false;
            if (_forceOffscreenRender)
            {
                profileCamera = Camera.main;
                if (profileCamera == null)
                {
                    FinishFailure(
                        "The offscreen graphics capture requires a Main Camera.");
                    yield break;
                }

                cameraWasEnabled = profileCamera.enabled;
                profileCamera.enabled = false;
                profileTarget = RenderTexture.GetTemporary(
                    1280,
                    720,
                    24,
                    RenderTextureFormat.Default);
                profileCamera.targetTexture = profileTarget;
            }

            long frameCount = 0;
            long mainThreadTotalNanoseconds = 0;
            long mainThreadMaximumNanoseconds = 0;
            long globalAllocatedBytes = 0;
            long globalAllocatingFrames = 0;
            long globalMaximumFrameBytes = 0;
            long drawCallTotal = 0;
            long drawCallMaximum = 0;
            long setPassTotal = 0;
            long batchTotal = 0;
            long triangleMaximum = 0;
            double sampleStart = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - sampleStart <
                   _sampleDurationSeconds)
            {
                if (_forceOffscreenRender)
                {
                    profileCamera.Render();
                }

                yield return null;
                frameCount++;
                AccumulateMaximum(
                    mainThread.LastValue,
                    ref mainThreadTotalNanoseconds,
                    ref mainThreadMaximumNanoseconds);
                long allocatedBytes = gcAllocated.LastValue;
                globalAllocatedBytes += allocatedBytes;
                if (allocatedBytes > 0)
                {
                    globalAllocatingFrames++;
                    globalMaximumFrameBytes = Math.Max(
                        globalMaximumFrameBytes,
                        allocatedBytes);
                }

                AccumulateMaximum(
                    drawCalls.LastValue,
                    ref drawCallTotal,
                    ref drawCallMaximum);
                setPassTotal += setPassCalls.LastValue;
                batchTotal += batches.LastValue;
                triangleMaximum = Math.Max(
                    triangleMaximum,
                    triangles.LastValue);
            }

            mainThread.Stop();
            gcAllocated.Stop();
            drawCalls.Stop();
            setPassCalls.Stop();
            batches.Stop();
            triangles.Stop();
            if (profileCamera != null)
            {
                profileCamera.targetTexture = null;
                profileCamera.enabled = cameraWasEnabled;
            }

            if (profileTarget != null)
            {
                RenderTexture.ReleaseTemporary(profileTarget);
            }

            SandboxAllocationSnapshot allocationSnapshot =
                SandboxPerformanceDiagnostics.GetAllocationSnapshot();
            QueryMetrics queryMetrics = CaptureQueryMetrics(
                bootstrap.UnitRegistry);
            int poolCreatedGrowth = GetCreatedGrowth(
                bootstrap.PoolManager);
            RendererMetrics rendererMetrics = CaptureRendererMetrics();
            bool passed = allocationSnapshot.GameplayAllocatedBytes == 0 &&
                queryMetrics.SaturatedScanCount == 0 &&
                poolCreatedGrowth == 0 && rendererMetrics.InvalidBoundsCount == 0;
            List<string> lines = new List<string>
            {
                $"TimestampUtc={DateTime.UtcNow:O}",
                $"Result={(passed ? "Passed" : "Failed")}",
                $"Platform={Application.platform}",
                $"UnityVersion={Application.unityVersion}",
                $"DevelopmentBuild={Debug.isDebugBuild}",
                $"Preset={_perFactionCount}v{_perFactionCount}",
                $"WarmupSeconds={k_WarmupSeconds.ToString(CultureInfo.InvariantCulture)}",
                $"SampleSeconds={_sampleDurationSeconds.ToString(CultureInfo.InvariantCulture)}",
                $"ForcedOffscreenRender={_forceOffscreenRender}",
                $"Frames={frameCount}",
                $"MainThreadAverageMs={GetAverageMilliseconds(mainThreadTotalNanoseconds, frameCount)}",
                $"MainThreadMaximumMs={ToMilliseconds(mainThreadMaximumNanoseconds)}",
                $"GlobalAllocatedBytes={globalAllocatedBytes}",
                $"GlobalAllocatingFrames={globalAllocatingFrames}",
                $"GlobalMaximumFrameBytes={globalMaximumFrameBytes}",
                $"GameplayAllocatedBytes={allocationSnapshot.GameplayAllocatedBytes}",
                $"TargetingAllocatedBytes={allocationSnapshot.Targeting.AllocatedBytes}",
                $"AIAllocatedBytes={allocationSnapshot.AI.AllocatedBytes}",
                $"AttackAllocatedBytes={allocationSnapshot.Attack.AllocatedBytes}",
                $"ProjectileAllocatedBytes={allocationSnapshot.Projectile.AllocatedBytes}",
                $"PoolRentAllocatedBytes={allocationSnapshot.PoolRent.AllocatedBytes}",
                $"PoolReturnAllocatedBytes={allocationSnapshot.PoolReturn.AllocatedBytes}",
                $"TargetScans={queryMetrics.ScanCount}",
                $"SaturatedScans={queryMetrics.SaturatedScanCount}",
                $"DestinationCommands={queryMetrics.DestinationCommandCount}",
                $"PoolCreatedGrowth={poolCreatedGrowth}",
                $"DrawCallsAverage={GetAverage(drawCallTotal, frameCount)}",
                $"DrawCallsMaximum={drawCallMaximum}",
                $"SetPassAverage={GetAverage(setPassTotal, frameCount)}",
                $"BatchesAverage={GetAverage(batchTotal, frameCount)}",
                $"TrianglesMaximum={triangleMaximum}",
                $"ActiveRenderers={rendererMetrics.ActiveRendererCount}",
                $"MaterialSlots={rendererMetrics.MaterialSlotCount}",
                $"InvalidRendererBounds={rendererMetrics.InvalidBoundsCount}",
                $"MaximumRendererBoundsMagnitude={rendererMetrics.MaximumBoundsMagnitude.ToString("0.###", CultureInfo.InvariantCulture)}"
            };
            AppendPoolDiagnostics(lines, bootstrap.PoolManager);
            WriteReport(lines.ToArray());
            Application.Quit(passed ? 0 : 2);
        }

        private bool TryParseArguments(out string failureMessage)
        {
            if (!TryGetArgument(k_CountArgument, out string countText) ||
                !int.TryParse(
                    countText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _perFactionCount) ||
                (_perFactionCount != 10 && _perFactionCount != 50 &&
                 _perFactionCount != 100) ||
                !TryGetArgument(
                    k_DurationArgument,
                    out string durationText) ||
                !double.TryParse(
                    durationText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _sampleDurationSeconds) ||
                _sampleDurationSeconds <= 0d ||
                !TryGetArgument(k_OutputArgument, out _outputPath) ||
                string.IsNullOrWhiteSpace(_outputPath))
            {
                failureMessage =
                    "Standalone profiling requires count 10/50/100, a positive duration, and an output path.";
                return false;
            }

            _outputPath = Path.GetFullPath(_outputPath);
            _forceOffscreenRender =
                TryGetArgument(k_RenderArgument, out string renderText) &&
                bool.TryParse(renderText, out bool shouldRender) &&
                shouldRender;
            failureMessage = string.Empty;
            return true;
        }

        private void CaptureCreatedCounts(PoolManager poolManager)
        {
            _createdAtStart.Clear();
            poolManager.CopyDiagnostics(_poolDiagnostics);
            foreach (PoolDiagnostics diagnostics in _poolDiagnostics)
            {
                _createdAtStart.Add(
                    diagnostics.PoolId,
                    diagnostics.CreatedCount);
            }
        }

        private int GetCreatedGrowth(PoolManager poolManager)
        {
            poolManager.CopyDiagnostics(_poolDiagnostics);
            int growth = 0;
            foreach (PoolDiagnostics diagnostics in _poolDiagnostics)
            {
                if (_createdAtStart.TryGetValue(
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

        private void AppendPoolDiagnostics(
            List<string> lines,
            PoolManager poolManager)
        {
            poolManager.CopyDiagnostics(_poolDiagnostics);
            foreach (PoolDiagnostics diagnostics in _poolDiagnostics)
            {
                lines.Add(
                    $"Pool={diagnostics.PoolId};Created={diagnostics.CreatedCount};" +
                    $"Active={diagnostics.ActiveCount};Inactive={diagnostics.InactiveCount};" +
                    $"Peak={diagnostics.PeakActiveCount};Failed={diagnostics.FailedRentCount};" +
                    $"Capacity={diagnostics.CapacityReachedCount};" +
                    $"Overflow={diagnostics.OverflowDestroyCount}");
            }
        }

        private static RendererMetrics CaptureRendererMetrics()
        {
            Renderer[] renderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude);
            int activeRendererCount = 0;
            int materialSlotCount = 0;
            int invalidBoundsCount = 0;
            float maximumBoundsMagnitude = 0f;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                activeRendererCount++;
                materialSlotCount += renderer.sharedMaterials.Length;
                Bounds bounds = renderer.bounds;
                float magnitude = bounds.size.magnitude;
                if (float.IsNaN(magnitude) || float.IsInfinity(magnitude))
                {
                    invalidBoundsCount++;
                }
                else
                {
                    maximumBoundsMagnitude = Mathf.Max(
                        maximumBoundsMagnitude,
                        magnitude);
                }
            }

            return new RendererMetrics(
                activeRendererCount,
                materialSlotCount,
                invalidBoundsCount,
                maximumBoundsMagnitude);
        }

        private QueryMetrics CaptureQueryMetrics(UnitRegistry unitRegistry)
        {
            _units.Clear();
            unitRegistry.CopySnapshot(_units);
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

        private void FinishFailure(string failureMessage)
        {
            WriteReport(new[]
            {
                $"TimestampUtc={DateTime.UtcNow:O}",
                "Result=Failed",
                $"Failure={failureMessage}"
            });
            Debug.LogError($"[Step16StandaloneProfile] {failureMessage}");
            Application.Quit(2);
        }

        private void WriteReport(string[] lines)
        {
            if (string.IsNullOrWhiteSpace(_outputPath))
            {
                _outputPath = Path.Combine(
                    Application.persistentDataPath,
                    "Step16StandaloneProfile.txt");
            }

            string directory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(_outputPath, lines);
            Debug.Log(
                $"[Step16StandaloneProfile] Wrote '{_outputPath}'.");
        }

        private static bool TryGetArgument(
            string prefix,
            out string value)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = argument.Substring(prefix.Length).Trim('"');
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static void AccumulateMaximum(
            long value,
            ref long total,
            ref long maximum)
        {
            total += value;
            maximum = Math.Max(maximum, value);
        }

        private static string GetAverageMilliseconds(
            long nanoseconds,
            long count)
        {
            return (count == 0
                    ? 0d
                    : nanoseconds / (double)count / 1000000d)
                .ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string ToMilliseconds(long nanoseconds)
        {
            return (nanoseconds / 1000000d).ToString(
                "0.###",
                CultureInfo.InvariantCulture);
        }

        private static string GetAverage(long total, long count)
        {
            return (count == 0 ? 0d : total / (double)count).ToString(
                "0.###",
                CultureInfo.InvariantCulture);
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

        private readonly struct RendererMetrics
        {
            public int ActiveRendererCount { get; }
            public int MaterialSlotCount { get; }
            public int InvalidBoundsCount { get; }
            public float MaximumBoundsMagnitude { get; }

            public RendererMetrics(
                int activeRendererCount,
                int materialSlotCount,
                int invalidBoundsCount,
                float maximumBoundsMagnitude)
            {
                ActiveRendererCount = activeRendererCount;
                MaterialSlotCount = materialSlotCount;
                InvalidBoundsCount = invalidBoundsCount;
                MaximumBoundsMagnitude = maximumBoundsMagnitude;
            }
        }
    }
}
