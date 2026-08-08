using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    [CreateAssetMenu(menuName = "Monsters vs Zombies/Units/Player Definition")]
    public sealed class PlayerUnitDefinition : UnitDefinition
    {
        public override ValidationResult Validate()
        {
            ValidationResult result = base.Validate();
            if (Faction != UnitFaction.Player)
            {
                result.AddError(
                    ValidationCode.InvalidFaction,
                    $"{name} must use the Player faction.");
            }

            return result;
        }
    }
}
