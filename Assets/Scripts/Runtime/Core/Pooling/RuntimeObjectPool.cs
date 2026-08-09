using System;
using System.Collections.Generic;
using MonstersVsZombies.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace MonstersVsZombies.Core.Pooling
{
    internal sealed class RuntimeObjectPool
    {
        private readonly HashSet<PooledEntity> _activeEntities =
            new HashSet<PooledEntity>();
        private readonly PoolManager _poolManager;
        private readonly PoolCatalogEntry _catalogEntry;
        private readonly ObjectPool<PooledEntity> _objectPool;
        private bool _isReleasingEntity;
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
            _objectPool = new ObjectPool<PooledEntity>(
                CreateEntity,
                null,
                null,
                DestroyEntity,
                collectionChecksEnabled,
                Math.Max(1, catalogEntry.InitialPrewarmCount),
                catalogEntry.MaximumInactiveRetainedCount);
        }

        public bool CollectionChecksEnabled { get; }

        public bool TryPrewarm(out PoolFailureReason failureReason)
        {
            List<PooledEntity> prewarmedEntities = new List<PooledEntity>(
                _catalogEntry.InitialPrewarmCount);
            try
            {
                for (int prewarmIndex = 0;
                     prewarmIndex < _catalogEntry.InitialPrewarmCount;
                     prewarmIndex++)
                {
                    PooledEntity entity = _objectPool.Get();
                    if (entity == null)
                    {
                        failureReason = PoolFailureReason.CreationFailed;
                        return false;
                    }

                    prewarmedEntities.Add(entity);
                }

                foreach (PooledEntity entity in prewarmedEntities)
                {
                    _objectPool.Release(entity);
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
            finally
            {
                foreach (PooledEntity entity in prewarmedEntities)
                {
                    if (entity != null && _objectPool.CountActive > 0)
                    {
                        try
                        {
                            _objectPool.Release(entity);
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                }
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
                entity = _objectPool.Get();
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
                    _objectPool.Release(entity);
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
            _isReleasingEntity = true;
            try
            {
                _objectPool.Release(entity);
            }
            finally
            {
                _isReleasingEntity = false;
            }

            return PoolReturnResult.CreateSuccess(PoolId);
        }

        public PoolDiagnostics GetDiagnostics()
        {
            return new PoolDiagnostics(
                PoolId,
                _createdCount,
                _activeEntities.Count,
                _objectPool.CountInactive,
                _peakActiveCount,
                _failedRentCount,
                _capacityReachedCount,
                _overflowDestroyCount,
                CollectionChecksEnabled);
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
            _objectPool.Clear();
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

        private void DestroyEntity(PooledEntity entity)
        {
            if (_isReleasingEntity)
            {
                _overflowDestroyCount++;
            }

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
