using System;
using System.Collections.Generic;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Core.Pooling
{
    /// <summary>
    /// Implements one prefab-specific inactive stack, capacity policy, exact
    /// prewarming, creation statistics, and safe release behavior.
    /// </summary>
    internal sealed class RuntimeObjectPool
    {
        private readonly HashSet<PooledEntity> _activeEntities =
            new HashSet<PooledEntity>();
        private readonly Stack<PooledEntity> _inactiveEntities =
            new Stack<PooledEntity>();
        private readonly PoolManager _poolManager;
        private readonly PoolCatalogEntry _catalogEntry;
        private int _createdCount;
        private int _peakActiveCount;
        private int _failedRentCount;
        private int _capacityReachedCount;
        private int _overflowDestroyCount;

        public PoolId PoolId => _catalogEntry.PoolId;
        public int FailedRentCount => _failedRentCount;

        public RuntimeObjectPool(
            PoolManager poolManager,
            PoolCatalogEntry catalogEntry)
        {
            _poolManager = poolManager ??
                throw new ArgumentNullException(nameof(poolManager));
            _catalogEntry = catalogEntry ??
                throw new ArgumentNullException(nameof(catalogEntry));

            bool collectionChecksEnabled =
                catalogEntry.EnableCollectionChecks &&
                (Application.isEditor || Debug.isDebugBuild);
            CollectionChecksEnabled = collectionChecksEnabled;
        }

        public bool CollectionChecksEnabled { get; }

        public bool TryPrewarm(out PoolFailureReason failureReason)
        {
            return TryEnsureInactiveCount(
                _catalogEntry.InitialPrewarmCount,
                out failureReason);
        }

        public bool TryEnsureInactiveCount(
            int inactiveCount,
            out PoolFailureReason failureReason)
        {
            if (inactiveCount < 0 ||
                inactiveCount > _catalogEntry.MaximumInactiveRetainedCount)
            {
                failureReason = PoolFailureReason.CapacityReached;
                return false;
            }

            try
            {
                while (_inactiveEntities.Count < inactiveCount)
                {
                    _inactiveEntities.Push(CreateEntity());
                }

                failureReason = PoolFailureReason.None;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _poolManager);
                _failedRentCount++;
                failureReason = PoolFailureReason.CreationFailed;
                return false;
            }
        }

        public PoolRentResult<PooledEntity> Rent()
        {
            if (_catalogEntry.CapacityPolicy == PoolCapacityPolicy.HardActiveLimit &&
                _activeEntities.Count >= _catalogEntry.MaximumActiveCount)
            {
                _failedRentCount++;
                _capacityReachedCount++;
                return PoolRentResult<PooledEntity>.CreateFailure(
                    PoolId,
                    PoolFailureReason.CapacityReached);
            }

            PooledEntity entity;
            try
            {
                entity = _inactiveEntities.Count > 0
                    ? _inactiveEntities.Pop()
                    : CreateEntity();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _poolManager);
                _failedRentCount++;
                return PoolRentResult<PooledEntity>.CreateFailure(
                    PoolId,
                    PoolFailureReason.CreationFailed);
            }

            if (entity == null || !entity.MarkRented() || !_activeEntities.Add(entity))
            {
                _failedRentCount++;
                if (entity != null)
                {
                    entity.MarkReturning();
                    entity.gameObject.SetActive(false);
                    StoreOrDestroy(entity);
                }

                return PoolRentResult<PooledEntity>.CreateFailure(
                    PoolId,
                    PoolFailureReason.InitializationFailed);
            }

            _peakActiveCount = Math.Max(_peakActiveCount, _activeEntities.Count);
            return PoolRentResult<PooledEntity>.CreateSuccess(PoolId, entity);
        }

        public PoolReturnResult Return(PooledEntity entity)
        {
            if (entity == null || !entity.IsOwnedBy(_poolManager) ||
                entity.PoolId != PoolId)
            {
                return PoolReturnResult.CreateFailure(
                    entity == null ? default : entity.PoolId,
                    PoolFailureReason.ForeignEntity);
            }

            if (!_activeEntities.Remove(entity))
            {
                return PoolReturnResult.CreateFailure(
                    PoolId,
                    entity.IsRented
                        ? PoolFailureReason.ForeignEntity
                        : PoolFailureReason.AlreadyReturned);
            }

            entity.MarkReturning();
            entity.PrepareForReturn();
            entity.gameObject.SetActive(false);
            StoreOrDestroy(entity);

            return PoolReturnResult.CreateSuccess(PoolId);
        }

        public PoolDiagnostics GetDiagnostics()
        {
            return new PoolDiagnostics(
                PoolId,
                _createdCount,
                _activeEntities.Count,
                _inactiveEntities.Count,
                _peakActiveCount,
                _failedRentCount,
                _capacityReachedCount,
                _overflowDestroyCount,
                CollectionChecksEnabled);
        }

        public void CopyActiveEntities(List<PooledEntity> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            foreach (PooledEntity entity in _activeEntities)
            {
                if (entity != null)
                {
                    destination.Add(entity);
                }
            }
        }

        public void Clear()
        {
            foreach (PooledEntity entity in _activeEntities)
            {
                if (entity != null)
                {
                    entity.MarkReturning();
                    entity.PrepareForReturn();
                    entity.gameObject.SetActive(false);
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(entity.gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(entity.gameObject);
                    }
                }
            }

            _activeEntities.Clear();
            while (_inactiveEntities.Count > 0)
            {
                DestroyEntity(_inactiveEntities.Pop());
            }
        }

        private PooledEntity CreateEntity()
        {
            GameObject instance = UnityEngine.Object.Instantiate(
                _catalogEntry.Prefab,
                _poolManager.InactiveCreationRoot,
                false);
            instance.name = $"{_catalogEntry.Prefab.name} (Pooled)";
            instance.SetActive(false);
            instance.transform.SetParent(_poolManager.transform, false);

            PooledEntity pooledEntity = instance.GetComponent<PooledEntity>();
            if (pooledEntity == null ||
                !pooledEntity.BindToPool(_poolManager, PoolId))
            {
                UnityEngine.Object.DestroyImmediate(instance);
                throw new InvalidOperationException(
                    $"Pool '{PoolId}' could not initialize its pooled prefab root.");
            }

            _createdCount++;
            return pooledEntity;
        }

        private void StoreOrDestroy(PooledEntity entity)
        {
            if (_inactiveEntities.Count <
                _catalogEntry.MaximumInactiveRetainedCount)
            {
                _inactiveEntities.Push(entity);
                return;
            }

            _overflowDestroyCount++;
            DestroyEntity(entity);
        }

        private static void DestroyEntity(PooledEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(entity.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(entity.gameObject);
            }
        }
    }
}
