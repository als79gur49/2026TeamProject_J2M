namespace Game.Feature.Stages
{
    public static class StageContentPaths
    {
        public const string StagesRoot = "Assets/_Features/Stages";
        public const string LegacyContentRoot = StagesRoot + "/Content";
        public const string CampaignRoot = LegacyContentRoot + "/Campaigns/campaign-main";
        public const string CampaignCatalogRoot = CampaignRoot + "/Catalog";
        public const string CampaignSharedRoot = CampaignRoot + "/_Shared";
        public const string CampaignLevelsRoot = CampaignRoot + "/Levels";
        public const string CampaignLevel01Root = CampaignLevelsRoot + "/level-01";
        public const string CampaignLevel01StagesRoot = CampaignLevel01Root + "/Stages";

        public const string CampaignMainAssetPath = CampaignRoot + "/CampaignMain.asset";
        public const string Level01AssetPath = CampaignLevel01Root + "/Level01.asset";
        public const string StageCatalogAssetPath = CampaignCatalogRoot + "/CampaignMain_StageCatalog.asset";
        public const string StageCatalogProviderAssetPath =
            CampaignCatalogRoot + "/CampaignMain_StageCatalogProvider.asset";
        public const string StageIdAliasTableAssetPath =
            CampaignCatalogRoot + "/CampaignMain_StageIdAliasTable.asset";
        public const string CampaignStageSequenceAssetPath =
            CampaignCatalogRoot + "/CampaignMain_StageSequence.asset";

        public const string SharedGameplayRoot = CampaignSharedRoot + "/Gameplay";
        public const string SharedPresentationRoot = CampaignSharedRoot + "/Presentation";
        public const string SharedAudioRoot = CampaignSharedRoot + "/Audio";

        public const string SharedEnemyAiRoot = SharedGameplayRoot + "/EnemyAI";
        public const string SharedConditionsRoot = SharedGameplayRoot + "/Conditions";
        public const string SharedEnemyPresentationRoot = SharedPresentationRoot + "/Enemy";
        public const string SharedStaticPresentationRoot = SharedPresentationRoot + "/Static";
        public const string SharedBoxGameplayRoot = SharedGameplayRoot + "/Boxes";
        public const string SharedBoxPresentationRoot = SharedPresentationRoot + "/Boxes";
        public const string SharedBoardPresentationRoot = SharedPresentationRoot + "/Board";
        public const string SharedTopologyPresentationRoot = SharedPresentationRoot + "/Topology";
        public const string SharedVfxPresentationRoot = SharedPresentationRoot + "/VFX";
    }
}
