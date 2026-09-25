using System;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    public interface IGameplayCampaignFailureSource
    {
        event Action CampaignRunFailed;
        bool HasCampaignRunFailure { get; }
    }

    public interface IGameplayPresentationFeed
    {
        event Action<GameplayPresentationFrame> FramePublished;

        event Action<GameplayPresentationState> StateChanged;

        event Action<GameplayLevelFailedReadModel> LevelFailedCommitted;

        GameplayPresentationState CurrentState { get; }

        MinimalStageCompletionReadModel CurrentMinimalStageCompletion { get; }

        GameplayLevelFailedReadModel CurrentLevelFailed { get; }

        bool HasPendingStageClearPresentation { get; }
    }
}
