using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Spawning;
using UnityEngine;

namespace MonstersVsZombies.Units.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitController))]
    [RequireComponent(typeof(TargetingController))]
    [RequireComponent(typeof(AttackController))]
    public sealed class PlayerCombatController : MonoBehaviour, IPoolable
    {
        [field: SerializeField] public ProjectileAttackExecutor ProjectileExecutor { get; private set; }
        [field: SerializeField] public GrenadeAttackExecutor GrenadeExecutor { get; private set; }
        [field: SerializeField] public HitscanAttackExecutor HitscanExecutor { get; private set; }
        [field: SerializeField] public Transform AttackOrigin { get; private set; }
        [field: SerializeField] public PoolId BeamPoolId { get; private set; }
        [field: SerializeField] public int HitscanCastCapacity { get; private set; }

        private UnitController _unitController;
        private TargetingController _targetingController;
        private AttackController _attackController;
        private StatusEffectController _statusEffectController;
        private bool _isPreparedForSpawn;
        private bool _isActivationComplete;

        public bool HasRuntimeServices { get; private set; }

        private void Awake()
        {
            CacheSiblingComponents();
        }

        private void Update()
        {
            TickAutoAttack();
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheSiblingComponents();
            if (_unitController == null || _targetingController == null ||
                _attackController == null || _statusEffectController == null ||
                ProjectileExecutor == null || GrenadeExecutor == null ||
                HitscanExecutor == null || AttackOrigin == null ||
                !BeamPoolId.IsValid || HitscanCastCapacity <= 0 ||
                ProjectileExecutor.DeliveryType != AttackDeliveryType.Projectile ||
                GrenadeExecutor.DeliveryType != AttackDeliveryType.Grenade ||
                HitscanExecutor.DeliveryType != AttackDeliveryType.Hitscan)
            {
                failureMessage =
                    "PlayerCombatController requires its unit capabilities, three compatible executors, attack origin, beam pool, and hitscan capacity.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool ConfigureRuntimeServices(
            SpawnManager spawnManager,
            InteractionSystem interactionSystem,
            PoolManager poolManager)
        {
            HasRuntimeServices =
                ValidateConfiguration(out _) &&
                ProjectileExecutor.Configure(
                    spawnManager,
                    interactionSystem,
                    AttackOrigin) &&
                GrenadeExecutor.Configure(
                    spawnManager,
                    interactionSystem,
                    AttackOrigin) &&
                HitscanExecutor.Configure(
                    interactionSystem,
                    poolManager,
                    AttackOrigin,
                    BeamPoolId,
                    HitscanCastCapacity);
            return HasRuntimeServices;
        }

        public bool PrepareForSpawn()
        {
            CacheSiblingComponents();
            _isActivationComplete = false;
            _isPreparedForSpawn = ValidateConfiguration(out _);
            return _isPreparedForSpawn;
        }

        public bool CompleteSpawn()
        {
            _isActivationComplete =
                _isPreparedForSpawn && gameObject.activeInHierarchy;
            return _isActivationComplete;
        }

        public void PrepareForReturn()
        {
            _isPreparedForSpawn = false;
            _isActivationComplete = false;
        }

        internal bool TickAutoAttack()
        {
            if (!_isPreparedForSpawn || !_isActivationComplete ||
                !HasRuntimeServices || _unitController == null ||
                !_unitController.IsActive || _targetingController == null ||
                _targetingController.CurrentTarget == null ||
                _statusEffectController == null ||
                _statusEffectController.IsAttackBlocked)
            {
                return false;
            }

            return _attackController.TryStartAttack();
        }

        private void CacheSiblingComponents()
        {
            _unitController = GetComponent<UnitController>();
            _targetingController = GetComponent<TargetingController>();
            _attackController = GetComponent<AttackController>();
            _statusEffectController = GetComponent<StatusEffectController>();
        }
    }
}
