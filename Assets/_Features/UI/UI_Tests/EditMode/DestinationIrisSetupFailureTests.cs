using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class DestinationIrisSetupFailureTests
    {
        private const string MainMenuScreenPrefabPath =
            "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab";
        private const string PopupCatalogPath =
            "Assets/_Features/UI/UI_Popups/Prefabs/GameplayPopupPrefabCatalog.asset";
        private const string UiAudioCueMapPath =
            "Assets/_Features/UI/UI_Composition/Authoring/UiAudioCueMap_V1.asset";
        private const string RouteConfigPath =
            "Assets/_Features/UI/UI_Composition/Authoring/GameplayStageLaunchRouteConfig.asset";

        [SetUp]
        public void SetUp()
        {
            TerminalIrisOverlayView.BeforeSetupOperationForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            TerminalIrisOverlayView.BeforeSetupOperationForTests = null;
            GameplayEntryTransitionVisualSnapshotRegistry.ResetForTests();
            MainMenuTransitionVisualPolicy.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(null);

            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        [TestCase("ManualRetry")]
        [TestCase("GameplayEntry")]
        [TestCase("StageAdvance")]
        [TestCase("DeathRetry")]
        public void EnsureTerminalTransitionPortThrow_FailsCapturedLoadingSceneEntry(
            string transitionIntentName)
        {
            var transitionIntent = Enum.Parse<SceneTransitionIntent>(
                transitionIntentName);
            var root = new GameObject(
                $"loading-owner-terminal-setup-{transitionIntent}");
            try
            {
                var host = CreateHost(root);
                var installer = root.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignComicSequenceOverlayPrefab(installer);
                var invalidRoot = new GameObject("InvalidGameplayUiCanvasRoot")
                    .AddComponent<GameplayUiCanvasRootView>();
                invalidRoot.transform.SetParent(root.transform, false);
                SetPrivateField(installer, "_rootView", invalidRoot);

                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.CurrentSceneGeneration;
                var terminalToken = default(TerminalSessionToken);
                if (transitionIntent == SceneTransitionIntent.DeathRetry)
                {
                    var terminalClaim = authority.TryClaim(
                        new TerminalClaimRequest(
                            TerminalTransitionKind.Defeat,
                            sourceGeneration,
                            TerminalDestinationKind.ReloadedGameplay));
                    Assert.That(terminalClaim.Accepted, Is.True);
                    terminalToken = terminalClaim.Token;
                    Assert.That(
                        authority.TryBindTransition(
                            terminalToken,
                            transitionId: 9900,
                            TerminalDestinationKind.ReloadedGameplay),
                        Is.True);
                    Assert.That(
                        authority.TryAdvancePhase(
                            terminalToken,
                            TerminalSessionPhase.WaitingDestinationReady),
                        Is.True);
                }

                var token = PrepareLoadingSceneEntry(
                    transitionIntent,
                    sourceGeneration,
                    transitionId: 9900);
                var destinationGeneration = authority.RegisterSceneBootstrap(
                    9901,
                    "loading-owner-terminal-setup-destination");

                var exception = CaptureException(() => installer.Install(host));
                var session = SceneEntryPresentationRegistry.Current;

                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(session.IsActive, Is.True);
                Assert.That(session.Token, Is.EqualTo(token));
                Assert.That(session.TransitionIntent, Is.EqualTo(transitionIntent));
                Assert.That(
                    session.DestinationStageId,
                    Is.EqualTo(StageId.CreateOrThrow("stage-0-1")));
                Assert.That(
                    session.DestinationSceneGeneration,
                    Is.Zero,
                    "Destination registration must remain after terminal/root setup.");
                Assert.That(
                    authority.CurrentSceneGeneration,
                    Is.EqualTo(destinationGeneration));
                Assert.That(
                    session.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                Assert.That(
                    session.FailureReason,
                    Does.Contain("DESTINATION_ENTRY_INSTALL_FAILED"));
                Assert.That(
                    SceneEntryPresentationRegistry.IsActive,
                    Is.True,
                    "The failed owner must retain persistent-cover and input admission ownership.");

                if (transitionIntent == SceneTransitionIntent.DeathRetry)
                {
                    var terminalSession = authority.Current;
                    Assert.That(terminalSession.Token, Is.EqualTo(terminalToken));
                    Assert.That(
                        terminalSession.Phase,
                        Is.EqualTo(TerminalSessionPhase.FailedHoldingCover));
                    Assert.That(
                        terminalSession.FailureReason,
                        Is.EqualTo(exception.Message));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EnsureTerminalTransitionPortThrow_DefeatReportsOwnersIndependently()
        {
            var root = new GameObject(
                nameof(EnsureTerminalTransitionPortThrow_DefeatReportsOwnersIndependently));
            var reportException = new InvalidOperationException(
                "LOADING_SCENE_ENTRY_REPORT_FAILURE");
            var token = default(SceneEntrySessionToken);
            void ThrowAfterSceneEntryFailure(SceneEntryPresentationSnapshot snapshot)
            {
                if (snapshot.Token == token &&
                    snapshot.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    throw reportException;
                }
            }

            try
            {
                var host = CreateHost(root);
                var installer = root.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignComicSequenceOverlayPrefab(installer);
                var invalidRoot = new GameObject("InvalidGameplayUiCanvasRoot")
                    .AddComponent<GameplayUiCanvasRootView>();
                invalidRoot.transform.SetParent(root.transform, false);
                SetPrivateField(installer, "_rootView", invalidRoot);

                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.CurrentSceneGeneration;
                var terminalClaim = authority.TryClaim(
                    new TerminalClaimRequest(
                        TerminalTransitionKind.Defeat,
                        sourceGeneration,
                        TerminalDestinationKind.ReloadedGameplay));
                Assert.That(terminalClaim.Accepted, Is.True);
                Assert.That(
                    authority.TryBindTransition(
                        terminalClaim.Token,
                        transitionId: 9902,
                        TerminalDestinationKind.ReloadedGameplay),
                    Is.True);
                Assert.That(
                    authority.TryAdvancePhase(
                        terminalClaim.Token,
                        TerminalSessionPhase.WaitingDestinationReady),
                    Is.True);
                token = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.DeathRetry,
                    sourceGeneration,
                    transitionId: 9902);
                authority.RegisterSceneBootstrap(
                    9903,
                    "loading-owner-dual-report-destination");
                SceneEntryPresentationRegistry.ReadModel.Changed +=
                    ThrowAfterSceneEntryFailure;

                var exception = CaptureException(() => installer.Install(host));

                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception, Is.Not.SameAs(reportException));
                Assert.That(
                    exception.Data["SceneEntryFailureReportingFailure"],
                    Is.SameAs(reportException));
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                Assert.That(
                    TerminalSessionRegistry.Current.Token,
                    Is.EqualTo(terminalClaim.Token));
                Assert.That(
                    TerminalSessionRegistry.Current.Phase,
                    Is.EqualTo(TerminalSessionPhase.FailedHoldingCover));
                Assert.That(
                    TerminalSessionRegistry.Current.FailureReason,
                    Is.EqualTo(exception.Message));
            }
            finally
            {
                SceneEntryPresentationRegistry.ReadModel.Changed -=
                    ThrowAfterSceneEntryFailure;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("NoActiveSceneEntry")]
        [TestCase("StaleToken")]
        [TestCase("NewerToken")]
        [TestCase("GenerationMismatch")]
        [TestCase("IntentMismatch")]
        [TestCase("DestinationMismatch")]
        [TestCase("WaitingRuntimeReady")]
        [TestCase("Completed")]
        [TestCase("AlreadyFailed")]
        public void LoadingSceneEntryFailureGuard_PreservesNonMatchingOwner(
            string isolationCase)
        {
            var authority = TerminalSessionRegistry.Authority;
            var sourceGeneration = authority.RegisterSceneBootstrap(
                9904,
                "loading-owner-guard-source");
            var token = PrepareLoadingSceneEntry(
                SceneTransitionIntent.ManualRetry,
                sourceGeneration,
                transitionId: 9905);
            var expectedOwner = SceneEntryPresentationRegistry.Current;
            var expectedDestinationGeneration = authority.RegisterSceneBootstrap(
                9906,
                "loading-owner-guard-destination");

            switch (isolationCase)
            {
                case "NoActiveSceneEntry":
                    SceneEntryPresentationRegistry.ResetForTests();
                    break;
                case "StaleToken":
                    expectedOwner = CopyWithToken(
                        expectedOwner,
                        new SceneEntrySessionToken(token.Value + 1000));
                    break;
                case "NewerToken":
                    SceneEntryPresentationRegistry.ResetForTests();
                    Assert.That(
                        SceneEntryPresentationRegistry.TryClaim(
                            SceneTransitionIntent.ManualRetry,
                            StageId.CreateOrThrow("stage-0-1"),
                            sourceGeneration,
                            out var dummyToken),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryCancelClaim(dummyToken),
                        Is.True);
                    PrepareLoadingSceneEntry(
                        SceneTransitionIntent.ManualRetry,
                        sourceGeneration,
                        transitionId: 9907);
                    break;
                case "GenerationMismatch":
                    authority.RegisterSceneBootstrap(
                        9908,
                        "loading-owner-guard-generation-mismatch");
                    break;
                case "IntentMismatch":
                    SceneEntryPresentationRegistry.ResetForTests();
                    PrepareLoadingSceneEntry(
                        SceneTransitionIntent.GameplayEntry,
                        sourceGeneration,
                        transitionId: 9905);
                    break;
                case "DestinationMismatch":
                    SceneEntryPresentationRegistry.ResetForTests();
                    PrepareLoadingSceneEntry(
                        SceneTransitionIntent.ManualRetry,
                        sourceGeneration,
                        transitionId: 9905,
                        destinationStageId: "stage-0-2");
                    break;
                case "WaitingRuntimeReady":
                    Assert.That(
                        SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                            token,
                            expectedDestinationGeneration),
                        Is.True);
                    break;
                case "Completed":
                    Assert.That(
                        SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                            token,
                            expectedDestinationGeneration),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryAdvance(
                            token,
                            SceneEntryPresentationPhase.EntryIrisClosed),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryAdvance(
                            token,
                            SceneEntryPresentationPhase.Opening),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryComplete(token),
                        Is.True);
                    break;
                case "AlreadyFailed":
                    Assert.That(
                        SceneEntryPresentationRegistry.TryFailHoldingCover(
                            token,
                            "existing loading owner failure"),
                        Is.True);
                    break;
                default:
                    Assert.Fail($"Unknown isolation case: {isolationCase}");
                    break;
            }

            var before = SceneEntryPresentationRegistry.Current;
            InvokePrivateStatic(
                typeof(GameplayUiFlowInstaller),
                "ReportCapturedLoadingSceneEntryFailureIfOwned",
                expectedOwner,
                expectedDestinationGeneration,
                "UNEXPECTED_LOADING_OWNER_MUTATION",
                isolationCase);
            var after = SceneEntryPresentationRegistry.Current;
            AssertSceneEntrySnapshotEqual(before, after);
        }

        [TestCase("ConfigureTransitionColor", "ManualRetry")]
        [TestCase("Show", "ManualRetry")]
        [TestCase("ApplyClosedEntry", "ManualRetry")]
        [TestCase("ConfigureTransitionColor", "GameplayEntry")]
        [TestCase("Show", "GameplayEntry")]
        [TestCase("ApplyClosedEntry", "GameplayEntry")]
        [TestCase("ConfigureTransitionColor", "StageAdvance")]
        [TestCase("Show", "StageAdvance")]
        [TestCase("ApplyClosedEntry", "StageAdvance")]
        [TestCase("ConfigureTransitionColor", "DeathRetry")]
        [TestCase("Show", "DeathRetry")]
        [TestCase("ApplyClosedEntry", "DeathRetry")]
        public void DestinationIrisSetupThrow_FailsExactGameplaySceneEntryOnce(
            string failureOperationName,
            string transitionIntentName)
        {
            var failureOperation = Enum.Parse<TerminalIrisSetupOperation>(
                failureOperationName);
            var transitionIntent = Enum.Parse<SceneTransitionIntent>(
                transitionIntentName);
            var root = new GameObject(
                $"gameplay-destination-iris-{transitionIntent}-{failureOperation}");
            try
            {
                var host = CreateHost(root);
                var installer = root.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.CurrentSceneGeneration;
                var terminalToken = default(TerminalSessionToken);
                if (transitionIntent == SceneTransitionIntent.DeathRetry)
                {
                    InvokePrivateNoArgs(
                        installer,
                        "UnsubscribeTerminalSession");
                    var terminalClaim = authority.TryClaim(
                        new TerminalClaimRequest(
                            TerminalTransitionKind.Defeat,
                            sourceGeneration,
                            TerminalDestinationKind.ReloadedGameplay));
                    Assert.That(terminalClaim.Accepted, Is.True);
                    terminalToken = terminalClaim.Token;
                    Assert.That(
                        authority.TryBindTransition(
                            terminalToken,
                            transitionId: 9910,
                            TerminalDestinationKind.ReloadedGameplay),
                        Is.True);
                    Assert.That(
                        authority.TryAdvancePhase(
                            terminalToken,
                            TerminalSessionPhase.WaitingDestinationReady),
                        Is.True);
                }

                var token = PrepareLoadingSceneEntry(
                    transitionIntent,
                    sourceGeneration,
                    transitionId: 9910);
                var destinationGeneration = authority.RegisterSceneBootstrap(
                    9911,
                    "destination-iris-gameplay");
                Assert.That(
                    SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                        token,
                        destinationGeneration),
                    Is.True);
                if (transitionIntent == SceneTransitionIntent.DeathRetry)
                {
                    InvokePrivateNoArgs(
                        installer,
                        "SubscribeTerminalSession");
                }

                GameplayEntryTransitionVisualSnapshotRegistry.Capture(
                    token,
                    SceneTransitionRoutePolicyCatalog.ResolveProduction(
                        transitionIntent),
                    stageAdvanceColor:
                        transitionIntent == SceneTransitionIntent.StageAdvance
                            ? Color.blue
                            : null);

                var expected = new InvalidOperationException(
                    $"GAMEPLAY_DESTINATION_IRIS_{failureOperation}_TEST_FAILURE");
                var calls = 0;
                TerminalIrisOverlayView.BeforeSetupOperationForTests = operation =>
                {
                    if (operation != failureOperation)
                    {
                        return;
                    }

                    calls++;
                    throw expected;
                };

                var first = CaptureException(
                    () => InvokeTick(installer, "TickSceneEntryPresentation"));
                var firstSession = SceneEntryPresentationRegistry.Current;
                var iris = installer.RootView.TerminalIrisOverlayView;
                var callsBeforeSecond = calls;
                var second = CaptureException(
                    () => InvokeTick(installer, "TickSceneEntryPresentation"));
                var secondSession = SceneEntryPresentationRegistry.Current;

                Assert.That(first, Is.SameAs(expected), "Original setup exception must remain primary.");
                Assert.That(firstSession.IsActive, Is.True);
                Assert.That(firstSession.Token, Is.EqualTo(token));
                Assert.That(
                    firstSession.DestinationSceneGeneration,
                    Is.EqualTo(destinationGeneration));
                Assert.That(
                    firstSession.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                Assert.That(
                    firstSession.FailureReason,
                    Does.Contain("DESTINATION_ENTRY_IRIS_PREPARATION_FAILED"));
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True,
                    "An active failed entry keeps gameplay input admission fail-closed.");
                Assert.That(iris.IsVisible, Is.False, "Partial destination Iris must be hidden.");
                Assert.That(iris.BlocksRaycasts, Is.False);
                Assert.That(
                    GetPrivateField<bool>(installer, "_entryIrisClosedPrepared"),
                    Is.False);
                Assert.That(callsBeforeSecond, Is.EqualTo(1));
                Assert.That(second, Is.Null, "The failed session must not retry next Update.");
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(
                    secondSession.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                Assert.That(secondSession.FailureReason, Is.EqualTo(firstSession.FailureReason));
                if (transitionIntent == SceneTransitionIntent.DeathRetry)
                {
                    var terminalSession = authority.Current;
                    Assert.That(terminalSession.Token, Is.EqualTo(terminalToken));
                    Assert.That(
                        terminalSession.Phase,
                        Is.EqualTo(TerminalSessionPhase.FailedHoldingCover));
                    Assert.That(
                        terminalSession.FailureReason,
                        Does.Contain("DESTINATION_IRIS_PREPARATION_FAILED"));
                }
            }
            finally
            {
                TerminalIrisOverlayView.BeforeSetupOperationForTests = null;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("ConfigureTransitionColor", "ReturnToMainMenu")]
        [TestCase("Show", "ReturnToMainMenu")]
        [TestCase("ApplyClosedEntry", "ReturnToMainMenu")]
        [TestCase("ConfigureTransitionColor", "ComicOutroToMainMenu")]
        [TestCase("Show", "ComicOutroToMainMenu")]
        [TestCase("ApplyClosedEntry", "ComicOutroToMainMenu")]
        public void DestinationIrisSetupThrow_FailsExactMainMenuEntryOnce(
            string failureOperationName,
            string transitionIntentName)
        {
            var failureOperation = Enum.Parse<TerminalIrisSetupOperation>(
                failureOperationName);
            var transitionIntent = Enum.Parse<SceneTransitionIntent>(
                transitionIntentName);
            GameObject root = null;
            ProviderHarness provider = null;
            var sourceScene = default(Scene);
            var destinationScene = default(Scene);
            try
            {
                sourceScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                var sourceGeneration =
                    TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                        sourceScene.handle,
                        sourceScene.name);
                Assert.That(
                    MainMenuEntryPresentationRegistry.TryClaim(
                        transitionIntent,
                        sourceGeneration,
                        "destination-iris-focused-test",
                        out var token),
                    Is.True);
                Assert.That(
                    MainMenuEntryPresentationRegistry.TryBindTransition(
                        token,
                        transitionId: 9920),
                    Is.True);
                Assert.That(
                    MainMenuEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.PersistentCoverReady),
                    Is.True);
                Assert.That(
                    MainMenuEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.Loading),
                    Is.True);

                destinationScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                provider = CreateProvider("stage-0-1");
                root = new GameObject(
                    $"main-menu-destination-iris-{failureOperation}");
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                root.AddComponent<CanvasScaler>();
                root.AddComponent<GraphicRaycaster>();
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignComicSequenceOverlayPrefab(installer);
                root.AddComponent<AudioRuntimeInstaller>();
                root.AddComponent<DisplayRuntimeInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(
                    installer,
                    "_mainMenuScreenPrefab",
                    AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(
                        MainMenuScreenPrefabPath));
                SetPrivateField(
                    installer,
                    "_screenPrefabCatalog",
                    UiTestPrefabAssetUtility.LoadScreenCatalog());
                SetPrivateField(
                    installer,
                    "_popupPrefabCatalog",
                    AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(
                        PopupCatalogPath));
                SetPrivateField(
                    installer,
                    "_uiAudioCueMap",
                    AssetDatabase.LoadAssetAtPath<UiAudioCueMap>(
                        UiAudioCueMapPath));
                SetPrivateField(
                    installer,
                    "_routeConfig",
                    AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(
                        RouteConfigPath));
                SetPrivateField(
                    installer,
                    "_stageCatalogProvider",
                    provider.Provider);
                SetPrivateField(
                    installer,
                    "_campaignStageSequenceDefinition",
                    CampaignStageSequenceTestAsset.LoadProductionDefinition());

                installer.Install();
                var destinationGeneration = installer.SourceSceneGenerationForTests;
                var registered = MainMenuEntryPresentationRegistry.Current;
                Assert.That(registered.Token, Is.EqualTo(token));
                Assert.That(
                    registered.DestinationSceneGeneration,
                    Is.EqualTo(destinationGeneration));
                Assert.That(
                    registered.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.WaitingRuntimeReady));
                MainMenuTransitionVisualPolicy.Capture(
                    token,
                    SceneTransitionRoutePolicyCatalog.ResolveProduction(
                        transitionIntent),
                    comicSequenceOpaqueColor:
                        transitionIntent ==
                        SceneTransitionIntent.ComicOutroToMainMenu
                            ? Color.black
                            : null);

                var eventSystem =
                    UnityEngine.Object.FindFirstObjectByType<EventSystem>();
                Assert.That(eventSystem, Is.Not.Null);
                var inputModule = eventSystem.GetComponent<BaseInputModule>();
                Assert.That(inputModule, Is.Not.Null);
                SetPrivateField(
                    eventSystem,
                    "m_CurrentInputModule",
                    inputModule);
                Assert.That(eventSystem.currentInputModule, Is.SameAs(inputModule));

                var expected = new InvalidOperationException(
                    $"MAIN_MENU_DESTINATION_IRIS_{failureOperation}_TEST_FAILURE");
                var calls = 0;
                TerminalIrisOverlayView.BeforeSetupOperationForTests = operation =>
                {
                    if (operation != failureOperation)
                    {
                        return;
                    }

                    calls++;
                    throw expected;
                };

                Exception first = null;
                for (var attempt = 0; attempt < 4 && calls == 0; attempt++)
                {
                    first = CaptureException(
                        () => InvokeTick(
                            installer,
                            "TickMainMenuEntryPresentation"));
                }

                var firstSession = MainMenuEntryPresentationRegistry.Current;
                var destinationRoot = GetPrivateField<GameplayUiCanvasRootView>(
                    installer,
                    "_mainMenuDestinationRoot");
                var iris = destinationRoot.TerminalIrisOverlayView;
                var callsBeforeSecond = calls;
                var second = CaptureException(
                    () => InvokeTick(installer, "TickMainMenuEntryPresentation"));
                var secondSession = MainMenuEntryPresentationRegistry.Current;

                Assert.That(calls, Is.EqualTo(1), "Destination setup must be reachable exactly once.");
                Assert.That(first, Is.SameAs(expected), "Original setup exception must remain primary.");
                Assert.That(firstSession.IsActive, Is.True);
                Assert.That(firstSession.Token, Is.EqualTo(token));
                Assert.That(
                    firstSession.DestinationSceneGeneration,
                    Is.EqualTo(destinationGeneration));
                Assert.That(
                    firstSession.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                Assert.That(
                    firstSession.FailureReason,
                    Does.Contain("MAIN_MENU_DESTINATION_IRIS_PREPARATION_FAILED"));
                Assert.That(installer.IsGameplayEntryInteractionBlocked, Is.True,
                    "Main Menu interaction must remain fail-closed.");
                Assert.That(iris.IsVisible, Is.False, "Partial destination Iris must be hidden.");
                Assert.That(iris.BlocksRaycasts, Is.False);
                Assert.That(
                    GetPrivateField<bool>(
                        installer,
                        "_mainMenuDestinationClosedPrepared"),
                    Is.False);
                Assert.That(callsBeforeSecond, Is.EqualTo(1));
                Assert.That(second, Is.Null, "The failed session must not retry next Update.");
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(
                    secondSession.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                Assert.That(secondSession.FailureReason, Is.EqualTo(firstSession.FailureReason));
            }
            finally
            {
                TerminalIrisOverlayView.BeforeSetupOperationForTests = null;
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (destinationScene.IsValid() && destinationScene.isLoaded)
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }

                provider?.Dispose();
            }
        }

        [TestCase("StaleToken")]
        [TestCase("NewerToken")]
        [TestCase("GenerationMismatch")]
        [TestCase("Completed")]
        [TestCase("AlreadyFailed")]
        [TestCase("NoActiveSession")]
        public void GameplayDestinationIrisFailureGuard_PreservesNonMatchingSession(
            string isolationCase)
        {
            var authority = TerminalSessionRegistry.Authority;
            var sourceGeneration = authority.RegisterSceneBootstrap(
                9930,
                "gameplay-guard-source");
            var expectedToken = PrepareLoadingSceneEntry(
                SceneTransitionIntent.ManualRetry,
                sourceGeneration,
                transitionId: 9931);
            var expectedGeneration = authority.RegisterSceneBootstrap(
                9932,
                "gameplay-guard-destination");
            Assert.That(
                SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                    expectedToken,
                    expectedGeneration),
                Is.True);

            var reportToken = expectedToken;
            switch (isolationCase)
            {
                case "StaleToken":
                    reportToken = new SceneEntrySessionToken(
                        expectedToken.Value + 1000);
                    break;
                case "NewerToken":
                    SceneEntryPresentationRegistry.ResetForTests();
                    Assert.That(
                        SceneEntryPresentationRegistry.TryClaim(
                            SceneTransitionIntent.ManualRetry,
                            StageId.CreateOrThrow("stage-0-1"),
                            expectedGeneration,
                            out var dummyToken),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryCancelClaim(dummyToken),
                        Is.True);
                    var newerToken = PrepareLoadingSceneEntry(
                        SceneTransitionIntent.ManualRetry,
                        expectedGeneration,
                        transitionId: 9933);
                    var newerGeneration = authority.RegisterSceneBootstrap(
                        9934,
                        "gameplay-guard-newer");
                    Assert.That(
                        SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                            newerToken,
                            newerGeneration),
                        Is.True);
                    break;
                case "GenerationMismatch":
                    authority.RegisterSceneBootstrap(
                        9935,
                        "gameplay-guard-generation-mismatch");
                    break;
                case "Completed":
                    Assert.That(
                        SceneEntryPresentationRegistry.TryAdvance(
                            expectedToken,
                            SceneEntryPresentationPhase.EntryIrisClosed),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryAdvance(
                            expectedToken,
                            SceneEntryPresentationPhase.Opening),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryComplete(expectedToken),
                        Is.True);
                    break;
                case "AlreadyFailed":
                    Assert.That(
                        SceneEntryPresentationRegistry.TryFailHoldingCover(
                            expectedToken,
                            "existing gameplay failure"),
                        Is.True);
                    break;
                case "NoActiveSession":
                    SceneEntryPresentationRegistry.ResetForTests();
                    break;
                default:
                    Assert.Fail($"Unknown isolation case: {isolationCase}");
                    break;
            }

            var before = SceneEntryPresentationRegistry.Current;
            InvokePrivateStatic(
                typeof(GameplayUiFlowInstaller),
                "ReportSceneEntryIrisPreparationFailureIfOwned",
                reportToken,
                expectedGeneration,
                "UNEXPECTED_MUTATION",
                isolationCase);
            var after = SceneEntryPresentationRegistry.Current;
            AssertSceneEntrySnapshotEqual(before, after);
        }

        [TestCase("StaleToken")]
        [TestCase("NewerToken")]
        [TestCase("GenerationMismatch")]
        [TestCase("Completed")]
        [TestCase("AlreadyFailed")]
        [TestCase("NoActiveSession")]
        public void MainMenuDestinationIrisFailureGuard_PreservesNonMatchingSession(
            string isolationCase)
        {
            var authority = TerminalSessionRegistry.Authority;
            var sourceGeneration = authority.RegisterSceneBootstrap(
                9940,
                "main-menu-guard-source");
            var expectedToken = PrepareLoadingMainMenuEntry(
                sourceGeneration,
                transitionId: 9941);
            var expectedGeneration = authority.RegisterSceneBootstrap(
                9942,
                "main-menu-guard-destination");
            Assert.That(
                MainMenuEntryPresentationRegistry.TryRegisterDestinationScene(
                    expectedToken,
                    expectedGeneration),
                Is.True);

            var reportToken = expectedToken;
            switch (isolationCase)
            {
                case "StaleToken":
                    reportToken = new MainMenuEntrySessionToken(
                        expectedToken.Value + 1000);
                    break;
                case "NewerToken":
                    MainMenuEntryPresentationRegistry.ResetForTests();
                    Assert.That(
                        MainMenuEntryPresentationRegistry.TryClaim(
                            SceneTransitionIntent.ReturnToMainMenu,
                            expectedGeneration,
                            "guard-dummy",
                            out var dummyToken),
                        Is.True);
                    Assert.That(
                        MainMenuEntryPresentationRegistry.TryCancelClaim(dummyToken),
                        Is.True);
                    var newerToken = PrepareLoadingMainMenuEntry(
                        expectedGeneration,
                        transitionId: 9943);
                    var newerGeneration = authority.RegisterSceneBootstrap(
                        9944,
                        "main-menu-guard-newer");
                    Assert.That(
                        MainMenuEntryPresentationRegistry
                            .TryRegisterDestinationScene(
                                newerToken,
                                newerGeneration),
                        Is.True);
                    break;
                case "GenerationMismatch":
                    authority.RegisterSceneBootstrap(
                        9945,
                        "main-menu-guard-generation-mismatch");
                    break;
                case "Completed":
                    Assert.That(
                        MainMenuEntryPresentationRegistry.TryAdvance(
                            expectedToken,
                            SceneEntryPresentationPhase.EntryIrisClosed),
                        Is.True);
                    Assert.That(
                        MainMenuEntryPresentationRegistry.TryAdvance(
                            expectedToken,
                            SceneEntryPresentationPhase.Opening),
                        Is.True);
                    Assert.That(
                        MainMenuEntryPresentationRegistry.TryComplete(
                            expectedToken),
                        Is.True);
                    break;
                case "AlreadyFailed":
                    Assert.That(
                        MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                            expectedToken,
                            "existing main menu failure"),
                        Is.True);
                    break;
                case "NoActiveSession":
                    MainMenuEntryPresentationRegistry.ResetForTests();
                    break;
                default:
                    Assert.Fail($"Unknown isolation case: {isolationCase}");
                    break;
            }

            var before = MainMenuEntryPresentationRegistry.Current;
            InvokePrivateStatic(
                typeof(MainMenuUiFlowInstaller),
                "ReportMainMenuEntryIrisPreparationFailureIfOwned",
                reportToken,
                expectedGeneration,
                "UNEXPECTED_MUTATION",
                isolationCase);
            var after = MainMenuEntryPresentationRegistry.Current;
            AssertMainMenuEntrySnapshotEqual(before, after);
        }

        private static SceneEntrySessionToken PrepareLoadingSceneEntry(
            SceneTransitionIntent intent,
            long sourceGeneration,
            long transitionId,
            string destinationStageId = "stage-0-1")
        {
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    intent,
                    StageId.CreateOrThrow(destinationStageId),
                    sourceGeneration,
                    out var token),
                Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.TryBindTransition(
                    token,
                    transitionId),
                Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.TryAdvance(
                    token,
                    SceneEntryPresentationPhase.PersistentCoverReady),
                Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.TryAdvance(
                    token,
                    SceneEntryPresentationPhase.Loading),
                Is.True);
            return token;
        }

        private static SceneEntryPresentationSnapshot CopyWithToken(
            SceneEntryPresentationSnapshot snapshot,
            SceneEntrySessionToken token)
        {
            return new SceneEntryPresentationSnapshot(
                snapshot.IsActive,
                token,
                snapshot.Phase,
                snapshot.TransitionIntent,
                snapshot.DestinationStageId,
                snapshot.TransitionId,
                snapshot.SourceSceneGeneration,
                snapshot.DestinationSceneGeneration,
                snapshot.LaunchProvenance,
                snapshot.LaunchSlotNumber,
                snapshot.LaunchToken,
                snapshot.FailureReason);
        }

        private static MainMenuEntrySessionToken PrepareLoadingMainMenuEntry(
            long sourceGeneration,
            long transitionId)
        {
            Assert.That(
                MainMenuEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.ReturnToMainMenu,
                    sourceGeneration,
                    "destination-iris-guard-test",
                    out var token),
                Is.True);
            Assert.That(
                MainMenuEntryPresentationRegistry.TryBindTransition(
                    token,
                    transitionId),
                Is.True);
            Assert.That(
                MainMenuEntryPresentationRegistry.TryAdvance(
                    token,
                    SceneEntryPresentationPhase.PersistentCoverReady),
                Is.True);
            Assert.That(
                MainMenuEntryPresentationRegistry.TryAdvance(
                    token,
                    SceneEntryPresentationPhase.Loading),
                Is.True);
            return token;
        }

        private static GameplaySceneHost CreateHost(GameObject root)
        {
            var playerPrefabObject = new GameObject("DestinationIrisTestPlayerPrefab");
            playerPrefabObject.transform.SetParent(root.transform, false);
            var playerPrefab = playerPrefabObject.AddComponent<GameplayEntityView>();
            playerPrefabObject.AddComponent<PlayerAnimatorDriver>();
            playerPrefabObject.AddComponent<PlayerAnimationTimingAuthoring>();
            // Entry Iris readiness projects the Player renderer bounds before setup.
            var visualProfile = GameplayEntityVisualProfile.Create(EntityType.Unit, 1f);
            playerPrefab.ConfigureModelRoot(visualProfile.ModelLocalPosition, visualProfile.ModelLocalRotation);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "DestinationIrisTestPlayerVisual";
            visual.transform.SetParent(playerPrefab.ModelRoot, false);
            visual.transform.localScale = visualProfile.ModelLocalScale;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            var host = root.AddComponent<GameplaySceneHost>();
            host.Initialize(new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                InitialBoardBounds = new BoardBounds(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 1)),
                InitialEntities = new[]
                {
                    new EntityState
                    {
                        entityId = 10,
                        position = new SurfaceCell(FaceId.Floor, 0, 1),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                        unitRole = UnitRole.Player,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Up,
                        boardPresence = EntityBoardPresence.Occupying,
                        aiMode = EnemyAiMode.None,
                    },
                },
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                ObjectiveRuntimeDefinition = StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
                PlayerViewPrefab = playerPrefab,
            });
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            Assert.That(playerView.GetComponentInChildren<Renderer>(), Is.Not.Null,
                "The Entry Iris fixture needs rendered Player bounds to reach the setup operation under test.");
            return host;
        }

        private static ProviderHarness CreateProvider(params string[] stageIds)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            var provider =
                ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            var entries = new StageContentEntry[stageIds.Length];
            for (var i = 0; i < stageIds.Length; i++)
            {
                entries[i] = ScriptableObject.CreateInstance<StageContentEntry>();
                entries[i].AssignStageId(StageId.CreateOrThrow(stageIds[i]));
            }

            catalog.SetEntries(entries);
            provider.AssignCatalog(catalog);
            return new ProviderHarness(provider, catalog, entries);
        }

        private static void InvokeTick(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            try
            {
                method.Invoke(target, new object[] { 0f });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        private static void InvokePrivateNoArgs(
            object target,
            string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static Exception CaptureException(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivateStatic(
            Type owner,
            string methodName,
            params object[] arguments)
        {
            var method = owner.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{owner.Name}.{methodName}");
            try
            {
                method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        private static void AssertSceneEntrySnapshotEqual(
            SceneEntryPresentationSnapshot expected,
            SceneEntryPresentationSnapshot actual)
        {
            Assert.That(actual.IsActive, Is.EqualTo(expected.IsActive));
            Assert.That(actual.Token, Is.EqualTo(expected.Token));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(
                actual.TransitionIntent,
                Is.EqualTo(expected.TransitionIntent));
            Assert.That(
                actual.DestinationStageId,
                Is.EqualTo(expected.DestinationStageId));
            Assert.That(actual.TransitionId, Is.EqualTo(expected.TransitionId));
            Assert.That(
                actual.SourceSceneGeneration,
                Is.EqualTo(expected.SourceSceneGeneration));
            Assert.That(
                actual.DestinationSceneGeneration,
                Is.EqualTo(expected.DestinationSceneGeneration));
            Assert.That(
                actual.LaunchProvenance,
                Is.EqualTo(expected.LaunchProvenance));
            Assert.That(
                actual.LaunchSlotNumber,
                Is.EqualTo(expected.LaunchSlotNumber));
            Assert.That(actual.LaunchToken, Is.EqualTo(expected.LaunchToken));
            Assert.That(actual.FailureReason, Is.EqualTo(expected.FailureReason));
        }

        private static void AssertMainMenuEntrySnapshotEqual(
            MainMenuEntryPresentationSnapshot expected,
            MainMenuEntryPresentationSnapshot actual)
        {
            Assert.That(actual.IsActive, Is.EqualTo(expected.IsActive));
            Assert.That(actual.Token, Is.EqualTo(expected.Token));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(
                actual.DestinationSceneGeneration,
                Is.EqualTo(expected.DestinationSceneGeneration));
            Assert.That(actual.FailureReason, Is.EqualTo(expected.FailureReason));
        }

        private sealed class ProviderHarness : IDisposable
        {
            private readonly StageCatalog _catalog;
            private readonly StageContentEntry[] _entries;

            public ProviderHarness(
                ScriptableObjectStageCatalogProvider provider,
                StageCatalog catalog,
                StageContentEntry[] entries)
            {
                Provider = provider;
                _catalog = catalog;
                _entries = entries;
            }

            public ScriptableObjectStageCatalogProvider Provider { get; }

            public void Dispose()
            {
                foreach (var entry in _entries)
                {
                    UnityEngine.Object.DestroyImmediate(entry);
                }

                UnityEngine.Object.DestroyImmediate(Provider);
                UnityEngine.Object.DestroyImmediate(_catalog);
            }
        }
    }
}
