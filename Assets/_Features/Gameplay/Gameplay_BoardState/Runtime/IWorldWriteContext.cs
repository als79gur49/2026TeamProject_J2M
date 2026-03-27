namespace Game.Feature.Gameplay.BoardState
{
    public interface IWorldWriteContext
    {
        void MoveEntity(int entityId, SurfaceCell destination);

        void ApplyDamage(int entityId, int amount);

        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void MarkDestroy(int entityId);

        void SpawnEntity(EntityState entity);

        void RemoveEntity(int entityId);

        void SetFacing(int entityId, Direction facing);
    }
}
