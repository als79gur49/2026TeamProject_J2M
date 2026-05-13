using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class BoardSurfaceCellPresentationPoseResolver : ISurfaceCellPresentationPoseResolver
    {
        private readonly GameplayCubeProjector _projector;
        private CubeTopologyState _topology;

        public BoardSurfaceCellPresentationPoseResolver(
            BoardBounds boardBounds,
            float cellSize,
            CubeTopologyState topology,
            float faceSeamGap)
        {
            _projector = new GameplayCubeProjector(boardBounds, cellSize, faceSeamGap);
            _topology = topology;
        }

        public void RefreshTopology(CubeTopologyState topology)
        {
            _topology = topology;
        }

        public bool TryResolvePose(SurfaceCell cell, out SurfaceCellPresentationPose pose)
        {
            if (!_topology.IsFaceActive(cell.face))
            {
                pose = default;
                return false;
            }

            if (!_projector.TryProjectSurfaceCell(cell, _topology, out var projectedPose))
            {
                pose = default;
                return false;
            }

            pose = new SurfaceCellPresentationPose(
                projectedPose.LocalPosition,
                projectedPose.LocalRotation,
                Vector3.one);
            return true;
        }
    }
}
