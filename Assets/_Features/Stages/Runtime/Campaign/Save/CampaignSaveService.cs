using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Feature.Stages
{
    public enum CampaignSaveCommandStatus
    {
        Succeeded = 0,
        InvalidSlotNumber = 1,
        SlotNotFound = 2,
        InvalidRequest = 3,
        LoadFailed = 4,
        SaveFailed = 5,
    }

    public sealed class CampaignSaveServiceResult
    {
        public CampaignSaveServiceResult(
            CampaignSaveCommandStatus status,
            CampaignProfileDocument document,
            CampaignSlotDocument slot,
            CampaignSlotDocument[] slots,
            CampaignStageClearProfileDocument stageClearProfile,
            string message,
            bool hasProfileLoadStatus = false,
            CampaignProfileLoadStatus profileLoadStatus = CampaignProfileLoadStatus.Missing)
        {
            Status = status;
            Document = document;
            Slot = slot;
            Slots = slots ?? Array.Empty<CampaignSlotDocument>();
            StageClearProfile = stageClearProfile;
            Message = message ?? string.Empty;
            HasProfileLoadStatus = hasProfileLoadStatus;
            ProfileLoadStatus = profileLoadStatus;
        }

        public CampaignSaveCommandStatus Status { get; }

        public bool Succeeded => Status == CampaignSaveCommandStatus.Succeeded;

        public CampaignProfileDocument Document { get; }

        public CampaignSlotDocument Slot { get; }

        public CampaignSlotDocument[] Slots { get; }

        public CampaignStageClearProfileDocument StageClearProfile { get; }

        public string Message { get; }

        public bool HasProfileLoadStatus { get; }

        public CampaignProfileLoadStatus ProfileLoadStatus { get; }

        public static CampaignSaveServiceResult Success(
            CampaignProfileDocument document,
            CampaignSlotDocument slot = null,
            CampaignStageClearProfileDocument stageClearProfile = null,
            string message = "",
            bool hasProfileLoadStatus = false,
            CampaignProfileLoadStatus profileLoadStatus = CampaignProfileLoadStatus.Missing)
        {
            return new CampaignSaveServiceResult(
                CampaignSaveCommandStatus.Succeeded,
                document,
                slot,
                document?.Slots,
                stageClearProfile,
                message,
                hasProfileLoadStatus,
                profileLoadStatus);
        }

        public static CampaignSaveServiceResult Failure(
            CampaignSaveCommandStatus status,
            string message,
            CampaignProfileDocument document = null,
            bool hasProfileLoadStatus = false,
            CampaignProfileLoadStatus profileLoadStatus = CampaignProfileLoadStatus.Missing)
        {
            return new CampaignSaveServiceResult(
                status,
                document,
                null,
                document?.Slots,
                null,
                message,
                hasProfileLoadStatus,
                profileLoadStatus);
        }
    }

    public sealed class CampaignSlotUpdate
    {
        public string StageId { get; set; }

        public string LevelGroupId { get; set; }

        public int? RemainingChances { get; set; }

        public bool? CampaignCompleted { get; set; }

        public bool? IntroPlayed { get; set; }

        public bool? OutroPlayed { get; set; }

        public int? TotalDeaths { get; set; }

        public string LastPlayedAtUtc { get; set; }

        public CampaignStageClearProfileDocument StageClearProfileSnapshot { get; set; }

        public NormalCampaignCompletionReceiptDocument NormalCampaignCompletionReceipt { get; set; }
    }

    public sealed class CampaignNewGameRequest
    {
        public int SlotNumber { get; set; }

        public string InitialStageId { get; set; }

        public string InitialLevelGroupId { get; set; }

        public string LastPlayedAtUtc { get; set; }
    }

    public sealed class CampaignDeathSaveUpdate
    {
        public string StageId { get; set; }

        public string LevelGroupId { get; set; }

        public int? RemainingChances { get; set; }

        public int DeathsToAdd { get; set; } = 1;
    }

    public sealed class StageClearSaveUpdate
    {
        public string ClearedStageId { get; set; }

        public string NextStageId { get; set; }

        public string LevelGroupId { get; set; }

        public bool IsCampaignCompleted { get; set; }

        public string StageRunId { get; set; }

        public string StageCompletionAttemptId { get; set; }

        public int? RemainingChances { get; set; }

        public bool HasAttempted { get; set; } = true;

        public bool HasCleared { get; set; } = true;

        public int ClearCountIncrement { get; set; } = 1;
    }

    public sealed class CampaignCinematicFlagUpdate
    {
        public string StageId { get; set; }

        public bool Played { get; set; } = true;
    }

    public interface ICampaignSaveResetMarkerPort
    {
        void MarkResetImportDisabled(string resetTombstoneUtc);
    }

    public interface ICampaignDeletedSlotGuardMarkerPort
    {
        void RecordDeletedSlotGuard(
            int slotNumber,
            string importedSourceHash,
            string deletedAtUtc,
            string reason);
    }

    public sealed class CampaignLegacyImportResetMarkerPort : ICampaignSaveResetMarkerPort
    {
        private readonly CampaignLegacyImportMarkerStore _markerStore;

        public CampaignLegacyImportResetMarkerPort()
            : this(new CampaignLegacyImportMarkerStore())
        {
        }

        public CampaignLegacyImportResetMarkerPort(CampaignLegacyImportMarkerStore markerStore)
        {
            _markerStore = markerStore ?? throw new ArgumentNullException(nameof(markerStore));
        }

        public void MarkResetImportDisabled(string resetTombstoneUtc)
        {
            _markerStore.SetImportDisabled(true);
            _markerStore.SetResetTombstoneUtc(resetTombstoneUtc);
        }
    }

    public sealed class CampaignLegacyDeletedSlotGuardMarkerPort : ICampaignDeletedSlotGuardMarkerPort
    {
        private readonly CampaignLegacyImportMarkerStore _markerStore;

        public CampaignLegacyDeletedSlotGuardMarkerPort()
            : this(new CampaignLegacyImportMarkerStore())
        {
        }

        public CampaignLegacyDeletedSlotGuardMarkerPort(CampaignLegacyImportMarkerStore markerStore)
        {
            _markerStore = markerStore ?? throw new ArgumentNullException(nameof(markerStore));
        }

        public void RecordDeletedSlotGuard(
            int slotNumber,
            string importedSourceHash,
            string deletedAtUtc,
            string reason)
        {
            _markerStore.RecordDeletedSlotGuard(
                slotNumber,
                importedSourceHash,
                deletedAtUtc,
                reason);
        }
    }

    public sealed class CampaignSaveService
    {
        private const int SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion;
        private const string DeleteSlotGuardReason = "DeleteSlot";

        private readonly ICampaignProfileRepository _repository;
        private readonly ICampaignSaveResetMarkerPort _resetMarkerPort;
        private readonly ICampaignDeletedSlotGuardMarkerPort _deletedSlotGuardMarkerPort;
        private readonly Func<string> _utcNowProvider;
        private readonly string _profileId;
        private readonly string _productVersion;
        private CampaignProfileLoadStatus _lastProfileLoadStatus = CampaignProfileLoadStatus.Missing;

        public CampaignSaveService(
            ICampaignProfileRepository repository,
            ICampaignSaveResetMarkerPort resetMarkerPort,
            Func<string> utcNowProvider,
            string profileId,
            string productVersion,
            ICampaignDeletedSlotGuardMarkerPort deletedSlotGuardMarkerPort = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _resetMarkerPort = resetMarkerPort;
            _deletedSlotGuardMarkerPort = deletedSlotGuardMarkerPort;
            _utcNowProvider = utcNowProvider ?? DefaultUtcNow;
            _profileId = string.IsNullOrWhiteSpace(profileId) ? "campaign-profile" : profileId;
            _productVersion = productVersion ?? string.Empty;
        }

        public CampaignSaveService(
            ICampaignProfileRepository repository,
            ICampaignSaveResetMarkerPort resetMarkerPort = null,
            Func<string> utcNowProvider = null)
            : this(repository, resetMarkerPort, utcNowProvider, "campaign-profile", string.Empty)
        {
        }

        public CampaignSaveServiceResult LoadProfile()
        {
            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            return CampaignSaveServiceResult.Success(
                document,
                message: "Campaign profile loaded.",
                hasProfileLoadStatus: true,
                profileLoadStatus: _lastProfileLoadStatus);
        }

        public CampaignSaveServiceResult GetSlots()
        {
            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            return CampaignSaveServiceResult.Success(
                document,
                message: "Campaign slots loaded.",
                hasProfileLoadStatus: true,
                profileLoadStatus: _lastProfileLoadStatus);
        }

        public CampaignSaveServiceResult GetSlot(int slotNumber)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            return TryFindSlot(document, slotNumber, out var slot)
                ? CampaignSaveServiceResult.Success(document, CloneSlot(slot), message: "Campaign slot loaded.")
                : CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.SlotNotFound,
                    "Campaign slot does not exist.",
                    document);
        }

        public CampaignSaveServiceResult InitializeNewGame(
            int slotNumber,
            string initialStageId,
            string initialLevelGroupId)
        {
            return InitializeNewGame(new CampaignNewGameRequest
            {
                SlotNumber = slotNumber,
                InitialStageId = initialStageId,
                InitialLevelGroupId = initialLevelGroupId,
            });
        }

        public CampaignSaveServiceResult InitializeNewGame(CampaignNewGameRequest request)
        {
            if (request == null)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "New game request must not be null.");
            }

            if (!SaveSlotStore.IsValidSlotNumber(request.SlotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            if (string.IsNullOrWhiteSpace(request.InitialStageId))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Initial stage id must not be empty.");
            }

            return Mutate(document =>
            {
                var now = Now();
                var lastPlayedAtUtc = string.IsNullOrWhiteSpace(request.LastPlayedAtUtc)
                    ? now
                    : request.LastPlayedAtUtc;
                var slot = new CampaignSlotDocument
                {
                    SlotNumber = request.SlotNumber,
                    StageId = request.InitialStageId ?? string.Empty,
                    LevelGroupId = request.InitialLevelGroupId ?? string.Empty,
                    RemainingChances = SaveSlotStore.DefaultRemainingChances,
                    CampaignCompleted = false,
                    NormalCampaignCompletionReceipt = null,
                    IntroPlayed = false,
                    OutroPlayed = false,
                    TotalDeaths = 0,
                    LastPlayedAtUtc = lastPlayedAtUtc,
                    StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
                };

                UpsertSlot(document, slot);
                document.LastPlayedSlotNumber = request.SlotNumber;
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    message: "New campaign slot initialized.");
            });
        }

        public CampaignSaveServiceResult UpdateSlot(int slotNumber, CampaignSlotUpdate update)
        {
            if (update == null)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Slot update must not be null.");
            }

            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                var now = Now();
                var slot = GetOrCreateSlot(document, slotNumber);
                ApplySlotUpdate(slot, update);
                TouchSlot(
                    slot,
                    string.IsNullOrWhiteSpace(update.LastPlayedAtUtc)
                        ? now
                        : update.LastPlayedAtUtc);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    message: "Campaign slot updated.");
            });
        }

        public CampaignSaveServiceResult DeleteSlot(int slotNumber)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out _))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                RemoveSlot(document, slotNumber);
                if (document.LastPlayedSlotNumber == slotNumber)
                {
                    document.LastPlayedSlotNumber = FindFirstSlotNumber(document);
                }

                var now = Now();
                UpsertDeletedSlotGuard(document, slotNumber, now, DeleteSlotGuardReason);
                _deletedSlotGuardMarkerPort?.RecordDeletedSlotGuard(
                    slotNumber,
                    document.LegacyImport.ImportedSourceHash,
                    now,
                    DeleteSlotGuardReason);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(document, message: "Campaign slot deleted.");
            });
        }

        public CampaignSaveServiceResult ClearAll()
        {
            return Mutate(document =>
            {
                var now = Now();
                document.SchemaVersion = SchemaVersion;
                document.ProductVersion = _productVersion;
                document.ProfileId = _profileId;
                document.Slots = Array.Empty<CampaignSlotDocument>();
                document.LastPlayedSlotNumber = 0;
                document.LegacyImport = new CampaignLegacyImportDocument
                {
                    ImportDisabled = true,
                    ResetTombstoneUtc = now,
                };
                TouchProfile(document, now);
                _resetMarkerPort?.MarkResetImportDisabled(now);
                return CampaignSaveServiceResult.Success(document, message: "Campaign profile cleared.");
            });
        }

        public CampaignSaveServiceResult MarkLastPlayedSlot(int slotNumber)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out _))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                document.LastPlayedSlotNumber = slotNumber;
                TouchProfile(document, Now());
                return CampaignSaveServiceResult.Success(document, message: "Last played campaign slot marked.");
            });
        }

        public CampaignSaveServiceResult ApplyDeath(int slotNumber, CampaignDeathSaveUpdate update)
        {
            if (update == null)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Death save update must not be null.");
            }

            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out var slot))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                var now = Now();
                if (!string.IsNullOrWhiteSpace(update.StageId))
                {
                    slot.StageId = update.StageId;
                }

                if (update.LevelGroupId != null)
                {
                    slot.LevelGroupId = update.LevelGroupId;
                }

                if (update.RemainingChances.HasValue)
                {
                    slot.RemainingChances = Math.Max(0, update.RemainingChances.Value);
                }

                slot.TotalDeaths = Math.Max(0, slot.TotalDeaths) + Math.Max(0, update.DeathsToAdd);
                TouchSlot(slot, now);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    message: "Campaign death update applied.");
            });
        }

        public CampaignSaveServiceResult ApplyStageClear(int slotNumber, StageClearSaveUpdate update)
        {
            if (update == null)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Stage clear save update must not be null.");
            }

            if (string.IsNullOrWhiteSpace(update.ClearedStageId))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Cleared stage id must not be empty.");
            }

            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out var slot))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                var now = Now();
                slot.StageId = string.IsNullOrWhiteSpace(update.NextStageId)
                    ? update.ClearedStageId
                    : update.NextStageId;
                if (update.LevelGroupId != null)
                {
                    slot.LevelGroupId = update.LevelGroupId;
                }

                if (update.RemainingChances.HasValue)
                {
                    slot.RemainingChances = Math.Max(0, update.RemainingChances.Value);
                }

                slot.CampaignCompleted = update.IsCampaignCompleted;
                slot.StageClearProfileSnapshot = ApplyStageClearProfileUpdate(
                    slot.StageClearProfileSnapshot,
                    update);
                TouchSlot(slot, now);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    CloneStageClearProfile(slot.StageClearProfileSnapshot),
                    "Campaign stage clear update applied.");
            });
        }

        public CampaignSaveServiceResult SetIntroPlayed(int slotNumber, string stageId)
        {
            return SetCinematicFlag(slotNumber, new CampaignCinematicFlagUpdate { StageId = stageId, Played = true }, intro: true);
        }

        public CampaignSaveServiceResult SetOutroPlayed(int slotNumber, string stageId)
        {
            return SetCinematicFlag(slotNumber, new CampaignCinematicFlagUpdate { StageId = stageId, Played = true }, intro: false);
        }

        public CampaignSaveServiceResult GetStageClearProfile(int slotNumber)
        {
            var slotResult = GetSlot(slotNumber);
            if (!slotResult.Succeeded)
            {
                return slotResult;
            }

            return CampaignSaveServiceResult.Success(
                slotResult.Document,
                slotResult.Slot,
                CloneStageClearProfile(slotResult.Slot.StageClearProfileSnapshot),
                "Campaign stage clear profile loaded.");
        }

        private CampaignSaveServiceResult SetCinematicFlag(
            int slotNumber,
            CampaignCinematicFlagUpdate update,
            bool intro)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out var slot))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                var now = Now();
                if (!string.IsNullOrWhiteSpace(update.StageId) && string.IsNullOrWhiteSpace(slot.StageId))
                {
                    slot.StageId = update.StageId;
                }

                if (intro)
                {
                    slot.IntroPlayed = update.Played;
                }
                else
                {
                    slot.OutroPlayed = update.Played;
                }

                TouchSlot(slot, now);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    message: intro ? "Intro cinematic flag saved." : "Outro cinematic flag saved.");
            });
        }

        private CampaignSaveServiceResult Mutate(Func<CampaignProfileDocument, CampaignSaveServiceResult> mutation)
        {
            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            var result = mutation(document);
            if (!result.Succeeded)
            {
                return result;
            }

            try
            {
                Normalize(document);
                _repository.Save(document);
            }
            catch (Exception exception)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.SaveFailed,
                    exception.Message,
                    document);
            }

            return result;
        }

        private bool TryLoadProfile(
            out CampaignProfileDocument document,
            out CampaignSaveServiceResult failure,
            bool allowMissing)
        {
            document = null;
            failure = null;
            CampaignProfileLoadResult loadResult;
            try
            {
                loadResult = _repository.Load();
            }
            catch (Exception exception)
            {
                failure = CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.LoadFailed,
                    exception.Message);
                return false;
            }

            if (loadResult.Status == CampaignProfileLoadStatus.Loaded ||
                loadResult.Status == CampaignProfileLoadStatus.BackupRecovered)
            {
                _lastProfileLoadStatus = loadResult.Status;
                document = CloneProfile(loadResult.Document);
                Normalize(document);
                return true;
            }

            if (allowMissing && loadResult.Status == CampaignProfileLoadStatus.Missing)
            {
                _lastProfileLoadStatus = loadResult.Status;
                document = CreateEmptyProfile();
                return true;
            }

            _lastProfileLoadStatus = loadResult.Status;
            failure = CampaignSaveServiceResult.Failure(
                CampaignSaveCommandStatus.LoadFailed,
                loadResult.Message,
                hasProfileLoadStatus: true,
                profileLoadStatus: loadResult.Status);
            return false;
        }

        private CampaignProfileDocument CreateEmptyProfile()
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = SchemaVersion,
                ProductVersion = _productVersion,
                SavedAtUtc = string.Empty,
                ProfileId = _profileId,
                LastPlayedSlotNumber = 0,
                LegacyImport = new CampaignLegacyImportDocument(),
                Slots = Array.Empty<CampaignSlotDocument>(),
            };
        }

        private static void ApplySlotUpdate(CampaignSlotDocument slot, CampaignSlotUpdate update)
        {
            if (update.StageId != null)
            {
                slot.StageId = update.StageId;
            }

            if (update.LevelGroupId != null)
            {
                slot.LevelGroupId = update.LevelGroupId;
            }

            if (update.RemainingChances.HasValue)
            {
                slot.RemainingChances = Math.Max(0, update.RemainingChances.Value);
            }

            if (update.CampaignCompleted.HasValue)
            {
                slot.CampaignCompleted = update.CampaignCompleted.Value;
            }

            if (update.NormalCampaignCompletionReceipt != null)
            {
                slot.HasNormalCampaignCompletionReceipt = true;
                slot.NormalCampaignCompletionReceipt = CloneReceipt(
                    update.NormalCampaignCompletionReceipt);
            }

            if (update.IntroPlayed.HasValue)
            {
                slot.IntroPlayed = update.IntroPlayed.Value;
            }

            if (update.OutroPlayed.HasValue)
            {
                slot.OutroPlayed = update.OutroPlayed.Value;
            }

            if (update.TotalDeaths.HasValue)
            {
                slot.TotalDeaths = Math.Max(0, update.TotalDeaths.Value);
            }

            if (update.LastPlayedAtUtc != null)
            {
                slot.LastPlayedAtUtc = update.LastPlayedAtUtc;
            }

            if (update.StageClearProfileSnapshot != null)
            {
                slot.StageClearProfileSnapshot = CloneStageClearProfile(update.StageClearProfileSnapshot);
            }
        }

        private static CampaignStageClearProfileDocument ApplyStageClearProfileUpdate(
            CampaignStageClearProfileDocument profile,
            StageClearSaveUpdate update)
        {
            profile = CloneStageClearProfile(profile);
            var records = new List<PlayerStageClearRecordDocument>(
                profile.Records ?? Array.Empty<PlayerStageClearRecordDocument>());
            var record = records.Find(item => string.Equals(item?.StageId, update.ClearedStageId, StringComparison.Ordinal));
            if (record == null)
            {
                record = new PlayerStageClearRecordDocument
                {
                    StageId = update.ClearedStageId,
                    ProcessedStageRunIds = Array.Empty<string>(),
                };
                records.Add(record);
            }

            var wasProcessed = Contains(profile.ProcessedClearAttemptIds, update.StageCompletionAttemptId) ||
                               Contains(profile.ProcessedStageRunIds, update.StageRunId);
            record.HasAttempted |= update.HasAttempted;
            record.HasCleared |= update.HasCleared;
            record.ProcessedStageRunIds = AddUnique(record.ProcessedStageRunIds, update.StageRunId, sort: false);
            profile.ProcessedStageRunIds = AddUnique(profile.ProcessedStageRunIds, update.StageRunId, sort: true);
            profile.ProcessedClearAttemptIds = AddUnique(
                profile.ProcessedClearAttemptIds,
                update.StageCompletionAttemptId,
                sort: true);

            if (!wasProcessed)
            {
                if (update.HasCleared)
                {
                    record.ClearCount = Math.Max(0, record.ClearCount) + Math.Max(0, update.ClearCountIncrement);
                }

                profile.Version = Math.Max(0, profile.Version) + 1;
            }

            records.Sort((left, right) => string.CompareOrdinal(left?.StageId, right?.StageId));
            profile.Records = records.ToArray();
            return profile;
        }

        private static bool Contains(string[] values, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || values == null)
            {
                return false;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] AddUnique(string[] values, string value, bool sort)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return CloneArray(values);
            }

            var list = new List<string>(values ?? Array.Empty<string>());
            if (!list.Contains(value))
            {
                list.Add(value);
            }

            if (sort)
            {
                list.Sort(StringComparer.Ordinal);
            }

            return list.ToArray();
        }

        private static CampaignSlotDocument GetOrCreateSlot(CampaignProfileDocument document, int slotNumber)
        {
            if (TryFindSlot(document, slotNumber, out var slot))
            {
                return slot;
            }

            slot = new CampaignSlotDocument
            {
                SlotNumber = slotNumber,
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
            };
            UpsertSlot(document, slot);
            return slot;
        }

        private static bool TryFindSlot(
            CampaignProfileDocument document,
            int slotNumber,
            out CampaignSlotDocument slot)
        {
            slot = null;
            var slots = document?.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].SlotNumber == slotNumber)
                {
                    slot = slots[i];
                    return true;
                }
            }

            return false;
        }

        private static void UpsertSlot(CampaignProfileDocument document, CampaignSlotDocument slot)
        {
            RemoveSlot(document, slot.SlotNumber);
            var slots = new List<CampaignSlotDocument>(document.Slots ?? Array.Empty<CampaignSlotDocument>())
            {
                slot,
            };
            slots.Sort((left, right) => left.SlotNumber.CompareTo(right.SlotNumber));
            document.Slots = slots.ToArray();
        }

        private static void RemoveSlot(CampaignProfileDocument document, int slotNumber)
        {
            var slots = new List<CampaignSlotDocument>();
            var existing = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].SlotNumber != slotNumber)
                {
                    slots.Add(existing[i]);
                }
            }

            document.Slots = slots.ToArray();
        }

        private static int FindFirstSlotNumber(CampaignProfileDocument document)
        {
            var slots = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            Array.Sort(slots, (left, right) => left.SlotNumber.CompareTo(right.SlotNumber));
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && SaveSlotStore.IsValidSlotNumber(slots[i].SlotNumber))
                {
                    return slots[i].SlotNumber;
                }
            }

            return 0;
        }

        private static void TouchProfile(CampaignProfileDocument document, string now)
        {
            document.SavedAtUtc = now ?? string.Empty;
        }

        private static void TouchSlot(CampaignSlotDocument slot, string now)
        {
            slot.LastPlayedAtUtc = now ?? string.Empty;
        }

        private string Now()
        {
            return _utcNowProvider();
        }

        private static string DefaultUtcNow()
        {
            return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        private static void Normalize(CampaignProfileDocument document)
        {
            document.SchemaVersion = document.SchemaVersion <= 0 ? SchemaVersion : document.SchemaVersion;
            document.ProfileId = string.IsNullOrWhiteSpace(document.ProfileId) ? "campaign-profile" : document.ProfileId;
            document.ProductVersion ??= string.Empty;
            document.SavedAtUtc ??= string.Empty;
            document.LegacyImport ??= new CampaignLegacyImportDocument();
            document.LegacyImport.ImportedSourceHash ??= string.Empty;
            document.LegacyImport.ResetTombstoneUtc ??= string.Empty;
            document.LegacyImport.DeletedSlotGuards =
                NormalizeDeletedSlotGuards(document.LegacyImport.DeletedSlotGuards);
            document.Slots ??= Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < document.Slots.Length; i++)
            {
                Normalize(document.Slots[i]);
            }
        }

        private static void Normalize(CampaignSlotDocument slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.StageId ??= string.Empty;
            slot.LevelGroupId ??= string.Empty;
            slot.LastPlayedAtUtc ??= string.Empty;
            slot.RemainingChances = Math.Max(0, slot.RemainingChances);
            slot.TotalDeaths = Math.Max(0, slot.TotalDeaths);
            slot.StageClearProfileSnapshot = CloneStageClearProfile(slot.StageClearProfileSnapshot);
        }

        private static CampaignProfileDocument CloneProfile(CampaignProfileDocument document)
        {
            if (document == null)
            {
                return null;
            }

            var slots = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            var clonedSlots = new CampaignSlotDocument[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                clonedSlots[i] = CloneSlot(slots[i]);
            }

            return new CampaignProfileDocument
            {
                SchemaVersion = document.SchemaVersion,
                ProductVersion = document.ProductVersion ?? string.Empty,
                SavedAtUtc = document.SavedAtUtc ?? string.Empty,
                ProfileId = document.ProfileId ?? string.Empty,
                LastPlayedSlotNumber = document.LastPlayedSlotNumber,
                LegacyImport = CloneLegacyImport(document.LegacyImport),
                Slots = clonedSlots,
            };
        }

        private static CampaignLegacyImportDocument CloneLegacyImport(CampaignLegacyImportDocument legacyImport)
        {
            legacyImport ??= new CampaignLegacyImportDocument();
            return new CampaignLegacyImportDocument
            {
                ImportedSourceHash = legacyImport.ImportedSourceHash ?? string.Empty,
                ImportDisabled = legacyImport.ImportDisabled,
                ResetTombstoneUtc = legacyImport.ResetTombstoneUtc ?? string.Empty,
                DeletedSlotGuards = CloneDeletedSlotGuards(legacyImport.DeletedSlotGuards),
            };
        }

        private static void UpsertDeletedSlotGuard(
            CampaignProfileDocument document,
            int slotNumber,
            string deletedAtUtc,
            string reason)
        {
            document.LegacyImport ??= new CampaignLegacyImportDocument();
            document.LegacyImport.DeletedSlotGuards =
                UpsertDeletedSlotGuard(
                    document.LegacyImport.DeletedSlotGuards,
                    new CampaignLegacyDeletedSlotGuardDocument
                    {
                        SlotNumber = slotNumber,
                        ImportedSourceHash = document.LegacyImport.ImportedSourceHash ?? string.Empty,
                        DeletedAtUtc = deletedAtUtc ?? string.Empty,
                        Reason = reason ?? string.Empty,
                    });
        }

        private static CampaignLegacyDeletedSlotGuardDocument[] UpsertDeletedSlotGuard(
            CampaignLegacyDeletedSlotGuardDocument[] existing,
            CampaignLegacyDeletedSlotGuardDocument replacement)
        {
            if (replacement == null || !SaveSlotStore.IsValidSlotNumber(replacement.SlotNumber))
            {
                return NormalizeDeletedSlotGuards(existing);
            }

            var guards = new List<CampaignLegacyDeletedSlotGuardDocument>(
                NormalizeDeletedSlotGuards(existing));
            var normalizedHash = replacement.ImportedSourceHash ?? string.Empty;
            for (var i = 0; i < guards.Count; i++)
            {
                var guard = guards[i];
                if (guard.SlotNumber == replacement.SlotNumber &&
                    string.Equals(
                        guard.ImportedSourceHash ?? string.Empty,
                        normalizedHash,
                        StringComparison.Ordinal))
                {
                    guards[i] = CloneDeletedSlotGuard(replacement);
                    return guards.ToArray();
                }
            }

            guards.Add(CloneDeletedSlotGuard(replacement));
            return guards.ToArray();
        }

        private static CampaignLegacyDeletedSlotGuardDocument[] NormalizeDeletedSlotGuards(
            CampaignLegacyDeletedSlotGuardDocument[] guards)
        {
            if (guards == null || guards.Length == 0)
            {
                return Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
            }

            var normalized = new List<CampaignLegacyDeletedSlotGuardDocument>();
            for (var i = 0; i < guards.Length; i++)
            {
                var guard = guards[i];
                if (guard == null || !SaveSlotStore.IsValidSlotNumber(guard.SlotNumber))
                {
                    continue;
                }

                normalized.Add(CloneDeletedSlotGuard(guard));
            }

            return normalized.ToArray();
        }

        private static CampaignLegacyDeletedSlotGuardDocument[] CloneDeletedSlotGuards(
            CampaignLegacyDeletedSlotGuardDocument[] guards)
        {
            return NormalizeDeletedSlotGuards(guards);
        }

        private static CampaignLegacyDeletedSlotGuardDocument CloneDeletedSlotGuard(
            CampaignLegacyDeletedSlotGuardDocument guard)
        {
            if (guard == null)
            {
                return null;
            }

            return new CampaignLegacyDeletedSlotGuardDocument
            {
                SlotNumber = guard.SlotNumber,
                ImportedSourceHash = guard.ImportedSourceHash ?? string.Empty,
                DeletedAtUtc = guard.DeletedAtUtc ?? string.Empty,
                Reason = guard.Reason ?? string.Empty,
            };
        }

        private static CampaignSlotDocument CloneSlot(CampaignSlotDocument slot)
        {
            if (slot == null)
            {
                return null;
            }

            return new CampaignSlotDocument
            {
                SlotNumber = slot.SlotNumber,
                StageId = slot.StageId ?? string.Empty,
                LevelGroupId = slot.LevelGroupId ?? string.Empty,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    slot.NormalCampaignCompletionReceipt != null,
                NormalCampaignCompletionReceipt = CloneReceipt(
                    slot.NormalCampaignCompletionReceipt),
                IntroPlayed = slot.IntroPlayed,
                OutroPlayed = slot.OutroPlayed,
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAtUtc ?? string.Empty,
                StageClearProfileSnapshot = CloneStageClearProfile(slot.StageClearProfileSnapshot),
            };
        }

        private static CampaignStageClearProfileDocument CloneStageClearProfile(
            CampaignStageClearProfileDocument profile)
        {
            profile ??= new CampaignStageClearProfileDocument();
            var records = profile.Records ?? Array.Empty<PlayerStageClearRecordDocument>();
            var clonedRecords = new PlayerStageClearRecordDocument[records.Length];
            for (var i = 0; i < records.Length; i++)
            {
                clonedRecords[i] = CloneRecord(records[i]);
            }

            return new CampaignStageClearProfileDocument
            {
                Version = Math.Max(0, profile.Version),
                Records = clonedRecords,
                ProcessedStageRunIds = CloneArray(profile.ProcessedStageRunIds),
                ProcessedClearAttemptIds = CloneArray(profile.ProcessedClearAttemptIds),
            };
        }

        private static NormalCampaignCompletionReceiptDocument CloneReceipt(
            NormalCampaignCompletionReceiptDocument receipt)
        {
            if (receipt == null)
            {
                return null;
            }

            return new NormalCampaignCompletionReceiptDocument
            {
                Version = receipt.Version,
                CompletedStageId = receipt.CompletedStageId,
                StageRunId = receipt.StageRunId,
                ClearSource = receipt.ClearSource,
            };
        }

        private static PlayerStageClearRecordDocument CloneRecord(PlayerStageClearRecordDocument record)
        {
            if (record == null)
            {
                return null;
            }

            return new PlayerStageClearRecordDocument
            {
                StageId = record.StageId ?? string.Empty,
                HasAttempted = record.HasAttempted,
                HasCleared = record.HasCleared,
                ClearCount = Math.Max(0, record.ClearCount),
                ProcessedStageRunIds = CloneArray(record.ProcessedStageRunIds),
            };
        }

        private static string[] CloneArray(string[] values)
        {
            return (string[])(values ?? Array.Empty<string>()).Clone();
        }
    }
}
