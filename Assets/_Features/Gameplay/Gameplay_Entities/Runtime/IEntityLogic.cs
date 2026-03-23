using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Model.Phases;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Entities
{
    public interface IEntityLogic
    {
        void CollectMovementIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawMovementIntent> buffer);

        void CollectAttackIntents(
            WorldSnapshot snapshot,
            List<RawAttackIntent> buffer);
    }

    public interface IEntityLogicSourceBinding
    {
        bool ControlsEntity(int entityId, TickPhase phase);
    }

    public interface IEntityLogicFactory
    {
        bool CanCreate(in EntityState entity);
        IEntityLogic Create(in EntityState entity);
    }

    public interface ISnapshotEntityLogicProvider
    {
        IReadOnlyList<IEntityLogic> Build(
            WorldSnapshot snapshot,
            IReadOnlyList<IEntityLogic> staticEntityLogics);
    }
}
