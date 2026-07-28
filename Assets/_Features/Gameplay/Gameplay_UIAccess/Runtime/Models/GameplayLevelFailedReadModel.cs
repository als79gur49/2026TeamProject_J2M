using System;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public enum GameplayLevelFailureReason
    {
        None = 0,
        ChancesExhausted = 1,
    }

    public sealed class GameplayLevelFailedReadModel
    {
        public GameplayLevelFailedReadModel(
            GameplayLevelFailureReason reason,
            StageNavigationRequest restartLevelRequest)
        {
            if (!restartLevelRequest.IsValid)
            {
                throw new ArgumentException(
                    "Level failed restart flow requires a valid StageNavigationRequest.",
                    nameof(restartLevelRequest));
            }

            Reason = reason;
            RestartLevelRequest = restartLevelRequest;
        }

        public GameplayLevelFailureReason Reason { get; }

        public StageNavigationRequest RestartLevelRequest { get; }
    }
}
