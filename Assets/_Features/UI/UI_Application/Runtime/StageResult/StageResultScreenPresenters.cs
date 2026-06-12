using System;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
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
                payload.TitleText,
                payload.DetailText,
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
