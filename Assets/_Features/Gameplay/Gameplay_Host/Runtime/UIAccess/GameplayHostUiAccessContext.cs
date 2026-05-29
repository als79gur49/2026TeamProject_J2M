using System;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    public sealed class GameplayHostUiAccessContext : IDisposable
    {
        public GameplayHostUiAccessContext(
            IGameplayCommandGateway commandGateway,
            IGameplayQueryFacade queryFacade,
            IGameplayPresentationFeed presentationFeed,
            IGameplayPauseService pauseService,
            IDemoGameplayOverrideCommandPort demoGameplayOverrideCommandPort = null,
            IDemoStageControlCompletionBridge demoStageControlCompletionBridge = null)
        {
            CommandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
            QueryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            PresentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
            PauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            DemoGameplayOverrideCommandPort = demoGameplayOverrideCommandPort;
            DemoStageControlCompletionBridge = demoStageControlCompletionBridge;
        }

        public IGameplayCommandGateway CommandGateway { get; }

        public IGameplayQueryFacade QueryFacade { get; }

        public IGameplayPresentationFeed PresentationFeed { get; }

        public IGameplayPauseService PauseService { get; }

        public IDemoGameplayOverrideCommandPort DemoGameplayOverrideCommandPort { get; }

        public IDemoStageControlCompletionBridge DemoStageControlCompletionBridge { get; }

        public void Dispose()
        {
            if (CommandGateway is IDisposable disposableCommandGateway)
            {
                disposableCommandGateway.Dispose();
            }

            if (PresentationFeed is IDisposable disposableFeed)
            {
                disposableFeed.Dispose();
            }
        }
    }
}
