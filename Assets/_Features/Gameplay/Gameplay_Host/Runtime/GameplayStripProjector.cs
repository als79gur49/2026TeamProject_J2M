using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayStripProjector
    {
        private static readonly Quaternion StripRotation = Quaternion.identity;
        private static readonly Vector3 StripNormal = Vector3.forward;

        private readonly BoardBounds _boardBounds;
        private readonly float _cellSize;
        private readonly Vector3 _gridOrigin;

        public GameplayStripProjector(
            BoardBounds boardBounds,
            Vector3 gridOrigin,
            float cellSize)
        {
            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException("GameplayStripProjector requires bounded board bounds.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            _boardBounds = boardBounds;
            _gridOrigin = gridOrigin;
            _cellSize = cellSize;
        }

        public BoardBounds BoardBounds => _boardBounds;

        public float CellSize => _cellSize;

        public Vector3 GridOrigin => _gridOrigin;

        public int Width => _boardBounds.MaxInclusive.x - _boardBounds.MinInclusive.x + 1;

        public int Height => _boardBounds.MaxInclusive.y - _boardBounds.MinInclusive.y + 1;

        public bool TryProjectEntityCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            out ProjectedCellPose projectedPose)
        {
            return TryProjectActiveStripCell(cell, topology, out projectedPose);
        }

        public bool TryProjectSurfaceCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            out ProjectedCellPose projectedPose)
        {
            return TryProjectActiveStripCell(cell, topology, out projectedPose);
        }

        public Vector3 GetActiveStripCenter(Vector3 continuityAnchor)
        {
            var centerX = (_boardBounds.MinInclusive.x + _boardBounds.MaxInclusive.x) * _cellSize * 0.5f;
            var centerY = (_boardBounds.MinInclusive.y * _cellSize) +
                          (((Height * 2) - 1) * _cellSize * 0.5f);

            return _gridOrigin +
                   continuityAnchor +
                   new Vector3(centerX, centerY, 0f);
        }

        public Vector3 GetContinuityStepOffset()
        {
            return Vector3.up * (Height * _cellSize);
        }

        private bool TryProjectActiveStripCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            out ProjectedCellPose projectedPose)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) || !topology.IsFaceActive(cell.face))
            {
                projectedPose = default;
                return false;
            }

            var localX = cell.x * _cellSize;
            var localY = cell.y * _cellSize;
            var faceOffsetY = ResolveFaceOffsetY(cell.face, topology);
            projectedPose = new ProjectedCellPose(
                _gridOrigin + new Vector3(localX, localY + faceOffsetY, 0f),
                StripRotation,
                StripNormal);
            return true;
        }

        private float ResolveFaceOffsetY(FaceId face, CubeTopologyState topology)
        {
            if (face == topology.BottomFace)
            {
                return 0f;
            }

            if (face == topology.FrontFace)
            {
                return Height * _cellSize;
            }

            throw new InvalidOperationException(
                $"Cannot project inactive face {face}. Bottom={topology.BottomFace}, Front={topology.FrontFace}");
        }
    }
}
