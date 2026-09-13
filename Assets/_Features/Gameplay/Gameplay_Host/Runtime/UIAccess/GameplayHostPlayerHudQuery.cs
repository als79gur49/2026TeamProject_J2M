using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPlayerHudQuery : IGameplayPlayerHudQuery
    {
        private readonly GameplayHostCommandAdmissionPolicy _admissionPolicy;
        private readonly ICampaignChancesReadSource _campaignChancesReadSource;
        private readonly GameplayInputHost _inputHost;

        public GameplayHostPlayerHudQuery(
            GameplayInputHost inputHost,
            GameplayHostCommandAdmissionPolicy admissionPolicy,
            ICampaignChancesReadSource campaignChancesReadSource = null)
        {
            _inputHost = inputHost;
            _admissionPolicy = admissionPolicy;
            _campaignChancesReadSource = campaignChancesReadSource;
            CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.HudQueryConstructed)
            {
                SourceType = campaignChancesReadSource != null ? campaignChancesReadSource.GetType().Name : string.Empty,
                SourceIsNull = campaignChancesReadSource == null,
                FailureReason = campaignChancesReadSource == null
                    ? CampaignChanceReadFailureReason.SourceMissing
                    : CampaignChanceReadFailureReason.None,
            });
        }

        public GameplayPlayerHudReadModel Read()
        {
            if (_inputHost == null ||
                _admissionPolicy == null ||
                !_admissionPolicy.TryCreateSnapshot(out _))
            {
                return default;
            }

            var remainingChances = 0;
            var maxChances = 0;
            var chanceAudioPolicy = GameplayChanceAudioPolicy.Default;
            var hasRemainingChances = _campaignChancesReadSource != null &&
                                      _campaignChancesReadSource.TryReadChances(
                                          out remainingChances,
                                          out maxChances,
                                          out chanceAudioPolicy);
            if (CampaignChanceHudDiagnostics.IsEnabled)
            {
                var hasPlayer = _admissionPolicy.TryGetCommittedControllableActor(out _);
                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.HudQueryRead)
                {
                    SourceType = _campaignChancesReadSource != null ? _campaignChancesReadSource.GetType().Name : string.Empty,
                    SourceIsNull = _campaignChancesReadSource == null,
                    TryReadResult = hasRemainingChances,
                    FailureReason = _campaignChancesReadSource == null
                        ? CampaignChanceReadFailureReason.SourceMissing
                        : hasRemainingChances
                            ? maxChances > 0
                                ? CampaignChanceReadFailureReason.None
                                : CampaignChanceReadFailureReason.MaxChancesZero
                            : CampaignChanceReadFailureReason.Unknown,
                    RemainingChances = hasRemainingChances ? remainingChances : 0,
                    MaxChances = hasRemainingChances ? maxChances : 0,
                    PlayerFound = hasPlayer,
                    FinalHasChances = hasRemainingChances && maxChances > 0,
                });
            }

            return new GameplayPlayerHudReadModel(
                hasRemainingChances: hasRemainingChances,
                remainingChances: hasRemainingChances ? remainingChances : 0,
                maxChances: hasRemainingChances ? maxChances : 0,
                chanceAudioPolicy: hasRemainingChances
                    ? chanceAudioPolicy
                    : GameplayChanceAudioPolicy.Default);
        }
    }
}
