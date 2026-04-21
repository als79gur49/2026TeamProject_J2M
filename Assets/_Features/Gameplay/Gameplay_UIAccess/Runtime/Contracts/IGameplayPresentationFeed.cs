using System;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    public interface IGameplayPresentationFeed
    {
        event Action<GameplayPresentationFrame> FramePublished;

        event Action<GameplayPresentationState> StateChanged;

        GameplayPresentationState CurrentState { get; }

        StageCompletionReadModel CurrentStageCompletion { get; }
    }
}
