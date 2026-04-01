using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Entities
{
    public interface IEntityLogic
    {
    }

    public interface IMovementEntityLogic : IEntityLogic
    {
        void CollectMovementIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawMovementIntent> buffer);
    }

    public interface IPreMovementStateLogic : IEntityLogic
    {
        void CommitPreMovementState(
            WorldSnapshot snapshot,
            in TickInput input,
            IPlayerControlCommitContext writeContext,
            List<string> updates,
            List<PlayerActionTransition> actionTransitions);
    }

    public interface IAttackEntityLogic : IEntityLogic
    {
        void CollectAttackIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawAttackIntent> buffer);
    }

    public enum EnemyAiTransitionStage
    {
        BeforeMovement = 0,
        BeforeAttack = 1,
        AfterAttack = 2,
    }

    public interface IEnemyAiStateLogic : IEntityLogic
    {
        void CommitAiTransitions(
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyAiTransitionStage stage,
            IEnemyAiCommitContext writeContext,
            List<string> transitions);
    }

    public interface IEntityLogicSourceBinding
    {
        int ControlledEntityId { get; }
    }

    public interface IEntityLogicFactory
    {
        bool CanCreate(in EntityState entity);
        IEntityLogic Create(in EntityState entity);
    }

    public sealed class EntityLogicSet
    {
        public EntityLogicSet(
            IReadOnlyList<IPreMovementStateLogic> preMovementStateLogics,
            IReadOnlyList<IEnemyAiStateLogic> aiStateLogics,
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            IReadOnlyList<IAttackEntityLogic> attackLogics)
        {
            PreMovementStateLogics = preMovementStateLogics ?? throw new System.ArgumentNullException(nameof(preMovementStateLogics));
            AiStateLogics = aiStateLogics ?? throw new System.ArgumentNullException(nameof(aiStateLogics));
            MovementLogics = movementLogics ?? throw new System.ArgumentNullException(nameof(movementLogics));
            AttackLogics = attackLogics ?? throw new System.ArgumentNullException(nameof(attackLogics));
        }

        public IReadOnlyList<IPreMovementStateLogic> PreMovementStateLogics { get; }

        public IReadOnlyList<IEnemyAiStateLogic> AiStateLogics { get; }

        public IReadOnlyList<IMovementEntityLogic> MovementLogics { get; }

        public IReadOnlyList<IAttackEntityLogic> AttackLogics { get; }
    }

    public interface ISnapshotEntityLogicProvider
    {
        EntityLogicSet Build(
            WorldSnapshot snapshot,
            IReadOnlyList<IEntityLogic> staticEntityLogics);
    }
}
