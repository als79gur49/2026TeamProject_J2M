using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class StageAuthoringNormalizedPresentationSnapshot
    {
        public StageAuthoringNormalizedPresentationSnapshot(
            StageAuthoringNormalizedPresentationBinding[] enemyBindings,
            StageAuthoringNormalizedPresentationBinding[] staticBindings,
            StageAuthoringNormalizedTileFeaturePresentationSelection[] tileFeatureSelections)
        {
            EnemyBindings = enemyBindings ?? Array.Empty<StageAuthoringNormalizedPresentationBinding>();
            StaticBindings = staticBindings ?? Array.Empty<StageAuthoringNormalizedPresentationBinding>();
            TileFeatureSelections = tileFeatureSelections ?? Array.Empty<StageAuthoringNormalizedTileFeaturePresentationSelection>();
        }

        public StageAuthoringNormalizedPresentationBinding[] EnemyBindings { get; }

        public StageAuthoringNormalizedPresentationBinding[] StaticBindings { get; }

        public StageAuthoringNormalizedTileFeaturePresentationSelection[] TileFeatureSelections { get; }
    }

    public readonly struct StageAuthoringNormalizedPresentationBinding
    {
        public StageAuthoringNormalizedPresentationBinding(int entityId, string presentationId)
        {
            EntityId = entityId;
            PresentationId = presentationId ?? string.Empty;
        }

        public int EntityId { get; }

        public string PresentationId { get; }
    }

    public readonly struct StageAuthoringNormalizedTileFeaturePresentationSelection
    {
        public StageAuthoringNormalizedTileFeaturePresentationSelection(
            int tileId,
            string presentationKey,
            GameObject visualPrefab)
        {
            TileId = tileId;
            PresentationKey = presentationKey ?? string.Empty;
            VisualPrefab = visualPrefab;
        }

        public int TileId { get; }

        public string PresentationKey { get; }

        public GameObject VisualPrefab { get; }
    }
}
