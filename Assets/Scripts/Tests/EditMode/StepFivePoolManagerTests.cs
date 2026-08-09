using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepFivePoolManagerTests
    {
        private StepFivePoolFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new StepFivePoolFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void Initialize_RejectsMissingCatalog()
        {
            PoolManager poolManager = _factory.CreatePoolManager();

            bool initialized = poolManager.Initialize(null, out string failureMessage);

            Assert.That(initialized, Is.False);
            Assert.That(poolManager.IsInitialized, Is.False);
            Assert.That(failureMessage, Does.Contain("PoolCatalog"));
        }

        [Test]
        public void Initialize_RejectsInvalidCatalogBeforeCreatingPools()
        {
            PoolManager poolManager = _factory.CreatePoolManager();
            PoolCatalog catalog = _factory.CreateCatalog(
                _factory.CreateEntry(
                    new PoolId("Invalid"),
                    null,
                    0,
                    1,
                    PoolCapacityPolicy.Expandable,
                    0,
                    true));

            bool initialized = poolManager.Initialize(catalog, out string failureMessage);

            Assert.That(initialized, Is.False);
            Assert.That(poolManager.IsInitialized, Is.False);
            Assert.That(failureMessage, Does.Contain("prefab"));
            Assert.That(
                poolManager.TryGetDiagnostics(new PoolId("Invalid"), out _),
                Is.False);
        }

        [Test]
        public void Initialize_RejectsPrefabWithoutRootPooledEntity()
        {
            GameObject invalidPrefab = _factory.CreateGameObject("InvalidPrefab");
            invalidPrefab.SetActive(false);
            GameObject child = _factory.CreateGameObject("NestedPooledEntity");
            child.transform.SetParent(invalidPrefab.transform, false);
            child.AddComponent<PooledEntity>();
            PoolCatalog catalog = _factory.CreateCatalog(
                _factory.CreateEntry(
                    new PoolId("InvalidRoot"),
                    invalidPrefab,
                    0,
                    1,
                    PoolCapacityPolicy.Expandable,
                    0,
                    true));
            PoolManager poolManager = _factory.CreatePoolManager();

            bool initialized = poolManager.Initialize(catalog, out string failureMessage);

            Assert.That(initialized, Is.False);
            Assert.That(failureMessage, Does.Contain("root"));
        }

        [Test]
        public void Prewarm_CreatesConfiguredInactiveCountWithoutActivation()
        {
            PoolId poolId = new PoolId("Prewarm");
            PoolManager poolManager = _factory.CreateInitializedPool(
                poolId,
                initialPrewarmCount: 3,
                maximumInactiveRetainedCount: 4);

            Assert.That(poolManager.TryGetDiagnostics(poolId, out PoolDiagnostics diagnostics), Is.True);
            Assert.That(diagnostics.CreatedCount, Is.EqualTo(3));
            Assert.That(diagnostics.ActiveCount, Is.Zero);
            Assert.That(diagnostics.InactiveCount, Is.EqualTo(3));
            Assert.That(diagnostics.PeakActiveCount, Is.Zero);

            StepFivePoolableProbe[] probes =
                poolManager.GetComponentsInChildren<StepFivePoolableProbe>(true);
            Assert.That(probes, Has.Length.EqualTo(3));
            foreach (StepFivePoolableProbe probe in probes)
            {
                Assert.That(probe.gameObject.activeInHierarchy, Is.False);
                Assert.That(probe.EnableCount, Is.Zero);
            }
        }

        [Test]
        public void RentSpawnReturnAndReuse_FollowsTwoPhaseOrderAndCleansState()
        {
            PoolId poolId = new PoolId("Lifecycle");
            PoolManager poolManager = _factory.CreateInitializedPool(
                poolId,
                initialPrewarmCount: 1,
                maximumInactiveRetainedCount: 1);
            List<string> eventLog = new List<string>();

            PoolRentResult<PooledEntity> firstRent = poolManager.Rent(poolId);
            PooledEntity firstEntity = firstRent.Entity;
            StepFivePoolableProbe firstProbe =
                firstEntity.GetComponent<StepFivePoolableProbe>();
            firstProbe.EventLog = eventLog;
            firstProbe.SpawnId = new SpawnId(1);
            firstProbe.HasTransientState = true;

            Assert.That(firstRent.IsSuccess, Is.True);
            Assert.That(firstEntity.gameObject.activeInHierarchy, Is.False);
            Assert.That(firstEntity.PrepareForSpawn(), Is.True);
            Assert.That(firstProbe.PrepareObservedInactive, Is.True);
            Assert.That(firstProbe.HasTransientState, Is.False);

            firstEntity.gameObject.SetActive(true);
            Assert.That(firstEntity.CompleteSpawn(), Is.True);
            Assert.That(firstProbe.CompleteObservedActive, Is.True);
            Assert.That(firstProbe.CompleteObservedLogicalInactive, Is.True);
            Assert.That(firstProbe.CompleteObservedUnregistered, Is.True);

            firstProbe.IsLogicallyActive = true;
            firstProbe.IsRegistered = true;
            firstProbe.HasTransientState = true;
            Assert.That(poolManager.Return(firstEntity).IsSuccess, Is.True);
            Assert.That(firstProbe.ReturnObservedRentedStateCleared, Is.True);
            Assert.That(firstProbe.IsLogicallyActive, Is.False);
            Assert.That(firstProbe.IsRegistered, Is.False);
            Assert.That(firstProbe.HasTransientState, Is.False);
            Assert.That(firstProbe.SpawnId.IsValid, Is.False);
            Assert.That(firstEntity.gameObject.activeInHierarchy, Is.False);

            PoolRentResult<PooledEntity> secondRent = poolManager.Rent(poolId);
            StepFivePoolableProbe secondProbe =
                secondRent.Entity.GetComponent<StepFivePoolableProbe>();
            Assert.That(secondRent.Entity, Is.SameAs(firstEntity));
            Assert.That(secondProbe.SpawnId.IsValid, Is.False);
            secondProbe.EventLog = eventLog;
            secondProbe.SpawnId = new SpawnId(2);
            Assert.That(secondRent.Entity.PrepareForSpawn(), Is.True);

            Assert.That(
                eventLog,
                Is.EqualTo(new[]
                {
                    "PrepareForSpawn",
                    "CompleteSpawn",
                    "PrepareForReturn",
                    "PrepareForSpawn"
                }));
        }

        [Test]
        public void SpawnPhases_RejectWrongActivationState()
        {
            PoolId poolId = new PoolId("PhaseGuard");
            PoolManager poolManager = _factory.CreateInitializedPool(poolId);
            PooledEntity entity = poolManager.Rent(poolId).Entity;
            StepFivePoolableProbe probe = entity.GetComponent<StepFivePoolableProbe>();
            probe.SpawnId = new SpawnId(1);

            Assert.That(entity.CompleteSpawn(), Is.False);
            entity.gameObject.SetActive(true);
            Assert.That(entity.PrepareForSpawn(), Is.False);
            entity.gameObject.SetActive(false);
            Assert.That(entity.PrepareForSpawn(), Is.True);
            Assert.That(entity.CompleteSpawn(), Is.False);
        }

        [Test]
        public void FailedSpawnPhase_CanReturnWithoutLogicalActivation()
        {
            PoolId poolId = new PoolId("FailedPhase");
            PoolManager poolManager = _factory.CreateInitializedPool(poolId);
            PooledEntity entity = poolManager.Rent(poolId).Entity;
            StepFivePoolableProbe probe = entity.GetComponent<StepFivePoolableProbe>();
            probe.SpawnId = new SpawnId(1);
            probe.FailComplete = true;

            Assert.That(entity.PrepareForSpawn(), Is.True);
            entity.gameObject.SetActive(true);
            Assert.That(entity.CompleteSpawn(), Is.False);
            Assert.That(probe.IsLogicallyActive, Is.False);
            Assert.That(poolManager.Return(entity).IsSuccess, Is.True);
            Assert.That(entity.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void FailedActivationIndependentPhase_CanReturnInactive()
        {
            PoolId poolId = new PoolId("FailedPrepare");
            PoolManager poolManager = _factory.CreateInitializedPool(poolId);
            PooledEntity entity = poolManager.Rent(poolId).Entity;
            StepFivePoolableProbe probe = entity.GetComponent<StepFivePoolableProbe>();
            probe.SpawnId = new SpawnId(1);
            probe.FailPrepare = true;

            Assert.That(entity.PrepareForSpawn(), Is.False);
            Assert.That(entity.IsPreparedForSpawn, Is.False);
            Assert.That(entity.gameObject.activeInHierarchy, Is.False);
            Assert.That(probe.IsLogicallyActive, Is.False);
            Assert.That(poolManager.Return(entity).IsSuccess, Is.True);
            Assert.That(entity.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void Return_DetectsDoubleReturnAndForeignEntity()
        {
            PoolId poolId = new PoolId("ReturnErrors");
            PoolManager poolManager = _factory.CreateInitializedPool(poolId);
            PooledEntity entity = poolManager.Rent(poolId).Entity;

            PoolReturnResult firstReturn = poolManager.Return(entity);
            PoolReturnResult secondReturn = poolManager.Return(entity);
            GameObject foreignObject = _factory.CreateGameObject("ForeignEntity");
            foreignObject.SetActive(false);
            PooledEntity foreignEntity = foreignObject.AddComponent<PooledEntity>();
            PoolReturnResult foreignReturn = poolManager.Return(foreignEntity);

            Assert.That(firstReturn.IsSuccess, Is.True);
            Assert.That(
                secondReturn.FailureReason,
                Is.EqualTo(PoolFailureReason.AlreadyReturned));
            Assert.That(
                foreignReturn.FailureReason,
                Is.EqualTo(PoolFailureReason.ForeignEntity));
        }

        [Test]
        public void Rent_UnknownPoolReturnsControlledFailureAndDiagnostic()
        {
            PoolManager poolManager = _factory.CreateInitializedPool(new PoolId("Known"));

            PoolRentResult<PooledEntity> result =
                poolManager.Rent(new PoolId("Missing"));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(PoolFailureReason.UnknownPool));
            Assert.That(poolManager.UnknownPoolFailedRentCount, Is.EqualTo(1));
            Assert.That(poolManager.TotalFailedRentCount, Is.EqualTo(1));
        }

        [Test]
        public void Return_OverflowDestroysBeyondMaximumInactiveRetainedCount()
        {
            PoolId poolId = new PoolId("Retention");
            PoolManager poolManager = _factory.CreateInitializedPool(
                poolId,
                maximumInactiveRetainedCount: 1);
            PooledEntity firstEntity = poolManager.Rent(poolId).Entity;
            PooledEntity secondEntity = poolManager.Rent(poolId).Entity;

            Assert.That(poolManager.Return(firstEntity).IsSuccess, Is.True);
            Assert.That(poolManager.Return(secondEntity).IsSuccess, Is.True);
            Assert.That(poolManager.TryGetDiagnostics(poolId, out PoolDiagnostics diagnostics), Is.True);

            Assert.That(diagnostics.CreatedCount, Is.EqualTo(2));
            Assert.That(diagnostics.ActiveCount, Is.Zero);
            Assert.That(diagnostics.InactiveCount, Is.EqualTo(1));
            Assert.That(diagnostics.PeakActiveCount, Is.EqualTo(2));
            Assert.That(diagnostics.OverflowDestroyCount, Is.EqualTo(1));
        }

        [Test]
        public void ExpandablePool_GrowsWithoutFailedRent()
        {
            PoolId poolId = new PoolId("Expandable");
            PoolManager poolManager = _factory.CreateInitializedPool(
                poolId,
                maximumInactiveRetainedCount: 1,
                capacityPolicy: PoolCapacityPolicy.Expandable);

            PoolRentResult<PooledEntity> first = poolManager.Rent(poolId);
            PoolRentResult<PooledEntity> second = poolManager.Rent(poolId);
            PoolRentResult<PooledEntity> third = poolManager.Rent(poolId);
            Assert.That(poolManager.TryGetDiagnostics(poolId, out PoolDiagnostics diagnostics), Is.True);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(third.IsSuccess, Is.True);
            Assert.That(diagnostics.CreatedCount, Is.EqualTo(3));
            Assert.That(diagnostics.ActiveCount, Is.EqualTo(3));
            Assert.That(diagnostics.FailedRentCount, Is.Zero);
            Assert.That(poolManager.TotalFailedRentCount, Is.Zero);
        }

        [Test]
        public void HardActiveLimit_RejectsBeforeObjectPoolGet()
        {
            PoolId poolId = new PoolId("HardLimit");
            PoolManager poolManager = _factory.CreateInitializedPool(
                poolId,
                maximumInactiveRetainedCount: 1,
                capacityPolicy: PoolCapacityPolicy.HardActiveLimit,
                maximumActiveCount: 2);

            Assert.That(poolManager.Rent(poolId).IsSuccess, Is.True);
            Assert.That(poolManager.Rent(poolId).IsSuccess, Is.True);
            PoolRentResult<PooledEntity> rejectedRent = poolManager.Rent(poolId);
            Assert.That(poolManager.TryGetDiagnostics(poolId, out PoolDiagnostics diagnostics), Is.True);

            Assert.That(
                rejectedRent.FailureReason,
                Is.EqualTo(PoolFailureReason.CapacityReached));
            Assert.That(diagnostics.CreatedCount, Is.EqualTo(2));
            Assert.That(diagnostics.ActiveCount, Is.EqualTo(2));
            Assert.That(diagnostics.FailedRentCount, Is.EqualTo(1));
            Assert.That(diagnostics.CapacityReachedCount, Is.EqualTo(1));
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void CollectionChecks_RespectCatalogSettingInEditor(
            bool catalogSetting,
            bool expectedEnabled)
        {
            PoolId poolId = new PoolId($"CollectionChecks{catalogSetting}");
            PoolManager poolManager = _factory.CreateInitializedPool(
                poolId,
                enableCollectionChecks: catalogSetting);

            Assert.That(poolManager.TryGetDiagnostics(poolId, out PoolDiagnostics diagnostics), Is.True);
            Assert.That(diagnostics.CollectionChecksEnabled, Is.EqualTo(expectedEnabled));
        }

        [Test]
        public void NestedPooledEntity_OwnsItsOwnPoolableCallbacks()
        {
            GameObject prefab = _factory.CreatePooledPrefab("NestedOwnership");
            GameObject nestedObject = _factory.CreateGameObject("NestedRoot");
            nestedObject.SetActive(false);
            nestedObject.transform.SetParent(prefab.transform, false);
            StepFivePoolableProbe nestedProbe =
                nestedObject.AddComponent<StepFivePoolableProbe>();
            nestedObject.AddComponent<PooledEntity>();
            PoolId poolId = new PoolId("NestedOwnership");
            PoolManager poolManager = _factory.CreateInitializedPool(poolId, prefab: prefab);
            PooledEntity rentedRoot = poolManager.Rent(poolId).Entity;
            StepFivePoolableProbe rootProbe =
                rentedRoot.GetComponent<StepFivePoolableProbe>();
            StepFivePoolableProbe clonedNestedProbe =
                rentedRoot.transform.GetChild(0).GetComponent<StepFivePoolableProbe>();
            rootProbe.SpawnId = new SpawnId(1);
            clonedNestedProbe.SpawnId = new SpawnId(2);

            Assert.That(rentedRoot.PrepareForSpawn(), Is.True);

            Assert.That(rootProbe.PrepareObservedInactive, Is.True);
            Assert.That(clonedNestedProbe.PrepareObservedInactive, Is.False);
            Assert.That(nestedProbe.PrepareObservedInactive, Is.False);
        }
    }

    internal sealed class StepFivePoolFactory : IDisposable
    {
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();

        public PoolManager CreateInitializedPool(
            PoolId poolId,
            int initialPrewarmCount = 0,
            int maximumInactiveRetainedCount = 4,
            PoolCapacityPolicy capacityPolicy = PoolCapacityPolicy.Expandable,
            int maximumActiveCount = 0,
            bool enableCollectionChecks = true,
            GameObject prefab = null)
        {
            GameObject pooledPrefab = prefab ?? CreatePooledPrefab(poolId.ToString());
            PoolCatalogEntry entry = CreateEntry(
                poolId,
                pooledPrefab,
                initialPrewarmCount,
                maximumInactiveRetainedCount,
                capacityPolicy,
                maximumActiveCount,
                enableCollectionChecks);
            PoolCatalog catalog = CreateCatalog(entry);
            PoolManager poolManager = CreatePoolManager();
            Assert.That(poolManager.Initialize(catalog, out string failureMessage),
                Is.True,
                failureMessage);
            return poolManager;
        }

        public PoolManager CreatePoolManager()
        {
            return CreateGameObject("PoolManager").AddComponent<PoolManager>();
        }

        public GameObject CreatePooledPrefab(string objectName)
        {
            GameObject prefab = CreateGameObject(objectName);
            prefab.SetActive(false);
            prefab.AddComponent<StepFivePoolableProbe>();
            prefab.AddComponent<PooledEntity>();
            return prefab;
        }

        public GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        public PoolCatalogEntry CreateEntry(
            PoolId poolId,
            GameObject prefab,
            int initialPrewarmCount,
            int maximumInactiveRetainedCount,
            PoolCapacityPolicy capacityPolicy,
            int maximumActiveCount,
            bool enableCollectionChecks)
        {
            PoolCatalogEntry entry = new PoolCatalogEntry();
            SetAutoProperty(entry, nameof(PoolCatalogEntry.PoolId), poolId);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.Prefab), prefab);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.InitialPrewarmCount),
                initialPrewarmCount);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                maximumInactiveRetainedCount);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.CapacityPolicy),
                capacityPolicy);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumActiveCount),
                maximumActiveCount);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.EnableCollectionChecks),
                enableCollectionChecks);
            return entry;
        }

        public PoolCatalog CreateCatalog(params PoolCatalogEntry[] entries)
        {
            PoolCatalog catalog = ScriptableObject.CreateInstance<PoolCatalog>();
            catalog.name = "StepFivePoolCatalog";
            _createdObjects.Add(catalog);
            FieldInfo entriesField = typeof(PoolCatalog).GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            entriesField.SetValue(catalog, entries);
            return catalog;
        }

        public void Dispose()
        {
            for (int objectIndex = _createdObjects.Count - 1;
                 objectIndex >= 0;
                 objectIndex--)
            {
                UnityEngine.Object createdObject = _createdObjects[objectIndex];
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            FieldInfo backingField = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (backingField == null)
            {
                throw new MissingFieldException(target.GetType().FullName, propertyName);
            }

            backingField.SetValue(target, value);
        }
    }
}
