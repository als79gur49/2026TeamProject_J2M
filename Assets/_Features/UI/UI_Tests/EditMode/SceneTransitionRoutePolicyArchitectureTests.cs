using System;
using System.IO;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Feature.UI.Tests
{
    public sealed class SceneTransitionRoutePolicyArchitectureTests
    {
        private static readonly SceneTransitionIntent[] ExpectedProductionIntents =
        {
            SceneTransitionIntent.StageAdvance,
            SceneTransitionIntent.DeathRetry,
            SceneTransitionIntent.ManualRetry,
            SceneTransitionIntent.DemoStageRelaunch,
            SceneTransitionIntent.GameplayEntry,
            SceneTransitionIntent.ReturnToMainMenu,
            SceneTransitionIntent.CinematicToGameplay,
            SceneTransitionIntent.CinematicToMainMenu,
        };

        [Test]
        public void ProductionIntentManifestExecutorAndVisualPolicySets_AreExactlyEight()
        {
            var expected = ExpectedProductionIntents.OrderBy(intent => intent).ToArray();
            var enumSubset = Enum.GetValues(typeof(SceneTransitionIntent))
                .Cast<SceneTransitionIntent>()
                .Where(intent => (int)intent > 0 && (int)intent < 100)
                .OrderBy(intent => intent)
                .ToArray();
            var manifest = SceneTransitionRoutePolicyCatalog.All
                .Where(policy =>
                    policy.Classification == SceneTransitionRouteClassification.Production)
                .Select(policy => policy.Intent)
                .OrderBy(intent => intent)
                .ToArray();
            var executorKeys = SceneTransitionRoutePolicyCatalog.All
                .Where(policy =>
                    policy.Classification == SceneTransitionRouteClassification.Production &&
                    policy.DestinationExecutorKind !=
                    SceneTransitionDestinationExecutorKind.Unknown)
                .Select(policy => policy.Intent)
                .OrderBy(intent => intent)
                .ToArray();
            var visualPolicyKeys =
                GameplayEntryTransitionVisualSnapshotRegistry.SupportedProductionIntents
                    .Concat(MainMenuTransitionVisualPolicy.SupportedProductionIntents)
                    .OrderBy(intent => intent)
                    .ToArray();

            Assert.That(expected, Has.Length.EqualTo(8));
            Assert.That(enumSubset, Is.EqualTo(expected));
            Assert.That(manifest, Is.EqualTo(expected));
            Assert.That(executorKeys, Is.EqualTo(expected));
            Assert.That(visualPolicyKeys, Is.EqualTo(expected));
            Assert.That(visualPolicyKeys.Distinct().Count(), Is.EqualTo(8));
        }

        [TestCase(SceneTransitionIntent.StageAdvance, SceneTransitionDestinationKind.Gameplay, SceneTransitionDestinationExecutorKind.GameplayEntry)]
        [TestCase(SceneTransitionIntent.DeathRetry, SceneTransitionDestinationKind.Gameplay, SceneTransitionDestinationExecutorKind.GameplayEntry)]
        [TestCase(SceneTransitionIntent.ManualRetry, SceneTransitionDestinationKind.Gameplay, SceneTransitionDestinationExecutorKind.GameplayEntry)]
        [TestCase(SceneTransitionIntent.DemoStageRelaunch, SceneTransitionDestinationKind.Gameplay, SceneTransitionDestinationExecutorKind.GameplayEntry)]
        [TestCase(SceneTransitionIntent.GameplayEntry, SceneTransitionDestinationKind.Gameplay, SceneTransitionDestinationExecutorKind.GameplayEntry)]
        [TestCase(SceneTransitionIntent.CinematicToGameplay, SceneTransitionDestinationKind.Gameplay, SceneTransitionDestinationExecutorKind.GameplayEntry)]
        [TestCase(SceneTransitionIntent.ReturnToMainMenu, SceneTransitionDestinationKind.MainMenu, SceneTransitionDestinationExecutorKind.MainMenuEntry)]
        [TestCase(SceneTransitionIntent.CinematicToMainMenu, SceneTransitionDestinationKind.MainMenu, SceneTransitionDestinationExecutorKind.MainMenuEntry)]
        public void ProductionIntent_MapsToExactDestinationExecutor(
            SceneTransitionIntent intent,
            SceneTransitionDestinationKind destination,
            SceneTransitionDestinationExecutorKind executor)
        {
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(intent);

            Assert.That(policy.DestinationKind, Is.EqualTo(destination));
            Assert.That(policy.DestinationExecutorKind, Is.EqualTo(executor));
            Assert.That(policy.Status, Is.EqualTo(SceneTransitionRouteStatus.Canonical));
        }

        [Test]
        public void ProductionRouteOrigins_EmitOrValidateExplicitSemanticIntent()
        {
            AssertSourceContains(
                "Assets/_Features/Stages/Runtime/ClearFlow/StageProgressAndCompletion.cs",
                "SceneTransitionIntent.StageAdvance",
                "SceneTransitionIntent.ManualRetry");
            AssertSourceContains(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
                "SceneTransitionIntent.DeathRetry",
                "SceneTransitionIntent.ManualRetry");
            AssertSourceContains(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs",
                "SceneTransitionIntent.GameplayEntry");
            AssertSourceContains(
                "Assets/_Features/UI/UI_Flow/Runtime/UIFlowCoordinator.cs",
                "SceneTransitionIntent.ManualRetry",
                "SceneTransitionIntent.ReturnToMainMenu",
                "SceneTransitionIntent.StageAdvance");
            AssertSourceContains(
                "Assets/_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs",
                "SceneTransitionIntent.DemoStageRelaunch");
            AssertSourceContains(
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs",
                "SceneTransitionIntent.CinematicToGameplay");
            AssertSourceContains(
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicMainMenuReturnRouter.cs",
                "SceneTransitionIntent.CinematicToMainMenu");

            var validatingRouters = new[]
            {
                "Assets/_Features/UI/UI_Composition/Runtime/CurrentSceneStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ConfiguredGameplayStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ConfiguredMainMenuReturnRouter.cs",
            };
            foreach (var path in validatingRouters)
            {
                AssertSourceContains(path, "SceneTransitionRoutePolicyCatalog.ResolveProduction");
            }
        }

        [Test]
        public void Routers_DoNotOwnVisualImplementationChoices()
        {
            var paths = new[]
            {
                "Assets/_Features/UI/UI_Composition/Runtime/CurrentSceneStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ConfiguredGameplayStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ConfiguredMainMenuReturnRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicMainMenuReturnRouter.cs",
                "Assets/_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs",
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs",
            };

            var source = string.Join(Environment.NewLine, paths.Select(File.ReadAllText));
            Assert.That(source, Does.Not.Contain("Color.black"));
            Assert.That(source, Does.Not.Contain("TerminalIrisOverlayView"));
            Assert.That(source, Does.Not.Contain(".prefab"));
        }

        [Test]
        public void ProductionRuntimeDirectSceneLoads_MatchExactGovernedWhitelist()
        {
            var actual = Directory.GetFiles("Assets/_Features", "*.cs", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => path.IndexOf("/Editor/", StringComparison.Ordinal) < 0)
                .Where(path => path.IndexOf("/Tests/", StringComparison.Ordinal) < 0)
                .Where(path => path.IndexOf("_Tests/", StringComparison.Ordinal) < 0)
                .Where(path => File.ReadAllText(path).Contains("SceneManager.LoadScene"))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var expected = new[]
            {
                "Assets/_Features/Stages/Runtime/Queries/SceneStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs",
            };

            Assert.That(actual, Is.EqualTo(expected));
            AssertSourceContains(
                expected[0],
                "SceneTransitionIntent.EditorDirectSceneLoad",
                "SceneTransitionIntent.TestInjectedSceneLoad");
            AssertSourceContains(
                expected[1],
                "SceneTransitionRoutePolicyCatalog.ResolveProduction");
        }

        [Test]
        public void GameplayHost_RequiresProductionInstalledRouterProvider()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");

            Assert.That(source, Does.Not.Contain("new SceneNameStageLaunchRouter(currentSceneName)"));
            Assert.That(source, Does.Contain("Production campaign bootstrap requires an IStageLaunchRouterProvider"));
        }

        [Test]
        public void ProductionRouters_DoNotExposeInjectedLoadPortsThroughPublicConstructors()
        {
            Assert.That(
                typeof(ConfiguredGameplayStageLaunchRouter)
                    .GetConstructors()
                    .Single()
                    .GetParameters()
                    .Length,
                Is.EqualTo(1));
            Assert.That(
                typeof(ConfiguredMainMenuReturnRouter)
                    .GetConstructors()
                    .Single()
                    .GetParameters()
                    .Length,
                Is.EqualTo(1));
        }

        [Test]
        public void Coordinator_UsesResolvedStageAdvancePolicyInsteadOfRawNavigationFallback()
        {
            var source = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs");

            Assert.That(source, Does.Contain("routePolicy.Intent == SceneTransitionIntent.StageAdvance"));
            Assert.That(source, Does.Not.Contain("request.NavigationKind == StageNavigationKind.NextStage"));
            Assert.That(source, Does.Contain("SceneTransitionRoutePolicyCatalog.ResolveProduction"));
            Assert.That(source, Does.Contain("SceneTransitionRouteDiagnostic.Format"));
        }

        [Test]
        public void M2AndM3Routes_ShareGameplayEntryExecutorAndDoNotUseGenericCompletionHide()
        {
            var coordinator = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs");
            var installer = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var retryStyle = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/TerminalIrisMotionProfile.cs");

            Assert.That(coordinator, Does.Contain("RunGameplayEntryIrisTransition"));
            Assert.That(coordinator, Does.Contain("SceneTransitionIntent.ManualRetry"));
            Assert.That(coordinator, Does.Contain("SceneTransitionIntent.DemoStageRelaunch"));
            Assert.That(coordinator, Does.Contain("SceneTransitionIntent.DeathRetry"));
            Assert.That(coordinator, Does.Contain("SceneTransitionIntent.GameplayEntry"));
            Assert.That(coordinator, Does.Contain("MainMenuUiFlowInstaller"));
            Assert.That(installer, Does.Contain("IsStrongGameplayDestinationReady"));
            Assert.That(installer, Does.Contain("HasRenderedEntryClosedFrame"));
            Assert.That(installer, Does.Contain("ReleaseSceneEntryCover"));
            Assert.That(retryStyle, Does.Not.Contain("ResultDimVisualSnapshot"));
            Assert.That(retryStyle, Does.Not.Contain("GenericLoading"));
        }

        [Test]
        public void TerminalPlayerSmokeRenderEnvironment_RejectsNullGraphicsInvalidResolutionAndMissingRender()
        {
            Assert.That(
                TerminalPlayerBuildSmokeProbe.TryValidateRenderEnvironment(
                    GraphicsDeviceType.Null,
                    1920,
                    1080,
                    1920,
                    1080,
                    new Rect(0f, 0f, 1920f, 1080f),
                    0,
                    cameraActive: true,
                    cameraRenderAcknowledged: true,
                    out var nullGraphicsFailure),
                Is.False);
            Assert.That(nullGraphicsFailure, Is.EqualTo("graphicsDeviceType=Null"));

            Assert.That(
                TerminalPlayerBuildSmokeProbe.TryValidateRenderEnvironment(
                    GraphicsDeviceType.Direct3D11,
                    1920,
                    1080,
                    1280,
                    720,
                    new Rect(0f, 0f, 1280f, 720f),
                    0,
                    cameraActive: true,
                    cameraRenderAcknowledged: true,
                    out var resolutionFailure),
                Is.False);
            Assert.That(resolutionFailure, Does.Contain("actualResolution=1280x720"));

            Assert.That(
                TerminalPlayerBuildSmokeProbe.TryValidateRenderEnvironment(
                    GraphicsDeviceType.Direct3D11,
                    1920,
                    1080,
                    1920,
                    1080,
                    new Rect(0f, 0f, 1920f, 1080f),
                    0,
                    cameraActive: true,
                    cameraRenderAcknowledged: false,
                    out var renderFailure),
                Is.False);
            Assert.That(
                renderFailure,
                Is.EqualTo("firstOutputCameraRenderAcknowledged=false"));
        }

        [Test]
        public void TerminalPlayerSmokeRenderEnvironment_AcceptsRenderedD3D11Window()
        {
            Assert.That(
                TerminalPlayerBuildSmokeProbe.TryValidateRenderEnvironment(
                    GraphicsDeviceType.Direct3D11,
                    3440,
                    1440,
                    3440,
                    1440,
                    new Rect(0f, 0f, 3440f, 1440f),
                    0,
                    cameraActive: true,
                    cameraRenderAcknowledged: true,
                    out var failureReason),
                Is.True,
                failureReason);
        }

        [Test]
        public void TerminalPlayerSmokeRenderEnvironment_RejectsInactiveCameraInvalidPixelRectAndWrongDisplay()
        {
            Assert.That(
                TerminalPlayerBuildSmokeProbe.TryValidateRenderEnvironment(
                    GraphicsDeviceType.Direct3D11,
                    1920,
                    1080,
                    1920,
                    1080,
                    new Rect(0f, 0f, 1920f, 1080f),
                    0,
                    cameraActive: false,
                    cameraRenderAcknowledged: true,
                    out var inactiveCameraFailure),
                Is.False);
            Assert.That(inactiveCameraFailure, Is.EqualTo("outputCameraActive=false"));

            Assert.That(
                TerminalPlayerBuildSmokeProbe.TryValidateRenderEnvironment(
                    GraphicsDeviceType.Direct3D11,
                    1920,
                    1080,
                    1920,
                    1080,
                    new Rect(0f, 0f, 0f, 1080f),
                    0,
                    cameraActive: true,
                    cameraRenderAcknowledged: true,
                    out var pixelRectFailure),
                Is.False);
            Assert.That(pixelRectFailure, Does.StartWith("cameraPixelRect="));

            Assert.That(
                TerminalPlayerBuildSmokeProbe.TryValidateRenderEnvironment(
                    GraphicsDeviceType.Direct3D11,
                    1920,
                    1080,
                    1920,
                    1080,
                    new Rect(0f, 0f, 1920f, 1080f),
                    1,
                    cameraActive: true,
                    cameraRenderAcknowledged: true,
                    out var targetDisplayFailure),
                Is.False);
            Assert.That(targetDisplayFailure, Is.EqualTo("cameraTargetDisplay=1"));
        }

        [Test]
        public void TerminalPlayerSmokeRunner_UsesHeadfulD3DWindowForRuntimeProcesses()
        {
            var runner = File.ReadAllText("run_tests.sh");
            var probe = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/TerminalPlayerBuildSmokeProbe.cs");
            var functionStart = runner.IndexOf(
                "run_terminal_player_build_smoke()",
                StringComparison.Ordinal);
            var functionEnd = runner.IndexOf(
                "run_typography_visual()",
                functionStart,
                StringComparison.Ordinal);
            Assert.That(functionStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(functionEnd, Is.GreaterThan(functionStart));
            var smokeFunction = runner.Substring(
                functionStart,
                functionEnd - functionStart);

            Assert.That(smokeFunction, Does.Contain("-force-d3d11"));
            Assert.That(smokeFunction, Does.Contain("-screen-width"));
            Assert.That(smokeFunction, Does.Contain("-screen-height"));
            Assert.That(
                smokeFunction,
                Does.Contain("--terminal-player-build-smoke-width"));
            Assert.That(
                smokeFunction,
                Does.Contain("--terminal-player-build-smoke-height"));
            Assert.That(
                runner,
                Does.Not.Contain("\"$player_path\" \\\n            -batchmode"));
            Assert.That(
                runner,
                Does.Not.Contain("\"$player_path\" \\\n            -nographics"));
            Assert.That(smokeFunction, Does.Contain("for attempt in 1 2 3"));
            Assert.That(smokeFunction, Does.Contain("gameclear-sequential|stage-4-2"));
            Assert.That(smokeFunction, Does.Contain("defeat-3-to-2|stage-2-2|defeat|pointer|3|1"));
            Assert.That(smokeFunction, Does.Contain("defeat-2-to-1|stage-2-2|defeat|pointer|2|1"));
            Assert.That(smokeFunction, Does.Contain("defeat-1-to-0|stage-2-2|defeat|pointer|1|1"));
            Assert.That(
                smokeFunction,
                Does.Contain(
                    "mainmenu-new-game|stage-1-1|mainmenu-gameplay|pointer||1|NewGame|${campaign_first_stage}|"));
            Assert.That(
                smokeFunction,
                Does.Contain(
                    "mainmenu-continue|stage-1-1|mainmenu-gameplay|pointer||1|Continue|${campaign_continue_stage}|${campaign_continue_stage}"));
            Assert.That(
                smokeFunction,
                Does.Contain("--terminal-player-campaign-expected-intent"));
            Assert.That(
                smokeFunction,
                Does.Contain("--terminal-player-campaign-expected-stage"));
            Assert.That(smokeFunction, Does.Contain("pause-retry|stage-1-1|pause-retry|pointer||2"));
            Assert.That(smokeFunction, Does.Contain("level-failed-restart|stage-2-2|level-failed-restart|pointer|1|2"));
            Assert.That(smokeFunction, Does.Contain("TERMINAL_PLAYER_SMOKE_PROFILE"));
            Assert.That(smokeFunction, Does.Contain("--terminal-player-build-smoke-revision"));
            Assert.That(smokeFunction, Does.Contain("--capture-campaign-temp-slot-chances"));
            Assert.That(smokeFunction, Does.Contain("wait_for_terminal_player_exit"));
            Assert.That(smokeFunction, Does.Contain("terminate_terminal_player_processes"));
            Assert.That(
                smokeFunction,
                Does.Contain(
                    "TERMINAL_PLAYER_BUILD_SMOKE:PASS.*requestedResolution=${TERMINAL_PLAYER_SMOKE_WIDTH}"));
            Assert.That(
                probe,
                Does.Contain("UnityEngine.Application.runInBackground = true"));
            Assert.That(
                probe,
                Does.Contain("InputSettings.BackgroundBehavior.IgnoreFocus"));
            Assert.That(probe, Does.Contain("WaitForRenderedInteractionFrame"));
            Assert.That(probe, Does.Contain("WaitForInputWindowFocus"));
            Assert.That(probe, Does.Contain("SetForegroundWindow"));
            Assert.That(probe, Does.Contain("InputSystem.DisableDevice"));
            Assert.That(probe, Does.Contain("RunDefeatSmoke"));
            Assert.That(
                probe,
                Does.Contain("defeat destination resolution mismatch"));
            Assert.That(probe, Does.Contain("private void LateUpdate()"));
            Assert.That(probe, Does.Contain("MaintainRequestedResolution();"));
            Assert.That(probe, Does.Contain("RunMainMenuGameplaySmoke"));
            Assert.That(probe, Does.Contain("RunPauseRetrySmoke"));
            Assert.That(probe, Does.Contain("RunLevelFailedRestartContinuation"));
            Assert.That(probe, Does.Contain("MainMenuEntryPresentationRegistry"));
            Assert.That(probe, Does.Contain("TryValidateSingleActiveEventSystem"));
            Assert.That(probe, Does.Not.Contain("PausePopupView.ClickRetry()"));
            Assert.That(probe, Does.Not.Contain("LevelFailedScreenView.ClickRestartLevel()"));
            Assert.That(probe, Does.Not.Contain("GameClearScreenView.ClickMain()"));
            Assert.That(probe, Does.Contain("hudCue=0 resultCue=1"));
            Assert.That(probe, Does.Not.Contain("InputSystem.Update()"));
        }

        [Test]
        public void ValidationOutputRoot_DefaultsStayLocalAndExplicitOverridesStayExternal()
        {
            var runner = File.ReadAllText("run_tests.sh");

            Assert.That(runner, Does.Contain("CODEX_VALIDATION_ROOT=\"${CODEX_VALIDATION_ROOT:-}\""));
            Assert.That(runner, Does.Contain("TEST_RESULTS_ROOT:-$DEFAULT_TEST_RESULTS_ROOT"));
            Assert.That(runner, Does.Contain("TEST_LOG_ROOT:-$DEFAULT_TEST_LOG_ROOT"));
            Assert.That(runner, Does.Contain("CAPTURE_ROOT:-$DEFAULT_CAPTURE_ROOT"));
            Assert.That(runner, Does.Contain("PLAYER_BUILD_ROOT:-$DEFAULT_PLAYER_BUILD_ROOT"));
            Assert.That(runner, Does.Contain("$PROJECT_PATH_WSL/TestResults"));
            Assert.That(runner, Does.Contain("$PROJECT_PATH_WSL/TestLogs"));
        }

        [Test]
        public void M3GameplayEntry_UsesAuthoredMainMenuBlueWithoutResultSnapshotOwnership()
        {
            var profile = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/TerminalIrisMotionProfile.cs");
            var mainMenu = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(
                profile,
                Does.Contain("CreateGameplayEntryTransitionVisualSnapshot"));
            Assert.That(profile, Does.Contain("GameplayEntrySourceCloseVisualKind.MainMenuIris"));
            Assert.That(profile, Does.Contain("GameplayEntryFocusPolicy.AuthoredThenScreenCenter"));
            Assert.That(mainMenu, Does.Contain("ResolveGameplayEntryClose"));
            Assert.That(mainMenu, Does.Contain("HasRenderedEntryClosedFrame"));
            Assert.That(mainMenu, Does.Not.Contain("ResultDimVisualSnapshot"));
            var sourceCloseStart = mainMenu.IndexOf(
                "TryBeginGameplayEntrySourceClose",
                StringComparison.Ordinal);
            var sourceCloseEnd = mainMenu.IndexOf(
                "TickGameplayEntrySourceClose",
                sourceCloseStart,
                StringComparison.Ordinal);
            Assert.That(sourceCloseStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(sourceCloseEnd, Is.GreaterThan(sourceCloseStart));
            var sourceClose = mainMenu.Substring(
                sourceCloseStart,
                sourceCloseEnd - sourceCloseStart);
            Assert.That(
                sourceClose,
                Does.Contain("ConfigureTransitionColor(visual.SourceCloseColor)"));
            Assert.That(sourceClose, Does.Not.Contain("Color.black"));
        }

        [Test]
        public void M4MainMenuAndCinematicRoutes_UseRenderedDestinationAndOpaqueOwnershipSeams()
        {
            var coordinator = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs");
            var mainMenu = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            var cinematic = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicVideoOverlayView.cs");
            var profile = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/TerminalIrisMotionProfile.cs");

            Assert.That(coordinator, Does.Contain("RunMainMenuDestinationTransition"));
            Assert.That(coordinator, Does.Contain("TryBeginMainMenuReturnSourceClose"));
            Assert.That(coordinator, Does.Contain("TryTransferToPersistentCover"));
            Assert.That(mainMenu, Does.Contain("IsStrongMainMenuDestinationReady"));
            Assert.That(mainMenu, Does.Contain("HasRenderedEntryClosedFrame"));
            Assert.That(mainMenu, Does.Contain("ReleaseMainMenuEntryCover"));
            Assert.That(cinematic, Does.Contain("Canvas.willRenderCanvases"));
            Assert.That(cinematic, Does.Contain("TryAcknowledgeCinematicOpaqueRendered"));
            Assert.That(profile, Does.Contain("MainMenuTransitionVisualPolicy"));
            Assert.That(profile, Does.Contain("CinematicOpaqueOwner"));
            Assert.That(profile, Does.Not.Contain("ResultDimVisualSnapshot"));
        }

        [Test]
        public void GameplayInputHost_BlocksTicksAndActionsForAnyActiveGameplayEntrySession()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs");

            Assert.That(source, Does.Contain("_sceneEntrySession?.IsActive"));
            Assert.That(source, Does.Contain("UnbindActions();"));
            Assert.That(source, Does.Contain("ClearAllPendingInputForTerminalSession();"));
            Assert.That(source, Does.Contain("RunSingleTick()"));
        }

        [Test]
        public void Coordinator_RejectsUnknownIntentBeforeTransitionAcceptance()
        {
            var coordinatorObject = new GameObject(nameof(Coordinator_RejectsUnknownIntentBeforeTransitionAcceptance));
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var request = new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Retry,
                    "unregistered-route");

                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.TryStartStageTransition(request, "UIAudioScene"));
                Assert.That(coordinator.AcceptedTransitionCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void M5LegacyFallbackSymbols_CannotReenterProductionTransitionCode()
        {
            var coordinator = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs");
            var stageProfiles = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Queries/StageTransitionTypes.cs");
            var routeManifest = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Queries/SceneTransitionRoutePolicy.cs");
            var contentCatalog = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionOverlayContentCatalog.cs");
            var contentResolver = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionOverlayContentResolver.cs");
            var overlayContract = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/ISceneTransitionOverlayShellView.cs");
            var terminalSessions = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Queries/TerminalTransitionTypes.cs");

            Assert.That(coordinator, Does.Contain("RunStageAdvanceTransition"));
            Assert.That(coordinator, Does.Not.Contain("TryHideOverlay"));
            Assert.That(coordinator, Does.Not.Contain("RequestOpaqueTakeover()"));
            Assert.That(coordinator, Does.Not.Contain("FailedHoldingBlack"));
            Assert.That(stageProfiles, Does.Not.Contain("GenericLoading"));
            Assert.That(routeManifest, Does.Not.Contain("LegacyPendingMigration"));
            Assert.That(routeManifest, Does.Not.Contain("KnownLegacyProductionRoutes"));
            Assert.That(contentCatalog, Does.Not.Contain("GenericFallbackPrefab"));
            Assert.That(contentCatalog, Does.Not.Contain("_genericFallbackPrefab"));
            Assert.That(contentCatalog, Does.Not.Contain("_fallbackOverlayKind"));
            Assert.That(contentResolver, Does.Not.Contain("FindByOverlayKind"));
            Assert.That(overlayContract, Does.Not.Contain("RequestOpaqueTakeover();"));
            Assert.That(overlayContract, Does.Not.Contain("SetPersistentBlackOpacity"));
            var overlayShell = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionOverlayShellView.cs");
            Assert.That(overlayShell, Does.Not.Contain("opaqueBlack"));
            Assert.That(overlayShell, Does.Not.Contain("PersistentBlackOpacityForTests"));
            Assert.That(terminalSessions, Does.Not.Contain("TryAdoptLegacyClaim"));
            Assert.That(terminalSessions, Does.Not.Contain("TryBegin(long"));
            Assert.That(terminalSessions, Does.Not.Contain("TryAdvance(long"));
            Assert.That(terminalSessions, Does.Not.Contain("TryComplete(long"));
            Assert.That(terminalSessions, Does.Not.Contain("IsReady(long"));
            Assert.That(terminalSessions, Does.Not.Contain("Signal(long"));
            Assert.That(terminalSessions, Does.Not.Contain("RequestReveal(long"));
            Assert.That(terminalSessions, Does.Not.Contain("CompleteHandoff(long"));
            Assert.That(terminalSessions, Does.Not.Contain("Cancel(long"));
            Assert.That(terminalSessions, Does.Not.Contain("TryFailHoldingBlack"));
            Assert.That(terminalSessions, Does.Not.Contain("FailedHoldingBlack"));
        }

        private static void AssertSourceContains(string path, params string[] expectedTokens)
        {
            var source = File.ReadAllText(path);
            foreach (var token in expectedTokens)
            {
                Assert.That(source, Does.Contain(token), $"{path} must contain {token}.");
            }
        }
    }
}
