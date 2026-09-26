using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPlayerHudQuery : IGameplayPlayerHudQuery, IGameplayHudChanceChanges, IGameplayHudRevisionedQuery<GameplayPlayerHudReadModel>
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

        public bool IsChanceDisplayUpdating => (_campaignChancesReadSource as SaveSlotCampaignChancesReadSource)?.IsUpdating == true;
        public bool TryGetChanceRevision(out long revision)
        {
            revision = 0;
            return _campaignChancesReadSource is SaveSlotCampaignChancesReadSource source && source.TryGetRevision(out revision);
        }
        public GameplayPlayerHudReadModel ReadChance(out long revision)
        {
            var source = _campaignChancesReadSource as SaveSlotCampaignChancesReadSource;
            var readVersion = source?.ReadVersion ?? 0;
            TryGetChanceRevision(out revision);
            try { return Read(); }
            finally
            {
                if (source != null && source.ReadVersion != readVersion) revision = source.LastReadRevision;
            }
        }

        public bool TryGetRevision(out GameplayHudQueryStamp stamp)
        {
            var window = _admissionPolicy?.ProbeWindowGeneration() ?? 0;
            // Actor lookup stays on the committed window, including cache-hit probes.
            _admissionPolicy?.TryGetCommittedControllableActor(out _);
            var supported = TryGetChanceRevision(out var chance);
            stamp = new GameplayHudQueryStamp(this, generation: window, chanceRevision: chance);
            // Detailed diagnostics retain their existing observation records and live launch identity.
            return (_campaignChancesReadSource == null || supported) && !CampaignChanceHudDiagnostics.IsEnabled;
        }
        public GameplayHudQueryRead<GameplayPlayerHudReadModel> ReadWithRevision()
        {
            var window = _admissionPolicy?.ProbeWindowGeneration() ?? 0;
            var value = ReadChance(out var chance);
            var supported = TryGetRevision(out var after);
            var stable = window == (_admissionPolicy?.ProbeWindowGeneration() ?? 0) && chance == after.ChanceRevision;
            return new GameplayHudQueryRead<GameplayPlayerHudReadModel>(value, after, supported && stable);
        }

        public GameplayPlayerHudReadModel Read()
        {
            if (_inputHost == null ||
                _admissionPolicy == null ||
                !_admissionPolicy.TryCreateSnapshot(out var snapshot))
            {
                return default;
            }

            if (_inputHost.CampaignGameMode == GameMode.Casual)
            {
                var hp = snapshot.TryGetEntity(_inputHost.PlayerEntityId, out var player) ? player.hp : 0;
                return new GameplayPlayerHudReadModel(hasHealth: true, hp: hp, maxHp: CampaignSaveSlotPolicy.CasualMaxHp);
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
