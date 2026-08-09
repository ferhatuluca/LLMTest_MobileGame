using System;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepElevenLiveVerification
    {
        [MenuItem("Tools/Monsters vs Zombies/Step 11/Report Live AI State %#a")]
        public static void ReportLiveAIState()
        {
            AISandboxScenarioController scenario = RequireLiveScenario();
            CombatSandboxBootstrap bootstrap = scenario.Bootstrap;
            UnitController ally = scenario.InitialAlly;
            UnitController enemy = scenario.InitialEnemy;
            NavMeshUnitMotor allyMotor = ally == null
                ? null
                : ally.GetComponent<NavMeshUnitMotor>();
            NavMeshUnitMotor enemyMotor = enemy == null
                ? null
                : enemy.GetComponent<NavMeshUnitMotor>();
            NavMeshAgent allyAgent = ally == null
                ? null
                : ally.GetComponent<NavMeshAgent>();
            NavMeshAgent enemyAgent = enemy == null
                ? null
                : enemy.GetComponent<NavMeshAgent>();

            Debug.Log(
                $"[StepElevenLiveState] " +
                $"PlayerHealth={GetHealth(bootstrap.InitialPlayer):0.##}; " +
                $"StationaryEnemyHealth={GetHealth(bootstrap.InitialStationaryEnemy):0.##}; " +
                $"AllyPosition={GetPosition(ally):F3}; " +
                $"AllyState={GetState(ally)}; " +
                $"AllyOnNavMesh={allyMotor != null && allyMotor.IsOnNavMesh}; " +
                $"AllyStopped={allyMotor == null || allyMotor.IsStopped}; " +
                $"AllyCommands={allyMotor?.DestinationCommandCount ?? 0}; " +
                $"AllyHasPath={allyAgent != null && allyAgent.hasPath}; " +
                $"AllyPending={allyAgent != null && allyAgent.pathPending}; " +
                $"AllyVelocity={GetVelocity(allyAgent):F3}; " +
                $"EnemyPosition={GetPosition(enemy):F3}; " +
                $"EnemyState={GetState(enemy)}; " +
                $"EnemyOnNavMesh={enemyMotor != null && enemyMotor.IsOnNavMesh}; " +
                $"EnemyStopped={enemyMotor == null || enemyMotor.IsStopped}; " +
                $"EnemyCommands={enemyMotor?.DestinationCommandCount ?? 0}; " +
                $"EnemyHasPath={enemyAgent != null && enemyAgent.hasPath}; " +
                $"EnemyPending={enemyAgent != null && enemyAgent.pathPending}; " +
                $"EnemyVelocity={GetVelocity(enemyAgent):F3}.");
        }

        private static AISandboxScenarioController RequireLiveScenario()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter CombatSandbox Play Mode before AI verification.");
            }

            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "__Systems")
                {
                    continue;
                }

                Transform scenarioTransform =
                    root.transform.Find("AISandboxScenario");
                AISandboxScenarioController scenario =
                    scenarioTransform == null
                        ? null
                        : scenarioTransform
                            .GetComponent<AISandboxScenarioController>();
                if (scenario != null && scenario.IsInitialized &&
                    scenario.InitialAlly != null &&
                    scenario.InitialEnemy != null)
                {
                    return scenario;
                }
            }

            throw new InvalidOperationException(
                "The live Step 11 AI scenario is not ready.");
        }

        private static float GetHealth(UnitController unit)
        {
            return unit == null || unit.HealthController == null
                ? 0f
                : unit.HealthController.CurrentHealth;
        }

        private static Vector3 GetPosition(UnitController unit)
        {
            return unit == null ? default : unit.transform.position;
        }

        private static AIUnitState GetState(UnitController unit)
        {
            AIUnitBrain brain = unit == null
                ? null
                : unit.GetComponent<AIUnitBrain>();
            return brain == null ? AIUnitState.Disabled : brain.State;
        }

        private static Vector3 GetVelocity(NavMeshAgent agent)
        {
            return agent == null || !agent.isOnNavMesh
                ? default
                : agent.velocity;
        }
    }
}
