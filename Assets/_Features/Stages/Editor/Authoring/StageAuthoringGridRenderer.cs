using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGridRenderer
    {
        public static string GetMarker(StagePlacedEntityAuthoring placement)
        {
            if (placement == null)
            {
                return ".";
            }

            return placement.Kind switch
            {
                StageAuthoringEntityKind.Player => "P",
                StageAuthoringEntityKind.Enemy => "E",
                StageAuthoringEntityKind.Box => "B",
                StageAuthoringEntityKind.Wall => "W",
                _ => "?",
            };
        }

        public static bool IsOnFace(StagePlacedEntityAuthoring placement, FaceId face)
        {
            return placement != null && placement.Cell.face == face;
        }
    }
}
