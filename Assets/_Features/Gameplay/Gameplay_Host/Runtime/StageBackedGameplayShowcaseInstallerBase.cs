using Game.Feature.Flow.Audio;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class StageBackedGameplayShowcaseInstallerBase : GameplayShowcaseSceneInstallerBase
    {
        private const string StageBackgroundRootObjectName = "StageBackgroundRoot";

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
        private CampaignChanceDisplayOverride _campaignChanceDisplayOverride;
        private CampaignGameplayFlowController _campaignFlowController;
        private bool _campaignRuntimeActive;
        private StagePresentationDefinition _resolvedPresentationDefinition;
        private SaveSlotStore _saveSlotStore;
        private readonly StagePresentationRuntimeAdapter _stagePresentationRuntimeAdapter = new();
        private BackgroundWallSurfaceTintPresenterAdapter _backgroundWallSurfaceTintPresenterAdapter;

        protected ScriptableObjectStageCatalogProvider StageCatalogProvider => stageCatalogProvider;

        internal bool CampaignRuntimeActive => _campaignRuntimeActive;

        internal bool HasCampaignFlowController => _campaignFlowController != null;

        protected sealed override InitialGameplayState BuildInitialGameplayState()
        {
            var resolved = RuntimeContentResolver.Resolve(CreateStageLoadRequest());
            var buildResult = StageRuntimeBuilder.Build(resolved.Entry.GameplayDefinition);
            var resolvedPresentation = StagePresentationAssembler.Resolve(
                resolved.Entry.GameplayDefinition,
                resolved.Entry.PresentationDefinition);
            _resolvedPresentationDefinition = resolved.Entry.PresentationDefinition;
            var compositionData = StageSceneCompositionAssembler.Compose(buildResult, resolvedPresentation);

            return new InitialGameplayState(
                compositionData.GameplayBuildResult.BoardBounds,
                compositionData.GameplayBuildResult.InitialTopology,
                compositionData.GameplayBuildResult.InitialEntities,
                compositionData.GameplayBuildResult.InitialTerrain,
                compositionData.GameplayBuildResult.InitialTileFeatures,
                compositionData.GameplayBuildResult.TileFeatureDefinitions,
                compositionData.GameplayBuildResult.MoonBlockRespawnDefinitions,
                compositionData.GameplayBuildResult.PlayerEntityId,
                compositionData.GameplayBuildResult.ObjectiveRuntimeDefinition,
                compositionData.GameplayBuildResult.EnemyAiProfileOverrides,
                resolved.Entry,
                resolved.Entry.GameplayDefinition.EnemyUnitArchetypeCatalog,
                compositionData.PresentationData.EnemyPresentationCatalog,
                compositionData.PresentationData.EnemyPresentationArchetypeCatalog,
                compositionData.PresentationData.EnemyPresentationBindings,
                compositionData.PresentationData.StaticEntityPresentationCatalog,
                compositionData.PresentationData.StaticEntityPresentationBindings,
                compositionData.PresentationData.BoardPresentationProfile,
                compositionData.PresentationData.BoardTilePresentationCatalog,
                compositionData.PresentationData.BoardTileStyleCatalog,
                compositionData.PresentationData.BoardTileOverlayCatalog,
                compositionData.PresentationData.TileFeatureBindings,
                compositionData.PresentationData.WorldGuideCatalog,
                compositionData.PresentationData.WorldGuideInstructions,
                compositionData.PresentationData.BoardTilePresentationOverrides,
                compositionData.PresentationData.BoardTilePaintOverrides,
                compositionData.PresentationData.BoardTileOverlayOverrides,
                compositionData.PresentationData.SuppressedBaseTileCells);
        }

        protected override void ConfigureRuntimeConfiguration(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            EnsureCampaignStores();
            var directPlayContext = EditorDirectPlayContextStore.GetCurrentOrNone();
            var activation = CampaignRuntimeActivationPolicy.Evaluate(enableCampaignFlow, _activeSlotProvider);
            _campaignRuntimeActive = activation.IsActive;
            if (!_campaignRuntimeActive)
            {
                _campaignChanceDisplayOverride = null;
                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.Installer)
                {
                    SceneName = gameObject.scene.name,
                    LaunchStageId = StageLaunchContextStore.CurrentStageId.IsValid
                        ? StageLaunchContextStore.CurrentStageId.Value
                        : string.Empty,
                    ResolvedStageId = initialState.StageContentEntry != null && initialState.StageContentEntry.StageId.IsValid
                        ? initialState.StageContentEntry.StageId.Value
                        : string.Empty,
                    EditorDirectPlayMode = directPlayContext.Mode,
                    SuppressCampaignFlow = directPlayContext.SuppressCampaignFlow,
                    HasCustomSaveNamespace = directPlayContext.HasCustomSaveNamespace,
                    EnableCampaignFlow = activation.EnableCampaignFlow,
                    CampaignRuntimeActive = false,
                    HasActiveSlot = activation.HasActiveSlot,
                    ActiveSlotNumber = _activeSlotProvider != null && _activeSlotProvider.TryGetActiveSlotNumber(out var inactiveSlotNumber)
                        ? inactiveSlotNumber
                        : 0,
                    SaveSlotStoreKey = _saveSlotStore != null ? _saveSlotStore.PlayerPrefsKey : string.Empty,
                    ActiveSlotProviderKey = _activeSlotProvider != null ? _activeSlotProvider.PlayerPrefsKey : string.Empty,
                    SourceIsNull = true,
                    FailureReason = !activation.HasActiveSlot
                        ? CampaignChanceReadFailureReason.NoActiveSlot
                        : directPlayContext.SuppressCampaignFlow
                            ? CampaignChanceReadFailureReason.EditorDirectPlaySuppressed
                            : CampaignChanceReadFailureReason.SourceMissing,
                });
                return;
            }

            ValidateActiveSlotMatchesLaunchStage(initialState.StageContentEntry != null
                ? initialState.StageContentEntry.StageId
                : StageId.None);
            _campaignChanceDisplayOverride = new CampaignChanceDisplayOverride();
            configuration.DisablePlayerRespawn = true;
            configuration.CampaignChancesReadSource = new SaveSlotCampaignChancesReadSource(
                _saveSlotStore,
                _activeSlotProvider,
                _campaignChanceDisplayOverride);
            configuration.StageCompletionProfileStore = new SaveSlotStageCompletionProfileStore(
                _saveSlotStore,
                _activeSlotProvider);
            CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.Installer)
            {
                SceneName = gameObject.scene.name,
                LaunchStageId = StageLaunchContextStore.CurrentStageId.IsValid
                    ? StageLaunchContextStore.CurrentStageId.Value
                    : string.Empty,
                ResolvedStageId = initialState.StageContentEntry != null && initialState.StageContentEntry.StageId.IsValid
                    ? initialState.StageContentEntry.StageId.Value
                    : string.Empty,
                EditorDirectPlayMode = directPlayContext.Mode,
                SuppressCampaignFlow = directPlayContext.SuppressCampaignFlow,
                HasCustomSaveNamespace = directPlayContext.HasCustomSaveNamespace,
                EnableCampaignFlow = activation.EnableCampaignFlow,
                CampaignRuntimeActive = true,
                HasActiveSlot = activation.HasActiveSlot,
                ActiveSlotNumber = _activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber)
                    ? activeSlotNumber
                    : 0,
                SaveSlotStoreKey = _saveSlotStore.PlayerPrefsKey,
                ActiveSlotProviderKey = _activeSlotProvider.PlayerPrefsKey,
                SourceType = configuration.CampaignChancesReadSource.GetType().Name,
                SourceIsNull = false,
            });
        }

        protected override void ConfigureObjectiveRuntimeDefinition(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            var gameplayDefinition = initialState.StageContentEntry != null
                ? initialState.StageContentEntry.GameplayDefinition
                : null;
            if (gameplayDefinition == null)
            {
                return;
            }

            var timing = StageSimulationTiming.FromTicksPerSecond(configuration.SimulationTicksPerSecond);
            configuration.ObjectiveRuntimeDefinition =
                StageRuntimeBuilder.Build(gameplayDefinition, timing).ObjectiveRuntimeDefinition;
        }

        protected override void OnHostInitialized(
            GameplaySceneHost host,
            in InitialGameplayState initialState)
        {
            _stagePresentationRuntimeAdapter.Apply(
                _resolvedPresentationDefinition,
                ResolveStageBackgroundRoot(),
                stageBgmProfileCatalog,
                globalAudioFlowBootstrap);
            AttachBackgroundWallSurfaceTintPresenter(host);

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
                CreateStageLaunchRouter(gameObject, gameObject.scene.name),
                _campaignChanceDisplayOverride);
            _campaignFlowController.Bind();
        }

        private static IStageLaunchRouter CreateStageLaunchRouter(
            GameObject owner,
            string currentSceneName)
        {
            if (owner != null)
            {
                var behaviours = owner.GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IStageLaunchRouterProvider provider &&
                        provider.TryCreateStageLaunchRouter(currentSceneName, out var router) &&
                        router != null)
                    {
                        return router;
                    }
                }
            }

            return new SceneNameStageLaunchRouter(currentSceneName);
        }

        private void OnDestroy()
        {
            _backgroundWallSurfaceTintPresenterAdapter?.Dispose();
            _backgroundWallSurfaceTintPresenterAdapter = null;
            _campaignFlowController?.Dispose();
        }

        private void AttachBackgroundWallSurfaceTintPresenter(GameplaySceneHost host)
        {
            _backgroundWallSurfaceTintPresenterAdapter?.Dispose();
            _backgroundWallSurfaceTintPresenterAdapter = null;

            var backgroundInstance = _stagePresentationRuntimeAdapter.CurrentBackgroundInstance;
            if (host == null || host.Presenter == null || backgroundInstance == null)
            {
                return;
            }

            var authoring = backgroundInstance.GetComponent<BackgroundWallSurfaceTintAuthoring>();
            if (authoring == null)
            {
                return;
            }

            _backgroundWallSurfaceTintPresenterAdapter =
                new BackgroundWallSurfaceTintPresenterAdapter(authoring, host.Presenter);
        }

        private StageLoadRequest CreateStageLoadRequest()
        {
            return StageLoadRequest.CreateLaunchContextOnly(
                stageCatalogProvider,
                gameObject.scene.name);
        }

        private Transform ResolveStageBackgroundRoot()
        {
            if (stageBackgroundRoot != null)
            {
                return stageBackgroundRoot;
            }

            var existingRoot = transform.Find(StageBackgroundRootObjectName);
            if (existingRoot != null)
            {
                stageBackgroundRoot = existingRoot;
                return stageBackgroundRoot;
            }

            var rootObject = new GameObject(StageBackgroundRootObjectName);
            stageBackgroundRoot = rootObject.transform;
            stageBackgroundRoot.SetParent(transform, worldPositionStays: false);
            stageBackgroundRoot.localPosition = Vector3.zero;
            stageBackgroundRoot.localRotation = Quaternion.identity;
            stageBackgroundRoot.localScale = Vector3.one;
            return stageBackgroundRoot;
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
