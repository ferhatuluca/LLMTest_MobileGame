using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AttackController))]
    public sealed class StunnerHitPolicy : MonoBehaviour,
        IAttackPayloadPolicy,
        IAttackResultPolicy,
        IPoolable
    {
        private readonly StunnerHitSchedule _schedule =
            new StunnerHitSchedule();

        private AttackController _attackController;
        private UnitController _unitController;

        public int SuccessfulHitCount => _schedule.SuccessfulHitCount;
        public bool ShouldStunNextSuccessfulHit =>
            _schedule.ShouldStunNextSuccessfulHit;

        private void Awake()
        {
            _attackController = GetComponent<AttackController>();
            _unitController = GetComponent<UnitController>();
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            _attackController = GetComponent<AttackController>();
            _unitController = GetComponent<UnitController>();
            AttackDefinition attackDefinition =
                _attackController == null
                    ? null
                    : _attackController.AttackDefinition;
            AcceptedHitEffectConfiguration acceptedHitEffect =
                attackDefinition == null
                    ? default
                    : attackDefinition.AcceptedHitEffect;
            if (attackDefinition == null ||
                attackDefinition.DeliveryType != AttackDeliveryType.Melee ||
                acceptedHitEffect.EffectType != StatusEffectType.Stun ||
                acceptedHitEffect.Duration <= 0f ||
                float.IsNaN(acceptedHitEffect.Duration) ||
                float.IsInfinity(acceptedHitEffect.Duration))
            {
                failureMessage =
                    "StunnerHitPolicy requires a melee attack with a positive finite accepted Stun effect.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public DamagePayload ModifyPayload(
            AttackExecutionContext executionContext,
            DamagePayload basePayload)
        {
            if (!basePayload.IsValid)
            {
                return basePayload;
            }

            if (executionContext.Source == _unitController &&
                ValidateConfiguration(out _) &&
                _schedule.ShouldStunNextSuccessfulHit)
            {
                return basePayload;
            }

            return CreatePayloadWithoutEffects(basePayload);
        }

        private static DamagePayload CreatePayloadWithoutEffects(
            DamagePayload basePayload)
        {
            return new DamagePayload(
                basePayload.SourceSpawnId,
                basePayload.SourceFaction,
                basePayload.AttackSequenceId,
                basePayload.BaseDamage,
                basePayload.DamageCategory);
        }

        public void HandleSuccessfulInteraction(
            AttackExecutionContext executionContext,
            InteractionResult interactionResult)
        {
            _schedule.RecordInteraction(interactionResult);
        }

        public bool PrepareForSpawn()
        {
            _schedule.Reset();
            return ValidateConfiguration(out _);
        }

        public bool CompleteSpawn()
        {
            return gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
            _schedule.Reset();
        }
    }
}
