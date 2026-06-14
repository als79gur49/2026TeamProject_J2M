using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class TileFeatureVisualProfileProvider : MonoBehaviour
    {
        [SerializeField] private TileFeatureVisualProfile[] profiles;

        public IReadOnlyList<TileFeatureVisualProfile> Profiles =>
            profiles ?? Array.Empty<TileFeatureVisualProfile>();

        public bool TryGetProfile(TileFeatureKind featureKind, out TileFeatureVisualProfile profile)
        {
            var resolvedProfiles = Profiles;
            for (var i = 0; i < resolvedProfiles.Count; i++)
            {
                var candidate = resolvedProfiles[i];
                if (candidate != null &&
                    candidate.FeatureKind == featureKind)
                {
                    profile = candidate;
                    return true;
                }
            }

            profile = null;
            return false;
        }

        public TileFeatureVisualBindingDiagnostics ValidateProfiles(TileFeatureVisualTargetView target)
        {
            var diagnostics = new TileFeatureVisualBindingDiagnostics();
            var seenFeatureKinds = new HashSet<TileFeatureKind>();
            var resolvedProfiles = Profiles;
            for (var i = 0; i < resolvedProfiles.Count; i++)
            {
                var profile = resolvedProfiles[i];
                if (profile == null)
                {
                    diagnostics.Add($"Profile at index {i} is missing.");
                    continue;
                }

                if (!seenFeatureKinds.Add(profile.FeatureKind))
                {
                    diagnostics.Add($"Duplicate profile feature kind: {profile.FeatureKind}.");
                }

                var profileDiagnostics = TileFeatureVisualBindingDiagnostics.ForProfile(profile, target);
                for (var messageIndex = 0; messageIndex < profileDiagnostics.Messages.Count; messageIndex++)
                {
                    diagnostics.Add($"{profile.FeatureKind}: {profileDiagnostics.Messages[messageIndex]}");
                }
            }

            return diagnostics;
        }
    }
}
