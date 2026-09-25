using System;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public enum GameplayLevelFailureReason
    {
        None = 0,
        ChancesExhausted = 1,
        CasualDeath = 2,
        CampaignChancesExhausted = 3,
    }

    public sealed class GameplayLevelFailedReadModel
    {
        public GameplayLevelFailedReadModel(
            GameplayLevelFailureReason reason,
            StageNavigationRequest restartLevelRequest,
            TerminalSessionToken terminalToken = default)
        {
            if (!restartLevelRequest.IsValid)
            {
                throw new ArgumentException(
                    "Level failed restart flow requires a valid StageNavigationRequest.",
                    nameof(restartLevelRequest));
            }

            Reason = reason;
            RestartLevelRequest = restartLevelRequest;
            TerminalToken = terminalToken;
        }

        public GameplayLevelFailedReadModel(
            string titleText,
            string detailText,
            string restartLevelLabel,
            string mainLabel,
            StageNavigationRequest restartLevelRequest,
            TerminalSessionToken terminalToken = default)
            : this(
                GameplayLevelFailureReason.ChancesExhausted,
                restartLevelRequest,
                terminalToken)
        {
        }

        public GameplayLevelFailureReason Reason { get; }

        public StageNavigationRequest RestartLevelRequest { get; }

        public TerminalSessionToken TerminalToken { get; }

        public long TerminalClaimId => TerminalToken.Sequence;

        public GameplayLevelFailedReadModel(
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
}
