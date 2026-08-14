using UnityEngine;

namespace MonstersVsZombies.Diagnostics
{
    /// <summary>
    /// Stores the developer-only AI pause toggle.
    /// </summary>
    public static class SandboxDebugRuntime
    {
        private static bool s_areAIDecisionsPaused;

        public static bool IsAvailable =>
            Application.isEditor || Debug.isDebugBuild;
        public static bool AreAIDecisionsPaused =>
            IsAvailable && s_areAIDecisionsPaused;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_areAIDecisionsPaused = false;
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
    }
}
