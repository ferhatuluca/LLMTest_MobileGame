using System;
using System.Collections.Generic;
using System.Reflection;
using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Projectiles;
using MonstersVsZombies.Combat.StatusEffects;
using MonstersVsZombies.Core;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using UnityEditor;
using UnityEngine;

namespace MonstersVsZombies.Editor.Validation
{
    public static class StepEightAssetSetup
    {
        private const string k_AttackAssetFolder = "Assets/Data/Attacks";
        private const string k_ProjectileAssetFolder = "Assets/Data/Projectiles";
        private const string k_CatalogAssetFolder = "Assets/Data/Catalogs";
        private const string k_ProjectilePrefabFolder = "Assets/Prefabs/Projectiles";
        private const string k_EffectPrefabFolder = "Assets/Prefabs/Effects";
        private const int k_SweepCapacity = 32;
        private const int k_GrenadeAreaCapacity = 64;

        private static readonly PoolId s_bulletPoolId = new PoolId("Bullet");
        private static readonly PoolId s_fireballPoolId = new PoolId("Fireball");
        private static readonly PoolId s_grenadePoolId = new PoolId("Grenade");
        private static readonly PoolId s_laserBeamPoolId = new PoolId("LaserBeam");

        [MenuItem("Tools/Monsters vs Zombies/Step 8/Create and Verify Assets")]
        public static void CreateAndVerifyAssets()
        {
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer < 0)
            {
                throw new InvalidOperationException(
                    "Step 8 requires the Projectile layer from Step 0.");
            }

            ProjectileDefinition bulletDefinition = CreateProjectileDefinition(
                $"{k_ProjectileAssetFolder}/PD_Bullet.asset",
                s_bulletPoolId,
                AttackDeliveryType.Projectile,
                20f,
                2f,
                0.10f,
                0f,
                0f,
                0f);
            ProjectileDefinition fireballDefinition = CreateProjectileDefinition(
                $"{k_ProjectileAssetFolder}/PD_Fireball.asset",
                s_fireballPoolId,
                AttackDeliveryType.Projectile,
                12f,
                3f,
                0.25f,
                0f,
                0f,
                0f);
            ProjectileDefinition grenadeDefinition = CreateProjectileDefinition(
                $"{k_ProjectileAssetFolder}/PD_Grenade.asset",
                s_grenadePoolId,
                AttackDeliveryType.Grenade,
                12f,
                0f,
                0.20f,
                1f,
                3f,
                2f);

            GameObject bulletPrefab = CreateKinematicProjectilePrefab(
                $"{k_ProjectilePrefabFolder}/PF_Projectile_Bullet.prefab",
                "PF_Projectile_Bullet",
                projectileLayer,
                0.10f);
            GameObject fireballPrefab = CreateKinematicProjectilePrefab(
                $"{k_ProjectilePrefabFolder}/PF_Projectile_Fireball.prefab",
                "PF_Projectile_Fireball",
                projectileLayer,
                0.25f);
            GameObject grenadePrefab = CreateGrenadePrefab(
                $"{k_ProjectilePrefabFolder}/PF_Projectile_Grenade.prefab",
                projectileLayer,
                0.20f);
            GameObject laserBeamPrefab = CreateLaserBeamPrefab(
                $"{k_EffectPrefabFolder}/PF_Effect_LaserBeam.prefab",
                projectileLayer);

            CreateAttackDefinition(
                $"{k_AttackAssetFolder}/AD_BasicMelee.asset",
                "BasicMelee",
                10f,
                1.8f,
                1f,
                0.25f,
                0.25f,
                AttackDeliveryType.Melee,
                null,
                "Direct");
            CreateAttackDefinition(
                $"{k_AttackAssetFolder}/AD_BasicBullet.asset",
                "BasicBullet",
                8f,
                8f,
                1.2f,
                0.20f,
                0.20f,
                AttackDeliveryType.Projectile,
                bulletDefinition,
                "Direct");
            CreateAttackDefinition(
                $"{k_AttackAssetFolder}/AD_DragonFireball.asset",
                "DragonFireball",
                14f,
                10f,
                1.6f,
                0.40f,
                0.30f,
                AttackDeliveryType.Projectile,
                fireballDefinition,
                "Direct");
            CreateAttackDefinition(
                $"{k_AttackAssetFolder}/AD_PlayerGrenadeGun.asset",
                "PlayerGrenadeGun",
                25f,
                9f,
                1.8f,
                0.25f,
                0.30f,
                AttackDeliveryType.Grenade,
                grenadeDefinition,
                "Explosion");
            CreateAttackDefinition(
                $"{k_AttackAssetFolder}/AD_PlayerSpaceGun.asset",
                "PlayerSpaceGun",
                18f,
                12f,
                1f,
                0.10f,
                0.15f,
                AttackDeliveryType.Hitscan,
                null,
                "Direct");

            PoolCatalog poolCatalog = LoadOrCreateAsset<PoolCatalog>(
                $"{k_CatalogAssetFolder}/PC_ProjectilePools.asset");
            List<PoolCatalogEntry> poolEntries =
                GetPreservedPoolEntries(poolCatalog);
            poolEntries.AddRange(new[]
            {
                CreatePoolEntry(s_bulletPoolId, bulletPrefab, 50, 200),
                CreatePoolEntry(s_fireballPoolId, fireballPrefab, 30, 100),
                CreatePoolEntry(s_grenadePoolId, grenadePrefab, 20, 60),
                CreatePoolEntry(s_laserBeamPoolId, laserBeamPrefab, 20, 60)
            });
            SetField(
                poolCatalog,
                "_entries",
                poolEntries.ToArray());
            EditorUtility.SetDirty(poolCatalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyAssets(poolCatalog);
            Debug.Log(
                "[StepEightAssetSetup] Created and verified projectile, beam, pool, and attack assets.");
        }

        private static ProjectileDefinition CreateProjectileDefinition(
            string assetPath,
            PoolId poolId,
            AttackDeliveryType deliveryType,
            float speed,
            float maximumLifetime,
            float collisionRadius,
            float gravityScale,
            float explosionRadius,
            float fuseDuration)
        {
            ProjectileDefinition definition =
                LoadOrCreateAsset<ProjectileDefinition>(assetPath);
            SetAutoProperty(definition, nameof(ProjectileDefinition.PoolId), poolId);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.CompatibleDeliveryType),
                deliveryType);
            SetAutoProperty(definition, nameof(ProjectileDefinition.Speed), speed);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.MaximumLifetime),
                maximumLifetime);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.CollisionRadius),
                collisionRadius);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.GravityScale),
                gravityScale);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.ExplosionRadius),
                explosionRadius);
            SetAutoProperty(
                definition,
                nameof(ProjectileDefinition.FuseDuration),
                fuseDuration);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static AttackDefinition CreateAttackDefinition(
            string assetPath,
            string attackId,
            float damage,
            float attackRange,
            float cooldownDuration,
            float windupDuration,
            float recoveryDuration,
            AttackDeliveryType deliveryType,
            ProjectileDefinition projectileDefinition,
            string damageCategory)
        {
            AttackDefinition definition =
                LoadOrCreateAsset<AttackDefinition>(assetPath);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AttackId),
                new AttackId(attackId));
            SetAutoProperty(definition, nameof(AttackDefinition.Damage), damage);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AttackRange),
                attackRange);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.CooldownDuration),
                cooldownDuration);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.WindupDuration),
                windupDuration);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.RecoveryDuration),
                recoveryDuration);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.DeliveryType),
                deliveryType);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.ProjectileDefinition),
                projectileDefinition);
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.AcceptedHitEffect),
                new AcceptedHitEffectConfiguration(StatusEffectType.None, 0f));
            SetAutoProperty(
                definition,
                nameof(AttackDefinition.DamageCategoryId),
                new DamageCategoryId(damageCategory));
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static GameObject CreateKinematicProjectilePrefab(
            string prefabPath,
            string prefabName,
            int projectileLayer,
            float visualRadius)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = prefabName;
            root.SetActive(false);
            root.layer = projectileLayer;
            root.transform.localScale = Vector3.one * (visualRadius * 2f);
            root.AddComponent<PooledEntity>();
            KinematicProjectileMovement movement =
                root.AddComponent<KinematicProjectileMovement>();
            if (!movement.InitializeSweepCapacity(k_SweepCapacity))
            {
                throw new InvalidOperationException(
                    $"Could not configure {prefabName} sweep capacity.");
            }

            root.AddComponent<ProjectileController>();
            root.SetActive(true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateGrenadePrefab(
            string prefabPath,
            int projectileLayer,
            float visualRadius)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "PF_Projectile_Grenade";
            root.SetActive(false);
            root.layer = projectileLayer;
            root.transform.localScale = Vector3.one * (visualRadius * 2f);
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<PooledEntity>();
            GrenadeProjectileMovement movement =
                root.AddComponent<GrenadeProjectileMovement>();
            if (!movement.InitializeAreaCapacity(k_GrenadeAreaCapacity))
            {
                throw new InvalidOperationException(
                    "Could not configure grenade area capacity.");
            }

            root.AddComponent<ProjectileController>();
            root.SetActive(true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateLaserBeamPrefab(
            string prefabPath,
            int projectileLayer)
        {
            GameObject root = new GameObject("PF_Effect_LaserBeam");
            root.SetActive(false);
            root.layer = projectileLayer;
            root.AddComponent<PooledEntity>();
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.layer = projectileLayer;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(0.04f, 0.04f, 1f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            LaserBeamPresentationController beamController =
                root.AddComponent<LaserBeamPresentationController>();
            SetAutoProperty(
                beamController,
                nameof(LaserBeamPresentationController.VisualTransform),
                visual.transform);
            SetAutoProperty(
                beamController,
                nameof(LaserBeamPresentationController.Lifetime),
                0.12f);
            root.SetActive(true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static PoolCatalogEntry CreatePoolEntry(
            PoolId poolId,
            GameObject prefab,
            int initialPrewarmCount,
            int maximumInactiveRetainedCount)
        {
            PoolCatalogEntry entry = new PoolCatalogEntry();
            SetAutoProperty(entry, nameof(PoolCatalogEntry.PoolId), poolId);
            SetAutoProperty(entry, nameof(PoolCatalogEntry.Prefab), prefab);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.InitialPrewarmCount),
                initialPrewarmCount);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumInactiveRetainedCount),
                maximumInactiveRetainedCount);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.CapacityPolicy),
                PoolCapacityPolicy.Expandable);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.MaximumActiveCount),
                0);
            SetAutoProperty(
                entry,
                nameof(PoolCatalogEntry.EnableCollectionChecks),
                true);
            return entry;
        }

        private static void VerifyAssets(PoolCatalog poolCatalog)
        {
            if (poolCatalog == null || !poolCatalog.Validate().IsValid ||
                poolCatalog.Count < 4)
            {
                throw new InvalidOperationException(
                    "Step 8 projectile pool catalog validation failed.");
            }

            PoolId[] requiredPoolIds =
            {
                s_bulletPoolId,
                s_fireballPoolId,
                s_grenadePoolId,
                s_laserBeamPoolId
            };
            foreach (PoolId requiredPoolId in requiredPoolIds)
            {
                if (!poolCatalog.TryGetEntry(requiredPoolId, out _))
                {
                    throw new InvalidOperationException(
                        $"Step 8 pool '{requiredPoolId}' is missing.");
                }
            }

            string[] projectileDefinitionGuids = AssetDatabase.FindAssets(
                "t:ProjectileDefinition",
                new[] { k_ProjectileAssetFolder });
            string[] attackDefinitionGuids = AssetDatabase.FindAssets(
                "t:AttackDefinition",
                new[] { k_AttackAssetFolder });
            if (projectileDefinitionGuids.Length < 3 ||
                attackDefinitionGuids.Length < 5)
            {
                throw new InvalidOperationException(
                    "Step 8 definition assets are incomplete.");
            }

            for (int entryIndex = 0;
                 entryIndex < poolCatalog.Count;
                 entryIndex++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(entryIndex);
                if (entry.Prefab == null ||
                    entry.Prefab.GetComponent<PooledEntity>() == null)
                {
                    throw new InvalidOperationException(
                        $"Pool entry {entryIndex} has an invalid prefab.");
                }
            }
        }

        private static T LoadOrCreateAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static List<PoolCatalogEntry> GetPreservedPoolEntries(
            PoolCatalog poolCatalog)
        {
            List<PoolCatalogEntry> preservedEntries =
                new List<PoolCatalogEntry>();
            for (int entryIndex = 0;
                 entryIndex < poolCatalog.Count;
                 entryIndex++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(entryIndex);
                if (entry != null && entry.PoolId != s_bulletPoolId &&
                    entry.PoolId != s_fireballPoolId &&
                    entry.PoolId != s_grenadePoolId &&
                    entry.PoolId != s_laserBeamPoolId)
                {
                    preservedEntries.Add(entry);
                }
            }

            return preservedEntries;
        }

        private static void SetAutoProperty<TValue>(
            object target,
            string propertyName,
            TValue value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void SetField<TValue>(
            object target,
            string fieldName,
            TValue value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
