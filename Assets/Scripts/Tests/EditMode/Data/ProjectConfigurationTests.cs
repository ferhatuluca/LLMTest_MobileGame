using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Units;
using NUnit.Framework;
using UnityEditor;

namespace MonstersVsZombies.Tests.EditMode.Data
{
    public sealed class ProjectConfigurationTests
    {
        [Test]
        public void Validate_CombatCatalogsHaveNoErrors()
        {
            PoolCatalog poolCatalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");
            UnitCatalog unitCatalog = AssetDatabase.LoadAssetAtPath<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");

            Assert.That(poolCatalog, Is.Not.Null);
            Assert.That(unitCatalog, Is.Not.Null);
            Assert.That(poolCatalog.Validate().IsValid, Is.True);
            Assert.That(unitCatalog.Validate().IsValid, Is.True);
        }

        [Test]
        public void Validate_EveryUnitDefinitionIsValidAndCatalogued()
        {
            UnitCatalog unitCatalog = AssetDatabase.LoadAssetAtPath<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");
            string[] definitionGuids = AssetDatabase.FindAssets(
                "t:UnitDefinition",
                new[] { "Assets/Data/Units" });

            Assert.That(definitionGuids, Is.Not.Empty);
            foreach (string definitionGuid in definitionGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(definitionGuid);
                UnitDefinition definition =
                    AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.That(definition.Validate().IsValid, Is.True, path);
                Assert.That(
                    unitCatalog.TryGetDefinition(
                        definition.UnitId,
                        out UnitDefinition cataloguedDefinition),
                    Is.True,
                    path);
                Assert.That(cataloguedDefinition, Is.SameAs(definition), path);
            }
        }

        [Test]
        public void Validate_EveryPoolPrefabHasPooledEntityAtItsRoot()
        {
            PoolCatalog poolCatalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(
                "Assets/Data/Catalogs/PC_ProjectilePools.asset");

            for (int index = 0; index < poolCatalog.Count; index++)
            {
                PoolCatalogEntry entry = poolCatalog.GetEntry(index);
                Assert.That(entry.Prefab, Is.Not.Null, entry.PoolId.ToString());
                Assert.That(
                    entry.Prefab.GetComponent<PooledEntity>(),
                    Is.Not.Null,
                    entry.PoolId.ToString());
            }
        }

        [Test]
        public void Validate_FactionsContainConcreteUnitsForBothSides()
        {
            UnitCatalog unitCatalog = AssetDatabase.LoadAssetAtPath<UnitCatalog>(
                "Assets/Data/Catalogs/UC_CombatSandbox.asset");
            int allyCount = 0;
            int enemyCount = 0;
            for (int index = 0; index < unitCatalog.Count; index++)
            {
                UnitDefinition definition = unitCatalog.GetEntry(index).Definition;
                allyCount += definition.Faction == UnitFaction.Ally ? 1 : 0;
                enemyCount += definition.Faction == UnitFaction.Enemy ? 1 : 0;
            }

            Assert.That(allyCount, Is.GreaterThan(0));
            Assert.That(enemyCount, Is.GreaterThan(0));
        }
    }
}
