using System;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.Projectiles;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    /// <summary>
    /// Resolves a non-allocating raycast attack, selects the nearest valid world
    /// or hostile contact, submits damage through InteractionSystem, and spawns
    /// a pooled beam presentation using the attacker's faction color.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitscanAttackExecutor : MonoBehaviour, IAttackExecutor
    {
        private RaycastHit[] _castHits;
        private int _deliveryLayerMask;

        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public PoolManager PoolManager { get; private set; }
        [field: SerializeField] public Transform AttackOrigin { get; private set; }
        [field: SerializeField] public PoolId BeamPoolId { get; private set; }
        [field: SerializeField] public int CastCapacity { get; private set; }

        public AttackDeliveryType DeliveryType => AttackDeliveryType.Hitscan;
        public bool IsInitialized => _castHits != null;
        public bool WasLastCastSaturated { get; private set; }

        private void Awake()
        {
            EnsureCastBuffer();
        }

        private void OnValidate()
        {
            EnsureCastBuffer();
        }

        public bool InitializeCastCapacity(int castCapacity)
        {
            if (castCapacity <= 0 ||
                (CastCapacity > 0 && CastCapacity != castCapacity))
            {
                return false;
            }

            CastCapacity = castCapacity;
            _castHits = new RaycastHit[castCapacity];
            _deliveryLayerMask = DeliveryCollisionRules.CreateDeliveryLayerMask();
            return true;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            EnsureCastBuffer();
            if (!IsInitialized || InteractionSystem == null ||
                PoolManager == null || AttackOrigin == null ||
                !BeamPoolId.IsValid)
            {
                failureMessage =
                    "HitscanAttackExecutor requires an explicit cast capacity, interaction and pool services, attack origin, and beam pool ID.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public InteractionResult ExecuteImpact(
            AttackExecutionContext executionContext)
        {
            if (!ValidateConfiguration(out _) ||
                executionContext.Source == null ||
                executionContext.Target == null ||
                executionContext.Definition == null ||
                executionContext.Definition.DeliveryType != DeliveryType ||
                executionContext.HitLedger == null)
            {
                return CreateRejectedResult(executionContext);
            }

            Vector3 castDirection = executionContext.TargetPosition -
                                    AttackOrigin.position;
            if (castDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                castDirection = AttackOrigin.forward;
            }
            else
            {
                castDirection.Normalize();
            }

            DamagePayload damagePayload =
                AttackPayloadFactory.Create(executionContext);
            int hitCount = Physics.RaycastNonAlloc(
                AttackOrigin.position,
                castDirection,
                _castHits,
                executionContext.Definition.AttackRange,
                _deliveryLayerMask,
                QueryTriggerInteraction.Collide);
            WasLastCastSaturated = hitCount >= _castHits.Length;

            int selectedHitIndex = -1;
            DeliveryContactType selectedContactType = DeliveryContactType.Ignore;
            DamageTargetProxy selectedTarget = null;
            float selectedDistance = float.PositiveInfinity;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit castHit = _castHits[hitIndex];
                DeliveryContactType contactType = DeliveryCollisionRules.Classify(
                    castHit.collider,
                    damagePayload,
                    out DamageTargetProxy targetProxy);
                if (contactType == DeliveryContactType.Ignore ||
                    castHit.distance > selectedDistance ||
                    (castHit.distance == selectedDistance &&
                     selectedContactType == DeliveryContactType.World))
                {
                    continue;
                }

                selectedHitIndex = hitIndex;
                selectedContactType = contactType;
                selectedTarget = targetProxy;
                selectedDistance = castHit.distance;
            }

            Vector3 beamEnd = AttackOrigin.position +
                              (castDirection * executionContext.Definition.AttackRange);
            InteractionResult result = CreateRejectedResult(executionContext);
            if (selectedHitIndex >= 0)
            {
                RaycastHit selectedHit = _castHits[selectedHitIndex];
                beamEnd = selectedHit.point;
                if (selectedContactType == DeliveryContactType.HostileTarget)
                {
                    HitContext hitContext = new HitContext(
                        damagePayload,
                        selectedTarget.DamageController,
                        selectedHit.point,
                        selectedHit.normal,
                        HitType.Direct,
                        $"Hitscan:{executionContext.Definition.AttackId}");
                    result = InteractionSystem.ResolveHit(
                        hitContext,
                        executionContext.HitLedger);
                }
            }

            TrySpawnBeam(
                AttackOrigin.position,
                beamEnd,
                executionContext.Source.Faction);
            ClearCastHits(hitCount);
            return result;
        }

        internal bool Configure(
            InteractionSystem interactionSystem,
            PoolManager poolManager,
            Transform attackOrigin,
            PoolId beamPoolId,
            int castCapacity)
        {
            if (interactionSystem == null || poolManager == null ||
                attackOrigin == null || !beamPoolId.IsValid ||
                (IsInitialized
                    ? CastCapacity != castCapacity
                    : !InitializeCastCapacity(castCapacity)))
            {
                return false;
            }

            InteractionSystem = interactionSystem;
            PoolManager = poolManager;
            AttackOrigin = attackOrigin;
            BeamPoolId = beamPoolId;
            return true;
        }

        private void TrySpawnBeam(
            Vector3 startPosition,
            Vector3 endPosition,
            UnitFaction sourceFaction)
        {
            PoolRentResult<PooledEntity> rentResult =
                PoolManager.Rent(BeamPoolId);
            if (!rentResult.IsSuccess)
            {
                return;
            }

            PooledEntity pooledEntity = rentResult.Entity;
            LaserBeamPresentationController beamController =
                pooledEntity.GetComponent<LaserBeamPresentationController>();
            if (beamController == null ||
                !beamController.ConfigurePresentation(
                    startPosition,
                    endPosition,
                    PoolManager,
                    sourceFaction) ||
                !pooledEntity.PrepareForSpawn())
            {
                PoolManager.Return(pooledEntity);
                return;
            }

            pooledEntity.gameObject.SetActive(true);
            if (!pooledEntity.CompleteSpawn())
            {
                PoolManager.Return(pooledEntity);
            }
        }

        private void ClearCastHits(int hitCount)
        {
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                _castHits[hitIndex] = default;
            }
        }

        private static InteractionResult CreateRejectedResult(
            AttackExecutionContext executionContext)
        {
            return InteractionResult.CreateRejected(
                InteractionOutcome.InvalidTarget,
                executionContext.AttackKey,
                executionContext.Target == null
                    ? default
                    : executionContext.Target.SpawnId);
        }

        private void EnsureCastBuffer()
        {
            if (CastCapacity <= 0 ||
                (_castHits != null && _castHits.Length == CastCapacity))
            {
                return;
            }

            _castHits = new RaycastHit[CastCapacity];
            _deliveryLayerMask = DeliveryCollisionRules.CreateDeliveryLayerMask();
        }
    }
}
