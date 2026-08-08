using System.Collections.Generic;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Player;
using NUnit.Framework;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class FactionRulesTests
    {
        [TestCase(UnitFaction.Player, UnitFaction.Player, false)]
        [TestCase(UnitFaction.Player, UnitFaction.Ally, false)]
        [TestCase(UnitFaction.Player, UnitFaction.Enemy, true)]
        [TestCase(UnitFaction.Ally, UnitFaction.Player, false)]
        [TestCase(UnitFaction.Ally, UnitFaction.Ally, false)]
        [TestCase(UnitFaction.Ally, UnitFaction.Enemy, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Player, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Ally, true)]
        [TestCase(UnitFaction.Enemy, UnitFaction.Enemy, false)]
        public void AreHostile_ReturnsDocumentedFactionMatrix(
            UnitFaction attackerFaction,
            UnitFaction targetFaction,
            bool expectedResult)
        {
            Assert.That(FactionRules.AreHostile(attackerFaction, targetFaction), Is.EqualTo(expectedResult));
        }
    }

    public sealed class HealthStateTests
    {
        [Test]
        public void Initialize_SetsMaximumCurrentAndAliveState()
        {
            HealthState healthState = new HealthState();

            healthState.Initialize(100f);

            Assert.That(healthState.MaximumHealth, Is.EqualTo(100f));
            Assert.That(healthState.CurrentHealth, Is.EqualTo(100f));
            Assert.That(healthState.IsAlive, Is.True);
        }

        [Test]
        public void ApplyDamageAndHealing_ClampsInsideHealthBounds()
        {
            HealthState healthState = new HealthState();
            healthState.Initialize(100f);

            HealthChangeResult damage = healthState.ApplyDamage(35f);
            HealthChangeResult healing = healthState.ApplyHealing(200f);

            Assert.That(damage.AppliedAmount, Is.EqualTo(35f));
            Assert.That(healing.AppliedAmount, Is.EqualTo(35f));
            Assert.That(healthState.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void ApplyOverkill_KillsOnceAndRejectsLaterChanges()
        {
            HealthState healthState = new HealthState();
            healthState.Initialize(40f);

            HealthChangeResult killingDamage = healthState.ApplyDamage(100f);
            HealthChangeResult laterDamage = healthState.ApplyDamage(10f);
            HealthChangeResult laterHealing = healthState.ApplyHealing(10f);

            Assert.That(killingDamage.AppliedAmount, Is.EqualTo(40f));
            Assert.That(killingDamage.BecameDead, Is.True);
            Assert.That(laterDamage.Outcome, Is.EqualTo(HealthChangeOutcome.AlreadyDead));
            Assert.That(laterDamage.BecameDead, Is.False);
            Assert.That(laterHealing.Outcome, Is.EqualTo(HealthChangeOutcome.AlreadyDead));
            Assert.That(healthState.CurrentHealth, Is.Zero);
        }

        [Test]
        public void ApplyExactLethalDamage_ReachesZeroAndReportsDeathOnce()
        {
            HealthState healthState = new HealthState();
            healthState.Initialize(40f);

            HealthChangeResult killingDamage = healthState.ApplyDamage(40f);
            HealthChangeResult repeatedDamage = healthState.ApplyDamage(1f);

            Assert.That(killingDamage.CurrentHealth, Is.Zero);
            Assert.That(killingDamage.BecameDead, Is.True);
            Assert.That(repeatedDamage.BecameDead, Is.False);
        }

        [Test]
        public void Reset_RestoresFullAliveState()
        {
            HealthState healthState = new HealthState();
            healthState.Initialize(75f);
            healthState.ApplyDamage(75f);

            healthState.Reset();

            Assert.That(healthState.CurrentHealth, Is.EqualTo(75f));
            Assert.That(healthState.IsAlive, Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Initialize_RejectsNonPositiveMaximum(float maximumHealth)
        {
            HealthState healthState = new HealthState();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => healthState.Initialize(maximumHealth));
        }

        [Test]
        public void RejectNonFiniteHealthValues()
        {
            HealthState healthState = new HealthState();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => healthState.Initialize(float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => healthState.Initialize(float.PositiveInfinity));

            healthState.Initialize(10f);
            Assert.That(healthState.ApplyDamage(float.NaN).Outcome, Is.EqualTo(HealthChangeOutcome.InvalidAmount));
            Assert.That(
                healthState.ApplyHealing(float.PositiveInfinity).Outcome,
                Is.EqualTo(HealthChangeOutcome.InvalidAmount));
        }
    }

    public sealed class WeaponIndexCycleTests
    {
        [TestCase(0, 3, 1)]
        [TestCase(1, 3, 2)]
        [TestCase(2, 3, 0)]
        public void GetNextIndex_Wraps(int currentIndex, int weaponCount, int expectedIndex)
        {
            Assert.That(WeaponIndexCycle.GetNextIndex(currentIndex, weaponCount), Is.EqualTo(expectedIndex));
        }

        [TestCase(0, 3, 2)]
        [TestCase(1, 3, 0)]
        [TestCase(2, 3, 1)]
        public void GetPreviousIndex_Wraps(int currentIndex, int weaponCount, int expectedIndex)
        {
            Assert.That(WeaponIndexCycle.GetPreviousIndex(currentIndex, weaponCount), Is.EqualTo(expectedIndex));
        }

        [Test]
        public void CycleSingleWeapon_RemainsSelectedInBothDirections()
        {
            Assert.That(WeaponIndexCycle.GetPreviousIndex(0, 1), Is.Zero);
            Assert.That(WeaponIndexCycle.GetNextIndex(0, 1), Is.Zero);
        }
    }

    public sealed class StunnerHitScheduleTests
    {
        private static readonly AttackKey s_AttackKey =
            new AttackKey(new SpawnId(1), new AttackSequenceId(1));

        [Test]
        public void IdentifyStunHitsOneFourAndSeven()
        {
            StunnerHitSchedule schedule = new StunnerHitSchedule();

            for (int hitNumber = 1; hitNumber <= 7; hitNumber++)
            {
                bool expectedStun = hitNumber == 1 || hitNumber == 4 || hitNumber == 7;
                Assert.That(schedule.ShouldStunNextSuccessfulHit, Is.EqualTo(expectedStun), $"Hit {hitNumber}");
                schedule.RecordInteraction(CreateAppliedResult());
            }
        }

        [Test]
        public void RecordRejectedHitsAndMisses_DoesNotAdvanceSchedule()
        {
            StunnerHitSchedule schedule = new StunnerHitSchedule();

            schedule.RecordInteraction(InteractionResult.CreateRejected(
                InteractionOutcome.InvalidFaction,
                s_AttackKey,
                new SpawnId(2)));
            schedule.RecordInteraction(InteractionResult.CreateRejected(
                InteractionOutcome.OutOfRange,
                s_AttackKey,
                new SpawnId(2)));

            Assert.That(schedule.SuccessfulHitCount, Is.Zero);
            Assert.That(schedule.ShouldStunNextSuccessfulHit, Is.True);
        }

        [Test]
        public void Reset_RestoresFirstHitCadence()
        {
            StunnerHitSchedule schedule = new StunnerHitSchedule();
            schedule.RecordInteraction(CreateAppliedResult());
            schedule.RecordInteraction(CreateAppliedResult());

            schedule.Reset();

            Assert.That(schedule.SuccessfulHitCount, Is.Zero);
            Assert.That(schedule.ShouldStunNextSuccessfulHit, Is.True);
        }

        private static InteractionResult CreateAppliedResult()
        {
            return InteractionResult.CreateApplied(
                s_AttackKey,
                new SpawnId(2),
                DamageResult.CreateApplied(10f, false));
        }
    }

    public sealed class IdentityTests
    {
        [Test]
        public void CompareAttackKeys_UsesSourceSpawnAndLocalSequenceForEquality()
        {
            AttackKey first = new AttackKey(new SpawnId(10), new AttackSequenceId(3));
            AttackKey same = new AttackKey(new SpawnId(10), new AttackSequenceId(3));
            AttackKey laterSequence = new AttackKey(new SpawnId(10), new AttackSequenceId(4));
            AttackKey reusedSequenceOnNewSpawn = new AttackKey(new SpawnId(11), new AttackSequenceId(3));

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(laterSequence));
            Assert.That(first, Is.Not.EqualTo(reusedSequenceOnNewSpawn));
        }

        [Test]
        public void CompareStableStringIds_OrdinallyByTypeAndValue()
        {
            UnitId unitId = new UnitId("ClassicMelee");
            UnitId sameUnitId = new UnitId("ClassicMelee");
            UnitId differentCase = new UnitId("classicmelee");
            PoolId poolId = new PoolId("ClassicMelee");

            Assert.That(unitId, Is.EqualTo(sameUnitId));
            Assert.That(unitId, Is.Not.EqualTo(differentCase));
            Assert.That(unitId.Equals(poolId), Is.False);
        }

        [Test]
        public void StoreStableIdsAndAttackKeys_RemainsUniqueInHashSets()
        {
            HashSet<UnitId> unitIds = new HashSet<UnitId>
            {
                new UnitId("ClassicMelee"),
                new UnitId("ClassicMelee"),
                new UnitId("Dragon")
            };
            HashSet<AttackId> attackIds = new HashSet<AttackId>
            {
                new AttackId("BasicMelee"),
                new AttackId("BasicMelee"),
                new AttackId("BasicBullet")
            };
            HashSet<PoolId> poolIds = new HashSet<PoolId>
            {
                new PoolId("Bullet"),
                new PoolId("Bullet"),
                new PoolId("Fireball")
            };
            HashSet<AttackKey> attackKeys = new HashSet<AttackKey>
            {
                new AttackKey(new SpawnId(1), new AttackSequenceId(1)),
                new AttackKey(new SpawnId(1), new AttackSequenceId(1)),
                new AttackKey(new SpawnId(2), new AttackSequenceId(1))
            };
            HashSet<SpawnId> spawnIds = new HashSet<SpawnId>
            {
                new SpawnId(1),
                new SpawnId(1),
                new SpawnId(2)
            };
            HashSet<AttackSequenceId> sequenceIds = new HashSet<AttackSequenceId>
            {
                new AttackSequenceId(1),
                new AttackSequenceId(1),
                new AttackSequenceId(2)
            };

            Assert.That(unitIds.Count, Is.EqualTo(2));
            Assert.That(attackIds.Count, Is.EqualTo(2));
            Assert.That(poolIds.Count, Is.EqualTo(2));
            Assert.That(attackKeys.Count, Is.EqualTo(2));
            Assert.That(spawnIds.Count, Is.EqualTo(2));
            Assert.That(sequenceIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void CreateDamagePayload_CopiesStatusEffectsAndExposesCompositeKey()
        {
            Combat.StatusEffects.StatusEffectPayload[] effects =
            {
                new Combat.StatusEffects.StatusEffectPayload(
                    Combat.StatusEffects.StatusEffectType.Stun,
                    2f)
            };
            DamagePayload payload = new DamagePayload(
                new SpawnId(5),
                UnitFaction.Enemy,
                new AttackSequenceId(9),
                15f,
                default,
                effects);

            effects[0] = default;

            Assert.That(payload.AttackKey, Is.EqualTo(new AttackKey(new SpawnId(5), new AttackSequenceId(9))));
            Assert.That(payload.GetStatusEffect(0).Type, Is.EqualTo(Combat.StatusEffects.StatusEffectType.Stun));
            Assert.That(payload.IsValid, Is.True);
        }

        [Test]
        public void CheckDefaultResults_DoNotReportSuccess()
        {
            Assert.That(default(DamageResult).IsApplied, Is.False);
            Assert.That(default(InteractionResult).IsApplied, Is.False);
            Assert.That(default(HealthChangeResult).IsApplied, Is.False);
            Assert.That(default(Core.Pooling.PoolReturnResult).IsSuccess, Is.False);
            Assert.That(PoolReturnResult.CreateSuccess(new PoolId("Fixture")).IsSuccess, Is.True);
            Assert.Throws<System.ArgumentException>(() =>
                PoolRentResult<object>.CreateSuccess(default, new object()));
        }

        [Test]
        public void CreateDamageResult_CopiesAcceptedEffectDetails()
        {
            Combat.StatusEffects.StatusEffectPayload[] acceptedEffects =
            {
                new Combat.StatusEffects.StatusEffectPayload(
                    Combat.StatusEffects.StatusEffectType.Stun,
                    2f)
            };
            DamageResult result = DamageResult.CreateApplied(12f, false, acceptedEffects);

            acceptedEffects[0] = default;

            Assert.That(result.AcceptedStatusEffectCount, Is.EqualTo(1));
            Assert.That(result.GetAcceptedStatusEffect(0).Type, Is.EqualTo(Combat.StatusEffects.StatusEffectType.Stun));
            Assert.That(result.GetAcceptedStatusEffect(0).Duration, Is.EqualTo(2f));
            Assert.Throws<System.ArgumentException>(() => DamageResult.CreateApplied(
                12f,
                true,
                new Combat.StatusEffects.StatusEffectPayload(
                    Combat.StatusEffects.StatusEffectType.Stun,
                    2f)));
        }

        [Test]
        public void CreateHitContext_CarriesTheImmutableDamagePayload()
        {
            DamagePayload payload = new DamagePayload(
                new SpawnId(15),
                UnitFaction.Ally,
                new AttackSequenceId(4),
                8f,
                default);
            HitContext hitContext = new HitContext(
                payload,
                null,
                UnityEngine.Vector3.one,
                UnityEngine.Vector3.up,
                HitType.Direct,
                "TestExecutor");

            Assert.That(hitContext.Payload.AttackKey, Is.EqualTo(payload.AttackKey));
            Assert.That(hitContext.Payload.BaseDamage, Is.EqualTo(8f));
        }

        [Test]
        public void ReadDefaultAttackDeliveryType_ReturnsUnspecified()
        {
            Assert.That(default(AttackDeliveryType), Is.EqualTo(AttackDeliveryType.Unspecified));
        }

        [Test]
        public void CreateRejectedInteraction_DoesNotFabricateDamageOutcome()
        {
            InteractionResult result = InteractionResult.CreateRejected(
                InteractionOutcome.InvalidFaction,
                new AttackKey(new SpawnId(1), new AttackSequenceId(1)),
                new SpawnId(2));

            Assert.That(result.DamageResult.Outcome, Is.EqualTo(DamageOutcome.None));
            Assert.That(result.IsApplied, Is.False);
        }

        [Test]
        public void RejectUninitializedFactionAndDamageCategory_AsGameplayValues()
        {
            Assert.That(FactionRules.AreHostile(default, UnitFaction.Enemy), Is.False);
            Assert.That(default(DamageCategoryId).IsValid, Is.False);
            Assert.That(default(AttackKey).IsValid, Is.False);
            Assert.That(
                new Combat.StatusEffects.StatusEffectPayload(
                    (Combat.StatusEffects.StatusEffectType)999,
                    1f).IsValid,
                Is.False);
            Assert.Throws<System.ArgumentException>(() =>
                InteractionResult.CreateRejected(
                    (InteractionOutcome)999,
                    new AttackKey(new SpawnId(1), new AttackSequenceId(1)),
                    new SpawnId(2)));
        }

        [Test]
        public void ValidateDamagePayload_RejectsInvalidIdentityFactionDamageAndEffects()
        {
            DamagePayload invalidIdentity = new DamagePayload(
                default,
                UnitFaction.Ally,
                new AttackSequenceId(1),
                1f,
                default);
            DamagePayload invalidFaction = new DamagePayload(
                new SpawnId(1),
                default,
                new AttackSequenceId(1),
                1f,
                default);
            DamagePayload invalidDamage = new DamagePayload(
                new SpawnId(1),
                UnitFaction.Ally,
                new AttackSequenceId(1),
                float.NaN,
                default);
            DamagePayload invalidEffect = new DamagePayload(
                new SpawnId(1),
                UnitFaction.Ally,
                new AttackSequenceId(1),
                1f,
                default,
                new Combat.StatusEffects.StatusEffectPayload(
                    Combat.StatusEffects.StatusEffectType.Stun,
                    0f));

            Assert.That(invalidIdentity.IsValid, Is.False);
            Assert.That(invalidFaction.IsValid, Is.False);
            Assert.That(invalidDamage.IsValid, Is.False);
            Assert.That(invalidEffect.IsValid, Is.False);
        }
    }
}
