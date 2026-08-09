using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSixSpawnManagerTests
    {
        private StepSixSpawnFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new StepSixSpawnFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void SpawnUnit_SelectsPoolFromDefinition()
        {
            PoolId firstPoolId = new PoolId("FirstPool");
            PoolId secondPoolId = new PoolId("SecondPool");
            StepSixUnitPoolSpec firstSpec = new StepSixUnitPoolSpec(
                firstPoolId,
                UnitFaction.Player);
            StepSixUnitPoolSpec secondSpec = new StepSixUnitPoolSpec(
                secondPoolId,
                UnitFaction.Enemy);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(firstSpec, secondSpec);

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(secondSpec.Definition, Vector3.zero));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Entity.Definition, Is.SameAs(secondSpec.Definition));
            Assert.That(
                environment.PoolManager.TryGetDiagnostics(
                    firstPoolId,
                    out PoolDiagnostics firstDiagnostics),
                Is.True);
            Assert.That(
                environment.PoolManager.TryGetDiagnostics(
                    secondPoolId,
                    out PoolDiagnostics secondDiagnostics),
                Is.True);
            Assert.That(firstDiagnostics.ActiveCount, Is.Zero);
            Assert.That(secondDiagnostics.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void SpawnUnit_InvalidDefinitionDoesNotRent()
        {
            PoolId poolId = new PoolId("ValidPool");
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                poolId,
                UnitFaction.Player);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                new UnitSpawnRequest(
                    null,
                    Vector3.zero,
                    Quaternion.identity,
                    default,
                    SpawnReason.Initial));

            Assert.That(result.FailureReason, Is.EqualTo(SpawnFailureReason.InvalidDefinition));
            Assert.That(
                environment.PoolManager.TryGetDiagnostics(
                    poolId,
                    out PoolDiagnostics diagnostics),
                Is.True);
            Assert.That(diagnostics.ActiveCount, Is.Zero);
            Assert.That(diagnostics.CreatedCount, Is.Zero);
        }

        [Test]
        public void SpawnUnit_UnknownDefinitionPoolReturnsClearFailure()
        {
            StepSixUnitPoolSpec knownSpec = new StepSixUnitPoolSpec(
                new PoolId("KnownPool"),
                UnitFaction.Player);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(knownSpec);
            UnitDefinition unknownDefinition =
                _factory.CreateUnitDefinition(
                    new PoolId("MissingPool"),
                    UnitFaction.Player,
                    "UnknownUnit");

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(unknownDefinition, Vector3.zero));

            Assert.That(result.FailureReason, Is.EqualTo(SpawnFailureReason.UnknownPool));
            Assert.That(environment.PoolManager.UnknownPoolFailedRentCount, Is.EqualTo(1));
        }

        [Test]
        public void SpawnUnit_InvalidPoseReturnsInvalidPositionBeforeRent()
        {
            PoolId poolId = new PoolId("PosePool");
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                poolId,
                UnitFaction.Player);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            Vector3 invalidPosition = new Vector3(float.NaN, 0f, 0f);

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(spec.Definition, invalidPosition));

            Assert.That(result.FailureReason, Is.EqualTo(SpawnFailureReason.InvalidPosition));
            Assert.That(
                environment.PoolManager.TryGetDiagnostics(
                    poolId,
                    out PoolDiagnostics diagnostics),
                Is.True);
            Assert.That(diagnostics.CreatedCount, Is.Zero);
        }

        [Test]
        public void SpawnUnit_AssignsPoseContextAndIdentityBeforeActivation()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("ContextPool"),
                UnitFaction.Enemy);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            Vector3 position = new Vector3(3f, 2f, -4f);
            Quaternion rotation = Quaternion.Euler(0f, 75f, 0f);
            SpawnId sourceSpawnId = new SpawnId(99);
            SpawnResult<UnitController> result =
                environment.SpawnManager.SpawnDeathUnit(
                    spec.Definition,
                    new Pose(position, rotation),
                    sourceSpawnId);
            StepSixUnitSpawnProbe probe =
                result.Entity.GetComponent<StepSixUnitSpawnProbe>();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(probe.ContextObservedInactive, Is.True);
            Assert.That(probe.PrepareObservedInactive, Is.True);
            Assert.That(probe.CapturedDefinition, Is.SameAs(spec.Definition));
            Assert.That(probe.CapturedFaction, Is.EqualTo(UnitFaction.Enemy));
            Assert.That(probe.CapturedSpawnId, Is.EqualTo(result.Entity.SpawnId));
            Assert.That(probe.CapturedSpawnId.IsValid, Is.True);
            Assert.That(probe.CapturedSourceSpawnId, Is.EqualTo(sourceSpawnId));
            Assert.That(probe.CapturedReason, Is.EqualTo(SpawnReason.DeathEffect));
            Assert.That(probe.CapturedPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(probe.CapturedRotation, rotation), Is.LessThan(0.001f));
            Assert.That(probe.CompleteObservedLogicalInactive, Is.True);
            Assert.That(probe.CompleteObservedUnregistered, Is.True);
            Assert.That(probe.GameplayActionCount, Is.Zero);
        }

        [Test]
        public void DeathSpawnEntryPoint_RequiresValidSourceIdentity()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("DeathEntryPool"),
                UnitFaction.Enemy);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);

            SpawnResult<UnitController> result =
                environment.SpawnManager.SpawnDeathUnit(
                    spec.Definition,
                    new Pose(Vector3.zero, Quaternion.identity),
                    default);

            Assert.That(result.FailureReason, Is.EqualTo(SpawnFailureReason.InvalidDefinition));
            Assert.That(environment.UnitRegistry.Count, Is.Zero);
        }

        [Test]
        public void SpawnAndReturn_UpdatesRegistryAndDeactivatesInstance()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("RegistryPool"),
                UnitFaction.Ally);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            SpawnResult<UnitController> spawnResult =
                environment.SpawnManager.SpawnUnit(
                    CreateUnitRequest(spec.Definition, Vector3.zero));

            Assert.That(spawnResult.IsSuccess, Is.True);
            Assert.That(environment.UnitRegistry.Count, Is.EqualTo(1));
            Assert.That(environment.UnitRegistry.GetFactionCount(UnitFaction.Ally), Is.EqualTo(1));

            PoolReturnResult returnResult =
                environment.SpawnManager.ReturnUnit(spawnResult.Entity);

            Assert.That(returnResult.IsSuccess, Is.True);
            Assert.That(environment.UnitRegistry.Count, Is.Zero);
            Assert.That(spawnResult.Entity.gameObject.activeInHierarchy, Is.False);
            Assert.That(spawnResult.Entity.SpawnId.IsValid, Is.False);
        }

        [Test]
        public void PoolReturnRequest_ReturnsUnitThroughSpawnManager()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("RequestedReturnPool"),
                UnitFaction.Player);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            UnitController unit = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(spec.Definition, Vector3.zero)).Entity;

            unit.LifecycleController.RequestPoolReturn();

            Assert.That(environment.UnitRegistry.Count, Is.Zero);
            Assert.That(unit.IsActive, Is.False);
            Assert.That(unit.gameObject.activeInHierarchy, Is.False);
            Assert.That(unit.SpawnId.IsValid, Is.False);
        }

        [Test]
        public void Respawn_ReusesInstanceWithNewSpawnIdentityAndContext()
        {
            PoolId poolId = new PoolId("ReusePool");
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                poolId,
                UnitFaction.Player,
                maximumInactiveRetainedCount: 1);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            SpawnResult<UnitController> firstSpawn = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(spec.Definition, Vector3.zero));
            UnitController firstUnit = firstSpawn.Entity;
            SpawnId firstSpawnId = firstUnit.SpawnId;
            Assert.That(environment.SpawnManager.ReturnUnit(firstUnit).IsSuccess, Is.True);

            SpawnResult<UnitController> secondSpawn = environment.SpawnManager.SpawnUnit(
                new UnitSpawnRequest(
                    spec.Definition,
                    Vector3.one,
                    Quaternion.Euler(0f, 180f, 0f),
                    default,
                    SpawnReason.Debug));

            Assert.That(secondSpawn.Entity, Is.SameAs(firstUnit));
            Assert.That(secondSpawn.Entity.SpawnId.IsValid, Is.True);
            Assert.That(secondSpawn.Entity.SpawnId, Is.Not.EqualTo(firstSpawnId));
            Assert.That(secondSpawn.Entity.SpawnId.Value, Is.GreaterThan(firstSpawnId.Value));
            Assert.That(secondSpawn.Entity.transform.position, Is.EqualTo(Vector3.one));
            Assert.That(
                secondSpawn.Entity.GetComponent<StepSixUnitSpawnProbe>().CapturedReason,
                Is.EqualTo(SpawnReason.Debug));
        }

        [Test]
        public void ActivationDependentFailure_ReturnsWithoutRegistration()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("FailedCompletePool"),
                UnitFaction.Enemy,
                failComplete: true);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(spec.Definition, Vector3.zero));
            PooledEntity returnedEntity = environment.PoolManager
                .GetComponentInChildren<PooledEntity>(true);
            UnitController returnedUnit = returnedEntity.GetComponent<UnitController>();

            Assert.That(
                result.FailureReason,
                Is.EqualTo(SpawnFailureReason.ActivationDependentInitializationFailed));
            Assert.That(environment.UnitRegistry.Count, Is.Zero);
            Assert.That(returnedEntity.gameObject.activeInHierarchy, Is.False);
            Assert.That(returnedEntity.IsRented, Is.False);
            Assert.That(returnedUnit.IsActive, Is.False);
            Assert.That(returnedUnit.SpawnId.IsValid, Is.False);
        }

        [Test]
        public void ContextFailure_ReturnsInactiveWithoutRegistration()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("FailedContextPool"),
                UnitFaction.Enemy,
                failContext: true);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(spec.Definition, Vector3.zero));
            PooledEntity returnedEntity = environment.PoolManager
                .GetComponentInChildren<PooledEntity>(true);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(SpawnFailureReason.ActivationIndependentInitializationFailed));
            Assert.That(environment.UnitRegistry.Count, Is.Zero);
            Assert.That(returnedEntity.gameObject.activeInHierarchy, Is.False);
            Assert.That(returnedEntity.IsRented, Is.False);
        }

        [Test]
        public void PositionValidator_RejectsBeforeRent()
        {
            PoolId poolId = new PoolId("RejectedPositionPool");
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                poolId,
                UnitFaction.Enemy);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            StubSpawnPositionValidator validator =
                new StubSpawnPositionValidator(false, default);

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(spec.Definition, Vector3.one),
                validator);

            Assert.That(result.FailureReason, Is.EqualTo(SpawnFailureReason.InvalidPosition));
            Assert.That(validator.CallCount, Is.EqualTo(1));
            Assert.That(
                environment.PoolManager.TryGetDiagnostics(
                    poolId,
                    out PoolDiagnostics diagnostics),
                Is.True);
            Assert.That(diagnostics.CreatedCount, Is.Zero);
        }

        [Test]
        public void PositionValidator_AppliesResolvedPositionBeforeActivation()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("ResolvedPositionPool"),
                UnitFaction.Enemy);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            Vector3 resolvedPosition = new Vector3(8f, 0.5f, -2f);
            StubSpawnPositionValidator validator =
                new StubSpawnPositionValidator(true, resolvedPosition);

            SpawnResult<UnitController> result = environment.SpawnManager.SpawnUnit(
                CreateUnitRequest(spec.Definition, Vector3.one),
                validator);
            StepSixUnitSpawnProbe probe =
                result.Entity.GetComponent<StepSixUnitSpawnProbe>();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Entity.transform.position, Is.EqualTo(resolvedPosition));
            Assert.That(probe.CapturedPosition, Is.EqualTo(resolvedPosition));
            Assert.That(probe.ContextObservedInactive, Is.True);
        }

        [Test]
        public void InitialAndDebugEntryPoints_SetTheirSpawnReasons()
        {
            StepSixUnitPoolSpec spec = new StepSixUnitPoolSpec(
                new PoolId("EntryPointPool"),
                UnitFaction.Player,
                maximumInactiveRetainedCount: 1);
            StepSixSpawnEnvironment environment =
                _factory.CreateUnitEnvironment(spec);
            InitialSandboxSpawner initialSpawner = _factory
                .CreateGameObject("InitialSpawner")
                .AddComponent<InitialSandboxSpawner>();
            DebugUnitSpawner debugSpawner = _factory
                .CreateGameObject("DebugSpawner")
                .AddComponent<DebugUnitSpawner>();
            StepSixSpawnFactory.SetAutoProperty(
                initialSpawner,
                nameof(InitialSandboxSpawner.SpawnManager),
                environment.SpawnManager);
            StepSixSpawnFactory.SetAutoProperty(
                debugSpawner,
                nameof(DebugUnitSpawner.SpawnManager),
                environment.SpawnManager);

            UnitController initialUnit = initialSpawner.Spawn(
                spec.Definition,
                new Pose(Vector3.zero, Quaternion.identity)).Entity;
            Assert.That(
                initialUnit.GetComponent<StepSixUnitSpawnProbe>().CapturedReason,
                Is.EqualTo(SpawnReason.Initial));
            Assert.That(environment.SpawnManager.ReturnUnit(initialUnit).IsSuccess, Is.True);

            UnitController debugUnit = debugSpawner.Spawn(
                spec.Definition,
                new Pose(Vector3.one, Quaternion.identity)).Entity;
            Assert.That(
                debugUnit.GetComponent<StepSixUnitSpawnProbe>().CapturedReason,
                Is.EqualTo(SpawnReason.Debug));
        }

        [Test]
        public void SpawnProjectile_ConfiguresInactiveAndStartsAfterBothPhases()
        {
            PoolId poolId = new PoolId("ProjectilePool");
            StepSixProjectileEnvironment environment =
                _factory.CreateProjectileEnvironment(poolId);
            ProjectileSpawnRequest request = _factory.CreateProjectileRequest(
                environment.Definition,
                new Vector3(2f, 3f, 4f),
                Quaternion.Euler(0f, 30f, 0f));

            SpawnResult<PooledEntity> result =
                environment.SpawnManager.SpawnProjectile(request);
            StepSixProjectileSpawnProbe probe =
                result.Entity.GetComponent<StepSixProjectileSpawnProbe>();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(probe.ConfigurationObservedInactive, Is.True);
            Assert.That(probe.PrepareObservedInactive, Is.True);
            Assert.That(probe.CompleteObservedActiveAndNotStarted, Is.True);
            Assert.That(probe.IsStarted, Is.True);
            Assert.That(probe.StartCount, Is.EqualTo(1));
            Assert.That(probe.CapturedRequest.Definition, Is.SameAs(environment.Definition));
            Assert.That(
                probe.CapturedRequest.DamagePayload.AttackKey,
                Is.EqualTo(request.DamagePayload.AttackKey));
        }

        [Test]
        public void SpawnProjectile_ActivationFailureReturnsWithoutStarting()
        {
            PoolId poolId = new PoolId("FailedProjectilePool");
            StepSixProjectileEnvironment environment =
                _factory.CreateProjectileEnvironment(poolId, failComplete: true);

            SpawnResult<PooledEntity> result = environment.SpawnManager.SpawnProjectile(
                _factory.CreateProjectileRequest(
                    environment.Definition,
                    Vector3.zero,
                    Quaternion.identity));
            PooledEntity returnedEntity = environment.PoolManager
                .GetComponentInChildren<PooledEntity>(true);
            StepSixProjectileSpawnProbe probe =
                returnedEntity.GetComponent<StepSixProjectileSpawnProbe>();

            Assert.That(
                result.FailureReason,
                Is.EqualTo(SpawnFailureReason.ActivationDependentInitializationFailed));
            Assert.That(returnedEntity.gameObject.activeInHierarchy, Is.False);
            Assert.That(returnedEntity.IsRented, Is.False);
            Assert.That(probe.IsStarted, Is.False);
            Assert.That(probe.StartCount, Is.Zero);
        }

        [Test]
        public void SpawnProjectile_StartFailureReturnsInactive()
        {
            PoolId poolId = new PoolId("FailedProjectileStartPool");
            StepSixProjectileEnvironment environment =
                _factory.CreateProjectileEnvironment(poolId, failStart: true);

            SpawnResult<PooledEntity> result = environment.SpawnManager.SpawnProjectile(
                _factory.CreateProjectileRequest(
                    environment.Definition,
                    Vector3.zero,
                    Quaternion.identity));
            PooledEntity returnedEntity = environment.PoolManager
                .GetComponentInChildren<PooledEntity>(true);
            StepSixProjectileSpawnProbe probe =
                returnedEntity.GetComponent<StepSixProjectileSpawnProbe>();

            Assert.That(
                result.FailureReason,
                Is.EqualTo(SpawnFailureReason.ActivationDependentInitializationFailed));
            Assert.That(returnedEntity.gameObject.activeInHierarchy, Is.False);
            Assert.That(returnedEntity.IsRented, Is.False);
            Assert.That(probe.IsStarted, Is.False);
            Assert.That(probe.StartCount, Is.Zero);
        }

        [Test]
        public void SpawnProjectile_MissingLifecycleReturnsControlledFailure()
        {
            PoolId poolId = new PoolId("MissingProjectileLifecyclePool");
            StepSixProjectileEnvironment environment =
                _factory.CreateProjectileEnvironment(
                    poolId,
                    includeLifecycle: false);

            SpawnResult<PooledEntity> result = environment.SpawnManager.SpawnProjectile(
                _factory.CreateProjectileRequest(
                    environment.Definition,
                    Vector3.zero,
                    Quaternion.identity));
            PooledEntity returnedEntity = environment.PoolManager
                .GetComponentInChildren<PooledEntity>(true);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(SpawnFailureReason.ActivationIndependentInitializationFailed));
            Assert.That(returnedEntity.gameObject.activeInHierarchy, Is.False);
            Assert.That(returnedEntity.IsRented, Is.False);
        }

        [Test]
        public void SpawnProjectile_InvalidPayloadDoesNotRent()
        {
            PoolId poolId = new PoolId("InvalidPayloadPool");
            StepSixProjectileEnvironment environment =
                _factory.CreateProjectileEnvironment(poolId);
            ProjectileSpawnRequest request = new ProjectileSpawnRequest(
                environment.Definition,
                default,
                Vector3.zero,
                Quaternion.identity);

            SpawnResult<PooledEntity> result =
                environment.SpawnManager.SpawnProjectile(request);

            Assert.That(result.FailureReason, Is.EqualTo(SpawnFailureReason.InvalidDefinition));
            Assert.That(
                environment.PoolManager.TryGetDiagnostics(
                    poolId,
                    out PoolDiagnostics diagnostics),
                Is.True);
            Assert.That(diagnostics.CreatedCount, Is.Zero);
        }

        [Test]
        public void NavMeshValidator_RequiresExplicitSamplingConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new NavMeshSpawnPositionValidator(0f, -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new NavMeshSpawnPositionValidator(1f, 0));

            NavMeshSpawnPositionValidator validator =
                new NavMeshSpawnPositionValidator(2f, -1);
            Assert.That(validator.MaximumSampleDistance, Is.EqualTo(2f));
            Assert.That(validator.AreaMask, Is.EqualTo(-1));
        }

        private static UnitSpawnRequest CreateUnitRequest(
            UnitDefinition definition,
            Vector3 position)
        {
            return new UnitSpawnRequest(
                definition,
                position,
                Quaternion.identity,
                default,
                SpawnReason.Gameplay);
        }
    }

    public sealed class StepSixSpawnPointGroupTests
    {
        private StepSixSpawnFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new StepSixSpawnFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void SpawnPointGroup_DeterministicLookupUsesAuthoredOrder()
        {
            SpawnPointGroup group = _factory
                .CreateGameObject("SpawnPoints")
                .AddComponent<SpawnPointGroup>();
            Transform first = _factory.CreateGameObject("First").transform;
            Transform second = _factory.CreateGameObject("Second").transform;
            first.SetPositionAndRotation(Vector3.one, Quaternion.identity);
            second.SetPositionAndRotation(
                new Vector3(2f, 0f, 3f),
                Quaternion.Euler(0f, 90f, 0f));
            group.Configure(new[] { first, second });

            Assert.That(group.TryGetPoint(1, out Pose pose), Is.True);
            Assert.That(pose.position, Is.EqualTo(second.position));
            Assert.That(Quaternion.Angle(pose.rotation, second.rotation), Is.LessThan(0.001f));
            Assert.That(group.TryGetPoint(-1, out _), Is.False);
            Assert.That(group.TryGetPoint(2, out _), Is.False);
        }

        [Test]
        public void SpawnPointGroup_RoundRobinIsRepeatableAndResettable()
        {
            SpawnPointGroup group = _factory
                .CreateGameObject("SpawnPoints")
                .AddComponent<SpawnPointGroup>();
            Transform first = _factory.CreateGameObject("First").transform;
            Transform second = _factory.CreateGameObject("Second").transform;
            Transform third = _factory.CreateGameObject("Third").transform;
            first.position = new Vector3(1f, 0f, 0f);
            second.position = new Vector3(2f, 0f, 0f);
            third.position = new Vector3(3f, 0f, 0f);
            group.Configure(new[] { first, second, third });

            Assert.That(group.TryGetNext(out Pose firstPose), Is.True);
            Assert.That(group.TryGetNext(out Pose secondPose), Is.True);
            Assert.That(group.TryGetNext(out Pose thirdPose), Is.True);
            Assert.That(group.TryGetNext(out Pose wrappedPose), Is.True);
            group.ResetRoundRobin();
            Assert.That(group.TryGetNext(out Pose resetPose), Is.True);

            Assert.That(firstPose.position, Is.EqualTo(first.position));
            Assert.That(secondPose.position, Is.EqualTo(second.position));
            Assert.That(thirdPose.position, Is.EqualTo(third.position));
            Assert.That(wrappedPose.position, Is.EqualTo(first.position));
            Assert.That(resetPose.position, Is.EqualTo(first.position));
        }
    }

    internal sealed class StubSpawnPositionValidator : ISpawnPositionValidator
    {
        private readonly bool _isValid;
        private readonly Vector3 _resolvedPosition;

        public int CallCount { get; private set; }

        public StubSpawnPositionValidator(
            bool isValid,
            Vector3 resolvedPosition)
        {
            _isValid = isValid;
            _resolvedPosition = resolvedPosition;
        }

        public bool TryResolvePosition(
            Vector3 requestedPosition,
            out Vector3 resolvedPosition)
        {
            CallCount++;
            resolvedPosition = _resolvedPosition;
            return _isValid;
        }
    }

    internal sealed class StepSixUnitPoolSpec
    {
        public PoolId PoolId { get; }
        public UnitFaction Faction { get; }
        public int MaximumInactiveRetainedCount { get; }
        public bool FailContext { get; }
        public bool FailPrepare { get; }
        public bool FailComplete { get; }
        public UnitDefinition Definition { get; set; }
        public GameObject Prefab { get; set; }

        public StepSixUnitPoolSpec(
            PoolId poolId,
            UnitFaction faction,
            int maximumInactiveRetainedCount = 4,
            bool failContext = false,
            bool failPrepare = false,
            bool failComplete = false)
        {
            PoolId = poolId;
            Faction = faction;
            MaximumInactiveRetainedCount = maximumInactiveRetainedCount;
            FailContext = failContext;
            FailPrepare = failPrepare;
            FailComplete = failComplete;
        }
    }

    internal sealed class StepSixSpawnEnvironment
    {
        public PoolManager PoolManager { get; }
        public UnitRegistry UnitRegistry { get; }
        public SpawnManager SpawnManager { get; }

        public StepSixSpawnEnvironment(
            PoolManager poolManager,
            UnitRegistry unitRegistry,
            SpawnManager spawnManager)
        {
            PoolManager = poolManager;
            UnitRegistry = unitRegistry;
            SpawnManager = spawnManager;
        }
    }

    internal sealed class StepSixProjectileEnvironment
    {
        public PoolManager PoolManager { get; }
        public UnitRegistry UnitRegistry { get; }
        public SpawnManager SpawnManager { get; }
        public ProjectileDefinition Definition { get; }

        public StepSixProjectileEnvironment(
            PoolManager poolManager,
            UnitRegistry unitRegistry,
            SpawnManager spawnManager,
            ProjectileDefinition definition)
        {
            PoolManager = poolManager;
            UnitRegistry = unitRegistry;
            SpawnManager = spawnManager;
            Definition = definition;
        }
    }

    internal sealed class StepSixSpawnFactory : IDisposable
    {
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();

        public StepSixSpawnEnvironment CreateUnitEnvironment(
            params StepSixUnitPoolSpec[] poolSpecs)
        {
            UnitRegistry unitRegistry = CreateGameObject("UnitRegistry")
                .AddComponent<UnitRegistry>();
            PoolCatalogEntry[] entries = new PoolCatalogEntry[poolSpecs.Length];
            for (int specIndex = 0; specIndex < poolSpecs.Length; specIndex++)
            {
                StepSixUnitPoolSpec spec = poolSpecs[specIndex];
                spec.Definition = CreateUnitDefinition(
                    spec.PoolId,
                    spec.Faction,
                    $"Unit{specIndex}");
                spec.Prefab = CreateUnitPrefab(
                    $"UnitPrefab{specIndex}",
                    unitRegistry,
                    spec.FailContext,
                    spec.FailPrepare,
                    spec.FailComplete);
                entries[specIndex] = CreatePoolEntry(
                    spec.PoolId,
                    spec.Prefab,
                    spec.MaximumInactiveRetainedCount);
            }

            PoolManager poolManager = CreatePoolManager(entries);
            SpawnManager spawnManager = CreateSpawnManager(poolManager, unitRegistry);
            return new StepSixSpawnEnvironment(
                poolManager,
                unitRegistry,
                spawnManager);
        }

        public StepSixProjectileEnvironment CreateProjectileEnvironment(
            PoolId poolId,
            bool failComplete = false,
            bool failStart = false,
            bool includeLifecycle = true)
        {
            UnitRegistry unitRegistry = CreateGameObject("UnitRegistry")
                .AddComponent<UnitRegistry>();
            ProjectileDefinition definition = CreateProjectileDefinition(poolId);
            GameObject prefab = CreateProjectilePrefab(
                "ProjectilePrefab",
                failComplete,
                failStart,
                includeLifecycle);
            PoolManager poolManager = CreatePoolManager(
                CreatePoolEntry(poolId, prefab, 4));
            SpawnManager spawnManager = CreateSpawnManager(poolManager, unitRegistry);
            return new StepSixProjectileEnvironment(
                poolManager,
                unitRegistry,
                spawnManager,
                definition);
        }

        public UnitDefinition CreateUnitDefinition(
            PoolId poolId,
            UnitFaction faction,
            string definitionName)
        {
            UnitDefinition definition;
            if (faction == UnitFaction.Player)
            {
                definition = CreateScriptableObject<PlayerUnitDefinition>();
            }
            else
            {
                AttackDefinition attackDefinition =
                    CreateScriptableObject<AttackDefinition>();
                attackDefinition.name = $"{definitionName}Attack";
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.AttackId),
                    new AttackId($"{definitionName}Attack"));
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.Damage),
                    1f);
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.AttackRange),
                    1f);
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.CooldownDuration),
                    1f);
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.WindupDuration),
                    0f);
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.RecoveryDuration),
                    0f);
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.DeliveryType),
                    AttackDeliveryType.Melee);
                SetAutoProperty(
                    attackDefinition,
                    nameof(AttackDefinition.AcceptedHitEffect),
                    new AcceptedHitEffectConfiguration());

                AIUnitDefinition aiDefinition =
                    CreateScriptableObject<AIUnitDefinition>();
                SetAutoProperty(
                    aiDefinition,
                    nameof(AIUnitDefinition.ChaseRange),
                    5f);
                SetAutoProperty(
                    aiDefinition,
                    nameof(AIUnitDefinition.DefaultAttackDefinition),
                    attackDefinition);
                definition = aiDefinition;
            }

            definition.name = definitionName;
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.UnitId),
                new UnitId(definitionName));
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.DisplayName),
                definitionName);
            SetAutoProperty(definition, nameof(UnitDefinition.Faction), faction);
            SetAutoProperty(definition, nameof(UnitDefinition.MaximumHealth), 100f);
            SetAutoProperty(definition, nameof(UnitDefinition.MoveSpeed), 5f);
            SetAutoProperty(definition, nameof(UnitDefinition.TurnSpeed), 360f);
            SetAutoProperty(definition, nameof(UnitDefinition.PoolId), poolId);
            return definition;
        }

        public ProjectileSpawnRequest CreateProjectileRequest(
            ProjectileDefinition definition,
            Vector3 position,
            Quaternion rotation)
        {
            DamagePayload damagePayload = new DamagePayload(
                new SpawnId(50),
                UnitFaction.Player,
                new AttackSequenceId(3),
                10f,
                default);
            return new ProjectileSpawnRequest(
                definition,
                damagePayload,
                position,
                rotation);
        }

        public GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            return gameObject;
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

        public static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            Type currentType = target.GetType();
            string fieldName = $"<{propertyName}>k__BackingField";
            while (currentType != null)
            {
                FieldInfo backingField = currentType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (backingField != null)
                {
                    backingField.SetValue(target, value);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }

        private PoolManager CreatePoolManager(params PoolCatalogEntry[] entries)
        {
            PoolCatalog catalog = CreateScriptableObject<PoolCatalog>();
            catalog.name = "StepSixPoolCatalog";
            FieldInfo entriesField = typeof(PoolCatalog).GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            entriesField.SetValue(catalog, entries);

            PoolManager poolManager = CreateGameObject("PoolManager")
                .AddComponent<PoolManager>();
            Assert.That(poolManager.Initialize(catalog, out string failureMessage),
                Is.True,
                failureMessage);
            return poolManager;
        }

        private SpawnManager CreateSpawnManager(
            PoolManager poolManager,
            UnitRegistry unitRegistry)
        {
            SpawnManager spawnManager = CreateGameObject("SpawnManager")
                .AddComponent<SpawnManager>();
            Assert.That(
                spawnManager.Initialize(
                    poolManager,
                    unitRegistry,
                    out string failureMessage),
                Is.True,
                failureMessage);
            return spawnManager;
        }

        private GameObject CreateUnitPrefab(
            string objectName,
            UnitRegistry unitRegistry,
            bool failContext,
            bool failPrepare,
            bool failComplete)
        {
            GameObject prefab = CreateGameObject(objectName);
            prefab.SetActive(false);
            prefab.AddComponent<HealthController>();
            prefab.AddComponent<StatusEffectController>();
            prefab.AddComponent<DamageController>();
            prefab.AddComponent<UnitLifecycleController>();
            prefab.AddComponent<UnitController>();
            StepSixUnitSpawnProbe probe =
                prefab.AddComponent<StepSixUnitSpawnProbe>();
            SetAutoProperty(
                probe,
                nameof(StepSixUnitSpawnProbe.UnitRegistry),
                unitRegistry);
            SetAutoProperty(
                probe,
                nameof(StepSixUnitSpawnProbe.FailContext),
                failContext);
            SetAutoProperty(
                probe,
                nameof(StepSixUnitSpawnProbe.FailPrepare),
                failPrepare);
            SetAutoProperty(
                probe,
                nameof(StepSixUnitSpawnProbe.FailComplete),
                failComplete);
            prefab.AddComponent<PooledEntity>();
            return prefab;
        }

        private GameObject CreateProjectilePrefab(
            string objectName,
            bool failComplete,
            bool failStart,
            bool includeLifecycle)
        {
            GameObject prefab = CreateGameObject(objectName);
            prefab.SetActive(false);
            if (includeLifecycle)
            {
                StepSixProjectileSpawnProbe probe =
                    prefab.AddComponent<StepSixProjectileSpawnProbe>();
                SetAutoProperty(
                    probe,
                    nameof(StepSixProjectileSpawnProbe.FailComplete),
                    failComplete);
                SetAutoProperty(
                    probe,
                    nameof(StepSixProjectileSpawnProbe.FailStart),
                    failStart);
            }

            prefab.AddComponent<PooledEntity>();
            return prefab;
        }

        private ProjectileDefinition CreateProjectileDefinition(PoolId poolId)
        {
            ProjectileDefinition definition =
                CreateScriptableObject<ProjectileDefinition>();
            definition.name = "StepSixProjectileDefinition";
            SetAutoProperty(definition, nameof(ProjectileDefinition.PoolId), poolId);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.CompatibleDeliveryType),
                AttackDeliveryType.Projectile);
            SetAutoProperty(definition, nameof(ProjectileDefinition.Speed), 10f);
            SetAutoProperty(definition, nameof(ProjectileDefinition.MaximumLifetime), 2f);
            SetAutoProperty(definition, nameof(ProjectileDefinition.CollisionRadius), 0.1f);
            SetAutoProperty(definition, nameof(ProjectileDefinition.GravityScale), 0f);
            SetAutoProperty(definition, nameof(ProjectileDefinition.ExplosionRadius), 0f);
            SetAutoProperty(definition, nameof(ProjectileDefinition.FuseDuration), 0f);
            return definition;
        }

        private PoolCatalogEntry CreatePoolEntry(
            PoolId poolId,
            GameObject prefab,
            int maximumInactiveRetainedCount)
        {
            PoolCatalogEntry entry = new PoolCatalogEntry();
            SetAutoProperty(entry, nameof(PoolCatalogEntry.PoolId), poolId);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.Prefab), prefab);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.InitialPrewarmCount), 0);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                maximumInactiveRetainedCount);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.CapacityPolicy),
                PoolCapacityPolicy.Expandable);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.MaximumActiveCount), 0);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.EnableCollectionChecks), true);
            return entry;
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }
    }
}
