using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Model.Actions
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

    public readonly struct BoardPresenceChangeAction
    {
        public BoardPresenceChangeAction(int entityId, EntityBoardPresence boardPresence)
        {
            EntityId = entityId;
            BoardPresence = boardPresence;
        }

        public int EntityId { get; }

        public EntityBoardPresence BoardPresence { get; }
    }

    public readonly struct TopologyChangeAction
    {
        public TopologyChangeAction(CubeRotationKind rotationKind, CubeTopologyState updatedTopology)
        {
            RotationKind = rotationKind;
            UpdatedTopology = updatedTopology;
        }

        public CubeRotationKind RotationKind { get; }

        public CubeTopologyState UpdatedTopology { get; }
    }
}
