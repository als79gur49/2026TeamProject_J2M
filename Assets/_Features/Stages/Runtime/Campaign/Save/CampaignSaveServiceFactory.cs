using System;
using System.Globalization;

namespace Game.Feature.Stages
{
    public enum CampaignSaveBackendMode
    {
        PlayerPrefsLegacy = 0,
        ProfileJsonExplicit = 1,
    }

    public sealed class CampaignSaveCompositionOptions
    {
        public CampaignSaveBackendMode BackendMode { get; set; } = CampaignSaveBackendMode.PlayerPrefsLegacy;

        public ISavePathProvider PathProvider { get; set; }

        public string ProductVersion { get; set; } = string.Empty;

        public string ProfileId { get; set; } = "campaign-profile";

        public Func<DateTime> UtcNow { get; set; }

        public bool EnableProfileWrite { get; set; }

        public bool AllowLegacyImport { get; set; } = true;

        public string LegacyCampaignSourceKey { get; set; }

        public string LegacyActiveSlotKey { get; set; }

        public CampaignLegacyImportMarkerStore LegacyImportMarkerStore { get; set; }
    }

    public sealed class CampaignSaveFacadeFactoryResult
    {
        internal CampaignSaveFacadeFactoryResult(
            CampaignSaveBackendMode backendMode,
            ICampaignSaveSlotStore campaignSaveSlots,
            CampaignSaveServiceFactoryResult profileServices,
            CampaignSaveMigrationResult migrationResult)
        {
            BackendMode = backendMode;
            CampaignSaveSlots = campaignSaveSlots ?? throw new ArgumentNullException(nameof(campaignSaveSlots));
            ProfileServices = profileServices;
            MigrationResult = migrationResult;
        }

        public CampaignSaveBackendMode BackendMode { get; }

        public ICampaignSaveSlotStore CampaignSaveSlots { get; }

        public CampaignSaveServiceFactoryResult ProfileServices { get; }

        public CampaignSaveMigrationResult MigrationResult { get; }
    }

    public static class CampaignSaveFacadeFactory
    {
        public static CampaignSaveFacadeFactoryResult Create(CampaignSaveCompositionOptions options = null)
        {
            options ??= new CampaignSaveCompositionOptions();
            switch (options.BackendMode)
            {
                case CampaignSaveBackendMode.PlayerPrefsLegacy:
                    return new CampaignSaveFacadeFactoryResult(
                        options.BackendMode,
                        new SaveSlotStore(),
                        null,
                        null);

                case CampaignSaveBackendMode.ProfileJsonExplicit:
                    var profileServices = CampaignSaveServiceFactory.CreateForTests(
                        new CampaignSaveServiceFactoryOptions
                        {
                            PathProvider = options.PathProvider,
                            ProductVersion = options.ProductVersion,
                            ProfileId = options.ProfileId,
                            UtcNow = options.UtcNow,
                            EnableProfileWrite = options.EnableProfileWrite,
                            AllowLegacyImport = options.AllowLegacyImport,
                            LegacyCampaignSourceKey = options.LegacyCampaignSourceKey,
                            LegacyActiveSlotKey = options.LegacyActiveSlotKey,
                            LegacyImportMarkerStore = options.LegacyImportMarkerStore,
                            CreateCompatibilityAdapter = true,
                        });
                    var migrationResult = profileServices.Coordinator.Run();
                    return new CampaignSaveFacadeFactoryResult(
                        options.BackendMode,
                        profileServices.CompatibilityAdapter,
                        profileServices,
                        migrationResult);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(options),
                        options.BackendMode,
                        "Unsupported campaign save backend mode.");
            }
        }
    }

    public sealed class CampaignSaveServiceFactoryOptions
    {
        public ISavePathProvider PathProvider { get; set; }

        public string ProductVersion { get; set; } = string.Empty;

        public string ProfileId { get; set; } = "campaign-profile";

        public Func<DateTime> UtcNow { get; set; }

        public bool EnableProfileWrite { get; set; }

        public bool AllowLegacyImport { get; set; } = true;

        public string LegacyCampaignSourceKey { get; set; }

        public string LegacyActiveSlotKey { get; set; }

        public CampaignLegacyImportMarkerStore LegacyImportMarkerStore { get; set; }

        public bool CreateCompatibilityAdapter { get; set; }
    }

    public sealed class CampaignSaveServiceFactoryResult
    {
        internal CampaignSaveServiceFactoryResult(
            ISavePathProvider pathProvider,
            IAtomicTextFileStore textFileStore,
            FileCampaignProfileRepository repository,
            LegacyPlayerPrefsCampaignImporter legacyImporter,
            CampaignSaveMigrationCoordinator coordinator,
            CampaignSaveService service,
            SaveSlotStoreCompatibilityAdapter compatibilityAdapter,
            CampaignSaveMigrationOptions migrationOptions)
        {
            PathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            TextFileStore = textFileStore ?? throw new ArgumentNullException(nameof(textFileStore));
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            LegacyImporter = legacyImporter ?? throw new ArgumentNullException(nameof(legacyImporter));
            Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            Service = service ?? throw new ArgumentNullException(nameof(service));
            CompatibilityAdapter = compatibilityAdapter;
            MigrationOptions = migrationOptions ?? throw new ArgumentNullException(nameof(migrationOptions));
        }

        public ISavePathProvider PathProvider { get; }

        public IAtomicTextFileStore TextFileStore { get; }

        public FileCampaignProfileRepository Repository { get; }

        public LegacyPlayerPrefsCampaignImporter LegacyImporter { get; }

        public CampaignSaveMigrationCoordinator Coordinator { get; }

        public CampaignSaveService Service { get; }

        public SaveSlotStoreCompatibilityAdapter CompatibilityAdapter { get; }

        public CampaignSaveMigrationOptions MigrationOptions { get; }
    }

    public static class CampaignSaveServiceFactory
    {
        public static CampaignSaveServiceFactoryResult CreateForTests(
            CampaignSaveServiceFactoryOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var pathProvider = options.PathProvider ??
                               throw new ArgumentException(
                                   "A test save path provider is required.",
                                   nameof(options));
            var utcNow = options.UtcNow ?? (() => DateTime.UtcNow);
            string UtcNowString()
            {
                return utcNow().ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            }

            var textFileStore = new AtomicTextFileStore(pathProvider.SaveRootPath);
            var repository = new FileCampaignProfileRepository(textFileStore);
            var markerStore = options.LegacyImportMarkerStore ?? new CampaignLegacyImportMarkerStore();
            var legacyImporter = new LegacyPlayerPrefsCampaignImporter(
                new CampaignLegacySourceReader(
                    options.LegacyCampaignSourceKey ?? CampaignLegacySourceReader.CampaignSourceKey,
                    options.LegacyActiveSlotKey ?? CampaignLegacySourceReader.ActiveSlotKey),
                markerStore,
                UtcNowString,
                options.ProfileId,
                options.ProductVersion);
            var legacySource = options.AllowLegacyImport
                ? (ICampaignLegacyImportCandidateSource)legacyImporter
                : DisabledLegacyImportCandidateSource.Instance;
            var migrationOptions = new CampaignSaveMigrationOptions
            {
                EnableProfileWrite = options.EnableProfileWrite,
            };
            var coordinator = new CampaignSaveMigrationCoordinator(
                repository,
                legacySource,
                markerStore,
                migrationOptions);
            var service = new CampaignSaveService(
                repository,
                new CampaignLegacyImportResetMarkerPort(markerStore),
                UtcNowString,
                options.ProfileId,
                options.ProductVersion,
                new CampaignLegacyDeletedSlotGuardMarkerPort(markerStore));
            var adapter = options.CreateCompatibilityAdapter
                ? new SaveSlotStoreCompatibilityAdapter(service)
                : null;

            return new CampaignSaveServiceFactoryResult(
                pathProvider,
                textFileStore,
                repository,
                legacyImporter,
                coordinator,
                service,
                adapter,
                migrationOptions);
        }

        private sealed class DisabledLegacyImportCandidateSource : ICampaignLegacyImportCandidateSource
        {
            public static readonly DisabledLegacyImportCandidateSource Instance =
                new DisabledLegacyImportCandidateSource();

            public CampaignLegacyImportResult BuildImportCandidate()
            {
                return new CampaignLegacyImportResult(
                    CampaignLegacyImportStatus.ImportDisabled,
                    null,
                    string.Empty,
                    "Legacy campaign import is disabled by factory options.",
                    sourceFound: false,
                    importDisabled: true);
            }
        }
    }

    public static class CampaignSaveProductionReadinessPolicy
    {
        public static CampaignSaveProductionReadinessResult EvaluateDeleteSlotProductionReadiness()
        {
            return new CampaignSaveProductionReadinessResult(
                false,
                "DeleteSlot has deleted-slot guard coverage, but production integration remains deferred until SaveSlotStore call-site migration, profile writes, and adapter wiring are explicitly switched.");
        }
    }

    public readonly struct CampaignSaveProductionReadinessResult
    {
        public CampaignSaveProductionReadinessResult(bool isReady, string reason)
        {
            IsReady = isReady;
            Reason = reason ?? string.Empty;
        }

        public bool IsReady { get; }

        public string Reason { get; }
    }
}
