using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public sealed class CampaignSaveMigrationOptions
    {
        public static readonly CampaignSaveMigrationOptions Default = new();

        public bool EnableProfileWrite { get; set; }
    }

    public enum CampaignSaveMigrationStatus
    {
        FileLoaded = 0,
        FileBackupRecovered = 1,
        NoSave = 2,
        ImportBlocked = 3,
        LegacyMissing = 4,
        LegacyInvalid = 5,
        MigrationDeferred = 6,
        ImportSucceeded = 7,
        ImportWriteFailed = 8,
        RepairRequired = 9,
        SchemaInvalid = 10,
        IoFailed = 11,
        AlreadyImported = 12,
        Unauthorized = 13,
    }

    public sealed class CampaignSaveMigrationResult
    {
        public CampaignSaveMigrationResult(
            CampaignSaveMigrationStatus status,
            CampaignProfileLoadResult profileLoadResult,
            CampaignLegacyImportResult legacyImportResult,
            CampaignProfileDocument document,
            bool profileWriteAttempted,
            bool profileWriteSucceeded,
            bool requiresRepair,
            string message)
        {
            Status = status;
            ProfileLoadResult = profileLoadResult;
            LegacyImportResult = legacyImportResult;
            Document = document;
            ProfileWriteAttempted = profileWriteAttempted;
            ProfileWriteSucceeded = profileWriteSucceeded;
            RequiresRepair = requiresRepair;
            Message = message ?? string.Empty;
        }

        public CampaignSaveMigrationStatus Status { get; }

        public CampaignProfileLoadResult ProfileLoadResult { get; }

        public CampaignLegacyImportResult LegacyImportResult { get; }

        public CampaignProfileDocument Document { get; }

        public bool ProfileWriteAttempted { get; }

        public bool ProfileWriteSucceeded { get; }

        public bool RequiresRepair { get; }

        public string Message { get; }

        public bool HasImportCandidate => LegacyImportResult != null && LegacyImportResult.HasDocument;
    }

    public sealed class CampaignSaveMigrationCoordinator
    {
        private readonly ICampaignProfileRepository _repository;
        private readonly ICampaignLegacyImportCandidateSource _legacyImportSource;
        private readonly ICampaignLegacyImportMarkerStore _markerStore;
        private readonly CampaignSaveMigrationOptions _options;

        public CampaignSaveMigrationCoordinator(
            ICampaignProfileRepository repository,
            ICampaignLegacyImportCandidateSource legacyImportSource,
            ICampaignLegacyImportMarkerStore markerStore,
            CampaignSaveMigrationOptions options = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _legacyImportSource = legacyImportSource ?? throw new ArgumentNullException(nameof(legacyImportSource));
            _markerStore = markerStore ?? throw new ArgumentNullException(nameof(markerStore));
            _options = options ?? CampaignSaveMigrationOptions.Default;
        }

        public CampaignSaveMigrationCoordinator()
            : this(
                new FileCampaignProfileRepository(
                    new AtomicTextFileStore(UnityEngine.Application.persistentDataPath)),
                new LegacyPlayerPrefsCampaignImporter(),
                new CampaignLegacyImportMarkerStore(),
                CampaignSaveMigrationOptions.Default)
        {
        }

        public CampaignSaveMigrationResult Run()
        {
            var loadResult = _repository.Load();
            switch (loadResult.Status)
            {
                case CampaignProfileLoadStatus.Loaded:
                    return Result(
                        CampaignSaveMigrationStatus.FileLoaded,
                        loadResult,
                        null,
                        loadResult.Document,
                        profileWriteAttempted: false,
                        profileWriteSucceeded: false,
                        requiresRepair: false,
                        "profile.json loaded; legacy import was not consulted.");

                case CampaignProfileLoadStatus.BackupRecovered:
                    return Result(
                        CampaignSaveMigrationStatus.FileBackupRecovered,
                        loadResult,
                        null,
                        loadResult.Document,
                        profileWriteAttempted: false,
                        profileWriteSucceeded: false,
                        requiresRepair: false,
                        "profile.json backup recovered; legacy import was not consulted.");

                case CampaignProfileLoadStatus.SchemaInvalid:
                    return Result(
                        CampaignSaveMigrationStatus.SchemaInvalid,
                        loadResult,
                        null,
                        null,
                        profileWriteAttempted: false,
                        profileWriteSucceeded: false,
                        requiresRepair: true,
                        "profile.json schema is invalid and requires repair.");

                case CampaignProfileLoadStatus.Unauthorized:
                    return Result(
                        CampaignSaveMigrationStatus.Unauthorized,
                        loadResult,
                        null,
                        null,
                        profileWriteAttempted: false,
                        profileWriteSucceeded: false,
                        requiresRepair: false,
                        "profile.json could not be read due to authorization failure.");

                case CampaignProfileLoadStatus.IoFailed:
                    return Result(
                        CampaignSaveMigrationStatus.IoFailed,
                        loadResult,
                        null,
                        null,
                        profileWriteAttempted: false,
                        profileWriteSucceeded: false,
                        requiresRepair: false,
                        "profile.json could not be read due to an IO failure.");

                case CampaignProfileLoadStatus.CorruptQuarantined:
                case CampaignProfileLoadStatus.CorruptNoFallback:
                    return HandleLegacyCandidate(loadResult, allowWrite: false, requiresRepair: true);

                case CampaignProfileLoadStatus.Missing:
                    return HandleLegacyCandidate(
                        loadResult,
                        allowWrite: _options.EnableProfileWrite,
                        requiresRepair: false);

                default:
                    return Result(
                        CampaignSaveMigrationStatus.IoFailed,
                        loadResult,
                        null,
                        null,
                        profileWriteAttempted: false,
                        profileWriteSucceeded: false,
                        requiresRepair: false,
                        "profile.json returned an unknown load status.");
            }
        }

        private CampaignSaveMigrationResult HandleLegacyCandidate(
            CampaignProfileLoadResult loadResult,
            bool allowWrite,
            bool requiresRepair)
        {
            if (_markerStore.IsImportDisabled() || _markerStore.HasResetTombstone())
            {
                return Result(
                    CampaignSaveMigrationStatus.ImportBlocked,
                    loadResult,
                    null,
                    null,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    "Legacy campaign import is blocked by a local marker.");
            }

            var importResult = _legacyImportSource.BuildImportCandidate();
            if (importResult == null)
            {
                return Result(
                    requiresRepair ? CampaignSaveMigrationStatus.RepairRequired : CampaignSaveMigrationStatus.NoSave,
                    loadResult,
                    null,
                    null,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    requiresRepair
                        ? "profile.json requires repair and no legacy candidate was available."
                        : "No campaign profile or legacy campaign source was available.");
            }

            if (importResult.Status == CampaignLegacyImportStatus.ImportDisabled)
            {
                return Result(
                    CampaignSaveMigrationStatus.ImportBlocked,
                    loadResult,
                    importResult,
                    null,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    importResult.Reason);
            }

            if (importResult.Status == CampaignLegacyImportStatus.Missing)
            {
                return Result(
                    requiresRepair ? CampaignSaveMigrationStatus.RepairRequired : CampaignSaveMigrationStatus.LegacyMissing,
                    loadResult,
                    importResult,
                    null,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    importResult.Reason);
            }

            if (importResult.Status != CampaignLegacyImportStatus.Importable || !importResult.HasDocument)
            {
                return Result(
                    requiresRepair ? CampaignSaveMigrationStatus.RepairRequired : CampaignSaveMigrationStatus.LegacyInvalid,
                    loadResult,
                    importResult,
                    null,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    importResult.Reason);
            }

            var importedSourceHash = importResult.ImportedSourceHash;
            var guardApplication = ApplyDeletedSlotGuards(importResult.Document, importedSourceHash);
            if (guardApplication.BlockedByChangedSource)
            {
                return Result(
                    CampaignSaveMigrationStatus.MigrationDeferred,
                    loadResult,
                    importResult,
                    guardApplication.Document,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    "Legacy campaign source changed and contains a guarded deleted slot; automatic import is deferred.");
            }

            if (guardApplication.AllImportableSlotsGuarded)
            {
                return Result(
                    CampaignSaveMigrationStatus.MigrationDeferred,
                    loadResult,
                    importResult,
                    null,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    "Legacy campaign source contains only deleted-slot guarded importable slots; automatic import is deferred.");
            }

            if (!string.IsNullOrWhiteSpace(importedSourceHash) &&
                string.Equals(
                    _markerStore.GetImportedSourceHash(),
                    importedSourceHash,
                    StringComparison.Ordinal) &&
                !guardApplication.FilteredAnySlot)
            {
                return Result(
                    CampaignSaveMigrationStatus.AlreadyImported,
                    loadResult,
                    importResult,
                    importResult.Document,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    "Legacy campaign source hash is already recorded as imported.");
            }

            if (!allowWrite)
            {
                return Result(
                    CampaignSaveMigrationStatus.MigrationDeferred,
                    loadResult,
                    importResult,
                    guardApplication.Document,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    requiresRepair
                        ? "profile.json requires repair; legacy candidate is deferred to avoid rollback."
                        : "Legacy campaign source is importable, but profile write is disabled.");
            }

            EnsureLegacyImportMarker(guardApplication.Document, importedSourceHash);
            try
            {
                _repository.Save(guardApplication.Document);
            }
            catch (Exception exception)
            {
                return Result(
                    CampaignSaveMigrationStatus.ImportWriteFailed,
                    loadResult,
                    importResult,
                    guardApplication.Document,
                    profileWriteAttempted: true,
                    profileWriteSucceeded: false,
                    requiresRepair: false,
                    exception.Message);
            }

            _markerStore.SetImportedSourceHash(importedSourceHash);
            return Result(
                CampaignSaveMigrationStatus.ImportSucceeded,
                loadResult,
                importResult,
                guardApplication.Document,
                profileWriteAttempted: true,
                profileWriteSucceeded: true,
                requiresRepair: false,
                "Legacy campaign source was imported to profile.json.");
        }

        private static void EnsureLegacyImportMarker(CampaignProfileDocument document, string importedSourceHash)
        {
            if (document == null)
            {
                return;
            }

            document.LegacyImport ??= new CampaignLegacyImportDocument();
            document.LegacyImport.ImportedSourceHash = importedSourceHash ?? string.Empty;
            document.LegacyImport.DeletedSlotGuards ??=
                Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
        }

        private DeletedSlotGuardApplication ApplyDeletedSlotGuards(
            CampaignProfileDocument document,
            string importedSourceHash)
        {
            var candidate = CloneProfile(document);
            var guards = CollectDeletedSlotGuards(
                candidate?.LegacyImport?.DeletedSlotGuards,
                _markerStore.ReadDeletedSlotGuards());
            if (candidate == null || guards.Length == 0)
            {
                return new DeletedSlotGuardApplication(candidate, false, false, false);
            }

            var originalImportableSlotCount = CountImportableSlots(candidate.Slots);
            var changedSourceBlocked = false;
            for (var i = 0; i < guards.Length; i++)
            {
                var guard = guards[i];
                if (IsChangedSourceGuard(guard, importedSourceHash) &&
                    ContainsImportableSlot(candidate.Slots, guard.SlotNumber))
                {
                    changedSourceBlocked = true;
                    break;
                }
            }

            if (changedSourceBlocked)
            {
                return new DeletedSlotGuardApplication(
                    candidate,
                    filteredAnySlot: false,
                    allImportableSlotsGuarded: false,
                    blockedByChangedSource: true);
            }

            var filteredSlots = new List<CampaignSlotDocument>();
            var filteredAny = false;
            var slots = candidate.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && IsGuardedForCurrentSource(slot.SlotNumber, importedSourceHash, guards))
                {
                    filteredAny = true;
                    continue;
                }

                filteredSlots.Add(slot);
            }

            candidate.Slots = filteredSlots.ToArray();
            if (filteredAny && !ContainsImportableSlot(candidate.Slots, candidate.LastPlayedSlotNumber))
            {
                candidate.LastPlayedSlotNumber = FindFirstImportableSlotNumber(candidate.Slots);
            }

            var allImportableGuarded =
                filteredAny &&
                originalImportableSlotCount > 0 &&
                CountImportableSlots(candidate.Slots) == 0;
            return new DeletedSlotGuardApplication(
                candidate,
                filteredAny,
                allImportableGuarded,
                blockedByChangedSource: false);
        }

        private static CampaignLegacyDeletedSlotGuardDocument[] CollectDeletedSlotGuards(
            CampaignLegacyDeletedSlotGuardDocument[] profileGuards,
            CampaignLegacyDeletedSlotGuardDocument[] localGuards)
        {
            var guards = new List<CampaignLegacyDeletedSlotGuardDocument>();
            AppendDeletedSlotGuards(guards, profileGuards);
            AppendDeletedSlotGuards(guards, localGuards);
            return guards.ToArray();
        }

        private static void AppendDeletedSlotGuards(
            List<CampaignLegacyDeletedSlotGuardDocument> destination,
            CampaignLegacyDeletedSlotGuardDocument[] source)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var guard = source[i];
                if (guard == null || !SaveSlotStore.IsValidSlotNumber(guard.SlotNumber))
                {
                    continue;
                }

                destination.Add(new CampaignLegacyDeletedSlotGuardDocument
                {
                    SlotNumber = guard.SlotNumber,
                    ImportedSourceHash = guard.ImportedSourceHash ?? string.Empty,
                    DeletedAtUtc = guard.DeletedAtUtc ?? string.Empty,
                    Reason = guard.Reason ?? string.Empty,
                });
            }
        }

        private static bool IsGuardedForCurrentSource(
            int slotNumber,
            string importedSourceHash,
            CampaignLegacyDeletedSlotGuardDocument[] guards)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return false;
            }

            for (var i = 0; i < guards.Length; i++)
            {
                var guard = guards[i];
                if (guard.SlotNumber != slotNumber)
                {
                    continue;
                }

                var guardHash = guard.ImportedSourceHash ?? string.Empty;
                if (string.IsNullOrWhiteSpace(guardHash) ||
                    string.Equals(guardHash, importedSourceHash ?? string.Empty, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsChangedSourceGuard(
            CampaignLegacyDeletedSlotGuardDocument guard,
            string importedSourceHash)
        {
            var guardHash = guard?.ImportedSourceHash ?? string.Empty;
            return !string.IsNullOrWhiteSpace(guardHash) &&
                   !string.Equals(guardHash, importedSourceHash ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool ContainsImportableSlot(CampaignSlotDocument[] slots, int slotNumber)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber) || slots == null)
            {
                return false;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.SlotNumber == slotNumber && IsImportableSlot(slot))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountImportableSlots(CampaignSlotDocument[] slots)
        {
            var count = 0;
            if (slots == null)
            {
                return count;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (IsImportableSlot(slots[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static int FindFirstImportableSlotNumber(CampaignSlotDocument[] slots)
        {
            var first = 0;
            if (slots == null)
            {
                return first;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (IsImportableSlot(slot) &&
                    (first == 0 || slot.SlotNumber < first))
                {
                    first = slot.SlotNumber;
                }
            }

            return first;
        }

        private static bool IsImportableSlot(CampaignSlotDocument slot)
        {
            return slot != null &&
                   SaveSlotStore.IsValidSlotNumber(slot.SlotNumber) &&
                   !string.IsNullOrWhiteSpace(slot.StageId);
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

        private static CampaignLegacyImportDocument CloneLegacyImport(
            CampaignLegacyImportDocument legacyImport)
        {
            legacyImport ??= new CampaignLegacyImportDocument();
            return new CampaignLegacyImportDocument
            {
                ImportedSourceHash = legacyImport.ImportedSourceHash ?? string.Empty,
                ImportDisabled = legacyImport.ImportDisabled,
                ResetTombstoneUtc = legacyImport.ResetTombstoneUtc ?? string.Empty,
                DeletedSlotGuards = CollectDeletedSlotGuards(
                    legacyImport.DeletedSlotGuards,
                    Array.Empty<CampaignLegacyDeletedSlotGuardDocument>()),
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
                IntroPlayed = slot.IntroPlayed,
                OutroPlayed = slot.OutroPlayed,
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAtUtc ?? string.Empty,
                StageClearProfileSnapshot = slot.StageClearProfileSnapshot ?? new CampaignStageClearProfileDocument(),
            };
        }

        private sealed class DeletedSlotGuardApplication
        {
            public DeletedSlotGuardApplication(
                CampaignProfileDocument document,
                bool filteredAnySlot,
                bool allImportableSlotsGuarded,
                bool blockedByChangedSource)
            {
                Document = document;
                FilteredAnySlot = filteredAnySlot;
                AllImportableSlotsGuarded = allImportableSlotsGuarded;
                BlockedByChangedSource = blockedByChangedSource;
            }

            public CampaignProfileDocument Document { get; }

            public bool FilteredAnySlot { get; }

            public bool AllImportableSlotsGuarded { get; }

            public bool BlockedByChangedSource { get; }
        }

        private static CampaignSaveMigrationResult Result(
            CampaignSaveMigrationStatus status,
            CampaignProfileLoadResult loadResult,
            CampaignLegacyImportResult importResult,
            CampaignProfileDocument document,
            bool profileWriteAttempted,
            bool profileWriteSucceeded,
            bool requiresRepair,
            string message)
        {
            return new CampaignSaveMigrationResult(
                status,
                loadResult,
                importResult,
                document,
                profileWriteAttempted,
                profileWriteSucceeded,
                requiresRepair,
                message);
        }
    }
}
