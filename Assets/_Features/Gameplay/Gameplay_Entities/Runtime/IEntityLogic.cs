using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
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
}
