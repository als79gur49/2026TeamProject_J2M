using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Vfx
{
    internal static class VfxOrdering
    {
        public static int CompareCell(SurfaceCell left, SurfaceCell right)
        {
            var faceCompare = left.face.CompareTo(right.face);
            if (faceCompare != 0)
            {
                return faceCompare;
            }

            var xCompare = left.x.CompareTo(right.x);
            return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
        }

        public static int CompareTopology(CubeTopologyState left, CubeTopologyState right)
        {
            return left.BottomFace.CompareTo(right.BottomFace);
        }
    }
}
