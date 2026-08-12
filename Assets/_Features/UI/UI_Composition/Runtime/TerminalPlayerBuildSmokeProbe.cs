using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal sealed class TerminalPlayerBuildSmokeProbe : MonoBehaviour
    {
        internal const string LaunchArgument = "--terminal-player-build-smoke";
        internal const string InputArgument = "--terminal-player-build-smoke-input";
        internal const string ScenarioArgument = "--terminal-player-build-smoke-scenario";
        internal const string CampaignExpectedIntentArgument =
            "--terminal-player-campaign-expected-intent";
        internal const string CampaignExpectedStageArgument =
            "--terminal-player-campaign-expected-stage";
        internal const string ChancesArgument = "--capture-campaign-temp-slot-chances";
        internal const string WidthArgument = "--terminal-player-build-smoke-width";
        internal const string HeightArgument = "--terminal-player-build-smoke-height";
        internal const string RevisionArgument = "--terminal-player-build-smoke-revision";
        internal const string SuccessMarker = "TERMINAL_PLAYER_BUILD_SMOKE:PASS";
        internal const string FailureMarker = "TERMINAL_PLAYER_BUILD_SMOKE:FAIL";
        private const float TimeoutSeconds = 20f;
        private const float RenderEnvironmentTimeoutSeconds = 5f;
        private const float InputDispatchTimeoutSeconds = 1f;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int ShowWindowRestore = 9;
#endif
        private static readonly HashSet<int> RenderedCameraInstanceIds = new();
        private static Keyboard smokeKeyboard;
        private static Mouse smokeMouse;
        private bool _interactionRenderAcknowledged;
        private bool _inputWindowFocused;
        private int _lastCanvasRenderFrame = -1;
        private string _scenarioId = string.Empty;
        private int _scenarioSequenceIndex;
        private float _probeStartedAt;
        private float _resultInteractionReadyAt;
        private bool _pointerDispatchSucceeded;
        private bool _gameplayEntryCompletionSucceeded;
        private bool _sourceOpaqueAcknowledged;
        private bool _persistentCoverAcknowledged;
        private bool _destinationReadyAcknowledged;
        private bool _destinationClosedIrisAcknowledged;
        private bool _openingCompleted;
        private bool _inputReleased;
        private int _requestedWidth;
        private int _requestedHeight;
        private StageId _authoritySourceStageId;
        private StageId _authorityExpectedNextStageId;
        private StageId _authoritySavedStageId;
        private StageId _authorityResultNextStageId;
        private bool _authorityCampaignCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (!HasArgument(LaunchArgument))
            {
                return;
            }

            UnityEngine.Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            var nativeInputDevices = InputSystem.devices
                .Where(device => device is Keyboard || device is Mouse)
                .ToArray();
            for (var i = 0; i < nativeInputDevices.Length; i++)
            {
                InputSystem.DisableDevice(nativeInputDevices[i]);
            }

            smokeKeyboard = InputSystem.AddDevice<Keyboard>();
            smokeMouse = InputSystem.AddDevice<Mouse>();
            RenderedCameraInstanceIds.Clear();
            Camera.onPostRender -= HandleBuiltInCameraRendered;
            Camera.onPostRender += HandleBuiltInCameraRendered;
            RenderPipelineManager.endCameraRendering -= HandleScriptableCameraRendered;
            RenderPipelineManager.endCameraRendering += HandleScriptableCameraRendered;
            var root = new GameObject(nameof(TerminalPlayerBuildSmokeProbe));
            DontDestroyOnLoad(root);
            root.AddComponent<TerminalPlayerBuildSmokeProbe>();
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleCanvasRendered;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleCanvasRendered;
        }

        private void LateUpdate()
        {
            MaintainRequestedResolution();
        }

        private void HandleCanvasRendered()
        {
            _lastCanvasRenderFrame = Time.frameCount;
        }

        private IEnumerator Start()
        {
            var inputMode = ReadArgumentValue(InputArgument);
            var scenario = ReadArgumentValue(ScenarioArgument);
            _scenarioId = string.IsNullOrEmpty(scenario) ? "stageresult" : scenario;
            _scenarioSequenceIndex = 1;
            _probeStartedAt = Time.realtimeSinceStartup;
            if (!int.TryParse(
                    ReadArgumentValue(WidthArgument),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var requestedWidth) ||
                !int.TryParse(
                    ReadArgumentValue(HeightArgument),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var requestedHeight) ||
                requestedWidth <= 0 ||
                requestedHeight <= 0)
            {
                Fail("requested render resolution arguments are missing or invalid");
                yield break;
            }

            var gameClearScenario = string.Equals(
                scenario,
                "gameclear",
                StringComparison.OrdinalIgnoreCase);
            var normalGameClearScenario = string.Equals(
                scenario,
                "gameclear-normal",
                StringComparison.OrdinalIgnoreCase);
            gameClearScenario |= normalGameClearScenario;
            var sequentialGameClearScenario = string.Equals(
                scenario,
                "gameclear-sequential",
                StringComparison.OrdinalIgnoreCase);
            var defeatScenario = string.Equals(
                scenario,
                "defeat",
                StringComparison.OrdinalIgnoreCase);
            var mainMenuGameplayScenario = string.Equals(
                scenario,
                "mainmenu-gameplay",
                StringComparison.OrdinalIgnoreCase);
            var pauseRetryScenario = string.Equals(
                scenario,
                "pause-retry",
                StringComparison.OrdinalIgnoreCase);
            var levelFailedRestartScenario = string.Equals(
                scenario,
                "level-failed-restart",
                StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(scenario) &&
                !gameClearScenario &&
                !sequentialGameClearScenario &&
                !defeatScenario &&
                !mainMenuGameplayScenario &&
                !pauseRetryScenario &&
                !levelFailedRestartScenario)
            {
                Fail($"scenario='{scenario}' is invalid");
                yield break;
            }

            if (!string.Equals(inputMode, "pointer", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(inputMode, "keyboard", StringComparison.OrdinalIgnoreCase))
            {
                Fail($"inputMode='{inputMode}' is invalid");
                yield break;
            }

            Screen.SetResolution(
                requestedWidth,
                requestedHeight,
                FullScreenMode.Windowed);
            _requestedWidth = requestedWidth;
            _requestedHeight = requestedHeight;
            UnityEngine.Application.targetFrameRate = 60;

            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            GameplayUiFlowInstaller installer = null;
            GameplaySceneHost host = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                installer = FindFirstObjectByType<GameplayUiFlowInstaller>(FindObjectsInactive.Include);
                host = FindFirstObjectByType<GameplaySceneHost>(FindObjectsInactive.Include);
                if (installer != null &&
                    host != null &&
                    installer.IsInstalledForDiagnostics &&
                    installer.TryGetTerminalTransitionPort(out _))
                {
                    break;
                }

                yield return null;
            }

            if (installer == null || host == null)
            {
                Fail("production gameplay host or UI installer did not become ready");
                yield break;
            }

            if (normalGameClearScenario)
            {
                if (EditorDirectPlayContextStore.GetCurrentOrNone().Mode !=
                    EditorDirectPlayMode.None)
                {
                    Fail("normal Campaign capture unexpectedly retained DirectPlay context");
                    yield break;
                }
            }
            else if (!StageLaunchContextStore.TryPeek(out var bootstrapContext) ||
                     !StageLaunchContextStore.TryConsume(
                         bootstrapContext,
                         out var consumedContext) ||
                     consumedContext == null ||
                     !consumedContext.Equals(bootstrapContext))
            {
                Fail("exact capture bootstrap launch context was not consumable");
                yield break;
            }

            GameplayEntityView playerView = null;
            if (host.OutputCamera == null ||
                host.PlayerEntityId <= 0 ||
                !host.ViewRegistry.TryGetView(host.PlayerEntityId, out playerView) ||
                playerView == null)
            {
                Fail(
                    $"outputCamera={(host.OutputCamera != null)} playerEntityId={host.PlayerEntityId} " +
                    $"playerView={(playerView != null)}");
                yield break;
            }

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Fail(
                    "render environment rejected Null graphics device; " +
                    DescribeRenderEnvironment(
                        host.OutputCamera,
                        requestedWidth,
                        requestedHeight));
                yield break;
            }

            var renderEnvironmentReady = false;
            var renderEnvironmentFailure = string.Empty;
            deadline = Time.realtimeSinceStartup + RenderEnvironmentTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (TryValidateRenderEnvironment(
                        SystemInfo.graphicsDeviceType,
                        requestedWidth,
                        requestedHeight,
                        Screen.width,
                        Screen.height,
                        host.OutputCamera.pixelRect,
                        host.OutputCamera.targetDisplay,
                        host.OutputCamera.isActiveAndEnabled &&
                        host.OutputCamera.gameObject.activeInHierarchy,
                        RenderedCameraInstanceIds.Contains(
                            host.OutputCamera.GetInstanceID()),
                        out renderEnvironmentFailure))
                {
                    renderEnvironmentReady = true;
                    break;
                }

                yield return null;
            }

            if (!renderEnvironmentReady)
            {
                Fail(
                    $"render environment did not become ready: {renderEnvironmentFailure}; " +
                    DescribeRenderEnvironment(
                        host.OutputCamera,
                        requestedWidth,
                        requestedHeight));
                yield break;
            }

            host.InputHost.SetAutoAdvanceTicks(false);
            if (mainMenuGameplayScenario)
            {
                yield return PrepareMainMenuFixture(installer);
                var mainMenuDestination =
                    FindFirstObjectByType<MainMenuUiFlowInstaller>(
                        FindObjectsInactive.Include);
                if (MainMenuEntryPresentationRegistry.IsActive ||
                    mainMenuDestination == null ||
                    SceneManager.GetActiveScene().handle !=
                    mainMenuDestination.gameObject.scene.handle)
                {
                    yield break;
                }

                yield return RunMainMenuGameplaySmoke(requestedWidth, requestedHeight);
                yield break;
            }

            if (defeatScenario || levelFailedRestartScenario)
            {
                if (!int.TryParse(
                        ReadArgumentValue(ChancesArgument),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var initialChances) ||
                    initialChances < 1 ||
                    initialChances > SaveSlotStore.DefaultRemainingChances)
                {
                    Fail($"defeat scenario remaining chances are invalid: '{ReadArgumentValue(ChancesArgument)}'");
                    yield break;
                }

                yield return RunDefeatSmoke(
                    installer,
                    host,
                    initialChances,
                    restartAfterLevelFailed: levelFailedRestartScenario);
                yield break;
            }

            if (pauseRetryScenario)
            {
                yield return RunPauseRetrySmoke(installer, host);
                yield break;
            }

            var sequenceResolver = host.UiAccess?.CampaignStageSequenceResolver;
            var sourceStage = host.UiAccess?.QueryFacade.Stage.Read() ?? default;
            var resolverProvider = host.GetComponents<MonoBehaviour>()
                .OfType<ICampaignStageSequenceResolverProvider>()
                .SingleOrDefault();
            CampaignStageSequenceResolver providerResolver = null;
            if (sequenceResolver == null ||
                resolverProvider == null ||
                !resolverProvider.TryCreateCampaignStageSequenceResolver(
                    out providerResolver) ||
                !ReferenceEquals(sequenceResolver, providerResolver) ||
                !sourceStage.StageId.IsValid ||
                !sequenceResolver.Contains(sourceStage.StageId) ||
                string.IsNullOrWhiteSpace(sourceStage.DisplayNameKey) ||
                StageId.TryCreate("legacy-stage-5-1", out var catalogOnlyStageId) &&
                sequenceResolver.Contains(catalogOnlyStageId))
            {
                Fail(
                    $"campaign authority bootstrap invalid resolver={(sequenceResolver != null)} " +
                    $"provider={(resolverProvider != null)} sameInstance=" +
                    $"{ReferenceEquals(sequenceResolver, providerResolver)} " +
                    $"stage={sourceStage.StageId.Value} displayKey={sourceStage.DisplayNameKey}");
                yield break;
            }

            if (!TryValidateDemoStageControl(
                    installer,
                    host,
                    resolverProvider,
                    sequenceResolver,
                    out var demoStageControlFailure))
            {
                Fail($"Demo Stage Control validation failed: {demoStageControlFailure}");
                yield break;
            }

            Debug.Log(
                $"TERMINAL_PLAYER_BUILD_SMOKE:DEMO_STAGE_CONTROL_VALIDATED " +
                $"count={sequenceResolver.Entries.Count}");

            var sourceIsFinal = sequenceResolver.IsFinal(sourceStage.StageId);
            var expectedNextStageId = sequenceResolver.GetNextOrNone(sourceStage.StageId);
            if (gameClearScenario != sourceIsFinal ||
                !gameClearScenario && !expectedNextStageId.IsValid)
            {
                Fail(
                    $"campaign terminal classification mismatch stage={sourceStage.StageId.Value} " +
                    $"isFinal={sourceIsFinal} scenario={_scenarioId} " +
                    $"next={expectedNextStageId.Value}");
                yield break;
            }

            MoveViewToViewport(host.OutputCamera, playerView, new Vector2(0.2f, 0.5f));
            var projectedCenter = ProjectRendererBoundsCenter(host.OutputCamera, playerView);
            var achievementPath = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                "Saves",
                "achievements.json");
            var achievementBefore = File.Exists(achievementPath)
                ? File.ReadAllText(achievementPath)
                : null;
            var victoryStarted = normalGameClearScenario
                ? PresentObjectiveClearTick(host)
                : installer.TryForceClearCurrentStageForDiagnostics(out _);
            if (!victoryStarted ||
                !installer.TryGetTerminalTransitionPort(out var transitionPort) ||
                transitionPort is not GameplayTerminalTransitionPort productionPort ||
                productionPort.CurrentPlayback == null)
            {
                Fail($"production victory failed scenario={_scenarioId}");
                yield break;
            }

            var completionReadModel =
                host.UiAccess.PresentationFeed.CurrentMinimalStageCompletion;
            var savedSlot = normalGameClearScenario
                ? CampaignSaveCompositionProvider.CreateProductionProfileBacked().LoadSlot(1)
                : new SaveSlotStore(
                        EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                        EditorDirectPlayContextStore.TempActiveSlotProviderKey)
                    .LoadSlot(1);
            var achievementAfter = File.Exists(achievementPath)
                ? File.ReadAllText(achievementPath)
                : null;
            var achievementContractAligned = normalGameClearScenario
                ? savedSlot.HasNormalCampaignCompletionReceipt &&
                  savedSlot.NormalCampaignCompletionReceipt != null &&
                  savedSlot.NormalCampaignCompletionReceipt.Version ==
                      NormalCampaignCompletionReceipt.CurrentVersion &&
                  string.Equals(
                      savedSlot.NormalCampaignCompletionReceipt.CompletedStageId,
                      sourceStage.StageId.Value,
                      StringComparison.Ordinal) &&
                  string.IsNullOrEmpty(savedSlot.NormalCampaignCompletionReceipt.StageRunId) &&
                  !string.IsNullOrEmpty(achievementAfter) &&
                  achievementAfter.Contains("campaign.complete", StringComparison.Ordinal)
                : !savedSlot.HasNormalCampaignCompletionReceipt &&
                  savedSlot.NormalCampaignCompletionReceipt == null &&
                  string.Equals(achievementBefore, achievementAfter, StringComparison.Ordinal);
            if (!achievementContractAligned)
            {
                Fail(
                    $"Campaign Achievement contract diverged scenario={_scenarioId} " +
                    $"receiptPresent={savedSlot.HasNormalCampaignCompletionReceipt} " +
                    $"receiptVersion={savedSlot.NormalCampaignCompletionReceipt?.Version ?? 0} " +
                    $"ledgerChanged={!string.Equals(achievementBefore, achievementAfter, StringComparison.Ordinal)}");
                yield break;
            }
            var progressionAligned = gameClearScenario
                ? completionReadModel != null &&
                  completionReadModel.StageId.Equals(sourceStage.StageId) &&
                  !completionReadModel.NextStageRequest.IsValid &&
                  savedSlot.CurrentStageId.Equals(sourceStage.StageId) &&
                  savedSlot.CampaignCompleted
                : completionReadModel != null &&
                  completionReadModel.StageId.Equals(sourceStage.StageId) &&
                  completionReadModel.NextStageRequest.IsValid &&
                  completionReadModel.NextStageRequest.StageId.Equals(
                      expectedNextStageId) &&
                  savedSlot.CurrentStageId.Equals(expectedNextStageId) &&
                  !savedSlot.CampaignCompleted;
            if (!progressionAligned)
            {
                Fail(
                    $"campaign progression divergence source={sourceStage.StageId.Value} " +
                    $"expectedNext={expectedNextStageId.Value} " +
                    $"resultNext={completionReadModel?.NextStageRequest.StageId.Value ?? string.Empty} " +
                    $"saved={savedSlot.CurrentStageId.Value} " +
                    $"completed={savedSlot.CampaignCompleted}");
                yield break;
            }

            _authoritySourceStageId = sourceStage.StageId;
            _authorityExpectedNextStageId = expectedNextStageId;
            _authoritySavedStageId = savedSlot.CurrentStageId;
            _authorityResultNextStageId = completionReadModel.NextStageRequest.StageId;
            _authorityCampaignCompleted = savedSlot.CampaignCompleted;

            var diagnostics = productionPort.LastFocusCaptureDiagnostics;
            var playback = productionPort.CurrentPlayback;
            var irisView = installer.RootView.TerminalIrisOverlayView;
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (playback.State == TerminalTransitionState.Focusing &&
                   Time.realtimeSinceStartup < deadline)
            {
                MaintainRequestedResolution();
                yield return null;
            }
            var runtimeMaterial = irisView.RuntimeMaterialForTests;
            var materialVector = runtimeMaterial != null
                ? runtimeMaterial.GetVector("_Center")
                : Vector4.zero;
            var materialCenter = new Vector2(materialVector.x, materialVector.y);
            if (!diagnostics.Succeeded ||
                diagnostics.IsFallback ||
                diagnostics.OutputCamera != host.OutputCamera ||
                diagnostics.View != playerView ||
                diagnostics.RendererCount <= 0 ||
                Vector2.Distance(projectedCenter, diagnostics.CapturedCenter) > 0.025f ||
                Vector2.Distance(diagnostics.CapturedCenter, playback.FocusTarget.NormalizedCenter) > 0.0001f ||
                Vector2.Distance(diagnostics.CapturedCenter, materialCenter) > 0.0001f ||
                Mathf.Abs(diagnostics.CapturedCenter.x - 0.2f) > 0.025f)
            {
                Fail(
                    $"focus projected={projectedCenter} captured={diagnostics.CapturedCenter} " +
                    $"material={materialCenter} fallback={diagnostics.IsFallback} " +
                    $"reason={diagnostics.FailureReason} renderers={diagnostics.RendererCount}");
                yield break;
            }

            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            var previousHandoffAlpha = 1f;
            var previousContentAlpha = 0f;
            var sawIntermediateHandoffAlpha = false;
            var sawIntermediateContentAlpha = false;
            while (TerminalSessionRegistry.IsActive && Time.realtimeSinceStartup < deadline)
            {
                IResultTransitionScreenView activeResult = gameClearScenario
                    ? installer.GameClearScreenView
                    : installer.StageResultScreenView;
                if (activeResult != null)
                {
                    if (activeResult.HandoffCoverAlpha > previousHandoffAlpha + 0.0001f ||
                        activeResult.ContentAlpha < previousContentAlpha - 0.0001f)
                    {
                        Fail(
                            $"non-monotonic result alpha cover={activeResult.HandoffCoverAlpha:F6} " +
                            $"previousCover={previousHandoffAlpha:F6} content={activeResult.ContentAlpha:F6} " +
                            $"previousContent={previousContentAlpha:F6}");
                        yield break;
                    }

                    sawIntermediateHandoffAlpha |=
                        activeResult.HandoffCoverAlpha > 0f &&
                        activeResult.HandoffCoverAlpha < 0.999f;
                    sawIntermediateContentAlpha |=
                        activeResult.ContentAlpha > 0f &&
                        activeResult.ContentAlpha < 0.999f;
                    previousHandoffAlpha = activeResult.HandoffCoverAlpha;
                    previousContentAlpha = activeResult.ContentAlpha;
                }

                yield return null;
            }

            if (gameClearScenario)
            {
                yield return RunGameClearSmoke(
                    installer,
                    irisView,
                    inputMode,
                    sawIntermediateHandoffAlpha,
                    sawIntermediateContentAlpha);
                yield break;
            }

            var stageResult = installer.StageResultScreenView;
            var button = stageResult != null
                ? stageResult.GetComponentInChildren<Button>(true)
                : null;
            var irisGroup = irisView.GetComponent<CanvasGroup>();
            var irisImage = irisView.GetComponentInChildren<Image>(true);
            var requiredTraceEvents = new[]
            {
                TerminalTraceEvent.VictoryClaimAccepted,
                TerminalTraceEvent.BlackReached,
                TerminalTraceEvent.StageClearedPublished,
                TerminalTraceEvent.StageResultCreated,
                TerminalTraceEvent.PayloadBound,
                TerminalTraceEvent.DestinationReadyAccepted,
                TerminalTraceEvent.ResultBackdropReady,
                TerminalTraceEvent.ResultBackdropHandoff,
                TerminalTraceEvent.OverlayHidden,
                TerminalTraceEvent.ResultContentEntranceStarted,
                TerminalTraceEvent.ResultInteractionReady,
                TerminalTraceEvent.SessionCompleted,
            };
            var trace = TerminalRuntimeTrace.Snapshot;
            if (TerminalSessionRegistry.IsActive ||
                TerminalSessionRegistry.Current.Phase != TerminalSessionPhase.Completed ||
                installer.ScreenController.CurrentScreenId != ScreenId.StageResult ||
                stageResult == null ||
                !stageResult.IsVisible ||
                !stageResult.IsInteractionReady ||
                stageResult.HandoffCoverAlpha > 0f ||
                stageResult.IsHandoffCoverActive ||
                Mathf.Abs(stageResult.ContentAlpha - 1f) > 0.0001f ||
                !sawIntermediateHandoffAlpha ||
                !sawIntermediateContentAlpha ||
                button == null ||
                !button.gameObject.activeInHierarchy ||
                !button.interactable ||
                irisView.IsVisible ||
                irisView.BlocksRaycasts ||
                irisView.gameObject.activeSelf ||
                irisGroup == null ||
                irisGroup.alpha > 0f ||
                irisGroup.blocksRaycasts ||
                irisGroup.interactable ||
                irisImage == null ||
                irisImage.raycastTarget ||
                installer.Coordinator.CurrentBlockSnapshot.BlocksScreenInteraction ||
                installer.Coordinator.CurrentBlockSnapshot.BlocksLowerLayerPointer ||
                requiredTraceEvents.Any(required => trace.All(record => record.Event != required)) ||
                trace.Any(record => !record.Accepted))
            {
                Fail(
                    $"reveal/session cleanup failed phase={TerminalSessionRegistry.Current.Phase} " +
                    $"screen={installer.ScreenController.CurrentScreenId} stageResult={(stageResult != null)} " +
                    $"button={(button != null)} irisVisible={irisView.IsVisible} irisRaycast={irisView.BlocksRaycasts} " +
                    $"coverIntermediate={sawIntermediateHandoffAlpha} contentIntermediate={sawIntermediateContentAlpha}");
                yield break;
            }

            var eventSystem = EventSystem.current;
            var coordinator = SceneTransitionCoordinator.Instance;
            var sourceHostInstanceId = host.GetInstanceID();
            var sourceOutputCameraInstanceId = host.OutputCamera.GetInstanceID();
            var sourcePlayerEntityId = host.PlayerEntityId;
            var irisShaderName = runtimeMaterial.shader.name;
            var baselineAccepted = coordinator.AcceptedTransitionCount;
            var inputRouter = installer.GetComponent<UiNavigationInputRouter>();
            var baselineSubmit = inputRouter != null
                ? inputRouter.SubmitPerformedCount
                : 0;
            var clickCount = 0;
            button.onClick.AddListener(() => clickCount++);
            _resultInteractionReadyAt = Time.realtimeSinceStartup;
            yield return WaitForInputWindowFocus();
            if (!_inputWindowFocused)
            {
                Fail("terminal input dispatch did not acquire foreground window focus");
                yield break;
            }

            yield return WaitForRenderedInteractionFrame();
            if (!_interactionRenderAcknowledged)
            {
                Fail("terminal input dispatch did not observe a rendered interaction-ready frame");
                yield break;
            }

            if (string.Equals(inputMode, "pointer", StringComparison.OrdinalIgnoreCase))
            {
                if (eventSystem == null || smokeMouse == null)
                {
                    Fail("pointer EventSystem or smoke mouse is missing");
                    yield break;
                }

                var buttonRect = (RectTransform)button.transform;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(
                    null,
                    buttonRect.TransformPoint(buttonRect.rect.center));
                var pointerData = new PointerEventData(eventSystem) { position = screenPoint };
                var raycasts = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, raycasts);
                if (!raycasts.Any(result =>
                        result.gameObject == button.gameObject ||
                        result.gameObject.transform.IsChildOf(button.transform)))
                {
                    Fail(
                        "actual EventSystem raycast did not reach the StageResult button; " +
                        DescribeInputDispatch(
                            installer,
                            stageResult,
                            button,
                            inputMode,
                            screenPoint,
                            raycasts));
                    yield break;
                }

                InputSystem.QueueStateEvent(smokeMouse, new MouseState { position = screenPoint });
                yield return null;
                var pressed = new MouseState { position = screenPoint };
                pressed.WithButton(MouseButton.Left);
                InputSystem.QueueStateEvent(smokeMouse, pressed);
                yield return null;
                InputSystem.QueueStateEvent(smokeMouse, new MouseState { position = screenPoint });
                yield return null;
                deadline = Time.realtimeSinceStartup + InputDispatchTimeoutSeconds;
                while (clickCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }
            else
            {
                if (eventSystem == null ||
                    smokeKeyboard == null ||
                    eventSystem.currentSelectedGameObject != button.gameObject)
                {
                    Fail(
                        $"keyboard selection invalid selected={eventSystem?.currentSelectedGameObject?.name ?? "null"}; " +
                        DescribeInputDispatch(
                            installer,
                            stageResult,
                            button,
                            inputMode,
                            ResolveButtonScreenPoint(button),
                            Raycast(eventSystem, ResolveButtonScreenPoint(button))));
                    yield break;
                }

                InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState(Key.Enter));
                yield return null;
                InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState());
                yield return null;
                deadline = Time.realtimeSinceStartup + InputDispatchTimeoutSeconds;
                while (inputRouter != null &&
                       inputRouter.SubmitPerformedCount == baselineSubmit &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }

            var isPointer = string.Equals(
                inputMode,
                "pointer",
                StringComparison.OrdinalIgnoreCase);
            var inputAccepted =
                isPointer
                    ? clickCount == 1
                    : clickCount == 0 &&
                      inputRouter != null &&
                      inputRouter.SubmitPerformedCount - baselineSubmit == 1 &&
                      inputRouter.LastSubmitDispatchResult;
            if (!inputAccepted ||
                coordinator.AcceptedTransitionCount - baselineAccepted != 1)
            {
                Fail(
                    $"inputMode={inputMode} clickCount={clickCount} " +
                    $"submitCount={inputRouter?.SubmitPerformedCount - baselineSubmit ?? -1} " +
                    $"navigationAccepted={coordinator.AcceptedTransitionCount - baselineAccepted}; " +
                    DescribeInputDispatch(
                        installer,
                        stageResult,
                        button,
                        inputMode,
                        ResolveButtonScreenPoint(button),
                        Raycast(EventSystem.current, ResolveButtonScreenPoint(button))));
                yield break;
            }

            if (!SceneEntryPresentationRegistry.IsActive)
            {
                Fail("accepted next-stage navigation did not claim an entry presentation session");
                yield break;
            }

            var entryToken = SceneEntryPresentationRegistry.Current.Token;
            var persistentCover = FindFirstObjectByType<SceneTransitionOverlayShellView>(
                FindObjectsInactive.Include);
            if (persistentCover == null)
            {
                Fail("persistent transition cover is missing after accepted next-stage navigation");
                yield break;
            }

            var previousPersistentAlpha = persistentCover.PersistentCoverOpacityForTests;
            var sawIntermediatePersistentAlpha =
                previousPersistentAlpha > 0f && previousPersistentAlpha < 0.999f;
            var sawOpaquePersistentFrame = false;
            GameplaySceneHost destinationHost = null;
            GameplayUiFlowInstaller destinationInstaller = null;
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                destinationHost = FindObjectsByType<GameplaySceneHost>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.GetInstanceID() != sourceHostInstanceId);
                destinationInstaller = destinationHost != null
                    ? destinationHost.GetComponent<GameplayUiFlowInstaller>()
                    : null;
                var persistentAlpha = persistentCover.PersistentCoverOpacityForTests;
                if (!sawOpaquePersistentFrame &&
                    persistentAlpha < previousPersistentAlpha - 0.0001f)
                {
                    Fail(
                        $"persistent cover alpha regressed from {previousPersistentAlpha:F6} " +
                        $"to {persistentAlpha:F6}");
                    yield break;
                }

                sawIntermediatePersistentAlpha |=
                    persistentAlpha > 0f && persistentAlpha < 0.999f;
                sawOpaquePersistentFrame |=
                    persistentAlpha >= 0.999f &&
                    persistentCover.HasRenderedOpaqueFrame;
                previousPersistentAlpha = persistentAlpha;
                if (destinationHost != null &&
                    destinationInstaller != null &&
                    SceneEntryPresentationRegistry.Current.Phase ==
                    SceneEntryPresentationPhase.Opening)
                {
                    break;
                }

                yield return null;
            }

            if (destinationHost == null ||
                destinationInstaller == null ||
                !sawIntermediatePersistentAlpha ||
                !sawOpaquePersistentFrame ||
                !persistentCover.HasAcknowledgedOpaqueFrame ||
                SceneEntryPresentationRegistry.Current.Phase !=
                SceneEntryPresentationPhase.Opening)
            {
                Fail(
                    "destination gameplay bootstrap did not reach player-centered entry opening; " +
                    $"phase={SceneEntryPresentationRegistry.Current.Phase} " +
                    $"reason={SceneEntryPresentationRegistry.Current.FailureReason} " +
                    $"persistentAlpha={persistentCover.PersistentCoverOpacityForTests:F6} " +
                    $"persistentIntermediate={sawIntermediatePersistentAlpha}; " +
                    DescribeDestinationProjection(destinationHost));
                yield break;
            }

            var openingTickIndex =
                destinationHost.UiAccess.QueryFacade.Session.Read().NextTickIndex;
            if (destinationHost.InputHost.RunSingleTick() != null ||
                destinationHost.UiAccess.QueryFacade.Session.Read().NextTickIndex !=
                openingTickIndex)
            {
                Fail("gameplay tick was admitted before entry opening completed");
                yield break;
            }

            var entryIris = destinationInstaller.RootView.TerminalIrisOverlayView;
            var entryMaterialVector = entryIris.RuntimeMaterialForTests.GetVector("_Center");
            var entryMaterialCenter = new Vector2(entryMaterialVector.x, entryMaterialVector.y);
            if (!entryIris.IsVisible ||
                !entryIris.BlocksRaycasts ||
                Vector2.Distance(
                    entryMaterialCenter,
                    destinationInstaller.EntryFocusCenterForTests) > 0.0001f)
            {
                Fail(
                    $"entry iris was not closed on the destination player; material={entryMaterialCenter} " +
                    $"captured={destinationInstaller.EntryFocusCenterForTests}");
                yield break;
            }

            for (var i = 0; i < 2; i++)
            {
                yield return null;
                if (destinationHost.UiAccess.QueryFacade.Session.Read().NextTickIndex !=
                    openingTickIndex)
                {
                    Fail("automatic gameplay tick advanced while entry opening owned input");
                    yield break;
                }
            }

            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (SceneEntryPresentationRegistry.IsActive &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (SceneEntryPresentationRegistry.IsActive ||
                SceneEntryPresentationRegistry.Current.Phase !=
                SceneEntryPresentationPhase.Completed ||
                entryIris.IsVisible ||
                entryIris.BlocksRaycasts)
            {
                Fail(
                    $"entry opening did not complete cleanly phase={SceneEntryPresentationRegistry.Current.Phase} " +
                    $"visible={entryIris.IsVisible} raycast={entryIris.BlocksRaycasts}");
                yield break;
            }

            destinationHost.InputHost.SetAutoAdvanceTicks(false);
            var releasedTickIndex =
                destinationHost.UiAccess.QueryFacade.Session.Read().NextTickIndex;
            if (destinationHost.InputHost.RunSingleTick() == null ||
                destinationHost.UiAccess.QueryFacade.Session.Read().NextTickIndex !=
                releasedTickIndex + 1)
            {
                Fail("first gameplay tick was not admitted exactly once after entry opening");
                yield break;
            }

            if (sequentialGameClearScenario)
            {
                _scenarioSequenceIndex = 2;
                yield return RunSequentialGameClearSmoke(
                    destinationInstaller,
                    destinationHost,
                    inputMode,
                    eventSystem);
                yield break;
            }

            Debug.Log(
                $"{SuccessMarker} scenario=stageresult revision={ReadArgumentValue(RevisionArgument)} " +
                $"requestedResolution={ReadArgumentValue(WidthArgument)}x{ReadArgumentValue(HeightArgument)} " +
                $"actualResolution={Screen.width}x{Screen.height} graphicsDevice={SystemInfo.graphicsDeviceType} " +
                $"input={inputMode} inputDispatchCount=1 selectedRoute=StageAdvance " +
                $"selectedPreset=VictoryClose/StageEntryOpen sourceOpaque=true persistentCover=true " +
                $"destinationReady=true destinationClosedIris=true openingCompleted=true inputRelease=true " +
                $"finalDestinationScene={SceneManager.GetActiveScene().name} " +
                $"token={TerminalSessionRegistry.Current.Token} " +
                $"outputCameraInstanceId={sourceOutputCameraInstanceId} playerEntityId={sourcePlayerEntityId} " +
                $"projected={projectedCenter} captured={diagnostics.CapturedCenter} material={materialCenter} " +
                $"fallback={diagnostics.IsFallback} resultHandoff=completed cleanup=nonblocking navigationRequests=1 " +
                $"entryToken={entryToken} entryCenter={entryMaterialCenter} entryOpening=completed firstTick=1 " +
                $"authorityResolver=same-instance sourceStage={sourceStage.StageId.Value} " +
                $"savedNext={savedSlot.CurrentStageId.Value} resultNext=" +
                $"{completionReadModel.NextStageRequest.StageId.Value} " +
                $"shader={irisShaderName}");
            UnityEngine.Application.Quit(0);
        }

        private IEnumerator PrepareMainMenuFixture(
            GameplayUiFlowInstaller sourceInstaller)
        {
            if (sourceInstaller == null ||
                !sourceInstaller.Coordinator.TryReturnToMainMenu())
            {
                Fail("Main Menu fixture could not enter the production return route");
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (MainMenuEntryPresentationRegistry.IsActive &&
                   Time.realtimeSinceStartup < deadline)
            {
                MaintainRequestedResolution();
                if (MainMenuEntryPresentationRegistry.Current.Phase ==
                    SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    Fail(
                        $"Main Menu fixture return failed: " +
                        $"{MainMenuEntryPresentationRegistry.Current.FailureReason}");
                    yield break;
                }

                yield return null;
            }

            var destination = FindFirstObjectByType<MainMenuUiFlowInstaller>(
                FindObjectsInactive.Include);
            if (MainMenuEntryPresentationRegistry.IsActive ||
                MainMenuEntryPresentationRegistry.Current.Phase !=
                SceneEntryPresentationPhase.Completed ||
                destination == null ||
                destination.IsGameplayEntryInteractionBlocked ||
                destination.MainMenuScreenView == null ||
                !destination.MainMenuScreenView.CanHandleUiNavigation)
            {
                Fail(
                    $"Main Menu fixture did not complete production destination readiness " +
                    $"phase={MainMenuEntryPresentationRegistry.Current.Phase} " +
                    $"destination={(destination != null)}");
            }
        }

        private IEnumerator RunMainMenuGameplaySmoke(
            int requestedWidth,
            int requestedHeight)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            MainMenuUiFlowInstaller mainMenu = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                MaintainRequestedResolution();
                mainMenu = FindFirstObjectByType<MainMenuUiFlowInstaller>(
                    FindObjectsInactive.Include);
                if (mainMenu != null &&
                    mainMenu.Controller != null &&
                    mainMenu.HubController != null &&
                    mainMenu.MainMenuScreenView != null &&
                    mainMenu.MainMenuScreenView.CanHandleUiNavigation)
                {
                    break;
                }

                yield return null;
            }

            if (mainMenu == null ||
                mainMenu.Controller == null ||
                mainMenu.MainMenuScreenView == null)
            {
                Fail("production Main Menu interaction did not become ready");
                yield break;
            }

            var expectedIntentValue = ReadArgumentValue(
                CampaignExpectedIntentArgument);
            var expectedStageValue = ReadArgumentValue(
                CampaignExpectedStageArgument);
            if (!Enum.TryParse(
                    expectedIntentValue,
                    ignoreCase: true,
                    out SaveSlotIntentKind expectedIntent) ||
                expectedIntent != SaveSlotIntentKind.NewGame &&
                expectedIntent != SaveSlotIntentKind.Continue ||
                !StageId.TryCreate(expectedStageValue, out var expectedStageId))
            {
                Fail(
                    $"Main Menu campaign expectation is invalid intent=" +
                    $"'{expectedIntentValue}' stage='{expectedStageValue}'");
                yield break;
            }

            var mainMenuResolver =
                mainMenu.CampaignStageSequenceResolverForDiagnostics;
            if (mainMenuResolver == null ||
                !mainMenuResolver.Contains(expectedStageId) ||
                expectedIntent == SaveSlotIntentKind.NewGame &&
                !mainMenuResolver.FirstStageId.Equals(expectedStageId))
            {
                Fail(
                    $"Main Menu authoritative resolver mismatch resolver=" +
                    $"{(mainMenuResolver != null)} expected={expectedStageId.Value} " +
                    $"first={mainMenuResolver?.FirstStageId.Value ?? string.Empty}");
                yield break;
            }

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Fail("Main Menu route rejected Null graphics device");
                yield break;
            }

            deadline = Time.realtimeSinceStartup + RenderEnvironmentTimeoutSeconds;
            while ((Screen.width != requestedWidth ||
                    Screen.height != requestedHeight ||
                    !FindObjectsByType<Camera>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .Any(camera =>
                            camera.isActiveAndEnabled &&
                            RenderedCameraInstanceIds.Contains(camera.GetInstanceID()))) &&
                   Time.realtimeSinceStartup < deadline)
            {
                MaintainRequestedResolution();
                yield return null;
            }

            if (Screen.width != requestedWidth || Screen.height != requestedHeight)
            {
                Fail(
                    $"Main Menu render resolution mismatch requested=" +
                    $"{requestedWidth}x{requestedHeight} actual={Screen.width}x{Screen.height}");
                yield break;
            }

            if (!TryValidateSingleActiveEventSystem(out var eventSystemDiagnostic))
            {
                Fail($"Main Menu interaction owner invalid: {eventSystemDiagnostic}");
                yield break;
            }

            var screen = mainMenu.MainMenuScreenView;
            var startButton = FindNamedButton(screen, "StartButton");
            if (startButton == null || !startButton.interactable)
            {
                Fail("Main Menu Start button is not interactable");
                yield break;
            }

            var startClickCount = 0;
            startButton.onClick.AddListener(() => startClickCount++);
            yield return DispatchPointerClick(
                startButton,
                screen,
                gameplayInstaller: null,
                "Main Menu Start");
            if (!_pointerDispatchSucceeded || startClickCount != 1)
            {
                Fail($"Main Menu Start pointer dispatch count was {startClickCount}");
                yield break;
            }

            var panel = screen.SaveSlotPanel;
            deadline = Time.realtimeSinceStartup + InputDispatchTimeoutSeconds;
            while ((panel == null || !panel.gameObject.activeInHierarchy) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            var card = panel?.SlotCards?.FirstOrDefault(candidate =>
                candidate != null && candidate.CanFocusPrimary);
            var primaryButton = FindNamedButton(card, "PrimaryButton");
            if (card == null || primaryButton == null || !primaryButton.interactable)
            {
                Fail("Main Menu has no interaction-ready New Game or Continue primary button");
                yield break;
            }

            var expectedCard = mainMenu.Controller.BuildViewModel().SlotCards
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.PrimaryIntentKind != SaveSlotIntentKind.None);
            if (expectedCard == null ||
                expectedCard.PrimaryIntentKind != expectedIntent ||
                expectedIntent == SaveSlotIntentKind.Continue &&
                string.IsNullOrWhiteSpace(expectedCard.StageText))
            {
                Fail(
                    $"Main Menu campaign card mismatch expectedIntent={expectedIntent} " +
                    $"actualIntent={expectedCard?.PrimaryIntentKind} " +
                    $"stageText='{expectedCard?.StageText ?? string.Empty}'");
                yield break;
            }

            var primaryClickCount = 0;
            var routeDispatchCount = 0;
            var selectedIntent = SaveSlotIntentKind.None;
            primaryButton.onClick.AddListener(() => primaryClickCount++);
            card.IntentRequested += intent =>
            {
                routeDispatchCount++;
                selectedIntent = intent.IntentKind;
            };
            var acceptedBefore = SceneTransitionCoordinator.Instance.AcceptedTransitionCount;
            yield return DispatchPointerClick(
                primaryButton,
                card,
                gameplayInstaller: null,
                "Main Menu slot primary");
            var entryIntent = SceneEntryPresentationRegistry.Current.TransitionIntent;
            if (!_pointerDispatchSucceeded ||
                primaryClickCount != 1 ||
                routeDispatchCount != 1 ||
                selectedIntent != expectedIntent ||
                !SceneEntryPresentationRegistry.IsActive ||
                entryIntent != SceneTransitionIntent.GameplayEntry &&
                entryIntent != SceneTransitionIntent.CinematicToGameplay)
            {
                Fail(
                    $"Main Menu route dispatch invalid click={primaryClickCount} " +
                    $"callback={routeDispatchCount} intent={selectedIntent} " +
                    $"entryActive={SceneEntryPresentationRegistry.IsActive} " +
                    $"entryIntent={entryIntent} " +
                    $"accepted={SceneTransitionCoordinator.Instance.AcceptedTransitionCount - acceptedBefore}");
                yield break;
            }

            var introSkipDispatchCount = 0;
            if (entryIntent == SceneTransitionIntent.CinematicToGameplay)
            {
                CinematicVideoOverlayView cinematic = null;
                Button cinematicSkipButton = null;
                deadline = Time.realtimeSinceStartup + TimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    cinematic = FindFirstObjectByType<CinematicVideoOverlayView>(
                        FindObjectsInactive.Include);
                    cinematicSkipButton = FindNamedButton(cinematic, "Background");
                    if (cinematic != null &&
                        cinematic.IsPlaying &&
                        cinematic.CurrentPresentationState ==
                            CinematicPresentationState.Playing &&
                        cinematicSkipButton != null &&
                        cinematicSkipButton.interactable)
                    {
                        break;
                    }

                    yield return null;
                }

                if (cinematic == null ||
                    !cinematic.IsPlaying ||
                    cinematicSkipButton == null ||
                    !cinematicSkipButton.interactable)
                {
                    Fail("Main Menu intro cinematic did not expose its production skip target");
                    yield break;
                }

                cinematicSkipButton.onClick.AddListener(() => introSkipDispatchCount++);
                yield return DispatchPointerClick(
                    cinematicSkipButton,
                    cinematic,
                    gameplayInstaller: null,
                    "Main Menu intro cinematic skip");
                if (!_pointerDispatchSucceeded || introSkipDispatchCount != 1)
                {
                    Fail(
                        $"Main Menu intro cinematic skip dispatch count was " +
                        $"{introSkipDispatchCount}, expected exactly one");
                    yield break;
                }

                deadline = Time.realtimeSinceStartup + TimeoutSeconds;
                while (SceneTransitionCoordinator.Instance.AcceptedTransitionCount -
                           acceptedBefore != 1 &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }

            if (SceneTransitionCoordinator.Instance.AcceptedTransitionCount -
                acceptedBefore != 1)
            {
                Fail(
                    $"Main Menu gameplay transition was not accepted intent={entryIntent} " +
                    $"accepted={SceneTransitionCoordinator.Instance.AcceptedTransitionCount - acceptedBefore}");
                yield break;
            }

            yield return AwaitGameplayEntryCompletion(
                sourceHostInstanceId: 0,
                expectedIntent: entryIntent,
                expectedSourceKind: entryIntent == SceneTransitionIntent.CinematicToGameplay
                    ? GameplayEntrySourceCloseVisualKind.CinematicOpaqueOwner
                    : GameplayEntrySourceCloseVisualKind.MainMenuIris,
                sourceIris: null,
                sourceRootName: entryIntent == SceneTransitionIntent.GameplayEntry
                    ? "MainMenuGameplayEntryIrisSource"
                    : string.Empty);
            if (!_gameplayEntryCompletionSucceeded)
            {
                yield break;
            }

            if (routeDispatchCount != 1 || primaryClickCount != 1 ||
                !TryValidateSingleActiveEventSystem(out eventSystemDiagnostic))
            {
                Fail(
                    $"Main Menu stale callback/EventSystem cleanup failed " +
                    $"click={primaryClickCount} callback={routeDispatchCount} " +
                    $"{eventSystemDiagnostic}");
                yield break;
            }

            var destinationHost = FindFirstObjectByType<GameplaySceneHost>(
                FindObjectsInactive.Exclude);
            var destinationStage =
                destinationHost?.UiAccess?.QueryFacade.Stage.Read() ?? default;
            var destinationResolver =
                destinationHost?.UiAccess?.CampaignStageSequenceResolver;
            var destinationProvider = destinationHost?.GetComponents<MonoBehaviour>()
                .OfType<ICampaignStageSequenceResolverProvider>()
                .SingleOrDefault();
            var destinationSameInstance = destinationProvider != null &&
                destinationProvider.TryCreateCampaignStageSequenceResolver(
                    out var destinationProviderResolver) &&
                ReferenceEquals(destinationResolver, destinationProviderResolver);
            var savedSlot = CampaignSaveCompositionProvider
                .CreateProductionProfileBacked()
                .LoadSlot(expectedCard.SlotNumber);
            if (destinationHost == null ||
                destinationResolver == null ||
                !destinationSameInstance ||
                !destinationStage.StageId.Equals(expectedStageId) ||
                string.IsNullOrWhiteSpace(destinationStage.DisplayNameKey) ||
                !savedSlot.CurrentStageId.Equals(expectedStageId) ||
                !string.Equals(
                    savedSlot.CurrentLevelGroupId,
                    destinationResolver.GetLevelGroupId(expectedStageId),
                    StringComparison.Ordinal) ||
                savedSlot.CampaignCompleted)
            {
                Fail(
                    $"Main Menu campaign launch divergence intent={expectedIntent} " +
                    $"expected={expectedStageId.Value} " +
                    $"destination={destinationStage.StageId.Value} " +
                    $"saved={savedSlot.CurrentStageId.Value} " +
                    $"group={savedSlot.CurrentLevelGroupId} " +
                    $"sameInstance={destinationSameInstance} " +
                    $"displayKey={destinationStage.DisplayNameKey}");
                yield break;
            }

            _authoritySourceStageId = expectedStageId;
            _authorityExpectedNextStageId = StageId.None;
            _authoritySavedStageId = savedSlot.CurrentStageId;
            _authorityResultNextStageId = StageId.None;
            _authorityCampaignCompleted = savedSlot.CampaignCompleted;

            LogRouteSuccess(
                "GameplayEntryClose",
                selectedIntent.ToString(),
                routeDispatchCount,
                SceneManager.GetActiveScene().name,
                $"campaignIntent={selectedIntent} " +
                $"campaignStage={destinationStage.StageId.Value} " +
                $"campaignGroup={savedSlot.CurrentLevelGroupId} " +
                $"presentationKey={destinationStage.DisplayNameKey} " +
                $"entryIntent={entryIntent} introSkipDispatchCount={introSkipDispatchCount} " +
                "mainMenuResolver=true gameplayResolverSameInstance=true");
        }

        private IEnumerator RunPauseRetrySmoke(
            GameplayUiFlowInstaller installer,
            GameplaySceneHost host)
        {
            if (!TryInstallUiAudioRecorder(
                    installer,
                    out var audioRecorder,
                    out var recorderFailure))
            {
                Fail($"Pause Retry audio diagnostics installation failed: {recorderFailure}");
                yield break;
            }

            if (!installer.Coordinator.RequestPausePopup())
            {
                Fail("Pause Retry fixture could not open the production Pause popup");
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + InputDispatchTimeoutSeconds;
            PausePopupView pause = null;
            Button retryButton = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                pause = installer.PausePopupView;
                retryButton = FindNamedButton(pause, "RetryButton");
                if (pause != null &&
                    pause.IsVisible &&
                    pause.CanHandleUiNavigation &&
                    retryButton != null &&
                    retryButton.interactable)
                {
                    break;
                }

                yield return null;
            }

            if (pause == null || retryButton == null || !retryButton.interactable)
            {
                Fail("Pause Retry button did not become interaction-ready");
                yield break;
            }

            var clickCount = 0;
            var retryDispatchCount = 0;
            retryButton.onClick.AddListener(() => clickCount++);
            pause.CompletionRequested += completion =>
            {
                if (completion == PopupCompletionKind.RetryRequested)
                {
                    retryDispatchCount++;
                }
            };
            var traceCountBefore = TerminalRuntimeTrace.Snapshot.Count;
            var sourceHostId = host.GetInstanceID();
            var acceptedBefore = SceneTransitionCoordinator.Instance.AcceptedTransitionCount;
            yield return DispatchPointerClick(retryButton, pause, installer, "Pause Retry");
            if (!_pointerDispatchSucceeded ||
                clickCount != 1 ||
                retryDispatchCount != 1 ||
                !SceneEntryPresentationRegistry.IsActive ||
                SceneEntryPresentationRegistry.Current.TransitionIntent !=
                SceneTransitionIntent.ManualRetry ||
                SceneTransitionCoordinator.Instance.AcceptedTransitionCount -
                acceptedBefore != 1)
            {
                Fail(
                    $"Pause Retry dispatch invalid click={clickCount} callback={retryDispatchCount} " +
                    $"entryActive={SceneEntryPresentationRegistry.IsActive} " +
                    $"intent={SceneEntryPresentationRegistry.Current.TransitionIntent}");
                yield break;
            }

            yield return AwaitGameplayEntryCompletion(
                sourceHostId,
                SceneTransitionIntent.ManualRetry,
                GameplayEntrySourceCloseVisualKind.RetryIris,
                installer.RootView.TerminalIrisOverlayView,
                sourceRootName: string.Empty);
            if (!_gameplayEntryCompletionSucceeded)
            {
                yield break;
            }

            if (clickCount != 1 ||
                retryDispatchCount != 1 ||
                TerminalRuntimeTrace.Snapshot.Count != traceCountBefore ||
                audioRecorder.Count(UiAudioCueId.ChanceLoss) != 0 ||
                audioRecorder.Count(UiAudioCueId.LastChance) != 0 ||
                audioRecorder.Count(UiAudioCueId.LevelFailed) != 0 ||
                Time.timeScale != 1f)
            {
                Fail(
                    $"Pause Retry duplicated terminal/audio ownership click={clickCount} " +
                    $"callback={retryDispatchCount} traceDelta=" +
                    $"{TerminalRuntimeTrace.Snapshot.Count - traceCountBefore} " +
                    $"chanceLoss={audioRecorder.Count(UiAudioCueId.ChanceLoss)} " +
                    $"lastChance={audioRecorder.Count(UiAudioCueId.LastChance)} " +
                    $"levelFailed={audioRecorder.Count(UiAudioCueId.LevelFailed)} " +
                    $"timeScale={Time.timeScale:F3}");
                yield break;
            }

            LogRouteSuccess(
                "RetryClose",
                "ManualRetry",
                retryDispatchCount,
                SceneManager.GetActiveScene().name,
                "defeatClose=unused terminalDeathCue=0 defeatHold=0");
        }

        private IEnumerator AwaitGameplayEntryCompletion(
            int sourceHostInstanceId,
            SceneTransitionIntent expectedIntent,
            GameplayEntrySourceCloseVisualKind expectedSourceKind,
            TerminalIrisOverlayView sourceIris,
            string sourceRootName)
        {
            _gameplayEntryCompletionSucceeded = false;
            _sourceOpaqueAcknowledged = false;
            _persistentCoverAcknowledged = false;
            _destinationReadyAcknowledged = false;
            _destinationClosedIrisAcknowledged = false;
            _openingCompleted = false;
            _inputReleased = false;

            var claimed = SceneEntryPresentationRegistry.Current;
            if (!claimed.IsActive || claimed.TransitionIntent != expectedIntent)
            {
                Fail(
                    $"entry session claim mismatch active={claimed.IsActive} " +
                    $"intent={claimed.TransitionIntent} expected={expectedIntent}");
                yield break;
            }

            var cinematicHandoffToken =
                expectedSourceKind ==
                GameplayEntrySourceCloseVisualKind.CinematicOpaqueOwner
                    ? CinematicOpaqueHandoffRegistry.Current.Token
                    : default;

            var visual = GameplayEntryTransitionVisualSnapshotRegistry.Require(
                claimed.Token,
                expectedIntent);
            if (visual.SourceCloseVisualKind != expectedSourceKind ||
                !visual.RequireSourceOpaqueRenderAcknowledgement ||
                !visual.RequireDestinationRenderAcknowledgement ||
                visual.InputReleasePolicy !=
                GameplayEntryInputReleasePolicy.OpeningCompleted)
            {
                Fail(
                    $"entry visual contract mismatch source={visual.SourceCloseVisualKind} " +
                    $"expected={expectedSourceKind}");
                yield break;
            }

            SceneTransitionOverlayShellView persistentCover = null;
            GameplaySceneHost destinationHost = null;
            GameplayUiFlowInstaller destinationInstaller = null;
            var sawOpening = false;
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                MaintainRequestedResolution();
                if (sourceIris == null && !string.IsNullOrEmpty(sourceRootName))
                {
                    sourceIris = FindObjectsByType<GameplayUiCanvasRootView>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None)
                        .FirstOrDefault(root => root.name == sourceRootName)
                        ?.TerminalIrisOverlayView;
                }

                _sourceOpaqueAcknowledged |=
                    sourceIris != null && sourceIris.HasRenderedEntryClosedFrame;
                var cinematicHandoff = CinematicOpaqueHandoffRegistry.Current;
                _sourceOpaqueAcknowledged |=
                    cinematicHandoffToken.IsValid &&
                    cinematicHandoff.Token == cinematicHandoffToken &&
                    (cinematicHandoff.Phase ==
                         CinematicOpaqueHandoffPhase.CinematicOpaqueRendered ||
                     cinematicHandoff.Phase ==
                         CinematicOpaqueHandoffPhase.PersistentCoverRendered ||
                     cinematicHandoff.Phase ==
                         CinematicOpaqueHandoffPhase.Released);
                persistentCover ??=
                    FindFirstObjectByType<SceneTransitionOverlayShellView>(
                        FindObjectsInactive.Include);
                _persistentCoverAcknowledged |=
                    persistentCover != null &&
                    persistentCover.HasRenderedOpaqueFrame &&
                    persistentCover.HasAcknowledgedOpaqueFrame;

                destinationHost = FindObjectsByType<GameplaySceneHost>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        sourceHostInstanceId <= 0 ||
                        candidate.GetInstanceID() != sourceHostInstanceId);
                destinationInstaller = destinationHost != null
                    ? destinationHost.GetComponent<GameplayUiFlowInstaller>()
                    : null;
                var session = SceneEntryPresentationRegistry.Current;
                if (destinationHost != null &&
                    destinationInstaller != null &&
                    session.IsActive &&
                    session.Token == claimed.Token &&
                    session.Phase == SceneEntryPresentationPhase.Opening)
                {
                    _destinationReadyAcknowledged =
                        destinationHost.OutputCamera != null &&
                        destinationHost.PlayerEntityId > 0 &&
                        destinationInstaller.IsInstalledForDiagnostics;
                    var entryIris =
                        destinationInstaller.RootView.TerminalIrisOverlayView;
                    _destinationClosedIrisAcknowledged |=
                        entryIris.HasRenderedEntryClosedFrame &&
                        entryIris.IsVisible &&
                        entryIris.BlocksRaycasts;
                    if (!sawOpening)
                    {
                        sawOpening = true;
                        var openingTickIndex = destinationHost.UiAccess
                            .QueryFacade.Session.Read().NextTickIndex;
                        if (destinationHost.InputHost.RunSingleTick() != null ||
                            destinationHost.UiAccess.QueryFacade.Session.Read()
                                .NextTickIndex != openingTickIndex)
                        {
                            Fail("gameplay input was admitted before StageEntryOpen completed");
                            yield break;
                        }
                    }
                }

                if (!session.IsActive &&
                    session.Token == claimed.Token &&
                    session.Phase == SceneEntryPresentationPhase.Completed)
                {
                    _openingCompleted = true;
                    break;
                }

                if (session.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    Fail(
                        $"entry destination failed phase={session.Phase} " +
                        $"reason={session.FailureReason}");
                    yield break;
                }

                yield return null;
            }

            if (destinationHost == null ||
                destinationInstaller == null ||
                !_sourceOpaqueAcknowledged ||
                !_persistentCoverAcknowledged ||
                !_destinationReadyAcknowledged ||
                !_destinationClosedIrisAcknowledged ||
                !_openingCompleted)
            {
                Fail(
                    $"entry lifecycle incomplete sourceOpaque={_sourceOpaqueAcknowledged} " +
                    $"persistent={_persistentCoverAcknowledged} " +
                    $"destinationReady={_destinationReadyAcknowledged} " +
                    $"destinationClosed={_destinationClosedIrisAcknowledged} " +
                    $"opening={_openingCompleted} phase={SceneEntryPresentationRegistry.Current.Phase}; " +
                    DescribeDestinationProjection(destinationHost));
                yield break;
            }

            destinationHost.InputHost.SetAutoAdvanceTicks(false);
            var releasedTickIndex = destinationHost.UiAccess.QueryFacade
                .Session.Read().NextTickIndex;
            _inputReleased =
                destinationHost.InputHost.RunSingleTick() != null &&
                destinationHost.UiAccess.QueryFacade.Session.Read()
                    .NextTickIndex == releasedTickIndex + 1;
            if (!_inputReleased)
            {
                Fail("gameplay input was not released exactly once after StageEntryOpen");
                yield break;
            }

            _gameplayEntryCompletionSucceeded = true;
        }

        private IEnumerator DispatchPointerClick(
            Button button,
            Component view,
            GameplayUiFlowInstaller gameplayInstaller,
            string routeLabel)
        {
            _pointerDispatchSucceeded = false;
            _resultInteractionReadyAt = Time.realtimeSinceStartup;
            yield return WaitForInputWindowFocus();
            if (!_inputWindowFocused)
            {
                Fail($"{routeLabel} pointer dispatch did not acquire foreground focus");
                yield break;
            }

            yield return WaitForRenderedInteractionFrame();
            if (!_interactionRenderAcknowledged)
            {
                Fail($"{routeLabel} pointer dispatch did not observe a rendered UI frame");
                yield break;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null || smokeMouse == null || button == null)
            {
                Fail($"{routeLabel} pointer input owner is missing");
                yield break;
            }

            var screenPoint = ResolveButtonScreenPoint(button);
            var raycasts = Raycast(eventSystem, screenPoint);
            if (!raycasts.Any(result =>
                    result.gameObject == button.gameObject ||
                    result.gameObject.transform.IsChildOf(button.transform)))
            {
                Fail(
                    $"{routeLabel} EventSystem raycast did not reach the production button; " +
                    DescribeInputDispatch(
                        gameplayInstaller,
                        view,
                        button,
                        "pointer",
                        screenPoint,
                        raycasts));
                yield break;
            }

            InputSystem.QueueStateEvent(
                smokeMouse,
                new MouseState { position = screenPoint });
            yield return null;
            var pressed = new MouseState { position = screenPoint };
            pressed.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(smokeMouse, pressed);
            yield return null;
            InputSystem.QueueStateEvent(
                smokeMouse,
                new MouseState { position = screenPoint });
            yield return null;
            _pointerDispatchSucceeded = true;
        }

        private static Button FindNamedButton(Component owner, string buttonName)
        {
            return owner == null
                ? null
                : owner.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == buttonName);
        }

        private static bool TryValidateSingleActiveEventSystem(out string diagnostic)
        {
            var eventSystems = FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var active = eventSystems.Where(candidate =>
                    candidate != null &&
                    candidate.enabled &&
                    candidate.gameObject.activeInHierarchy)
                .ToArray();
            var activeModules = active
                .SelectMany(candidate =>
                    candidate.GetComponents<InputSystemUIInputModule>())
                .Count(module =>
                    module != null &&
                    module.enabled &&
                    module.gameObject.activeInHierarchy);
            diagnostic =
                $"eventSystems={eventSystems.Length} activeEventSystems={active.Length} " +
                $"activeModules={activeModules} current={EventSystem.current?.GetInstanceID() ?? 0}";
            return active.Length == 1 &&
                   activeModules == 1 &&
                   EventSystem.current == active[0];
        }

        private void LogRouteSuccess(
            string selectedPreset,
            string selectedRoute,
            int dispatchCount,
            string finalScene,
            string extra = "",
            string inputMode = "pointer")
        {
            Debug.Log(
                $"{SuccessMarker} scenario={_scenarioId} " +
                $"revision={ReadArgumentValue(RevisionArgument)} " +
                $"requestedResolution={ReadArgumentValue(WidthArgument)}x" +
                $"{ReadArgumentValue(HeightArgument)} actualResolution={Screen.width}x{Screen.height} " +
                $"graphicsDevice={SystemInfo.graphicsDeviceType} input={inputMode} " +
                $"inputDispatchCount={dispatchCount} selectedRoute={selectedRoute} " +
                $"selectedPreset={selectedPreset} sourceOpaque={_sourceOpaqueAcknowledged} " +
                $"persistentCover={_persistentCoverAcknowledged} " +
                $"destinationReady={_destinationReadyAcknowledged} " +
                $"destinationClosedIris={_destinationClosedIrisAcknowledged} " +
                $"openingCompleted={_openingCompleted} inputRelease={_inputReleased} " +
                $"finalDestinationScene={finalScene} " +
                $"authoritySource={_authoritySourceStageId.Value} " +
                $"authorityExpectedNext={_authorityExpectedNextStageId.Value} " +
                $"authorityResultNext={_authorityResultNextStageId.Value} " +
                $"authoritySaved={_authoritySavedStageId.Value} " +
                $"authorityCompleted={_authorityCampaignCompleted} {extra}".TrimEnd());
            UnityEngine.Application.Quit(0);
        }

        private IEnumerator RunSequentialGameClearSmoke(
            GameplayUiFlowInstaller installer,
            GameplaySceneHost host,
            string inputMode,
            EventSystem previousEventSystem)
        {
            var eventSystems = FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var activeEventSystems = eventSystems.Where(candidate =>
                    candidate != null &&
                    candidate.enabled &&
                    candidate.gameObject.activeInHierarchy)
                .ToArray();
            if (previousEventSystem != null ||
                activeEventSystems.Length != 1 ||
                EventSystem.current != activeEventSystems[0])
            {
                Fail(
                    "sequential GameClear scenario did not isolate the previous EventSystem; " +
                    $"previousDestroyed={previousEventSystem == null} total={eventSystems.Length} " +
                    $"active={activeEventSystems.Length} currentId={EventSystem.current?.GetInstanceID() ?? 0}");
                yield break;
            }

            if (host.OutputCamera == null ||
                host.PlayerEntityId <= 0 ||
                !host.ViewRegistry.TryGetView(host.PlayerEntityId, out var playerView) ||
                playerView == null)
            {
                Fail("sequential GameClear destination player projection owner is missing");
                yield break;
            }

            MoveViewToViewport(host.OutputCamera, playerView, new Vector2(0.2f, 0.5f));
            if (!installer.TryForceClearCurrentStageForDiagnostics(out var forceClearMessage) ||
                !installer.TryGetTerminalTransitionPort(out var transitionPort) ||
                transitionPort is not GameplayTerminalTransitionPort productionPort ||
                productionPort.CurrentPlayback == null)
            {
                Fail($"sequential production final victory failed: {forceClearMessage}");
                yield break;
            }

            var finalResolver = host.UiAccess?.CampaignStageSequenceResolver;
            var finalStage = host.UiAccess?.QueryFacade.Stage.Read() ?? default;
            var finalReadModel =
                host.UiAccess?.PresentationFeed.CurrentMinimalStageCompletion;
            var finalSavedSlot = new SaveSlotStore(
                    EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                    EditorDirectPlayContextStore.TempActiveSlotProviderKey)
                .LoadSlot(1);
            if (finalResolver == null ||
                !finalResolver.IsFinal(finalStage.StageId) ||
                finalReadModel == null ||
                finalReadModel.NextStageRequest.IsValid ||
                !finalSavedSlot.CurrentStageId.Equals(finalStage.StageId) ||
                !finalSavedSlot.CampaignCompleted)
            {
                Fail(
                    $"sequential final campaign divergence stage={finalStage.StageId.Value} " +
                    $"isFinal={finalResolver?.IsFinal(finalStage.StageId)} " +
                    $"resultNext={finalReadModel?.NextStageRequest.StageId.Value ?? string.Empty} " +
                    $"saved={finalSavedSlot.CurrentStageId.Value} " +
                    $"completed={finalSavedSlot.CampaignCompleted}");
                yield break;
            }

            _authoritySourceStageId = finalStage.StageId;
            _authorityExpectedNextStageId = StageId.None;
            _authoritySavedStageId = finalSavedSlot.CurrentStageId;
            _authorityResultNextStageId = StageId.None;
            _authorityCampaignCompleted = true;

            var irisView = installer.RootView.TerminalIrisOverlayView;
            var previousHandoffAlpha = 1f;
            var previousContentAlpha = 0f;
            var sawIntermediateHandoffAlpha = false;
            var sawIntermediateContentAlpha = false;
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (TerminalSessionRegistry.IsActive && Time.realtimeSinceStartup < deadline)
            {
                var activeResult = installer.GameClearScreenView as IResultTransitionScreenView;
                if (activeResult != null)
                {
                    if (activeResult.HandoffCoverAlpha > previousHandoffAlpha + 0.0001f ||
                        activeResult.ContentAlpha < previousContentAlpha - 0.0001f)
                    {
                        Fail(
                            $"sequential GameClear non-monotonic result alpha " +
                            $"cover={activeResult.HandoffCoverAlpha:F6} previousCover={previousHandoffAlpha:F6} " +
                            $"content={activeResult.ContentAlpha:F6} previousContent={previousContentAlpha:F6}");
                        yield break;
                    }

                    sawIntermediateHandoffAlpha |=
                        activeResult.HandoffCoverAlpha > 0f &&
                        activeResult.HandoffCoverAlpha < 0.999f;
                    sawIntermediateContentAlpha |=
                        activeResult.ContentAlpha > 0f &&
                        activeResult.ContentAlpha < 0.999f;
                    previousHandoffAlpha = activeResult.HandoffCoverAlpha;
                    previousContentAlpha = activeResult.ContentAlpha;
                }

                yield return null;
            }

            yield return RunGameClearSmoke(
                installer,
                irisView,
                inputMode,
                sawIntermediateHandoffAlpha,
                sawIntermediateContentAlpha);
        }

        private IEnumerator RunDefeatSmoke(
            GameplayUiFlowInstaller installer,
            GameplaySceneHost host,
            int initialChances,
            bool restartAfterLevelFailed)
        {
            var saveStore = new SaveSlotStore(
                EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            var initialSlot = saveStore.LoadSlot(1);
            if (initialSlot.RemainingChances != initialChances)
            {
                Fail(
                    $"defeat bootstrap chance mismatch expected={initialChances} " +
                    $"actual={initialSlot.RemainingChances}");
                yield break;
            }

            if (!TryInstallUiAudioRecorder(installer, out var audioRecorder, out var audioRecorderFailure))
            {
                Fail($"defeat UI audio diagnostics installation failed: {audioRecorderFailure}");
                yield break;
            }

            var sourceHostInstanceId = host.GetInstanceID();
            var sourceInstallerInstanceId = installer.GetInstanceID();
            var result = CreateLethalPlayerTickResult(host.PlayerEntityId);
            host.Presenter.Present(result);
            yield return null;

            if (!TryReadPlayerDamageSfxDiagnostics(
                    host.Presenter,
                    out var hurtPlanned,
                    out var hurtRequested,
                    out var hurtSucceeded,
                    out var hurtDuplicateSuppressed,
                    out var hurtFailure) ||
                hurtPlanned != 1 ||
                hurtRequested != 1 ||
                hurtSucceeded != 1 ||
                hurtDuplicateSuppressed != 0)
            {
                Fail(
                    $"lethal hurt audio was not emitted exactly once planned={hurtPlanned} " +
                    $"requested={hurtRequested} succeeded={hurtSucceeded} " +
                    $"duplicateSuppressed={hurtDuplicateSuppressed} reason={hurtFailure}");
                yield break;
            }

            var presentationFeed = host.UiAccess?.PresentationFeed;
            var handleTickCompleted = presentationFeed?.GetType().GetMethod(
                "HandleTickCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(TickResult) },
                null);
            if (handleTickCompleted == null)
            {
                Fail("canonical gameplay presentation feed death dispatch seam is unavailable");
                yield break;
            }

            try
            {
                handleTickCompleted.Invoke(presentationFeed, new object[] { result });
            }
            catch (TargetInvocationException exception)
            {
                Fail($"canonical defeat dispatch threw: {exception.InnerException?.Message ?? exception.Message}");
                yield break;
            }

            var expectedRemainingChances = Math.Max(0, initialChances - 1);
            var hudSuppressionObserved = false;
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && installer != null)
            {
                var chanceViewModel = installer.HudView?.ChancePanelView?.ViewModel;
                if (chanceViewModel != null &&
                    chanceViewModel.RemainingChances == expectedRemainingChances &&
                    chanceViewModel.AnimationHint.AudioCuePolicy ==
                    Game.Feature.UI.HUD.ChanceChangeAudioCuePolicy.Suppress)
                {
                    hudSuppressionObserved = true;
                    break;
                }

                yield return null;
            }

            if (!hudSuppressionObserved)
            {
                Fail(
                    $"terminal HUD chance cue suppression was not observed for " +
                    $"{initialChances}->{expectedRemainingChances}");
                yield break;
            }

            GameplayUiFlowInstaller destinationInstaller = null;
            GameplaySceneHost destinationHost = null;
            if (initialChances > 1)
            {
                deadline = Time.realtimeSinceStartup + TimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    destinationInstaller = FindFirstObjectByType<GameplayUiFlowInstaller>(
                        FindObjectsInactive.Include);
                    destinationHost = FindFirstObjectByType<GameplaySceneHost>(
                        FindObjectsInactive.Include);
                    if (destinationInstaller != null &&
                        destinationHost != null &&
                        destinationInstaller.GetInstanceID() != sourceInstallerInstanceId &&
                        destinationHost.GetInstanceID() != sourceHostInstanceId &&
                        destinationInstaller.IsInstalledForDiagnostics &&
                        !TerminalSessionRegistry.IsActive &&
                        !SceneEntryPresentationRegistry.IsActive)
                    {
                        break;
                    }

                    yield return null;
                }

                if (destinationInstaller == null ||
                    destinationHost == null ||
                    destinationInstaller.GetInstanceID() == sourceInstallerInstanceId ||
                    destinationHost.GetInstanceID() == sourceHostInstanceId ||
                    destinationInstaller.ScreenController.CurrentScreenId != ScreenId.Gameplay ||
                    TerminalSessionRegistry.IsActive ||
                    SceneEntryPresentationRegistry.IsActive)
                {
                    Fail(
                        $"DeathRetry destination did not complete sourceHost={sourceHostInstanceId} " +
                        $"destinationHost={destinationHost?.GetInstanceID() ?? 0} " +
                        $"screen={destinationInstaller?.ScreenController.CurrentScreenId}");
                    yield break;
                }
            }
            else
            {
                deadline = Time.realtimeSinceStartup + TimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    var levelFailedView = installer != null
                        ? installer.LevelFailedScreenView
                        : null;
                    if (installer != null &&
                        !TerminalSessionRegistry.IsActive &&
                        installer.ScreenController.CurrentScreenId == ScreenId.LevelFailed &&
                        levelFailedView != null &&
                        levelFailedView.IsVisible)
                    {
                        break;
                    }

                    yield return null;
                }

                var finalLevelFailedView = installer != null
                    ? installer.LevelFailedScreenView
                    : null;
                var finalIris = installer != null
                    ? installer.RootView.TerminalIrisOverlayView
                    : null;
                if (installer == null ||
                    TerminalSessionRegistry.IsActive ||
                    installer.ScreenController.CurrentScreenId != ScreenId.LevelFailed ||
                    finalLevelFailedView == null ||
                    !finalLevelFailedView.IsVisible ||
                    finalIris == null ||
                    finalIris.IsVisible ||
                    finalIris.BlocksRaycasts)
                {
                    Fail(
                        $"LevelFailed DefeatReveal did not complete screen=" +
                        $"{installer?.ScreenController.CurrentScreenId} " +
                        $"view={finalLevelFailedView?.IsVisible} iris={finalIris?.IsVisible} " +
                        $"raycast={finalIris?.BlocksRaycasts}");
                    yield break;
                }
            }

            deadline = Time.realtimeSinceStartup + RenderEnvironmentTimeoutSeconds;
            while ((Screen.width != _requestedWidth ||
                    Screen.height != _requestedHeight) &&
                   Time.realtimeSinceStartup < deadline)
            {
                MaintainRequestedResolution();
                yield return null;
            }

            if (Screen.width != _requestedWidth ||
                Screen.height != _requestedHeight)
            {
                Fail(
                    $"defeat destination resolution mismatch requested=" +
                    $"{_requestedWidth}x{_requestedHeight} actual=" +
                    $"{Screen.width}x{Screen.height}");
                yield break;
            }

            var savedSlot = saveStore.LoadSlot(1);
            var chanceLossCueCount = audioRecorder.Count(UiAudioCueId.ChanceLoss);
            var lastChanceCueCount = audioRecorder.Count(UiAudioCueId.LastChance);
            var levelFailedCueCount = audioRecorder.Count(UiAudioCueId.LevelFailed);
            var expectedChanceLossCueCount = initialChances > 1 ? 1 : 0;
            var expectedLevelFailedCueCount = initialChances == 1 ? 1 : 0;
            var expectedSavedChances = initialChances == 1
                ? SaveSlotStore.DefaultRemainingChances
                : expectedRemainingChances;
            if (savedSlot.RemainingChances != expectedSavedChances ||
                chanceLossCueCount != expectedChanceLossCueCount ||
                lastChanceCueCount != 0 ||
                levelFailedCueCount != expectedLevelFailedCueCount ||
                audioRecorder.TotalCount != 1)
            {
                Fail(
                    $"defeat route/audio mismatch chance={initialChances}->{savedSlot.RemainingChances} " +
                    $"expectedHud={expectedRemainingChances} expectedSaved={expectedSavedChances} " +
                    $"chanceLoss={chanceLossCueCount} " +
                    $"lastChance={lastChanceCueCount} levelFailed={levelFailedCueCount} " +
                    $"totalUi={audioRecorder.TotalCount}");
                yield break;
            }

            var trace = TerminalRuntimeTrace.Snapshot;
            if (TerminalSessionRegistry.Current.TerminalKind != TerminalTransitionKind.Defeat ||
                trace.All(record => record.Event != TerminalTraceEvent.BlackReached) ||
                trace.All(record => record.Event != TerminalTraceEvent.DestinationReadyAccepted) ||
                trace.All(record => record.Event != TerminalTraceEvent.RevealCompleted) ||
                trace.All(record => record.Event != TerminalTraceEvent.SessionCompleted) ||
                trace.Any(record => !record.Accepted))
            {
                Fail(
                    $"defeat terminal trace incomplete terminalKind=" +
                    $"{TerminalSessionRegistry.Current.TerminalKind} events={trace.Count}");
                yield break;
            }

            if (restartAfterLevelFailed)
            {
                if (initialChances != 1)
                {
                    Fail(
                        $"LevelFailed Restart requires the final-death fixture, " +
                        $"but initialChances={initialChances}");
                    yield break;
                }

                yield return RunLevelFailedRestartContinuation(
                    installer,
                    host,
                    audioRecorder,
                    levelFailedCueCount);
                yield break;
            }

            Debug.Log(
                $"{SuccessMarker} scenario=defeat revision={ReadArgumentValue(RevisionArgument)} " +
                $"requestedResolution={ReadArgumentValue(WidthArgument)}x{ReadArgumentValue(HeightArgument)} " +
                $"actualResolution={Screen.width}x{Screen.height} graphicsDevice={SystemInfo.graphicsDeviceType} " +
                $"input=pointer inputDispatchCount=1 selectedRoute=" +
                $"{(initialChances > 1 ? "DeathRetry" : "LevelFailed")} " +
                $"selectedPreset=DefeatClose/DefeatReveal sourceOpaque=true persistentCover=" +
                $"{(initialChances > 1)} destinationReady=true destinationClosedIris=true " +
                $"openingCompleted=true inputRelease=true finalDestinationScene={SceneManager.GetActiveScene().name} " +
                $"chances={initialChances}->{expectedRemainingChances} " +
                $"route={(initialChances > 1 ? "ChanceLost" : "LevelFailed")} " +
                $"savedChances={savedSlot.RemainingChances} " +
                $"hurt=1 hudCue=0 resultCue=1 uiCueTotal={audioRecorder.TotalCount} " +
                $"sourceHost={sourceHostInstanceId} destinationHost={destinationHost?.GetInstanceID() ?? sourceHostInstanceId}");
            UnityEngine.Application.Quit(0);
        }

        private IEnumerator RunLevelFailedRestartContinuation(
            GameplayUiFlowInstaller installer,
            GameplaySceneHost host,
            RecordingUiAudioPort audioRecorder,
            int levelFailedCueCountBeforeRestart)
        {
            var sequenceResolver = host.UiAccess?.CampaignStageSequenceResolver;
            var failedStage = host.UiAccess?.QueryFacade.Stage.Read() ?? default;
            var levelGroupId = sequenceResolver?.GetLevelGroupId(
                failedStage.StageId) ?? string.Empty;
            var expectedRetryStageId = sequenceResolver != null
                ? sequenceResolver.GetFirstStageInLevelGroupOrNone(levelGroupId)
                : StageId.None;
            if (sequenceResolver == null ||
                !failedStage.StageId.IsValid ||
                string.IsNullOrWhiteSpace(levelGroupId) ||
                !expectedRetryStageId.IsValid)
            {
                Fail(
                    $"LevelFailed Retry group authority invalid stage=" +
                    $"{failedStage.StageId.Value} group={levelGroupId} " +
                    $"first={expectedRetryStageId.Value}");
                yield break;
            }

            var levelFailed = installer.LevelFailedScreenView;
            var restartButton = FindNamedButton(levelFailed, "RestartLevelButton");
            if (levelFailed == null ||
                !levelFailed.IsVisible ||
                restartButton == null ||
                !restartButton.interactable)
            {
                Fail("LevelFailed Restart button did not become interaction-ready");
                yield break;
            }

            var clickCount = 0;
            var restartDispatchCount = 0;
            restartButton.onClick.AddListener(() => clickCount++);
            levelFailed.RestartLevelRequested += () => restartDispatchCount++;
            var traceBefore = TerminalRuntimeTrace.Snapshot.ToArray();
            var sourceHostId = host.GetInstanceID();
            var acceptedBefore = SceneTransitionCoordinator.Instance.AcceptedTransitionCount;
            yield return DispatchPointerClick(
                restartButton,
                levelFailed,
                installer,
                "LevelFailed Restart");
            if (!_pointerDispatchSucceeded ||
                clickCount != 1 ||
                restartDispatchCount != 1 ||
                !SceneEntryPresentationRegistry.IsActive ||
                SceneEntryPresentationRegistry.Current.TransitionIntent !=
                SceneTransitionIntent.ManualRetry ||
                SceneTransitionCoordinator.Instance.AcceptedTransitionCount -
                acceptedBefore != 1)
            {
                Fail(
                    $"LevelFailed Restart dispatch invalid click={clickCount} " +
                    $"callback={restartDispatchCount} " +
                    $"entryActive={SceneEntryPresentationRegistry.IsActive} " +
                    $"intent={SceneEntryPresentationRegistry.Current.TransitionIntent}");
                yield break;
            }

            yield return AwaitGameplayEntryCompletion(
                sourceHostId,
                SceneTransitionIntent.ManualRetry,
                GameplayEntrySourceCloseVisualKind.RetryIris,
                installer.RootView.TerminalIrisOverlayView,
                sourceRootName: string.Empty);
            if (!_gameplayEntryCompletionSucceeded)
            {
                yield break;
            }

            var traceAfter = TerminalRuntimeTrace.Snapshot;
            var duplicateDefeatTrace = traceAfter.Count > traceBefore.Length &&
                                       traceAfter.Skip(traceBefore.Length).Any();
            if (clickCount != 1 ||
                restartDispatchCount != 1 ||
                duplicateDefeatTrace ||
                audioRecorder.Count(UiAudioCueId.LevelFailed) !=
                levelFailedCueCountBeforeRestart)
            {
                Fail(
                    $"LevelFailed Restart duplicated defeat ownership click={clickCount} " +
                    $"callback={restartDispatchCount} " +
                    $"traceBefore={traceBefore.Length} traceAfter={traceAfter.Count} " +
                    $"levelFailedCueBefore={levelFailedCueCountBeforeRestart} " +
                    $"levelFailedCueAfter={audioRecorder.Count(UiAudioCueId.LevelFailed)}");
                yield break;
            }

            var destinationHost = FindObjectsByType<GameplaySceneHost>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate.GetInstanceID() != sourceHostId);
            var destinationStage =
                destinationHost?.UiAccess?.QueryFacade.Stage.Read() ?? default;
            var savedSlot = new SaveSlotStore(
                    EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                    EditorDirectPlayContextStore.TempActiveSlotProviderKey)
                .LoadSlot(1);
            if (destinationHost == null ||
                !destinationStage.StageId.Equals(expectedRetryStageId) ||
                !savedSlot.CurrentStageId.Equals(expectedRetryStageId) ||
                !string.Equals(
                    savedSlot.CurrentLevelGroupId,
                    levelGroupId,
                    StringComparison.Ordinal))
            {
                Fail(
                    $"LevelFailed Retry group divergence failed={failedStage.StageId.Value} " +
                    $"group={levelGroupId} expected={expectedRetryStageId.Value} " +
                    $"destination={destinationStage.StageId.Value} " +
                    $"saved={savedSlot.CurrentStageId.Value}");
                yield break;
            }

            _authoritySourceStageId = failedStage.StageId;
            _authorityExpectedNextStageId = expectedRetryStageId;
            _authoritySavedStageId = savedSlot.CurrentStageId;
            _authorityResultNextStageId = expectedRetryStageId;
            _authorityCampaignCompleted = savedSlot.CampaignCompleted;

            LogRouteSuccess(
                "RetryClose",
                "LevelFailedRestart",
                restartDispatchCount,
                SceneManager.GetActiveScene().name,
                $"defeatCloseReplay=0 duplicateResultAudio=0 " +
                $"levelGroup={levelGroupId} groupFirst={expectedRetryStageId.Value}");
        }

        private static TickResult CreateLethalPlayerTickResult(int playerEntityId)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickPlayerDeathPresentationSignal(
                        playerEntityId,
                        didDieThisTick: true,
                        sourceEntityId: 0,
                        fallbackFacing: Direction.Right,
                        resolvedDamageSourceAvailable: false,
                        damageAmountAtFatalHit: 1,
                        deathDirectionHintKind: DeathDirectionHintKind.FacingReverse),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());
            var result = new TickResult(
                1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>());
            var presentationDataField = typeof(TickResult).GetField(
                "<PresentationData>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (presentationDataField == null)
            {
                throw new InvalidOperationException("TickResult presentation-data backing field is unavailable.");
            }

            presentationDataField.SetValue(result, presentationData);
            return result;
        }

        private static bool PresentObjectiveClearTick(GameplaySceneHost host)
        {
            if (host?.Presenter == null || host.UiAccess?.QueryFacade == null)
            {
                return false;
            }

            var tickIndex = host.UiAccess.QueryFacade.Session.Read().NextTickIndex;
            var objective = new StageObjectiveTickResult(
                hasObjective: true,
                goalReached: true,
                allConditionsSatisfied: true,
                clearedThisTick: true,
                isCleared: true,
                Array.Empty<StageConditionStatus>());
            var result = new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>());
            var objectiveResultField = typeof(TickResult).GetField(
                "<ObjectiveResult>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (objectiveResultField == null)
            {
                return false;
            }

            objectiveResultField.SetValue(result, objective);
            host.Presenter.Present(result);
            var presentationFeed = host.UiAccess.PresentationFeed;
            var handleTickCompleted = presentationFeed?.GetType().GetMethod(
                "HandleTickCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(TickResult) },
                null);
            if (handleTickCompleted == null)
            {
                return false;
            }

            try
            {
                handleTickCompleted.Invoke(presentationFeed, new object[] { result });
                return true;
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            }
        }

        private static bool TryInstallUiAudioRecorder(
            GameplayUiFlowInstaller installer,
            out RecordingUiAudioPort recorder,
            out string failure)
        {
            recorder = null;
            failure = string.Empty;
            var installerAudioField = typeof(GameplayUiFlowInstaller).GetField(
                "_uiAudioPort",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var original = installerAudioField?.GetValue(installer) as IUiAudioPort;
            if (original == null)
            {
                failure = "production UI audio port is unavailable";
                return false;
            }

            recorder = new RecordingUiAudioPort(original);
            var coordinatorAudioField = typeof(UIFlowCoordinator).GetField(
                "_uiAudioPort",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var hudControllerField = typeof(GameplayUiFlowInstaller).GetField(
                "_hudUiAudioFeedbackController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var hudController = hudControllerField?.GetValue(installer);
            var hudAudioField = hudController?.GetType().GetField(
                "_uiAudioPort",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (coordinatorAudioField == null || hudController == null || hudAudioField == null)
            {
                failure = "UI flow or HUD audio owner is unavailable";
                recorder = null;
                return false;
            }

            coordinatorAudioField.SetValue(installer.Coordinator, recorder);
            hudAudioField.SetValue(hudController, recorder);
            SceneTransitionCoordinator.Instance.BindUiAudioPort(recorder);
            return true;
        }

        private static bool TryReadPlayerDamageSfxDiagnostics(
            GameplayTickViewPresenter presenter,
            out int planned,
            out int requested,
            out int succeeded,
            out int duplicateSuppressed,
            out string failure)
        {
            planned = 0;
            requested = 0;
            succeeded = 0;
            duplicateSuppressed = 0;
            failure = string.Empty;
            var diagnosticsProperty = presenter?.GetType().GetProperty(
                "CoreGameplaySfxExecutorDiagnostics",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var diagnostics = diagnosticsProperty?.GetValue(presenter);
            var semanticProperty = diagnostics?.GetType().GetProperty("SemanticDiagnostics");
            if (semanticProperty?.GetValue(diagnostics) is not IEnumerable semanticDiagnostics)
            {
                failure = "core gameplay SFX semantic diagnostics are unavailable";
                return false;
            }

            foreach (var semantic in semanticDiagnostics)
            {
                var semanticType = semantic.GetType();
                if (!string.Equals(
                        semanticType.GetProperty("CueKey")?.GetValue(semantic)?.ToString(),
                        "PlayerDamage",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                planned = (int)(semanticType.GetProperty("PlannedCount")?.GetValue(semantic) ?? 0);
                requested = (int)(semanticType.GetProperty("RequestedCount")?.GetValue(semantic) ?? 0);
                succeeded = (int)(semanticType.GetProperty("SucceededCount")?.GetValue(semantic) ?? 0);
                duplicateSuppressed =
                    (int)(semanticType.GetProperty("DuplicateSuppressedCount")?.GetValue(semantic) ?? 0);
                return true;
            }

            failure = "PlayerDamage semantic diagnostics entry is missing";
            return false;
        }

        private sealed class RecordingUiAudioPort : IUiAudioPort
        {
            private readonly Dictionary<UiAudioCueId, int> _counts = new();
            private readonly IUiAudioPort _inner;

            public RecordingUiAudioPort(IUiAudioPort inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public int TotalCount { get; private set; }

            public int Count(UiAudioCueId cueId)
            {
                return _counts.TryGetValue(cueId, out var count) ? count : 0;
            }

            public void Play(UiAudioCueId cueId)
            {
                TotalCount++;
                _counts[cueId] = Count(cueId) + 1;
                _inner.Play(cueId);
            }
        }

        private IEnumerator RunGameClearSmoke(
            GameplayUiFlowInstaller installer,
            TerminalIrisOverlayView irisView,
            string inputMode,
            bool sawIntermediateHandoffAlpha,
            bool sawIntermediateContentAlpha)
        {
            var gameClear = installer.GameClearScreenView;
            var button = gameClear != null
                ? gameClear.GetComponentInChildren<Button>(true)
                : null;
            if (TerminalSessionRegistry.IsActive ||
                TerminalSessionRegistry.Current.Phase != TerminalSessionPhase.Completed ||
                installer.ScreenController.CurrentScreenId != ScreenId.GameClear ||
                gameClear == null ||
                !gameClear.IsVisible ||
                !gameClear.IsInteractionReady ||
                gameClear.HandoffCoverAlpha > 0f ||
                gameClear.IsHandoffCoverActive ||
                Mathf.Abs(gameClear.ContentAlpha - 1f) > 0.0001f ||
                !sawIntermediateHandoffAlpha ||
                !sawIntermediateContentAlpha ||
                button == null ||
                !button.interactable ||
                irisView.IsVisible ||
                irisView.BlocksRaycasts ||
                SceneEntryPresentationRegistry.IsActive)
            {
                Fail(
                    $"GameClear handoff invalid phase={TerminalSessionRegistry.Current.Phase} " +
                    $"screen={installer.ScreenController.CurrentScreenId} view={(gameClear != null)} " +
                    $"interaction={gameClear?.IsInteractionReady} button={button?.interactable}");
                yield break;
            }

            var eventSystem = EventSystem.current;
            var inputRouter = installer.GetComponent<UiNavigationInputRouter>();
            var baselineSubmit = inputRouter != null ? inputRouter.SubmitPerformedCount : 0;
            var clickCount = 0;
            var mainDispatchCount = 0;
            button.onClick.AddListener(() => clickCount++);
            gameClear.MainRequested += () => mainDispatchCount++;
            _resultInteractionReadyAt = Time.realtimeSinceStartup;
            yield return WaitForInputWindowFocus();
            if (!_inputWindowFocused)
            {
                Fail("GameClear input dispatch did not acquire foreground window focus");
                yield break;
            }

            yield return WaitForRenderedInteractionFrame();
            if (!_interactionRenderAcknowledged)
            {
                Fail("GameClear input dispatch did not observe a rendered interaction-ready frame");
                yield break;
            }

            if (string.Equals(inputMode, "pointer", StringComparison.OrdinalIgnoreCase))
            {
                if (eventSystem == null || smokeMouse == null)
                {
                    Fail("GameClear pointer EventSystem or smoke mouse is missing");
                    yield break;
                }

                var buttonRect = (RectTransform)button.transform;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(
                    null,
                    buttonRect.TransformPoint(buttonRect.rect.center));
                var pointerData = new PointerEventData(eventSystem) { position = screenPoint };
                var raycasts = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, raycasts);
                if (!raycasts.Any(result =>
                        result.gameObject == button.gameObject ||
                        result.gameObject.transform.IsChildOf(button.transform)))
                {
                    Fail(
                        "actual EventSystem raycast did not reach the GameClear Main button; " +
                        DescribeInputDispatch(
                            installer,
                            gameClear,
                            button,
                            inputMode,
                            screenPoint,
                            raycasts));
                    yield break;
                }

                InputSystem.QueueStateEvent(smokeMouse, new MouseState { position = screenPoint });
                yield return null;
                var pressed = new MouseState { position = screenPoint };
                pressed.WithButton(MouseButton.Left);
                InputSystem.QueueStateEvent(smokeMouse, pressed);
                yield return null;
                InputSystem.QueueStateEvent(smokeMouse, new MouseState { position = screenPoint });
                yield return null;
                var inputDeadline = Time.realtimeSinceStartup + InputDispatchTimeoutSeconds;
                while (clickCount == 0 && Time.realtimeSinceStartup < inputDeadline)
                {
                    yield return null;
                }
                if (clickCount != 1)
                {
                    Fail(
                        $"GameClear pointer click count was {clickCount}, expected exactly one; " +
                        DescribeInputDispatch(
                            installer,
                            gameClear,
                            button,
                            inputMode,
                            screenPoint,
                            Raycast(eventSystem, screenPoint)));
                    yield break;
                }
            }
            else
            {
                if (eventSystem == null ||
                    smokeKeyboard == null ||
                    eventSystem.currentSelectedGameObject != button.gameObject)
                {
                    Fail(
                        "GameClear keyboard selection is not on the Main button; " +
                        DescribeInputDispatch(
                            installer,
                            gameClear,
                            button,
                            inputMode,
                            ResolveButtonScreenPoint(button),
                            Raycast(eventSystem, ResolveButtonScreenPoint(button))));
                    yield break;
                }

                InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState(Key.Enter));
                yield return null;
                InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState());
                yield return null;
                var inputDeadline = Time.realtimeSinceStartup + InputDispatchTimeoutSeconds;
                while (inputRouter != null &&
                       inputRouter.SubmitPerformedCount == baselineSubmit &&
                       Time.realtimeSinceStartup < inputDeadline)
                {
                    yield return null;
                }
                if (inputRouter == null ||
                    inputRouter.SubmitPerformedCount - baselineSubmit != 1 ||
                    !inputRouter.LastSubmitDispatchResult)
                {
                    Fail(
                        $"GameClear keyboard submit count=" +
                        $"{inputRouter?.SubmitPerformedCount - baselineSubmit ?? -1}; " +
                        DescribeInputDispatch(
                            installer,
                            gameClear,
                            button,
                            inputMode,
                            ResolveButtonScreenPoint(button),
                            Raycast(eventSystem, ResolveButtonScreenPoint(button))));
                    yield break;
                }
            }

            if (mainDispatchCount != 1)
            {
                Fail(
                    $"GameClear MainRequested dispatch count was {mainDispatchCount}, expected exactly one; " +
                    DescribeInputDispatch(
                        installer,
                        gameClear,
                        button,
                        inputMode,
                        ResolveButtonScreenPoint(button),
                        Raycast(eventSystem, ResolveButtonScreenPoint(button))));
                yield break;
            }

            if (SceneEntryPresentationRegistry.IsActive)
            {
                Fail("GameClear Main transition must not claim a stage Entry Iris session");
                yield break;
            }

            if (!MainMenuEntryPresentationRegistry.IsActive)
            {
                Fail("GameClear Main transition did not claim the Main Menu destination session");
                yield break;
            }

            _sourceOpaqueAcknowledged = false;
            _persistentCoverAcknowledged = false;
            _destinationReadyAcknowledged = false;
            _destinationClosedIrisAcknowledged = false;
            _openingCompleted = false;
            _inputReleased = false;
            var claimed = MainMenuEntryPresentationRegistry.Current;
            var selectedPreset = claimed.TransitionIntent.ToString();
            var cinematicHandoffToken = default(CinematicOpaqueHandoffToken);
            var cinematicSkipDispatchCount = 0;
            if (claimed.TransitionIntent == SceneTransitionIntent.CinematicToMainMenu)
            {
                CinematicVideoOverlayView cinematic = null;
                Button cinematicSkipButton = null;
                var cinematicDeadline = Time.realtimeSinceStartup + TimeoutSeconds;
                while (Time.realtimeSinceStartup < cinematicDeadline)
                {
                    cinematic = FindFirstObjectByType<CinematicVideoOverlayView>(
                        FindObjectsInactive.Include);
                    cinematicSkipButton = FindNamedButton(cinematic, "Background");
                    if (cinematic != null &&
                        cinematic.IsPlaying &&
                        cinematic.CurrentPresentationState ==
                            CinematicPresentationState.Playing &&
                        cinematicSkipButton != null &&
                        cinematicSkipButton.interactable)
                    {
                        break;
                    }

                    yield return null;
                }

                if (cinematic == null ||
                    !cinematic.IsPlaying ||
                    cinematic.CurrentPresentationState !=
                        CinematicPresentationState.Playing ||
                    cinematicSkipButton == null ||
                    !cinematicSkipButton.interactable)
                {
                    Fail("GameClear outro cinematic did not expose its production skip target");
                    yield break;
                }

                cinematicHandoffToken = CinematicOpaqueHandoffRegistry.Current.Token;
                cinematicSkipButton.onClick.AddListener(
                    () => cinematicSkipDispatchCount++);
                yield return DispatchPointerClick(
                    cinematicSkipButton,
                    cinematic,
                    installer,
                    "GameClear outro cinematic skip");
                if (!_pointerDispatchSucceeded || cinematicSkipDispatchCount != 1)
                {
                    Fail(
                        $"GameClear outro cinematic skip dispatch count was " +
                        $"{cinematicSkipDispatchCount}, expected exactly one");
                    yield break;
                }
            }

            SceneTransitionOverlayShellView persistentCover = null;
            MainMenuUiFlowInstaller destination = null;
            TerminalIrisOverlayView destinationIris = null;
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                MaintainRequestedResolution();
                var cinematicHandoff = CinematicOpaqueHandoffRegistry.Current;
                _sourceOpaqueAcknowledged |=
                    irisView != null && irisView.HasRenderedEntryClosedFrame ||
                    cinematicHandoffToken.IsValid &&
                    cinematicHandoff.Token == cinematicHandoffToken &&
                    (cinematicHandoff.Phase ==
                         CinematicOpaqueHandoffPhase.CinematicOpaqueRendered ||
                     cinematicHandoff.Phase ==
                         CinematicOpaqueHandoffPhase.PersistentCoverRendered ||
                     cinematicHandoff.Phase ==
                         CinematicOpaqueHandoffPhase.Released);
                persistentCover ??=
                    FindFirstObjectByType<SceneTransitionOverlayShellView>(
                        FindObjectsInactive.Include);
                _persistentCoverAcknowledged |=
                    persistentCover != null &&
                    persistentCover.HasRenderedOpaqueFrame &&
                    persistentCover.HasAcknowledgedOpaqueFrame;
                destination = FindFirstObjectByType<MainMenuUiFlowInstaller>(
                    FindObjectsInactive.Include);
                var session = MainMenuEntryPresentationRegistry.Current;
                if (destination != null &&
                    session.IsActive &&
                    session.Token == claimed.Token &&
                    session.Phase == SceneEntryPresentationPhase.Opening)
                {
                    _destinationReadyAcknowledged =
                        destination.Controller != null &&
                        destination.HubController != null &&
                        destination.MainMenuScreenView != null;
                    destinationIris = FindObjectsByType<GameplayUiCanvasRootView>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None)
                        .FirstOrDefault(root => root.name == "MainMenuDestinationIris")
                        ?.TerminalIrisOverlayView;
                    _destinationClosedIrisAcknowledged |=
                        destinationIris != null &&
                        destinationIris.HasRenderedEntryClosedFrame &&
                        destinationIris.IsVisible &&
                        destinationIris.BlocksRaycasts;
                }

                if (!session.IsActive &&
                    session.Token == claimed.Token &&
                    session.Phase == SceneEntryPresentationPhase.Completed)
                {
                    _openingCompleted = true;
                    break;
                }

                if (session.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    Fail(
                        $"GameClear Main Menu destination failed reason={session.FailureReason}");
                    yield break;
                }

                yield return null;
            }

            _inputReleased = destination != null &&
                             !destination.IsGameplayEntryInteractionBlocked &&
                             destination.MainMenuScreenView != null &&
                             destination.MainMenuScreenView.CanHandleUiNavigation;
            var eventSystemValid = TryValidateSingleActiveEventSystem(
                out var eventSystemDiagnostic);
            if (!_sourceOpaqueAcknowledged ||
                !_persistentCoverAcknowledged ||
                !_destinationReadyAcknowledged ||
                !_destinationClosedIrisAcknowledged ||
                !_openingCompleted ||
                !_inputReleased ||
                gameClear != null ||
                mainDispatchCount != 1 ||
                !eventSystemValid)
            {
                Fail(
                    $"GameClear Main Menu completion invalid sourceOpaque={_sourceOpaqueAcknowledged} " +
                    $"persistent={_persistentCoverAcknowledged} " +
                    $"destinationReady={_destinationReadyAcknowledged} " +
                    $"destinationClosed={_destinationClosedIrisAcknowledged} " +
                    $"opening={_openingCompleted} inputRelease={_inputReleased} " +
                    $"staleGameClear={gameClear != null} callback={mainDispatchCount} " +
                    $"intent={MainMenuEntryPresentationRegistry.Current.TransitionIntent} " +
                    $"phase={MainMenuEntryPresentationRegistry.Current.Phase} " +
                    $"transitionId={MainMenuEntryPresentationRegistry.Current.TransitionId} " +
                    $"cinematicHandoffPhase={CinematicOpaqueHandoffRegistry.Current.Phase} " +
                    $"cinematicSkipDispatchCount={cinematicSkipDispatchCount} " +
                    eventSystemDiagnostic);
                yield break;
            }

            LogRouteSuccess(
                selectedPreset,
                "GameClearToMainMenu",
                mainDispatchCount,
                SceneManager.GetActiveScene().name,
                $"entrySession=none staleGameClearRoot=false callbackDuplicate=0 " +
                $"cinematicSkipDispatchCount={cinematicSkipDispatchCount}",
                inputMode);
        }

        private IEnumerator WaitForRenderedInteractionFrame()
        {
            _interactionRenderAcknowledged = false;
            var interactionReadyFrame = Time.frameCount;
            var deadline = Time.realtimeSinceStartup + RenderEnvironmentTimeoutSeconds;
            while (_lastCanvasRenderFrame <= interactionReadyFrame &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            _interactionRenderAcknowledged = _lastCanvasRenderFrame > interactionReadyFrame;
        }

        private void MaintainRequestedResolution()
        {
            if (_requestedWidth <= 0 ||
                _requestedHeight <= 0 ||
                Screen.width == _requestedWidth &&
                Screen.height == _requestedHeight)
            {
                return;
            }

            Screen.SetResolution(
                _requestedWidth,
                _requestedHeight,
                FullScreenMode.Windowed);
        }

        private IEnumerator WaitForInputWindowFocus()
        {
            _inputWindowFocused = false;
            var deadline = Time.realtimeSinceStartup + RenderEnvironmentTimeoutSeconds;
            while (!UnityEngine.Application.isFocused &&
                   Time.realtimeSinceStartup < deadline)
            {
                TryFocusPlayerWindow();
                yield return null;
            }

            _inputWindowFocused = UnityEngine.Application.isFocused;
        }

        private static void TryFocusPlayerWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            var window = process.MainWindowHandle;
            if (window != IntPtr.Zero)
            {
                ShowWindow(window, ShowWindowRestore);
                SetForegroundWindow(window);
            }
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr window, int command);
#endif

        private string DescribeInputDispatch(
            GameplayUiFlowInstaller installer,
            Component view,
            Button button,
            string inputMode,
            Vector2 requestedPointerPosition,
            IReadOnlyList<RaycastResult> raycasts)
        {
            var eventSystem = EventSystem.current;
            var eventSystems = FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var activeEventSystemCount = eventSystems.Count(candidate =>
                candidate != null &&
                candidate.enabled &&
                candidate.gameObject.activeInHierarchy);
            var inputModule = eventSystem != null
                ? eventSystem.GetComponent<InputSystemUIInputModule>()
                : null;
            var host = FindFirstObjectByType<GameplaySceneHost>(FindObjectsInactive.Include);
            var actions = host != null && host.InputHost != null
                ? host.InputHost.Actions
                : null;
            var uiMap = actions?.FindActionMap("UI", throwIfNotFound: false);
            var gameplayMap = actions?.FindActionMap("Player", throwIfNotFound: false);
            var canvas = view != null ? view.GetComponentInParent<Canvas>() : null;
            var canvasGroup = button != null ? button.GetComponentInParent<CanvasGroup>() : null;
            var topRaycast = raycasts != null && raycasts.Count > 0
                ? GetPath(raycasts[0].gameObject.transform)
                : "none";
            var buttonRect = button != null ? (RectTransform)button.transform : null;
            var pointerInside = buttonRect != null &&
                                RectTransformUtility.RectangleContainsScreenPoint(
                                    buttonRect,
                                    requestedPointerPosition,
                                    null);
            var devices = string.Join(
                ",",
                InputSystem.devices.Select(device =>
                    $"{device.layout}#{device.deviceId}:enabled={device.enabled}:added={device.added}:background={device.canRunInBackground}"));

            return
                $"scenario={_scenarioId} sequenceIndex={_scenarioSequenceIndex} " +
                $"frame={Time.frameCount} sinceProbe={Time.realtimeSinceStartup - _probeStartedAt:F3} " +
                $"sinceInteractionReady={Time.realtimeSinceStartup - _resultInteractionReadyAt:F3} " +
                $"focused={UnityEngine.Application.isFocused} " +
                $"runInBackground={UnityEngine.Application.runInBackground} " +
                $"inputBackground={InputSystem.settings.backgroundBehavior} inputUpdate={InputSystem.settings.updateMode} " +
                $"eventSystemCount={eventSystems.Length} activeEventSystemCount={activeEventSystemCount} " +
                $"eventSystemId={(eventSystem != null ? eventSystem.GetInstanceID() : 0)} " +
                $"eventSystemEnabled={eventSystem?.enabled} eventSystemActive={eventSystem?.gameObject.activeInHierarchy} " +
                $"selected={GetPathOrNull(eventSystem?.currentSelectedGameObject)} " +
                $"alreadySelecting={eventSystem?.alreadySelecting} sendNavigationEvents={eventSystem?.sendNavigationEvents} " +
                $"pixelDragThreshold={eventSystem?.pixelDragThreshold} " +
                $"moduleId={(inputModule != null ? inputModule.GetInstanceID() : 0)} " +
                $"moduleEnabled={inputModule?.enabled} moduleActive={inputModule?.gameObject.activeInHierarchy} " +
                $"moduleActionsId={(inputModule?.actionsAsset != null ? inputModule.actionsAsset.GetInstanceID() : 0)} " +
                $"point={DescribeAction(inputModule?.point?.action)} click={DescribeAction(inputModule?.leftClick?.action)} " +
                $"moduleSubmit={DescribeAction(inputModule?.submit?.action)} moduleCancel={DescribeAction(inputModule?.cancel?.action)} " +
                $"moduleMove={DescribeAction(inputModule?.move?.action)} uiMapEnabled={uiMap?.enabled} " +
                $"gameplayMapEnabled={gameplayMap?.enabled} uiSubmit={DescribeAction(uiMap?.FindAction("Submit", false))} " +
                $"terminalActive={TerminalSessionRegistry.IsActive} entryActive={SceneEntryPresentationRegistry.IsActive} " +
                $"blocksScreen={installer?.Coordinator?.CurrentBlockSnapshot.BlocksScreenInteraction} " +
                $"viewActive={view?.gameObject.activeInHierarchy} canvasEnabled={canvas?.enabled} " +
                $"canvasGroupAlpha={canvasGroup?.alpha:F3} canvasGroupInteractable={canvasGroup?.interactable} " +
                $"canvasGroupBlocksRaycasts={canvasGroup?.blocksRaycasts} " +
                $"buttonActive={button?.gameObject.activeInHierarchy} buttonEnabled={button?.enabled} " +
                $"buttonInteractable={button?.interactable} buttonNavigation={button?.navigation.mode} " +
                $"buttonRegistered={(button != null && Selectable.allSelectablesArray.Contains(button))} " +
                $"buttonPath={(button != null ? GetPath(button.transform) : "null")} " +
                $"requestedPointer={Format(requestedPointerPosition)} " +
                $"actualPointer={(smokeMouse != null ? Format(smokeMouse.position.ReadValue()) : "null")} " +
                $"screen={Screen.width}x{Screen.height} pointerDeviceId={smokeMouse?.deviceId ?? 0} " +
                $"pointerPressed={smokeMouse?.leftButton.isPressed} pointerInside={pointerInside} " +
                $"raycastCount={raycasts?.Count ?? 0} topRaycast={topRaycast} " +
                $"keyboardCurrent={(Keyboard.current != null)} mouseCurrent={(Mouse.current != null)} " +
                $"devices=[{devices}] inputMode={inputMode}";
        }

        private static string DescribeAction(InputAction action)
        {
            return action == null
                ? "null"
                : $"{action.actionMap?.name}/{action.name}:enabled={action.enabled}:phase={action.phase}:" +
                  $"activeDevice={action.activeControl?.device.deviceId ?? 0}:" +
                  $"activeControl={action.activeControl?.path ?? "null"}";
        }

        private static string GetPathOrNull(GameObject gameObject)
        {
            return gameObject != null ? GetPath(gameObject.transform) : "null";
        }

        private static Vector2 ResolveButtonScreenPoint(Button button)
        {
            if (button == null)
            {
                return Vector2.zero;
            }

            var rect = (RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
        }

        private static List<RaycastResult> Raycast(
            EventSystem eventSystem,
            Vector2 screenPoint)
        {
            var results = new List<RaycastResult>();
            if (eventSystem != null)
            {
                eventSystem.RaycastAll(
                    new PointerEventData(eventSystem) { position = screenPoint },
                    results);
            }

            return results;
        }

        private static void MoveViewToViewport(
            Camera camera,
            GameplayEntityView view,
            Vector2 targetViewport)
        {
            var bounds = CalculateRendererBounds(view);
            var currentViewport = camera.WorldToViewportPoint(bounds.center);
            var targetWorld = camera.ViewportToWorldPoint(
                new Vector3(targetViewport.x, targetViewport.y, currentViewport.z));
            view.transform.position += targetWorld - bounds.center;
        }

        private static Vector2 ProjectRendererBoundsCenter(
            Camera camera,
            GameplayEntityView view)
        {
            var viewport = camera.WorldToViewportPoint(CalculateRendererBounds(view).center);
            return new Vector2(viewport.x, viewport.y);
        }

        private static Bounds CalculateRendererBounds(GameplayEntityView view)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        internal static bool TryValidateRenderEnvironment(
            GraphicsDeviceType graphicsDeviceType,
            int requestedWidth,
            int requestedHeight,
            int actualWidth,
            int actualHeight,
            Rect cameraPixelRect,
            int cameraTargetDisplay,
            bool cameraActive,
            bool cameraRenderAcknowledged,
            out string failureReason)
        {
            if (graphicsDeviceType == GraphicsDeviceType.Null)
            {
                failureReason = "graphicsDeviceType=Null";
                return false;
            }

            if (requestedWidth <= 0 || requestedHeight <= 0)
            {
                failureReason =
                    $"requestedResolution={requestedWidth}x{requestedHeight}";
                return false;
            }

            if (actualWidth != requestedWidth || actualHeight != requestedHeight)
            {
                failureReason =
                    $"requestedResolution={requestedWidth}x{requestedHeight} " +
                    $"actualResolution={actualWidth}x{actualHeight}";
                return false;
            }

            if (!cameraActive)
            {
                failureReason = "outputCameraActive=false";
                return false;
            }

            if (cameraTargetDisplay != 0)
            {
                failureReason = $"cameraTargetDisplay={cameraTargetDisplay}";
                return false;
            }

            if (cameraPixelRect.width <= 0f ||
                cameraPixelRect.height <= 0f ||
                float.IsNaN(cameraPixelRect.width) ||
                float.IsNaN(cameraPixelRect.height) ||
                float.IsInfinity(cameraPixelRect.width) ||
                float.IsInfinity(cameraPixelRect.height))
            {
                failureReason = $"cameraPixelRect={cameraPixelRect}";
                return false;
            }

            if (!cameraRenderAcknowledged)
            {
                failureReason = "firstOutputCameraRenderAcknowledged=false";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static string DescribeRenderEnvironment(
            Camera camera,
            int requestedWidth,
            int requestedHeight)
        {
            return
                $"graphicsDeviceType={SystemInfo.graphicsDeviceType} " +
                $"graphicsDeviceName='{SystemInfo.graphicsDeviceName}' " +
                $"requestedResolution={requestedWidth}x{requestedHeight} " +
                $"actualResolution={Screen.width}x{Screen.height} " +
                $"cameraInstanceId={(camera != null ? camera.GetInstanceID() : 0)} " +
                $"cameraTargetDisplay={(camera != null ? camera.targetDisplay : -1)} " +
                $"cameraPixelRect={(camera != null ? camera.pixelRect : default)} " +
                $"cameraRect={(camera != null ? camera.rect : default)} " +
                $"cameraActive={camera != null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy} " +
                $"firstCameraRenderAcknowledged={camera != null && RenderedCameraInstanceIds.Contains(camera.GetInstanceID())}";
        }

        private static string DescribeDestinationProjection(GameplaySceneHost host)
        {
            if (host == null)
            {
                return "destinationProjection host=null";
            }

            var camera = host.OutputCamera;
            host.ViewRegistry.TryGetView(host.PlayerEntityId, out var view);
            var builder = new StringBuilder(1024);
            builder.Append("destinationProjection ");
            builder.Append("frame=").Append(Time.frameCount).Append(' ');
            builder.Append("graphicsDeviceType=").Append(SystemInfo.graphicsDeviceType).Append(' ');
            builder.Append("screen=").Append(Screen.width).Append('x').Append(Screen.height).Append(' ');
            builder.Append("playerEntityId=").Append(host.PlayerEntityId).Append(' ');
            builder.Append("playerViewInstanceId=").Append(view != null ? view.GetInstanceID() : 0).Append(' ');
            builder.Append("playerActive=").Append(view != null && view.gameObject.activeInHierarchy).Append(' ');
            builder.Append("playerWorldPosition=").Append(view != null ? Format(view.transform.position) : "null").Append(' ');
            builder.Append("cameraInstanceId=").Append(camera != null ? camera.GetInstanceID() : 0).Append(' ');
            builder.Append("cameraPath='").Append(camera != null ? GetPath(camera.transform) : "null").Append("' ");
            builder.Append("cameraEnabled=").Append(camera != null && camera.enabled).Append(' ');
            builder.Append("cameraActive=").Append(camera != null && camera.gameObject.activeInHierarchy).Append(' ');
            builder.Append("cameraTag='").Append(camera != null ? camera.tag : "null").Append("' ");
            builder.Append("cameraTargetDisplay=").Append(camera != null ? camera.targetDisplay : -1).Append(' ');
            builder.Append("cameraPixelRect=").Append(camera != null ? camera.pixelRect.ToString() : "null").Append(' ');
            builder.Append("cameraRect=").Append(camera != null ? camera.rect.ToString() : "null").Append(' ');
            builder.Append("cameraAspect=").Append(camera != null ? Float(camera.aspect) : "null").Append(' ');
            builder.Append("cameraOrthographic=").Append(camera != null && camera.orthographic).Append(' ');
            builder.Append("cameraOrthographicSize=").Append(camera != null ? Float(camera.orthographicSize) : "null").Append(' ');
            builder.Append("cameraFieldOfView=").Append(camera != null ? Float(camera.fieldOfView) : "null").Append(' ');
            builder.Append("cameraWorldPosition=").Append(camera != null ? Format(camera.transform.position) : "null").Append(' ');
            builder.Append("cameraWorldRotation=").Append(camera != null ? Format(camera.transform.rotation.eulerAngles) : "null").Append(' ');
            builder.Append("cameraCullingMask=").Append(camera != null ? camera.cullingMask : 0).Append(' ');
            builder.Append("firstCameraRenderAcknowledged=").Append(
                camera != null && RenderedCameraInstanceIds.Contains(camera.GetInstanceID())).Append(' ');

            if (camera == null || view == null)
            {
                return builder.ToString();
            }

            var renderers = view.GetComponentsInChildren<Renderer>(true);
            builder.Append("rendererCount=").Append(renderers.Length).Append(' ');
            if (renderers.Length == 0)
            {
                return builder.ToString();
            }

            var combined = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }

            builder.Append("combinedBoundsCenter=").Append(Format(combined.center)).Append(' ');
            builder.Append("combinedBoundsExtents=").Append(Format(combined.extents)).Append(' ');
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var behind = 0;
            var left = 0;
            var right = 0;
            var bottom = 0;
            var top = 0;
            var cornerIndex = 0;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var world = combined.center + Vector3.Scale(
                            combined.extents,
                            new Vector3(x, y, z));
                        var viewport = camera.WorldToViewportPoint(world);
                        min = Vector2.Min(min, viewport);
                        max = Vector2.Max(max, viewport);
                        behind += viewport.z <= 0f ? 1 : 0;
                        left += viewport.x < 0f ? 1 : 0;
                        right += viewport.x > 1f ? 1 : 0;
                        bottom += viewport.y < 0f ? 1 : 0;
                        top += viewport.y > 1f ? 1 : 0;
                        builder.Append("corner").Append(cornerIndex++).Append("World=")
                            .Append(Format(world)).Append(' ');
                        builder.Append("corner").Append(cornerIndex - 1).Append("Viewport=")
                            .Append(Format(viewport)).Append(' ');
                    }
                }
            }

            builder.Append("viewportMin=").Append(Format(min)).Append(' ');
            builder.Append("viewportMax=").Append(Format(max)).Append(' ');
            builder.Append("viewportCenter=").Append(Format((min + max) * 0.5f)).Append(' ');
            builder.Append("behindCount=").Append(behind).Append(' ');
            builder.Append("outsideLeftCount=").Append(left).Append(' ');
            builder.Append("outsideRightCount=").Append(right).Append(' ');
            builder.Append("outsideBottomCount=").Append(bottom).Append(' ');
            builder.Append("outsideTopCount=").Append(top);
            return builder.ToString();
        }

        private static bool TryValidateDemoStageControl(
            GameplayUiFlowInstaller installer,
            GameplaySceneHost host,
            ICampaignStageSequenceResolverProvider resolverProvider,
            CampaignStageSequenceResolver sequenceResolver,
            out string failureReason)
        {
            var contextProvider = resolverProvider as IDemoStageControlGameplayContextProvider;
            var completionBridge = host.UiAccess?.DemoStageControlCompletionBridge;
            var context = default(DemoStageControlGameplayContext);
            if (contextProvider == null ||
                !contextProvider.TryCreateDemoStageControlContext(out context) ||
                !context.IsValid ||
                completionBridge == null ||
                !ReferenceEquals(sequenceResolver, context.SequenceResolver))
            {
                failureReason =
                    $"contextProvider={(contextProvider != null)} contextValid={context.IsValid} " +
                    $"completionBridge={(completionBridge != null)} sameSequence=" +
                    $"{ReferenceEquals(sequenceResolver, context.SequenceResolver)}";
                return false;
            }

            var launchRouter = new CurrentSceneStageLaunchRouter(installer.gameObject.scene.name);
            var service = new DemoStageControlService(
                DemoStageControlSettings.EnabledByDefault(),
                context.StageCatalogProvider,
                context.SequenceResolver,
                context.CampaignBridge,
                new DemoStageControlLaunchBridge(
                    launchRouter,
                    () => launchRouter.IsLaunchInProgress),
                completionBridge);
            var demoStages = service.GetStages();
            var sequenceEntries = sequenceResolver.Entries;
            if (demoStages.Count != sequenceEntries.Count)
            {
                failureReason =
                    $"count mismatch demo={demoStages.Count} sequence={sequenceEntries.Count}";
                return false;
            }

            var catalogResolver = new StageCatalogResolver(context.StageCatalogProvider);
            for (var i = 0; i < sequenceEntries.Count; i++)
            {
                var sequenceEntry = sequenceEntries[i];
                var demoStage = demoStages[i];
                if (!catalogResolver.TryResolve(sequenceEntry.StageId, out var catalogEntry) ||
                    catalogEntry == null)
                {
                    failureReason =
                        $"sequence stage '{sequenceEntry.StageId.Value}' has no catalog entry";
                    return false;
                }

                var presentationKey = catalogEntry.PresentationDefinition != null
                    ? catalogEntry.PresentationDefinition.DisplayNameKey
                    : string.Empty;
                var expectedDisplayNameKey = string.IsNullOrWhiteSpace(presentationKey)
                    ? StageDisplayNameKeys.ForStage(sequenceEntry.StageId)
                    : StageDisplayNameKeys.Normalize(presentationKey);
                if (!demoStage.StageId.Equals(sequenceEntry.StageId) ||
                    !string.Equals(
                        demoStage.DisplayNameKey,
                        expectedDisplayNameKey,
                        StringComparison.Ordinal) ||
                    demoStage.IsUnlocked != catalogEntry.IsInitiallyAvailable ||
                    catalogEntry.CampaignParticipation != CampaignParticipation.Campaign)
                {
                    failureReason =
                        $"stage[{i}] expected={sequenceEntry.StageId.Value}/" +
                        $"{expectedDisplayNameKey}/{catalogEntry.IsInitiallyAvailable} " +
                        $"actual={demoStage.StageId.Value}/{demoStage.DisplayNameKey}/" +
                        $"{demoStage.IsUnlocked} participation=" +
                        $"{catalogEntry.CampaignParticipation}";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static void HandleBuiltInCameraRendered(Camera camera)
        {
            if (camera != null)
            {
                RenderedCameraInstanceIds.Add(camera.GetInstanceID());
            }
        }

        private static void HandleScriptableCameraRendered(
            ScriptableRenderContext context,
            Camera camera)
        {
            HandleBuiltInCameraRendered(camera);
        }

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static string Format(Vector2 value)
        {
            return $"({Float(value.x)},{Float(value.y)})";
        }

        private static string Format(Vector3 value)
        {
            return $"({Float(value.x)},{Float(value.y)},{Float(value.z)})";
        }

        private static string Float(float value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }

        private static bool HasArgument(string argument)
        {
            return Array.Exists(
                Environment.GetCommandLineArgs(),
                candidate => string.Equals(candidate, argument, StringComparison.Ordinal));
        }

        private static string ReadArgumentValue(string argument)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.Ordinal) &&
                    i + 1 < args.Length)
                {
                    return args[i + 1] ?? string.Empty;
                }

                var prefix = argument + "=";
                if (args[i] != null && args[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return args[i].Substring(prefix.Length);
                }
            }

            return string.Empty;
        }

        private static void Fail(string diagnostic)
        {
            Debug.LogError($"{FailureMarker} {diagnostic}");
            UnityEngine.Application.Quit(2);
        }
    }
}
