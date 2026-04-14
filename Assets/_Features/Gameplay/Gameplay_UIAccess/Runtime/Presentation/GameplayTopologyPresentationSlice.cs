using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.UIAccess.Presentation
{
    public readonly struct GameplayTopologyPresentationSlice
    {
        public GameplayTopologyPresentationSlice(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind)
        {
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
        }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }
    }
}
