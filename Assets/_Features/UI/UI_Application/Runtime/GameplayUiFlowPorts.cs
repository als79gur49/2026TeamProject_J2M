using System;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.UI.Application
{
    public interface IUiFlowPauseService
    {
        bool IsPaused { get; }

        void Pause();

        void Resume();

        void Toggle();
    }

    public readonly struct GameplayUiFlowPorts
    {
        public GameplayUiFlowPorts(
            IGameplayCommandGateway commandGateway,
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource,
            IGameplayPauseService pauseService)
        {
            CommandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
            QueryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            PresentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            GameplayPauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            PauseService = new UiFlowPauseServiceAdapter(GameplayPauseService);
        }

        public IGameplayCommandGateway CommandGateway { get; }

        public IGameplayQueryFacade QueryFacade { get; }

        public IGameplayUiPresentationSource PresentationSource { get; }

        public IGameplayPauseService GameplayPauseService { get; }

        public IUiFlowPauseService PauseService { get; }

        private sealed class UiFlowPauseServiceAdapter : IUiFlowPauseService
        {
            private readonly IGameplayPauseService _pauseService;

            public UiFlowPauseServiceAdapter(IGameplayPauseService pauseService)
            {
                _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            }

            public bool IsPaused => _pauseService.IsPaused;

            public void Pause()
            {
                _pauseService.Pause();
            }

            public void Resume()
            {
                _pauseService.Resume();
            }

            public void Toggle()
            {
                _pauseService.Toggle();
            }
        }
    }
}
