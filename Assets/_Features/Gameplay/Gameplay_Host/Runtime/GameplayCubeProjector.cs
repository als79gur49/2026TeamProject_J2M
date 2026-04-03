using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayCubeProjector
    {
        private const float DefaultEntitySurfaceOffsetMultiplier = 0.08f;
        private const float ProjectileSurfaceOffsetMultiplier = 0.18f;
        private const float ActiveFaceSeamGapMultiplier = 1f;

        private readonly BoardBounds _boardBounds;
        private readonly float _cellSize;
        private readonly float _faceSeamHalfGap;
        private readonly float _halfDepth;
        private readonly float _halfHeight;
        private readonly float _halfWidth;

        public GameplayCubeProjector(BoardBounds boardBounds, float cellSize)
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
            _cellSize = cellSize;
            _faceSeamHalfGap = _cellSize * ActiveFaceSeamGapMultiplier * 0.5f;
            _halfWidth = Width * _cellSize * 0.5f;
            _halfHeight = Height * _cellSize * 0.5f;
            _halfDepth = Height * _cellSize * 0.5f;
        }

        public BoardBounds BoardBounds => _boardBounds;

        public float CellSize => _cellSize;

        public Vector3 CubeCenter => Vector3.zero;

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

            projectedPose = ProjectCell(slot, cell, -ResolveEntitySurfaceOffset(entityType));
            return true;
        }

        public bool TryProjectTransitionEntityCell(
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            out ProjectedCellPose projectedPose)
        {
            return TryProjectTransitionEntityCell(
                cell,
                sourceTopology,
                destinationTopology,
                EntityType.Unit,
                out projectedPose);
        }

        public bool TryProjectTransitionEntityCell(
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            EntityType entityType,
            out ProjectedCellPose projectedPose)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) ||
                !TryResolveTransitionSurfaceSlot(cell.face, sourceTopology, destinationTopology, out var slot))
            {
                projectedPose = default;
                return false;
            }

            projectedPose = ProjectCell(slot, cell, -ResolveEntitySurfaceOffset(entityType));
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

        public bool TryProjectTransitionSurfaceCell(
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            out ProjectedCellPose projectedPose)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) ||
                !TryResolveTransitionSurfaceSlot(cell.face, sourceTopology, destinationTopology, out var slot))
            {
                projectedPose = default;
                return false;
            }

            projectedPose = ProjectCell(slot, cell, 0f);
            return true;
        }

        public bool TryResolveEntityRotation(
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing,
            out Quaternion rotation)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) ||
                !TryResolveEntitySlot(cell.face, topology, out var slot))
            {
                rotation = default;
                return false;
            }

            rotation = ResolveEntityRotation(ResolveFrame(slot), facing);
            return true;
        }

        public bool TryResolveTransitionEntityRotation(
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            Direction facing,
            out Quaternion rotation)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) ||
                !TryResolveTransitionSurfaceSlot(cell.face, sourceTopology, destinationTopology, out var slot))
            {
                rotation = default;
                return false;
            }

            rotation = ResolveEntityRotation(ResolveFrame(slot), facing);
            return true;
        }

        public Bounds GetVisibleCubeBounds(CubeTopologyState topology)
        {
            return new Bounds(
                GetCubeCenter(),
                new Vector3(
                    _halfWidth * 2f,
                    (_halfHeight + _faceSeamHalfGap) * 2f,
                    (_halfDepth + _faceSeamHalfGap) * 2f));
        }

        public Vector3 GetCubeCenter()
        {
            return Vector3.zero;
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

        private bool TryResolveTransitionSurfaceSlot(
            FaceId face,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            out CubeFaceSlot slot)
        {
            if (!sourceTopology.IsFaceActive(face) &&
                !destinationTopology.IsFaceActive(face))
            {
                slot = default;
                return false;
            }

            return TryResolveSurfaceSlot(face, destinationTopology, out slot);
        }

        private ProjectedCellPose ProjectCell(CubeFaceSlot slot, SurfaceCell cell, float surfaceOffset)
        {
            var centeredX = ResolveCenteredCoordinate(cell.x, _boardBounds.MinInclusive.x, Width);
            var centeredY = ResolveCenteredCoordinate(cell.y, _boardBounds.MinInclusive.y, Height);
            var frame = ResolveFrame(slot);
            var localPosition = frame.PlaneCenterOffset +
                                (frame.PositionRightAxis * centeredX) +
                                (frame.PositionUpAxis * centeredY) +
                                (frame.Normal * surfaceOffset);
            return new ProjectedCellPose(localPosition, frame.Rotation, frame.Normal);
        }

        private FaceFrame ResolveFrame(CubeFaceSlot slot)
        {
            var referenceFaceDistance = _halfHeight + _faceSeamHalfGap;
            var referenceFrame = new FaceFrame(
                Vector3.down,
                Vector3.right,
                Vector3.forward,
                Vector3.down * referenceFaceDistance,
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            var slotRotation = ResolveSlotRotation(slot);

            // Every physical slot is the same exploded cube face rotated around the cube's X axis.
            // The seam gap is applied only along the slot normal so the face centers stay on a rigid cube.
            return referenceFrame.Rotate(slotRotation);
        }

        private static Quaternion ResolveSlotRotation(CubeFaceSlot slot)
        {
            return slot switch
            {
                CubeFaceSlot.Bottom => Quaternion.identity,
                CubeFaceSlot.Front => Quaternion.Euler(-90f, 0f, 0f),
                CubeFaceSlot.Top => Quaternion.Euler(180f, 0f, 0f),
                CubeFaceSlot.Back => Quaternion.Euler(90f, 0f, 0f),
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

        private static Quaternion ResolveEntityRotation(FaceFrame frame, Direction facing)
        {
            var upAxis = facing switch
            {
                Direction.Right => frame.PositionRightAxis,
                Direction.Down => -frame.PositionUpAxis,
                Direction.Left => -frame.PositionRightAxis,
                _ => frame.PositionUpAxis,
            };

            return Quaternion.LookRotation(frame.Normal, upAxis);
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

            public FaceFrame Rotate(Quaternion rotation)
            {
                return new FaceFrame(
                    rotation * Normal,
                    rotation * PositionRightAxis,
                    rotation * PositionUpAxis,
                    rotation * PlaneCenterOffset,
                    rotation * Rotation);
            }
        }
    }
}
