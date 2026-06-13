using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    // TODO: remove adapter component from production prefabs in adapter deletion PR.
    [Obsolete("Diagnostic residue only. Production TileFeature visuals must route through TileFeatureVisualProfileProvider and ITileFeatureVisualCueSink.")]
    public sealed class LegacyTileFeatureVisualCueAdapter :
        MonoBehaviour,
        ITileFeatureVisualCueSink,
        IGameplayPresentationPausable
    {
        [SerializeField] private TileFeatureVisualTargetView targetView;
        [SerializeField] private TileFeatureVisualProfile[] profiles;

        private readonly HashSet<string> reportedDiagnosticKeys = new();

        public int DebugUnsupportedLegacyUsageCount { get; private set; }

        public TileFeatureVisualCueId DebugLastUnsupportedCueId { get; private set; }

        public TileFeatureKind DebugLastUnsupportedFeatureKind { get; private set; }

        public int DebugConfiguredMigrationProfileCount => CountConfiguredProfiles();

        public bool IsGameplayPresentationPaused { get; private set; }

        public void ConfigureTarget(TileFeatureVisualTargetView target)
        {
            targetView = target;
        }

        public bool TryHandle(in TileFeatureVisualRequest request)
        {
            DebugUnsupportedLegacyUsageCount++;
            DebugLastUnsupportedCueId = request.CueId;
            DebugLastUnsupportedFeatureKind = request.FeatureKind;
            WarnUnsupportedUsageOnce(request);
            return false;
        }

        public void SetPresentationPaused(bool paused)
        {
            IsGameplayPresentationPaused = paused;
        }

        private void WarnUnsupportedUsageOnce(in TileFeatureVisualRequest request)
        {
            var key = $"{request.FeatureKind}:{request.CueId}";
            if (!reportedDiagnosticKeys.Add(key))
            {
                return;
            }

            var targetDescription = targetView != null
                ? $"tile {targetView.TileId} at {targetView.Cell}"
                : "unconfigured tile feature target";
            var residueDescription = DebugConfiguredMigrationProfileCount > 0
                ? " Serialized profile residue is ignored."
                : string.Empty;
            UnityEngine.Debug.LogWarning(
                $"{nameof(LegacyTileFeatureVisualCueAdapter)} is diagnostic-only and ignored {request.FeatureKind} visual cue {request.CueId} for {targetDescription}.{residueDescription}",
                this);
        }

        private int CountConfiguredProfiles()
        {
            if (profiles == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < profiles.Length; i++)
            {
                if (profiles[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
