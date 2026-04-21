using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public static class GrandfatherGameplayAssetGuidRegistry
    {
        public const string CombinedGameplayShowcaseGuid = "768e58af510a487eafd9bf00b45b4ca0";
        public const string TutorialSceneGuid = "17f422e552184a24d8a666d77d93ab18";

        private static readonly HashSet<string> AllowedGuids = new(StringComparer.Ordinal)
        {
            CombinedGameplayShowcaseGuid,
            TutorialSceneGuid,
        };

        public static IReadOnlyCollection<string> Entries => AllowedGuids;

        public static bool Contains(string assetGuid)
        {
            return !string.IsNullOrWhiteSpace(assetGuid) && AllowedGuids.Contains(assetGuid);
        }

        public static HashSet<string> CreateSet()
        {
            return new HashSet<string>(AllowedGuids, StringComparer.Ordinal);
        }
    }
}
