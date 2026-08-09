using System;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Units.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepTenLiveVerification
    {
        [MenuItem("Tools/Monsters vs Zombies/Step 10/Report Live Player State %#r")]
        public static void ReportLivePlayerState()
        {
            CombatSandboxBootstrap bootstrap = RequireLiveBootstrap();
            PlayerWeaponController weapons =
                bootstrap.InitialPlayer.GetComponent<PlayerWeaponController>();
            float enemyHealth = bootstrap.InitialStationaryEnemy == null
                ? 0f
                : bootstrap.InitialStationaryEnemy.HealthController.CurrentHealth;
            Debug.Log(
                $"[StepTenLiveState] Position={bootstrap.InitialPlayer.transform.position:F3}; " +
                $"Weapon={weapons.CurrentWeapon.DisplayName}; " +
                $"EnemyActive={bootstrap.InitialStationaryEnemy != null && bootstrap.InitialStationaryEnemy.IsActive}; " +
                $"EnemyHealth={enemyHealth:0.##}.");
        }

        [MenuItem("Tools/Monsters vs Zombies/Step 10/Hold On-Screen Stick Forward %#h")]
        public static void HoldOnScreenStickForward()
        {
            OnScreenStick stick = RequireLiveOnScreenStick();
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                throw new InvalidOperationException(
                    "The live CombatSandbox has no EventSystem.");
            }

            RectTransform stickTransform = stick.transform as RectTransform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(
                null,
                stickTransform.position);
            PointerEventData pointerEvent = new PointerEventData(eventSystem)
            {
                position = center
            };
            stick.OnPointerDown(pointerEvent);
            pointerEvent.position = center +
                                    (Vector2.up * stick.movementRange);
            stick.OnDrag(pointerEvent);
            Debug.Log(
                "[StepTenLiveState] On-screen stick is holding the shared left-stick control forward.");
        }

        [MenuItem("Tools/Monsters vs Zombies/Step 10/Release On-Screen Stick %#j")]
        public static void ReleaseOnScreenStick()
        {
            OnScreenStick stick = RequireLiveOnScreenStick();
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                throw new InvalidOperationException(
                    "The live CombatSandbox has no EventSystem.");
            }

            stick.OnPointerUp(new PointerEventData(eventSystem));
            Debug.Log("[StepTenLiveState] On-screen stick released.");
        }

        private static CombatSandboxBootstrap RequireLiveBootstrap()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode in CombatSandbox before live verification.");
            }

            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "__Systems")
                {
                    continue;
                }

                Transform bootstrapTransform =
                    root.transform.Find("CombatSandboxBootstrap");
                CombatSandboxBootstrap bootstrap = bootstrapTransform == null
                    ? null
                    : bootstrapTransform.GetComponent<CombatSandboxBootstrap>();
                if (bootstrap != null && bootstrap.IsInitialized &&
                    bootstrap.InitialPlayer != null)
                {
                    return bootstrap;
                }
            }

            throw new InvalidOperationException(
                "The live CombatSandbox Player bootstrap is not ready.");
        }

        private static OnScreenStick RequireLiveOnScreenStick()
        {
            RequireLiveBootstrap();
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "UI")
                {
                    OnScreenStick stick =
                        root.GetComponentInChildren<OnScreenStick>(true);
                    if (stick != null)
                    {
                        return stick;
                    }
                }
            }

            throw new InvalidOperationException(
                "The live CombatSandbox OnScreenStick is missing.");
        }

    }
}
