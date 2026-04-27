using System;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public sealed class GameplayLevelFailedReadModel
    {
        public GameplayLevelFailedReadModel(
            string titleText,
            string detailText,
            string restartLevelLabel,
            string mainLabel,
            StageNavigationRequest restartLevelRequest)
        {
            if (!restartLevelRequest.IsValid)
            {
                throw new ArgumentException(
                    "Level failed restart flow requires a valid StageNavigationRequest.",
                    nameof(restartLevelRequest));
            }

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
}
