using System;
using Game.Feature.Stages;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class StageResultScreenPayload : IScreenPayload
    {
        public StageResultScreenPayload(
            string continueLabel,
            StageNavigationRequest continueStageRequest,
            StageNavigationRequest retryStageRequest,
            StageNavigationRequest nextStageRequest,
            bool isContinueEnabled = true)
        {
            ContinueLabel = continueLabel ?? string.Empty;
            ContinueStageRequest = continueStageRequest;
            RetryStageRequest = retryStageRequest;
            NextStageRequest = nextStageRequest;
            IsContinueEnabled = isContinueEnabled && continueStageRequest.IsValid;
        }

        public string ContinueLabel { get; }

        public StageNavigationRequest ContinueStageRequest { get; }

        public StageNavigationRequest RetryStageRequest { get; }

        public StageNavigationRequest NextStageRequest { get; }

        public bool IsContinueEnabled { get; }
    }

    public sealed class LevelFailedScreenPayload : IScreenPayload
    {
        public LevelFailedScreenPayload(
            string titleText,
            string detailText,
            string restartLevelLabel,
            string mainLabel,
            StageNavigationRequest restartLevelRequest)
        {
            TitleText = string.IsNullOrWhiteSpace(titleText) ? "Level Failed" : titleText;
            DetailText = detailText ?? string.Empty;
            RestartLevelLabel = string.IsNullOrWhiteSpace(restartLevelLabel)
                ? "Restart Level"
                : restartLevelLabel;
            MainLabel = string.IsNullOrWhiteSpace(mainLabel) ? "Main" : mainLabel;
            RestartLevelRequest = restartLevelRequest;
        }

        public string TitleText { get; }

        public string DetailText { get; }

        public string RestartLevelLabel { get; }

        public string MainLabel { get; }

        public StageNavigationRequest RestartLevelRequest { get; }
    }

    public sealed class GameClearScreenPayload : IScreenPayload
    {
        public static readonly GameClearScreenPayload Default = new("Game Clear", "Main");

        public GameClearScreenPayload(
            string titleText,
            string mainLabel)
        {
            TitleText = string.IsNullOrWhiteSpace(titleText) ? "Game Clear" : titleText;
            MainLabel = string.IsNullOrWhiteSpace(mainLabel) ? "Main" : mainLabel;
        }

        public string TitleText { get; }

        public string MainLabel { get; }
    }

    public sealed class StageResultScreenPresenter
    {
        public StageResultScreenViewModel ViewModel { get; } = new StageResultScreenViewModel();

        public void Apply(StageResultScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.ContinueLabel,
                payload.IsContinueEnabled);
        }
    }

    public sealed class LevelFailedScreenPresenter
    {
        public LevelFailedScreenViewModel ViewModel { get; } = new LevelFailedScreenViewModel();

        public void Apply(LevelFailedScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.DetailText,
                payload.RestartLevelLabel,
                payload.MainLabel);
        }
    }

    public sealed class GameClearScreenPresenter
    {
        public GameClearScreenViewModel ViewModel { get; } = new GameClearScreenViewModel();

        public void Apply(GameClearScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.MainLabel);
        }
    }
}
