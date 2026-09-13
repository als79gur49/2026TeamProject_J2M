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
        internal long Revision { get; private set; }
        internal SaveSlotCampaignChancesReadSource Source { get; set; }
        internal CampaignChanceDisplayUpdateScope ActiveUpdate { get; private set; }
        internal CampaignChanceDisplayUpdateScope BeginUpdate()
        {
            if (ActiveUpdate != null) throw new InvalidOperationException("A Chance display update is already active.");
            ActiveUpdate = new CampaignChanceDisplayUpdateScope(this, Source);
            Revision++;
            return ActiveUpdate;
        }
        internal void EndUpdate(CampaignChanceDisplayUpdateScope scope)
        {
            if (!ReferenceEquals(ActiveUpdate, scope)) return;
            ActiveUpdate = null;
            Revision++;
        }
        private int _remainingChances;
        private int _maxChances;
        private GameplayChanceAudioPolicy _audioPolicy;

        public void Set(
            int remainingChances,
            int maxChances,
            GameplayChanceAudioPolicy audioPolicy = GameplayChanceAudioPolicy.Default)
        {
            if (!_hasOverride || _remainingChances != Math.Max(0, remainingChances) ||
                _maxChances != Math.Max(0, maxChances) || _audioPolicy != audioPolicy) Revision++;
            _hasOverride = true;
            _remainingChances = Math.Max(0, remainingChances);
            _maxChances = Math.Max(0, maxChances);
            _audioPolicy = audioPolicy;
        }

        public void Clear()
        {
            Revision++;
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

    internal sealed class CampaignChanceDisplayUpdateScope : IDisposable
    {
        private CampaignChanceDisplayOverride _owner;
        private readonly CampaignHudBeforeMutationCapture _capture;
        private GameplayPlayerHudReadModel _frozen;
        private bool _hasFrozen;
        internal CampaignChanceDisplayUpdateScope(CampaignChanceDisplayOverride owner, SaveSlotCampaignChancesReadSource source)
        {
            _owner = owner;
            if (owner.TryRead(out var remaining, out var max, out var policy))
            {
                _frozen = new GameplayPlayerHudReadModel(true, Math.Max(0, Math.Min(remaining, max)), max, policy);
                _hasFrozen = true;
            }
            else if (source != null) _hasFrozen = source.TryGetValidatedDisplay(out _frozen);
            _capture = source?.Session?.CaptureBeforeMutation();
        }
        internal void ObserveBeforeMutation(CampaignSlotState state)
        {
            if (_hasFrozen || state == null) return;
            _frozen = new GameplayPlayerHudReadModel(true,
                CampaignSaveSlotPolicy.RequireValidRemainingChances(state.RemainingChances),
                CampaignSaveSlotPolicy.DefaultRemainingChances);
            _hasFrozen = true;
        }
        internal bool TryRead(out GameplayPlayerHudReadModel display)
        {
            ObserveBeforeMutation(_capture?.State);
            display = _frozen;
            return _hasFrozen;
        }
        internal void Complete() => Dispose();
        public void Dispose()
        {
            _capture?.Dispose();
            _owner?.EndUpdate(this);
            _owner = null;
        }
    }

    internal sealed class SaveSlotCampaignChancesReadSource : ICampaignChancesReadSource, IDisposable
    {
        private readonly CampaignChanceDisplayOverride _displayOverride;
        private readonly CampaignRunningSlotContext _runningSlotContext;
        private readonly ICampaignSaveQuery _saveSlotStore;
        internal ICampaignHudReadSession Session { get; }
        private long _observedSaved = -1, _observedDisplay = -1, _revision;
        private long _lastSavedRevision = -1, _lastGeneration = -1;
        private GameplayPlayerHudReadModel _lastDisplay;
        internal long ReadVersion { get; private set; }
        internal long LastReadRevision { get; private set; }
        private void RecordReadRevision()
        {
            TryGetRevision(out var revision);
            LastReadRevision = revision;
            ReadVersion++;
        }
        internal bool IsUpdating => _displayOverride?.ActiveUpdate != null;
        internal bool TryGetRevision(out long revision)
        {
            var saved = Session?.Revision ?? 0;
            var display = _displayOverride?.Revision ?? 0;
            if (_observedSaved != saved || _observedDisplay != display)
            {
                _observedSaved = saved;
                _observedDisplay = display;
                _revision++;
            }
            revision = _revision;
            return Session != null;
        }
        internal bool TryGetValidatedDisplay(out GameplayPlayerHudReadModel display)
        {
            display = _lastDisplay;
            return Session != null && _lastSavedRevision == Session.Revision &&
                _lastGeneration == Session.Generation && display.HasRemainingChances;
        }
        internal void Reload() => Session?.Reload();
        public void Dispose()
        {
            _displayOverride?.ActiveUpdate?.Dispose();
            if (_displayOverride != null && ReferenceEquals(_displayOverride.Source, this)) _displayOverride.Source = null;
            Session?.Dispose();
        }

        public SaveSlotCampaignChancesReadSource(
            ICampaignSaveQuery saveSlotStore,
            CampaignRunningSlotContext runningSlotContext,
            CampaignChanceDisplayOverride displayOverride = null)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _runningSlotContext = runningSlotContext ?? throw new ArgumentNullException(nameof(runningSlotContext));
            _displayOverride = displayOverride;
            Session = (saveSlotStore as ICampaignHudReadProvider)?.OpenHudReadSession(runningSlotContext.SlotNumber);
            if (_displayOverride != null) _displayOverride.Source = this;
        }

        public bool TryReadChances(
            out int remainingChances,
            out int maxChances,
            out GameplayChanceAudioPolicy audioPolicy)
        {
            try { return ReadCore(out remainingChances, out maxChances, out audioPolicy); }
            catch { RecordReadRevision(); throw; }
        }

        private bool ReadCore(
            out int remainingChances,
            out int maxChances,
            out GameplayChanceAudioPolicy audioPolicy)
        {
            if (_displayOverride?.ActiveUpdate != null &&
                _displayOverride.ActiveUpdate.TryRead(out var frozen))
            {
                remainingChances = frozen.RemainingChances;
                maxChances = frozen.MaxChances;
                audioPolicy = frozen.ChanceAudioPolicy;
                RecordReadRevision();
                return frozen.HasRemainingChances;
            }
            remainingChances = 0;
            maxChances = CampaignSaveSlotPolicy.DefaultRemainingChances;
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
                RecordReadRevision();
                if (CampaignChanceHudDiagnostics.IsEnabled)
                {
                    CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.SourceRead)
                    {
                        SourceType = GetType().Name,
                        TryReadResult = true,
                        RemainingChances = remainingChances,
                        MaxChances = maxChances,
                        FailureReason = maxChances > 0
                            ? CampaignChanceReadFailureReason.None
                            : CampaignChanceReadFailureReason.MaxChancesZero,
                        SaveStoreDiagnosticsKey = _saveSlotStore.DiagnosticsKey,
                        ActiveSlotNumber = _runningSlotContext.SlotNumber,
                    });
                }
                return true;
            }

            var runningSlotNumber = _runningSlotContext.SlotNumber;
            var entry = Session != null ? Session.Read() : _saveSlotStore.LoadSlot(runningSlotNumber);
            if (entry == null || entry.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Campaign slot '{runningSlotNumber}' is empty or missing.");
            }

            var slot = entry.State;
            var launchStageId = StageLaunchContextStore.CurrentStageId;
            var validatedRemainingChances = CampaignSaveSlotPolicy.RequireValidRemainingChances(
                slot.RemainingChances);
            remainingChances = Clamp(validatedRemainingChances, 0, maxChances);
            _lastDisplay = new GameplayPlayerHudReadModel(true, remainingChances, maxChances, audioPolicy);
            _lastSavedRevision = Session?.Revision ?? -1;
            _lastGeneration = Session?.Generation ?? -1;
            RecordReadRevision();
            if (CampaignChanceHudDiagnostics.IsEnabled)
            {
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
                    ActiveSlotNumber = runningSlotNumber,
                    RemainingChances = remainingChances,
                    MaxChances = maxChances,
                    SaveStoreDiagnosticsKey = _saveSlotStore.DiagnosticsKey,
                });
            }
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
