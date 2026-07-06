using System;

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
            if (!string.IsNullOrWhiteSpace(importedSourceHash) &&
                string.Equals(
                    _markerStore.GetImportedSourceHash(),
                    importedSourceHash,
                    StringComparison.Ordinal))
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
                    importResult.Document,
                    profileWriteAttempted: false,
                    profileWriteSucceeded: false,
                    requiresRepair,
                    requiresRepair
                        ? "profile.json requires repair; legacy candidate is deferred to avoid rollback."
                        : "Legacy campaign source is importable, but profile write is disabled.");
            }

            EnsureLegacyImportMarker(importResult.Document, importedSourceHash);
            try
            {
                _repository.Save(importResult.Document);
            }
            catch (Exception exception)
            {
                return Result(
                    CampaignSaveMigrationStatus.ImportWriteFailed,
                    loadResult,
                    importResult,
                    importResult.Document,
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
                importResult.Document,
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
