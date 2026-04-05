using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.BoardState
{
    public interface IEnemyAiCommitContext
    {
        void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer);

        void SetFacing(int entityId, Direction facing);
    }

    public interface IEnemyActionCommitContext : IEnemyAiCommitContext
    {
        void SetEnemyActionState(int entityId, EnemyActionRuntimeState state);
    }

    public interface IPlayerControlCommitContext
    {
        void SetPlayerControlState(int entityId, PlayerControlState state);
    }

    public interface IPreMovementStateCommitContext : IPlayerControlCommitContext
    {
        void SetFacing(int entityId, Direction facing);

        void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks);
    }

    internal interface IMovementCommitContext : IPlayerControlCommitContext
    {
        void MoveEntity(int entityId, SurfaceCell destination);

        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void MarkDestroy(int entityId);

        void SetFacing(int entityId, Direction facing);

        void SetBoardPresence(int entityId, EntityBoardPresence boardPresence);

        void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks);

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

    internal interface IWorldWriteContext : IPreMovementStateCommitContext, IMovementCommitContext, IAttackCommitContext, ICleanupCommitContext, IEnemyActionCommitContext
    {
    }
}
