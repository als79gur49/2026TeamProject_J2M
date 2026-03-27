namespace Game.Feature.Gameplay.BoardState
{
    internal interface IWorldStateMutationPort
    {
        bool TryGetEntity(int entityId, out EntityState entity);
        void MoveEntityTo(int entityId, UnityEngine.Vector2Int destination);
        void SpawnEntity(EntityState entity);
        void RemoveEntity(int entityId);
        void ApplyDamage(int entityId, int amount);
        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);
        void MarkDestroy(int entityId);
        void SetFacing(int entityId, Direction facing);
    }
}
