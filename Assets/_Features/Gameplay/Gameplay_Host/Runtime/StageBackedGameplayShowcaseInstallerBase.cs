using Game.Feature.Flow.Audio;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class StageBackedGameplayShowcaseInstallerBase : GameplayShowcaseSceneInstallerBase
    {
        private static readonly StageRuntimeContentResolver RuntimeContentResolver = new();

        [Header("Stage Catalog")]
        [SerializeField] private ScriptableObjectStageCatalogProvider stageCatalogProvider;

        [Header("Campaign Flow")]
        [SerializeField] private CampaignStageSequenceDefinition campaignStageSequenceDefinition;
        [SerializeField] private bool enableCampaignFlow = true;

        [Header("Stage Presentation Runtime")]
        [SerializeField] private Transform stageBackgroundRoot;

        [Header("Persistent BGM Flow")]
        [SerializeField] private StageBgmProfileCatalog stageBgmProfileCatalog;
        [SerializeField] private GlobalAudioFlowBootstrap globalAudioFlowBootstrap;

        private ActiveSlotProvider _activeSlotProvider;
        private CampaignGameplayFlowController _campaignFlowController;
        private bool _campaignRuntimeActive;
        private StagePresentationDefinition _resolvedPresentationDefinition;
        private SaveSlotStore _saveSlotStore;
        private readonly StagePresentationRuntimeAdapter _stagePresentationRuntimeAdapter = new();

        protected ScriptableObjectStageCatalogProvider StageCatalogProvider => stageCatalogProvider;

        internal bool CampaignRuntimeActive => _campaignRuntimeActive;

        internal bool HasCampaignFlowController => _campaignFlowController != null;

        protected sealed override InitialGameplayState BuildInitialGameplayState()
        {
            var resolved = RuntimeContentResolver.Resolve(CreateStageLoadRequest());
            var buildResult = StageRuntimeBuilder.Build(resolved.Entry.GameplayDefinition);
            var resolvedPresentation = StagePresentationAssembler.Resolve(resolved.Entry.PresentationDefinition);
            _resolvedPresentationDefinition = resolved.Entry.PresentationDefinition;
            var compositionData = StageSceneCompositionAssembler.Compose(buildResult, resolvedPresentation);

            return new InitialGameplayState(
                compositionData.GameplayBuildResult.BoardBounds,
                compositionData.GameplayBuildResult.InitialTopology,
                compositionData.GameplayBuildResult.InitialEntities,
                compositionData.GameplayBuildResult.InitialTerrain,
                compositionData.GameplayBuildResult.PlayerEntityId,
                compositionData.GameplayBuildResult.ObjectiveRuntimeDefinition,
                compositionData.GameplayBuildResult.EnemyAiProfileOverrides,
                resolved.Entry,
                resolved.Entry.GameplayDefinition.EnemyUnitArchetypeCatalog,
                compositionData.PresentationData.EnemyPresentationCatalog,
                compositionData.PresentationData.EnemyPresentationArchetypeCatalog,
                compositionData.PresentationData.EnemyPresentationBindings,
                compositionData.PresentationData.StaticEntityPresentationCatalog,
                compositionData.PresentationData.StaticEntityPresentationBindings);
        }

        protected override void ConfigureRuntimeConfiguration(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            EnsureCampaignStores();
            var activation = CampaignRuntimeActivationPolicy.Evaluate(enableCampaignFlow, _activeSlotProvider);
            _campaignRuntimeActive = activation.IsActive;
            if (!_campaignRuntimeActive)
            {
                return;
            }

            ValidateActiveSlotMatchesLaunchStage(initialState.StageContentEntry != null
                ? initialState.StageContentEntry.StageId
                : StageId.None);
            configuration.DisablePlayerRespawn = true;
            configuration.CampaignChancesReadSource = new SaveSlotCampaignChancesReadSource(
                _saveSlotStore,
                _activeSlotProvider);
            configuration.StageCompletionProfileStore = new SaveSlotStageCompletionProfileStore(
                _saveSlotStore,
                _activeSlotProvider);
        }

        protected override void OnHostInitialized(
            GameplaySceneHost host,
            in InitialGameplayState initialState)
        {
            _stagePresentationRuntimeAdapter.Apply(
                _resolvedPresentationDefinition,
                stageBackgroundRoot,
                stageBgmProfileCatalog,
                globalAudioFlowBootstrap);

            if (!_campaignRuntimeActive)
            {
                return;
            }

            var sequenceDefinition = campaignStageSequenceDefinition != null
                ? campaignStageSequenceDefinition
                : CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
            var sequenceResolver = new CampaignStageSequenceResolver(sequenceDefinition);
            _campaignFlowController = new CampaignGameplayFlowController(
                host,
                _saveSlotStore,
                _activeSlotProvider,
                sequenceResolver,
                new SceneNameStageLaunchRouter(gameObject.scene.name));
            _campaignFlowController.Bind();
        }

        private void OnDestroy()
        {
            _campaignFlowController?.Dispose();
        }

        private StageLoadRequest CreateStageLoadRequest()
        {
            return StageLoadRequest.CreateLaunchContextOnly(
                stageCatalogProvider,
                gameObject.scene.name);
        }

        private void EnsureCampaignStores()
        {
            var directPlayContext = EditorDirectPlayContextStore.GetCurrentOrNone();
            if (directPlayContext.HasCustomSaveNamespace)
            {
                _saveSlotStore ??= new SaveSlotStore(directPlayContext.SaveSlotStoreKey);
                _activeSlotProvider ??= new ActiveSlotProvider(directPlayContext.ActiveSlotProviderKey);
                return;
            }

            _saveSlotStore ??= new SaveSlotStore();
            _activeSlotProvider ??= new ActiveSlotProvider();
        }

        private void ValidateActiveSlotMatchesLaunchStage(StageId launchStageId)
        {
            if (!launchStageId.IsValid)
            {
                throw new System.InvalidOperationException("Campaign runtime requires a valid launch StageId.");
            }

            var activeSlotNumber = _activeSlotProvider.ActiveSlotNumber;
            var activeSlot = _saveSlotStore.LoadSlot(activeSlotNumber);
            if (!activeSlot.CurrentStageId.Equals(launchStageId))
            {
                throw new System.InvalidOperationException(
                    $"Campaign active slot stage '{activeSlot.CurrentStageId.Value}' does not match launch stage '{launchStageId.Value}'.");
            }
        }
    }
}
