namespace Game.Feature.Stages
{
    public static class CampaignSaveCompositionProvider
    {
        private static ICampaignSaveSlotStore productionProfileBackedStore;

        public static ICampaignSaveSlotStore CreateProductionProfileBacked()
        {
            if (productionProfileBackedStore != null)
            {
                return productionProfileBackedStore;
            }

            var options = CreateProductionProfileBackedOptions();
            var facade = CampaignSaveFacadeFactory.Create(options);
            var activeSlotStorage = CreateProductionLocalStateActiveSlotStorage(
                facade.CampaignSaveSlots,
                options.PathProvider);
            productionProfileBackedStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                facade.CampaignSaveSlots,
                activeSlotStorage,
                CampaignLaunchHandoffSessionStore.Instance);
            return productionProfileBackedStore;
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

            var storage = CreateProductionLocalStateActiveSlotStorage(
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
            productionProfileBackedStore = null;
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
    }
}
