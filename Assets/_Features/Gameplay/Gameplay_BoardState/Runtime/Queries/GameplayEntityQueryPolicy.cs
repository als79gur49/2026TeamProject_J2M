namespace Game.Feature.Gameplay.BoardState
{
    internal static class GameplayEntityQueryPolicy
    {
        public static bool IsEntityOnActiveFace(EntityState entity, CubeTopologyState topology)
        {
            return topology.IsFaceActive(entity.position.face);
        }

        public static bool IsEntityOccupyingBoard(EntityState entity)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying;
        }

        public static bool ShouldParticipateInGameplayQueries(EntityState entity, CubeTopologyState topology)
        {
            return IsEntityOccupyingBoard(entity) && IsEntityOnActiveFace(entity, topology);
        }

        public static bool IsBlockingPlacementEntity(
            EntityState entity,
            CubeTopologyState topology,
            bool requireGameplayVisibility)
        {
            return requireGameplayVisibility
                ? ShouldParticipateInGameplayQueries(entity, topology)
                : IsEntityOccupyingBoard(entity);
        }
    }
}
