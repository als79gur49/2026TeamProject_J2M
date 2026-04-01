using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.BoardState
{
    public interface IEnemyAiCommitContext
    {
        void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer);
    }

    public interface IPlayerControlCommitContext
    {
        void SetPlayerControlState(int entityId, PlayerControlState state);
    }

    internal interface IPreMovementStateCommitContext : IPlayerControlCommitContext
    {
    }

    internal interface IMovementCommitContext : IPlayerControlCommitContext
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

    internal interface IWorldWriteContext : IPreMovementStateCommitContext, IMovementCommitContext, IAttackCommitContext, ICleanupCommitContext, IEnemyAiCommitContext
    {
    }
}
