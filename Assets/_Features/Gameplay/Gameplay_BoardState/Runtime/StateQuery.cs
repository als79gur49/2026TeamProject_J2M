using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class StateQuery
    {
        public static LegalityActorRef BuildActorRef(
            WorldSnapshot snapshot,
            int entityId,
            EntityType entityType)
        {
            if (entityId > 0 &&
                snapshot.TryGetResolvedSpatialState(entityId, out var spatialState))
            {
                snapshot.TryGetEnemyGlideState(entityId, out var glideState);
                return new LegalityActorRef(entityId, entityType, spatialState, glideState);
            }

            return new LegalityActorRef(
                entityId,
                entityType,
                SpatialStateResolver.Resolve(
                    EntityBoardPresence.Detached,
                    jumpState: null,
                    phasedState: null,
                    isFaceActive: false));
        }

        public static LegalityActorRef BuildActorRef(WorldSnapshot snapshot, in EntityState actor)
        {
            if (snapshot.TryGetResolvedSpatialState(actor.entityId, out var spatialState))
            {
                snapshot.TryGetEnemyGlideState(actor.entityId, out var glideState);
                return new LegalityActorRef(actor.entityId, actor.type, spatialState, glideState);
            }

            return new LegalityActorRef(
                actor.entityId,
                actor.type,
                SpatialStateResolver.Resolve(actor, snapshot.Topology, jumpState: null, phasedState: null));
        }

        public static bool ClaimsAuthoritativeOccupancy(in ResolvedSpatialState spatialState)
        {
            return spatialState.ClaimsAuthoritativeOccupancy;
        }

        public static bool IsGameplayVisible(in ResolvedSpatialState spatialState)
        {
            return spatialState.IsGameplayVisible;
        }

        public static LegalityCapabilitySet GetBaseCapabilities(in ResolvedSpatialState spatialState)
        {
            return spatialState.Kind switch
            {
                SpatialState.Phased => LegalityCapabilitySet.None
                    .With(LegalityCapabilityId.IgnoreTraversalUnitBlocker)
                    .With(LegalityCapabilityId.IgnoreTraversalSolidBlocker),
                _ => LegalityCapabilitySet.None,
            };
        }
    }
}
