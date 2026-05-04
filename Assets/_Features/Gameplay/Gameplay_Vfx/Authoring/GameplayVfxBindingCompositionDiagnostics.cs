using System.Collections.Generic;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    public static class GameplayVfxBindingCompositionDiagnostics
    {
        public static VfxAuthoringValidationResult Validate(
            VfxCueMapAsset hostDefaultMap,
            IReadOnlyList<VfxProfileAsset> familyProfiles,
            GameplayVfxBindingCompositionOptions options = default)
        {
            var messages = new List<VfxAuthoringValidationMessage>();
            var profiles = familyProfiles ?? new List<VfxProfileAsset>();

            if (hostDefaultMap == null)
            {
                if (!options.AllowMissingHostDefaultMap)
                {
                    messages.Add(VfxAuthoringValidationResult.Error(
                        "VFX_COMPOSITION_MISSING_HOST_DEFAULT_MAP",
                        "Gameplay VFX composition requires a host default VFX cue map."));
                }
            }
            else
            {
                AppendMessages(hostDefaultMap.ValidateAuthoring(), messages);
            }

            if (hostDefaultMap == null && profiles.Count == 0)
            {
                messages.Add(VfxAuthoringValidationResult.Warning(
                    "VFX_COMPOSITION_EMPTY",
                    "Gameplay VFX composition has no host default map or family profiles."));
            }

            var seenFamilies = new HashSet<GameplayVfxFamily>();
            for (var i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null)
                {
                    if (!options.AllowNullProfileEntries)
                    {
                        messages.Add(VfxAuthoringValidationResult.Error(
                            "VFX_COMPOSITION_NULL_PROFILE",
                            $"Gameplay VFX composition contains a null profile entry at index {i}."));
                    }

                    continue;
                }

                if (profile.Family != GameplayVfxFamily.None &&
                    !seenFamilies.Add(profile.Family) &&
                    !options.AllowDuplicateProfileFamilies)
                {
                    messages.Add(VfxAuthoringValidationResult.Error(
                        "VFX_COMPOSITION_DUPLICATE_PROFILE_FAMILY",
                        $"Gameplay VFX composition contains duplicate profile family '{profile.Family}'.",
                        profile));
                }

                AppendMessages(profile.ValidateAuthoring(), messages);
            }

            return VfxAuthoringValidationResult.FromMessages(messages);
        }

        private static void AppendMessages(
            VfxAuthoringValidationResult validation,
            ICollection<VfxAuthoringValidationMessage> messages)
        {
            foreach (var message in validation.Messages)
            {
                messages.Add(message);
            }
        }
    }
}
