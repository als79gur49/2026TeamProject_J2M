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

        public static ICampaignSaveSlotStore CreateProductionLegacyRollback()
        {
            return Create(CreateProductionLegacyRollbackOptions());
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
                : CreateProductionLocalStateActiveSlotStorage(
                    profileSlots,
                    CreateProductionProfileBackedOptions().PathProvider);
            return new ActiveSlotProvider(storage);
        }

        internal static CampaignSaveCompositionOptions CreateProductionProfileBackedOptions()
        {
            return new CampaignSaveCompositionOptions
            {
                BackendMode = CampaignSaveBackendMode.ProfileJsonExplicit,
                EnableProfileWrite = true,
                AllowLegacyImport = true,
                PreservePlayerPrefsSource = true,
                PathProvider = new ApplicationPersistentDataSavePathProvider(),
            };
        }

        internal static CampaignSaveCompositionOptions CreateProductionLegacyRollbackOptions()
        {
            return new CampaignSaveCompositionOptions
            {
                BackendMode = CampaignSaveBackendMode.PlayerPrefsLegacy,
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
            var activeSlotStorage = CreateProductionLocalStateActiveSlotStorage(
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

        private static IActiveSlotStorage CreateProductionLocalStateActiveSlotStorage(
            ICampaignSaveSlotStore profileSlots,
            ISavePathProvider pathProvider)
        {
            pathProvider ??= new ApplicationPersistentDataSavePathProvider();
            return new LocalStateActiveSlotStorage(
                new FileCampaignLocalLaunchStateRepository(
                    new AtomicTextFileStore(pathProvider.SaveRootPath)),
                new PlayerPrefsActiveSlotStorage(SaveSlotPrefsKeys.ActiveSaveSlotKey),
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
