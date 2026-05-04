using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayTickPresentationExtensionContext
    {
        public GameplayTickPresentationExtensionContext(
            TickResult result,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            Result = result;
            Topology = topology;
            StateStore = stateStore;
            Projector = projector;
        }

        public TickResult Result { get; }

        public CubeTopologyState Topology { get; }

        public GameplayPresentationStateStore StateStore { get; }

        public GameplayCubeProjector Projector { get; }
    }

    public interface IGameplayTickPresentationExtension
    {
        void ResetSession();

        void Present(in GameplayTickPresentationExtensionContext context);

        void UpdatePresentation(float deltaTime);

        void HardCleanup();
    }
}
