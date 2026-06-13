namespace Game.Feature.Gameplay.BoardState
{
    internal static class LegalityDiagnosticsFormatter
    {
        public static string FormatStableSummary(LegalityResult legality)
        {
            return $"{FormatStableSummaryPrefix(legality)}|LegalityBlockerKinds={RuntimeLegalityBlockerFactory.FormatKinds(legality.Blockers)}";
        }

        public static string FormatStableSummary(
            LegalityResult legality,
            in ResolvedSpatialState actorSpatialState)
        {
            return
                $"{FormatStableSummaryPrefix(legality)}|ActorSpatialKind={actorSpatialState.Kind}|ActorSpatialSource={actorSpatialState.Source}|ActorSpatialOccClaim={(actorSpatialState.ClaimsAuthoritativeOccupancy ? 1 : 0)}|ActorSpatialGameplayVisible={(actorSpatialState.IsGameplayVisible ? 1 : 0)}|LegalityBlockerKinds={RuntimeLegalityBlockerFactory.FormatKinds(legality.Blockers)}";
        }

        public static string ResolveSpawnBlockedReason(LegalityResult legality)
        {
            var blocker = GetPrimaryBlocker(legality);
            return blocker.Kind switch
            {
                LegalityBlockerKind.BoardEdge => "SpawnDestinationOutsideBoard",
                LegalityBlockerKind.Solid => "SpawnDestinationBlockedByEntity",
                LegalityBlockerKind.Unit => "SpawnDestinationBlockedByEntity",
                LegalityBlockerKind.Reservation => "SpawnDestinationBlockedByReservation",
                LegalityBlockerKind.TileFeature => "SpawnDestinationBlockedByTileFeature",
                _ => "SpawnDestinationBlocked",
            };
        }

        public static string FormatPlacementBlocker(LegalityResult legality)
        {
            var blocker = GetPrimaryBlocker(legality);
            return blocker.Kind == LegalityBlockerKind.Solid || blocker.Kind == LegalityBlockerKind.Unit
                ? $"Cell=({legality.Cell.x},{legality.Cell.y})|Occupant={blocker.EntityId}|OccupantType={blocker.EntityType}"
                : $"Cell=({legality.Cell.x},{legality.Cell.y})";
        }

        public static string FormatRespawnSkippedEvent(
            EntityState entity,
            LegalityResult legality,
            int tickIndex)
        {
            var blocker = GetPrimaryBlocker(legality);
            var reason = blocker.Kind switch
            {
                LegalityBlockerKind.BoardEdge => "BoardEdge",
                LegalityBlockerKind.Unit => "Entity",
                LegalityBlockerKind.Solid => "Entity",
                LegalityBlockerKind.Reservation => "Reservation",
                LegalityBlockerKind.TileFeature => "TileFeature",
                _ => blocker.Kind.ToString(),
            };
            var prefix =
                $"RespawnSkipped|E={entity.entityId}|Pos=({entity.position.x},{entity.position.y})|Face={entity.position.face}|Tick={tickIndex}|Reason={reason}";
            return blocker.Kind == LegalityBlockerKind.Unit || blocker.Kind == LegalityBlockerKind.Solid
                ? $"{prefix}|BlockerEntity={blocker.EntityId}|BlockerType={blocker.EntityType}"
                : prefix;
        }

        private static string FormatStableSummaryPrefix(LegalityResult legality)
        {
            var requiredBottomFace = legality.TransitionRequirement.Kind == TransitionRequirementKind.TopologyUpdate
                ? legality.TransitionRequirement.UpdatedTopology.BottomFace.ToString()
                : "None";
            return
                $"LegalityDomain={legality.Domain}|LegalityVerdict={legality.Verdict}|ReservationStatus={legality.Reservation}|TransitionRequirementKind={legality.TransitionRequirement.Kind}|RotationKind={legality.TransitionRequirement.RotationKind}|RequiredTopologyBottomFace={requiredBottomFace}";
        }

        private static LegalityBlocker GetPrimaryBlocker(LegalityResult legality)
        {
            return legality.Blockers.Count > 0 ? legality.Blockers[0] : default;
        }
    }
}
