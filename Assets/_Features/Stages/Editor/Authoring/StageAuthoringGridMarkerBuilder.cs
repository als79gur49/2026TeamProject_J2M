using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGridMarkerBuilder
    {
        public static string Build(StagePlacedEntityAuthoring placement, bool duplicateCell = false)
        {
            var marker = BuildBaseMarker(placement);
            if (duplicateCell)
            {
                marker += "+";
            }

            return marker;
        }

        private static string BuildBaseMarker(StagePlacedEntityAuthoring placement)
        {
            if (placement == null)
            {
                return ".";
            }

            var kindMarker = StageAuthoringKindRegistry.TryGetMarker(placement.Kind, out var marker)
                ? marker
                : "?";
            return kindMarker + StageAuthoringFacingDisplayUtility.ToFacingArrow(placement.Facing);
        }

        public static string BuildTileFeatureBadge(StageTileFeatureDefinition feature)
        {
            return BuildTileFeatureKindMarker(feature.Kind) + feature.TileId;
        }

        private static string BuildTileFeatureKindMarker(TileFeatureKind kind)
        {
            return kind switch
            {
                TileFeatureKind.Button => "B",
                TileFeatureKind.Destroy => "D",
                TileFeatureKind.Slide => "S",
                TileFeatureKind.Barricade => "X",
                TileFeatureKind.Exit => "E",
                TileFeatureKind.MoonBlockGenerator => "G",
                _ => "?",
            };
        }
    }
}
