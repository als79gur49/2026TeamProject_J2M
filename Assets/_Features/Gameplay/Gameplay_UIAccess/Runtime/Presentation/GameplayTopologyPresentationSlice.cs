using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Presentation
{
    public readonly struct GameplayTopologyPresentationSlice
    {
        public GameplayTopologyPresentationSlice(
            GameplayUiTopology sourceTopology,
            GameplayUiTopology destinationTopology,
            GameplayUiRotationKind rotationKind)
        {
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
        }

        public GameplayUiTopology SourceTopology { get; }

        public GameplayUiTopology DestinationTopology { get; }

        public GameplayUiRotationKind RotationKind { get; }
    }
}
