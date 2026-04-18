using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimeSettlementLegalityPolicy
    {
        public static LegalityResult EvaluateLandingPlacement(SettlementContext context)
        {
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);

            if (!context.OccupancySnapshot.TryGetPlacementBlocker(
                    context.TerminalTopology,
                    context.Actor.EntityType,
                    context.TerminalCell,
                    context.Actor.EntityId,
                    out var blocker))
            {
                return LegalityResult.Allowed(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    context.ReservationStatus);
            }

            return LegalityResult.Blocked(
                LegalityDomain.Settlement,
                context.TerminalCell,
                context.TerminalTopology,
                RuntimeLegalityBlockerFactory.Create(context.OccupancySnapshot.EntitiesById, blocker),
                context.ReservationStatus);
        }

        public static LegalityResult EvaluateLandingPlacement(
            WorldSnapshot snapshot,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    BuildActorRef(snapshot, ignoredEntityId, entityType),
                    cell,
                    snapshot.Topology,
                    SpatialState.Anchored,
                    reservationStatus));
        }

        public static LegalityResult EvaluateJumpLandingCell(
            SettlementContext context,
            JumpLandingEvidence evidence)
        {
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);
            var modifiers = ModifierQuery.GetJumpLandingModifiers(context, evidence);

            if (modifiers.Has(LegalityModifierId.ExclusiveLockedPlayerExactStackAllowance) &&
                IsExclusiveLockedPlayerStack(
                    context.OccupancySnapshot,
                    context.TerminalCell,
                    context.Actor.EntityId))
            {
                return LegalityResult.Allowed(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    context.ReservationStatus);
            }

            if (context.OccupancySnapshot.TryGetAuthoritativePlacementBlocker(
                    context.Actor.EntityType,
                    context.TerminalCell,
                    context.Actor.EntityId,
                    out var blocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(context.OccupancySnapshot.EntitiesById, blocker),
                    context.ReservationStatus);
            }

            var occupants = new List<EntityState>();
            context.OccupancySnapshot.EnumerateUnitsAt(context.TerminalCell, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == context.Actor.EntityId ||
                    !context.OccupancySnapshot.TryGetResolvedSpatialState(occupant.entityId, out var spatialState) ||
                    !ModifierQuery.ShouldParticipateInSettlementBlocking(spatialState))
                {
                    continue;
                }

                if (!IsImpactTargetSurviving(evidence.DamageProjectionSnapshot, occupant.entityId))
                {
                    continue;
                }

                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(occupant),
                    context.ReservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Settlement,
                context.TerminalCell,
                context.TerminalTopology,
                context.ReservationStatus);
        }

        public static LegalityResult EvaluateJumpLandingCell(
            WorldSnapshot movementSnapshot,
            WorldSnapshot damageProjectionSnapshot,
            SurfaceCell destinationCell,
            int sourceId,
            int ignoredDeadTargetId,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (movementSnapshot == null)
            {
                throw new ArgumentNullException(nameof(movementSnapshot));
            }

            if (damageProjectionSnapshot == null)
            {
                throw new ArgumentNullException(nameof(damageProjectionSnapshot));
            }

            return EvaluateJumpLandingCell(
                new SettlementContext(
                    movementSnapshot,
                    BuildActorRef(movementSnapshot, sourceId, EntityType.Unit),
                    destinationCell,
                    movementSnapshot.Topology,
                    SpatialState.Anchored,
                    reservationStatus),
                new JumpLandingEvidence(damageProjectionSnapshot, destinationCell));
        }

        public static LegalityResult EvaluateImpactFollowThrough(
            SettlementContext context,
            ImpactFollowThroughEvidence evidence)
        {
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);
            var modifiers = ModifierQuery.GetImpactFollowThroughModifiers(evidence);

            if (ReservationQuery.BlocksSettlement(context.ReservationStatus))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    context.ReservationStatus);
            }

            if (!context.OccupancySnapshot.TryGetEntity(context.Actor.EntityId, out _) ||
                !context.OccupancySnapshot.TryGetEntity(evidence.TargetId, out _))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    context.ReservationStatus);
            }

            if (!modifiers.Has(LegalityModifierId.AcceptedDestroyVacatesTarget))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    context.ReservationStatus);
            }

            if (context.OccupancySnapshot.TryGetSolidSemanticAt(context.TerminalCell, out var solidOccupant) &&
                solidOccupant.Entity.entityId != evidence.TargetId)
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(solidOccupant.Entity),
                    context.ReservationStatus);
            }

            var occupants = new List<EntityState>();
            context.OccupancySnapshot.EnumerateUnitsAt(context.TerminalCell, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == evidence.TargetId ||
                    !context.OccupancySnapshot.TryGetResolvedSpatialState(occupant.entityId, out var spatialState) ||
                    !ModifierQuery.ShouldParticipateInSettlementBlocking(spatialState))
                {
                    continue;
                }

                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(occupant),
                    context.ReservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Settlement,
                context.TerminalCell,
                context.TerminalTopology,
                context.ReservationStatus);
        }

        public static LegalityResult EvaluateImpactFollowThrough(
            WorldSnapshot attackSnapshot,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            MovementImpactReservationPayload payload,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (attackSnapshot == null)
            {
                throw new ArgumentNullException(nameof(attackSnapshot));
            }

            if (destroyResolutions == null)
            {
                throw new ArgumentNullException(nameof(destroyResolutions));
            }

            return EvaluateImpactFollowThrough(
                new SettlementContext(
                    attackSnapshot,
                    BuildActorRef(attackSnapshot, payload.SourceEntityId, EntityType.Unit),
                    payload.ContingentDestinationCell,
                    attackSnapshot.Topology,
                    SpatialState.Anchored,
                    reservationStatus),
                new ImpactFollowThroughEvidence(
                    payload.AttackSourceEntityId,
                    payload.TargetEntityId,
                    destroyResolutions));
        }

        private static bool IsImpactTargetSurviving(WorldSnapshot snapshot, int targetEntityId)
        {
            return snapshot.TryGetEntity(targetEntityId, out var targetEntity) &&
                   targetEntity.hp > 0 &&
                   !targetEntity.markedForDeath;
        }

        private static bool IsExclusiveLockedPlayerStack(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            int sourceEntityId)
        {
            var occupants = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, occupants);
            var sawLockedPlayer = false;

            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == sourceEntityId ||
                    occupant.hp <= 0 ||
                    occupant.markedForDeath ||
                    !snapshot.TryGetResolvedSpatialState(occupant.entityId, out var spatialState) ||
                    !ModifierQuery.ShouldParticipateInSettlementBlocking(spatialState))
                {
                    continue;
                }

                if (!snapshot.TryGetPlayerControlState(occupant.entityId, out _))
                {
                    return false;
                }

                if (sawLockedPlayer)
                {
                    return false;
                }

                sawLockedPlayer = true;
            }

            return sawLockedPlayer;
        }

        private static LegalityActorRef BuildActorRef(
            WorldSnapshot snapshot,
            int entityId,
            EntityType entityType)
        {
            return StateQuery.BuildActorRef(snapshot, entityId, entityType);
        }
    }
}
