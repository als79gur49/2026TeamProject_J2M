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

            var kindMarker = placement.Kind switch
            {
                StageAuthoringEntityKind.Player => "P",
                StageAuthoringEntityKind.Enemy => "E",
                StageAuthoringEntityKind.Box => "B",
                StageAuthoringEntityKind.Wall => "W",
                _ => "?",
            };
            return kindMarker + StageAuthoringFacingDisplayUtility.ToFacingArrow(placement.Facing);
        }
    }
}
