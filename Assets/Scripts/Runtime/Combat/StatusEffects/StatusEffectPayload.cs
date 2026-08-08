namespace MonstersVsZombies.Combat.StatusEffects
{
    public enum StatusEffectType
    {
        None,
        Stun
    }

    public readonly struct StatusEffectPayload
    {
        public StatusEffectType Type { get; }
        public float Duration { get; }
        public bool IsValid =>
            Type != StatusEffectType.None &&
            System.Enum.IsDefined(typeof(StatusEffectType), Type) &&
            Duration > 0f &&
            !float.IsNaN(Duration) &&
            !float.IsInfinity(Duration);

        public StatusEffectPayload(StatusEffectType type, float duration)
        {
            Type = type;
            Duration = duration;
        }
    }
}
