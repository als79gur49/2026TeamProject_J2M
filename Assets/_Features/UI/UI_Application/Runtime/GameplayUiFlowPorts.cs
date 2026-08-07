using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Application
{
    public interface IUiFlowPauseService
    {
        bool IsPaused { get; }

        void Pause();

        void Resume();

        void Toggle();
    }

    public interface IPauseProgressionReadSource
    {
        bool TryRead(out PauseProgressionSnapshot snapshot);
    }

    public sealed class CampaignPauseProgressionReadSource : IPauseProgressionReadSource
    {
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly CampaignStageSequenceResolver _sequenceResolver;

        public CampaignPauseProgressionReadSource(
            CampaignStageSequenceResolver sequenceResolver,
            IGameplayUiPresentationSource presentationSource)
        {
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
        }

        public bool TryRead(out PauseProgressionSnapshot snapshot)
        {
            var currentStageId = _presentationSource.CurrentSnapshot.Stage.StageId;
            if (!currentStageId.IsValid || !_sequenceResolver.Contains(currentStageId))
            {
                snapshot = PauseProgressionSnapshot.Unavailable;
                return false;
            }

            var entries = _sequenceResolver.Entries;
            if (entries == null || entries.Count == 0)
            {
                snapshot = PauseProgressionSnapshot.Unavailable;
                return false;
            }

            var stages = new PauseProgressionStageSnapshot[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                stages[i] = new PauseProgressionStageSnapshot(
                    entries[i].StageId.Value,
                    entries[i].LevelGroupId);
            }

            snapshot = new PauseProgressionSnapshot(
                isAvailable: true,
                stages,
                currentStageId.Value);
            return true;
        }
    }

    public sealed class EmptyPauseProgressionReadSource : IPauseProgressionReadSource
    {
        public static readonly EmptyPauseProgressionReadSource Instance = new();

        private EmptyPauseProgressionReadSource()
        {
        }

        public bool TryRead(out PauseProgressionSnapshot snapshot)
        {
            snapshot = PauseProgressionSnapshot.Unavailable;
            return false;
        }
    }

    public readonly struct GameplayUiFlowPorts
    {
        public GameplayUiFlowPorts(
            IGameplayCommandGateway commandGateway,
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource,
            IGameplayPauseService pauseService,
            IPauseProgressionReadSource pauseProgressionReadSource = null)
        {
            CommandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
            QueryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            PresentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            GameplayPauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            PauseProgressionReadSource = pauseProgressionReadSource ?? EmptyPauseProgressionReadSource.Instance;
            PauseService = new UiFlowPauseServiceAdapter(GameplayPauseService);
        }

        public IGameplayCommandGateway CommandGateway { get; }

        public IGameplayQueryFacade QueryFacade { get; }

        public IGameplayUiPresentationSource PresentationSource { get; }

        public IGameplayPauseService GameplayPauseService { get; }

        public IPauseProgressionReadSource PauseProgressionReadSource { get; }

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
