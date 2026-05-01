using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Entities
{
    public readonly struct EntityLogicCreationContext
    {
        public EntityLogicCreationContext(WorldSnapshot snapshot, in EntityState entity)
        {
            Snapshot = snapshot ?? throw new System.ArgumentNullException(nameof(snapshot));
            Entity = entity;
        }

        public WorldSnapshot Snapshot { get; }

        public EntityState Entity { get; }
    }

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
            IPreMovementStateCommitContext writeContext,
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

    public enum EnemyActionStage
    {
        BeforeAttackCollection = 0,
        AfterAttack = 1,
    }

    public interface IEnemyActionStateLogic : IEntityLogic
    {
        void CommitEnemyActionState(
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyActionStage stage,
            IEnemyActionCommitContext writeContext,
            List<EnemyActionTransition> transitions);
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

    public interface IFrontFaceSupportLogic : IEntityLogic
    {
        void CollectFrontFaceSupportContributors(
            WorldSnapshot snapshot,
            in TickInput input,
            List<FrontFaceSupportContributor> buffer);
    }

    public interface IEntityLogicSourceBinding
    {
        int ControlledEntityId { get; }
    }

    public interface IEntityLogicFactory
    {
        bool CanCreate(in EntityLogicCreationContext context);
        IEntityLogic Create(in EntityLogicCreationContext context);
    }

    public sealed class EntityLogicSet
    {
        public EntityLogicSet(
            IReadOnlyList<IPreMovementStateLogic> preMovementStateLogics,
            IReadOnlyList<IEnemyAiStateLogic> aiStateLogics,
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            IReadOnlyList<IAttackEntityLogic> attackLogics)
            : this(
                preMovementStateLogics,
                aiStateLogics,
                System.Array.Empty<IEnemyActionStateLogic>(),
                System.Array.Empty<IFrontFaceSupportLogic>(),
                movementLogics,
                attackLogics)
        {
        }

        public EntityLogicSet(
            IReadOnlyList<IPreMovementStateLogic> preMovementStateLogics,
            IReadOnlyList<IEnemyAiStateLogic> aiStateLogics,
            IReadOnlyList<IEnemyActionStateLogic> enemyActionStateLogics,
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            IReadOnlyList<IAttackEntityLogic> attackLogics)
            : this(
                preMovementStateLogics,
                aiStateLogics,
                enemyActionStateLogics,
                System.Array.Empty<IFrontFaceSupportLogic>(),
                movementLogics,
                attackLogics)
        {
        }

        public EntityLogicSet(
            IReadOnlyList<IPreMovementStateLogic> preMovementStateLogics,
            IReadOnlyList<IEnemyAiStateLogic> aiStateLogics,
            IReadOnlyList<IEnemyActionStateLogic> enemyActionStateLogics,
            IReadOnlyList<IFrontFaceSupportLogic> frontFaceSupportLogics,
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            IReadOnlyList<IAttackEntityLogic> attackLogics)
        {
            PreMovementStateLogics = preMovementStateLogics ?? throw new System.ArgumentNullException(nameof(preMovementStateLogics));
            AiStateLogics = aiStateLogics ?? throw new System.ArgumentNullException(nameof(aiStateLogics));
            EnemyActionStateLogics = enemyActionStateLogics ?? throw new System.ArgumentNullException(nameof(enemyActionStateLogics));
            FrontFaceSupportLogics = frontFaceSupportLogics ?? throw new System.ArgumentNullException(nameof(frontFaceSupportLogics));
            MovementLogics = movementLogics ?? throw new System.ArgumentNullException(nameof(movementLogics));
            AttackLogics = attackLogics ?? throw new System.ArgumentNullException(nameof(attackLogics));
        }

        public IReadOnlyList<IPreMovementStateLogic> PreMovementStateLogics { get; }

        public IReadOnlyList<IEnemyAiStateLogic> AiStateLogics { get; }

        public IReadOnlyList<IEnemyActionStateLogic> EnemyActionStateLogics { get; }

        public IReadOnlyList<IFrontFaceSupportLogic> FrontFaceSupportLogics { get; }

        public IReadOnlyList<IMovementEntityLogic> MovementLogics { get; }

        public IReadOnlyList<IAttackEntityLogic> AttackLogics { get; }
    }

    public interface ISnapshotEntityLogicProvider
    {
        EntityLogicSet Build(
            WorldSnapshot snapshot,
            IReadOnlyList<IEntityLogic> staticEntityLogics);
    }

    internal interface IEnemyGlidePresentationSettingsResolver
    {
        bool TryResolveEnemyGlidePresentationSettings(
            WorldSnapshot snapshot,
            in EntityState entity,
            out EnemyGlidePresentationSettings settings);
    }
}
