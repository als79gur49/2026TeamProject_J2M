using System;
using Game.Feature.Stages;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public sealed class StageResultScreenPayload : IScreenPayload
    {
        public StageResultScreenPayload(
            StageNavigationRequest continueStageRequest,
            StageNavigationRequest retryStageRequest,
            StageNavigationRequest nextStageRequest,
            bool isContinueEnabled = true)
        {
            ContinueStageRequest = continueStageRequest;
            RetryStageRequest = retryStageRequest;
            NextStageRequest = nextStageRequest;
            IsContinueEnabled = isContinueEnabled && continueStageRequest.IsValid;
        }

        public StageNavigationRequest ContinueStageRequest { get; }

        public StageNavigationRequest RetryStageRequest { get; }

        public StageNavigationRequest NextStageRequest { get; }

        public bool IsContinueEnabled { get; }
    }

    public sealed class LevelFailedScreenPayload : IScreenPayload
    {
        public LevelFailedScreenPayload(
            LocalizedTextDescriptor detailTextDescriptor,
            StageNavigationRequest restartLevelRequest,
            TerminalSessionToken terminalToken = default)
        {
            TitleTextDescriptor = TerminalResultTextDescriptors.LevelFailedTitle;
            DetailTextDescriptor = detailTextDescriptor;
            RestartStageLabelDescriptor = TerminalResultTextDescriptors.RestartStage;
            MainMenuLabelDescriptor = TerminalResultTextDescriptors.MainMenu;
            RestartLevelRequest = restartLevelRequest;
            TerminalToken = terminalToken;
        }

        public LevelFailedScreenPayload(
            string titleText,
            string detailText,
            string restartLevelLabel,
            string mainLabel,
            StageNavigationRequest restartLevelRequest,
            TerminalSessionToken terminalToken = default)
            : this(
                TerminalResultTextDescriptors.ChancesExhaustedDetail,
                restartLevelRequest,
                terminalToken)
        {
        }

        public LocalizedTextDescriptor TitleTextDescriptor { get; }

        public LocalizedTextDescriptor DetailTextDescriptor { get; }

        public LocalizedTextDescriptor RestartStageLabelDescriptor { get; }

        public LocalizedTextDescriptor MainMenuLabelDescriptor { get; }

        public StageNavigationRequest RestartLevelRequest { get; }

        public TerminalSessionToken TerminalToken { get; }

        public long TerminalClaimId => TerminalToken.Sequence;

        public LevelFailedScreenPayload(
            string titleText,
            string detailText,
            string restartLevelLabel,
            string mainLabel,
            StageNavigationRequest restartLevelRequest,
            long terminalClaimId)
            : this(
                titleText,
                detailText,
                restartLevelLabel,
                mainLabel,
                restartLevelRequest,
                terminalClaimId > 0
                    ? new TerminalSessionToken(
                        TerminalSessionRegistry.Authority.AuthorityGeneration,
                        terminalClaimId)
                    : default)
        {
        }
    }

    public sealed class GameClearScreenPayload : IScreenPayload
    {
        public static readonly GameClearScreenPayload Default = new();

        public GameClearScreenPayload()
        {
            TitleTextDescriptor = TerminalResultTextDescriptors.GameClearTitle;
            MainMenuLabelDescriptor = TerminalResultTextDescriptors.MainMenu;
        }

        public LocalizedTextDescriptor TitleTextDescriptor { get; }

        public LocalizedTextDescriptor MainMenuLabelDescriptor { get; }
    }

    public sealed class StageResultScreenPresenter
    {
        private readonly ILocalizedTextResolver _localizedTextResolver;

        public StageResultScreenPresenter(ILocalizedTextResolver localizedTextResolver)
        {
            _localizedTextResolver = localizedTextResolver
                ?? throw new ArgumentNullException(nameof(localizedTextResolver));
        }

        public StageResultScreenViewModel ViewModel { get; } = new();

        public void Apply(StageResultScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                _localizedTextResolver.Resolve(TerminalResultTextDescriptors.Continue),
                payload.IsContinueEnabled);
        }
    }

    public sealed class LevelFailedScreenPresenter
    {
        private readonly ILocalizedTextResolver _localizedTextResolver;

        public LevelFailedScreenPresenter(ILocalizedTextResolver localizedTextResolver)
        {
            _localizedTextResolver = localizedTextResolver
                ?? throw new ArgumentNullException(nameof(localizedTextResolver));
        }

        public LevelFailedScreenViewModel ViewModel { get; } = new();

        public void Apply(LevelFailedScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                Resolve(payload.TitleTextDescriptor),
                Resolve(payload.DetailTextDescriptor),
                Resolve(payload.RestartStageLabelDescriptor),
                Resolve(payload.MainMenuLabelDescriptor));
        }

        private string Resolve(LocalizedTextDescriptor descriptor)
        {
            return string.IsNullOrEmpty(descriptor.Table) || string.IsNullOrEmpty(descriptor.Key)
                ? string.Empty
                : _localizedTextResolver.Resolve(descriptor) ?? string.Empty;
        }
    }

    public sealed class GameClearScreenPresenter
    {
        private readonly ILocalizedTextResolver _localizedTextResolver;

        public GameClearScreenPresenter(ILocalizedTextResolver localizedTextResolver)
        {
            _localizedTextResolver = localizedTextResolver
                ?? throw new ArgumentNullException(nameof(localizedTextResolver));
        }

        public GameClearScreenViewModel ViewModel { get; } = new();

        public void Apply(GameClearScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                _localizedTextResolver.Resolve(payload.TitleTextDescriptor),
                _localizedTextResolver.Resolve(payload.MainMenuLabelDescriptor));
        }
    }
}
