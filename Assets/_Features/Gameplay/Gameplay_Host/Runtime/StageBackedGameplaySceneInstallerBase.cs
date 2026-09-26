using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Flow.Audio;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Stages;
using Game.Product.Achievements.CampaignIntegration;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class StageBackedGameplaySceneInstallerBase : GameplayShowcaseSceneInstallerBase,
        IDemoStageControlGameplayContextProvider,
        ICampaignStageSequenceResolverProvider
    {
        private const string StageBackgroundRootObjectName = "StageBackgroundRoot";
        private const string MissingCampaignStageSequenceDefinitionMessage =
            "StageBackedGameplaySceneInstallerBase requires the authoritative serialized " +
            "CampaignStageSequenceDefinition for the gameplay scene composition.";

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
        private SaveSlotCampaignChancesReadSource _campaignChancesReadSource;
        private CampaignGameplayFlowController _campaignFlowController;
        private CampaignStageSequenceResolver _campaignStageSequenceResolver;
        private bool _campaignRuntimeActive;
        private CampaignRunningSlotContext _runningSlotContext;
        private CampaignSlotState _preparedCampaignSlot;
        private EditorDirectPlayContext _runtimeDirectPlayContext = EditorDirectPlayContext.None;
        private StagePresentationDefinition _resolvedPresentationDefinition;
        private ICampaignSaveRuntime _saveSlotStore;
        private StageAudioResolvedData _resolvedAudioData = StageAudioAssembler.EmptyResolvedData;
        private readonly StageVisualRuntimeAdapter _stageVisualRuntimeAdapter = new();
        private readonly StageAudioRuntimeRequestSource _stageAudioRuntimeRequestSource = new();
        private BgmRequestLease _stageBgmRequestLease;
        private BackgroundWallSurfaceTintPresenterAdapter _backgroundWallSurfaceTintPresenterAdapter;
        private BackgroundSpaceOrbitPresenterAdapter _backgroundSpaceOrbitPresenterAdapter;
        private StageStaticWallPresentationProvenance _staticWallPresentationProvenance =
            StageStaticWallPresentationProvenance.Empty;

        protected ScriptableObjectStageCatalogProvider StageCatalogProvider => stageCatalogProvider;

        internal bool CampaignRuntimeActive => _campaignRuntimeActive;

        internal bool HasCampaignFlowController => _campaignFlowController != null;

        internal bool TerminalOutcomesEnabled => _campaignRuntimeActive;

        public bool TryCreateDemoStageControlContext(out DemoStageControlGameplayContext context)
        {
            EnsureCampaignStores(_runtimeDirectPlayContext);
            if (stageCatalogProvider == null || _saveSlotStore == null || _activeSlotProvider == null)
            {
                context = default;
                return false;
            }

            var sequenceResolver = RequireCampaignStageSequenceResolver();
            context = new DemoStageControlGameplayContext(
                stageCatalogProvider,
                new DemoStageControlCampaignBridge(
                    _saveSlotStore,
                    _saveSlotStore,
                    _activeSlotProvider,
                    sequenceResolver),
                sequenceResolver);
            return true;
        }

        public bool TryCreateCampaignStageSequenceResolver(out CampaignStageSequenceResolver resolver)
        {
            if (!_campaignRuntimeActive)
            {
                resolver = null;
                return false;
            }

            resolver = RequireCampaignStageSequenceResolver();
            return true;
        }

        protected sealed override InitialGameplayState BuildInitialGameplayState()
        {
            CampaignLaunchHandoffSessionStore.Instance.TryPeek(out var capturedHandoff);
            StageLaunchContextStore.TryPeek(out var capturedContext);
            try
            {
                RequireCampaignStageSequenceResolver();
                var resolved = RuntimeContentResolver.Resolve(CreateStageLoadRequest());
                var buildResult = StageRuntimeBuilder.Build(resolved.Entry.GameplayDefinition);
                var resolvedPresentation = StagePresentationAssembler.Resolve(
                    resolved.Entry.GameplayDefinition,
                    resolved.Entry.PresentationDefinition);
                var resolvedAudio = StageAudioAssembler.Resolve(resolved.Entry.AudioDefinition);
                _resolvedPresentationDefinition = resolved.Entry.PresentationDefinition;
                _resolvedAudioData = resolvedAudio;
                var compositionData = StageSceneCompositionAssembler.ComposeStageBacked(
                    resolved.Entry.GameplayDefinition,
                    buildResult,
                    resolvedPresentation,
                    resolvedAudio);
                _staticWallPresentationProvenance = compositionData.StaticWallPresentationProvenance;

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
            catch
            {
                CleanupCapturedLaunch(capturedHandoff, capturedContext);
                throw;
            }
        }

        protected override void ConfigureRuntimeConfiguration(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            configuration.StaticWallPresentationProvenance =
                _staticWallPresentationProvenance ?? StageStaticWallPresentationProvenance.Empty;
            configuration.CampaignStageSequenceResolver = RequireCampaignStageSequenceResolver();
            CampaignLaunchHandoff capturedHandoff = null;
            StageLaunchContext capturedContext = null;
            try
            {
                configuration.TerminalSessionReadModel =
                    ResolveTerminalSessionReadModel(gameObject);
                configuration.SceneEntryPresentationReadModel =
                    SceneEntryPresentationRegistry.ReadModel;
                StageLaunchContextStore.TryPeek(out capturedContext);
                var directPlayContext = capturedContext != null &&
                                        capturedContext.EditorDirectPlayContext.Mode != EditorDirectPlayMode.None
                    ? capturedContext.EditorDirectPlayContext
                    : EditorDirectPlayContextStore.GetCurrentOrNone();
                _runtimeDirectPlayContext = directPlayContext;
                if (directPlayContext.Mode != EditorDirectPlayMode.None)
                {
                    EditorDirectPlayContextStore.SetCurrent(directPlayContext);
                }

                EnsureCampaignStores(directPlayContext);
                var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
                var hasAnyPendingLaunch = launchHandoffStore.TryPeek(out capturedHandoff);
                if (directPlayContext.Mode != EditorDirectPlayMode.None && hasAnyPendingLaunch)
                {
                    throw new System.InvalidOperationException(
                        "DirectPlay cannot start while a normal campaign launch handoff is pending.");
                }

                var canUseProductionHandoff = directPlayContext.Mode == EditorDirectPlayMode.None;
                var hasPendingLaunch =
                    canUseProductionHandoff &&
                    hasAnyPendingLaunch;
                var hasActiveSlot = _activeSlotProvider != null && _activeSlotProvider.HasActiveSlot;
                var isSuppressed = directPlayContext.SuppressCampaignFlow;
                _campaignRuntimeActive =
                    enableCampaignFlow &&
                    !isSuppressed &&
                    (hasPendingLaunch || hasActiveSlot);
                if (!_campaignRuntimeActive)
                {
                    _campaignChancesReadSource?.Dispose();
                    _campaignChancesReadSource = null;
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
                        UsesTemporaryCampaignState = directPlayContext.UsesTemporaryCampaignState,
                        EnableCampaignFlow = enableCampaignFlow,
                        CampaignRuntimeActive = false,
                        HasActiveSlot = hasActiveSlot,
                        ActiveSlotNumber = _activeSlotProvider != null && _activeSlotProvider.TryGetActiveSlotNumber(out var inactiveSlotNumber)
                            ? inactiveSlotNumber
                            : 0,
                        HasLaunchHandoff = hasPendingLaunch,
                        HandoffSlotNumber = hasPendingLaunch ? capturedHandoff.SlotNumber : 0,
                        HandoffToken = hasPendingLaunch ? capturedHandoff.Token.ToString("N") : string.Empty,
                        SaveStoreDiagnosticsKey = _saveSlotStore != null ? _saveSlotStore.DiagnosticsKey : string.Empty,
                        ActiveSlotDiagnosticsKey = _activeSlotProvider != null ? _activeSlotProvider.DiagnosticsKey : string.Empty,
                        SourceIsNull = true,
                        FailureReason = isSuppressed
                            ? CampaignChanceReadFailureReason.EditorDirectPlaySuppressed
                            : !hasActiveSlot && !hasPendingLaunch
                                ? CampaignChanceReadFailureReason.NoActiveSlot
                                : CampaignChanceReadFailureReason.SourceMissing,
                    });
                    throw new System.InvalidOperationException(
                        "Stage gameplay requires an active Campaign slot or launch handoff before play begins.");
                }

                var resolvedStageId = initialState.StageContentEntry != null
                    ? initialState.StageContentEntry.StageId
                    : StageId.None;
                _runningSlotContext = ResolveRunningSlotContext(
                    resolvedStageId,
                    directPlayContext,
                    hasPendingLaunch ? capturedHandoff : null,
                    capturedContext, configuration);
                _campaignChanceDisplayOverride = new CampaignChanceDisplayOverride();
                _campaignChancesReadSource?.Dispose();
                configuration.CampaignChancesReadSource = _campaignChancesReadSource = new SaveSlotCampaignChancesReadSource(
                    _saveSlotStore,
                    _runningSlotContext,
                    _campaignChanceDisplayOverride);
                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.Installer)
                {
                    SceneName = gameObject.scene.name,
                    LaunchStageId = resolvedStageId.IsValid ? resolvedStageId.Value : string.Empty,
                    ResolvedStageId = resolvedStageId.IsValid ? resolvedStageId.Value : string.Empty,
                    EditorDirectPlayMode = directPlayContext.Mode,
                    SuppressCampaignFlow = directPlayContext.SuppressCampaignFlow,
                    UsesTemporaryCampaignState = directPlayContext.UsesTemporaryCampaignState,
                    EnableCampaignFlow = enableCampaignFlow,
                    CampaignRuntimeActive = true,
                    HasActiveSlot = _activeSlotProvider.HasActiveSlot,
                    ActiveSlotNumber = _runningSlotContext.SlotNumber,
                    HasLaunchHandoff = hasPendingLaunch,
                    HandoffSlotNumber = hasPendingLaunch ? capturedHandoff.SlotNumber : 0,
                    HandoffToken = hasPendingLaunch ? capturedHandoff.Token.ToString("N") : string.Empty,
                    SaveStoreDiagnosticsKey = _saveSlotStore.DiagnosticsKey,
                    ActiveSlotDiagnosticsKey = _activeSlotProvider.DiagnosticsKey,
                    SourceType = configuration.CampaignChancesReadSource.GetType().Name,
                    SourceIsNull = false,
                });
            }
            catch
            {
                _campaignChancesReadSource?.Dispose();
                _campaignChancesReadSource = null;
                _runningSlotContext = null;
                CleanupCapturedLaunch(capturedHandoff, capturedContext);
                throw;
            }
        }

        private static ITerminalSessionReadModel ResolveTerminalSessionReadModel(
            GameObject owner)
        {
            if (owner != null)
            {
                var behaviours = owner.GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is ITerminalSessionAuthorityProvider provider &&
                        provider.TryGetTerminalSessionAuthority(
                            out var readModel,
                            out var authority) &&
                        readModel != null &&
                        authority != null &&
                        ReferenceEquals(readModel, authority))
                    {
                        return readModel;
                    }
                }
            }

            throw new System.InvalidOperationException(
                "Production stage-backed bootstrap requires a co-located " +
                "ITerminalSessionAuthorityProvider backed by the persistent terminal authority.");
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
            ReplaceStageBgmRequestLease();
            try
            {
                AttachBackgroundWallSurfaceTintPresenter(host);
                AttachBackgroundSpaceOrbitPresenter(host);
                var terminalTransitionPort = CreateTerminalTransitionPort(gameObject);

                if (_runningSlotContext == null)
                {
                    throw new System.InvalidOperationException("Campaign runtime requires a running slot context.");
                }

                _campaignFlowController = new CampaignGameplayFlowController(
                    host,
                    _saveSlotStore,
                    _saveSlotStore,
                    _runningSlotContext,
                    RequireCampaignStageSequenceResolver(),
                    CreateStageLaunchRouter(gameObject, gameObject.scene.name),
                    _campaignChanceDisplayOverride,
                    terminalTransitionPort,
                    _runtimeDirectPlayContext,
                    CreateCampaignStageAchievementIntegration(),
                    _preparedCampaignSlot,
                    (_saveSlotStore as ICampaignHudReadProvider)?.HudReadStore);
                _campaignFlowController.Bind();
            }
            catch
            {
                ReleaseStageBgmRequestLease();
                throw;
            }
        }

        private CampaignStageSequenceResolver RequireCampaignStageSequenceResolver()
        {
            if (_campaignStageSequenceResolver != null)
            {
                return _campaignStageSequenceResolver;
            }

            if (campaignStageSequenceDefinition == null)
            {
                throw new System.InvalidOperationException(
                    $"{MissingCampaignStageSequenceDefinitionMessage} Scene='{gameObject.scene.name}', Component='{GetType().Name}'.");
            }

            _campaignStageSequenceResolver =
                new CampaignStageSequenceResolver(campaignStageSequenceDefinition);
            return _campaignStageSequenceResolver;
        }

        private static ICampaignStageAchievementIntegration
            CreateCampaignStageAchievementIntegration()
        {
            return ProductAchievementEarningSinkHandoff
                .CreateCampaignStageIntegrationForSceneComposition();
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

            throw new System.InvalidOperationException(
                "Production campaign bootstrap requires an IStageLaunchRouterProvider; " +
                $"direct scene load fallback is forbidden for scene '{currentSceneName}'.");
        }

        private static ITerminalTransitionPort CreateTerminalTransitionPort(GameObject owner)
        {
            if (owner == null)
            {
                throw new System.InvalidOperationException(
                    "Production campaign bootstrap requires an owner with an ITerminalTransitionPortProvider.");
            }

            var behaviours = owner.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITerminalTransitionPortProvider provider &&
                    provider.TryGetTerminalTransitionPort(out var port) &&
                    port != null)
                {
                    return port;
                }
            }

            throw new System.InvalidOperationException(
                "Production campaign bootstrap requires a co-located ITerminalTransitionPortProvider. " +
                "Install GameplayUiFlowInstaller or an explicit deterministic test port provider.");
        }

        private void OnDestroy()
        {
            ReleaseStageBgmRequestLease();
            _campaignChancesReadSource?.Dispose();
            _campaignChancesReadSource = null;
            _backgroundSpaceOrbitPresenterAdapter?.Dispose();
            _backgroundSpaceOrbitPresenterAdapter = null;
            _backgroundWallSurfaceTintPresenterAdapter?.Dispose();
            _backgroundWallSurfaceTintPresenterAdapter = null;
            _campaignFlowController?.Dispose();
        }

        private void ReplaceStageBgmRequestLease()
        {
            var acquiredLease = _stageAudioRuntimeRequestSource.Apply(
                _resolvedAudioData,
                globalAudioFlowBootstrap.GetRequestRouterOrThrow());
            var previousLease = _stageBgmRequestLease;
            _stageBgmRequestLease = acquiredLease;
            previousLease?.Dispose();
        }

        private void ReleaseStageBgmRequestLease()
        {
            var lease = _stageBgmRequestLease;
            _stageBgmRequestLease = null;
            lease?.Dispose();
        }

        private void AttachBackgroundSpaceOrbitPresenter(GameplaySceneHost host)
        {
            _backgroundSpaceOrbitPresenterAdapter?.Dispose();
            _backgroundSpaceOrbitPresenterAdapter = null;

            var backgroundInstance = _stageVisualRuntimeAdapter.CurrentBackgroundInstance;
            if (host == null || host.Presenter == null || backgroundInstance == null)
            {
                return;
            }

            host.Presenter.RegisterPresentationPauseRoot(backgroundInstance);

            var authoring = BackgroundSpaceOrbitPresenterAdapter.ResolveSingleAuthoring(backgroundInstance);
            if (authoring == null)
            {
                return;
            }

            _backgroundSpaceOrbitPresenterAdapter =
                new BackgroundSpaceOrbitPresenterAdapter(authoring, host.Presenter);
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

        private void EnsureCampaignStores(EditorDirectPlayContext directPlayContext)
        {
            if (directPlayContext.UsesTemporaryCampaignState)
            {
                _saveSlotStore ??= CampaignSaveCompositionProvider.CreateTemporaryProfileBacked();
                _activeSlotProvider ??= CampaignSaveCompositionProvider.CreateTemporaryActiveSlotProvider(
                    _saveSlotStore);
                return;
            }

            _saveSlotStore ??= CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            _activeSlotProvider ??= CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(_saveSlotStore);
        }

        private void PrepareCampaignPlayer(GameplaySceneHostConfiguration configuration, CampaignSlotState slot)
        {
            var entities = configuration.InitialEntities;
            if (slot.GameMode == GameMode.Casual)
            {
                var copy = (EntityState[])entities.Clone();
                var found = false;
                for (var i = 0; i < copy.Length; i++)
                {
                    if (copy[i].entityId != configuration.PlayerEntityId) continue;
                    if (copy[i].type != EntityType.Unit || copy[i].unitRole != UnitRole.Player)
                        throw new System.InvalidOperationException("Campaign player identity does not identify a Player.");
                    copy[i].hp = slot.ResumeHp;
                    copy[i].maxHp = CampaignSaveSlotPolicy.CasualMaxHp;
                    found = true;
                }
                if (!found) throw new System.InvalidOperationException("Casual launch requires an initial Player.");
                configuration.InitialEntities = copy;
            }
            configuration.CampaignGameMode = slot.GameMode;
            _preparedCampaignSlot = slot;
        }

        protected override void ConfigureAfterTimingPresets(GameplaySceneHostConfiguration configuration)
        {
            if (configuration.CampaignGameMode != GameMode.Casual) return;
            configuration.PlayerControlTiming = configuration.PlayerControlTiming.Clone();
            configuration.PlayerControlTiming.DamageCooldownSeconds = CampaignSaveSlotPolicy.CasualDamageCooldownSeconds;
        }

        private CampaignRunningSlotContext ResolveRunningSlotContext(
            StageId resolvedStageId,
            EditorDirectPlayContext directPlayContext,
            CampaignLaunchHandoff pendingHandoff,
            StageLaunchContext launchContext,
            GameplaySceneHostConfiguration configuration)
        {
            if (directPlayContext.Mode != EditorDirectPlayMode.None)
            {
                var slot = ValidateCommittedActiveSlotMatchesLaunchStage(resolvedStageId);
                PrepareCampaignPlayer(configuration, slot);
                var slotNumber = slot.SlotNumber;
                if (launchContext != null &&
                    (launchContext.IsEditorDirectPlayBootstrap ||
                     launchContext.EditorDirectPlayContext.Mode != EditorDirectPlayMode.None))
                {
                    if (!StaticStageLaunchContextCommitStore.Instance.TryConsume(
                            launchContext,
                            out var consumedContext))
                    {
                        throw new System.InvalidOperationException(
                            "DirectPlay campaign bootstrap could not consume its exact stage launch context.");
                    }

                    if (!ReferenceEquals(consumedContext, launchContext))
                    {
                        throw new System.InvalidOperationException(
                            "DirectPlay campaign bootstrap consumed a different stage launch context.");
                    }
                }

                return new CampaignRunningSlotContext(slotNumber);
            }

            if (launchContext == null)
            {
                throw new System.InvalidOperationException(
                    "Campaign runtime requires a stage launch context.");
            }

            var transaction = new CampaignLaunchCommitTransaction(
                _saveSlotStore,
                _activeSlotProvider,
                CampaignLaunchHandoffSessionStore.Instance,
                StaticStageLaunchContextCommitStore.Instance,
                CampaignRunningSlotContextFactory.Instance,
                slot => PrepareCampaignPlayer(configuration, slot));
            return pendingHandoff != null
                ? transaction.CommitPending(pendingHandoff, launchContext, resolvedStageId)
                : transaction.CommitPendingless(launchContext, resolvedStageId);
        }

        private CampaignSlotState ValidateCommittedActiveSlotMatchesLaunchStage(StageId resolvedStageId)
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

            return activeSlot;
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

        private CampaignSlotState LoadNonEmptySlot(int slotNumber)
        {
            var entry = _saveSlotStore.LoadSlot(slotNumber);
            if (entry == null || entry.IsEmpty || !entry.State.CurrentStageId.IsValid)
            {
                throw new System.InvalidOperationException(
                    $"Campaign slot '{slotNumber}' is empty or missing.");
            }

            return entry.State;
        }

        private static void CleanupCapturedLaunch(
            CampaignLaunchHandoff capturedHandoff,
            StageLaunchContext capturedContext)
        {
            if (capturedHandoff != null &&
                capturedContext != null &&
                !capturedContext.Matches(capturedHandoff))
            {
                return;
            }

            if (capturedHandoff != null)
            {
                var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
                if (handoffStore.TryPeek(out var currentHandoff) &&
                    currentHandoff.Matches(capturedHandoff))
                {
                    handoffStore.TryClear(capturedHandoff.Token);
                }
            }

            if (capturedContext != null)
            {
                StageLaunchContextStore.TryClear(capturedContext);
            }
        }
    }

    internal interface IStageLaunchContextCommitStore
    {
        bool TryPeek(out StageLaunchContext context);
        bool IsCurrent(StageLaunchContext expected);
        bool TryClear(StageLaunchContext expected);
        bool TryConsume(StageLaunchContext expected, out StageLaunchContext consumed);
    }

    internal sealed class StaticStageLaunchContextCommitStore : IStageLaunchContextCommitStore
    {
        public static StaticStageLaunchContextCommitStore Instance { get; } = new();

        private StaticStageLaunchContextCommitStore()
        {
        }

        public bool TryPeek(out StageLaunchContext context) => StageLaunchContextStore.TryPeek(out context);
        public bool IsCurrent(StageLaunchContext expected) => StageLaunchContextStore.IsCurrent(expected);
        public bool TryClear(StageLaunchContext expected) => StageLaunchContextStore.TryClear(expected);
        public bool TryConsume(StageLaunchContext expected, out StageLaunchContext consumed) =>
            StageLaunchContextStore.TryConsume(expected, out consumed);
    }

    internal interface ICampaignRunningSlotContextFactory
    {
        CampaignRunningSlotContext Create(int slotNumber);
    }

    internal sealed class CampaignRunningSlotContextFactory : ICampaignRunningSlotContextFactory
    {
        public static CampaignRunningSlotContextFactory Instance { get; } = new();

        private CampaignRunningSlotContextFactory()
        {
        }

        public CampaignRunningSlotContext Create(int slotNumber) => new(slotNumber);
    }

    internal sealed class CampaignLaunchCommitTransaction
    {
        private readonly ICampaignSaveQuery _saveSlotStore;
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly ICampaignLaunchHandoffStore _handoffStore;
        private readonly IStageLaunchContextCommitStore _contextStore;
        private readonly ICampaignRunningSlotContextFactory _runningFactory;
        private readonly System.Action<CampaignSlotState> _prepareSlot;

        public CampaignLaunchCommitTransaction(
            ICampaignSaveQuery saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            ICampaignLaunchHandoffStore handoffStore,
            IStageLaunchContextCommitStore contextStore,
            ICampaignRunningSlotContextFactory runningFactory,
            System.Action<CampaignSlotState> prepareSlot = null)
        {
            _saveSlotStore = saveSlotStore ?? throw new System.ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new System.ArgumentNullException(nameof(activeSlotProvider));
            _handoffStore = handoffStore ?? throw new System.ArgumentNullException(nameof(handoffStore));
            _contextStore = contextStore ?? throw new System.ArgumentNullException(nameof(contextStore));
            _runningFactory = runningFactory ?? throw new System.ArgumentNullException(nameof(runningFactory));
            _prepareSlot = prepareSlot;
        }

        public CampaignRunningSlotContext CommitPending(
            CampaignLaunchHandoff expectedHandoff,
            StageLaunchContext expectedContext,
            StageId resolvedStageId)
        {
            if (expectedHandoff == null)
            {
                throw new System.ArgumentNullException(nameof(expectedHandoff));
            }

            if (expectedContext == null)
            {
                throw new System.ArgumentNullException(nameof(expectedContext));
            }

            var hadPreviousActive = false;
            var previousActiveSlot = 0;
            var activeWriteAttempted = false;
            try
            {
                ValidatePendingOwnership(expectedHandoff, expectedContext, resolvedStageId);
                var slot = LoadValidatedSlot(expectedHandoff.SlotNumber, resolvedStageId);
                _prepareSlot?.Invoke(slot);
                hadPreviousActive = _activeSlotProvider.TryGetActiveSlotNumber(out previousActiveSlot);

                activeWriteAttempted = true;
                _activeSlotProvider.SetActiveSlot(slot.SlotNumber);
                var runningContext = _runningFactory.Create(slot.SlotNumber);
                if (runningContext == null || runningContext.SlotNumber != slot.SlotNumber)
                {
                    throw new System.InvalidOperationException("Campaign running slot factory returned an invalid context.");
                }

                if (!_handoffStore.TryPeek(out var handoffAtConsume) ||
                    !handoffAtConsume.Matches(expectedHandoff) ||
                    !_handoffStore.TryConsume(expectedHandoff.Token, out var consumedHandoff) ||
                    consumedHandoff == null ||
                    !consumedHandoff.Matches(expectedHandoff))
                {
                    throw new System.InvalidOperationException(
                        "Campaign launch handoff changed before gameplay installer commit completed.");
                }

                if (!_contextStore.TryConsume(expectedContext, out var consumedContext) ||
                    consumedContext == null ||
                    !consumedContext.Equals(expectedContext))
                {
                    throw new System.InvalidOperationException(
                        "Stage launch context changed before gameplay installer commit completed.");
                }

                return runningContext;
            }
            catch (System.Exception failure)
            {
                var failures = new System.Collections.Generic.List<System.Exception> { failure };
                var rollbackFailure = TryRollbackActive(
                    activeWriteAttempted,
                    hadPreviousActive,
                    previousActiveSlot);
                if (rollbackFailure != null)
                {
                    failures.Add(rollbackFailure);
                }

                CollectCleanupFailures(expectedHandoff, expectedContext, failures);
                if (failures.Count > 1)
                {
                    throw new System.AggregateException(
                        "Campaign launch commit failed and one or more compensation steps also failed.",
                        failures);
                }

                throw;
            }
        }

        public CampaignRunningSlotContext CommitPendingless(
            StageLaunchContext expectedContext,
            StageId resolvedStageId)
        {
            if (expectedContext == null)
            {
                throw new System.ArgumentNullException(nameof(expectedContext));
            }

            try
            {
                if (!_contextStore.IsCurrent(expectedContext))
                {
                    throw new System.InvalidOperationException(
                        "Pending-less campaign reload requires the exact current stage launch context.");
                }

                var request = new StageNavigationRequest(
                    expectedContext.StageId,
                    expectedContext.NavigationKind,
                    expectedContext.Source);
                if (!CampaignPendinglessLaunchPolicy.IsAllowed(request))
                {
                    throw new System.InvalidOperationException(
                        "Pending-less campaign reload is not allowed for this navigation/source.");
                }

                ValidateStageIds(expectedContext.StageId, resolvedStageId);
                if (!_activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber))
                {
                    throw new System.InvalidOperationException(
                        "Pending-less campaign reload requires a committed active slot.");
                }

                var slot = LoadValidatedSlot(activeSlotNumber, resolvedStageId);
                _prepareSlot?.Invoke(slot);
                var runningContext = _runningFactory.Create(slot.SlotNumber);
                if (runningContext == null || runningContext.SlotNumber != slot.SlotNumber)
                {
                    throw new System.InvalidOperationException("Campaign running slot factory returned an invalid context.");
                }

                if (!_contextStore.TryConsume(expectedContext, out var consumedContext) ||
                    consumedContext == null ||
                    !consumedContext.Equals(expectedContext))
                {
                    throw new System.InvalidOperationException(
                        "Stage launch context changed before pending-less reload finalized.");
                }

                return runningContext;
            }
            catch
            {
                _contextStore.TryClear(expectedContext);
                throw;
            }
        }

        private void ValidatePendingOwnership(
            CampaignLaunchHandoff expectedHandoff,
            StageLaunchContext expectedContext,
            StageId resolvedStageId)
        {
            if (!_handoffStore.TryPeek(out var currentHandoff) ||
                !currentHandoff.Matches(expectedHandoff))
            {
                throw new System.InvalidOperationException(
                    "Campaign launch handoff changed before gameplay installer validation.");
            }

            if (!_contextStore.IsCurrent(expectedContext))
            {
                throw new System.InvalidOperationException(
                    "Stage launch context changed before gameplay installer validation.");
            }

            if (!expectedContext.Matches(expectedHandoff))
            {
                throw new System.InvalidOperationException(
                    "Campaign handoff and stage launch context do not identify the same operation.");
            }

            ValidateStageIds(expectedHandoff.StageId, resolvedStageId);
        }

        private CampaignSlotState LoadValidatedSlot(int slotNumber, StageId resolvedStageId)
        {
            var entry = _saveSlotStore.LoadSlot(slotNumber);
            if (entry == null || entry.IsEmpty || !entry.State.CurrentStageId.IsValid)
            {
                throw new System.InvalidOperationException($"Campaign slot '{slotNumber}' is empty or missing.");
            }

            var slot = entry.State;

            if (!slot.CurrentStageId.Equals(resolvedStageId))
            {
                throw new System.InvalidOperationException(
                    $"Campaign slot stage '{slot.CurrentStageId.Value}' does not match resolved launch stage '{resolvedStageId.Value}'.");
            }

            return slot;
        }

        private static void ValidateStageIds(StageId requestedStageId, StageId resolvedStageId)
        {
            if (!requestedStageId.IsValid || !resolvedStageId.IsValid)
            {
                throw new System.InvalidOperationException(
                    "Campaign runtime requires valid requested and resolved StageIds.");
            }

            if (!requestedStageId.Equals(resolvedStageId))
            {
                throw new System.InvalidOperationException(
                    $"Campaign launch StageIds do not match. requested='{requestedStageId.Value}', resolved='{resolvedStageId.Value}'.");
            }
        }

        private System.Exception TryRollbackActive(
            bool activeWriteAttempted,
            bool hadPreviousActive,
            int previousActiveSlot)
        {
            if (!activeWriteAttempted)
            {
                return null;
            }

            try
            {
                if (hadPreviousActive)
                {
                    _activeSlotProvider.SetActiveSlot(previousActiveSlot);
                }
                else
                {
                    _activeSlotProvider.ClearActiveSlot();
                }

                return null;
            }
            catch (System.Exception rollbackFailure)
            {
                return rollbackFailure;
            }
        }

        private void CollectCleanupFailures(
            CampaignLaunchHandoff expectedHandoff,
            StageLaunchContext expectedContext,
            System.Collections.Generic.List<System.Exception> failures)
        {
            if (!expectedContext.Matches(expectedHandoff))
            {
                return;
            }

            try
            {
                if (_handoffStore.TryPeek(out var currentHandoff) &&
                    currentHandoff.Matches(expectedHandoff))
                {
                    _handoffStore.TryClear(expectedHandoff.Token);
                }
            }
            catch (System.Exception cleanupFailure)
            {
                failures.Add(cleanupFailure);
            }

            try
            {
                _contextStore.TryClear(expectedContext);
            }
            catch (System.Exception cleanupFailure)
            {
                failures.Add(cleanupFailure);
            }
        }
    }
}
