namespace MonstersVsZombies.Units
{
    /// <summary>
    /// Centralizes the Player/Ally versus Enemy hostility matrix used by every
    /// targeting and damage-delivery path.
    /// </summary>
    public static class FactionRules
    {
        public static bool AreHostile(UnitFaction attackerFaction, UnitFaction targetFaction)
        {
            switch (attackerFaction)
            {
                case UnitFaction.Player:
                case UnitFaction.Ally:
                    return targetFaction == UnitFaction.Enemy;
                case UnitFaction.Enemy:
                    return targetFaction == UnitFaction.Player || targetFaction == UnitFaction.Ally;
                default:
                    return false;
            }
        }
    }
}
