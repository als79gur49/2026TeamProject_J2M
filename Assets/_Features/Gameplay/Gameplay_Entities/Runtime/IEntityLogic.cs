using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
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

    public interface IAttackEntityLogic : IEntityLogic
    {
        void CollectAttackIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawAttackIntent> buffer);
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
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            IReadOnlyList<IAttackEntityLogic> attackLogics)
        {
            MovementLogics = movementLogics ?? throw new System.ArgumentNullException(nameof(movementLogics));
            AttackLogics = attackLogics ?? throw new System.ArgumentNullException(nameof(attackLogics));
        }

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
