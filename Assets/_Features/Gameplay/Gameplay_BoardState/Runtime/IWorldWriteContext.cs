using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    public interface IEnemyAiCommitContext
    {
        void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer);
    }

    internal interface IMovementCommitContext
    {
        void MoveEntity(int entityId, SurfaceCell destination);

        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void MarkDestroy(int entityId);

        void SetFacing(int entityId, Direction facing);

        void SetBoardPresence(int entityId, EntityBoardPresence boardPresence);

        void SetTopology(CubeTopologyState topology);
    }

    internal interface IAttackCommitContext
    {
        void ApplyDamage(int entityId, int amount);

        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void MarkDestroy(int entityId);

        void SpawnEntity(EntityState entity);
    }

    internal interface ICleanupCommitContext
    {
        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void RemoveEntity(int entityId);
    }

    internal interface IWorldWriteContext : IMovementCommitContext, IAttackCommitContext, ICleanupCommitContext, IEnemyAiCommitContext
    {
    }
}
