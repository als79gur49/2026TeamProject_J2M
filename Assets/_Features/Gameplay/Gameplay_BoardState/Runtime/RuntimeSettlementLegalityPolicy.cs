using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimeSettlementLegalityPolicy
    {
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

            if (!snapshot.TryGetPlacementBlocker(
                    snapshot.Topology,
                    entityType,
                    cell,
                    ignoredEntityId,
                    out var blocker))
            {
                return LegalityResult.Allowed(
                    LegalityDomain.Settlement,
                    cell,
                    snapshot.Topology,
                    reservationStatus);
            }

            return LegalityResult.Blocked(
                LegalityDomain.Settlement,
                cell,
                snapshot.Topology,
                RuntimeLegalityBlockerFactory.Create(snapshot.EntitiesById, blocker),
                reservationStatus);
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

            if (movementSnapshot.TryGetAuthoritativePlacementBlocker(
                    EntityType.Unit,
                    destinationCell,
                    sourceId,
                    out var blocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    destinationCell,
                    movementSnapshot.Topology,
                    RuntimeLegalityBlockerFactory.Create(movementSnapshot.EntitiesById, blocker),
                    reservationStatus);
            }

            var occupants = new List<EntityState>();
            movementSnapshot.EnumerateUnitsAt(destinationCell, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == sourceId ||
                    occupant.boardPresence != EntityBoardPresence.Occupying)
                {
                    continue;
                }

                if (occupant.entityId == ignoredDeadTargetId &&
                    !IsImpactTargetSurviving(damageProjectionSnapshot, ignoredDeadTargetId))
                {
                    continue;
                }

                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    destinationCell,
                    movementSnapshot.Topology,
                    RuntimeLegalityBlockerFactory.Create(occupant),
                    reservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Settlement,
                destinationCell,
                movementSnapshot.Topology,
                reservationStatus);
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

            if (reservationStatus == ReservationStatus.Conflicted)
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    payload.ContingentDestinationCell,
                    attackSnapshot.Topology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    reservationStatus);
            }

            if (!attackSnapshot.TryGetEntity(payload.SourceEntityId, out _) ||
                !attackSnapshot.TryGetEntity(payload.TargetEntityId, out _))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    payload.ContingentDestinationCell,
                    attackSnapshot.Topology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    reservationStatus);
            }

            if (!HasAcceptedImpactDestroy(
                    destroyResolutions,
                    payload.AttackSourceEntityId,
                    payload.TargetEntityId))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    payload.ContingentDestinationCell,
                    attackSnapshot.Topology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    reservationStatus);
            }

            if (attackSnapshot.TryGetSolidSemanticAt(payload.ContingentDestinationCell, out var solidOccupant) &&
                solidOccupant.Entity.entityId != payload.TargetEntityId)
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    payload.ContingentDestinationCell,
                    attackSnapshot.Topology,
                    RuntimeLegalityBlockerFactory.Create(solidOccupant.Entity),
                    reservationStatus);
            }

            var occupants = new List<EntityState>();
            attackSnapshot.EnumerateUnitsAt(payload.ContingentDestinationCell, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == payload.TargetEntityId ||
                    occupant.boardPresence != EntityBoardPresence.Occupying)
                {
                    continue;
                }

                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    payload.ContingentDestinationCell,
                    attackSnapshot.Topology,
                    RuntimeLegalityBlockerFactory.Create(occupant),
                    reservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Settlement,
                payload.ContingentDestinationCell,
                attackSnapshot.Topology,
                reservationStatus);
        }

        private static bool HasAcceptedImpactDestroy(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int sourceEntityId,
            int targetEntityId)
        {
            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                if (destroyResolutions[i].Accepted &&
                    destroyResolutions[i].SourceId == sourceEntityId &&
                    destroyResolutions[i].TargetId == targetEntityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsImpactTargetSurviving(WorldSnapshot snapshot, int targetEntityId)
        {
            return snapshot.TryGetEntity(targetEntityId, out var targetEntity) &&
                   targetEntity.hp > 0 &&
                   !targetEntity.markedForDeath;
        }
    }
}
