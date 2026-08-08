using System;

namespace Game.Platform.Steam
{
    public sealed class SteamRuntimeDependencies
    {
        public SteamRuntimeDependencies(
            ISteamNativeApi lifecycle,
            ISteamAchievementApi achievements)
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            Achievements = achievements;
        }

        public ISteamNativeApi Lifecycle { get; }

        public ISteamAchievementApi Achievements { get; }
    }
}
