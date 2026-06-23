using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal enum Free2DTopologyTransitionRejectReason
    {
        None = 0,
        MissingEntity = 1,
        NonUnit = 2,
        MissingContinuousPose = 3,
        NonCardinalDelta = 4,
        UnsupportedSeam = 5,
        TopologyTransitionUnavailable = 6,
        TargetFaceOutOfBounds = 7,
        TargetFaceBlockedBySolid = 9,
        TargetFaceBlockedByUnit = 10,
        TargetFaceBlockedByReservation = 11,
        TargetFaceFootprintBlocked = 12,
        RemapInvalid = 13,
        CrossingAxisDidNotReachSeam = 14,
        TargetFaceBlockedByTileFeature = 15,
    }

    internal readonly struct Free2DTopologyTransitionResult
    {
        public Free2DTopologyTransitionResult(
            bool success,
            int entityId,
            SurfaceCell sourceAnchor,
            SimulationOffset2 sourceLocalOffset,
            SimulationVelocity2 sourceVelocity,
            SurfaceCell targetAnchor,
            CubeTopologyState updatedTopology,
            CubeRotationKind rotationKind,
            SimulationOffset2 targetLocalOffset,
            SimulationVelocity2 targetVelocity,
            int targetResidualX,
            int targetResidualY,
            Free2DTopologyTransitionRejectReason rejectReason,
            LegalityResult targetLegality = default,
            SurfaceContactProjectionResult sourceContactProjection = default)
        {
            Success = success;
            EntityId = entityId;
            SourceAnchor = sourceAnchor;
            SourceLocalOffset = sourceLocalOffset;
            SourceVelocity = sourceVelocity;
            TargetAnchor = targetAnchor;
            UpdatedTopology = updatedTopology;
            RotationKind = rotationKind;
            TargetLocalOffset = targetLocalOffset;
            TargetVelocity = targetVelocity;
            TargetResidualX = targetResidualX;
            TargetResidualY = targetResidualY;
            RejectReason = rejectReason;
            TargetLegality = targetLegality;
            SourceContactProjection = sourceContactProjection;
        }

        public bool Success { get; }

        public int EntityId { get; }

        public SurfaceCell SourceAnchor { get; }

        public SimulationOffset2 SourceLocalOffset { get; }

        public SimulationVelocity2 SourceVelocity { get; }

        public SurfaceCell TargetAnchor { get; }

        public CubeTopologyState UpdatedTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public SimulationOffset2 TargetLocalOffset { get; }

        public SimulationVelocity2 TargetVelocity { get; }

        public int TargetResidualX { get; }

        public int TargetResidualY { get; }

        public Free2DTopologyTransitionRejectReason RejectReason { get; }

        public LegalityResult TargetLegality { get; }

        public SurfaceContactProjectionResult SourceContactProjection { get; }
    }

    [Flags]
    internal enum SurfaceContactProjectionAxes
    {
        None = 0,
        X = 1,
        Y = 2,
    }

    internal enum SurfaceContactProjectionSide
    {
        None = 0,
        PositiveX = 1,
        NegativeX = 2,
        PositiveY = 3,
        NegativeY = 4,
    }

    internal readonly struct SurfaceContactProjectionContact
    {
        public SurfaceContactProjectionContact(
            SurfaceContactProjectionSide side,
            SurfaceCell cell,
            LegalityResult legality)
        {
            Side = side;
            Cell = cell;
            Legality = legality;
        }

        public SurfaceContactProjectionSide Side { get; }

        public SurfaceCell Cell { get; }

        public LegalityResult Legality { get; }
    }

    internal readonly struct SurfaceContactProjectionResult
    {
        private static readonly IReadOnlyList<SurfaceContactProjectionContact> EmptyContacts =
            Array.Empty<SurfaceContactProjectionContact>();

        public SurfaceContactProjectionResult(
            SimulationOffset2 originalLocalOffset,
            SimulationOffset2 projectedLocalOffset,
            bool clampedPositiveX,
            bool clampedNegativeX,
            bool clampedPositiveY,
            bool clampedNegativeY,
            IReadOnlyList<SurfaceContactProjectionContact> contacts = null)
        {
            OriginalLocalOffset = originalLocalOffset;
            ProjectedLocalOffset = projectedLocalOffset;
            ClampedPositiveX = clampedPositiveX;
            ClampedNegativeX = clampedNegativeX;
            ClampedPositiveY = clampedPositiveY;
            ClampedNegativeY = clampedNegativeY;
            Contacts = contacts ?? EmptyContacts;
        }

        public SimulationOffset2 OriginalLocalOffset { get; }

        public SimulationOffset2 ProjectedLocalOffset { get; }

        public bool ClampedPositiveX { get; }

        public bool ClampedNegativeX { get; }

        public bool ClampedPositiveY { get; }

        public bool ClampedNegativeY { get; }

        public IReadOnlyList<SurfaceContactProjectionContact> Contacts { get; }
    }

    internal static class SurfaceContinuousContactProjectionQueries
    {
        public static SurfaceContactProjectionResult ProjectLocalOffsetAgainstSourceFaceBlockers(
            WorldSnapshot snapshot,
            in LegalityActorRef actor,
            SurfaceCell sourceAnchor,
            SimulationOffset2 sourceLocalOffset,
            int radiusUnits,
            CubeTopologyState evaluationTopology,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            SurfaceContactProjectionAxes axes)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var radius = NormalizeCollisionRadiusUnits(radiusUnits);
            if (radius <= 0 ||
                axes == SurfaceContactProjectionAxes.None)
            {
                return new SurfaceContactProjectionResult(
                    sourceLocalOffset,
                    sourceLocalOffset,
                    clampedPositiveX: false,
                    clampedNegativeX: false,
                    clampedPositiveY: false,
                    clampedNegativeY: false);
            }

            var projectedX = sourceLocalOffset.X.RawValue;
            var projectedY = sourceLocalOffset.Y.RawValue;
            var clampedPositiveX = false;
            var clampedNegativeX = false;
            var clampedPositiveY = false;
            var clampedNegativeY = false;
            List<SurfaceContactProjectionContact> contacts = null;

            if ((axes & SurfaceContactProjectionAxes.X) != 0)
            {
                if (projectedX + radius > SimulationFixed.HalfCellUnits &&
                    IsSourceFaceContactBlocked(
                        snapshot,
                        actor,
                        sourceAnchor,
                        sourceAnchor + Vector2Int.right,
                        evaluationTopology,
                        tileFeatureDefinitions,
                        out var legality))
                {
                    projectedX = GetPositiveBlockedClamp(radius);
                    clampedPositiveX = true;
                    AddContact(ref contacts, SurfaceContactProjectionSide.PositiveX, legality.Cell, legality);
                }

                if (projectedX - radius < SimulationFixed.MinLocalOffset &&
                    IsSourceFaceContactBlocked(
                        snapshot,
                        actor,
                        sourceAnchor,
                        sourceAnchor + Vector2Int.left,
                        evaluationTopology,
                        tileFeatureDefinitions,
                        out legality))
                {
                    projectedX = GetNegativeBlockedClamp(radius);
                    clampedNegativeX = true;
                    AddContact(ref contacts, SurfaceContactProjectionSide.NegativeX, legality.Cell, legality);
                }
            }

            if ((axes & SurfaceContactProjectionAxes.Y) != 0)
            {
                if (projectedY + radius > SimulationFixed.HalfCellUnits &&
                    IsSourceFaceContactBlocked(
                        snapshot,
                        actor,
                        sourceAnchor,
                        sourceAnchor + Vector2Int.up,
                        evaluationTopology,
                        tileFeatureDefinitions,
                        out var legality))
                {
                    projectedY = GetPositiveBlockedClamp(radius);
                    clampedPositiveY = true;
                    AddContact(ref contacts, SurfaceContactProjectionSide.PositiveY, legality.Cell, legality);
                }

                if (projectedY - radius < SimulationFixed.MinLocalOffset &&
                    IsSourceFaceContactBlocked(
                        snapshot,
                        actor,
                        sourceAnchor,
                        sourceAnchor + Vector2Int.down,
                        evaluationTopology,
                        tileFeatureDefinitions,
                        out legality))
                {
                    projectedY = GetNegativeBlockedClamp(radius);
                    clampedNegativeY = true;
                    AddContact(ref contacts, SurfaceContactProjectionSide.NegativeY, legality.Cell, legality);
                }
            }

            return new SurfaceContactProjectionResult(
                sourceLocalOffset,
                new SimulationOffset2(
                    SimulationFixed.FromRaw(projectedX),
                    SimulationFixed.FromRaw(projectedY)),
                clampedPositiveX,
                clampedNegativeX,
                clampedPositiveY,
                clampedNegativeY,
                contacts);
        }

        private static bool IsSourceFaceContactBlocked(
            WorldSnapshot snapshot,
            in LegalityActorRef actor,
            SurfaceCell sourceAnchor,
            SurfaceCell contactCell,
            CubeTopologyState evaluationTopology,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out LegalityResult legality)
        {
            legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                new TraverseContext(
                    snapshot,
                    actor,
                    sourceAnchor,
                    contactCell,
                    evaluationTopology,
                    TransitionRequirement.None,
                    tileFeatureDefinitions: tileFeatureDefinitions));
            return legality.Verdict != LegalityVerdict.Allowed;
        }

        private static void AddContact(
            ref List<SurfaceContactProjectionContact> contacts,
            SurfaceContactProjectionSide side,
            SurfaceCell cell,
            LegalityResult legality)
        {
            if (contacts == null)
            {
                contacts = new List<SurfaceContactProjectionContact>();
            }

            contacts.Add(new SurfaceContactProjectionContact(side, cell, legality));
        }

        private static int NormalizeCollisionRadiusUnits(int radiusUnits)
        {
            return Math.Max(0, Math.Min(SimulationFixed.HalfCellUnits - 1, radiusUnits));
        }

        private static int GetPositiveBlockedClamp(int radiusUnits)
        {
            return Math.Min(
                SimulationFixed.MaxPositiveLocalOffset,
                SimulationFixed.HalfCellUnits - radiusUnits);
        }

        private static int GetNegativeBlockedClamp(int radiusUnits)
        {
            return SimulationFixed.MinLocalOffset + radiusUnits;
        }
    }

    internal static class SurfaceFree2DTopologyTransitionQueries
    {
        public static bool TryResolveFree2DTopologyTransition(
            WorldSnapshot snapshot,
            int entityId,
            Vector2Int directionDelta,
            SimulationVelocity2 velocityDelta,
            int collisionRadiusUnits,
            out Free2DTopologyTransitionResult result,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (Math.Abs(directionDelta.x) + Math.Abs(directionDelta.y) != 1)
            {
                result = CreateRejected(entityId, default, SimulationOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.NonCardinalDelta);
                return false;
            }

            if (!snapshot.TryGetEntity(entityId, out var entity))
            {
                result = CreateRejected(entityId, default, SimulationOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.MissingEntity);
                return false;
            }

            if (entity.type != EntityType.Unit)
            {
                result = CreateRejected(entityId, entity.position, SimulationOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.NonUnit);
                return false;
            }

            if (!snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var pose))
            {
                result = CreateRejected(entityId, entity.position, SimulationOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.MissingContinuousPose);
                return false;
            }

            if (pose.AnchorCell != entity.position)
            {
                result = CreateRejected(entityId, pose.AnchorCell, pose.LocalOffset, velocityDelta, Free2DTopologyTransitionRejectReason.RemapInvalid);
                return false;
            }

            var sourceContactProjection =
                SurfaceContinuousContactProjectionQueries.ProjectLocalOffsetAgainstSourceFaceBlockers(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, entity),
                    entity.position,
                    pose.LocalOffset,
                    collisionRadiusUnits,
                    snapshot.Topology,
                    tileFeatureDefinitions,
                    ResolveSourceContactProjectionAxes(directionDelta));

            if (!SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                    snapshot.Topology,
                    snapshot.BoardBounds,
                    entity.position,
                    directionDelta,
                    sourceContactProjection.ProjectedLocalOffset,
                    velocityDelta,
                    collisionRadiusUnits,
                    out var remapRejectReason,
                    out var remap))
            {
                result = CreateRejected(
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remapRejectReason,
                    sourceContactProjection);
                return false;
            }

            if (TryResolveDirectTargetOccupancyBlocker(
                    snapshot,
                    entityId,
                    remap.UpdatedTopology,
                    remap.TargetAnchor,
                    out var directOccupancyRejectReason))
            {
                result = new Free2DTopologyTransitionResult(
                    false,
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remap.TargetAnchor,
                    remap.UpdatedTopology,
                    remap.RotationKind,
                    remap.TargetLocalOffset,
                    remap.TargetVelocity,
                    0,
                    0,
                    directOccupancyRejectReason,
                    sourceContactProjection: sourceContactProjection);
                return false;
            }

            if (TileFeatureMovementBlockerQuery.TryGetTopologyTransitionTileFeatureBlocker(
                    snapshot,
                    remap.TargetAnchor,
                    out var topologyTransitionBlocker))
            {
                var topologyTransitionBlockerLegality = LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    remap.TargetAnchor,
                    remap.UpdatedTopology,
                    RuntimeLegalityBlockerFactory.CreateTileFeature(topologyTransitionBlocker),
                    transitionRequirement: TransitionRequirement.TopologyUpdate(
                        remap.RotationKind,
                        remap.UpdatedTopology));
                result = new Free2DTopologyTransitionResult(
                    false,
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remap.TargetAnchor,
                    remap.UpdatedTopology,
                    remap.RotationKind,
                    remap.TargetLocalOffset,
                    remap.TargetVelocity,
                    0,
                    0,
                    Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTileFeature,
                    topologyTransitionBlockerLegality,
                    sourceContactProjection);
                return false;
            }

            var targetLegality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                remap.TargetAnchor,
                entityId,
                remap.UpdatedTopology,
                remap.RotationKind,
                remap.UpdatedTopology,
                tileFeatureDefinitions: tileFeatureDefinitions);
            if (targetLegality.Verdict != LegalityVerdict.Allowed)
            {
                result = new Free2DTopologyTransitionResult(
                    false,
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remap.TargetAnchor,
                    remap.UpdatedTopology,
                    remap.RotationKind,
                    remap.TargetLocalOffset,
                    remap.TargetVelocity,
                    0,
                    0,
                    ResolveRejectReason(targetLegality),
                    targetLegality,
                    sourceContactProjection);
                return false;
            }

            if (!snapshot.TryResolvePlayerStep(
                    entity.position,
                    directionDelta,
                    out var traversalTarget,
                    out var rotationKind,
                    out var updatedTopology) ||
                rotationKind == CubeRotationKind.None ||
                traversalTarget.face == entity.position.face ||
                remap.TargetAnchor != traversalTarget ||
                remap.RotationKind != rotationKind ||
                !remap.UpdatedTopology.Equals(updatedTopology))
            {
                result = CreateRejected(
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    Free2DTopologyTransitionRejectReason.TopologyTransitionUnavailable,
                    sourceContactProjection);
                return false;
            }

            if (SurfaceContinuousFootprintQueries.IsTargetLocalPoseBlocked(
                    snapshot,
                    entityId,
                    remap.TargetAnchor,
                    remap.TargetLocalOffset,
                    collisionRadiusUnits,
                    directionDelta,
                    updatedTopology,
                    out var footprintRejectReason,
                    out targetLegality,
                    tileFeatureDefinitions))
            {
                result = new Free2DTopologyTransitionResult(
                    false,
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remap.TargetAnchor,
                    updatedTopology,
                    rotationKind,
                    remap.TargetLocalOffset,
                    remap.TargetVelocity,
                    0,
                    0,
                    footprintRejectReason,
                    targetLegality,
                    sourceContactProjection);
                return false;
            }

            result = new Free2DTopologyTransitionResult(
                true,
                entityId,
                entity.position,
                pose.LocalOffset,
                velocityDelta,
                remap.TargetAnchor,
                updatedTopology,
                rotationKind,
                remap.TargetLocalOffset,
                remap.TargetVelocity,
                0,
                0,
                Free2DTopologyTransitionRejectReason.None,
                sourceContactProjection: sourceContactProjection);
            return true;
        }

        private static Free2DTopologyTransitionResult CreateRejected(
            int entityId,
            SurfaceCell sourceAnchor,
            SimulationOffset2 sourceLocalOffset,
            SimulationVelocity2 sourceVelocity,
            Free2DTopologyTransitionRejectReason reason,
            SurfaceContactProjectionResult sourceContactProjection = default)
        {
            return new Free2DTopologyTransitionResult(
                false,
                entityId,
                sourceAnchor,
                sourceLocalOffset,
                sourceVelocity,
                default,
                default,
                CubeRotationKind.None,
                SimulationOffset2.Zero,
                SimulationVelocity2.Zero,
                0,
                0,
                reason,
                sourceContactProjection: sourceContactProjection);
        }

        private static SurfaceContactProjectionAxes ResolveSourceContactProjectionAxes(Vector2Int directionDelta)
        {
            return directionDelta.y != 0
                ? SurfaceContactProjectionAxes.X
                : SurfaceContactProjectionAxes.None;
        }

        private static bool TryResolveDirectTargetOccupancyBlocker(
            WorldSnapshot snapshot,
            int movingEntityId,
            CubeTopologyState updatedTopology,
            SurfaceCell targetAnchor,
            out Free2DTopologyTransitionRejectReason rejectReason)
        {
            if (snapshot.TryGetSolidOccupantAt(updatedTopology, targetAnchor, out var solidOccupant) &&
                solidOccupant.entityId != movingEntityId)
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid;
                return true;
            }

            rejectReason = Free2DTopologyTransitionRejectReason.None;
            return false;
        }

        internal static Free2DTopologyTransitionRejectReason ResolveRejectReason(LegalityResult legality)
        {
            if (legality.Blockers.Count == 0)
            {
                return Free2DTopologyTransitionRejectReason.TargetFaceOutOfBounds;
            }

            return legality.Blockers[0].Kind switch
            {
                LegalityBlockerKind.BoardEdge => Free2DTopologyTransitionRejectReason.TargetFaceOutOfBounds,
                LegalityBlockerKind.Unit => Free2DTopologyTransitionRejectReason.TargetFaceBlockedByUnit,
                LegalityBlockerKind.Solid => Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid,
                LegalityBlockerKind.Reservation => Free2DTopologyTransitionRejectReason.TargetFaceBlockedByReservation,
                LegalityBlockerKind.TileFeature => Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTileFeature,
                _ => Free2DTopologyTransitionRejectReason.TargetFaceOutOfBounds,
            };
        }
    }

    internal static class SurfaceContinuousFootprintQueries
    {
        public static bool IsTargetLocalPoseBlocked(
            WorldSnapshot snapshot,
            int entityId,
            SurfaceCell targetAnchor,
            SimulationOffset2 targetLocalOffset,
            int collisionRadiusUnits,
            Vector2Int entryDirectionDelta,
            CubeTopologyState targetTopology,
            out Free2DTopologyTransitionRejectReason rejectReason,
            out LegalityResult legality,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            rejectReason = Free2DTopologyTransitionRejectReason.None;
            legality = default;
            var radius = Math.Max(0, Math.Min(SimulationFixed.HalfCellUnits - 1, collisionRadiusUnits));
            if (radius <= 0)
            {
                return false;
            }

            if (OverflowsPositiveFootprint(targetLocalOffset.X.RawValue, radius) &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.right, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            if (OverflowsNegativeFootprint(targetLocalOffset.X.RawValue, radius) &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.left, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            var movingForward = entryDirectionDelta.y > 0;
            if (movingForward &&
                OverflowsPositiveFootprint(targetLocalOffset.Y.RawValue, radius) &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.up, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            if (!movingForward &&
                OverflowsNegativeFootprint(targetLocalOffset.Y.RawValue, radius) &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.down, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            return false;
        }

        private static bool OverflowsPositiveFootprint(int rawOffset, int radius)
        {
            return rawOffset + radius > SimulationFixed.HalfCellUnits;
        }

        private static bool OverflowsNegativeFootprint(int rawOffset, int radius)
        {
            return rawOffset - radius < SimulationFixed.MinLocalOffset;
        }

        private static bool IsBlocked(
            WorldSnapshot snapshot,
            int entityId,
            SurfaceCell cell,
            CubeTopologyState targetTopology,
            out Free2DTopologyTransitionRejectReason rejectReason,
            out LegalityResult legality,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                cell,
                entityId,
                targetTopology,
                CubeRotationKind.None,
                targetTopology,
                tileFeatureDefinitions: tileFeatureDefinitions);
            if (legality.Verdict == LegalityVerdict.Allowed)
            {
                rejectReason = Free2DTopologyTransitionRejectReason.None;
                return false;
            }

            rejectReason = SurfaceFree2DTopologyTransitionQueries.ResolveRejectReason(legality);
            return true;
        }
    }
}
