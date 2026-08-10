using System;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using MonstersVsZombies.Combat.Interaction;
using UnityEngine;

namespace MonstersVsZombies.Spawning
{
    /// <summary>
    /// Coordinates transactional unit and projectile spawning: rent, configure
    /// while inactive, reset, activate, finalize, and roll back on any failure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnManager : MonoBehaviour
    {
        private long _lastSpawnId;

        [field: SerializeField] public PoolManager PoolManager { get; private set; }
        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }

        public bool IsInitialized { get; private set; }

        public bool Initialize(
            PoolManager poolManager,
            UnitRegistry unitRegistry,
            out string failureMessage)
        {
            if (IsInitialized)
            {
                failureMessage = "SpawnManager is already initialized.";
                return false;
            }

            if (poolManager == null || !poolManager.IsInitialized)
            {
                failureMessage = "SpawnManager requires an initialized PoolManager.";
                return false;
            }

            if (unitRegistry == null)
            {
                failureMessage = "SpawnManager requires a UnitRegistry.";
                return false;
            }

            PoolManager = poolManager;
            UnitRegistry = unitRegistry;
            IsInitialized = true;
            failureMessage = string.Empty;
            return true;
        }

        public SpawnResult<UnitController> SpawnUnit(UnitSpawnRequest spawnRequest)
        {
            return SpawnUnit(spawnRequest, null);
        }

        public SpawnResult<UnitController> SpawnUnit(
            UnitSpawnRequest spawnRequest,
            ISpawnPositionValidator positionValidator)
        {
            if (!IsInitialized)
            {
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.RentFailed);
            }

            if (!spawnRequest.HasValidDefinition ||
                !spawnRequest.HasValidMetadata)
            {
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.InvalidDefinition);
            }

            if (!spawnRequest.HasValidPose)
            {
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.InvalidPosition);
            }

            Vector3 resolvedPosition = spawnRequest.Position;
            if (positionValidator != null &&
                (!positionValidator.TryResolvePosition(
                     spawnRequest.Position,
                     out resolvedPosition) ||
                 !SpawnRequestValidation.IsPoseValid(
                     resolvedPosition,
                     spawnRequest.Rotation)))
            {
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.InvalidPosition);
            }

            PoolRentResult<PooledEntity> rentResult =
                PoolManager.Rent(spawnRequest.Definition.PoolId);
            if (!rentResult.IsSuccess)
            {
                return SpawnResult<UnitController>.CreateFailure(
                    MapPoolFailure(rentResult.FailureReason));
            }

            PooledEntity pooledEntity = rentResult.Entity;
            pooledEntity.transform.SetPositionAndRotation(
                resolvedPosition,
                spawnRequest.Rotation);

            UnitController unitController =
                pooledEntity.GetComponent<UnitController>();
            UnitLifecycleController lifecycleController =
                pooledEntity.GetComponent<UnitLifecycleController>();
            SpawnId spawnId = CreateSpawnId();
            UnitSpawnContext spawnContext = new UnitSpawnContext(
                spawnRequest,
                spawnId);

            if (unitController == null || lifecycleController == null ||
                !lifecycleController.ConfigureSpawn(
                    spawnRequest.Definition,
                    spawnId) ||
                !ConfigureUnitContextReceivers(pooledEntity, spawnContext) ||
                !pooledEntity.PrepareForSpawn())
            {
                ReturnPartialSpawn(pooledEntity);
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.ActivationIndependentInitializationFailed);
            }

            pooledEntity.gameObject.SetActive(true);
            if (!pooledEntity.CompleteSpawn() ||
                !lifecycleController.ActivateSpawn())
            {
                ReturnPartialSpawn(pooledEntity);
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.ActivationDependentInitializationFailed);
            }

            lifecycleController.PoolReturnRequested += HandleUnitPoolReturnRequested;
            lifecycleController.RegisterSpawnSubscription(
                () => lifecycleController.PoolReturnRequested -=
                    HandleUnitPoolReturnRequested);
            if (!UnitRegistry.Register(unitController))
            {
                ReturnPartialSpawn(pooledEntity);
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.ActivationDependentInitializationFailed);
            }

            return SpawnResult<UnitController>.CreateSuccess(unitController);
        }

        public SpawnResult<UnitController> SpawnDeathUnit(
            UnitDefinition definition,
            Pose spawnPose,
            SpawnId sourceSpawnId,
            ISpawnPositionValidator positionValidator = null)
        {
            if (!sourceSpawnId.IsValid)
            {
                return SpawnResult<UnitController>.CreateFailure(
                    SpawnFailureReason.InvalidDefinition);
            }

            return SpawnUnit(
                new UnitSpawnRequest(
                    definition,
                    spawnPose.position,
                    spawnPose.rotation,
                    sourceSpawnId,
                    SpawnReason.DeathEffect),
                positionValidator);
        }

        public SpawnResult<PooledEntity> SpawnProjectile(
            ProjectileSpawnRequest spawnRequest)
        {
            return SpawnProjectile(spawnRequest, null);
        }

        public SpawnResult<PooledEntity> SpawnProjectile(
            ProjectileSpawnRequest spawnRequest,
            InteractionSystem interactionSystem)
        {
            if (!IsInitialized)
            {
                return SpawnResult<PooledEntity>.CreateFailure(
                    SpawnFailureReason.RentFailed);
            }

            if (!spawnRequest.HasValidDefinition ||
                !spawnRequest.DamagePayload.IsValid)
            {
                return SpawnResult<PooledEntity>.CreateFailure(
                    SpawnFailureReason.InvalidDefinition);
            }

            if (!spawnRequest.HasValidPose)
            {
                return SpawnResult<PooledEntity>.CreateFailure(
                    SpawnFailureReason.InvalidPosition);
            }

            PoolRentResult<PooledEntity> rentResult =
                PoolManager.Rent(spawnRequest.Definition.PoolId);
            if (!rentResult.IsSuccess)
            {
                return SpawnResult<PooledEntity>.CreateFailure(
                    MapPoolFailure(rentResult.FailureReason));
            }

            PooledEntity pooledEntity = rentResult.Entity;
            pooledEntity.transform.SetPositionAndRotation(
                spawnRequest.Position,
                spawnRequest.Rotation);
            IProjectileSpawnLifecycle projectileLifecycle =
                FindProjectileLifecycle(pooledEntity);
            if (projectileLifecycle == null ||
                !ConfigureProjectileRuntime(
                    projectileLifecycle,
                    interactionSystem) ||
                !projectileLifecycle.ConfigureProjectileSpawn(spawnRequest) ||
                !pooledEntity.PrepareForSpawn())
            {
                ReturnPartialSpawn(pooledEntity);
                return SpawnResult<PooledEntity>.CreateFailure(
                    SpawnFailureReason.ActivationIndependentInitializationFailed);
            }

            pooledEntity.gameObject.SetActive(true);
            if (!pooledEntity.CompleteSpawn() ||
                !projectileLifecycle.StartProjectile())
            {
                ReturnPartialSpawn(pooledEntity);
                return SpawnResult<PooledEntity>.CreateFailure(
                    SpawnFailureReason.ActivationDependentInitializationFailed);
            }

            return SpawnResult<PooledEntity>.CreateSuccess(pooledEntity);
        }

        public PoolReturnResult ReturnUnit(UnitController unitController)
        {
            if (!IsInitialized || PoolManager == null || unitController == null)
            {
                return PoolReturnResult.CreateFailure(
                    default,
                    PoolFailureReason.ForeignEntity);
            }

            return PoolManager.Return(unitController.GetComponent<PooledEntity>());
        }

        public PoolReturnResult ReturnProjectile(PooledEntity pooledEntity)
        {
            if (!IsInitialized || PoolManager == null)
            {
                return PoolReturnResult.CreateFailure(
                    pooledEntity == null ? default : pooledEntity.PoolId,
                    PoolFailureReason.ForeignEntity);
            }

            return PoolManager.Return(pooledEntity);
        }

        private SpawnId CreateSpawnId()
        {
            _lastSpawnId = checked(_lastSpawnId + 1);
            return new SpawnId(_lastSpawnId);
        }

        private bool ConfigureUnitContextReceivers(
            PooledEntity pooledEntity,
            UnitSpawnContext spawnContext)
        {
            MonoBehaviour[] behaviours = pooledEntity.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IUnitSpawnContextReceiver receiver &&
                    !receiver.ConfigureUnitSpawn(spawnContext))
                {
                    return false;
                }
            }

            return true;
        }

        private static IProjectileSpawnLifecycle FindProjectileLifecycle(
            PooledEntity pooledEntity)
        {
            MonoBehaviour[] behaviours = pooledEntity.GetComponents<MonoBehaviour>();
            IProjectileSpawnLifecycle matchingLifecycle = null;
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (!(behaviour is IProjectileSpawnLifecycle lifecycle))
                {
                    continue;
                }

                if (matchingLifecycle != null)
                {
                    return null;
                }

                matchingLifecycle = lifecycle;
            }

            return matchingLifecycle;
        }

        private bool ConfigureProjectileRuntime(
            IProjectileSpawnLifecycle projectileLifecycle,
            InteractionSystem interactionSystem)
        {
            if (!(projectileLifecycle is IProjectileSpawnRuntimeContextReceiver receiver))
            {
                return true;
            }

            return receiver.ConfigureProjectileRuntime(this, interactionSystem);
        }

        private void HandleUnitPoolReturnRequested(UnitPoolReturnRequest returnRequest)
        {
            ReturnUnit(returnRequest.Unit);
        }

        private void ReturnPartialSpawn(PooledEntity pooledEntity)
        {
            if (pooledEntity != null)
            {
                PoolManager.Return(pooledEntity);
            }
        }

        private static SpawnFailureReason MapPoolFailure(
            PoolFailureReason poolFailureReason)
        {
            switch (poolFailureReason)
            {
                case PoolFailureReason.UnknownPool:
                    return SpawnFailureReason.UnknownPool;
                case PoolFailureReason.CapacityReached:
                    return SpawnFailureReason.CapacityReached;
                default:
                    return SpawnFailureReason.RentFailed;
            }
        }
    }
}
