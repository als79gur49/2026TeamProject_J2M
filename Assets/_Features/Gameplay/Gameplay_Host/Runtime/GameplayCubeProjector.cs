using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayCubeProjector
    {
        private const float DefaultEntitySurfaceOffsetMultiplier = 0.08f;
        private const float ProjectileSurfaceOffsetMultiplier = 0.18f;

        private readonly BoardBounds _boardBounds;
        private readonly Vector3 _cubeCenter;
        private readonly float _cellSize;
        private readonly float _halfDepth;
        private readonly float _halfHeight;
        private readonly float _halfWidth;

        public GameplayCubeProjector(BoardBounds boardBounds, float cellSize)
            : this(boardBounds, Vector3.zero, cellSize)
        {
        }

        public GameplayCubeProjector(BoardBounds boardBounds, Vector3 cubeCenter, float cellSize)
        {
            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException("GameplayCubeProjector requires bounded board bounds.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            _boardBounds = boardBounds;
            _cubeCenter = cubeCenter;
            _cellSize = cellSize;
            _halfWidth = Width * _cellSize * 0.5f;
            _halfHeight = Height * _cellSize * 0.5f;
            _halfDepth = Height * _cellSize * 0.5f;
        }

        public BoardBounds BoardBounds => _boardBounds;

        public float CellSize => _cellSize;

        public Vector3 CubeCenter => _cubeCenter;

        public int Width => _boardBounds.MaxInclusive.x - _boardBounds.MinInclusive.x + 1;

        public int Height => _boardBounds.MaxInclusive.y - _boardBounds.MinInclusive.y + 1;

        public bool TryProjectEntityCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            out ProjectedCellPose projectedPose)
        {
            return TryProjectEntityCell(cell, topology, EntityType.Unit, out projectedPose);
        }

        public bool TryProjectEntityCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            EntityType entityType,
            out ProjectedCellPose projectedPose)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) ||
                !TryResolveEntitySlot(cell.face, topology, out var slot))
            {
                projectedPose = default;
                return false;
            }

            projectedPose = ProjectCell(slot, cell, ResolveEntitySurfaceOffset(entityType));
            return true;
        }

        public bool TryProjectSurfaceCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            out ProjectedCellPose projectedPose)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) ||
                !TryResolveSurfaceSlot(cell.face, topology, out var slot))
            {
                projectedPose = default;
                return false;
            }

            projectedPose = ProjectCell(slot, cell, 0f);
            return true;
        }

        public Bounds GetVisibleCubeBounds(CubeTopologyState topology)
        {
            return new Bounds(
                GetCubeCenter(),
                new Vector3(_halfWidth * 2f, _halfHeight * 2f, _halfDepth * 2f));
        }

        public Vector3 GetCubeCenter()
        {
            return _cubeCenter;
        }

        private bool TryResolveEntitySlot(FaceId face, CubeTopologyState topology, out CubeFaceSlot slot)
        {
            if (face == topology.BottomFace)
            {
                slot = CubeFaceSlot.Bottom;
                return true;
            }

            if (face == topology.FrontFace)
            {
                slot = CubeFaceSlot.Front;
                return true;
            }

            slot = default;
            return false;
        }

        private bool TryResolveSurfaceSlot(FaceId face, CubeTopologyState topology, out CubeFaceSlot slot)
        {
            if (TryResolveEntitySlot(face, topology, out slot))
            {
                return true;
            }

            if (face == FaceIdUtility.GetNext(topology.FrontFace))
            {
                slot = CubeFaceSlot.Top;
                return true;
            }

            if (face == FaceIdUtility.GetPrevious(topology.BottomFace))
            {
                slot = CubeFaceSlot.Back;
                return true;
            }

            slot = default;
            return false;
        }

        private ProjectedCellPose ProjectCell(CubeFaceSlot slot, SurfaceCell cell, float surfaceOffset)
        {
            var centeredX = ResolveCenteredCoordinate(cell.x, _boardBounds.MinInclusive.x, Width);
            var centeredY = ResolveCenteredCoordinate(cell.y, _boardBounds.MinInclusive.y, Height);
            var frame = ResolveFrame(slot);
            var localPosition = _cubeCenter +
                                frame.PlaneCenterOffset +
                                (frame.PositionRightAxis * centeredX) +
                                (frame.PositionUpAxis * centeredY) +
                                (frame.Normal * surfaceOffset);
            return new ProjectedCellPose(localPosition, frame.Rotation, frame.Normal);
        }

        private FaceFrame ResolveFrame(CubeFaceSlot slot)
        {
            return slot switch
            {
                CubeFaceSlot.Bottom => new FaceFrame(
                    Vector3.up,
                    Vector3.right,
                    Vector3.back,
                    Vector3.up * _halfHeight,
                    Quaternion.LookRotation(Vector3.up, Vector3.back)),
                CubeFaceSlot.Front => new FaceFrame(
                    Vector3.back,
                    Vector3.right,
                    Vector3.up,
                    Vector3.back * _halfDepth,
                    Quaternion.LookRotation(Vector3.back, Vector3.up)),
                CubeFaceSlot.Top => new FaceFrame(
                    Vector3.down,
                    Vector3.right,
                    Vector3.forward,
                    Vector3.down * _halfHeight,
                    Quaternion.LookRotation(Vector3.down, Vector3.forward)),
                CubeFaceSlot.Back => new FaceFrame(
                    Vector3.forward,
                    Vector3.right,
                    Vector3.up,
                    Vector3.forward * _halfDepth,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up)),
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
            };
        }

        private float ResolveCenteredCoordinate(int coordinate, int minInclusive, int cellCount)
        {
            return ((coordinate - minInclusive) + 0.5f - (cellCount * 0.5f)) * _cellSize;
        }

        private float ResolveEntitySurfaceOffset(EntityType entityType)
        {
            return entityType == EntityType.Projectile
                ? ProjectileSurfaceOffsetMultiplier * _cellSize
                : DefaultEntitySurfaceOffsetMultiplier * _cellSize;
        }

        private enum CubeFaceSlot
        {
            Bottom = 0,
            Front = 1,
            Top = 2,
            Back = 3,
        }

        private readonly struct FaceFrame
        {
            public FaceFrame(
                Vector3 normal,
                Vector3 positionRightAxis,
                Vector3 positionUpAxis,
                Vector3 planeCenterOffset,
                Quaternion rotation)
            {
                Normal = normal;
                PositionRightAxis = positionRightAxis;
                PositionUpAxis = positionUpAxis;
                PlaneCenterOffset = planeCenterOffset;
                Rotation = rotation;
            }

            public Vector3 Normal { get; }

            public Vector3 PositionRightAxis { get; }

            public Vector3 PositionUpAxis { get; }

            public Vector3 PlaneCenterOffset { get; }

            public Quaternion Rotation { get; }
        }
    }
}
