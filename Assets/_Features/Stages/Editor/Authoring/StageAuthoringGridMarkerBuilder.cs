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
    }
}
