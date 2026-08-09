using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonstersVsZombies.Core.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PooledEntity : MonoBehaviour, IPoolable
    {
        private readonly List<IPoolable> _poolableComponents =
            new List<IPoolable>();
        private PoolManager _owningPoolManager;
        private bool _isPreparedForSpawn;
        private bool _isSpawnComplete;

        public PoolId PoolId { get; private set; }
        public bool IsRented { get; private set; }
        public bool IsPreparedForSpawn => _isPreparedForSpawn;
        public bool IsSpawnComplete => _isSpawnComplete;

        private void Awake()
        {
            CachePoolableComponents();
        }

        public bool PrepareForSpawn()
        {
            if (!IsRented || gameObject.activeInHierarchy)
            {
                return false;
            }

            _isPreparedForSpawn = false;
            _isSpawnComplete = false;
            foreach (IPoolable poolableComponent in _poolableComponents)
            {
                if (!poolableComponent.PrepareForSpawn())
                {
                    return false;
                }
            }

            _isPreparedForSpawn = true;
            return true;
        }

        public bool CompleteSpawn()
        {
            if (!IsRented || !_isPreparedForSpawn ||
                !gameObject.activeInHierarchy || _isSpawnComplete)
            {
                return false;
            }

            foreach (IPoolable poolableComponent in _poolableComponents)
            {
                if (!poolableComponent.CompleteSpawn())
                {
                    return false;
                }
            }

            _isSpawnComplete = true;
            return true;
        }

        public void PrepareForReturn()
        {
            foreach (IPoolable poolableComponent in _poolableComponents)
            {
                try
                {
                    poolableComponent.PrepareForReturn();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            _isPreparedForSpawn = false;
            _isSpawnComplete = false;
        }

        internal void CachePoolableComponents()
        {
            _poolableComponents.Clear();
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour == this ||
                    !(behaviour is IPoolable poolableComponent) ||
                    FindNearestPooledEntity(behaviour.transform) != this)
                {
                    continue;
                }

                _poolableComponents.Add(poolableComponent);
            }
        }

        internal bool BindToPool(PoolManager poolManager, PoolId poolId)
        {
            if (poolManager == null || !poolId.IsValid ||
                (_owningPoolManager != null && _owningPoolManager != poolManager))
            {
                return false;
            }

            _owningPoolManager = poolManager;
            PoolId = poolId;
            IsRented = false;
            _isPreparedForSpawn = false;
            _isSpawnComplete = false;
            CachePoolableComponents();
            return true;
        }

        internal bool IsOwnedBy(PoolManager poolManager)
        {
            return _owningPoolManager == poolManager;
        }

        internal bool MarkRented()
        {
            if (_owningPoolManager == null || IsRented ||
                gameObject.activeInHierarchy)
            {
                return false;
            }

            IsRented = true;
            _isPreparedForSpawn = false;
            _isSpawnComplete = false;
            return true;
        }

        internal void MarkReturning()
        {
            IsRented = false;
        }

        private static PooledEntity FindNearestPooledEntity(Transform startingTransform)
        {
            Transform currentTransform = startingTransform;
            while (currentTransform != null)
            {
                PooledEntity pooledEntity = currentTransform.GetComponent<PooledEntity>();
                if (pooledEntity != null)
                {
                    return pooledEntity;
                }

                currentTransform = currentTransform.parent;
            }

            return null;
        }
    }
}
