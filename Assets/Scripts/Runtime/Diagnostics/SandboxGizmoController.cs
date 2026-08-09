using System.Collections.Generic;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Player;
using UnityEngine;

namespace MonstersVsZombies.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class SandboxGizmoController : MonoBehaviour
    {
        private readonly List<UnitController> _unitSnapshot =
            new List<UnitController>();

        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }
        [field: SerializeField] public SpawnPointGroup PlayerSpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup AllySpawnPoints { get; private set; }
        [field: SerializeField] public SpawnPointGroup EnemySpawnPoints { get; private set; }

        public bool DrawChaseRanges { get; set; } = true;
        public bool DrawAttackRanges { get; set; } = true;
        public bool DrawTargetLines { get; set; } = true;
        public bool DrawSpawnPoints { get; set; } = true;

        public bool Configure(
            UnitRegistry unitRegistry,
            SpawnPointGroup playerSpawnPoints,
            SpawnPointGroup allySpawnPoints,
            SpawnPointGroup enemySpawnPoints)
        {
            UnitRegistry = unitRegistry;
            PlayerSpawnPoints = playerSpawnPoints;
            AllySpawnPoints = allySpawnPoints;
            EnemySpawnPoints = enemySpawnPoints;
            return UnitRegistry != null && PlayerSpawnPoints != null &&
                AllySpawnPoints != null && EnemySpawnPoints != null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnDrawGizmos()
        {
            if (!SandboxDebugRuntime.IsAvailable || UnitRegistry == null)
            {
                return;
            }

            UnitRegistry.CopySnapshot(_unitSnapshot);
            foreach (UnitController unit in _unitSnapshot)
            {
                if (unit == null || !unit.IsActive)
                {
                    continue;
                }

                DrawFactionIdentity(unit);
                if (DrawChaseRanges &&
                    unit.Definition is AIUnitDefinition aiDefinition)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(unit.transform.position, aiDefinition.ChaseRange);
                }

                if (DrawAttackRanges)
                {
                    float attackRange = GetAttackRange(unit);
                    if (attackRange > 0f)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireSphere(unit.transform.position, attackRange);
                    }
                }

                if (DrawTargetLines && unit.TargetingController?.CurrentTarget != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(
                        unit.transform.position,
                        unit.TargetingController.CurrentTargetPoint);
                }
            }

            if (DrawSpawnPoints)
            {
                DrawSpawnGroup(PlayerSpawnPoints, Color.green);
                DrawSpawnGroup(AllySpawnPoints, Color.blue);
                DrawSpawnGroup(EnemySpawnPoints, Color.magenta);
            }
        }

        private static void DrawFactionIdentity(UnitController unit)
        {
            switch (unit.Faction)
            {
                case UnitFaction.Ally:
                    Gizmos.color = Color.blue;
                    break;
                case UnitFaction.Enemy:
                    Gizmos.color = Color.magenta;
                    break;
                default:
                    Gizmos.color = Color.green;
                    break;
            }

            Gizmos.DrawWireSphere(unit.transform.position + Vector3.up * 0.25f, 0.35f);
        }

        private static float GetAttackRange(UnitController unit)
        {
            if (unit.Definition is AIUnitDefinition aiDefinition &&
                aiDefinition.DefaultAttackDefinition != null)
            {
                return aiDefinition.DefaultAttackDefinition.AttackRange;
            }

            PlayerWeaponController weapons = unit.GetComponent<PlayerWeaponController>();
            return weapons?.CurrentWeapon?.AttackDefinition == null
                ? 0f
                : weapons.CurrentWeapon.AttackDefinition.AttackRange;
        }

        private static void DrawSpawnGroup(
            SpawnPointGroup spawnPointGroup,
            Color color)
        {
            if (spawnPointGroup == null)
            {
                return;
            }

            Gizmos.color = color;
            for (int pointIndex = 0; pointIndex < spawnPointGroup.Count; pointIndex++)
            {
                if (spawnPointGroup.TryGetPoint(pointIndex, out Pose pose))
                {
                    Gizmos.DrawWireCube(pose.position, Vector3.one * 0.6f);
                    Gizmos.DrawRay(pose.position, pose.forward);
                }
            }
        }
#endif
    }
}
