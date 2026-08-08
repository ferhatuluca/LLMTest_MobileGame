using MonstersVsZombies.Combat.Interaction;

namespace MonstersVsZombies.Combat.Attacks
{
    public sealed class StunnerHitSchedule
    {
        private const int k_HitsBetweenStuns = 3;

        public int SuccessfulHitCount { get; private set; }
        public bool ShouldStunNextSuccessfulHit => SuccessfulHitCount % k_HitsBetweenStuns == 0;

        public void RecordInteraction(InteractionResult result)
        {
            if (result.IsApplied)
            {
                SuccessfulHitCount++;
            }
        }

        public void Reset()
        {
            SuccessfulHitCount = 0;
        }
    }
}
