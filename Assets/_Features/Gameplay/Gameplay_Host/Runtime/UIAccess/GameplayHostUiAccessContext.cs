using System;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    public sealed class GameplayHostUiAccessContext : IDisposable
    {
        private readonly IDisposable _admissionPolicyLifetime;
        private bool _isDisposed;

        public GameplayHostUiAccessContext(
            IDisposable admissionPolicyLifetime,
            IGameplayQueryFacade queryFacade,
            IGameplayPresentationFeed presentationFeed,
            IGameplayPauseService pauseService,
            IDemoGameplayOverrideCommandPort demoGameplayOverrideCommandPort = null,
            IDemoStageControlCompletionBridge demoStageControlCompletionBridge = null,
            CampaignStageSequenceResolver campaignStageSequenceResolver = null)
        {
            _admissionPolicyLifetime = admissionPolicyLifetime ?? throw new ArgumentNullException(nameof(admissionPolicyLifetime));
            QueryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            PresentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
            PauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            DemoGameplayOverrideCommandPort = demoGameplayOverrideCommandPort;
            DemoStageControlCompletionBridge = demoStageControlCompletionBridge;
            CampaignStageSequenceResolver = campaignStageSequenceResolver;
        }

        public IGameplayQueryFacade QueryFacade { get; }

        public IGameplayPresentationFeed PresentationFeed { get; }

        public IGameplayPauseService PauseService { get; }

        public IDemoGameplayOverrideCommandPort DemoGameplayOverrideCommandPort { get; }

        public IDemoStageControlCompletionBridge DemoStageControlCompletionBridge { get; }

        public CampaignStageSequenceResolver CampaignStageSequenceResolver { get; }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            try
            {
                _admissionPolicyLifetime.Dispose();
            }
            finally
            {
                (PresentationFeed as IDisposable)?.Dispose();
            }
        }
    }
}
