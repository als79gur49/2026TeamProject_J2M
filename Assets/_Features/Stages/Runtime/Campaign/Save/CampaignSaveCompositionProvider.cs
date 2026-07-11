namespace Game.Feature.Stages
{
    public static class CampaignSaveCompositionProvider
    {
        private static ICampaignSaveSlotStore productionProfileBackedStore;

        public static ICampaignSaveSlotStore CreateProductionProfileBacked()
        {
            return productionProfileBackedStore ??= Create(CreateProductionProfileBackedOptions());
        }

        public static CampaignSaveFacadeFactoryResult CreateProductionProfileBackedFacade()
        {
            return CampaignSaveFacadeFactory.Create(CreateProductionProfileBackedOptions());
        }

        public static ICampaignSaveSlotStore CreateProductionLegacyRollback()
        {
            return Create(CreateProductionLegacyRollbackOptions());
        }

        public static ICampaignSaveSlotStore Create(CampaignSaveCompositionOptions options)
        {
            return CampaignSaveFacadeFactory.Create(options).CampaignSaveSlots;
        }

        public static CampaignSaveCompositionOptions CreateProductionProfileBackedOptions()
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

        public static CampaignSaveCompositionOptions CreateProductionLegacyRollbackOptions()
        {
            return new CampaignSaveCompositionOptions
            {
                BackendMode = CampaignSaveBackendMode.PlayerPrefsLegacy,
            };
        }

        public static void ResetProductionProfileBackedForTests()
        {
            productionProfileBackedStore = null;
        }
    }
}
