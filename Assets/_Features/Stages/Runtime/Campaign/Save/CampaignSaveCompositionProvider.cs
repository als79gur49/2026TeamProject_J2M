namespace Game.Feature.Stages
{
    public static class CampaignSaveCompositionProvider
    {
        private static ProductionComposition productionComposition;
        private static ProductionComposition productionCompositionOverride;

        public static ICampaignSaveSlotStore CreateProductionProfileBacked()
        {
            return GetOrCreateProductionComposition().SlotStore;
        }

        public static ICampaignSaveRecoveryPort GetProductionRecoveryPort()
        {
            return GetOrCreateProductionComposition().RecoveryPort;
        }

        internal static ICampaignSaveSlotStore Create(CampaignSaveCompositionOptions options)
        {
            return CampaignSaveFacadeFactory.Create(options).CampaignSaveSlots;
        }

        public static ActiveSlotProvider CreateProductionActiveSlotProvider(
            ICampaignSaveSlotStore profileSlots)
        {
            if (profileSlots == null)
            {
                throw new System.ArgumentNullException(nameof(profileSlots));
            }

            var production = GetOrCreateProductionComposition();
            var storage = ReferenceEquals(profileSlots, production.SlotStore)
                ? production.ActiveSlotStorage
                : CreateLocalStateActiveSlotStorage(
                    profileSlots,
                    CreateProductionProfileBackedOptions().PathProvider);
            return new ActiveSlotProvider(storage);
        }

        public static ICampaignSaveSlotStore CreateTemporaryProfileBacked()
        {
            return Create(CreateTemporaryProfileBackedOptions());
        }

        public static ActiveSlotProvider CreateTemporaryActiveSlotProvider(
            ICampaignSaveSlotStore profileSlots)
        {
            if (profileSlots == null)
            {
                throw new System.ArgumentNullException(nameof(profileSlots));
            }

            return new ActiveSlotProvider(
                CreateLocalStateActiveSlotStorage(
                    profileSlots,
                    CreateTemporaryProfileBackedOptions().PathProvider));
        }

        public static void ClearTemporaryCampaignState()
        {
            ClearTemporaryCampaignState(new TemporaryCampaignSavePathProvider());
        }

        internal static void ClearTemporaryCampaignState(ISavePathProvider pathProvider)
        {
            if (pathProvider == null)
            {
                throw new System.ArgumentNullException(nameof(pathProvider));
            }

            var textFileStore = new AtomicTextFileStore(pathProvider.SaveRootPath);
            textFileStore.DeleteActiveFileArtifacts(FileCampaignProfileRepository.ProfileFileName);
            textFileStore.DeleteActiveFileArtifacts(CampaignLocalLaunchStateRepository.FileName);
            textFileStore.DeleteActiveFileArtifacts(CampaignSaveRecoveryService.PendingResetFileName);
        }

        internal static CampaignSaveCompositionOptions CreateProductionProfileBackedOptions()
        {
            return new CampaignSaveCompositionOptions
            {
                PathProvider = new ApplicationPersistentDataSavePathProvider(),
            };
        }

        internal static CampaignSaveCompositionOptions CreateTemporaryProfileBackedOptions()
        {
            return new CampaignSaveCompositionOptions
            {
                PathProvider = new TemporaryCampaignSavePathProvider(),
                ProfileId = "direct-play-campaign-profile",
            };
        }

        internal static void ResetProductionProfileBackedForTests()
        {
            productionComposition = null;
            productionCompositionOverride = null;
        }

        internal static void SetProductionCompositionForTests(
            ICampaignSaveSlotStore slotStore,
            ICampaignSaveRecoveryPort recoveryPort,
            IActiveSlotStorage activeSlotStorage)
        {
            productionComposition = null;
            productionCompositionOverride = new ProductionComposition(
                slotStore ?? throw new System.ArgumentNullException(nameof(slotStore)),
                recoveryPort ?? throw new System.ArgumentNullException(nameof(recoveryPort)),
                activeSlotStorage ?? throw new System.ArgumentNullException(nameof(activeSlotStorage)));
        }

        private static ProductionComposition GetOrCreateProductionComposition()
        {
            if (productionCompositionOverride != null)
            {
                return productionCompositionOverride;
            }

            if (productionComposition != null)
            {
                return productionComposition;
            }

            var options = CreateProductionProfileBackedOptions();
            var facade = CampaignSaveFacadeFactory.Create(options);
            var activeSlotStorage = CreateLocalStateActiveSlotStorage(
                facade.CampaignSaveSlots,
                options.PathProvider);
            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            var slotStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                facade.CampaignSaveSlots,
                activeSlotStorage,
                launchHandoffStore);
            var recoveryPort = new CampaignLaunchStateRepairingCampaignSaveRecoveryPort(
                facade.ProfileServices.Recovery,
                activeSlotStorage,
                launchHandoffStore);
            if (facade.RecoveryResumeResult == CampaignSaveResetResult.Completed)
            {
                recoveryPort.ClearLaunchState();
            }

            productionComposition = new ProductionComposition(
                slotStore,
                recoveryPort,
                activeSlotStorage);
            return productionComposition;
        }

        private static IActiveSlotStorage CreateLocalStateActiveSlotStorage(
            ICampaignSaveSlotStore profileSlots,
            ISavePathProvider pathProvider)
        {
            pathProvider ??= new ApplicationPersistentDataSavePathProvider();
            return new LocalStateActiveSlotStorage(
                new FileCampaignLocalLaunchStateRepository(
                    new AtomicTextFileStore(pathProvider.SaveRootPath)),
                profileSlots);
        }

        private sealed class ProductionComposition
        {
            public ProductionComposition(
                ICampaignSaveSlotStore slotStore,
                ICampaignSaveRecoveryPort recoveryPort,
                IActiveSlotStorage activeSlotStorage)
            {
                SlotStore = slotStore;
                RecoveryPort = recoveryPort;
                ActiveSlotStorage = activeSlotStorage;
            }

            public ICampaignSaveSlotStore SlotStore { get; }

            public ICampaignSaveRecoveryPort RecoveryPort { get; }

            public IActiveSlotStorage ActiveSlotStorage { get; }
        }
    }
}
