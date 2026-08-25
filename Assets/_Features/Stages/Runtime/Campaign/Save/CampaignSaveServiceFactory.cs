using System;
using System.Globalization;

namespace Game.Feature.Stages
{
    internal sealed class CampaignSaveCompositionOptions
    {
        public ISavePathProvider PathProvider { get; set; }

        internal IAtomicTextFileStore TextFileStore { get; set; }

        public string ProductVersion { get; set; } = string.Empty;

        public string ProfileId { get; set; } = "campaign-profile";

        public Func<DateTime> UtcNow { get; set; }
    }

    internal sealed class CampaignSaveFacadeFactoryResult
    {
        internal CampaignSaveFacadeFactoryResult(
            ICampaignSaveRuntime campaignSaveSlots,
            CampaignSaveServiceFactoryResult profileServices,
            CampaignSaveResetResult recoveryResumeResult)
        {
            CampaignSaveSlots = campaignSaveSlots ??
                                throw new ArgumentNullException(nameof(campaignSaveSlots));
            ProfileServices = profileServices ??
                              throw new ArgumentNullException(nameof(profileServices));
            RecoveryResumeResult = recoveryResumeResult;
        }

        public ICampaignSaveRuntime CampaignSaveSlots { get; }

        internal CampaignSaveServiceFactoryResult ProfileServices { get; }

        public CampaignSaveResetResult RecoveryResumeResult { get; }
    }

    internal static class CampaignSaveFacadeFactory
    {
        internal static CampaignSaveFacadeFactoryResult Create(
            CampaignSaveCompositionOptions options = null)
        {
            options ??= new CampaignSaveCompositionOptions();
            var profileServices = CampaignSaveServiceFactory.Create(
                new CampaignSaveServiceFactoryOptions
                {
                    PathProvider = options.PathProvider,
                    TextFileStore = options.TextFileStore,
                    ProductVersion = options.ProductVersion,
                    ProfileId = options.ProfileId,
                    UtcNow = options.UtcNow,
                });
            var recoveryResumeResult = profileServices.Recovery.RetryPendingReset();
            return new CampaignSaveFacadeFactoryResult(
                profileServices.SlotStore,
                profileServices,
                recoveryResumeResult);
        }
    }

    internal sealed class CampaignSaveServiceFactoryOptions
    {
        public ISavePathProvider PathProvider { get; set; }

        internal IAtomicTextFileStore TextFileStore { get; set; }

        public string ProductVersion { get; set; } = string.Empty;

        public string ProfileId { get; set; } = "campaign-profile";

        public Func<DateTime> UtcNow { get; set; }

    }

    internal sealed class CampaignSaveServiceFactoryResult
    {
        internal CampaignSaveServiceFactoryResult(
            ISavePathProvider pathProvider,
            IAtomicTextFileStore textFileStore,
            FileCampaignProfileRepository repository,
            CampaignSaveService service,
            CampaignSaveRecoveryService recovery,
            CampaignSaveSlotStoreAdapter slotStore)
        {
            PathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            TextFileStore = textFileStore ?? throw new ArgumentNullException(nameof(textFileStore));
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Service = service ?? throw new ArgumentNullException(nameof(service));
            Recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            SlotStore = slotStore ?? throw new ArgumentNullException(nameof(slotStore));
        }

        public ISavePathProvider PathProvider { get; }

        public IAtomicTextFileStore TextFileStore { get; }

        public FileCampaignProfileRepository Repository { get; }

        public CampaignSaveService Service { get; }

        public CampaignSaveRecoveryService Recovery { get; }

        public CampaignSaveSlotStoreAdapter SlotStore { get; }
    }

    internal static class CampaignSaveServiceFactory
    {
        internal static CampaignSaveServiceFactoryResult Create(
            CampaignSaveServiceFactoryOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var pathProvider = options.PathProvider ??
                               throw new ArgumentException(
                                   "A save path provider is required.",
                                   nameof(options));
            var utcNow = options.UtcNow ?? (() => DateTime.UtcNow);
            string UtcNowString()
            {
                return utcNow().ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            }

            var textFileStore = options.TextFileStore ??
                                new AtomicTextFileStore(pathProvider.SaveRootPath);
            var repository = new FileCampaignProfileRepository(textFileStore);
            var service = new CampaignSaveService(
                repository,
                UtcNowString,
                options.ProfileId,
                options.ProductVersion);
            var recovery = new CampaignSaveRecoveryService(
                repository,
                textFileStore,
                utcNow,
                options.ProfileId,
                options.ProductVersion);
            var slotStore = new CampaignSaveSlotStoreAdapter(service, recovery);

            return new CampaignSaveServiceFactoryResult(
                pathProvider,
                textFileStore,
                repository,
                service,
                recovery,
                slotStore);
        }
    }
}
