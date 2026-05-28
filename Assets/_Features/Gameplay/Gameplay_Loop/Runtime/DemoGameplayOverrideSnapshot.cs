namespace Game.Feature.Gameplay.Loop
{
    public readonly struct DemoGameplayOverrideSnapshot
    {
        public static readonly DemoGameplayOverrideSnapshot None = new(playerInvincible: false);

        public DemoGameplayOverrideSnapshot(bool playerInvincible)
        {
            PlayerInvincible = playerInvincible;
        }

        public bool PlayerInvincible { get; }
    }

    public interface IDemoGameplayOverrideSnapshotSource
    {
        DemoGameplayOverrideSnapshot GetSnapshot();
    }
}
