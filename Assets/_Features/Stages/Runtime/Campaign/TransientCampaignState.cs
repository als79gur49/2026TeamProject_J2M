using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    /// <summary>
    /// Non-persistent campaign slot storage for tests and short-lived diagnostic compositions.
    /// Production and DirectPlay campaign flows use the JSON-backed composition provider.
    /// </summary>
    public sealed class TransientCampaignSaveSlotStore : ICampaignSaveRuntime, ICampaignHudReadProvider
    {
        public const string DefaultDiagnosticsKey = "transient-campaign-state";

        private static readonly object Gate = new();
        private static readonly Dictionary<string, CampaignSlotEntry[]> EntriesByNamespace = new();

        private readonly string _diagnosticsKey;
        private readonly Func<string> _utcNowProvider;
        private readonly CampaignHudReadStore _hudReads;

        public TransientCampaignSaveSlotStore(string diagnosticsKey = DefaultDiagnosticsKey)
            : this(diagnosticsKey, DefaultUtcNow)
        {
        }

        internal TransientCampaignSaveSlotStore(
            string diagnosticsKey,
            Func<string> utcNowProvider)
        {
            _diagnosticsKey = string.IsNullOrWhiteSpace(diagnosticsKey)
                ? throw new ArgumentException("A transient campaign namespace is required.", nameof(diagnosticsKey))
                : diagnosticsKey;
            _hudReads = CampaignHudReadRegistry.Acquire("transient:" + _diagnosticsKey);
            _utcNowProvider = utcNowProvider ??
                throw new ArgumentNullException(nameof(utcNowProvider));
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Transient campaign state has not been read.");
        }

        CampaignHudReadStore ICampaignHudReadProvider.HudReadStore => _hudReads;

        ICampaignHudReadSession ICampaignHudReadProvider.OpenHudReadSession(int slotNumber) =>
            _hudReads.Open(slotNumber, LoadAllWithReport);

        public string DiagnosticsKey => _diagnosticsKey;

        public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; }

        public CampaignSlotEntry[] LoadAll()
        {
            return LoadAllEntries();
        }

        private CampaignSlotEntry[] LoadAllEntries()
        {
            return LoadAllWithReport().Slots;
        }

        public CampaignSaveLoadResult LoadAllWithReport()
        {
            lock (Gate)
            {
                var result = LoadAllWithReportCore();
                _hudReads.ObserveGate(false);
                _hudReads.Observe(result);
                return result;
            }
        }

        private CampaignSaveLoadResult LoadAllWithReportCore()
        {
            lock (Gate)
            {
                if (!EntriesByNamespace.TryGetValue(_diagnosticsKey, out var entries))
                {
                    LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Transient campaign state is empty.");
                    return new CampaignSaveLoadResult(CreateEmptyEntries(), LastCampaignLoadReport);
                }

                LastCampaignLoadReport = CampaignSaveLoadReport.Loaded(
                    "Transient campaign state loaded.",
                    _diagnosticsKey);
                return new CampaignSaveLoadResult(
                    (CampaignSlotEntry[])entries.Clone(),
                    LastCampaignLoadReport);
            }
        }

        public CampaignSlotEntry LoadSlot(int slotNumber)
        {
            return LoadEntry(slotNumber);
        }

        private CampaignSlotEntry LoadEntry(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            return LoadAllEntries()[slotNumber - 1];
        }

        public CampaignContinuePreparationResult PrepareContinue(
            CampaignContinuePreparationCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            lock (Gate)
            {
                var preparation = CampaignContinuePreparationPolicy.Evaluate(
                    GetOccupiedState(command.SlotNumber),
                    command);
                if (preparation.Succeeded && preparation.LevelGroupSynchronized)
                {
                    StoreState(preparation.CommittedState);
                }

                return preparation;
            }
        }

        public CampaignSlotState InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            return InitializeNewGameState(slotNumber, sequenceResolver, lastPlayedAt);
        }

        private CampaignSlotState InitializeNewGameState(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var state = CampaignSlotStateFactory.CreateNewGame(
                slotNumber,
                sequenceResolver,
                lastPlayedAt);
            lock (Gate)
            {
                if (string.IsNullOrWhiteSpace(state.LastPlayedAt))
                {
                    state = CampaignSlotStateFactory.WithLastPlayedAt(state, Now());
                }

                StoreState(state);
                return state;
            }
        }

        public void MarkIntroComicCompleted(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                var transition = CampaignSlotTransitionEngine.ApplyComicCompletion(
                    GetOccupiedState(slotNumber),
                    new CampaignComicCompletionCommand(
                        CampaignComicCompletionKind.Intro),
                    Now());
                if (!transition.Succeeded)
                {
                    throw CreateComicTransitionException(transition);
                }

                StoreState(transition.Slot);
            }
        }

        public void MarkOutroComicCompleted(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                var transition = CampaignSlotTransitionEngine.ApplyComicCompletion(
                    GetOccupiedState(slotNumber),
                    new CampaignComicCompletionCommand(
                        CampaignComicCompletionKind.Outro),
                    Now());
                if (!transition.Succeeded)
                {
                    throw CreateComicTransitionException(transition);
                }

                StoreState(transition.Slot);
            }
        }

        public CampaignSlotState SetActiveStageForDiagnostics(
            int slotNumber,
            StageId stageId,
            string levelGroupId)
        {
            return SetActiveStageForDiagnosticsState(slotNumber, stageId, levelGroupId);
        }

        private CampaignSlotState SetActiveStageForDiagnosticsState(
            int slotNumber,
            StageId stageId,
            string levelGroupId)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Diagnostic stage selection requires a valid stage id.", nameof(stageId));
            }

            lock (Gate)
            {
                var state = CampaignSlotStateFactory.SelectStageForDiagnostics(
                    GetOccupiedState(slotNumber),
                    stageId,
                    levelGroupId,
                    Now());
                StoreState(state);
                return state;
            }
        }

        public CampaignSlotState ImportSlotSeed(CampaignSlotSeedImportRequest request)
        {
            var state = CampaignSlotStateFactory.CreateImportedSeed(
                request ?? throw new ArgumentNullException(nameof(request)));
            lock (Gate)
            {
                if (string.IsNullOrWhiteSpace(state.LastPlayedAt))
                {
                    state = CampaignSlotStateFactory.WithLastPlayedAt(state, Now());
                }

                StoreState(state);
                return state;
            }
        }

        public CampaignDeathCommitResult CommitDeath(
            int slotNumber,
            CampaignDeathTransitionPlan plan)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                var transition = CampaignSlotTransitionEngine.ApplyDeath(
                    GetOccupiedState(slotNumber),
                    plan,
                    Now());
                if (!transition.Succeeded)
                {
                    throw CreateDeathTransitionException(transition);
                }

                StoreState(transition.Slot);
                return new CampaignDeathCommitResult(transition.Slot);
            }
        }

        public CampaignStageClearCommitResult CommitStageClear(
            int slotNumber,
            CampaignStageClearCommitRequest request)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                var before = GetOccupiedState(slotNumber);
                _hudReads.ObserveBeforeMutation(before);
                var transition = CampaignSlotTransitionEngine.ApplyStageClear(
                    before,
                    request,
                    Now());
                if (!transition.Succeeded)
                {
                    throw CreateStageClearTransitionException(transition);
                }

                StoreState(transition.Slot);
                return new CampaignStageClearCommitResult(
                    transition.Slot,
                    transition.PreviousRemainingChances.Value);
            }
        }

        private static Exception CreateDeathTransitionException(
            CampaignSlotTransitionResult transition)
        {
            if (transition.ReasonCode == CampaignSlotTransitionReasonCode.DeathPlanInvalid)
            {
                return new ArgumentException("Death transition plan is invalid.", "plan");
            }

            if (transition.ReasonCode ==
                CampaignSlotTransitionReasonCode.DeathPreconditionChanged)
            {
                return new InvalidOperationException(
                    "Campaign slot changed after the death transition was planned.");
            }

            if (transition.ReasonCode == CampaignSlotTransitionReasonCode.DeathCounterOverflow)
            {
                return new ArgumentException(
                    "Campaign slot replacement does not satisfy the current runtime slot contract.",
                    "slot");
            }

            return new InvalidOperationException(
                "Campaign slot does not satisfy the current runtime slot contract.");
        }

        private static Exception CreateStageClearTransitionException(
            CampaignSlotTransitionResult transition)
        {
            if (transition.ReasonCode ==
                CampaignSlotTransitionReasonCode.StageClearRequestNull)
            {
                return new ArgumentNullException("request");
            }

            if (transition.ReasonCode ==
                CampaignSlotTransitionReasonCode.StageClearRequestInvalid)
            {
                return new ArgumentException(
                    "Stage clear commit request is invalid.",
                    "request");
            }

            if (transition.ReasonCode ==
                CampaignSlotTransitionReasonCode.StageClearPreconditionChanged)
            {
                return new InvalidOperationException(
                    "Campaign slot changed after the stage clear transition was planned.");
            }

            return new InvalidOperationException(
                "Campaign slot does not satisfy the current runtime slot contract.");
        }

        private static Exception CreateComicTransitionException(
            CampaignSlotTransitionResult transition)
        {
            return transition.ReasonCode ==
                   CampaignSlotTransitionReasonCode.ComicCommandInvalid
                ? new ArgumentException(
                    "Comic completion command is invalid.",
                    "command")
                : new InvalidOperationException(
                    "Campaign slot does not satisfy the current runtime slot contract.");
        }

        public void DeleteSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                var entries = GetOrCreateEntries();
                entries[slotNumber - 1] = CampaignSlotEntry.Empty(slotNumber);
                LoadAllWithReport();
            }
        }

        public void ClearAll()
        {
            lock (Gate)
            {
                EntriesByNamespace.Remove(_diagnosticsKey);
                _hudReads.Reset();
                LoadAllWithReport();
            }

            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Transient campaign state cleared.");
        }

        private CampaignSlotEntry[] GetOrCreateEntries()
        {
            if (!EntriesByNamespace.TryGetValue(_diagnosticsKey, out var entries))
            {
                entries = CreateEmptyEntries();
                EntriesByNamespace.Add(_diagnosticsKey, entries);
            }

            return entries;
        }

        private CampaignSlotState GetOccupiedState(int slotNumber)
        {
            var entry = GetOrCreateEntries()[slotNumber - 1];
            if (entry.IsEmpty)
            {
                return null;
            }

            return entry.State;
        }

        private void StoreState(CampaignSlotState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var entries = GetOrCreateEntries();
            entries[state.SlotNumber - 1] = CampaignSlotEntry.Occupied(state);
            LoadAllWithReport();
        }

        private string Now()
        {
            return _utcNowProvider();
        }

        private static string DefaultUtcNow()
        {
            return DateTimeOffset.UtcNow.ToString("O");
        }

        private static CampaignSlotEntry[] CreateEmptyEntries()
        {
            var entries = new CampaignSlotEntry[CampaignSaveSlotPolicy.SlotCount];
            for (var index = 0; index < entries.Length; index++)
            {
                entries[index] = CampaignSlotEntry.Empty(index + 1);
            }

            return entries;
        }

    }

    /// <summary>
    /// Non-persistent active-slot storage for tests and short-lived diagnostic compositions.
    /// </summary>
    public sealed class TransientActiveSlotStorage : IActiveSlotStorage
    {
        public const string DefaultDiagnosticsKey = "transient-active-slot";

        private static readonly object Gate = new();
        private static readonly Dictionary<string, int> SlotsByNamespace = new();

        public TransientActiveSlotStorage(string diagnosticsKey = DefaultDiagnosticsKey)
        {
            DiagnosticsKey = string.IsNullOrWhiteSpace(diagnosticsKey)
                ? throw new ArgumentException("A transient active-slot namespace is required.", nameof(diagnosticsKey))
                : diagnosticsKey;
        }

        public string DiagnosticsKey { get; }

        public bool TryGetActiveSlot(out int slotNumber)
        {
            lock (Gate)
            {
                return SlotsByNamespace.TryGetValue(DiagnosticsKey, out slotNumber) &&
                       CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber);
            }
        }

        public void SetActiveSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                SlotsByNamespace[DiagnosticsKey] = slotNumber;
            }
        }

        public void ClearActiveSlot()
        {
            lock (Gate)
            {
                SlotsByNamespace.Remove(DiagnosticsKey);
            }
        }
    }
}
