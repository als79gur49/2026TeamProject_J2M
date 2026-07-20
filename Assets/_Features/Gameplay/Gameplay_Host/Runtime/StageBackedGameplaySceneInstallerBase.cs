using Game.Feature.Flow.Audio;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class StageBackedGameplaySceneInstallerBase : GameplayShowcaseSceneInstallerBase, IDemoStageControlGameplayContextProvider
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
        [SerializeField] private GlobalAudioFlowBootstrap globalAudioFlowBootstrap;

        private ActiveSlotProvider _activeSlotProvider;
        private CampaignChanceDisplayOverride _campaignChanceDisplayOverride;
        private CampaignGameplayFlowController _campaignFlowController;
        private bool _campaignRuntimeActive;
        private CampaignRunningSlotContext _runningSlotContext;
        private StagePresentationDefinition _resolvedPresentationDefinition;
        private ICampaignSaveSlotStore _saveSlotStore;
        private StageAudioResolvedData _resolvedAudioData = StageAudioAssembler.EmptyResolvedData;
        private readonly StageVisualRuntimeAdapter _stageVisualRuntimeAdapter = new();
        private readonly StageAudioRuntimeRequestSource _stageAudioRuntimeRequestSource = new();
        private BackgroundWallSurfaceTintPresenterAdapter _backgroundWallSurfaceTintPresenterAdapter;

        protected ScriptableObjectStageCatalogProvider StageCatalogProvider => stageCatalogProvider;

        internal bool CampaignRuntimeActive => _campaignRuntimeActive;

        internal bool HasCampaignFlowController => _campaignFlowController != null;

        public bool TryCreateDemoStageControlContext(out DemoStageControlGameplayContext context)
        {
            EnsureCampaignStores();
            if (stageCatalogProvider == null || _saveSlotStore == null || _activeSlotProvider == null)
            {
                context = default;
                return false;
            }

            var sequenceDefinition = campaignStageSequenceDefinition != null
                ? campaignStageSequenceDefinition
                : CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
            var sequenceResolver = new CampaignStageSequenceResolver(sequenceDefinition);
            context = new DemoStageControlGameplayContext(
                stageCatalogProvider,
                new DemoStageControlCampaignBridge(
                    _saveSlotStore,
                    _activeSlotProvider,
                    sequenceResolver),
                sequenceResolver);
            return true;
        }

        protected sealed override InitialGameplayState BuildInitialGameplayState()
        {
            var resolved = RuntimeContentResolver.Resolve(CreateStageLoadRequest());
            var buildResult = StageRuntimeBuilder.Build(resolved.Entry.GameplayDefinition);
            var resolvedPresentation = StagePresentationAssembler.Resolve(
                resolved.Entry.GameplayDefinition,
                resolved.Entry.PresentationDefinition);
            var resolvedAudio = StageAudioAssembler.Resolve(resolved.Entry.AudioDefinition);
            _resolvedPresentationDefinition = resolved.Entry.PresentationDefinition;
            _resolvedAudioData = resolvedAudio;
            var compositionData = StageSceneCompositionAssembler.Compose(buildResult, resolvedPresentation, resolvedAudio);

            return new InitialGameplayState(
                compositionData.GameplayBuildResult.BoardBounds,
                compositionData.GameplayBuildResult.InitialTopology,
                compositionData.GameplayBuildResult.InitialEntities,
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
                compositionData.PresentationData.TileFeatureBindings,
                compositionData.PresentationData.WorldGuideCatalog,
                compositionData.PresentationData.WorldGuideInstructions,
                compositionData.PresentationData.BoardTilePaintOverrides,
                compositionData.PresentationData.SuppressedBaseTileCells);
        }

        protected override void ConfigureRuntimeConfiguration(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            EnsureCampaignStores();
            var directPlayContext = EditorDirectPlayContextStore.GetCurrentOrNone();
            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            var canUseProductionHandoff = directPlayContext.Mode == EditorDirectPlayMode.None;
            CampaignLaunchHandoff pendingHandoff = null;
            var hasPendingLaunch =
                canUseProductionHandoff &&
                launchHandoffStore.TryPeek(out pendingHandoff);
            var hasActiveSlot = _activeSlotProvider != null && _activeSlotProvider.HasActiveSlot;
            var isSuppressed = directPlayContext.SuppressCampaignFlow;
            _campaignRuntimeActive =
                enableCampaignFlow &&
                !isSuppressed &&
                (hasPendingLaunch || hasActiveSlot);
            if (!_campaignRuntimeActive)
            {
                _campaignChanceDisplayOverride = null;
                _runningSlotContext = null;
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
                    EnableCampaignFlow = enableCampaignFlow,
                    CampaignRuntimeActive = false,
                    HasActiveSlot = hasActiveSlot,
                    ActiveSlotNumber = _activeSlotProvider != null && _activeSlotProvider.TryGetActiveSlotNumber(out var inactiveSlotNumber)
                        ? inactiveSlotNumber
                        : 0,
                    HasLaunchHandoff = hasPendingLaunch,
                    HandoffSlotNumber = hasPendingLaunch ? pendingHandoff.SlotNumber : 0,
                    HandoffToken = hasPendingLaunch ? pendingHandoff.Token.ToString("N") : string.Empty,
                    SaveSlotStoreKey = _saveSlotStore != null ? _saveSlotStore.DiagnosticsKey : string.Empty,
                    ActiveSlotProviderKey = _activeSlotProvider != null ? _activeSlotProvider.PlayerPrefsKey : string.Empty,
                    SourceIsNull = true,
                    FailureReason = !hasActiveSlot && !hasPendingLaunch
                        ? CampaignChanceReadFailureReason.NoActiveSlot
                        : isSuppressed
                            ? CampaignChanceReadFailureReason.EditorDirectPlaySuppressed
                            : CampaignChanceReadFailureReason.SourceMissing,
                });
                return;
            }

            var resolvedStageId = initialState.StageContentEntry != null
                ? initialState.StageContentEntry.StageId
                : StageId.None;
            _runningSlotContext = ResolveRunningSlotContext(
                resolvedStageId,
                directPlayContext,
                hasPendingLaunch ? pendingHandoff : null);
            _campaignChanceDisplayOverride = new CampaignChanceDisplayOverride();
            configuration.DisablePlayerRespawn = true;
            configuration.CampaignChancesReadSource = new SaveSlotCampaignChancesReadSource(
                _saveSlotStore,
                _runningSlotContext,
                _campaignChanceDisplayOverride);
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
                EnableCampaignFlow = enableCampaignFlow,
                CampaignRuntimeActive = true,
                HasActiveSlot = _activeSlotProvider.HasActiveSlot,
                ActiveSlotNumber = _runningSlotContext.SlotNumber,
                HasLaunchHandoff = hasPendingLaunch,
                HandoffSlotNumber = hasPendingLaunch ? pendingHandoff.SlotNumber : 0,
                HandoffToken = hasPendingLaunch ? pendingHandoff.Token.ToString("N") : string.Empty,
                SaveSlotStoreKey = _saveSlotStore.DiagnosticsKey,
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
            _stageVisualRuntimeAdapter.Apply(
                _resolvedPresentationDefinition,
                ResolveStageBackgroundRoot());
            _stageAudioRuntimeRequestSource.Apply(
                _resolvedAudioData,
                globalAudioFlowBootstrap.GetRequestRouterOrThrow());
            AttachBackgroundWallSurfaceTintPresenter(host);

            if (!_campaignRuntimeActive)
            {
                return;
            }

            if (_runningSlotContext == null)
            {
                throw new System.InvalidOperationException("Campaign runtime requires a running slot context.");
            }

            var sequenceDefinition = campaignStageSequenceDefinition != null
                ? campaignStageSequenceDefinition
                : CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
            var sequenceResolver = new CampaignStageSequenceResolver(sequenceDefinition);
            _campaignFlowController = new CampaignGameplayFlowController(
                host,
                _saveSlotStore,
                _runningSlotContext,
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

            var backgroundInstance = _stageVisualRuntimeAdapter.CurrentBackgroundInstance;
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
                _saveSlotStore ??= new SaveSlotStore(
                    directPlayContext.SaveSlotStoreKey,
                    directPlayContext.ActiveSlotProviderKey);
                _activeSlotProvider ??= new ActiveSlotProvider(directPlayContext.ActiveSlotProviderKey);
                return;
            }

            _saveSlotStore ??= CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            _activeSlotProvider ??= CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(_saveSlotStore);
        }

        private CampaignRunningSlotContext ResolveRunningSlotContext(
            StageId resolvedStageId,
            EditorDirectPlayContext directPlayContext,
            CampaignLaunchHandoff pendingHandoff)
        {
            if (directPlayContext.Mode != EditorDirectPlayMode.None || pendingHandoff == null)
            {
                return new CampaignRunningSlotContext(
                    ValidateCommittedActiveSlotMatchesLaunchStage(resolvedStageId));
            }

            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            var hadPreviousActiveSlot =
                _activeSlotProvider.TryGetActiveSlotNumber(out var previousActiveSlotNumber);
            var activeSlotCommitted = false;
            try
            {
                ValidateLaunchStageIds(pendingHandoff.StageId, resolvedStageId);
                var slot = LoadNonEmptySlot(pendingHandoff.SlotNumber);
                if (!slot.CurrentStageId.Equals(pendingHandoff.StageId))
                {
                    throw new System.InvalidOperationException(
                        $"Campaign pending slot stage '{slot.CurrentStageId.Value}' does not match handoff stage '{pendingHandoff.StageId.Value}'.");
                }

                _activeSlotProvider.SetActiveSlot(pendingHandoff.SlotNumber);
                activeSlotCommitted = true;
                var runningContext = new CampaignRunningSlotContext(pendingHandoff.SlotNumber);
                if (!launchHandoffStore.TryConsume(pendingHandoff.Token, out var consumedHandoff) ||
                    !object.ReferenceEquals(consumedHandoff, pendingHandoff))
                {
                    throw new System.InvalidOperationException(
                        "Campaign launch handoff changed before gameplay installer commit completed.");
                }

                return runningContext;
            }
            catch
            {
                launchHandoffStore.TryClear(pendingHandoff.Token);
                if (activeSlotCommitted)
                {
                    if (hadPreviousActiveSlot)
                    {
                        _activeSlotProvider.SetActiveSlot(previousActiveSlotNumber);
                    }
                    else
                    {
                        _activeSlotProvider.ClearActiveSlot();
                    }
                }

                throw;
            }
        }

        private int ValidateCommittedActiveSlotMatchesLaunchStage(StageId resolvedStageId)
        {
            if (_activeSlotProvider == null ||
                !_activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber))
            {
                throw new System.InvalidOperationException(
                    "Campaign runtime requires a committed active slot when no pending handoff exists.");
            }

            ValidateLaunchStageIds(resolvedStageId, resolvedStageId);
            var activeSlot = LoadNonEmptySlot(activeSlotNumber);
            if (!activeSlot.CurrentStageId.Equals(resolvedStageId))
            {
                throw new System.InvalidOperationException(
                    $"Campaign active slot stage '{activeSlot.CurrentStageId.Value}' does not match resolved launch stage '{resolvedStageId.Value}'.");
            }

            return activeSlotNumber;
        }

        private static void ValidateLaunchStageIds(
            StageId requestedStageId,
            StageId resolvedStageId)
        {
            var launchStageId = StageLaunchContextStore.CurrentStageId;
            if (!launchStageId.IsValid)
            {
                throw new System.InvalidOperationException("Campaign runtime requires a valid launch StageId.");
            }

            if (!requestedStageId.IsValid || !resolvedStageId.IsValid)
            {
                throw new System.InvalidOperationException(
                    "Campaign runtime requires valid requested and resolved StageIds.");
            }

            if (!requestedStageId.Equals(launchStageId) ||
                !requestedStageId.Equals(resolvedStageId))
            {
                throw new System.InvalidOperationException(
                    $"Campaign launch StageIds do not match. requested='{requestedStageId.Value}', context='{launchStageId.Value}', resolved='{resolvedStageId.Value}'.");
            }
        }

        private SaveSlotData LoadNonEmptySlot(int slotNumber)
        {
            var slot = _saveSlotStore.LoadSlot(slotNumber);
            if (slot == null || slot.IsEmpty || !slot.CurrentStageId.IsValid)
            {
                throw new System.InvalidOperationException(
                    $"Campaign slot '{slotNumber}' is empty or missing.");
            }

            return slot;
        }
    }
}
