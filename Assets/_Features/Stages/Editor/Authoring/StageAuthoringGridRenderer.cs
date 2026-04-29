using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGridRenderer
    {
        public static string GetMarker(StagePlacedEntityAuthoring placement)
        {
            return StageAuthoringGridMarkerBuilder.Build(placement);
        }

        public static bool IsOnFace(StagePlacedEntityAuthoring placement, FaceId face)
        {
            return placement != null && placement.Cell.face == face;
        }
    }
}
