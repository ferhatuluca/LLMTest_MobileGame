using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepFourInteractionTests
    {
        private StepFourTestFactory _factory;
        private InteractionSystem _interactionSystem;

        [SetUp]
        public void CreateFixture()
        {
            _factory = new StepFourTestFactory();
            _interactionSystem =
                _factory.CreateComponent<InteractionSystem>("InteractionSystem");
        }

        [TearDown]
        public void DestroyFixture()
        {
            _factory.Dispose();
        }

        [TestCase(UnitFaction.Player, UnitFaction.Player, false)]
        [TestCase(UnitFaction.Player, UnitFaction.Ally, false)]
        [TestCase(UnitFaction.Player, UnitFaction.Enemy, true)]
        [TestCase(UnitFaction.Ally, UnitFaction.Player, false)]
        [TestCase(UnitFaction.Ally, UnitFaction.Ally, false)]
        [TestCase(UnitFaction.Ally, UnitFaction.Enemy, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Player, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Ally, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Enemy, false)]
        public void ResolveHit_FactionMatrixIsEnforcedAtInteractionBoundary(
            UnitFaction sourceFaction,
            UnitFaction targetFaction,
            bool shouldApply)
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, targetFaction);
            HitContext hitContext = CreateHitContext(
                target.Damage,
                new SpawnId(99),
                sourceFaction,
                1,
                10f);
            AttackHitLedger hitLedger = CreateLedger(hitContext.Payload.AttackKey);

            InteractionResult result = _interactionSystem.ResolveHit(hitContext, hitLedger);

            Assert.That(result.IsApplied, Is.EqualTo(shouldApply));
            Assert.That(
                result.Outcome,
                Is.EqualTo(shouldApply
                    ? InteractionOutcome.Applied
                    : InteractionOutcome.InvalidFaction));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(shouldApply ? 90f : 100f));
            Assert.That(hitLedger.AcceptedTargetCount, Is.EqualTo(shouldApply ? 1 : 0));
        }

        [Test]
        public void ResolveHit_SameSourceAndTargetSpawnIsRejected()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            HitContext hitContext = CreateHitContext(
                target.Damage,
                new SpawnId(10),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(hitContext.Payload.AttackKey);

            InteractionResult result = _interactionSystem.ResolveHit(hitContext, ledger);

            Assert.That(result.Outcome, Is.EqualTo(InteractionOutcome.SourceEqualsTarget));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(ledger.AcceptedTargetCount, Is.Zero);
        }

        [Test]
        public void ResolveHit_SameAttackAndTargetAppliesOnce()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            HitContext hitContext = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                7,
                10f);
            AttackHitLedger ledger = CreateLedger(hitContext.Payload.AttackKey);

            InteractionResult first = _interactionSystem.ResolveHit(hitContext, ledger);
            InteractionResult duplicate = _interactionSystem.ResolveHit(hitContext, ledger);

            Assert.That(first.Outcome, Is.EqualTo(InteractionOutcome.Applied));
            Assert.That(first.AttackKey, Is.EqualTo(hitContext.Payload.AttackKey));
            Assert.That(first.TargetSpawnId, Is.EqualTo(new SpawnId(10)));
            Assert.That(first.DamageResult.AppliedAmount, Is.EqualTo(10f));
            Assert.That(duplicate.Outcome, Is.EqualTo(InteractionOutcome.AlreadyHit));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(ledger.AcceptedTargetCount, Is.EqualTo(1));
        }

        [Test]
        public void ResolveHit_SameAttackCanHitDifferentTargets()
        {
            StepFourUnitFixture firstTarget =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            StepFourUnitFixture secondTarget =
                _factory.CreateActiveUnit(11, UnitFaction.Enemy);
            HitContext firstContext = CreateHitContext(
                firstTarget.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            HitContext secondContext = CreateHitContext(
                secondTarget.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(firstContext.Payload.AttackKey);

            InteractionResult firstResult =
                _interactionSystem.ResolveHit(firstContext, ledger);
            InteractionResult secondResult =
                _interactionSystem.ResolveHit(secondContext, ledger);

            Assert.That(firstResult.IsApplied, Is.True);
            Assert.That(secondResult.IsApplied, Is.True);
            Assert.That(ledger.AcceptedTargetCount, Is.EqualTo(2));
            Assert.That(firstTarget.Health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(secondTarget.Health.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void BeginAttack_NewSequenceAllowsSameTargetAgain()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            HitContext firstContext = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(firstContext.Payload.AttackKey);
            Assert.That(
                _interactionSystem.ResolveHit(firstContext, ledger).IsApplied,
                Is.True);
            HitContext laterContext = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                2,
                10f);

            ledger.BeginAttack(laterContext.Payload.AttackKey);
            InteractionResult laterResult =
                _interactionSystem.ResolveHit(laterContext, ledger);

            Assert.That(laterResult.IsApplied, Is.True);
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(80f));
            Assert.That(ledger.AcceptedTargetCount, Is.EqualTo(1));
        }

        [Test]
        public void BeginAttack_SameSequenceUnderDifferentSourceIsDifferentKey()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            HitContext firstContext = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                5,
                10f);
            AttackHitLedger ledger = CreateLedger(firstContext.Payload.AttackKey);
            Assert.That(
                _interactionSystem.ResolveHit(firstContext, ledger).IsApplied,
                Is.True);
            HitContext reusedSequenceContext = CreateHitContext(
                target.Damage,
                new SpawnId(2),
                UnitFaction.Player,
                5,
                10f);

            ledger.BeginAttack(reusedSequenceContext.Payload.AttackKey);
            InteractionResult result =
                _interactionSystem.ResolveHit(reusedSequenceContext, ledger);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(80f));
        }

        [Test]
        public void ResolveHit_CapturedPayloadRemainsValidAfterSourceUnregisters()
        {
            UnitRegistry registry =
                _factory.CreateComponent<UnitRegistry>("UnitRegistry");
            StepFourUnitFixture source =
                _factory.CreateActiveUnit(1, UnitFaction.Player);
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(2, UnitFaction.Enemy);
            Assert.That(registry.Register(source.Unit), Is.True);
            HitContext capturedContext = CreateHitContext(
                target.Damage,
                source.Unit.SpawnId,
                source.Unit.Faction,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(capturedContext.Payload.AttackKey);

            source.Lifecycle.PrepareForReturn();
            InteractionResult result =
                _interactionSystem.ResolveHit(capturedContext, ledger);

            Assert.That(registry.Count, Is.Zero);
            Assert.That(source.Unit.SpawnId.IsValid, Is.False);
            Assert.That(result.IsApplied, Is.True);
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void ResolveHit_CapturedFactionDoesNotFollowReusedSourceObject()
        {
            StepFourUnitFixture source =
                _factory.CreateActiveUnit(1, UnitFaction.Player);
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(3, UnitFaction.Enemy);
            HitContext capturedContext = CreateHitContext(
                target.Damage,
                source.Unit.SpawnId,
                source.Unit.Faction,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(capturedContext.Payload.AttackKey);
            source.Lifecycle.PrepareForReturn();
            source.GameObject.SetActive(false);
            _factory.Respawn(source, 2, UnitFaction.Enemy);

            InteractionResult result =
                _interactionSystem.ResolveHit(capturedContext, ledger);

            Assert.That(source.Unit.Faction, Is.EqualTo(UnitFaction.Enemy));
            Assert.That(result.IsApplied, Is.True);
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void ResolveHit_UsesCurrentTargetSpawnIdentityAfterTargetReuse()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(1, UnitFaction.Enemy);
            target.Lifecycle.PrepareForReturn();
            target.GameObject.SetActive(false);
            _factory.Respawn(target, 2, UnitFaction.Enemy);
            HitContext context = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(context.Payload.AttackKey);

            InteractionResult result = _interactionSystem.ResolveHit(context, ledger);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.TargetSpawnId, Is.EqualTo(new SpawnId(2)));
        }

        [Test]
        public void ResolveHit_InvalidPayloadOrLedgerReturnsSpecificRejection()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            HitContext invalidPayloadContext = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                0f);
            AttackHitLedger validLedger =
                CreateLedger(new AttackKey(new SpawnId(1), new AttackSequenceId(1)));
            HitContext validContext = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                2,
                10f);
            AttackHitLedger uninitializedLedger = new AttackHitLedger();
            AttackHitLedger mismatchedLedger = CreateLedger(
                new AttackKey(new SpawnId(1), new AttackSequenceId(3)));

            InteractionResult invalidPayload =
                _interactionSystem.ResolveHit(invalidPayloadContext, validLedger);
            InteractionResult uninitialized =
                _interactionSystem.ResolveHit(validContext, uninitializedLedger);
            InteractionResult mismatched =
                _interactionSystem.ResolveHit(validContext, mismatchedLedger);

            Assert.That(invalidPayload.Outcome, Is.EqualTo(InteractionOutcome.InvalidPayload));
            Assert.That(uninitialized.Outcome, Is.EqualTo(InteractionOutcome.InvalidPayload));
            Assert.That(mismatched.Outcome, Is.EqualTo(InteractionOutcome.InvalidPayload));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void ResolveHit_MissingTargetReturnsInvalidTarget()
        {
            HitContext context = CreateHitContext(
                null,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(context.Payload.AttackKey);

            InteractionResult result = _interactionSystem.ResolveHit(context, ledger);

            Assert.That(result.Outcome, Is.EqualTo(InteractionOutcome.InvalidTarget));
            Assert.That(result.TargetSpawnId.IsValid, Is.False);
            Assert.That(ledger.AcceptedTargetCount, Is.Zero);
        }

        [Test]
        public void ResolveHit_ConfiguredInactiveDeadAndPooledTargetsAreDistinguished()
        {
            StepFourUnitFixture inactive =
                _factory.CreatePreparedUnit(10, UnitFaction.Enemy);
            HitContext inactiveContext = CreateHitContext(
                inactive.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger inactiveLedger = CreateLedger(inactiveContext.Payload.AttackKey);
            Assert.That(
                _interactionSystem.ResolveHit(inactiveContext, inactiveLedger).Outcome,
                Is.EqualTo(InteractionOutcome.TargetInactive));

            StepFourUnitFixture dead =
                _factory.CreateActiveUnit(20, UnitFaction.Enemy);
            dead.Damage.ApplyDamage(CreateHitContext(
                dead.Damage,
                new SpawnId(2),
                UnitFaction.Player,
                1,
                100f));
            HitContext deadContext = CreateHitContext(
                dead.Damage,
                new SpawnId(2),
                UnitFaction.Player,
                2,
                10f);
            AttackHitLedger deadLedger = CreateLedger(deadContext.Payload.AttackKey);
            Assert.That(dead.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Dying));
            Assert.That(
                _interactionSystem.ResolveHit(deadContext, deadLedger).Outcome,
                Is.EqualTo(InteractionOutcome.TargetDead));

            StepFourUnitFixture pooled =
                _factory.CreateActiveUnit(30, UnitFaction.Enemy);
            pooled.Lifecycle.PrepareForReturn();
            HitContext pooledContext = CreateHitContext(
                pooled.Damage,
                new SpawnId(3),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger pooledLedger = CreateLedger(pooledContext.Payload.AttackKey);
            Assert.That(
                _interactionSystem.ResolveHit(pooledContext, pooledLedger).Outcome,
                Is.EqualTo(InteractionOutcome.InvalidTarget));
        }

        [Test]
        public void ResolveHit_InvulnerabilityDoesNotConsumeLedgerEntry()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            target.Damage.SetInvulnerable(true);
            HitContext context = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(context.Payload.AttackKey);

            InteractionResult rejected = _interactionSystem.ResolveHit(context, ledger);
            target.Damage.SetInvulnerable(false);
            InteractionResult applied = _interactionSystem.ResolveHit(context, ledger);

            Assert.That(rejected.Outcome, Is.EqualTo(InteractionOutcome.Invulnerable));
            Assert.That(applied.IsApplied, Is.True);
            Assert.That(ledger.AcceptedTargetCount, Is.EqualTo(1));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void ResolveHit_ReentrantAttemptIsBlockedBeforeDamageDispatch()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            HitContext context = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger ledger = CreateLedger(context.Payload.AttackKey);
            InteractionResult nestedResult = default;
            int healthEventCount = 0;
            target.Health.HealthChanged += _ =>
            {
                healthEventCount++;
                nestedResult = _interactionSystem.ResolveHit(context, ledger);
            };

            InteractionResult outerResult = _interactionSystem.ResolveHit(context, ledger);

            Assert.That(outerResult.IsApplied, Is.True);
            Assert.That(nestedResult.Outcome, Is.EqualTo(InteractionOutcome.AlreadyHit));
            Assert.That(healthEventCount, Is.EqualTo(1));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(ledger.AcceptedTargetCount, Is.EqualTo(1));
        }

        [Test]
        public void ResolveHit_LethalSynchronousReturnKeepsCapturedTargetIdentity()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            target.Lifecycle.Dying += _ => target.Lifecycle.RequestPoolReturn();
            target.Lifecycle.PoolReturnRequested += _ =>
                target.Lifecycle.PrepareForReturn();
            HitContext context = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                100f);
            AttackHitLedger ledger = CreateLedger(context.Payload.AttackKey);

            InteractionResult result = _interactionSystem.ResolveHit(context, ledger);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.DamageResult.TargetDied, Is.True);
            Assert.That(result.TargetSpawnId, Is.EqualTo(new SpawnId(10)));
            Assert.That(ledger.HasAcceptedHit(context.Payload.AttackKey, new SpawnId(10)), Is.True);
            Assert.That(target.Unit.SpawnId.IsValid, Is.False);
            Assert.That(target.Lifecycle.State, Is.EqualTo(UnitLifecycleState.Inactive));
        }

        [Test]
        public void ResolveHit_PublishesOneDiagnosticAndOnlyAcceptedHitsReachDamageController()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            int interactionEventCount = 0;
            int damageEventCount = 0;
            InteractionResult observedResult = default;
            _interactionSystem.InteractionResolved += interactionEvent =>
            {
                interactionEventCount++;
                observedResult = interactionEvent.Result;
            };
            target.Damage.DamageResolved += _ => damageEventCount++;
            HitContext friendlyContext = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Enemy,
                1,
                10f);
            AttackHitLedger friendlyLedger = CreateLedger(friendlyContext.Payload.AttackKey);

            InteractionResult friendlyResult =
                _interactionSystem.ResolveHit(friendlyContext, friendlyLedger);

            Assert.That(friendlyResult.Outcome, Is.EqualTo(InteractionOutcome.InvalidFaction));
            Assert.That(observedResult.Outcome, Is.EqualTo(friendlyResult.Outcome));
            Assert.That(interactionEventCount, Is.EqualTo(1));
            Assert.That(damageEventCount, Is.Zero);

            HitContext hostileContext = CreateHitContext(
                target.Damage,
                new SpawnId(2),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger hostileLedger = CreateLedger(hostileContext.Payload.AttackKey);
            InteractionResult hostileResult =
                _interactionSystem.ResolveHit(hostileContext, hostileLedger);

            Assert.That(hostileResult.IsApplied, Is.True);
            Assert.That(interactionEventCount, Is.EqualTo(2));
            Assert.That(damageEventCount, Is.EqualTo(1));
        }

        [Test]
        public void Ledger_ResetAndSeparateInstancesRetainNoSceneLevelHistory()
        {
            StepFourUnitFixture target =
                _factory.CreateActiveUnit(10, UnitFaction.Enemy);
            HitContext context = CreateHitContext(
                target.Damage,
                new SpawnId(1),
                UnitFaction.Player,
                1,
                10f);
            AttackHitLedger firstLedger = CreateLedger(context.Payload.AttackKey);
            AttackHitLedger secondLedger = CreateLedger(context.Payload.AttackKey);

            Assert.That(_interactionSystem.ResolveHit(context, firstLedger).IsApplied, Is.True);
            Assert.That(_interactionSystem.ResolveHit(context, secondLedger).IsApplied, Is.True);
            firstLedger.Reset();

            Assert.That(firstLedger.IsActive, Is.False);
            Assert.That(firstLedger.AcceptedTargetCount, Is.Zero);
            Assert.That(secondLedger.AcceptedTargetCount, Is.EqualTo(1));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(80f));
            Assert.Throws<System.ArgumentException>(() => firstLedger.BeginAttack(default));
        }

        private static AttackHitLedger CreateLedger(AttackKey attackKey)
        {
            AttackHitLedger ledger = new AttackHitLedger();
            ledger.BeginAttack(attackKey);
            return ledger;
        }

        private static HitContext CreateHitContext(
            DamageController target,
            SpawnId sourceSpawnId,
            UnitFaction sourceFaction,
            long attackSequence,
            float damage)
        {
            DamagePayload payload = new DamagePayload(
                sourceSpawnId,
                sourceFaction,
                new AttackSequenceId(attackSequence),
                damage,
                default);
            return new HitContext(
                payload,
                target,
                target == null ? Vector3.zero : target.transform.position,
                Vector3.up,
                HitType.Direct,
                "StepFourInteractionTest");
        }
    }
}
