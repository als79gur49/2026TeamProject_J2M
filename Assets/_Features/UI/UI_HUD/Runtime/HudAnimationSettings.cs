namespace Game.Feature.UI.HUD
{
    public readonly struct HudAnimationSettings
    {
        public static readonly HudAnimationSettings Default = new(true, false);

        public HudAnimationSettings(
            bool useUnscaledTime,
            bool reduceMotion)
        {
            UseUnscaledTime = useUnscaledTime;
            ReduceMotion = reduceMotion;
        }

        public bool UseUnscaledTime { get; }

        public bool ReduceMotion { get; }
    }
}
