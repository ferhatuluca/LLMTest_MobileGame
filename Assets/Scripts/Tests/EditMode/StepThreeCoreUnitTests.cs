using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Movement;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepThreeCoreUnitTests
    {
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();
        private long _nextAttackSequence = 1;

        [TearDown]
        public void DestroyCreatedObjects()
        {
            for (int objectIndex = _createdObjects.Count - 1; objectIndex >= 0; objectIndex--)
            {
                UnityEngine.Object createdObject = _createdObjects[objectIndex];
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
            _nextAttackSequence = 1;
        }

        [Test]
        public void ValidateCoreFixture_SucceedsWithoutDeferredCapabilities()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);

            bool isValid = fixture.Unit.ValidateCoreComponents(out string failureMessage);

            Assert.That(isValid, Is.True, failureMessage);
            Assert.That(fixture.Unit.UnitMotor, Is.Null);
            Assert.That(fixture.GameObject.GetComponent("TargetingController"), Is.Null);
            Assert.That(fixture.GameObject.GetComponent("AttackController"), Is.Null);
            Assert.That(fixture.GameObject.GetComponent("PooledEntity"), Is.Null);
        }

        [TestCase("Health")]
        [TestCase("Damage")]
        [TestCase("Status")]
        [TestCase("Lifecycle")]
        public void ValidateCoreFixture_RejectsMissingCoreSibling(string omittedComponent)
        {
            GameObject gameObject = CreateGameObject("IncompleteUnit");
            gameObject.SetActive(false);
            if (omittedComponent != "Health")
            {
                gameObject.AddComponent<HealthController>();
            }

            if (omittedComponent != "Damage")
            {
                gameObject.AddComponent<DamageController>();
            }

            if (omittedComponent != "Status")
            {
                gameObject.AddComponent<StatusEffectController>();
            }

            if (omittedComponent != "Lifecycle")
            {
                gameObject.AddComponent<UnitLifecycleController>();
            }

            UnitController unitController = gameObject.AddComponent<UnitController>();

            Assert.That(unitController.ValidateCoreComponents(out string failureMessage), Is.False);
            Assert.That(failureMessage, Does.Contain("requires"));
        }

        [Test]
        public void InitializeSpawn_CopiesDefinitionIdentityWhileRemainingLogicallyInactive()
        {
            PlayerUnitDefinition definition = CreatePlayerDefinition("Player", 125f);
            UnitFixture fixture = CreatePreparedFixture(definition, 42);

            Assert.That(fixture.Unit.Definition, Is.SameAs(definition));
            Assert.That(fixture.Unit.Faction, Is.EqualTo(UnitFaction.Player));
            Assert.That(fixture.Unit.SpawnId, Is.EqualTo(new SpawnId(42)));
            Assert.That(fixture.Health.MaximumHealth, Is.EqualTo(125f));
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(125f));
            Assert.That(fixture.Unit.IsActive, Is.False);

            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);
            Assert.That(fixture.Unit.IsActive, Is.False);
            Assert.That(fixture.Lifecycle.ActivateSpawn(), Is.True);
            Assert.That(fixture.Unit.IsActive, Is.True);
        }

        [Test]
        public void GameObjectActivation_DoesNotImplyLogicalActivation()
        {
            UnitFixture fixture = CreatePreparedFixture(CreatePlayerDefinition("Player", 100f), 1);
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);

            DamageResult result = ApplyDamage(fixture, 10f);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.TargetInactive));
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void SpawnCallback_CannotReturnUnitBeforeActivationReportsSuccess()
        {
            UnitFixture fixture = CreatePreparedFixture(CreatePlayerDefinition("Player", 100f), 1);
            bool requestWasRejected = false;
            fixture.Lifecycle.Spawned += _ =>
            {
                requestWasRejected = Assert.Throws<InvalidOperationException>(
                    () => fixture.Lifecycle.RequestPoolReturn()) != null;
            };
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);

            bool wasActivated = fixture.Lifecycle.ActivateSpawn();

            Assert.That(wasActivated, Is.True);
            Assert.That(requestWasRejected, Is.True);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Active));
            Assert.That(fixture.Unit.IsActive, Is.True);
            Assert.That(fixture.Unit.SpawnId, Is.EqualTo(new SpawnId(1)));
        }

        [Test]
        public void Respawn_ReplacesDefinitionFactionHealthAndSpawnIdentity()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            ApplyDamage(fixture, 25f);
            fixture.Lifecycle.PrepareForReturn();
            fixture.GameObject.SetActive(false);
            AIUnitDefinition enemyDefinition = CreateAIDefinition("Enemy", 240f);

            ActivateExistingFixture(fixture, enemyDefinition, 2);

            Assert.That(fixture.Unit.Definition, Is.SameAs(enemyDefinition));
            Assert.That(fixture.Unit.Faction, Is.EqualTo(UnitFaction.Enemy));
            Assert.That(fixture.Unit.SpawnId, Is.EqualTo(new SpawnId(2)));
            Assert.That(fixture.Health.MaximumHealth, Is.EqualTo(240f));
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(240f));
            Assert.That(fixture.Health.IsAlive, Is.True);
        }

        [Test]
        public void HitContext_TargetType_IsConcreteDamageController()
        {
            PropertyInfo targetProperty = typeof(HitContext).GetProperty(nameof(HitContext.Target));

            Assert.That(targetProperty, Is.Not.Null);
            Assert.That(targetProperty.PropertyType, Is.EqualTo(typeof(DamageController)));
            Assert.That(
                typeof(DamageController).Assembly.GetType(
                    "MonstersVsZombies.Combat.Damage.IDamageReceiver"),
                Is.Null);
        }

        [Test]
        public void HealthController_ExposesNoPublicMutationOrStateEscape()
        {
            Assert.That(
                typeof(HealthController).GetMethod(
                    "ApplyDamage",
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(typeof(HealthController).GetProperty("HealthState"), Is.Null);
            AssertPropertyHasNoPublicSetter<HealthController>(nameof(HealthController.CurrentHealth));
            AssertPropertyHasNoPublicSetter<HealthController>(nameof(HealthController.MaximumHealth));
            AssertPropertyHasNoPublicSetter<HealthController>(nameof(HealthController.IsAlive));
            AssertPropertyHasNoPublicSetter<UnitController>(nameof(UnitController.SpawnId));
            AssertPropertyHasNoPublicSetter<UnitController>(nameof(UnitController.IsActive));
            AssertPropertyHasNoPublicSetter<UnitLifecycleController>(
                nameof(UnitLifecycleController.State));
        }

        [Test]
        public void ApplyDamage_ActiveTarget_ChangesHealthAndReturnsExactResult()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            HealthChangedEvent observedEvent = default;
            int healthEventCount = 0;
            fixture.Health.HealthChanged += healthChangedEvent =>
            {
                observedEvent = healthChangedEvent;
                healthEventCount++;
            };

            DamageResult result = ApplyDamage(fixture, 25f);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Applied));
            Assert.That(result.AppliedAmount, Is.EqualTo(25f));
            Assert.That(result.TargetDied, Is.False);
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(75f));
            Assert.That(healthEventCount, Is.EqualTo(1));
            Assert.That(observedEvent.PreviousHealth, Is.EqualTo(100f));
            Assert.That(observedEvent.CurrentHealth, Is.EqualTo(75f));
        }

        [Test]
        public void ApplyDamage_Overkill_ClampsAndEntersDyingOnce()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            int deathCount = 0;
            int dyingCount = 0;
            bool wasInactiveInsideDyingEvent = false;
            fixture.Health.Died += _ => deathCount++;
            fixture.Lifecycle.Dying += _ =>
            {
                dyingCount++;
                wasInactiveInsideDyingEvent = !fixture.Unit.IsActive;
            };

            DamageResult result = ApplyDamage(fixture, 150f);
            DamageResult laterResult = ApplyDamage(
                fixture,
                10f,
                new StatusEffectPayload(StatusEffectType.Stun, 2f));

            Assert.That(result.AppliedAmount, Is.EqualTo(100f));
            Assert.That(result.TargetDied, Is.True);
            Assert.That(fixture.Health.CurrentHealth, Is.Zero);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Dying));
            Assert.That(deathCount, Is.EqualTo(1));
            Assert.That(dyingCount, Is.EqualTo(1));
            Assert.That(wasInactiveInsideDyingEvent, Is.True);
            Assert.That(laterResult.Outcome, Is.EqualTo(DamageOutcome.TargetDead));
            Assert.That(fixture.StatusEffects.IsStunned, Is.False);
            Assert.That(deathCount, Is.EqualTo(1));
            Assert.That(dyingCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyDamage_InvulnerableOrInvalidAmount_DoesNotChangeState()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            fixture.Damage.SetInvulnerable(true);

            DamageResult invulnerableResult = ApplyDamage(fixture, 10f);
            fixture.Damage.SetInvulnerable(false);
            DamageResult zeroResult = ApplyDamage(fixture, 0f);
            DamageResult negativeResult = ApplyDamage(fixture, -1f);
            DamageResult nanResult = ApplyDamage(fixture, float.NaN);
            DamageResult positiveInfinityResult = ApplyDamage(fixture, float.PositiveInfinity);
            DamageResult negativeInfinityResult = ApplyDamage(fixture, float.NegativeInfinity);

            Assert.That(invulnerableResult.Outcome, Is.EqualTo(DamageOutcome.Invulnerable));
            Assert.That(zeroResult.Outcome, Is.EqualTo(DamageOutcome.InvalidAmount));
            Assert.That(negativeResult.Outcome, Is.EqualTo(DamageOutcome.InvalidAmount));
            Assert.That(nanResult.Outcome, Is.EqualTo(DamageOutcome.InvalidAmount));
            Assert.That(positiveInfinityResult.Outcome, Is.EqualTo(DamageOutcome.InvalidAmount));
            Assert.That(negativeInfinityResult.Outcome, Is.EqualTo(DamageOutcome.InvalidAmount));
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(fixture.StatusEffects.IsStunned, Is.False);
        }

        [Test]
        public void ApplyDamage_SurvivingStunHit_AppliesHealthBeforeStatus()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            List<string> eventOrder = new List<string>();
            fixture.Health.HealthChanged += _ => eventOrder.Add("Health");
            fixture.StatusEffects.StatusEffectChanged += _ => eventOrder.Add("Status");
            fixture.Damage.DamageResolved += _ => eventOrder.Add("DamageResolved");

            DamageResult result = ApplyDamage(
                fixture,
                10f,
                new StatusEffectPayload(StatusEffectType.Stun, 2f));

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.AcceptedStatusEffectCount, Is.EqualTo(1));
            Assert.That(fixture.StatusEffects.IsStunned, Is.True);
            Assert.That(eventOrder, Is.EqualTo(new[] { "Health", "Status", "DamageResolved" }));
        }

        [Test]
        public void ApplyDamage_LethalStunHit_DoesNotApplyStatus()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            int statusEventCount = 0;
            fixture.StatusEffects.StatusEffectChanged += _ => statusEventCount++;

            DamageResult result = ApplyDamage(
                fixture,
                100f,
                new StatusEffectPayload(StatusEffectType.Stun, 2f));

            Assert.That(result.TargetDied, Is.True);
            Assert.That(result.AcceptedStatusEffectCount, Is.Zero);
            Assert.That(fixture.StatusEffects.IsStunned, Is.False);
            Assert.That(statusEventCount, Is.Zero);
        }

        [Test]
        public void ApplyStatusEffect_DeadTargetRejectsDirectStatusRequest()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            ApplyDamage(fixture, 100f);
            int statusEventCount = 0;
            fixture.StatusEffects.StatusEffectChanged += _ => statusEventCount++;

            bool wasAccepted = fixture.StatusEffects.ApplyAcceptedEffect(
                new StatusEffectPayload(StatusEffectType.Stun, 2f));

            Assert.That(wasAccepted, Is.False);
            Assert.That(fixture.StatusEffects.IsStunned, Is.False);
            Assert.That(statusEventCount, Is.Zero);
        }

        [Test]
        public void DamageController_DoesNotEnforceFactionRules()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);

            DamageResult result = ApplyDamage(fixture, 10f, UnitFaction.Player);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void Stun_BlocksActionsRefreshesByMaximumAndExpiresOnce()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            int expiredCount = 0;
            fixture.StatusEffects.StatusEffectChanged += statusEvent =>
            {
                if (!statusEvent.IsActive)
                {
                    expiredCount++;
                }
            };
            ApplyDamage(
                fixture,
                1f,
                new StatusEffectPayload(StatusEffectType.Stun, 2f));

            Assert.That(fixture.StatusEffects.IsMovementBlocked, Is.True);
            Assert.That(fixture.StatusEffects.IsChaseBlocked, Is.True);
            Assert.That(fixture.StatusEffects.IsAttackBlocked, Is.True);
            fixture.StatusEffects.AdvanceTime(0.75f);
            ApplyDamage(
                fixture,
                1f,
                new StatusEffectPayload(StatusEffectType.Stun, 0.5f));
            Assert.That(fixture.StatusEffects.RemainingStunDuration, Is.EqualTo(1.25f));
            ApplyDamage(
                fixture,
                1f,
                new StatusEffectPayload(StatusEffectType.Stun, 3f));
            Assert.That(fixture.StatusEffects.RemainingStunDuration, Is.EqualTo(3f));

            fixture.StatusEffects.AdvanceTime(3f);
            fixture.StatusEffects.AdvanceTime(1f);

            Assert.That(fixture.StatusEffects.IsStunned, Is.False);
            Assert.That(fixture.StatusEffects.IsMovementBlocked, Is.False);
            Assert.That(fixture.StatusEffects.IsChaseBlocked, Is.False);
            Assert.That(fixture.StatusEffects.IsAttackBlocked, Is.False);
            Assert.That(expiredCount, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitPoolCallbacks_OwnResetAndRestoreTransientState()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            ApplyDamage(
                fixture,
                25f,
                new StatusEffectPayload(StatusEffectType.Stun, 2f));
            fixture.Damage.SetInvulnerable(true);

            fixture.GameObject.SetActive(false);
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(75f));
            Assert.That(fixture.StatusEffects.IsStunned, Is.True);

            fixture.Lifecycle.PrepareForReturn();
            fixture.GameObject.SetActive(false);
            ActivateExistingFixture(fixture, CreatePlayerDefinition("Respawned", 100f), 2);

            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(fixture.Health.IsAlive, Is.True);
            Assert.That(fixture.StatusEffects.IsStunned, Is.False);
            Assert.That(fixture.Damage.IsInvulnerable, Is.False);
            Assert.That(fixture.Unit.SpawnId, Is.EqualTo(new SpawnId(2)));
        }

        [Test]
        public void PermanentSiblingSubscription_SurvivesWithoutDuplicationAcrossRespawn()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            int dyingCount = 0;
            fixture.Lifecycle.Dying += _ => dyingCount++;

            ApplyDamage(fixture, 100f);
            fixture.Lifecycle.PrepareForReturn();
            fixture.GameObject.SetActive(false);
            ActivateExistingFixture(fixture, CreatePlayerDefinition("PlayerAgain", 100f), 2);
            ApplyDamage(fixture, 100f);

            Assert.That(dyingCount, Is.EqualTo(2));
        }

        [Test]
        public void PerSpawnSubscription_IsRemovedWhilePermanentObserverSurvives()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            int permanentCount = 0;
            int firstSpawnCount = 0;
            fixture.Health.HealthChanged += _ => permanentCount++;
            Action<HealthChangedEvent> firstSpawnHandler = _ => firstSpawnCount++;
            fixture.Health.HealthChanged += firstSpawnHandler;
            fixture.Lifecycle.RegisterSpawnSubscription(
                () => fixture.Health.HealthChanged -= firstSpawnHandler);

            ApplyDamage(fixture, 10f);
            fixture.Lifecycle.PrepareForReturn();
            fixture.GameObject.SetActive(false);
            ActivateExistingFixture(fixture, CreatePlayerDefinition("PlayerAgain", 100f), 2);
            ApplyDamage(fixture, 10f);

            Assert.That(firstSpawnCount, Is.EqualTo(1));
            Assert.That(permanentCount, Is.EqualTo(2));
        }

        [Test]
        public void LifecycleEvents_ReflectLogicalStateAndDoNotDoubleDespawn()
        {
            UnitFixture fixture = CreatePreparedFixture(CreatePlayerDefinition("Player", 100f), 1);
            List<string> transitions = new List<string>();
            int spawnCount = 0;
            int dyingCount = 0;
            int despawnCount = 0;
            fixture.Lifecycle.StateChanged += lifecycleEvent => transitions.Add(
                $"{lifecycleEvent.PreviousState}->{lifecycleEvent.CurrentState}");
            fixture.Lifecycle.Spawned += _ =>
            {
                spawnCount++;
                Assert.That(fixture.Unit.IsActive, Is.True);
                Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Active));
            };
            fixture.Lifecycle.Dying += _ =>
            {
                dyingCount++;
                Assert.That(fixture.Unit.IsActive, Is.False);
                Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Dying));
            };
            fixture.Lifecycle.Despawned += _ =>
            {
                despawnCount++;
                Assert.That(fixture.Unit.IsActive, Is.False);
            };
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);
            Assert.That(fixture.Lifecycle.ActivateSpawn(), Is.True);

            ApplyDamage(fixture, 100f);
            fixture.Lifecycle.PrepareForReturn();

            Assert.That(spawnCount, Is.EqualTo(1));
            Assert.That(dyingCount, Is.EqualTo(1));
            Assert.That(despawnCount, Is.EqualTo(1));
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Inactive));
            Assert.That(
                transitions,
                Is.EqualTo(new[]
                {
                    "Inactive->Active",
                    "Active->Dying",
                    "Dying->PoolReturn",
                    "PoolReturn->Inactive"
                }));
        }

        [Test]
        public void ImmediateReturnRequestFromDyingListener_DefersBeforePoolCallback()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            int dyingCount = 0;
            int despawnCount = 0;
            int returnRequestCount = 0;
            fixture.Lifecycle.PoolReturnRequested += _ =>
            {
                returnRequestCount++;
                fixture.Lifecycle.PrepareForReturn();
            };
            fixture.Lifecycle.Dying += _ =>
            {
                dyingCount++;
                fixture.Lifecycle.RequestPoolReturn();
            };
            fixture.Lifecycle.Despawned += _ => despawnCount++;

            DamageResult result = ApplyDamage(fixture, 100f);

            Assert.That(result.TargetDied, Is.True);
            Assert.That(dyingCount, Is.EqualTo(1));
            Assert.That(despawnCount, Is.EqualTo(1));
            Assert.That(returnRequestCount, Is.EqualTo(1));
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Inactive));
            Assert.That(fixture.Unit.SpawnId.IsValid, Is.False);
        }

        [Test]
        public void KillWhileStunned_ClearsStatusAfterEnteringDying()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            ApplyDamage(
                fixture,
                1f,
                new StatusEffectPayload(StatusEffectType.Stun, 2f));
            UnitLifecycleState stateObservedDuringClear = default;
            bool wasInactiveDuringClear = false;
            fixture.StatusEffects.StatusEffectChanged += statusEvent =>
            {
                if (!statusEvent.IsActive)
                {
                    stateObservedDuringClear = fixture.Lifecycle.State;
                    wasInactiveDuringClear = !fixture.Unit.IsActive;
                }
            };

            ApplyDamage(fixture, 99f);

            Assert.That(stateObservedDuringClear, Is.EqualTo(UnitLifecycleState.Dying));
            Assert.That(wasInactiveDuringClear, Is.True);
            Assert.That(fixture.StatusEffects.IsStunned, Is.False);
        }

        [Test]
        public void ReturnBeforeLogicalActivation_DoesNotPublishSpawnOrDespawn()
        {
            UnitFixture fixture = CreatePreparedFixture(CreatePlayerDefinition("Player", 100f), 1);
            int spawnCount = 0;
            int despawnCount = 0;
            fixture.Lifecycle.Spawned += _ => spawnCount++;
            fixture.Lifecycle.Despawned += _ => despawnCount++;
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);

            fixture.Lifecycle.PrepareForReturn();

            Assert.That(spawnCount, Is.Zero);
            Assert.That(despawnCount, Is.Zero);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Inactive));
            Assert.That(fixture.Unit.IsActive, Is.False);
        }

        [Test]
        public void ActiveReturn_PublishesOneLogicalDespawnAndEntersInactive()
        {
            UnitFixture fixture = CreateFixture(CreatePlayerDefinition("Player", 100f), 1);
            int despawnCount = 0;
            SpawnId observedSpawnId = default;
            UnitLifecycleState observedDespawnState = default;
            fixture.Lifecycle.Despawned += lifecycleEvent =>
            {
                despawnCount++;
                observedSpawnId = lifecycleEvent.SpawnId;
                observedDespawnState = lifecycleEvent.CurrentState;
                Assert.That(fixture.Unit.IsActive, Is.False);
            };

            fixture.Lifecycle.PrepareForReturn();
            fixture.Lifecycle.PrepareForReturn();

            Assert.That(despawnCount, Is.EqualTo(1));
            Assert.That(observedSpawnId, Is.EqualTo(new SpawnId(1)));
            Assert.That(observedDespawnState, Is.EqualTo(UnitLifecycleState.PoolReturn));
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Inactive));
            Assert.That(fixture.Unit.SpawnId.IsValid, Is.False);
        }

        private UnitFixture CreateFixture(UnitDefinition definition, long spawnId)
        {
            UnitFixture fixture = CreatePreparedFixture(definition, spawnId);
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);
            Assert.That(fixture.Unit.IsActive, Is.False);
            Assert.That(fixture.Lifecycle.ActivateSpawn(), Is.True);
            return fixture;
        }

        private UnitFixture CreatePreparedFixture(UnitDefinition definition, long spawnId)
        {
            GameObject gameObject = CreateGameObject($"Unit_{spawnId}");
            gameObject.SetActive(false);
            HealthController healthController = gameObject.AddComponent<HealthController>();
            StatusEffectController statusEffectController =
                gameObject.AddComponent<StatusEffectController>();
            DamageController damageController = gameObject.AddComponent<DamageController>();
            UnitLifecycleController lifecycleController =
                gameObject.AddComponent<UnitLifecycleController>();
            UnitController unitController = gameObject.AddComponent<UnitController>();
            UnitFixture fixture = new UnitFixture(
                gameObject,
                unitController,
                healthController,
                statusEffectController,
                damageController,
                lifecycleController);

            Assert.That(
                lifecycleController.ConfigureSpawn(definition, new SpawnId(spawnId)),
                Is.True);
            Assert.That(lifecycleController.PrepareForSpawn(), Is.True);
            return fixture;
        }

        private void ActivateExistingFixture(
            UnitFixture fixture,
            UnitDefinition definition,
            long spawnId)
        {
            Assert.That(
                fixture.Lifecycle.ConfigureSpawn(definition, new SpawnId(spawnId)),
                Is.True);
            Assert.That(fixture.Lifecycle.PrepareForSpawn(), Is.True);
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);
            Assert.That(fixture.Lifecycle.ActivateSpawn(), Is.True);
        }

        private DamageResult ApplyDamage(
            UnitFixture fixture,
            float amount,
            params StatusEffectPayload[] statusEffects)
        {
            return ApplyDamage(fixture, amount, UnitFaction.Enemy, statusEffects);
        }

        private DamageResult ApplyDamage(
            UnitFixture fixture,
            float amount,
            UnitFaction sourceFaction,
            params StatusEffectPayload[] statusEffects)
        {
            DamagePayload payload = new DamagePayload(
                new SpawnId(999),
                sourceFaction,
                new AttackSequenceId(_nextAttackSequence++),
                amount,
                default,
                statusEffects);
            HitContext hitContext = new HitContext(
                payload,
                fixture.Damage,
                fixture.GameObject.transform.position,
                Vector3.up,
                HitType.Direct,
                "StepThreeTest");
            return fixture.Damage.ApplyDamage(hitContext);
        }

        private PlayerUnitDefinition CreatePlayerDefinition(string id, float maximumHealth)
        {
            PlayerUnitDefinition definition = CreateScriptableObject<PlayerUnitDefinition>();
            ConfigureUnit(definition, id, UnitFaction.Player, maximumHealth);
            return definition;
        }

        private AIUnitDefinition CreateAIDefinition(string id, float maximumHealth)
        {
            AttackDefinition attackDefinition = CreateScriptableObject<AttackDefinition>();
            SetProperty(attackDefinition, nameof(AttackDefinition.AttackId), new AttackId($"{id}Attack"));
            SetProperty(attackDefinition, nameof(AttackDefinition.Damage), 1f);
            SetProperty(attackDefinition, nameof(AttackDefinition.AttackRange), 1f);
            SetProperty(attackDefinition, nameof(AttackDefinition.CooldownDuration), 1f);
            SetProperty(attackDefinition, nameof(AttackDefinition.WindupDuration), 0f);
            SetProperty(attackDefinition, nameof(AttackDefinition.RecoveryDuration), 0f);
            SetProperty(attackDefinition, nameof(AttackDefinition.DeliveryType), AttackDeliveryType.Melee);
            SetProperty(
                attackDefinition,
                nameof(AttackDefinition.AcceptedHitEffect),
                new AcceptedHitEffectConfiguration());
            AIUnitDefinition definition = CreateScriptableObject<AIUnitDefinition>();
            ConfigureUnit(definition, id, UnitFaction.Enemy, maximumHealth);
            SetProperty(definition, nameof(AIUnitDefinition.ChaseRange), 5f);
            SetProperty(
                definition,
                nameof(AIUnitDefinition.DefaultAttackDefinition),
                attackDefinition);
            return definition;
        }

        private void ConfigureUnit(
            UnitDefinition definition,
            string id,
            UnitFaction faction,
            float maximumHealth)
        {
            SetProperty(definition, nameof(UnitDefinition.UnitId), new UnitId(id));
            SetProperty(definition, nameof(UnitDefinition.DisplayName), id);
            SetProperty(definition, nameof(UnitDefinition.Faction), faction);
            SetProperty(definition, nameof(UnitDefinition.MaximumHealth), maximumHealth);
            SetProperty(definition, nameof(UnitDefinition.MoveSpeed), 5f);
            SetProperty(definition, nameof(UnitDefinition.TurnSpeed), 360f);
            SetProperty(definition, nameof(UnitDefinition.PoolId), new PoolId($"{id}Pool"));
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private GameObject CreateGameObject(string objectName)
        {
            GameObject instance = new GameObject(objectName);
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            Type type = target.GetType();
            string fieldName = $"<{propertyName}>k__BackingField";
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }

        private static void AssertPropertyHasNoPublicSetter<T>(string propertyName)
        {
            PropertyInfo property = typeof(T).GetProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.SetMethod == null || !property.SetMethod.IsPublic, Is.True);
        }

        private sealed class UnitFixture
        {
            public GameObject GameObject { get; }
            public UnitController Unit { get; }
            public HealthController Health { get; }
            public StatusEffectController StatusEffects { get; }
            public DamageController Damage { get; }
            public UnitLifecycleController Lifecycle { get; }

            public UnitFixture(
                GameObject gameObject,
                UnitController unit,
                HealthController health,
                StatusEffectController statusEffects,
                DamageController damage,
                UnitLifecycleController lifecycle)
            {
                GameObject = gameObject;
                Unit = unit;
                Health = health;
                StatusEffects = statusEffects;
                Damage = damage;
                Lifecycle = lifecycle;
            }
        }
    }
}
