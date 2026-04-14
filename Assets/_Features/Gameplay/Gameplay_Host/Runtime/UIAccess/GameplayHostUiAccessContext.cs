using System;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    public sealed class GameplayHostUiAccessContext : IDisposable
    {
        public GameplayHostUiAccessContext(
            IGameplayCommandGateway commandGateway,
            IGameplayQueryFacade queryFacade,
            IGameplayPresentationFeed presentationFeed,
            IGameplayPauseService pauseService)
        {
            CommandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
            QueryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            PresentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
            PauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
        }

        public IGameplayCommandGateway CommandGateway { get; }

        public IGameplayQueryFacade QueryFacade { get; }

        public IGameplayPresentationFeed PresentationFeed { get; }

        public IGameplayPauseService PauseService { get; }

        public void Dispose()
        {
            if (PresentationFeed is IDisposable disposableFeed)
            {
                disposableFeed.Dispose();
            }
        }
    }
}
