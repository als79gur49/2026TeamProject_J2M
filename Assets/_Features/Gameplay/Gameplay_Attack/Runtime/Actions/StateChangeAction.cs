using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Attack.Actions
{
    public readonly struct StateChangeAction
    {
        public StateChangeAction(int entityId, EntityPhaseState state, int stateTimer)
        {
            EntityId = entityId;
            State = state;
            StateTimer = stateTimer;
        }

        public int EntityId { get; }

        public EntityPhaseState State { get; }

        public int StateTimer { get; }
    }
}
