using MonstersVsZombies.Core.Pooling;
using UnityEngine;

namespace MonstersVsZombies.Units.Lifecycle
{
    /// <summary>
    /// Requests an immediate pooled return when a unit enters Dying, for units
    /// that have no ordered death effect to finish first.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitLifecycleController))]
    public sealed class ImmediateDeathPoolReturn : MonoBehaviour, IPoolable
    {
        private UnitLifecycleController _lifecycleController;

        private void Awake()
        {
            CacheAndSubscribe();
        }

        private void OnDestroy()
        {
            if (_lifecycleController != null)
            {
                _lifecycleController.Dying -= HandleDying;
            }
        }

        public bool PrepareForSpawn()
        {
            CacheAndSubscribe();
            return _lifecycleController != null;
        }

        public bool CompleteSpawn()
        {
            return _lifecycleController != null && gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
        }

        private void HandleDying(UnitLifecycleChangedEvent lifecycleEvent)
        {
            _lifecycleController.RequestPoolReturn();
        }

        private void CacheAndSubscribe()
        {
            UnitLifecycleController lifecycleController =
                GetComponent<UnitLifecycleController>();
            if (_lifecycleController == lifecycleController)
            {
                return;
            }

            if (_lifecycleController != null)
            {
                _lifecycleController.Dying -= HandleDying;
            }

            _lifecycleController = lifecycleController;
            if (_lifecycleController != null)
            {
                _lifecycleController.Dying += HandleDying;
            }
        }
    }
}
