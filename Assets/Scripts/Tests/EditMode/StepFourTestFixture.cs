using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Damage;
using MonstersVsZombies.Combat.Health;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    internal sealed class StepFourTestFactory : IDisposable
    {
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();

        public StepFourUnitFixture CreateActiveUnit(
            long spawnId,
            UnitFaction faction,
            float maximumHealth = 100f)
        {
            StepFourUnitFixture fixture = CreatePreparedUnit(
                spawnId,
                faction,
                maximumHealth);
            Activate(fixture);
            return fixture;
        }

        public StepFourUnitFixture CreatePreparedUnit(
            long spawnId,
            UnitFaction faction,
            float maximumHealth = 100f)
        {
            GameObject gameObject = CreateGameObject($"{faction}_{spawnId}");
            gameObject.SetActive(false);
            HealthController healthController = gameObject.AddComponent<HealthController>();
            StatusEffectController statusEffectController =
                gameObject.AddComponent<StatusEffectController>();
            DamageController damageController = gameObject.AddComponent<DamageController>();
            UnitLifecycleController lifecycleController =
                gameObject.AddComponent<UnitLifecycleController>();
            UnitController unitController = gameObject.AddComponent<UnitController>();
            StepFourUnitFixture fixture = new StepFourUnitFixture(
                gameObject,
                unitController,
                healthController,
                statusEffectController,
                damageController,
                lifecycleController);

            UnitDefinition definition = CreateUnitDefinition(
                $"{faction}{spawnId}",
                faction,
                maximumHealth);
            Assert.That(
                lifecycleController.ConfigureSpawn(definition, new SpawnId(spawnId)),
                Is.True);
            Assert.That(lifecycleController.PrepareForSpawn(), Is.True);
            return fixture;
        }

        public void Activate(StepFourUnitFixture fixture)
        {
            fixture.GameObject.SetActive(true);
            Assert.That(fixture.Lifecycle.CompleteSpawn(), Is.True);
            Assert.That(fixture.Lifecycle.ActivateSpawn(), Is.True);
        }

        public void Respawn(
            StepFourUnitFixture fixture,
            long spawnId,
            UnitFaction faction,
            float maximumHealth = 100f)
        {
            UnitDefinition definition = CreateUnitDefinition(
                $"{faction}{spawnId}",
                faction,
                maximumHealth);
            Assert.That(
                fixture.Lifecycle.ConfigureSpawn(definition, new SpawnId(spawnId)),
                Is.True);
            Assert.That(fixture.Lifecycle.PrepareForSpawn(), Is.True);
            Activate(fixture);
        }

        public DamageTargetProxy AddHurtbox(
            StepFourUnitFixture fixture,
            string objectName,
            Vector3 localPosition)
        {
            GameObject hurtbox = CreateGameObject(objectName);
            hurtbox.transform.SetParent(fixture.GameObject.transform, false);
            hurtbox.transform.localPosition = localPosition;
            hurtbox.layer = LayerMask.NameToLayer("UnitTarget");
            SphereCollider sphereCollider = hurtbox.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = 0.25f;
            DamageTargetProxy targetProxy = hurtbox.AddComponent<DamageTargetProxy>();
            targetProxy.CacheOwnerReferences();
            return targetProxy;
        }

        public GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        public T CreateComponent<T>(string objectName) where T : Component
        {
            return CreateGameObject(objectName).AddComponent<T>();
        }

        public void Dispose()
        {
            for (int objectIndex = _createdObjects.Count - 1;
                 objectIndex >= 0;
                 objectIndex--)
            {
                UnityEngine.Object createdObject = _createdObjects[objectIndex];
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        private UnitDefinition CreateUnitDefinition(
            string id,
            UnitFaction faction,
            float maximumHealth)
        {
            UnitDefinition definition;
            if (faction == UnitFaction.Player)
            {
                definition = CreateScriptableObject<PlayerUnitDefinition>();
            }
            else
            {
                AttackDefinition attackDefinition =
                    CreateScriptableObject<AttackDefinition>();
                SetProperty(
                    attackDefinition,
                    nameof(AttackDefinition.AttackId),
                    new AttackId($"{id}Attack"));
                SetProperty(attackDefinition, nameof(AttackDefinition.Damage), 1f);
                SetProperty(attackDefinition, nameof(AttackDefinition.AttackRange), 1f);
                SetProperty(attackDefinition, nameof(AttackDefinition.CooldownDuration), 1f);
                SetProperty(attackDefinition, nameof(AttackDefinition.WindupDuration), 0f);
                SetProperty(attackDefinition, nameof(AttackDefinition.RecoveryDuration), 0f);
                SetProperty(
                    attackDefinition,
                    nameof(AttackDefinition.DeliveryType),
                    AttackDeliveryType.Melee);
                SetProperty(
                    attackDefinition,
                    nameof(AttackDefinition.AcceptedHitEffect),
                    new AcceptedHitEffectConfiguration());

                AIUnitDefinition aiDefinition = CreateScriptableObject<AIUnitDefinition>();
                SetProperty(aiDefinition, nameof(AIUnitDefinition.ChaseRange), 5f);
                SetProperty(
                    aiDefinition,
                    nameof(AIUnitDefinition.DefaultAttackDefinition),
                    attackDefinition);
                definition = aiDefinition;
            }

            SetProperty(definition, nameof(UnitDefinition.UnitId), new UnitId(id));
            SetProperty(definition, nameof(UnitDefinition.DisplayName), id);
            SetProperty(definition, nameof(UnitDefinition.Faction), faction);
            SetProperty(definition, nameof(UnitDefinition.MaximumHealth), maximumHealth);
            SetProperty(definition, nameof(UnitDefinition.MoveSpeed), 5f);
            SetProperty(definition, nameof(UnitDefinition.TurnSpeed), 360f);
            SetProperty(definition, nameof(UnitDefinition.PoolId), new PoolId($"{id}Pool"));
            return definition;
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            Type type = target.GetType();
            string fieldName = $"<{propertyName}>k__BackingField";
            while (type != null)
            {
                FieldInfo fieldInfo = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }
    }

    internal sealed class StepFourUnitFixture
    {
        public GameObject GameObject { get; }
        public UnitController Unit { get; }
        public HealthController Health { get; }
        public StatusEffectController StatusEffects { get; }
        public DamageController Damage { get; }
        public UnitLifecycleController Lifecycle { get; }

        public StepFourUnitFixture(
            GameObject gameObject,
            UnitController unit,
            HealthController health,
            StatusEffectController statusEffects,
            DamageController damage,
            UnitLifecycleController lifecycle)
        {
            GameObject = gameObject;
            Unit = unit;
            Health = health;
            StatusEffects = statusEffects;
            Damage = damage;
            Lifecycle = lifecycle;
        }
    }
}
