using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    public enum KinematicSweepRejectionReason
    {
        None = 0,
        MissingEntity = 1,
        NonUnit = 2,
        NonCardinalDelta = 3,
        TopologySeam = 4,
        TraversalBlocked = 5,
    }

    public readonly struct KinematicSweepResult
    {
        public KinematicSweepResult(
            int entityId,
            UnitKinematicPose sourcePose,
            SurfaceCell resolvedAnchorCell,
            SimulationOffset2 resolvedLocalOffset,
            SimulationVelocity2 resolvedVelocity,
            bool anchorChanged,
            bool blocked,
            KinematicSweepRejectionReason rejectedBy)
        {
            EntityId = entityId;
            SourcePose = sourcePose;
            ResolvedAnchorCell = resolvedAnchorCell;
            ResolvedLocalOffset = resolvedLocalOffset;
            ResolvedVelocity = resolvedVelocity;
            AnchorChanged = anchorChanged;
            Blocked = blocked;
            RejectedBy = rejectedBy;
        }

        public int EntityId { get; }

        public UnitKinematicPose SourcePose { get; }

        public SurfaceCell ResolvedAnchorCell { get; }

        public SimulationOffset2 ResolvedLocalOffset { get; }

        public SimulationVelocity2 ResolvedVelocity { get; }

        public bool AnchorChanged { get; }

        public bool Blocked { get; }

        public KinematicSweepRejectionReason RejectedBy { get; }

        public UnitKinematicRuntimeState ToRuntimeState(MotionMode mode, ForcedMotionOp forcedOp = ForcedMotionOp.None)
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = ResolvedLocalOffset,
                velocity = ResolvedVelocity,
                mode = Blocked ? MotionMode.Interrupted : mode,
                forcedOp = Blocked ? ForcedMotionOp.Rebound : forcedOp,
                remainingDistanceUnits = 0,
                remainingTicks = 0,
                speedScalePermille = 1000,
                sequenceId = SourcePose.State.sequenceId + 1,
            }.NormalizedForStorage();
        }
    }

    internal static class SurfaceKinematicSweepQueries
    {
        public static bool TryResolveSameFaceDelta(
            WorldSnapshot snapshot,
            int entityId,
            SimulationVelocity2 delta,
            out KinematicSweepResult result,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var tileFeatureEvidence = tileFeatureDefinitions == null
                ? TileFeatureTraversalEvidence.Empty
                : new TileFeatureTraversalEvidence(tileFeatureDefinitions);

            if (!snapshot.TryGetEntity(entityId, out var entity))
            {
                result = CreateRejected(entityId, default, KinematicSweepRejectionReason.MissingEntity);
                return false;
            }

            if (entity.type != EntityType.Unit ||
                !snapshot.TryGetUnitKinematicPose(entityId, out var pose))
            {
                result = CreateRejected(entityId, default, KinematicSweepRejectionReason.NonUnit);
                return false;
            }

            if (delta.X.RawValue != 0 && delta.Y.RawValue != 0)
            {
                result = CreateRejected(entityId, pose, KinematicSweepRejectionReason.NonCardinalDelta);
                return false;
            }

            var targetX = checked(pose.LocalOffset.X.RawValue + delta.X.RawValue);
            var targetY = checked(pose.LocalOffset.Y.RawValue + delta.Y.RawValue);
            if (!UnitKinematicFootprintResolver.TryResolveAnchorDelta(pose.LocalOffset, delta, out var anchorDelta))
            {
                result = new KinematicSweepResult(
                    entityId,
                    pose,
                    pose.AnchorCell,
                    new SimulationOffset2(SimulationFixed.FromRaw(targetX), SimulationFixed.FromRaw(targetY)),
                    delta,
                    anchorChanged: false,
                    blocked: false,
                    rejectedBy: KinematicSweepRejectionReason.None);
                return true;
            }

            if (Math.Abs(anchorDelta.x) + Math.Abs(anchorDelta.y) != 1)
            {
                result = CreateRejected(entityId, pose, KinematicSweepRejectionReason.NonCardinalDelta);
                return false;
            }

            var candidateAnchor = pose.AnchorCell + anchorDelta;
            if (candidateAnchor.face != pose.AnchorCell.face)
            {
                result = CreateClamped(entityId, pose, delta, anchorDelta, KinematicSweepRejectionReason.TopologySeam);
                return true;
            }

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                candidateAnchor,
                entityId,
                snapshot.Topology,
                CubeRotationKind.None,
                snapshot.Topology,
                tileFeatureEvidence);
            if (legality.Verdict != LegalityVerdict.Allowed)
            {
                result = CreateClamped(entityId, pose, delta, anchorDelta, KinematicSweepRejectionReason.TraversalBlocked);
                return true;
            }

            result = new KinematicSweepResult(
                entityId,
                pose,
                candidateAnchor,
                NormalizeCrossedOffset(targetX, targetY, anchorDelta),
                delta,
                anchorChanged: true,
                blocked: false,
                rejectedBy: KinematicSweepRejectionReason.None);
            return true;
        }

        private static KinematicSweepResult CreateRejected(
            int entityId,
            UnitKinematicPose pose,
            KinematicSweepRejectionReason reason)
        {
            return new KinematicSweepResult(
                entityId,
                pose,
                pose.AnchorCell,
                pose.LocalOffset,
                SimulationVelocity2.Zero,
                anchorChanged: false,
                blocked: true,
                rejectedBy: reason);
        }

        private static KinematicSweepResult CreateClamped(
            int entityId,
            UnitKinematicPose pose,
            SimulationVelocity2 delta,
            Vector2Int anchorDelta,
            KinematicSweepRejectionReason reason)
        {
            var clampX = pose.LocalOffset.X.RawValue;
            var clampY = pose.LocalOffset.Y.RawValue;
            if (anchorDelta.x > 0)
            {
                clampX = SimulationFixed.MaxPositiveLocalOffset;
            }
            else if (anchorDelta.x < 0)
            {
                clampX = SimulationFixed.MinLocalOffset;
            }
            else if (anchorDelta.y > 0)
            {
                clampY = SimulationFixed.MaxPositiveLocalOffset;
            }
            else if (anchorDelta.y < 0)
            {
                clampY = SimulationFixed.MinLocalOffset;
            }

            return new KinematicSweepResult(
                entityId,
                pose,
                pose.AnchorCell,
                new SimulationOffset2(SimulationFixed.FromRaw(clampX), SimulationFixed.FromRaw(clampY)),
                SimulationVelocity2.Zero,
                anchorChanged: false,
                blocked: true,
                rejectedBy: reason);
        }

        private static SimulationOffset2 NormalizeCrossedOffset(int targetX, int targetY, Vector2Int anchorDelta)
        {
            if (anchorDelta.x > 0)
            {
                targetX -= SimulationFixed.UnitsPerCell;
            }
            else if (anchorDelta.x < 0)
            {
                targetX += SimulationFixed.UnitsPerCell;
            }
            else if (anchorDelta.y > 0)
            {
                targetY -= SimulationFixed.UnitsPerCell;
            }
            else if (anchorDelta.y < 0)
            {
                targetY += SimulationFixed.UnitsPerCell;
            }

            return new SimulationOffset2(SimulationFixed.FromRaw(targetX), SimulationFixed.FromRaw(targetY));
        }
    }
}
