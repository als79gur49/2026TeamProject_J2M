using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    internal interface IWorldStateMutationPort
    {
        bool TryGetEntity(int entityId, out EntityState entity);
        void MoveEntityTo(int entityId, SurfaceCell destination);
        void SpawnEntity(EntityState entity);
        void RemoveEntity(int entityId);
        void ApplyDamage(int entityId, int amount);
        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);
        void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer);
        void MarkDestroy(int entityId);
        void SetFacing(int entityId, Direction facing);
        void SetBoardPresence(int entityId, EntityBoardPresence boardPresence);
        void SetTopology(CubeTopologyState topology);
    }
}
