using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Spawning;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSixProjectileSpawnProbe : MonoBehaviour,
        IPoolable,
        IProjectileSpawnLifecycle
    {
        [field: SerializeField] public bool FailConfiguration { get; private set; }
        [field: SerializeField] public bool FailPrepare { get; private set; }
        [field: SerializeField] public bool FailComplete { get; private set; }
        [field: SerializeField] public bool FailStart { get; private set; }

        public ProjectileSpawnRequest CapturedRequest { get; private set; }
        public bool ConfigurationObservedInactive { get; private set; }
        public bool PrepareObservedInactive { get; private set; }
        public bool EnableObservedConfiguredAndNotStarted { get; private set; }
        public bool CompleteObservedActiveAndNotStarted { get; private set; }
        public bool IsStarted { get; private set; }
        public int StartCount { get; private set; }

        private void OnEnable()
        {
            EnableObservedConfiguredAndNotStarted =
                CapturedRequest.IsValid && !IsStarted;
        }

        bool IProjectileSpawnLifecycle.ConfigureProjectileSpawn(
            ProjectileSpawnRequest spawnRequest)
        {
            CapturedRequest = spawnRequest;
            ConfigurationObservedInactive = !gameObject.activeInHierarchy;
            IsStarted = false;
            return !FailConfiguration;
        }

        bool IProjectileSpawnLifecycle.StartProjectile()
        {
            if (FailStart)
            {
                return false;
            }

            IsStarted = true;
            StartCount++;
            return true;
        }

        public bool PrepareForSpawn()
        {
            PrepareObservedInactive = !gameObject.activeInHierarchy;
            return CapturedRequest.IsValid && !FailPrepare;
        }

        public bool CompleteSpawn()
        {
            CompleteObservedActiveAndNotStarted =
                gameObject.activeInHierarchy && !IsStarted;
            return !FailComplete;
        }

        public void PrepareForReturn()
        {
            CapturedRequest = default;
            IsStarted = false;
            FailConfiguration = false;
            FailPrepare = false;
            FailComplete = false;
            FailStart = false;
        }
    }
}
