using System;
using System.Collections.Generic;
using MonstersVsZombies.Data;
using MonstersVsZombies.Diagnostics;
using UnityEngine;

namespace MonstersVsZombies.Core.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PoolManager : MonoBehaviour
    {
        private readonly Dictionary<PoolId, RuntimeObjectPool> _pools =
            new Dictionary<PoolId, RuntimeObjectPool>();
        private int _unknownPoolFailedRentCount;
        private Transform _inactiveCreationRoot;

        [field: SerializeField] public PoolCatalog PoolCatalog { get; private set; }

        public bool IsInitialized { get; private set; }
        public int UnknownPoolFailedRentCount => _unknownPoolFailedRentCount;
        internal Transform InactiveCreationRoot => _inactiveCreationRoot;

        public int TotalFailedRentCount
        {
            get
            {
                int failedRentCount = _unknownPoolFailedRentCount;
                foreach (RuntimeObjectPool pool in _pools.Values)
                {
                    failedRentCount += pool.FailedRentCount;
                }

                return failedRentCount;
            }
        }

        private void OnDestroy()
        {
            ClearPools();
        }

        public bool Initialize(PoolCatalog poolCatalog, out string failureMessage)
        {
            if (IsInitialized)
            {
                failureMessage = "PoolManager is already initialized.";
                return false;
            }

            if (poolCatalog == null)
            {
                failureMessage = "PoolManager requires a PoolCatalog.";
                return false;
            }

            ValidationResult validationResult = poolCatalog.Validate();
            if (!validationResult.IsValid)
            {
                failureMessage = validationResult.Issues[0].Message;
                return false;
            }

            for (int entryIndex = 0; entryIndex < poolCatalog.Count; entryIndex++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(entryIndex);
                PooledEntity pooledEntity = entry.Prefab.GetComponent<PooledEntity>();
                if (pooledEntity == null)
                {
                    failureMessage =
                        $"Pool '{entry.PoolId}' prefab requires PooledEntity on its root.";
                    return false;
                }

                pooledEntity.CachePoolableComponents();
            }

            PoolCatalog = poolCatalog;
            GameObject creationRoot = new GameObject("__InactivePoolCreation");
            creationRoot.transform.SetParent(transform, false);
            creationRoot.SetActive(false);
            _inactiveCreationRoot = creationRoot.transform;
            for (int entryIndex = 0; entryIndex < poolCatalog.Count; entryIndex++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(entryIndex);
                RuntimeObjectPool pool = new RuntimeObjectPool(this, entry);
                _pools.Add(entry.PoolId, pool);
                if (!pool.TryPrewarm(out PoolFailureReason failureReason))
                {
                    failureMessage =
                        $"Pool '{entry.PoolId}' failed to prewarm: {failureReason}.";
                    ClearPools();
                    PoolCatalog = null;
                    return false;
                }
            }

            IsInitialized = true;
            failureMessage = string.Empty;
            return true;
        }

        public PoolRentResult<PooledEntity> Rent(PoolId poolId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
#endif
            using (SandboxPerformanceDiagnostics.PoolRentMarker.Auto())
            {
                PoolRentResult<PooledEntity> result = RentInternal(poolId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SandboxPerformanceDiagnostics.RecordAllocation(
                    SandboxPerformanceSubsystem.PoolRent,
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
#endif
                return result;
            }
        }

        private PoolRentResult<PooledEntity> RentInternal(PoolId poolId)
        {
            if (!poolId.IsValid || !_pools.TryGetValue(poolId, out RuntimeObjectPool pool))
            {
                _unknownPoolFailedRentCount++;
                return PoolRentResult<PooledEntity>.CreateFailure(
                    poolId,
                    PoolFailureReason.UnknownPool);
            }

            return pool.Rent();
        }

        public PoolReturnResult Return(PooledEntity entity)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
#endif
            using (SandboxPerformanceDiagnostics.PoolReturnMarker.Auto())
            {
                PoolReturnResult result = ReturnInternal(entity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SandboxPerformanceDiagnostics.RecordAllocation(
                    SandboxPerformanceSubsystem.PoolReturn,
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
#endif
                return result;
            }
        }

        private PoolReturnResult ReturnInternal(PooledEntity entity)
        {
            if (entity == null || !entity.IsOwnedBy(this))
            {
                return PoolReturnResult.CreateFailure(
                    entity == null ? default : entity.PoolId,
                    PoolFailureReason.ForeignEntity);
            }

            if (!_pools.TryGetValue(entity.PoolId, out RuntimeObjectPool pool))
            {
                return PoolReturnResult.CreateFailure(
                    entity.PoolId,
                    PoolFailureReason.UnknownPool);
            }

            return pool.Return(entity);
        }

        public bool TryEnsureInactiveCount(
            PoolId poolId,
            int inactiveCount,
            out PoolFailureReason failureReason)
        {
            if (!poolId.IsValid ||
                !_pools.TryGetValue(poolId, out RuntimeObjectPool pool))
            {
                failureReason = PoolFailureReason.UnknownPool;
                return false;
            }

            return pool.TryEnsureInactiveCount(
                inactiveCount,
                out failureReason);
        }

        public bool TryGetDiagnostics(PoolId poolId, out PoolDiagnostics diagnostics)
        {
            if (_pools.TryGetValue(poolId, out RuntimeObjectPool pool))
            {
                diagnostics = pool.GetDiagnostics();
                return true;
            }

            diagnostics = default;
            return false;
        }

        public int CopyDiagnostics(List<PoolDiagnostics> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (RuntimeObjectPool pool in _pools.Values)
            {
                destination.Add(pool.GetDiagnostics());
            }

            destination.Sort((left, right) => string.CompareOrdinal(
                left.PoolId.Value,
                right.PoolId.Value));
            return destination.Count;
        }

        public int CopyActiveEntities(List<PooledEntity> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (RuntimeObjectPool pool in _pools.Values)
            {
                pool.CopyActiveEntities(destination);
            }

            return destination.Count;
        }

        private void ClearPools()
        {
            foreach (RuntimeObjectPool pool in _pools.Values)
            {
                pool.Clear();
            }

            _pools.Clear();
            IsInitialized = false;
            if (_inactiveCreationRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_inactiveCreationRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(_inactiveCreationRoot.gameObject);
                }

                _inactiveCreationRoot = null;
            }
        }
    }
}
