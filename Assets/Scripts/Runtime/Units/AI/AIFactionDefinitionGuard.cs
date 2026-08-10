using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using UnityEngine;

namespace MonstersVsZombies.Units.AI
{
    /// <summary>
    /// Prevents Ally and Enemy prefab branches from accepting a mismatched unit
    /// definition during pooled spawn configuration.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitController))]
    public sealed class AIFactionDefinitionGuard : MonoBehaviour, IPoolable
    {
        [field: SerializeField] public UnitFaction ExpectedFaction { get; private set; }

        private UnitController _unitController;
        private bool _isPreparedForSpawn;

        private void Awake()
        {
            _unitController = GetComponent<UnitController>();
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            _unitController = GetComponent<UnitController>();
            if (ExpectedFaction != UnitFaction.Ally &&
                ExpectedFaction != UnitFaction.Enemy)
            {
                failureMessage =
                    "An AI faction branch must expect Ally or Enemy.";
                return false;
            }

            if (_unitController == null ||
                !(_unitController.Definition is AIUnitDefinition definition) ||
                definition.Faction != ExpectedFaction)
            {
                failureMessage =
                    $"The {ExpectedFaction} AI branch accepts only a matching AIUnitDefinition.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        public bool PrepareForSpawn()
        {
            _isPreparedForSpawn = ValidateConfiguration(out _);
            return _isPreparedForSpawn;
        }

        public bool CompleteSpawn()
        {
            return _isPreparedForSpawn && gameObject.activeInHierarchy;
        }

        public void PrepareForReturn()
        {
            _isPreparedForSpawn = false;
        }

        internal void Configure(UnitFaction expectedFaction)
        {
            ExpectedFaction = expectedFaction;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = ExpectedFaction == UnitFaction.Ally
                ? Color.blue
                : new Color(1f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
