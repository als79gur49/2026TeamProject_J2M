namespace Game.Feature.Gameplay.BoardState
{
    internal interface IWorldStateMutationPort
    {
        bool TryGetEntity(int entityId, out EntityState entity);
        void AddNewEntity(EntityState entity);
        void UpdateEntity(EntityState entity);
        void ClearOccupancy(EntityState entity);
        void SetOccupancy(EntityState entity);
        void RemoveEntityRecord(int entityId);
    }
}
