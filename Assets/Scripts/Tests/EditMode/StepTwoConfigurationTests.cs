using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepTwoConfigurationTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (UnityEngine.Object createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ValidateUnitCatalog_RejectsDuplicateUnitIds()
        {
            PlayerUnitDefinition definition = CreatePlayerDefinition("Player", UnitFaction.Player);
            UnitCatalogEntry first = CreateUnitEntry(definition);
            UnitCatalogEntry duplicate = CreateUnitEntry(definition);
            UnitCatalog catalog = CreateScriptableObject<UnitCatalog>();
            SetField(catalog, "_entries", new[] { first, duplicate });

            ValidationResult result = catalog.Validate();

            Assert.That(result.HasError(ValidationCode.DuplicateUnitId), Is.True);
        }

        [Test]
        public void ValidatePoolCatalog_RejectsDuplicatePoolIdsAndMissingPrefabs()
        {
            PoolCatalogEntry first =
                CreatePoolEntry("Shared", null, 0, 10, PoolCapacityPolicy.Expandable, 0);
            PoolCatalogEntry duplicate =
                CreatePoolEntry("Shared", null, 0, 10, PoolCapacityPolicy.Expandable, 0);
            PoolCatalog catalog = CreateScriptableObject<PoolCatalog>();
            SetField(catalog, "_entries", new[] { first, duplicate });

            ValidationResult result = catalog.Validate();

            Assert.That(result.HasError(ValidationCode.DuplicatePoolId), Is.True);
            Assert.That(result.HasError(ValidationCode.MissingReference), Is.True);
        }

        [Test]
        public void ValidatePoolEntry_EnforcesPrewarmRetentionAndHardLimitRules()
        {
            GameObject prefab = CreateGameObject("PoolFixture");
            PoolCatalogEntry excessivePrewarm =
                CreatePoolEntry("Excessive", prefab, 11, 10, PoolCapacityPolicy.Expandable, 0);
            PoolCatalogEntry invalidHardLimit =
                CreatePoolEntry("Hard", prefab, 0, 10, PoolCapacityPolicy.HardActiveLimit, 0);

            ValidationResult prewarmResult = excessivePrewarm.Validate("Excessive");
            ValidationResult hardLimitResult = invalidHardLimit.Validate("Hard");

            Assert.That(prewarmResult.HasError(ValidationCode.PrewarmExceedsRetainedCount), Is.True);
            Assert.That(hardLimitResult.HasError(ValidationCode.InvalidActiveLimit), Is.True);
        }

        [Test]
        public void ValidatePoolEntry_AllowsZeroPrewarmForExpandablePool()
        {
            GameObject prefab = CreateGameObject("PoolFixture");
            PoolCatalogEntry entry =
                CreatePoolEntry("Valid", prefab, 0, 1, PoolCapacityPolicy.Expandable, 0);

            ValidationResult result = entry.Validate("Valid");

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidatePlayerDefinition_RequiresPlayerFactionAndContainsNoChaseData()
        {
            PlayerUnitDefinition valid = CreatePlayerDefinition("Player", UnitFaction.Player);
            PlayerUnitDefinition invalid = CreatePlayerDefinition("InvalidPlayer", UnitFaction.Enemy);

            ValidationResult validResult = valid.Validate();
            ValidationResult invalidResult = invalid.Validate();

            Assert.That(validResult.IsValid, Is.True);
            Assert.That(invalidResult.HasError(ValidationCode.InvalidFaction), Is.True);
            Assert.That(typeof(PlayerUnitDefinition).GetProperty("ChaseRange"), Is.Null);
            Assert.That(typeof(PlayerUnitDefinition).GetProperty("DefaultAttackDefinition"), Is.Null);
        }

        [Test]
        public void ValidateAIDefinition_RequiresAIFactionChaseRangeAndDefaultAttack()
        {
            AIUnitDefinition definition = CreateAIDefinition(
                "InvalidAI",
                UnitFaction.Player,
                0f,
                null);

            ValidationResult result = definition.Validate();

            Assert.That(result.HasError(ValidationCode.InvalidFaction), Is.True);
            Assert.That(result.HasError(ValidationCode.InvalidPositiveValue), Is.True);
            Assert.That(result.HasError(ValidationCode.MissingReference), Is.True);
        }

        [Test]
        public void ValidateAIDefinition_RejectsAttackRangeBeyondChaseRange()
        {
            AttackDefinition attack = CreateAttackDefinition(
                "LongAttack",
                AttackDeliveryType.Melee,
                null,
                10f,
                8f,
                1f,
                0f,
                0f);
            AIUnitDefinition definition = CreateAIDefinition(
                "ShortChase",
                UnitFaction.Enemy,
                5f,
                attack);

            ValidationResult result = definition.Validate();

            Assert.That(result.HasError(ValidationCode.AttackRangeExceedsChaseRange), Is.True);
        }

        [Test]
        public void ValidateAttackDefinition_RequiresProjectileForMovingDeliveries()
        {
            AttackDefinition projectileAttack = CreateAttackDefinition(
                "ProjectileWithoutDefinition",
                AttackDeliveryType.Projectile,
                null,
                10f,
                5f,
                1f,
                0f,
                0f);
            AttackDefinition grenadeAttack = CreateAttackDefinition(
                "GrenadeWithoutDefinition",
                AttackDeliveryType.Grenade,
                null,
                10f,
                5f,
                1f,
                0f,
                0f);

            Assert.That(
                projectileAttack.Validate().HasError(ValidationCode.MissingProjectileDefinition),
                Is.True);
            Assert.That(
                grenadeAttack.Validate().HasError(ValidationCode.MissingProjectileDefinition),
                Is.True);
        }

        [Test]
        public void ValidateAttackDefinition_RejectsUnspecifiedAndIncompatibleDelivery()
        {
            ProjectileDefinition grenade = CreateProjectileDefinition(
                "Grenade",
                AttackDeliveryType.Grenade,
                10f,
                0f,
                0.1f,
                0f,
                3f,
                2f);
            AttackDefinition unspecified = CreateAttackDefinition(
                "Unspecified",
                AttackDeliveryType.Unspecified,
                null,
                10f,
                5f,
                1f,
                0f,
                0f);
            AttackDefinition incompatible = CreateAttackDefinition(
                "Incompatible",
                AttackDeliveryType.Projectile,
                grenade,
                10f,
                5f,
                1f,
                0f,
                0f);

            Assert.That(unspecified.Validate().HasError(ValidationCode.InvalidDeliveryType), Is.True);
            Assert.That(incompatible.Validate().HasError(ValidationCode.IncompatibleDeliveryType), Is.True);
        }

        [Test]
        public void ValidateOptionalNumbers_AllowsZeroAndRejectsNegativeValues()
        {
            ProjectileDefinition validProjectile = CreateProjectileDefinition(
                "Bullet",
                AttackDeliveryType.Projectile,
                10f,
                2f,
                0.1f,
                0f,
                0f);
            AttackDefinition validAttack = CreateAttackDefinition(
                "Valid",
                AttackDeliveryType.Projectile,
                validProjectile,
                10f,
                5f,
                1f,
                0f,
                0f);
            ProjectileDefinition invalidProjectile = CreateProjectileDefinition(
                "Invalid",
                AttackDeliveryType.Projectile,
                10f,
                2f,
                0.1f,
                -1f,
                -2f,
                0f);
            AttackDefinition invalidWindup = CreateAttackDefinition(
                "InvalidWindup",
                AttackDeliveryType.Melee,
                null,
                10f,
                5f,
                1f,
                -0.1f,
                0f);
            AttackDefinition invalidRecovery = CreateAttackDefinition(
                "InvalidRecovery",
                AttackDeliveryType.Melee,
                null,
                10f,
                5f,
                1f,
                0f,
                -0.1f);
            GameObject poolPrefab = CreateGameObject("PoolFixture");
            PoolCatalogEntry invalidPrewarm = CreatePoolEntry(
                "InvalidPrewarm",
                poolPrefab,
                -1,
                1,
                PoolCapacityPolicy.Expandable,
                0);
            PoolCatalogEntry invalidRetention = CreatePoolEntry(
                "InvalidRetention",
                poolPrefab,
                0,
                0,
                PoolCapacityPolicy.Expandable,
                0);

            Assert.That(validProjectile.Validate().IsValid, Is.True);
            Assert.That(validAttack.Validate().IsValid, Is.True);
            Assert.That(
                invalidProjectile.Validate().HasError(ValidationCode.InvalidNonNegativeValue),
                Is.True);
            Assert.That(
                invalidWindup.Validate().HasError(ValidationCode.InvalidNonNegativeValue),
                Is.True);
            Assert.That(
                invalidRecovery.Validate().HasError(ValidationCode.InvalidNonNegativeValue),
                Is.True);
            Assert.That(
                invalidPrewarm.Validate("InvalidPrewarm")
                    .HasError(ValidationCode.InvalidNonNegativeValue),
                Is.True);
            Assert.That(
                invalidRetention.Validate("InvalidRetention")
                    .HasError(ValidationCode.InvalidPositiveValue),
                Is.True);
        }

        [Test]
        public void ValidateProjectileDefinition_AppliesSelectedDeliveryRequirements()
        {
            ProjectileDefinition validGrenade = CreateProjectileDefinition(
                "Grenade",
                AttackDeliveryType.Grenade,
                10f,
                0f,
                0.1f,
                0f,
                3f,
                2f);
            ProjectileDefinition grenadeWithoutFuse = CreateProjectileDefinition(
                "GrenadeWithoutFuse",
                AttackDeliveryType.Grenade,
                10f,
                0f,
                0.1f,
                0f,
                3f,
                0f);
            ProjectileDefinition hitscan = CreateProjectileDefinition(
                "Hitscan",
                AttackDeliveryType.Hitscan,
                10f,
                2f,
                0.1f,
                0f,
                0f);

            Assert.That(validGrenade.Validate().IsValid, Is.True);
            Assert.That(
                grenadeWithoutFuse.Validate().HasError(ValidationCode.InvalidPositiveValue),
                Is.True);
            Assert.That(hitscan.Validate().HasError(ValidationCode.InvalidDeliveryType), Is.True);
        }

        [Test]
        public void ValidateAcceptedHitEffect_RequiresContextualDuration()
        {
            AcceptedHitEffectConfiguration absent =
                new AcceptedHitEffectConfiguration(StatusEffectType.None, 0f);
            AcceptedHitEffectConfiguration invalidAbsent =
                new AcceptedHitEffectConfiguration(StatusEffectType.None, 1f);
            AcceptedHitEffectConfiguration stun =
                new AcceptedHitEffectConfiguration(StatusEffectType.Stun, 1f);
            AcceptedHitEffectConfiguration invalidStun =
                new AcceptedHitEffectConfiguration(StatusEffectType.Stun, 0f);

            Assert.That(absent.Validate("Absent").IsValid, Is.True);
            Assert.That(
                invalidAbsent.Validate("InvalidAbsent").HasError(ValidationCode.InvalidStatusEffect),
                Is.True);
            Assert.That(stun.Validate("Stun").IsValid, Is.True);
            Assert.That(
                invalidStun.Validate("InvalidStun").HasError(ValidationCode.InvalidPositiveValue),
                Is.True);
        }

        [Test]
        public void ValidateAttackDefinition_RejectsProjectileOnMeleeOrHitscan()
        {
            ProjectileDefinition projectile = CreateProjectileDefinition(
                "Projectile",
                AttackDeliveryType.Projectile,
                10f,
                2f,
                0.1f,
                0f,
                0f);
            AttackDefinition melee = CreateAttackDefinition(
                "Melee",
                AttackDeliveryType.Melee,
                projectile,
                10f,
                2f,
                1f,
                0f,
                0f);
            AttackDefinition hitscan = CreateAttackDefinition(
                "Hitscan",
                AttackDeliveryType.Hitscan,
                projectile,
                10f,
                2f,
                1f,
                0f,
                0f);

            Assert.That(melee.Validate().HasError(ValidationCode.IncompatibleDeliveryType), Is.True);
            Assert.That(hitscan.Validate().HasError(ValidationCode.IncompatibleDeliveryType), Is.True);
        }

        [Test]
        public void ValidateRequiredNumbers_RejectsZeroNaNAndInfinity()
        {
            AttackDefinition invalidDamage = CreateAttackDefinition(
                "InvalidDamage",
                AttackDeliveryType.Melee,
                null,
                0f,
                5f,
                1f,
                0f,
                0f);
            AttackDefinition invalidRange = CreateAttackDefinition(
                "InvalidRange",
                AttackDeliveryType.Melee,
                null,
                10f,
                float.NaN,
                1f,
                0f,
                0f);
            AttackDefinition invalidCooldown = CreateAttackDefinition(
                "InvalidCooldown",
                AttackDeliveryType.Melee,
                null,
                10f,
                5f,
                float.PositiveInfinity,
                0f,
                0f);

            Assert.That(
                invalidDamage.Validate().HasError(ValidationCode.InvalidPositiveValue),
                Is.True);
            Assert.That(
                invalidRange.Validate().HasError(ValidationCode.InvalidPositiveValue),
                Is.True);
            Assert.That(
                invalidCooldown.Validate().HasError(ValidationCode.InvalidPositiveValue),
                Is.True);
        }

        [Test]
        public void ValidateRequiredReferences_RejectsMissingWeaponAttackAndSandboxPlayer()
        {
            GameObject weaponVisual = CreateGameObject("WeaponVisual");
            WeaponDefinition weaponWithoutAttack = CreateScriptableObject<WeaponDefinition>();
            SetProperty(
                weaponWithoutAttack,
                nameof(WeaponDefinition.WeaponId),
                new WeaponId("Weapon"));
            SetProperty(weaponWithoutAttack, nameof(WeaponDefinition.DisplayName), "Weapon");
            SetProperty(
                weaponWithoutAttack,
                nameof(WeaponDefinition.WeaponVisualPrefab),
                weaponVisual);
            SetProperty(
                weaponWithoutAttack,
                nameof(WeaponDefinition.MuzzleSocketName),
                "Muzzle");
            SandboxSpawnConfiguration spawnConfiguration = CreateScriptableObject<SandboxSpawnConfiguration>();

            Assert.That(
                weaponWithoutAttack.Validate().HasError(ValidationCode.MissingReference),
                Is.True);
            Assert.That(
                spawnConfiguration.Validate().HasError(ValidationCode.MissingReference),
                Is.True);
        }

        [Test]
        public void ValidateWeaponDefinition_RejectsMissingVisualAndSocketIndependently()
        {
            AttackDefinition attack = CreateAttackDefinition(
                "Melee",
                AttackDeliveryType.Melee,
                null,
                10f,
                2f,
                1f,
                0f,
                0f);
            WeaponDefinition weapon = CreateScriptableObject<WeaponDefinition>();
            SetProperty(weapon, nameof(WeaponDefinition.WeaponId), new WeaponId("Weapon"));
            SetProperty(weapon, nameof(WeaponDefinition.DisplayName), "Weapon");
            SetProperty(weapon, nameof(WeaponDefinition.AttackDefinition), attack);

            ValidationResult result = weapon.Validate();

            Assert.That(result.HasError(ValidationCode.MissingReference), Is.True);
            Assert.That(result.HasError(ValidationCode.MissingSocketName), Is.True);
        }

        [Test]
        public void ValidateRequiredReferences_RejectsMissingCatalogAndInitialUnitDefinitions()
        {
            UnitCatalogEntry catalogEntry = new UnitCatalogEntry();
            InitialUnitSpawnEntry initialUnit = new InitialUnitSpawnEntry();
            UnitCatalog catalog = CreateScriptableObject<UnitCatalog>();
            SetField(catalog, "_entries", new UnitCatalogEntry[] { null });

            Assert.That(
                catalogEntry.Validate("CatalogEntry").HasError(ValidationCode.MissingReference),
                Is.True);
            Assert.That(
                initialUnit.Validate("InitialUnit").HasError(ValidationCode.MissingReference),
                Is.True);
            Assert.That(catalog.Validate().HasError(ValidationCode.MissingReference), Is.True);
        }

        private PlayerUnitDefinition CreatePlayerDefinition(string id, UnitFaction faction)
        {
            PlayerUnitDefinition definition = CreateScriptableObject<PlayerUnitDefinition>();
            ConfigureUnit(definition, id, faction);
            return definition;
        }

        private AIUnitDefinition CreateAIDefinition(
            string id,
            UnitFaction faction,
            float chaseRange,
            AttackDefinition defaultAttack)
        {
            AIUnitDefinition definition = CreateScriptableObject<AIUnitDefinition>();
            ConfigureUnit(definition, id, faction);
            SetProperty(definition, nameof(AIUnitDefinition.ChaseRange), chaseRange);
            SetProperty(
                definition,
                nameof(AIUnitDefinition.DefaultAttackDefinition),
                defaultAttack);
            return definition;
        }

        private void ConfigureUnit(UnitDefinition definition, string id, UnitFaction faction)
        {
            SetProperty(definition, nameof(UnitDefinition.UnitId), new UnitId(id));
            SetProperty(definition, nameof(UnitDefinition.DisplayName), id);
            SetProperty(definition, nameof(UnitDefinition.Faction), faction);
            SetProperty(definition, nameof(UnitDefinition.MaximumHealth), 100f);
            SetProperty(definition, nameof(UnitDefinition.MoveSpeed), 5f);
            SetProperty(definition, nameof(UnitDefinition.TurnSpeed), 360f);
            SetProperty(definition, nameof(UnitDefinition.PoolId), new PoolId(id));
        }

        private AttackDefinition CreateAttackDefinition(
            string id,
            AttackDeliveryType deliveryType,
            ProjectileDefinition projectileDefinition,
            float damage,
            float range,
            float cooldown,
            float windup,
            float recovery)
        {
            AttackDefinition definition = CreateScriptableObject<AttackDefinition>();
            SetProperty(definition, nameof(AttackDefinition.AttackId), new AttackId(id));
            SetProperty(definition, nameof(AttackDefinition.DeliveryType), deliveryType);
            SetProperty(definition, nameof(AttackDefinition.ProjectileDefinition), projectileDefinition);
            SetProperty(definition, nameof(AttackDefinition.Damage), damage);
            SetProperty(definition, nameof(AttackDefinition.AttackRange), range);
            SetProperty(definition, nameof(AttackDefinition.CooldownDuration), cooldown);
            SetProperty(definition, nameof(AttackDefinition.WindupDuration), windup);
            SetProperty(definition, nameof(AttackDefinition.RecoveryDuration), recovery);
            SetProperty(
                definition,
                nameof(AttackDefinition.AcceptedHitEffect),
                new AcceptedHitEffectConfiguration());
            return definition;
        }

        private ProjectileDefinition CreateProjectileDefinition(
            string id,
            AttackDeliveryType deliveryType,
            float speed,
            float lifetime,
            float collisionRadius,
            float gravityScale,
            float explosionRadius,
            float fuseDuration = 0f)
        {
            ProjectileDefinition definition = CreateScriptableObject<ProjectileDefinition>();
            SetProperty(definition, nameof(ProjectileDefinition.PoolId), new PoolId(id));
            SetProperty(
                definition,
                nameof(ProjectileDefinition.CompatibleDeliveryType),
                deliveryType);
            SetProperty(definition, nameof(ProjectileDefinition.Speed), speed);
            SetProperty(definition, nameof(ProjectileDefinition.MaximumLifetime), lifetime);
            SetProperty(definition, nameof(ProjectileDefinition.CollisionRadius), collisionRadius);
            SetProperty(definition, nameof(ProjectileDefinition.GravityScale), gravityScale);
            SetProperty(definition, nameof(ProjectileDefinition.ExplosionRadius), explosionRadius);
            SetProperty(definition, nameof(ProjectileDefinition.FuseDuration), fuseDuration);
            return definition;
        }

        private PoolCatalogEntry CreatePoolEntry(
            string id,
            GameObject prefab,
            int prewarm,
            int retained,
            PoolCapacityPolicy policy,
            int hardLimit)
        {
            PoolCatalogEntry entry = new PoolCatalogEntry();
            SetProperty(entry, nameof(PoolCatalogEntry.PoolId), new PoolId(id));
            SetProperty(entry, nameof(PoolCatalogEntry.Prefab), prefab);
            SetProperty(entry, nameof(PoolCatalogEntry.InitialPrewarmCount), prewarm);
            SetProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                retained);
            SetProperty(entry, nameof(PoolCatalogEntry.CapacityPolicy), policy);
            SetProperty(entry, nameof(PoolCatalogEntry.MaximumActiveCount), hardLimit);
            SetProperty(entry, nameof(PoolCatalogEntry.EnableCollectionChecks), true);
            return entry;
        }

        private UnitCatalogEntry CreateUnitEntry(UnitDefinition definition)
        {
            UnitCatalogEntry entry = new UnitCatalogEntry();
            SetProperty(entry, nameof(UnitCatalogEntry.Definition), definition);
            return entry;
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private GameObject CreateGameObject(string objectName)
        {
            GameObject instance = new GameObject(objectName);
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            Type type = target.GetType();
            string fieldName = $"<{propertyName}>k__BackingField";
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
