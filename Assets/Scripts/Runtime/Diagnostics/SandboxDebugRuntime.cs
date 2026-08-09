using System;
using UnityEngine;

namespace MonstersVsZombies.Diagnostics
{
    public enum SandboxDiagnosticCode
    {
        None,
        SpawnSucceeded,
        InvalidDefinition,
        MissingPool,
        InvalidSpawnPosition,
        CapacityReached,
        RentFailed,
        InitializationFailed,
        TargetQueryBufferFull
    }

    public readonly struct SandboxDiagnosticEvent
    {
        public SandboxDiagnosticCode Code { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }

        public SandboxDiagnosticEvent(
            SandboxDiagnosticCode code,
            string message,
            UnityEngine.Object context)
        {
            Code = code;
            Message = message ?? string.Empty;
            Context = context;
        }
    }

    public static class SandboxDebugRuntime
    {
        private static bool s_areAIDecisionsPaused;
        private static bool s_areDiagnosticsEnabled;

        public static event Action<SandboxDiagnosticEvent> DiagnosticReported;

        public static bool IsAvailable => Application.isEditor || Debug.isDebugBuild;
        public static bool AreAIDecisionsPaused =>
            IsAvailable && s_areAIDecisionsPaused;
        public static bool AreDiagnosticsEnabled =>
            IsAvailable && s_areDiagnosticsEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_areAIDecisionsPaused = false;
            s_areDiagnosticsEnabled = false;
            DiagnosticReported = null;
        }

        public static bool SetAIDecisionsPaused(bool isPaused)
        {
            if (!IsAvailable)
            {
                return false;
            }

            s_areAIDecisionsPaused = isPaused;
            return true;
        }

        public static bool SetDiagnosticsEnabled(bool isEnabled)
        {
            if (!IsAvailable)
            {
                return false;
            }

            s_areDiagnosticsEnabled = isEnabled;
            return true;
        }

        public static void Report(
            SandboxDiagnosticCode code,
            string message,
            UnityEngine.Object context = null)
        {
            if (!AreDiagnosticsEnabled ||
                !Enum.IsDefined(typeof(SandboxDiagnosticCode), code) ||
                code == SandboxDiagnosticCode.None)
            {
                return;
            }

            SandboxDiagnosticEvent diagnosticEvent =
                new SandboxDiagnosticEvent(code, message, context);
            DiagnosticReported?.Invoke(diagnosticEvent);

            if (code == SandboxDiagnosticCode.TargetQueryBufferFull ||
                code == SandboxDiagnosticCode.InvalidDefinition ||
                code == SandboxDiagnosticCode.MissingPool ||
                code == SandboxDiagnosticCode.InvalidSpawnPosition ||
                code == SandboxDiagnosticCode.CapacityReached ||
                code == SandboxDiagnosticCode.RentFailed ||
                code == SandboxDiagnosticCode.InitializationFailed)
            {
                Debug.LogWarning($"[SandboxDiagnostics:{code}] {message}", context);
            }
        }
    }
}
