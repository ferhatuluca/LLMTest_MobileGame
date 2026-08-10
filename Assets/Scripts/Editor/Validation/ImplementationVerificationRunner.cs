using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MonstersVsZombies.Editor.Validation
{
    [InitializeOnLoad]
    public static class ImplementationVerificationRunner
    {
        private const string k_EditModeAssemblyName = "MonstersVsZombies.Tests.EditMode";
        private const string k_PlayModeAssemblyName = "MonstersVsZombies.Tests.PlayMode";
        private const string k_EditModeTriggerRelativePath =
            "Temp/RunImplementationEditModeTests.request";
        private const string k_PlayModeTriggerRelativePath =
            "Temp/RunImplementationPlayModeTests.request";
        private const string k_EditModeResultRelativePath =
            "Logs/ImplementationEditModeSummary.txt";
        private const string k_PlayModeResultRelativePath =
            "Logs/ImplementationPlayModeSummary.txt";

        private static bool s_isRunning;
        private static TestRunnerApi s_activeTestRunnerApi;

        static ImplementationVerificationRunner()
        {
            EditorApplication.update += TryRunRequestedVerification;
        }

        [MenuItem("Tools/Monsters vs Zombies/Verification/Run Edit Mode Tests")]
        public static void RunEditModeTests()
        {
            if (s_isRunning || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[ImplementationVerification] Unity is busy; Edit Mode verification was not started.");
                return;
            }

            s_isRunning = true;
            try
            {
                TestRunnerApi testRunnerApi =
                    ScriptableObject.CreateInstance<TestRunnerApi>();
                VerificationCallbacks callbacks = new VerificationCallbacks(
                    "Edit Mode",
                    GetProjectPath(k_EditModeResultRelativePath));
                testRunnerApi.RegisterCallbacks(callbacks);

                ExecutionSettings executionSettings = new ExecutionSettings(
                    new Filter
                    {
                        assemblyNames = new[] { k_EditModeAssemblyName },
                        testMode = TestMode.EditMode
                    })
                {
                    runSynchronously = true
                };

                Debug.Log(
                    $"[ImplementationVerification] Running {k_EditModeAssemblyName} synchronously.");
                testRunnerApi.Execute(executionSettings);
                UnityEngine.Object.DestroyImmediate(testRunnerApi);
            }
            catch (Exception exception)
            {
                WriteRunnerFailure(k_EditModeResultRelativePath, exception);
                Debug.LogException(exception);
            }
            finally
            {
                s_isRunning = false;
            }
        }

        [MenuItem("Tools/Monsters vs Zombies/Verification/Run Play Mode Tests")]
        public static void RunPlayModeTests()
        {
            if (s_isRunning || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[ImplementationVerification] Unity is busy; Play Mode verification was not started.");
                return;
            }

            s_isRunning = true;
            try
            {
                s_activeTestRunnerApi =
                    ScriptableObject.CreateInstance<TestRunnerApi>();
                VerificationCallbacks callbacks = new VerificationCallbacks(
                    "Play Mode",
                    GetProjectPath(k_PlayModeResultRelativePath),
                    CompleteAsynchronousRun);
                s_activeTestRunnerApi.RegisterCallbacks(callbacks);

                ExecutionSettings executionSettings = new ExecutionSettings(
                    new Filter
                    {
                        assemblyNames = new[] { k_PlayModeAssemblyName },
                        testMode = TestMode.PlayMode
                    });

                Debug.Log(
                    $"[ImplementationVerification] Running {k_PlayModeAssemblyName} asynchronously.");
                s_activeTestRunnerApi.Execute(executionSettings);
            }
            catch (Exception exception)
            {
                WriteRunnerFailure(k_PlayModeResultRelativePath, exception);
                CompleteAsynchronousRun();
                Debug.LogException(exception);
            }
        }

        private static void TryRunRequestedVerification()
        {
            if (s_isRunning || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string editModeTriggerPath =
                GetProjectPath(k_EditModeTriggerRelativePath);
            if (File.Exists(editModeTriggerPath))
            {
                File.Delete(editModeTriggerPath);
                RunEditModeTests();
                return;
            }

            string playModeTriggerPath =
                GetProjectPath(k_PlayModeTriggerRelativePath);
            if (File.Exists(playModeTriggerPath))
            {
                File.Delete(playModeTriggerPath);
                RunPlayModeTests();
            }
        }

        private static string GetProjectPath(string relativePath)
        {
            string projectPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectPath, relativePath);
        }

        private static void WriteRunnerFailure(
            string resultRelativePath,
            Exception exception)
        {
            string resultPath = GetProjectPath(resultRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllLines(
                resultPath,
                new[]
                {
                    $"TimestampUtc={DateTime.UtcNow:O}",
                    "Result=RunnerFailure",
                    $"Exception={exception}"
                });
        }

        private static void CompleteAsynchronousRun()
        {
            if (s_activeTestRunnerApi != null)
            {
                UnityEngine.Object.DestroyImmediate(s_activeTestRunnerApi);
                s_activeTestRunnerApi = null;
            }

            s_isRunning = false;
        }

        private sealed class VerificationCallbacks : ICallbacks
        {
            private readonly List<string> _failures = new List<string>();
            private readonly string _modeLabel;
            private readonly string _resultPath;
            private readonly Action _onRunFinished;

            public VerificationCallbacks(
                string modeLabel,
                string resultPath,
                Action onRunFinished = null)
            {
                _modeLabel = modeLabel;
                _resultPath = resultPath;
                _onRunFinished = onRunFinished;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                int total = result.PassCount + result.FailCount +
                            result.SkipCount + result.InconclusiveCount;
                List<string> lines = new List<string>
                {
                    $"TimestampUtc={DateTime.UtcNow:O}",
                    $"Result={result.ResultState}",
                    $"Total={total}",
                    $"Passed={result.PassCount}",
                    $"Failed={result.FailCount}",
                    $"Skipped={result.SkipCount}",
                    $"Inconclusive={result.InconclusiveCount}",
                    $"DurationSeconds={result.Duration:R}"
                };
                lines.AddRange(_failures);

                Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
                File.WriteAllLines(_resultPath, lines);
                Debug.Log(
                    $"[ImplementationVerification] {_modeLabel} {result.ResultState}: " +
                    $"{result.PassCount}/{total} passed, {result.FailCount} failed, " +
                    $"{result.SkipCount} skipped, {result.InconclusiveCount} inconclusive.");
                _onRunFinished?.Invoke();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.HasChildren && result.TestStatus == TestStatus.Failed)
                {
                    _failures.Add(
                        $"Failure={result.FullName}|{result.Message}|{result.StackTrace}");
                }
            }
        }
    }
}
