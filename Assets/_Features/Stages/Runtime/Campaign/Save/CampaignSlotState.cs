using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Feature.Stages
{
    public enum CampaignReceiptPresence
    {
        Absent = 0,
        PresentWithoutPayload = 1,
        PresentWithPayload = 2,
    }

    public sealed class CampaignCompletionReceiptState
    {
        internal CampaignCompletionReceiptState(
            int version,
            StageId completedStageId,
            string stageRunId,
            int clearSource)
        {
            if (!completedStageId.IsValid)
            {
                throw new ArgumentException(
                    "Campaign completion receipt state requires a valid completed StageId.",
                    nameof(completedStageId));
            }

            var candidate = new NormalCampaignCompletionReceipt
            {
                Version = version,
                CompletedStageId = completedStageId.Value,
                StageRunId = stageRunId ?? string.Empty,
                ClearSource = clearSource,
            };
            if (!candidate.IsStructurallyValid)
            {
                throw new ArgumentException(
                    "Campaign completion receipt state must be structurally valid.",
                    nameof(version));
            }

            Version = candidate.Version;
            CompletedStageId = completedStageId;
            StageRunId = candidate.StageRunId;
            ClearSource = candidate.ClearSource;
        }

        public int Version { get; }

        public StageId CompletedStageId { get; }

        public string StageRunId { get; }

        public int ClearSource { get; }
    }

    public sealed class CampaignReceiptState
    {
        private CampaignReceiptState(
            CampaignReceiptPresence presence,
            CampaignCompletionReceiptState payload)
        {
            if (presence == CampaignReceiptPresence.PresentWithPayload && payload == null)
            {
                throw new ArgumentException(
                    "A present-with-payload receipt state requires a payload.",
                    nameof(payload));
            }

            if (presence != CampaignReceiptPresence.PresentWithPayload && payload != null)
            {
                throw new ArgumentException(
                    "Only a present-with-payload receipt state may carry a payload.",
                    nameof(payload));
            }

            Presence = presence;
            Payload = payload;
        }

        public CampaignReceiptPresence Presence { get; }

        public CampaignCompletionReceiptState Payload { get; }

        internal static CampaignReceiptState Absent()
        {
            return new CampaignReceiptState(CampaignReceiptPresence.Absent, null);
        }

        internal static CampaignReceiptState PresentWithoutPayload()
        {
            return new CampaignReceiptState(
                CampaignReceiptPresence.PresentWithoutPayload,
                null);
        }

        internal static CampaignReceiptState PresentWithPayload(
            CampaignCompletionReceiptState payload)
        {
            return new CampaignReceiptState(
                CampaignReceiptPresence.PresentWithPayload,
                payload);
        }
    }

    public sealed class CampaignStagePerformanceState
    {
        internal CampaignStagePerformanceState(
            int version,
            StageId stageId,
            int bestCombinedPushFlipUses)
        {
            if (version != NormalStagePerformanceRecord.CurrentVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (!stageId.IsValid)
            {
                throw new ArgumentException(
                    "Campaign performance state requires a valid StageId.",
                    nameof(stageId));
            }

            if (bestCombinedPushFlipUses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bestCombinedPushFlipUses));
            }

            Version = version;
            StageId = stageId;
            BestCombinedPushFlipUses = bestCombinedPushFlipUses;
        }

        public int Version { get; }

        public StageId StageId { get; }

        public int BestCombinedPushFlipUses { get; }
    }

    public sealed class CampaignStageClearRecordState
    {
        private readonly ReadOnlyCollection<string> _processedStageRunIds;

        internal CampaignStageClearRecordState(
            StageId stageId,
            bool hasAttempted,
            bool hasCleared,
            int clearCount,
            IEnumerable<string> processedStageRunIds)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException(
                    "Campaign stage-clear record state requires a valid StageId.",
                    nameof(stageId));
            }

            if (clearCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clearCount));
            }

            StageId = stageId;
            HasAttempted = hasAttempted;
            HasCleared = hasCleared;
            ClearCount = clearCount;
            _processedStageRunIds = CampaignSlotStateCollections.CopyCanonicalIds(
                processedStageRunIds,
                nameof(processedStageRunIds));
        }

        public StageId StageId { get; }

        public bool HasAttempted { get; }

        public bool HasCleared { get; }

        public int ClearCount { get; }

        public IReadOnlyList<string> ProcessedStageRunIds => _processedStageRunIds;
    }

    public sealed class CampaignStageClearProfileState
    {
        private readonly ReadOnlyCollection<CampaignStageClearRecordState> _records;
        private readonly ReadOnlyCollection<string> _processedStageRunIds;
        private readonly ReadOnlyCollection<string> _processedClearAttemptIds;

        internal CampaignStageClearProfileState(
            int version,
            IEnumerable<CampaignStageClearRecordState> records,
            IEnumerable<string> processedStageRunIds,
            IEnumerable<string> processedClearAttemptIds)
        {
            if (version < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            var recordList = new List<CampaignStageClearRecordState>();
            var stageIds = new HashSet<StageId>();
            if (records != null)
            {
                foreach (var record in records)
                {
                    if (record == null || !stageIds.Add(record.StageId))
                    {
                        throw new ArgumentException(
                            "Campaign stage-clear profile records must be non-null and unique by stage.",
                            nameof(records));
                    }

                    recordList.Add(record);
                }
            }

            recordList.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.StageId.Value,
                right.StageId.Value));

            Version = version;
            _records = new ReadOnlyCollection<CampaignStageClearRecordState>(recordList);
            _processedStageRunIds = CampaignSlotStateCollections.CopyCanonicalIds(
                processedStageRunIds,
                nameof(processedStageRunIds));
            _processedClearAttemptIds = CampaignSlotStateCollections.CopyCanonicalIds(
                processedClearAttemptIds,
                nameof(processedClearAttemptIds));
        }

        public int Version { get; }

        public IReadOnlyList<CampaignStageClearRecordState> Records => _records;

        public IReadOnlyList<string> ProcessedStageRunIds => _processedStageRunIds;

        public IReadOnlyList<string> ProcessedClearAttemptIds => _processedClearAttemptIds;
    }

    public sealed class CampaignSlotState
    {
        private readonly ReadOnlyCollection<CampaignStagePerformanceState>
            _normalStagePerformanceRecords;

        internal CampaignSlotState(
            int slotNumber,
            StageId currentStageId,
            string currentLevelGroupId,
            int remainingChances,
            bool campaignCompleted,
            CampaignReceiptState receipt,
            bool introComicCompleted,
            bool outroComicCompleted,
            IEnumerable<CampaignStagePerformanceState> normalStagePerformanceRecords,
            int totalDeaths,
            string lastPlayedAt,
            CampaignStageClearProfileState stageClearProfile,
            GameMode gameMode = GameMode.Hardcore,
            int resumeHp = 0)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!currentStageId.IsValid)
            {
                throw new ArgumentException(
                    "Campaign slot state requires a valid current StageId.",
                    nameof(currentStageId));
            }

            CampaignSaveSlotPolicy.RequireValidSurvival(gameMode, resumeHp, remainingChances);
            if (totalDeaths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalDeaths));
            }

            var performanceRecords = new List<CampaignStagePerformanceState>();
            var performanceStageIds = new HashSet<StageId>();
            if (normalStagePerformanceRecords != null)
            {
                foreach (var record in normalStagePerformanceRecords)
                {
                    if (record == null || !performanceStageIds.Add(record.StageId))
                    {
                        throw new ArgumentException(
                            "Campaign performance state must be non-null and unique by stage.",
                            nameof(normalStagePerformanceRecords));
                    }

                    performanceRecords.Add(record);
                }
            }

            performanceRecords.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.StageId.Value,
                right.StageId.Value));

            SlotNumber = slotNumber;
            CurrentStageId = currentStageId;
            CurrentLevelGroupId = currentLevelGroupId ?? string.Empty;
            GameMode = gameMode;
            ResumeHp = resumeHp;
            RemainingChances = remainingChances;
            CampaignCompleted = campaignCompleted;
            Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
            IntroComicCompleted = introComicCompleted;
            OutroComicCompleted = outroComicCompleted;
            _normalStagePerformanceRecords =
                new ReadOnlyCollection<CampaignStagePerformanceState>(performanceRecords);
            TotalDeaths = totalDeaths;
            LastPlayedAt = lastPlayedAt ?? string.Empty;
            StageClearProfile = stageClearProfile ??
                                throw new ArgumentNullException(nameof(stageClearProfile));
        }

        public int SlotNumber { get; }

        public StageId CurrentStageId { get; }

        public string CurrentLevelGroupId { get; }

        public GameMode GameMode { get; }

        public int ResumeHp { get; }

        public int RemainingChances { get; }

        public bool CampaignCompleted { get; }

        public CampaignReceiptState Receipt { get; }

        public bool IntroComicCompleted { get; }

        public bool OutroComicCompleted { get; }

        public IReadOnlyList<CampaignStagePerformanceState>
            NormalStagePerformanceRecords => _normalStagePerformanceRecords;

        public int TotalDeaths { get; }

        public string LastPlayedAt { get; }

        public CampaignStageClearProfileState StageClearProfile { get; }
    }

    public sealed class CampaignSlotEntry
    {
        private CampaignSlotEntry(int slotNumber, CampaignSlotState state)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (state != null && state.SlotNumber != slotNumber)
            {
                throw new ArgumentException(
                    "An occupied campaign slot entry must match its state slot number.",
                    nameof(state));
            }

            SlotNumber = slotNumber;
            State = state;
        }

        public int SlotNumber { get; }

        public bool IsEmpty => State == null;

        public CampaignSlotState State { get; }

        // Occupied-entry projections keep query consumers on immutable campaign state
        // without reviving the mutable SaveSlotData compatibility surface.
        public StageId CurrentStageId => RequireState().CurrentStageId;

        public string CurrentLevelGroupId => RequireState().CurrentLevelGroupId;

        public GameMode GameMode => RequireState().GameMode;

        public int ResumeHp => RequireState().ResumeHp;

        public int RemainingChances => RequireState().RemainingChances;

        public bool CampaignCompleted => RequireState().CampaignCompleted;

        public CampaignReceiptState Receipt => RequireState().Receipt;

        public bool HasNormalCampaignCompletionReceipt =>
            RequireState().Receipt.Presence != CampaignReceiptPresence.Absent;

        public bool IntroComicCompleted => RequireState().IntroComicCompleted;

        public bool OutroComicCompleted => RequireState().OutroComicCompleted;

        public IReadOnlyList<CampaignStagePerformanceState> NormalStagePerformanceRecords =>
            RequireState().NormalStagePerformanceRecords;

        public int TotalDeaths => RequireState().TotalDeaths;

        public string LastPlayedAt => RequireState().LastPlayedAt;

        public CampaignStageClearProfileState StageClearProfile =>
            RequireState().StageClearProfile;

        private CampaignSlotState RequireState()
        {
            if (State == null)
            {
                throw new InvalidOperationException(
                    $"Campaign slot {SlotNumber} is empty and has no state.");
            }

            return State;
        }

        internal static CampaignSlotEntry Empty(int slotNumber)
        {
            return new CampaignSlotEntry(slotNumber, null);
        }

        internal static CampaignSlotEntry Occupied(CampaignSlotState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new CampaignSlotEntry(state.SlotNumber, state);
        }
    }

    internal enum CampaignSlotParseFailureKind
    {
        InvalidDocument = 1,
        SlotNumberMismatch = 2,
    }

    internal sealed class CampaignSlotDiagnostic
    {
        private readonly CampaignSlotDocument _rawDocument;

        internal CampaignSlotDiagnostic(
            CampaignSlotParseFailureKind failureKind,
            int expectedSlotNumber,
            CampaignSlotDocument rawDocument)
        {
            FailureKind = failureKind;
            ExpectedSlotNumber = expectedSlotNumber;
            _rawDocument = CampaignSlotRawDocumentCloner.Clone(rawDocument);
        }

        internal CampaignSlotParseFailureKind FailureKind { get; }

        internal int ExpectedSlotNumber { get; }

        internal CampaignSlotDocument CopyRawDocument()
        {
            return CampaignSlotRawDocumentCloner.Clone(_rawDocument);
        }
    }

    internal sealed class CampaignSlotParseResult
    {
        private CampaignSlotParseResult(
            CampaignSlotEntry entry,
            CampaignSlotDiagnostic diagnostic)
        {
            Entry = entry;
            Diagnostic = diagnostic;
        }

        internal bool IsSuccess => Entry != null;

        internal CampaignSlotEntry Entry { get; }

        internal CampaignSlotDiagnostic Diagnostic { get; }

        internal static CampaignSlotParseResult Success(CampaignSlotEntry entry)
        {
            return new CampaignSlotParseResult(
                entry ?? throw new ArgumentNullException(nameof(entry)),
                null);
        }

        internal static CampaignSlotParseResult Failure(CampaignSlotDiagnostic diagnostic)
        {
            return new CampaignSlotParseResult(
                null,
                diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
        }
    }

    internal static class CampaignSlotParser
    {
        internal static CampaignSlotParseResult ParseEntry(
            int slotNumber,
            CampaignSlotDocument document)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (document == null)
            {
                return CampaignSlotParseResult.Success(CampaignSlotEntry.Empty(slotNumber));
            }

            if (document.SlotNumber != slotNumber)
            {
                return CampaignSlotParseResult.Failure(new CampaignSlotDiagnostic(
                    CampaignSlotParseFailureKind.SlotNumberMismatch,
                    slotNumber,
                    document));
            }

            if (!CampaignSlotDocumentValidator.IsValid(document))
            {
                return CampaignSlotParseResult.Failure(new CampaignSlotDiagnostic(
                    CampaignSlotParseFailureKind.InvalidDocument,
                    slotNumber,
                    document));
            }

            return CampaignSlotParseResult.Success(CampaignSlotEntry.Occupied(
                CampaignSlotStateFactory.FromValidatedDocument(document)));
        }
    }

    public static class CampaignSlotStateFactory
    {
        public static CampaignSlotEntry CreateEmptyEntry(int slotNumber)
        {
            return CampaignSlotEntry.Empty(slotNumber);
        }

        public static CampaignSlotState CreateNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt,
            GameMode gameMode = GameMode.Hardcore)
        {
            if (sequenceResolver == null)
            {
                throw new ArgumentNullException(nameof(sequenceResolver));
            }

            var firstStageId = sequenceResolver.FirstStageId;
            return CreateNewGame(
                slotNumber,
                firstStageId,
                sequenceResolver.GetLevelGroupId(firstStageId),
                lastPlayedAt, gameMode);
        }

        internal static CampaignSlotState CreateNewGame(
            int slotNumber,
            StageId firstStageId,
            string firstLevelGroupId,
            string lastPlayedAt,
            GameMode gameMode = GameMode.Hardcore)
        {
            return new CampaignSlotState(
                slotNumber,
                firstStageId,
                firstLevelGroupId,
                gameMode == GameMode.Casual ? 0 : CampaignSaveSlotPolicy.DefaultRemainingChances,
                campaignCompleted: false,
                CampaignReceiptState.Absent(),
                introComicCompleted: false,
                outroComicCompleted: false,
                Array.Empty<CampaignStagePerformanceState>(),
                totalDeaths: 0,
                lastPlayedAt,
                CreateEmptyStageClearProfile(), gameMode,
                gameMode == GameMode.Casual ? CampaignSaveSlotPolicy.CasualMaxHp : 0);
        }

        internal static CampaignSlotState CreateImportedSeed(
            CampaignSlotSeedImportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new CampaignSlotState(
                request.SlotNumber,
                request.StageId,
                request.LevelGroupId,
                request.RemainingChances,
                campaignCompleted: false,
                CampaignReceiptState.Absent(),
                introComicCompleted: false,
                outroComicCompleted: false,
                Array.Empty<CampaignStagePerformanceState>(),
                totalDeaths: 0,
                request.LastPlayedAt,
                CreateEmptyStageClearProfile(), request.GameMode, request.ResumeHp);
        }

        internal static CampaignSlotState WithLastPlayedAt(
            CampaignSlotState current,
            string lastPlayedAt)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            return new CampaignSlotState(
                current.SlotNumber,
                current.CurrentStageId,
                current.CurrentLevelGroupId,
                current.RemainingChances,
                current.CampaignCompleted,
                current.Receipt,
                current.IntroComicCompleted,
                current.OutroComicCompleted,
                current.NormalStagePerformanceRecords,
                current.TotalDeaths,
                lastPlayedAt,
                current.StageClearProfile, current.GameMode, current.ResumeHp);
        }

        internal static CampaignSlotState WithCurrentLevelGroup(
            CampaignSlotState current,
            string currentLevelGroupId)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            return new CampaignSlotState(
                current.SlotNumber,
                current.CurrentStageId,
                currentLevelGroupId,
                current.RemainingChances,
                current.CampaignCompleted,
                current.Receipt,
                current.IntroComicCompleted,
                current.OutroComicCompleted,
                current.NormalStagePerformanceRecords,
                current.TotalDeaths,
                current.LastPlayedAt,
                current.StageClearProfile, current.GameMode, current.ResumeHp);
        }

        internal static CampaignSlotState SelectStageForDiagnostics(
            CampaignSlotState current,
            StageId stageId,
            string levelGroupId,
            string committedAtUtc)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (!stageId.IsValid)
            {
                throw new ArgumentException(
                    "Diagnostic stage selection requires a valid StageId.",
                    nameof(stageId));
            }

            var records = new List<CampaignStageClearRecordState>(
                current.StageClearProfile.Records.Count + 1);
            var containsStage = false;
            for (var index = 0; index < current.StageClearProfile.Records.Count; index++)
            {
                var record = current.StageClearProfile.Records[index];
                records.Add(record);
                containsStage |= record.StageId.Equals(stageId);
            }

            if (!containsStage)
            {
                records.Add(new CampaignStageClearRecordState(
                    stageId,
                    hasAttempted: false,
                    hasCleared: false,
                    clearCount: 0,
                    Array.Empty<string>()));
            }

            var profile = new CampaignStageClearProfileState(
                current.StageClearProfile.Version,
                records,
                current.StageClearProfile.ProcessedStageRunIds,
                current.StageClearProfile.ProcessedClearAttemptIds);
            return new CampaignSlotState(
                current.SlotNumber,
                stageId,
                levelGroupId,
                current.RemainingChances,
                campaignCompleted: false,
                current.Receipt,
                current.IntroComicCompleted,
                current.OutroComicCompleted,
                current.NormalStagePerformanceRecords,
                current.TotalDeaths,
                committedAtUtc,
                profile, current.GameMode, current.ResumeHp);
        }

        internal static CampaignSlotState FromValidatedDocument(
            CampaignSlotDocument document)
        {
            if (!CampaignSlotDocumentValidator.IsValid(document))
            {
                throw new ArgumentException(
                    "Campaign slot state can only be created from a valid document.",
                    nameof(document));
            }

            var performanceDocuments = document.NormalStagePerformanceRecords ??
                                       Array.Empty<NormalStagePerformanceRecordDocument>();
            var performanceRecords = new List<CampaignStagePerformanceState>(
                performanceDocuments.Length);
            for (var index = 0; index < performanceDocuments.Length; index++)
            {
                var record = performanceDocuments[index];
                performanceRecords.Add(new CampaignStagePerformanceState(
                    record.Version,
                    StageId.CreateOrThrow(record.StageId),
                    record.BestCombinedPushFlipUses));
            }

            return new CampaignSlotState(
                document.SlotNumber,
                StageId.CreateOrThrow(document.StageId),
                document.LevelGroupId,
                document.RemainingChances,
                document.CampaignCompleted,
                CreateReceipt(document),
                document.IntroComicCompleted,
                document.OutroComicCompleted,
                performanceRecords,
                document.TotalDeaths,
                document.LastPlayedAtUtc,
                CreateStageClearProfile(document.StageClearProfileSnapshot),
                document.GameMode, document.ResumeHp);
        }

        private static CampaignReceiptState CreateReceipt(CampaignSlotDocument document)
        {
            if (!document.HasNormalCampaignCompletionReceipt)
            {
                return CampaignReceiptState.Absent();
            }

            var receipt = document.NormalCampaignCompletionReceipt;
            if (receipt == null ||
                CampaignSlotDocumentValidator.IsExactDefaultReceiptResidue(receipt))
            {
                return CampaignReceiptState.PresentWithoutPayload();
            }

            return CampaignReceiptState.PresentWithPayload(
                new CampaignCompletionReceiptState(
                    receipt.Version,
                    StageId.CreateOrThrow(receipt.CompletedStageId),
                    receipt.StageRunId,
                    receipt.ClearSource));
        }

        private static CampaignStageClearProfileState CreateStageClearProfile(
            CampaignStageClearProfileDocument document)
        {
            if (document == null)
            {
                return CreateEmptyStageClearProfile();
            }

            var recordDocuments = document.Records ??
                                  Array.Empty<PlayerStageClearRecordDocument>();
            var records = new List<CampaignStageClearRecordState>(recordDocuments.Length);
            for (var index = 0; index < recordDocuments.Length; index++)
            {
                var record = recordDocuments[index];
                records.Add(new CampaignStageClearRecordState(
                    StageId.CreateOrThrow(record.StageId),
                    record.HasAttempted,
                    record.HasCleared,
                    record.ClearCount,
                    record.ProcessedStageRunIds));
            }

            return new CampaignStageClearProfileState(
                document.Version,
                records,
                document.ProcessedStageRunIds,
                document.ProcessedClearAttemptIds);
        }

        private static CampaignStageClearProfileState CreateEmptyStageClearProfile()
        {
            return new CampaignStageClearProfileState(
                version: 0,
                Array.Empty<CampaignStageClearRecordState>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }

    internal static class CampaignSlotStateDocumentMapper
    {
        internal static CampaignSlotDocument ToDocument(CampaignSlotState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var document = new CampaignSlotDocument
            {
                SlotNumber = state.SlotNumber,
                StageId = state.CurrentStageId.Value,
                LevelGroupId = state.CurrentLevelGroupId,
                GameMode = state.GameMode,
                ResumeHp = state.ResumeHp,
                RemainingChances = state.RemainingChances,
                CampaignCompleted = state.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    state.Receipt.Presence != CampaignReceiptPresence.Absent,
                NormalCampaignCompletionReceipt = ToReceiptDocument(state.Receipt),
                IntroComicCompleted = state.IntroComicCompleted,
                OutroComicCompleted = state.OutroComicCompleted,
                NormalStagePerformanceRecords = ToPerformanceDocuments(
                    state.NormalStagePerformanceRecords),
                TotalDeaths = state.TotalDeaths,
                LastPlayedAtUtc = state.LastPlayedAt,
                StageClearProfileSnapshot = ToStageClearProfileDocument(
                    state.StageClearProfile),
            };
            return document;
        }

        private static NormalCampaignCompletionReceiptDocument ToReceiptDocument(
            CampaignReceiptState receipt)
        {
            if (receipt.Presence != CampaignReceiptPresence.PresentWithPayload)
            {
                return null;
            }

            return new NormalCampaignCompletionReceiptDocument
            {
                Version = receipt.Payload.Version,
                CompletedStageId = receipt.Payload.CompletedStageId.Value,
                StageRunId = receipt.Payload.StageRunId,
                ClearSource = receipt.Payload.ClearSource,
            };
        }

        private static NormalStagePerformanceRecordDocument[] ToPerformanceDocuments(
            IReadOnlyList<CampaignStagePerformanceState> records)
        {
            var documents = new NormalStagePerformanceRecordDocument[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                documents[index] = new NormalStagePerformanceRecordDocument
                {
                    Version = record.Version,
                    StageId = record.StageId.Value,
                    BestCombinedPushFlipUses = record.BestCombinedPushFlipUses,
                };
            }

            return documents;
        }

        private static CampaignStageClearProfileDocument ToStageClearProfileDocument(
            CampaignStageClearProfileState profile)
        {
            var records = new PlayerStageClearRecordDocument[profile.Records.Count];
            for (var index = 0; index < profile.Records.Count; index++)
            {
                var record = profile.Records[index];
                records[index] = new PlayerStageClearRecordDocument
                {
                    StageId = record.StageId.Value,
                    HasAttempted = record.HasAttempted,
                    HasCleared = record.HasCleared,
                    ClearCount = record.ClearCount,
                    ProcessedStageRunIds = CopyStrings(record.ProcessedStageRunIds),
                };
            }

            return new CampaignStageClearProfileDocument
            {
                Version = profile.Version,
                Records = records,
                ProcessedStageRunIds = CopyStrings(profile.ProcessedStageRunIds),
                ProcessedClearAttemptIds = CopyStrings(profile.ProcessedClearAttemptIds),
            };
        }

        private static string[] CopyStrings(IReadOnlyList<string> source)
        {
            var values = new string[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                values[index] = source[index];
            }

            return values;
        }
    }

    internal static class CampaignSlotRawDocumentCloner
    {
        internal static CampaignSlotDocument Clone(CampaignSlotDocument source)
        {
            if (source == null)
            {
                return null;
            }

            return new CampaignSlotDocument
            {
                SlotNumber = source.SlotNumber,
                StageId = source.StageId,
                LevelGroupId = source.LevelGroupId,
                GameMode = source.GameMode,
                ResumeHp = source.ResumeHp,
                RemainingChances = source.RemainingChances,
                CampaignCompleted = source.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    source.HasNormalCampaignCompletionReceipt,
                NormalCampaignCompletionReceipt = CloneReceipt(
                    source.NormalCampaignCompletionReceipt),
                IntroComicCompleted = source.IntroComicCompleted,
                OutroComicCompleted = source.OutroComicCompleted,
                NormalStagePerformanceRecords = ClonePerformanceRecords(
                    source.NormalStagePerformanceRecords),
                TotalDeaths = source.TotalDeaths,
                LastPlayedAtUtc = source.LastPlayedAtUtc,
                StageClearProfileSnapshot = CloneStageClearProfile(
                    source.StageClearProfileSnapshot),
            };
        }

        private static NormalCampaignCompletionReceiptDocument CloneReceipt(
            NormalCampaignCompletionReceiptDocument source)
        {
            if (source == null)
            {
                return null;
            }

            return new NormalCampaignCompletionReceiptDocument
            {
                Version = source.Version,
                CompletedStageId = source.CompletedStageId,
                StageRunId = source.StageRunId,
                ClearSource = source.ClearSource,
            };
        }

        private static NormalStagePerformanceRecordDocument[] ClonePerformanceRecords(
            NormalStagePerformanceRecordDocument[] source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new NormalStagePerformanceRecordDocument[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var record = source[index];
                clone[index] = record == null
                    ? null
                    : new NormalStagePerformanceRecordDocument
                    {
                        Version = record.Version,
                        StageId = record.StageId,
                        BestCombinedPushFlipUses = record.BestCombinedPushFlipUses,
                    };
            }

            return clone;
        }

        private static CampaignStageClearProfileDocument CloneStageClearProfile(
            CampaignStageClearProfileDocument source)
        {
            if (source == null)
            {
                return null;
            }

            return new CampaignStageClearProfileDocument
            {
                Version = source.Version,
                Records = CloneClearRecords(source.Records),
                ProcessedStageRunIds = CloneStrings(source.ProcessedStageRunIds),
                ProcessedClearAttemptIds = CloneStrings(source.ProcessedClearAttemptIds),
            };
        }

        private static PlayerStageClearRecordDocument[] CloneClearRecords(
            PlayerStageClearRecordDocument[] source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new PlayerStageClearRecordDocument[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var record = source[index];
                clone[index] = record == null
                    ? null
                    : new PlayerStageClearRecordDocument
                    {
                        StageId = record.StageId,
                        HasAttempted = record.HasAttempted,
                        HasCleared = record.HasCleared,
                        ClearCount = record.ClearCount,
                        ProcessedStageRunIds = CloneStrings(record.ProcessedStageRunIds),
                    };
            }

            return clone;
        }

        private static string[] CloneStrings(string[] source)
        {
            return source == null ? null : (string[])source.Clone();
        }
    }

    internal static class CampaignSlotStateCollections
    {
        internal static ReadOnlyCollection<string> CopyCanonicalIds(
            IEnumerable<string> source,
            string parameterName)
        {
            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (var value in source)
                {
                    if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                    {
                        throw new ArgumentException(
                            "Campaign processed IDs must be non-blank and unique.",
                            parameterName);
                    }

                    values.Add(value);
                }
            }

            values.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(values);
        }
    }
}
