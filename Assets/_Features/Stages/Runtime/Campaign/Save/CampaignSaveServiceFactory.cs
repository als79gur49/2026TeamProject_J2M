using System;
using System.Globalization;

namespace Game.Feature.Stages
{
    public sealed class CampaignSaveServiceFactoryOptions
    {
        public ISavePathProvider PathProvider { get; set; }

        public string ProductVersion { get; set; } = string.Empty;

        public string ProfileId { get; set; } = "campaign-profile";

        public Func<DateTime> UtcNow { get; set; }

        public bool EnableProfileWrite { get; set; }

        public bool AllowLegacyImport { get; set; } = true;

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
            var markerStore = new CampaignLegacyImportMarkerStore();
            var legacyImporter = new LegacyPlayerPrefsCampaignImporter(
                new CampaignLegacySourceReader(),
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
                "DeleteSlot production integration is blocked until a slot-level tombstone or source-hash remigration guard exists.");
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
