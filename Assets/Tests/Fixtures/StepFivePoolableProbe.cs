using System.Collections.Generic;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepFivePoolableProbe : MonoBehaviour, IPoolable
    {
        public List<string> EventLog { get; set; }
        public SpawnId SpawnId { get; set; }
        public bool IsLogicallyActive { get; set; }
        public bool IsRegistered { get; set; }
        public bool HasTransientState { get; set; }
        public bool FailPrepare { get; set; }
        public bool FailComplete { get; set; }
        public bool PrepareObservedInactive { get; private set; }
        public bool CompleteObservedActive { get; private set; }
        public bool CompleteObservedLogicalInactive { get; private set; }
        public bool CompleteObservedUnregistered { get; private set; }
        public bool ReturnObservedRentedStateCleared { get; private set; }
        public int EnableCount { get; private set; }

        private void OnEnable()
        {
            EnableCount++;
        }

        public bool PrepareForSpawn()
        {
            PooledEntity pooledEntity =
                GetComponentInParent<PooledEntity>(true);
            PrepareObservedInactive = !gameObject.activeInHierarchy;
            EventLog?.Add("PrepareForSpawn");
            HasTransientState = false;
            IsLogicallyActive = false;
            IsRegistered = false;
            return pooledEntity != null && pooledEntity.IsRented &&
                   SpawnId.IsValid && !FailPrepare;
        }

        public bool CompleteSpawn()
        {
            CompleteObservedActive = gameObject.activeInHierarchy;
            CompleteObservedLogicalInactive = !IsLogicallyActive;
            CompleteObservedUnregistered = !IsRegistered;
            EventLog?.Add("CompleteSpawn");
            return !FailComplete;
        }

        public void PrepareForReturn()
        {
            PooledEntity pooledEntity =
                GetComponentInParent<PooledEntity>(true);
            ReturnObservedRentedStateCleared =
                pooledEntity != null && !pooledEntity.IsRented;
            EventLog?.Add("PrepareForReturn");
            IsLogicallyActive = false;
            IsRegistered = false;
            HasTransientState = false;
            SpawnId = default;
            FailPrepare = false;
            FailComplete = false;
        }
    }
}
