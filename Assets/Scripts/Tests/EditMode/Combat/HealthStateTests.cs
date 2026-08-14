using System;
using MonstersVsZombies.Combat.Health;
using NUnit.Framework;

namespace MonstersVsZombies.Tests.EditMode.Combat
{
    public sealed class HealthStateTests
    {
        private HealthState _health;

        [SetUp]
        public void SetUp()
        {
            _health = new HealthState();
            _health.Initialize(100f);
        }

        [Test]
        public void Initialize_StartsAtFullHealth()
        {
            Assert.That(_health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(_health.MaximumHealth, Is.EqualTo(100f));
            Assert.That(_health.IsAlive, Is.True);
        }

        [Test]
        public void ApplyDamage_SubtractsAndReportsAppliedAmount()
        {
            HealthChangeResult result = _health.ApplyDamage(25f);

            Assert.That(result.Outcome, Is.EqualTo(HealthChangeOutcome.Applied));
            Assert.That(result.AppliedAmount, Is.EqualTo(25f));
            Assert.That(_health.CurrentHealth, Is.EqualTo(75f));
            Assert.That(result.BecameDead, Is.False);
        }

        [Test]
        public void ApplyDamage_ClampsOverkillAndDiesOnce()
        {
            HealthChangeResult lethalResult = _health.ApplyDamage(150f);
            HealthChangeResult laterResult = _health.ApplyDamage(1f);

            Assert.That(lethalResult.AppliedAmount, Is.EqualTo(100f));
            Assert.That(lethalResult.BecameDead, Is.True);
            Assert.That(_health.IsAlive, Is.False);
            Assert.That(laterResult.Outcome, Is.EqualTo(HealthChangeOutcome.AlreadyDead));
        }

        [Test]
        public void ApplyHealing_ClampsAtMaximumHealth()
        {
            _health.ApplyDamage(10f);

            HealthChangeResult result = _health.ApplyHealing(50f);

            Assert.That(result.AppliedAmount, Is.EqualTo(10f));
            Assert.That(_health.CurrentHealth, Is.EqualTo(100f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ApplyDamage_RejectsInvalidAmounts(float amount)
        {
            HealthChangeResult result = _health.ApplyDamage(amount);

            Assert.That(result.Outcome, Is.EqualTo(HealthChangeOutcome.InvalidAmount));
            Assert.That(_health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void Reset_RestoresADeadStateForPoolReuse()
        {
            _health.ApplyDamage(100f);

            _health.Reset();

            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void Initialize_RejectsNonPositiveMaximumHealth()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HealthState().Initialize(0f));
        }
    }
}
