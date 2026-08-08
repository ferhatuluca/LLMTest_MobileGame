using System;

namespace MonstersVsZombies.Units.Player
{
    public static class WeaponIndexCycle
    {
        public static int GetNextIndex(int currentIndex, int weaponCount)
        {
            Validate(currentIndex, weaponCount);
            return (currentIndex + 1) % weaponCount;
        }

        public static int GetPreviousIndex(int currentIndex, int weaponCount)
        {
            Validate(currentIndex, weaponCount);
            return (currentIndex - 1 + weaponCount) % weaponCount;
        }

        private static void Validate(int currentIndex, int weaponCount)
        {
            if (weaponCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weaponCount), "Weapon count must be positive.");
            }

            if (currentIndex < 0 || currentIndex >= weaponCount)
            {
                throw new ArgumentOutOfRangeException(nameof(currentIndex));
            }
        }
    }
}
