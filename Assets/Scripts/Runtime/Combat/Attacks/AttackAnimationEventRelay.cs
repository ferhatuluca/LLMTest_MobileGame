using UnityEngine;

namespace MonstersVsZombies.Combat.Attacks
{
    /// <summary>
    /// Forwards animation timeline impact/complete events to AttackController
    /// without letting animation clips own gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackAnimationEventRelay : MonoBehaviour
    {
        [field: SerializeField] public AttackController AttackController { get; private set; }

        private void Awake()
        {
            CacheAttackController();
        }

        private void OnValidate()
        {
            CacheAttackController();
        }

        public void RequestImpact()
        {
            AttackController?.RequestImpact();
        }

        private void CacheAttackController()
        {
            if (AttackController == null)
            {
                AttackController = GetComponentInParent<AttackController>(true);
            }
        }
    }
}
