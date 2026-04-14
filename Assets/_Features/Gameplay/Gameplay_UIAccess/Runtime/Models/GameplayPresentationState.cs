using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplayPresentationState
    {
        public GameplayPresentationState(
            CubeTopologyState currentTopology,
            bool isPresentationActive,
            bool hasBlockingPresentation,
            bool isTopologyTransitionActive)
        {
            CurrentTopology = currentTopology;
            IsPresentationActive = isPresentationActive;
            HasBlockingPresentation = hasBlockingPresentation;
            IsTopologyTransitionActive = isTopologyTransitionActive;
        }

        public CubeTopologyState CurrentTopology { get; }

        public bool IsPresentationActive { get; }

        public bool HasBlockingPresentation { get; }

        public bool IsTopologyTransitionActive { get; }
    }
}
