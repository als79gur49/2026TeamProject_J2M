using System;
using System.Globalization;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum CampaignSaveLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        BackupRecovered = 3,
        CorruptRepairRequired = 4,
        SchemaInvalidRepairRequired = 5,
        IoFailed = 6,
        Unauthorized = 7,
        RecoveryPending = 8,
    }

    public readonly struct CampaignSaveLoadReport
    {
        public CampaignSaveLoadReport(
            CampaignSaveLoadStatus status,
            string reason,
            string matchedToken)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            MatchedToken = matchedToken ?? string.Empty;
        }

        public CampaignSaveLoadStatus Status { get; }

        public string Reason { get; }

        public string MatchedToken { get; }

        public bool RequiresRepair =>
            Status == CampaignSaveLoadStatus.CorruptRepairRequired ||
            Status == CampaignSaveLoadStatus.SchemaInvalidRepairRequired;

        public bool BlocksCampaignAccess =>
            Status == CampaignSaveLoadStatus.CorruptRepairRequired ||
            Status == CampaignSaveLoadStatus.SchemaInvalidRepairRequired ||
            Status == CampaignSaveLoadStatus.IoFailed ||
            Status == CampaignSaveLoadStatus.Unauthorized ||
            Status == CampaignSaveLoadStatus.RecoveryPending;

        public static CampaignSaveLoadReport Missing(string reason)
        {
            return new CampaignSaveLoadReport(CampaignSaveLoadStatus.Missing, reason, string.Empty);
        }

        public static CampaignSaveLoadReport Loaded(string reason, string matchedToken)
        {
            return new CampaignSaveLoadReport(CampaignSaveLoadStatus.Loaded, reason, matchedToken);
        }
    }

    public readonly struct CampaignSaveLoadResult
    {
        public CampaignSaveLoadResult(
            CampaignSlotEntry[] slots,
            CampaignSaveLoadReport report)
        {
            Slots = slots == null
                ? Array.Empty<CampaignSlotEntry>()
                : (CampaignSlotEntry[])slots.Clone();
            Report = report;
        }

        public CampaignSlotEntry[] Slots { get; }

        public CampaignSaveLoadReport Report { get; }
    }

    public sealed class CampaignStageClearCommitRequest
    {
        public CampaignStageClearTransitionPlan Plan { get; set; }

        public NormalCampaignCompletionReceipt CompletionReceipt { get; set; }

        public NormalStagePerformanceRecord PerformanceRecord { get; set; }
    }

    public sealed class CampaignDeathCommitResult
    {
        public CampaignDeathCommitResult(CampaignSlotState slot)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
        }

        public CampaignSlotState Slot { get; }
    }

    public sealed class CampaignStageClearCommitResult
    {
        public CampaignStageClearCommitResult(
            CampaignSlotState slot,
            int previousRemainingChances)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            PreviousRemainingChances = CampaignSaveSlotPolicy.RequireValidRemainingChances(
                previousRemainingChances);
        }

        public CampaignSlotState Slot { get; }

        public int PreviousRemainingChances { get; }
    }

    public interface ICampaignProgressionCommitter
    {
        CampaignDeathCommitResult CommitDeath(
            int slotNumber,
            CampaignDeathTransitionPlan plan);

        CampaignStageClearCommitResult CommitStageClear(
            int slotNumber,
            CampaignStageClearCommitRequest request);
    }

    public interface ICampaignSaveQuery
    {
        string DiagnosticsKey { get; }

        CampaignSaveLoadReport LastCampaignLoadReport { get; }

        CampaignSlotEntry[] LoadAll();

        CampaignSaveLoadResult LoadAllWithReport();

        CampaignSlotEntry LoadSlot(int slotNumber);
    }

    public sealed class CampaignContinuePreparationCommand
    {
        public CampaignContinuePreparationCommand(
            int slotNumber,
            StageId expectedStageId,
            string expectedPersistedLevelGroupId,
            string targetLevelGroupId)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!expectedStageId.IsValid)
            {
                throw new ArgumentException(
                    "Continue preparation requires a valid expected StageId.",
                    nameof(expectedStageId));
            }

            SlotNumber = slotNumber;
            ExpectedStageId = expectedStageId;
            ExpectedPersistedLevelGroupId = expectedPersistedLevelGroupId ??
                throw new ArgumentNullException(nameof(expectedPersistedLevelGroupId));
            TargetLevelGroupId = targetLevelGroupId ??
                throw new ArgumentNullException(nameof(targetLevelGroupId));
        }

        public int SlotNumber { get; }

        public StageId ExpectedStageId { get; }

        public string ExpectedPersistedLevelGroupId { get; }

        public string TargetLevelGroupId { get; }
    }

    public enum CampaignContinuePreparationStatus
    {
        Prepared = 0,
        SlotMissing = 1,
        StalePrecondition = 2,
    }

    public sealed class CampaignContinuePreparationResult
    {
        private CampaignContinuePreparationResult(
            CampaignContinuePreparationStatus status,
            CampaignSlotState committedState,
            bool levelGroupSynchronized)
        {
            Status = status;
            CommittedState = committedState;
            LevelGroupSynchronized = levelGroupSynchronized;
        }

        public CampaignContinuePreparationStatus Status { get; }

        public bool Succeeded => Status == CampaignContinuePreparationStatus.Prepared;

        public CampaignSlotState CommittedState { get; }

        public bool LevelGroupSynchronized { get; }

        public static CampaignContinuePreparationResult Prepared(
            CampaignSlotState committedState,
            bool levelGroupSynchronized)
        {
            return new CampaignContinuePreparationResult(
                CampaignContinuePreparationStatus.Prepared,
                committedState ?? throw new ArgumentNullException(nameof(committedState)),
                levelGroupSynchronized);
        }

        public static CampaignContinuePreparationResult SlotMissing()
        {
            return new CampaignContinuePreparationResult(
                CampaignContinuePreparationStatus.SlotMissing,
                null,
                levelGroupSynchronized: false);
        }

        public static CampaignContinuePreparationResult StalePrecondition()
        {
            return new CampaignContinuePreparationResult(
                CampaignContinuePreparationStatus.StalePrecondition,
                null,
                levelGroupSynchronized: false);
        }
    }

    public interface ICampaignContinuePreparationPort
    {
        CampaignContinuePreparationResult PrepareContinue(
            CampaignContinuePreparationCommand command);
    }

    internal static class CampaignContinuePreparationPolicy
    {
        internal static CampaignContinuePreparationResult Evaluate(
            CampaignSlotState current,
            CampaignContinuePreparationCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (current == null)
            {
                return CampaignContinuePreparationResult.SlotMissing();
            }

            if (current.CampaignCompleted ||
                current.SlotNumber != command.SlotNumber ||
                !current.CurrentStageId.Equals(command.ExpectedStageId) ||
                !string.Equals(
                    current.CurrentLevelGroupId,
                    command.ExpectedPersistedLevelGroupId,
                    StringComparison.Ordinal))
            {
                return CampaignContinuePreparationResult.StalePrecondition();
            }

            var synchronized = !string.Equals(
                current.CurrentLevelGroupId,
                command.TargetLevelGroupId,
                StringComparison.Ordinal);
            return CampaignContinuePreparationResult.Prepared(
                synchronized
                    ? CampaignSlotStateFactory.WithCurrentLevelGroup(
                        current,
                        command.TargetLevelGroupId)
                    : current,
                synchronized);
        }
    }

    public interface ICampaignSlotLifecyclePort
    {
        CampaignSlotState InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt);

        void DeleteSlot(int slotNumber);

        void ClearAll();
    }

    public interface ICampaignComicProgressPort
    {
        void MarkIntroComicCompleted(int slotNumber);

        void MarkOutroComicCompleted(int slotNumber);
    }

    public interface ICampaignDiagnosticSlotPort
    {
        CampaignSlotState SetActiveStageForDiagnostics(
            int slotNumber,
            StageId stageId,
            string levelGroupId);
    }

    public sealed class CampaignSlotSeedImportRequest
    {
        public CampaignSlotSeedImportRequest(
            int slotNumber,
            StageId stageId,
            string levelGroupId,
            int remainingChances,
            string lastPlayedAt)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!stageId.IsValid)
            {
                throw new ArgumentException(
                    "Campaign slot seed import requires a valid StageId.",
                    nameof(stageId));
            }

            SlotNumber = slotNumber;
            StageId = stageId;
            LevelGroupId = levelGroupId ?? string.Empty;
            RemainingChances = CampaignSaveSlotPolicy.RequireValidRemainingChances(
                remainingChances);
            LastPlayedAt = lastPlayedAt ?? string.Empty;
        }

        public int SlotNumber { get; }

        public StageId StageId { get; }

        public string LevelGroupId { get; }

        public int RemainingChances { get; }

        public string LastPlayedAt { get; }
    }

    public interface ICampaignSlotSeedImportPort
    {
        CampaignSlotState ImportSlotSeed(CampaignSlotSeedImportRequest request);
    }

    /// <summary>
    /// Composition-root aggregate. Runtime consumers should request the narrowest port above.
    /// </summary>
    public interface ICampaignSaveRuntime :
        ICampaignSaveQuery,
        ICampaignContinuePreparationPort,
        ICampaignSlotLifecyclePort,
        ICampaignComicProgressPort,
        ICampaignDiagnosticSlotPort,
        ICampaignSlotSeedImportPort,
        ICampaignProgressionCommitter
    {
    }

    [Flags]
    public enum CampaignSaveRecoveryActions
    {
        None = 0,
        Retry = 1 << 0,
        ResetProfile = 1 << 1,
    }

    public static class CampaignSaveRecoveryPolicy
    {
        public static CampaignSaveRecoveryActions GetActions(CampaignSaveLoadStatus status)
        {
            switch (status)
            {
                case CampaignSaveLoadStatus.SchemaInvalidRepairRequired:
                case CampaignSaveLoadStatus.CorruptRepairRequired:
                    return CampaignSaveRecoveryActions.Retry |
                           CampaignSaveRecoveryActions.ResetProfile;
                case CampaignSaveLoadStatus.Unauthorized:
                case CampaignSaveLoadStatus.IoFailed:
                case CampaignSaveLoadStatus.RecoveryPending:
                    return CampaignSaveRecoveryActions.Retry;
                default:
                    return CampaignSaveRecoveryActions.None;
            }
        }
    }

    public enum CampaignSaveResetResult
    {
        Completed = 0,
        StateChanged = 1,
        NotAllowed = 2,
        Failed = 3,
    }

    public interface ICampaignSaveRecoveryPort
    {
        bool HasPendingReset { get; }

        CampaignSaveResetResult ResetBlockedProfile(CampaignSaveLoadStatus expectedStatus);

        CampaignSaveResetResult RetryPendingReset();
    }

    [Serializable]
    internal sealed class CampaignSavePendingResetDocument
    {
        public string ResetId;
        public string StartedAtUtc;
    }

    public sealed class CampaignSaveRecoveryService : ICampaignSaveRecoveryPort
    {
        public const string PendingResetFileName = "profile.reset.pending.json";

        private const string ResetIdFormat = "yyyyMMddHHmmssfffffff";
        private const string UtcTimestampFormat = "o";

        private readonly ICampaignProfileRepository _repository;
        private readonly IAtomicTextFileStore _textFileStore;
        private readonly Func<DateTime> _utcNow;
        private readonly string _profileId;
        private readonly string _productVersion;

        public CampaignSaveRecoveryService(
            ICampaignProfileRepository repository,
            IAtomicTextFileStore textFileStore,
            Func<DateTime> utcNow,
            string profileId,
            string productVersion)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _textFileStore = textFileStore ?? throw new ArgumentNullException(nameof(textFileStore));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _profileId = string.IsNullOrWhiteSpace(profileId) ? "campaign-profile" : profileId;
            _productVersion = productVersion ?? string.Empty;
        }

        public CampaignSaveResetResult ResetBlockedProfile(CampaignSaveLoadStatus expectedStatus)
        {
            if ((CampaignSaveRecoveryPolicy.GetActions(expectedStatus) &
                 CampaignSaveRecoveryActions.ResetProfile) == 0)
            {
                return CampaignSaveResetResult.NotAllowed;
            }

            CampaignProfileLoadResult loadResult;
            try
            {
                loadResult = _repository.Load();
            }
            catch
            {
                return CampaignSaveResetResult.Failed;
            }

            if (MapStatus(loadResult.Status) != expectedStatus)
            {
                return CampaignSaveResetResult.StateChanged;
            }

            var now = _utcNow().ToUniversalTime();
            var pending = new CampaignSavePendingResetDocument
            {
                ResetId = now.ToString(ResetIdFormat, CultureInfo.InvariantCulture),
                StartedAtUtc = now.ToString(UtcTimestampFormat, CultureInfo.InvariantCulture),
            };

            try
            {
                _textFileStore.WriteAllTextAtomic(PendingResetFileName, JsonUtility.ToJson(pending));
            }
            catch
            {
                return CampaignSaveResetResult.Failed;
            }

            return CompletePendingReset(pending);
        }

        public bool HasPendingReset
        {
            get
            {
                try
                {
                    return _textFileStore.Exists(PendingResetFileName);
                }
                catch
                {
                    return true;
                }
            }
        }

        public CampaignSaveResetResult RetryPendingReset()
        {
            return ResumePendingReset();
        }

        public CampaignSaveResetResult ResumePendingReset()
        {
            if (!HasPendingReset)
            {
                return CampaignSaveResetResult.NotAllowed;
            }

            CampaignSavePendingResetDocument pending;
            string rawPending;
            try
            {
                rawPending = _textFileStore.ReadAllText(PendingResetFileName);
            }
            catch
            {
                return CampaignSaveResetResult.Failed;
            }

            try
            {
                pending = JsonUtility.FromJson<CampaignSavePendingResetDocument>(rawPending);
            }
            catch (ArgumentException)
            {
                pending = null;
            }

            if (!IsValidPendingReset(pending))
            {
                return RecoverUnreadablePendingReset();
            }

            try
            {
                var current = _repository.Load();
                if (current.Status == CampaignProfileLoadStatus.Loaded ||
                    current.Status == CampaignProfileLoadStatus.BackupRecovered)
                {
                    if (IsCompletedResetProfile(current.Document, pending.StartedAtUtc))
                    {
                        return DeletePendingResetArtifacts()
                            ? CampaignSaveResetResult.Completed
                            : CampaignSaveResetResult.Failed;
                    }

                    return DeletePendingResetArtifacts()
                        ? CampaignSaveResetResult.StateChanged
                        : CampaignSaveResetResult.Failed;
                }

                if (current.Status == CampaignProfileLoadStatus.Unauthorized ||
                    current.Status == CampaignProfileLoadStatus.IoFailed)
                {
                    return CampaignSaveResetResult.Failed;
                }
            }
            catch
            {
                return CampaignSaveResetResult.Failed;
            }

            return CompletePendingReset(pending);
        }

        private CampaignSaveResetResult RecoverUnreadablePendingReset()
        {
            CampaignProfileLoadResult current;
            try
            {
                current = _repository.Load();
            }
            catch
            {
                return CampaignSaveResetResult.Failed;
            }

            if (current.Status == CampaignProfileLoadStatus.Loaded ||
                current.Status == CampaignProfileLoadStatus.BackupRecovered)
            {
                return DeletePendingResetArtifacts()
                    ? CampaignSaveResetResult.StateChanged
                    : CampaignSaveResetResult.Failed;
            }

            if (current.Status == CampaignProfileLoadStatus.Unauthorized ||
                current.Status == CampaignProfileLoadStatus.IoFailed)
            {
                return CampaignSaveResetResult.Failed;
            }

            var now = _utcNow().ToUniversalTime();
            var replacement = new CampaignSavePendingResetDocument
            {
                ResetId = now.ToString(ResetIdFormat, CultureInfo.InvariantCulture),
                StartedAtUtc = now.ToString(UtcTimestampFormat, CultureInfo.InvariantCulture),
            };

            try
            {
                _textFileStore.WriteAllTextAtomic(PendingResetFileName, JsonUtility.ToJson(replacement));
            }
            catch
            {
                return CampaignSaveResetResult.Failed;
            }

            return CompletePendingReset(replacement);
        }

        private static bool IsValidPendingReset(CampaignSavePendingResetDocument pending)
        {
            if (pending == null ||
                string.IsNullOrWhiteSpace(pending.ResetId) ||
                string.IsNullOrWhiteSpace(pending.StartedAtUtc) ||
                !DateTime.TryParseExact(
                    pending.StartedAtUtc,
                    UtcTimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var startedAtUtc) ||
                startedAtUtc.Kind != DateTimeKind.Utc)
            {
                return false;
            }

            return string.Equals(
                pending.ResetId,
                startedAtUtc.ToString(ResetIdFormat, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        private CampaignSaveResetResult CompletePendingReset(CampaignSavePendingResetDocument pending)
        {
            try
            {
                var suffix = $"rejected.{pending.ResetId}";
                if (!TryNormalizeAndQuarantineIfPresent(
                        FileCampaignProfileRepository.ProfileFileName + ".bak",
                        suffix) ||
                    !TryNormalizeAndQuarantineIfPresent(
                        FileCampaignProfileRepository.ProfileFileName,
                        suffix))
                {
                    return CampaignSaveResetResult.Failed;
                }

                _repository.Save(CreateEmptyProfile(pending.StartedAtUtc));
                return DeletePendingResetArtifacts()
                    ? CampaignSaveResetResult.Completed
                    : CampaignSaveResetResult.Failed;
            }
            catch
            {
                return CampaignSaveResetResult.Failed;
            }
        }

        private bool TryNormalizeAndQuarantineIfPresent(string fileName, string suffix)
        {
            _textFileStore.CleanupTempFiles(fileName);
            return TryQuarantineIfPresent(fileName, suffix);
        }

        private bool TryQuarantineIfPresent(string fileName, string suffix)
        {
            return !_textFileStore.Exists(fileName) ||
                   _textFileStore.TryQuarantine(fileName, suffix, out _);
        }

        private bool DeletePendingResetArtifacts()
        {
            var deletedCanonical = _textFileStore.Delete(PendingResetFileName);
            _textFileStore.Delete(PendingResetFileName + ".bak");
            return deletedCanonical;
        }

        private CampaignProfileDocument CreateEmptyProfile(string resetAtUtc)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = _productVersion,
                SavedAtUtc = resetAtUtc,
                ProfileId = _profileId,
                LastPlayedSlotNumber = 0,
                Slots = Array.Empty<CampaignSlotDocument>(),
            };
        }

        private bool IsCompletedResetProfile(
            CampaignProfileDocument document,
            string resetAtUtc)
        {
            return document != null &&
                   document.SchemaVersion == CampaignProfileDocument.CurrentSchemaVersion &&
                   string.Equals(document.ProfileId, _profileId, StringComparison.Ordinal) &&
                   string.Equals(document.SavedAtUtc, resetAtUtc, StringComparison.Ordinal) &&
                   document.LastPlayedSlotNumber == 0 &&
                   (document.Slots == null || document.Slots.Length == 0);
        }

        private static CampaignSaveLoadStatus MapStatus(CampaignProfileLoadStatus status)
        {
            switch (status)
            {
                case CampaignProfileLoadStatus.Missing:
                    return CampaignSaveLoadStatus.Missing;
                case CampaignProfileLoadStatus.Loaded:
                    return CampaignSaveLoadStatus.Loaded;
                case CampaignProfileLoadStatus.BackupRecovered:
                    return CampaignSaveLoadStatus.BackupRecovered;
                case CampaignProfileLoadStatus.UnsupportedVersion:
                    return CampaignSaveLoadStatus.SchemaInvalidRepairRequired;
                case CampaignProfileLoadStatus.InvalidDocument:
                case CampaignProfileLoadStatus.CorruptNoFallback:
                    return CampaignSaveLoadStatus.CorruptRepairRequired;
                case CampaignProfileLoadStatus.Unauthorized:
                    return CampaignSaveLoadStatus.Unauthorized;
                default:
                    return CampaignSaveLoadStatus.IoFailed;
            }
        }
    }
}
