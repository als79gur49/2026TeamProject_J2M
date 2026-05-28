using System;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host
{
    public interface ICampaignChancesReadSource
    {
        bool TryReadChances(
            out int remainingChances,
            out int maxChances,
            out GameplayChanceAudioPolicy audioPolicy);
    }

    internal sealed class CampaignChanceDisplayOverride
    {
        private bool _hasOverride;
        private int _remainingChances;
        private int _maxChances;
        private GameplayChanceAudioPolicy _audioPolicy;

        public void Set(
            int remainingChances,
            int maxChances,
            GameplayChanceAudioPolicy audioPolicy = GameplayChanceAudioPolicy.Default)
        {
            _hasOverride = true;
            _remainingChances = Math.Max(0, remainingChances);
            _maxChances = Math.Max(0, maxChances);
            _audioPolicy = audioPolicy;
        }

        public void Clear()
        {
            _hasOverride = false;
            _remainingChances = 0;
            _maxChances = 0;
            _audioPolicy = GameplayChanceAudioPolicy.Default;
        }

        public bool TryRead(
            out int remainingChances,
            out int maxChances,
            out GameplayChanceAudioPolicy audioPolicy)
        {
            remainingChances = _remainingChances;
            maxChances = _maxChances;
            audioPolicy = _hasOverride
                ? _audioPolicy
                : GameplayChanceAudioPolicy.Default;
            return _hasOverride;
        }
    }

    internal sealed class SaveSlotCampaignChancesReadSource : ICampaignChancesReadSource
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly CampaignChanceDisplayOverride _displayOverride;
        private readonly SaveSlotStore _saveSlotStore;

        public SaveSlotCampaignChancesReadSource(
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            CampaignChanceDisplayOverride displayOverride = null)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
            _displayOverride = displayOverride;
        }

        public bool TryReadChances(
            out int remainingChances,
            out int maxChances,
            out GameplayChanceAudioPolicy audioPolicy)
        {
            remainingChances = 0;
            maxChances = SaveSlotStore.DefaultRemainingChances;
            audioPolicy = GameplayChanceAudioPolicy.Default;
            if (_displayOverride != null &&
                _displayOverride.TryRead(
                    out var overrideRemainingChances,
                    out var overrideMaxChances,
                    out var overrideAudioPolicy))
            {
                remainingChances = overrideRemainingChances;
                maxChances = overrideMaxChances;
                remainingChances = Clamp(remainingChances, 0, maxChances);
                audioPolicy = overrideAudioPolicy;
                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.SourceRead)
                {
                    SourceType = GetType().Name,
                    TryReadResult = true,
                    RemainingChances = remainingChances,
                    MaxChances = maxChances,
                    FailureReason = maxChances > 0
                        ? CampaignChanceReadFailureReason.None
                        : CampaignChanceReadFailureReason.MaxChancesZero,
                    SaveSlotStoreKey = _saveSlotStore.PlayerPrefsKey,
                    ActiveSlotProviderKey = _activeSlotProvider.PlayerPrefsKey,
                });
                return true;
            }

            if (!_activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber))
            {
                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.SourceRead)
                {
                    SourceType = GetType().Name,
                    TryReadResult = false,
                    FailureReason = CampaignChanceReadFailureReason.NoActiveSlot,
                    SaveSlotStoreKey = _saveSlotStore.PlayerPrefsKey,
                    ActiveSlotProviderKey = _activeSlotProvider.PlayerPrefsKey,
                });
                return false;
            }

            var slot = _saveSlotStore.LoadSlot(activeSlotNumber);
            var launchStageId = StageLaunchContextStore.CurrentStageId;
            var normalizedRemainingChances = slot.RemainingChances <= 0
                ? SaveSlotStore.DefaultRemainingChances
                : slot.RemainingChances;
            remainingChances = Clamp(normalizedRemainingChances, 0, maxChances);
            CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.SourceRead)
            {
                SourceType = GetType().Name,
                TryReadResult = true,
                FailureReason = !slot.CurrentStageId.IsValid
                    ? CampaignChanceReadFailureReason.StageIdMissing
                    : launchStageId.IsValid && !slot.CurrentStageId.Equals(launchStageId)
                        ? CampaignChanceReadFailureReason.StageIdMismatch
                        : maxChances > 0
                            ? CampaignChanceReadFailureReason.None
                            : CampaignChanceReadFailureReason.MaxChancesZero,
                LaunchStageId = launchStageId.IsValid ? launchStageId.Value : string.Empty,
                SourceStageId = slot.CurrentStageId.IsValid ? slot.CurrentStageId.Value : string.Empty,
                HasActiveSlot = true,
                ActiveSlotNumber = activeSlotNumber,
                RemainingChances = remainingChances,
                MaxChances = maxChances,
                SaveSlotStoreKey = _saveSlotStore.PlayerPrefsKey,
                ActiveSlotProviderKey = _activeSlotProvider.PlayerPrefsKey,
            });
            return true;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
