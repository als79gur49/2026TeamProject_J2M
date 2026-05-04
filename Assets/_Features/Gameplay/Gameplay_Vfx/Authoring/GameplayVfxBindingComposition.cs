using System.Collections.Generic;
using System.Linq;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    public static class GameplayVfxBindingComposition
    {
        public static GameplayVfxBindingCompositionResult Compose(
            VfxCueMapAsset hostDefaultMap,
            IEnumerable<VfxProfileAsset> familyProfiles,
            GameplayVfxBindingCompositionOptions options = default)
        {
            var profiles = (familyProfiles ?? Enumerable.Empty<VfxProfileAsset>()).ToArray();
            var validation = GameplayVfxBindingCompositionDiagnostics.Validate(
                hostDefaultMap,
                profiles,
                options);

            if (validation.HasErrors)
            {
                return new GameplayVfxBindingCompositionResult(
                    null,
                    null,
                    null,
                    validation);
            }

            var runtimeHostDefaultMap = hostDefaultMap != null
                ? hostDefaultMap.BuildRuntimeMap()
                : VfxCueMap.Empty;
            var runtimeProfiles = BuildRuntimeProfiles(profiles, options);
            var resolver = new CompositeVfxBindingResolver(runtimeHostDefaultMap, runtimeProfiles);

            return new GameplayVfxBindingCompositionResult(
                resolver,
                runtimeHostDefaultMap,
                runtimeProfiles,
                validation);
        }

        private static IReadOnlyDictionary<GameplayVfxFamily, VfxProfile> BuildRuntimeProfiles(
            IEnumerable<VfxProfileAsset> profiles,
            GameplayVfxBindingCompositionOptions options)
        {
            var runtimeProfiles = new Dictionary<GameplayVfxFamily, VfxProfile>();
            foreach (var profile in profiles.Where(profile => profile != null))
            {
                var runtimeProfile = profile.BuildRuntimeProfile();
                if (options.AllowDuplicateProfileFamilies)
                {
                    runtimeProfiles[runtimeProfile.Family] = runtimeProfile;
                }
                else
                {
                    runtimeProfiles.Add(runtimeProfile.Family, runtimeProfile);
                }
            }

            return runtimeProfiles;
        }
    }
}
