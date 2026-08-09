using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSevenTargetingTests
    {
        private StepSevenTestFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new StepSevenTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void CombatRangeRules_UsesInclusiveSquaredXZDistance()
        {
            Vector3 source = new Vector3(0f, -100f, 0f);
            Vector3 boundary = new Vector3(3f, 100f, 4f);
            Vector3 outside = new Vector3(3.001f, 100f, 4f);

            Assert.That(
                CombatRangeRules.GetSquaredPlanarDistance(source, boundary),
                Is.EqualTo(25f));
            Assert.That(CombatRangeRules.IsWithinRange(source, boundary, 5f), Is.True);
            Assert.That(CombatRangeRules.IsWithinRange(source, outside, 5f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CombatRangeRules.IsWithinRange(source, boundary, float.NaN));
        }

        [Test]
        public void ForceScan_SelectsNearestHostileTarget()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 5f);
            _factory.CreateUnit(
                2,
                UnitFaction.Enemy,
                new Vector3(3f, 0f, 0f));
            StepSevenUnitFixture nearest = _factory.CreateUnit(
                3,
                UnitFaction.Enemy,
                new Vector3(1f, 0f, 0f));

            Physics.SyncTransforms();
            Assert.That(attacker.Targeting.ForceScan(), Is.True);

            Assert.That(attacker.Targeting.CurrentTarget, Is.SameAs(nearest.Unit));
        }

        [Test]
        public void ForceScan_IgnoresDeadInactiveFriendlyAndOutOfRangeCandidates()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 3f);
            _factory.CreateUnit(
                2,
                UnitFaction.Ally,
                new Vector3(0.25f, 0f, 0f));
            _factory.CreateUnit(
                3,
                UnitFaction.Enemy,
                new Vector3(0.5f, 0f, 0f),
                activateLogically: false);
            StepSevenUnitFixture dead = _factory.CreateUnit(
                4,
                UnitFaction.Enemy,
                new Vector3(0.75f, 0f, 0f));
            dead.Health.ApplyDamage(dead.Health.MaximumHealth);
            _factory.CreateUnit(
                5,
                UnitFaction.Enemy,
                new Vector3(5f, 0f, 0f));
            StepSevenUnitFixture valid = _factory.CreateUnit(
                6,
                UnitFaction.Enemy,
                new Vector3(2f, 0f, 0f));

            Physics.SyncTransforms();
            Assert.That(attacker.Targeting.ForceScan(), Is.True);

            Assert.That(attacker.Targeting.CurrentTarget, Is.SameAs(valid.Unit));
        }

        [Test]
        public void ForceScan_EqualDistanceUsesLowestSpawnId()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 4f);
            _factory.CreateUnit(
                20,
                UnitFaction.Enemy,
                new Vector3(-1f, 0f, 0f));
            StepSevenUnitFixture lowerIdentity = _factory.CreateUnit(
                10,
                UnitFaction.Enemy,
                new Vector3(1f, 0f, 0f));

            Physics.SyncTransforms();
            attacker.Targeting.ForceScan();

            Assert.That(attacker.Targeting.CurrentTarget, Is.SameAs(lowerIdentity.Unit));
        }

        [Test]
        public void ForceScan_DeduplicatesMultipleHurtboxesBySpawnId()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 4f);
            StepSevenUnitFixture target = _factory.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.one);
            _factory.AddHurtbox(target, "SecondHurtbox", Vector3.right * 0.1f);

            Physics.SyncTransforms();
            attacker.Targeting.ForceScan();

            Assert.That(attacker.Targeting.CurrentTarget, Is.SameAs(target.Unit));
            Assert.That(attacker.Targeting.LastUniqueCandidateCount, Is.EqualTo(1));
        }

        [Test]
        public void CurrentTarget_DeathClearsImmediatelyAndPublishesLoss()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 4f);
            StepSevenUnitFixture target = _factory.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right);
            int acquiredCount = 0;
            int lostCount = 0;
            attacker.Targeting.TargetAcquired += _ => acquiredCount++;
            attacker.Targeting.TargetLost += _ => lostCount++;
            Physics.SyncTransforms();
            attacker.Targeting.ForceScan();

            target.Health.ApplyDamage(target.Health.MaximumHealth);

            Assert.That(acquiredCount, Is.EqualTo(1));
            Assert.That(lostCount, Is.EqualTo(1));
            Assert.That(attacker.Targeting.CurrentTarget, Is.Null);
        }

        [Test]
        public void SourceDeath_ClearsCurrentTargetAndSubscriptionImmediately()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 4f);
            _factory.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right);
            int lostCount = 0;
            attacker.Targeting.TargetLost += _ => lostCount++;
            Physics.SyncTransforms();
            attacker.Targeting.ForceScan();

            attacker.Health.ApplyDamage(attacker.Health.MaximumHealth);

            Assert.That(attacker.Targeting.CurrentTarget, Is.Null);
            Assert.That(lostCount, Is.EqualTo(1));
        }

        [Test]
        public void CheapValidation_ClearsOutOfRangeTargetBetweenFullScans()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 3f,
                scanInterval: 10f);
            StepSevenUnitFixture target = _factory.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right);
            Physics.SyncTransforms();
            attacker.Targeting.ForceScan();
            target.GameObject.transform.position = new Vector3(4f, 0f, 0f);
            Physics.SyncTransforms();

            attacker.Targeting.AdvanceTime(0f);

            Assert.That(attacker.Targeting.CurrentTarget, Is.Null);
        }

        [Test]
        public void StaggeredScan_WaitsForExplicitInitialDelay()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 3f,
                scanInterval: 1f,
                initialScanDelay: 0.5f);
            StepSevenUnitFixture target = _factory.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right);
            Physics.SyncTransforms();

            attacker.Targeting.AdvanceTime(0.49f);
            Assert.That(attacker.Targeting.CurrentTarget, Is.Null);
            attacker.Targeting.AdvanceTime(0.01f);

            Assert.That(attacker.Targeting.CurrentTarget, Is.SameAs(target.Unit));
        }

        [Test]
        public void PlayerTargeting_NeverRequestsMovementOrFacing()
        {
            StepSevenUnitFixture attacker = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero,
                attackRange: 3f,
                includeMotor: true);
            _factory.CreateUnit(
                2,
                UnitFaction.Enemy,
                Vector3.right);
            Physics.SyncTransforms();

            attacker.Targeting.ForceScan();
            attacker.Targeting.AdvanceTime(1f);

            Assert.That(attacker.Motor.MoveRequestCount, Is.Zero);
            Assert.That(attacker.Motor.FaceRequestCount, Is.Zero);
        }

        [Test]
        public void AIUsesChaseRangeWhilePlayerUsesAttackRangeOnly()
        {
            StepSevenUnitFixture aiAttacker = _factory.CreateUnit(
                1,
                UnitFaction.Ally,
                Vector3.zero,
                attackRange: 2f,
                chaseRange: 5f);
            StepSevenUnitFixture playerAttacker = _factory.CreateUnit(
                2,
                UnitFaction.Player,
                new Vector3(0f, 0f, 10f),
                attackRange: 2f);
            StepSevenUnitFixture aiTarget = _factory.CreateUnit(
                3,
                UnitFaction.Enemy,
                new Vector3(4f, 0f, 0f));
            _factory.CreateUnit(
                4,
                UnitFaction.Enemy,
                new Vector3(4f, 0f, 10f));
            Physics.SyncTransforms();

            Assert.That(aiAttacker.Targeting.ForceScan(), Is.True);
            Assert.That(playerAttacker.Targeting.ForceScan(), Is.False);

            Assert.That(aiAttacker.Targeting.CurrentTarget, Is.SameAs(aiTarget.Unit));
            Assert.That(aiAttacker.Targeting.Mode, Is.EqualTo(TargetingMode.AIChaseRange));
            Assert.That(playerAttacker.Targeting.Mode, Is.EqualTo(TargetingMode.PlayerAttackRange));
        }

        [Test]
        public void UnitController_CachesAndValidatesCompletedGameplayComponents()
        {
            StepSevenUnitFixture fixture = _factory.CreateUnit(
                1,
                UnitFaction.Player,
                Vector3.zero);

            Assert.That(fixture.Unit.ValidateGameplayComponents(out string failure),
                Is.True,
                failure);
            Assert.That(fixture.Unit.TargetingController, Is.SameAs(fixture.Targeting));
            Assert.That(fixture.Unit.AttackController, Is.SameAs(fixture.Attack));
        }

        [Test]
        public void TargetingInitialization_RejectsInvalidCapacityAndIsRequiredByUnitValidation()
        {
            GameObject gameObject = _factory.CreateGameObject("IncompleteGameplayUnit");
            gameObject.SetActive(false);
            gameObject.AddComponent<HealthController>();
            gameObject.AddComponent<StatusEffectController>();
            gameObject.AddComponent<DamageController>();
            gameObject.AddComponent<UnitLifecycleController>();
            UnitController unitController = gameObject.AddComponent<UnitController>();
            TargetingController targetingController =
                gameObject.AddComponent<TargetingController>();
            gameObject.AddComponent<AttackController>();

            Assert.That(
                targetingController.InitializeScanning(0, 1f, 0f),
                Is.False);
            Assert.That(
                unitController.ValidateGameplayComponents(out string failureMessage),
                Is.False);
            Assert.That(failureMessage, Does.Contain("TargetingController"));
        }
    }

    public sealed class StepSevenAttackTimingTests
    {
        private StepSevenTestFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new StepSevenTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void AttackSequence_ImpactsOnceAndFeedsSuccessfulPolicy()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                cooldown: 2f,
                windup: 0.5f,
                recovery: 1f);
            float startingHealth = pair.Target.Health.CurrentHealth;

            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);
            AttackKey attackKey = pair.Attacker.Attack.ActiveAttackKey;
            pair.Attacker.Attack.AdvanceTime(0.5f);
            pair.Attacker.AnimationRelay.RequestImpact();

            Assert.That(attackKey.IsValid, Is.True);
            Assert.That(pair.Attacker.Executor.ExecutionCount, Is.EqualTo(1));
            Assert.That(pair.Target.Health.CurrentHealth, Is.EqualTo(startingHealth - 10f));
            Assert.That(pair.Attacker.Policy.SuccessfulInteractionCount, Is.EqualTo(1));
            Assert.That(pair.Attacker.Attack.HitLedger.AcceptedTargetCount, Is.EqualTo(1));
            Assert.That(pair.Attacker.Attack.RequestImpact(), Is.False);
            Assert.That(pair.Attacker.Executor.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void CooldownAndRecovery_BothGateNextAttackStart()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                cooldown: 1f,
                windup: 0.5f,
                recovery: 1.5f);

            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);
            pair.Attacker.Attack.AdvanceTime(0.5f);
            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Recovery));
            pair.Attacker.Attack.AdvanceTime(0.5f);
            Assert.That(pair.Attacker.Attack.CooldownRemaining, Is.Zero);
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.False);
            pair.Attacker.Attack.AdvanceTime(1f);

            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Idle));
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);
            Assert.That(
                pair.Attacker.Attack.ActiveAttackKey.SequenceId,
                Is.EqualTo(new AttackSequenceId(2)));
        }

        [Test]
        public void StunCancellation_ClearsSequenceButKeepsCommittedCooldown()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                cooldown: 2f,
                windup: 1f,
                recovery: 0f);
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);

            Assert.That(
                pair.Attacker.StatusEffects.ApplyAcceptedEffect(
                    new StatusEffectPayload(StatusEffectType.Stun, 1f)),
                Is.True);

            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Idle));
            Assert.That(pair.Attacker.Attack.HasActiveSequence, Is.False);
            Assert.That(pair.Attacker.Attack.HitLedger.IsActive, Is.False);
            Assert.That(pair.Attacker.Attack.CooldownRemaining, Is.EqualTo(2f));
            Assert.That(pair.Attacker.Executor.ExecutionCount, Is.Zero);
            pair.Attacker.StatusEffects.AdvanceTime(1f);
            pair.Attacker.Attack.AdvanceTime(2f);
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);
        }

        [Test]
        public void TargetDespawn_CancelsWindupWithoutImpact()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                cooldown: 2f,
                windup: 1f,
                recovery: 0f);
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);

            pair.Target.Lifecycle.PrepareForReturn();

            Assert.That(pair.Attacker.Targeting.CurrentTarget, Is.Null);
            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Idle));
            Assert.That(pair.Attacker.Attack.CooldownRemaining, Is.EqualTo(2f));
            Assert.That(pair.Attacker.Executor.ExecutionCount, Is.Zero);
        }

        [Test]
        public void TargetLeavesAttackRangeWithinAIChaseRange_CancelsOnCheapCheck()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                attackerFaction: UnitFaction.Ally,
                attackRange: 2f,
                chaseRange: 5f,
                cooldown: 2f,
                windup: 1f,
                recovery: 0f);
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);
            pair.Target.GameObject.transform.position = new Vector3(3f, 0f, 0f);
            Physics.SyncTransforms();

            pair.Attacker.Attack.AdvanceTime(0f);

            Assert.That(pair.Attacker.Targeting.CurrentTarget, Is.SameAs(pair.Target.Unit));
            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Idle));
            Assert.That(pair.Attacker.Executor.ExecutionCount, Is.Zero);
        }

        [Test]
        public void AttackerDeath_ResetsAllCommittedTiming()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                cooldown: 2f,
                windup: 1f,
                recovery: 0f);
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);

            pair.Attacker.Health.ApplyDamage(pair.Attacker.Health.MaximumHealth);

            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Idle));
            Assert.That(pair.Attacker.Attack.CooldownRemaining, Is.Zero);
            Assert.That(pair.Attacker.Attack.HasActiveSequence, Is.False);
            Assert.That(pair.Attacker.Executor.ExecutionCount, Is.Zero);
        }

        [Test]
        public void PoolReturn_ResetsAllTimingAndSequenceCounter()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                cooldown: 2f,
                windup: 1f,
                recovery: 0f);
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);

            pair.Attacker.Attack.PrepareForReturn();

            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Idle));
            Assert.That(pair.Attacker.Attack.CooldownRemaining, Is.Zero);
            Assert.That(pair.Attacker.Attack.HasActiveSequence, Is.False);
            Assert.That(pair.Attacker.Attack.HitLedger.IsActive, Is.False);
        }

        [Test]
        public void MeleeAnimationImpact_RechecksPlanarRangeBeforeExecution()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                attackRange: 2f,
                cooldown: 2f,
                windup: 1f,
                recovery: 1f);
            float startingHealth = pair.Target.Health.CurrentHealth;
            Assert.That(pair.Attacker.Attack.TryStartAttack(), Is.True);
            pair.Target.GameObject.transform.position = new Vector3(3f, 100f, 0f);
            Physics.SyncTransforms();

            pair.Attacker.AnimationRelay.RequestImpact();

            Assert.That(pair.Attacker.Executor.ExecutionCount, Is.Zero);
            Assert.That(pair.Target.Health.CurrentHealth, Is.EqualTo(startingHealth));
            Assert.That(pair.Attacker.Attack.State, Is.EqualTo(AttackTimingState.Recovery));
            Assert.That(pair.Attacker.Attack.CooldownRemaining, Is.EqualTo(2f));
        }

        [Test]
        public void ExecutorBindings_RejectMissingDuplicateAndIncompatibleEntries()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair();
            AttackController attackController = pair.Attacker.Attack;

            attackController.ConfigureBindings(pair.Attacker.AttackDefinition, Array.Empty<AttackExecutorBinding>());
            Assert.That(attackController.ValidateConfiguration(out _), Is.False);

            AttackExecutorBinding binding = new AttackExecutorBinding(
                AttackDeliveryType.Melee,
                pair.Attacker.Executor);
            attackController.ConfigureBindings(
                pair.Attacker.AttackDefinition,
                new[] { binding, binding });
            Assert.That(attackController.ValidateConfiguration(out _), Is.False);

            StepSevenAttackExecutor incompatibleExecutor = pair.Attacker.GameObject
                .AddComponent<StepSevenAttackExecutor>();
            StepSevenTestFactory.SetAutoProperty(
                incompatibleExecutor,
                nameof(StepSevenAttackExecutor.DeliveryType),
                AttackDeliveryType.Hitscan);
            attackController.ConfigureBindings(
                pair.Attacker.AttackDefinition,
                new[]
                {
                    new AttackExecutorBinding(
                        AttackDeliveryType.Melee,
                        incompatibleExecutor)
                });
            Assert.That(attackController.ValidateConfiguration(out _), Is.False);
        }

        [Test]
        public void PlayerBindings_SwitchProjectileGrenadeAndHitscanWithoutReplacingController()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair();
            AttackController attackController = pair.Attacker.Attack;
            AttackDefinition projectileDefinition = _factory.CreateDeliveryAttackDefinition(
                "PlayerProjectile",
                AttackDeliveryType.Projectile);
            AttackDefinition grenadeDefinition = _factory.CreateDeliveryAttackDefinition(
                "PlayerGrenade",
                AttackDeliveryType.Grenade);
            AttackDefinition hitscanDefinition = _factory.CreateDeliveryAttackDefinition(
                "PlayerHitscan",
                AttackDeliveryType.Hitscan);
            StepSevenAttackExecutor projectileExecutor = _factory.CreateExecutor(
                pair.Attacker.GameObject,
                AttackDeliveryType.Projectile);
            StepSevenAttackExecutor grenadeExecutor = _factory.CreateExecutor(
                pair.Attacker.GameObject,
                AttackDeliveryType.Grenade);
            StepSevenAttackExecutor hitscanExecutor = _factory.CreateExecutor(
                pair.Attacker.GameObject,
                AttackDeliveryType.Hitscan);
            attackController.ConfigureBindings(
                projectileDefinition,
                new[]
                {
                    new AttackExecutorBinding(
                        AttackDeliveryType.Projectile,
                        projectileExecutor),
                    new AttackExecutorBinding(
                        AttackDeliveryType.Grenade,
                        grenadeExecutor),
                    new AttackExecutorBinding(
                        AttackDeliveryType.Hitscan,
                        hitscanExecutor)
                });

            Assert.That(attackController.ValidateConfiguration(out _), Is.True);
            Assert.That(attackController.SetAttackDefinition(projectileDefinition), Is.True);
            Assert.That(attackController.SetAttackDefinition(grenadeDefinition), Is.True);
            Assert.That(attackController.SetAttackDefinition(hitscanDefinition), Is.True);
            Assert.That(pair.Attacker.Unit.AttackController, Is.SameAs(attackController));
            Assert.That(attackController.AttackDefinition, Is.SameAs(hitscanDefinition));
        }

        [Test]
        public void FixedAI_RejectsAdditionalExecutorBindings()
        {
            StepSevenCombatPair pair = _factory.CreateCombatPair(
                attackerFaction: UnitFaction.Ally);
            StepSevenAttackExecutor hitscanExecutor = _factory.CreateExecutor(
                pair.Attacker.GameObject,
                AttackDeliveryType.Hitscan);
            pair.Attacker.Attack.ConfigureBindings(
                pair.Attacker.AttackDefinition,
                new[]
                {
                    new AttackExecutorBinding(
                        AttackDeliveryType.Melee,
                        pair.Attacker.Executor),
                    new AttackExecutorBinding(
                        AttackDeliveryType.Hitscan,
                        hitscanExecutor)
                });

            Assert.That(
                pair.Attacker.Attack.ValidateConfiguration(out string failureMessage),
                Is.False);
            Assert.That(failureMessage, Does.Contain("fixed AI"));
        }
    }

    internal sealed class StepSevenCombatPair
    {
        public StepSevenUnitFixture Attacker { get; }
        public StepSevenUnitFixture Target { get; }

        public StepSevenCombatPair(
            StepSevenUnitFixture attacker,
            StepSevenUnitFixture target)
        {
            Attacker = attacker;
            Target = target;
        }
    }

    internal sealed class StepSevenUnitFixture
    {
        public GameObject GameObject { get; }
        public UnitController Unit { get; }
        public HealthController Health { get; }
        public StatusEffectController StatusEffects { get; }
        public UnitLifecycleController Lifecycle { get; }
        public TargetingController Targeting { get; }
        public AttackController Attack { get; }
        public AttackDefinition AttackDefinition { get; }
        public StepSevenAttackExecutor Executor { get; }
        public StepSevenAttackResultPolicy Policy { get; }
        public StepSevenMotorProbe Motor { get; }
        public AttackAnimationEventRelay AnimationRelay { get; }

        public StepSevenUnitFixture(
            GameObject gameObject,
            UnitController unit,
            HealthController health,
            StatusEffectController statusEffects,
            UnitLifecycleController lifecycle,
            TargetingController targeting,
            AttackController attack,
            AttackDefinition attackDefinition,
            StepSevenAttackExecutor executor,
            StepSevenAttackResultPolicy policy,
            StepSevenMotorProbe motor,
            AttackAnimationEventRelay animationRelay)
        {
            GameObject = gameObject;
            Unit = unit;
            Health = health;
            StatusEffects = statusEffects;
            Lifecycle = lifecycle;
            Targeting = targeting;
            Attack = attack;
            AttackDefinition = attackDefinition;
            Executor = executor;
            Policy = policy;
            Motor = motor;
            AnimationRelay = animationRelay;
        }
    }

    internal sealed class StepSevenTestFactory : IDisposable
    {
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();
        private readonly InteractionSystem _interactionSystem;

        public StepSevenTestFactory()
        {
            _interactionSystem = CreateGameObject("InteractionSystem")
                .AddComponent<InteractionSystem>();
        }

        public StepSevenCombatPair CreateCombatPair(
            UnitFaction attackerFaction = UnitFaction.Player,
            float attackRange = 3f,
            float chaseRange = 5f,
            float cooldown = 2f,
            float windup = 1f,
            float recovery = 1f)
        {
            StepSevenUnitFixture attacker = CreateUnit(
                1,
                attackerFaction,
                Vector3.zero,
                attackRange: attackRange,
                chaseRange: chaseRange,
                cooldown: cooldown,
                windup: windup,
                recovery: recovery);
            UnitFaction targetFaction = attackerFaction == UnitFaction.Enemy
                ? UnitFaction.Player
                : UnitFaction.Enemy;
            StepSevenUnitFixture target = CreateUnit(
                2,
                targetFaction,
                Vector3.right,
                attackRange: attackRange,
                chaseRange: chaseRange);
            Physics.SyncTransforms();
            Assert.That(attacker.Targeting.ForceScan(), Is.True);
            Assert.That(attacker.Targeting.CurrentTarget, Is.SameAs(target.Unit));
            return new StepSevenCombatPair(attacker, target);
        }

        public StepSevenUnitFixture CreateUnit(
            long spawnId,
            UnitFaction faction,
            Vector3 position,
            bool activateLogically = true,
            float attackRange = 3f,
            float chaseRange = 5f,
            float cooldown = 1f,
            float windup = 0.5f,
            float recovery = 0.5f,
            float scanInterval = 1f,
            float initialScanDelay = 0f,
            bool includeMotor = false)
        {
            AttackDefinition attackDefinition = CreateAttackDefinition(
                $"Attack{spawnId}",
                AttackDeliveryType.Melee,
                attackRange,
                cooldown,
                windup,
                recovery);
            UnitDefinition unitDefinition = CreateUnitDefinition(
                $"Unit{spawnId}",
                faction,
                attackDefinition,
                chaseRange);

            GameObject gameObject = CreateGameObject($"Unit{spawnId}");
            gameObject.SetActive(false);
            gameObject.transform.position = position;
            HealthController healthController =
                gameObject.AddComponent<HealthController>();
            StatusEffectController statusEffectController =
                gameObject.AddComponent<StatusEffectController>();
            gameObject.AddComponent<DamageController>();
            UnitLifecycleController lifecycleController =
                gameObject.AddComponent<UnitLifecycleController>();
            UnitController unitController = gameObject.AddComponent<UnitController>();
            TargetingController targetingController =
                gameObject.AddComponent<TargetingController>();
            Assert.That(
                targetingController.InitializeScanning(
                    16,
                    scanInterval,
                    initialScanDelay),
                Is.True);
            StepSevenMotorProbe motor = includeMotor
                ? gameObject.AddComponent<StepSevenMotorProbe>()
                : null;
            StepSevenAttackExecutor executor =
                gameObject.AddComponent<StepSevenAttackExecutor>();
            SetAutoProperty(
                executor,
                nameof(StepSevenAttackExecutor.DeliveryType),
                AttackDeliveryType.Melee);
            SetAutoProperty(
                executor,
                nameof(StepSevenAttackExecutor.InteractionSystem),
                _interactionSystem);
            StepSevenAttackResultPolicy policy =
                gameObject.AddComponent<StepSevenAttackResultPolicy>();
            AttackController attackController =
                gameObject.AddComponent<AttackController>();
            attackController.ConfigureBindings(
                attackDefinition,
                new[]
                {
                    new AttackExecutorBinding(
                        AttackDeliveryType.Melee,
                        executor)
                });
            AttackAnimationEventRelay animationRelay =
                gameObject.AddComponent<AttackAnimationEventRelay>();
            StepSevenUnitFixture fixture = new StepSevenUnitFixture(
                gameObject,
                unitController,
                healthController,
                statusEffectController,
                lifecycleController,
                targetingController,
                attackController,
                attackDefinition,
                executor,
                policy,
                motor,
                animationRelay);
            AddHurtbox(fixture, "Hurtbox", Vector3.zero);

            Assert.That(
                lifecycleController.ConfigureSpawn(
                    unitDefinition,
                    new SpawnId(spawnId)),
                Is.True);
            Assert.That(lifecycleController.PrepareForSpawn(), Is.True);
            Assert.That(targetingController.PrepareForSpawn(), Is.True);
            Assert.That(attackController.PrepareForSpawn(), Is.True);
            gameObject.SetActive(true);
            Assert.That(lifecycleController.CompleteSpawn(), Is.True);
            Assert.That(targetingController.CompleteSpawn(), Is.True);
            Assert.That(attackController.CompleteSpawn(), Is.True);
            if (activateLogically)
            {
                Assert.That(lifecycleController.ActivateSpawn(), Is.True);
            }

            return fixture;
        }

        public DamageTargetProxy AddHurtbox(
            StepSevenUnitFixture fixture,
            string objectName,
            Vector3 localPosition)
        {
            GameObject hurtbox = CreateGameObject(objectName);
            hurtbox.transform.SetParent(fixture.GameObject.transform, false);
            hurtbox.transform.localPosition = localPosition;
            hurtbox.layer = LayerMask.NameToLayer("UnitTarget");
            SphereCollider collider = hurtbox.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.2f;
            DamageTargetProxy targetProxy =
                hurtbox.AddComponent<DamageTargetProxy>();
            targetProxy.CacheOwnerReferences();
            return targetProxy;
        }

        public GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        public StepSevenAttackExecutor CreateExecutor(
            GameObject gameObject,
            AttackDeliveryType deliveryType)
        {
            StepSevenAttackExecutor executor =
                gameObject.AddComponent<StepSevenAttackExecutor>();
            SetAutoProperty(
                executor,
                nameof(StepSevenAttackExecutor.DeliveryType),
                deliveryType);
            SetAutoProperty(
                executor,
                nameof(StepSevenAttackExecutor.InteractionSystem),
                _interactionSystem);
            return executor;
        }

        public AttackDefinition CreateDeliveryAttackDefinition(
            string definitionName,
            AttackDeliveryType deliveryType)
        {
            AttackDefinition definition = CreateAttackDefinition(
                definitionName,
                deliveryType,
                3f,
                1f,
                0.5f,
                0.5f);
            if (deliveryType == AttackDeliveryType.Projectile ||
                deliveryType == AttackDeliveryType.Grenade)
            {
                ProjectileDefinition projectileDefinition =
                    CreateScriptableObject<ProjectileDefinition>();
                projectileDefinition.name = $"{definitionName}Projectile";
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.PoolId),
                    new PoolId($"{definitionName}Pool"));
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.CompatibleDeliveryType),
                    deliveryType);
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.Speed),
                    10f);
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.CollisionRadius),
                    0.1f);
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.GravityScale),
                    deliveryType == AttackDeliveryType.Grenade ? 1f : 0f);
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.MaximumLifetime),
                    deliveryType == AttackDeliveryType.Projectile ? 2f : 0f);
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.ExplosionRadius),
                    deliveryType == AttackDeliveryType.Grenade ? 2f : 0f);
                SetAutoProperty(
                    projectileDefinition,
                    nameof(ProjectileDefinition.FuseDuration),
                    deliveryType == AttackDeliveryType.Grenade ? 1f : 0f);
                SetAutoProperty(
                    definition,
                    nameof(AttackDefinition.ProjectileDefinition),
                    projectileDefinition);
            }

            return definition;
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
            string backingFieldName = $"<{propertyName}>k__BackingField";
            while (currentType != null)
            {
                FieldInfo backingField = currentType.GetField(
                    backingFieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (backingField != null)
                {
                    backingField.SetValue(target, value);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new MissingFieldException(
                target.GetType().FullName,
                backingFieldName);
        }

        private UnitDefinition CreateUnitDefinition(
            string definitionName,
            UnitFaction faction,
            AttackDefinition attackDefinition,
            float chaseRange)
        {
            UnitDefinition definition;
            if (faction == UnitFaction.Player)
            {
                definition = CreateScriptableObject<PlayerUnitDefinition>();
            }
            else
            {
                AIUnitDefinition aiDefinition =
                    CreateScriptableObject<AIUnitDefinition>();
                SetAutoProperty(
                    aiDefinition,
                    nameof(AIUnitDefinition.ChaseRange),
                    chaseRange);
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
            SetAutoProperty(
                definition,
                nameof(UnitDefinition.PoolId),
                new PoolId($"{definitionName}Pool"));
            return definition;
        }

        private AttackDefinition CreateAttackDefinition(
            string definitionName,
            AttackDeliveryType deliveryType,
            float attackRange,
            float cooldown,
            float windup,
            float recovery)
        {
            AttackDefinition definition =
                CreateScriptableObject<AttackDefinition>();
            definition.name = definitionName;
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AttackId),
                new AttackId(definitionName));
            SetAutoProperty(definition, nameof(AttackDefinition.Damage), 10f);
            SetAutoProperty(definition, nameof(AttackDefinition.AttackRange), attackRange);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.CooldownDuration),
                cooldown);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.WindupDuration),
                windup);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.RecoveryDuration),
                recovery);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.DeliveryType),
                deliveryType);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AcceptedHitEffect),
                new AcceptedHitEffectConfiguration());
            return definition;
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }
    }
}
