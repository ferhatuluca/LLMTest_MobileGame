using System;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Damage
{
    public readonly struct DamageResolvedEvent
    {
        public HitContext HitContext { get; }
        public DamageResult Result { get; }

        public DamageResolvedEvent(HitContext hitContext, DamageResult result)
        {
            HitContext = hitContext;
            Result = result;
        }
    }

    [DisallowMultipleComponent]
    public sealed class DamageController : MonoBehaviour
    {
        private UnitController _unitController;
        private HealthController _healthController;
        private StatusEffectController _statusEffectController;

        public event Action<DamageResolvedEvent> DamageResolved;

        public SpawnId SpawnId => _unitController == null ? default : _unitController.SpawnId;
        public UnitFaction Faction => _unitController == null ? default : _unitController.Faction;
        public bool IsTargetActive => _unitController != null && _unitController.IsActive;
        public bool IsAlive => _healthController != null && _healthController.IsAlive;
        public bool IsInvulnerable { get; private set; }

        private void Awake()
        {
            CacheSiblingComponents();
        }

        public DamageResult ApplyDamage(HitContext hitContext)
        {
            CacheSiblingComponents();
            if (!hitContext.IsValid || hitContext.Target != this)
            {
                return PublishResult(
                    hitContext,
                    DamageResult.CreateRejected(DamageOutcome.InvalidAmount));
            }

            if (_healthController == null || !_healthController.IsAlive)
            {
                return PublishResult(
                    hitContext,
                    DamageResult.CreateRejected(DamageOutcome.TargetDead));
            }

            if (_unitController == null || !_unitController.IsActive)
            {
                return PublishResult(
                    hitContext,
                    DamageResult.CreateRejected(DamageOutcome.TargetInactive));
            }

            if (IsInvulnerable)
            {
                return PublishResult(
                    hitContext,
                    DamageResult.CreateRejected(DamageOutcome.Invulnerable));
            }

            float finalDamage = ApplyTargetModifiers(hitContext.Payload.BaseDamage);
            HealthChangeResult healthResult = _healthController.ApplyDamage(finalDamage);
            if (!healthResult.IsApplied || healthResult.AppliedAmount <= 0f)
            {
                return PublishResult(
                    hitContext,
                    DamageResult.CreateRejected(DamageOutcome.InvalidAmount));
            }

            StatusEffectPayload[] acceptedEffects = Array.Empty<StatusEffectPayload>();
            int acceptedEffectCount = 0;
            if (!healthResult.BecameDead && hitContext.Payload.StatusEffectCount > 0)
            {
                acceptedEffects = new StatusEffectPayload[hitContext.Payload.StatusEffectCount];
                for (int effectIndex = 0;
                     effectIndex < hitContext.Payload.StatusEffectCount;
                     effectIndex++)
                {
                    StatusEffectPayload effectPayload =
                        hitContext.Payload.GetStatusEffect(effectIndex);
                    if (_statusEffectController != null &&
                        _statusEffectController.ApplyAcceptedEffect(effectPayload))
                    {
                        acceptedEffects[acceptedEffectCount] = effectPayload;
                        acceptedEffectCount++;
                    }
                }

                if (acceptedEffectCount != acceptedEffects.Length)
                {
                    Array.Resize(ref acceptedEffects, acceptedEffectCount);
                }
            }

            DamageResult result = DamageResult.CreateApplied(
                healthResult.AppliedAmount,
                healthResult.BecameDead,
                acceptedEffects);
            return PublishResult(hitContext, result);
        }

        internal void SetInvulnerable(bool isInvulnerable)
        {
            IsInvulnerable = isInvulnerable;
        }

        internal void ResetForSpawn()
        {
            IsInvulnerable = false;
            CacheSiblingComponents();
        }

        private float ApplyTargetModifiers(float baseDamage)
        {
            return baseDamage;
        }

        private DamageResult PublishResult(HitContext hitContext, DamageResult result)
        {
            DamageResolved?.Invoke(new DamageResolvedEvent(hitContext, result));
            return result;
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
            _healthController = GetComponent<HealthController>();
            _statusEffectController = GetComponent<StatusEffectController>();
        }
    }
}
