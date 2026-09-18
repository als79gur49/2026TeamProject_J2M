using System.Collections.Generic;
using System.Reflection;
using ArgumentNullException = System.ArgumentNullException;
using InvalidOperationException = System.InvalidOperationException;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiFlowIntegrationTests
    {
        [SetUp]
        public void ResetTerminalSession()
        {
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            GameplayEntryTransitionVisualSnapshotRegistry.ResetForTests();
        }

        [TearDown]
        public void ClearTerminalSession()
        {
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            GameplayEntryTransitionVisualSnapshotRegistry.ResetForTests();
        }

        [Test]
        [Category("Extended")]
        public void ManualRetry_DestinationInstallThrows_FailsMatchingSceneEntrySession()
        {
            AssertDestinationInstallFailureTerminalizesSceneEntry(
                SceneTransitionIntent.ManualRetry);
        }

        [Test]
        [Category("Extended")]
        public void GameplayEntry_DestinationInstallThrows_FailsMatchingSceneEntrySession()
        {
            AssertDestinationInstallFailureTerminalizesSceneEntry(
                SceneTransitionIntent.GameplayEntry);
        }

        [Test]
        [Category("Extended")]
        public void StageAdvance_DestinationInstallThrows_FailsMatchingSceneEntrySession()
        {
            AssertDestinationInstallFailureTerminalizesSceneEntry(
                SceneTransitionIntent.StageAdvance);
        }

        [Test]
        [Category("Extended")]
        public void DefeatReload_DestinationInstallThrows_FailsTerminalAndSceneEntryOwners()
        {
            var hostObject = new GameObject(nameof(
                DefeatReload_DestinationInstallThrows_FailsTerminalAndSceneEntryOwners));
            try
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    8101,
                    "destination-install-failure-source");
                var terminalClaim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(terminalClaim.Accepted, Is.True);
                Assert.That(
                    authority.TryBindTransition(
                        terminalClaim.Token,
                        transitionId: 8103,
                        TerminalDestinationKind.ReloadedGameplay),
                    Is.True);
                Assert.That(
                    authority.TryAdvancePhase(
                        terminalClaim.Token,
                        TerminalSessionPhase.WaitingDestinationReady),
                    Is.True);
                var sceneEntryToken = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.DeathRetry,
                    sourceGeneration);
                var host = CreateHost(hostObject);
                var destinationGeneration = authority.CurrentSceneGeneration;
                var installer = CreateInstallerWithMissingAudioDependency(hostObject);

                Assert.Throws<InvalidOperationException>(() => installer.Install(host));

                AssertFailedSceneEntry(
                    sceneEntryToken,
                    destinationGeneration);
                Assert.That(TerminalSessionRegistry.Current.IsActive, Is.True);
                Assert.That(
                    TerminalSessionRegistry.Current.Phase,
                    Is.EqualTo(TerminalSessionPhase.FailedHoldingCover));
                Assert.That(
                    TerminalSessionRegistry.Current.FailureReason,
                    Does.Contain("GameplayUiFlowInstaller requires a co-located AudioRuntimeInstaller"));
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrow_UsesDestinationEntryInstallFailureCode()
        {
            AssertDestinationInstallFailureTerminalizesSceneEntry(
                SceneTransitionIntent.ManualRetry);
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrow_DoesNotFailStaleDestinationGeneration()
        {
            var hostObject = new GameObject(nameof(
                DestinationInstallThrow_DoesNotFailStaleDestinationGeneration));
            var installException = new InvalidOperationException("stale-generation-install-failure");
            SceneEntrySessionToken token = default;
            void AdvanceGenerationAndThrow(SceneEntryPresentationSnapshot snapshot)
            {
                if (snapshot.Token != token ||
                    snapshot.Phase != SceneEntryPresentationPhase.WaitingRuntimeReady)
                {
                    return;
                }

                TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                    8110,
                    "newer-destination-generation");
                throw installException;
            }

            try
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    8101,
                    "stale-generation-source");
                token = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.ManualRetry,
                    sourceGeneration);
                var host = CreateHost(hostObject);
                var destinationGeneration = authority.CurrentSceneGeneration;
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                SceneEntryPresentationRegistry.ReadModel.Changed += AdvanceGenerationAndThrow;

                var thrown = Assert.Throws<InvalidOperationException>(
                    () => installer.Install(host));

                Assert.That(thrown, Is.SameAs(installException));
                var session = SceneEntryPresentationRegistry.Current;
                Assert.That(session.Token, Is.EqualTo(token));
                Assert.That(
                    session.DestinationSceneGeneration,
                    Is.EqualTo(destinationGeneration));
                Assert.That(
                    session.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.WaitingRuntimeReady));
                Assert.That(session.FailureReason, Is.Empty);
                Assert.That(
                    authority.CurrentSceneGeneration,
                    Is.Not.EqualTo(destinationGeneration));
            }
            finally
            {
                SceneEntryPresentationRegistry.ReadModel.Changed -= AdvanceGenerationAndThrow;
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrow_DoesNotFailNewerSceneEntryToken()
        {
            var hostObject = new GameObject(nameof(
                DestinationInstallThrow_DoesNotFailNewerSceneEntryToken));
            var installException = new InvalidOperationException("newer-session-install-failure");
            var armed = true;
            var failedToken = default(SceneEntrySessionToken);
            var newerToken = default(SceneEntrySessionToken);
            long sourceGeneration = 0;
            void ReplaceSessionAndThrow(SceneEntryPresentationSnapshot snapshot)
            {
                if (!armed ||
                    snapshot.Token != failedToken ||
                    snapshot.Phase != SceneEntryPresentationPhase.WaitingRuntimeReady)
                {
                    return;
                }

                armed = false;
                Assert.That(
                    SceneEntryPresentationRegistry.TryFailHoldingCover(
                        failedToken,
                        "superseded install attempt"),
                    Is.True);
                SceneEntryPresentationRegistry.ResetForTests();
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.ManualRetry,
                        StageId.CreateOrThrow("stage-0-2"),
                        sourceGeneration,
                        out var dummyToken),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryCancelClaim(dummyToken),
                    Is.True);
                newerToken = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.GameplayEntry,
                    sourceGeneration);
                Assert.That(
                    SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                        newerToken,
                        TerminalSessionRegistry.Authority.CurrentSceneGeneration),
                    Is.True);
                throw installException;
            }

            try
            {
                var authority = TerminalSessionRegistry.Authority;
                sourceGeneration = authority.RegisterSceneBootstrap(
                    8101,
                    "newer-session-source");
                failedToken = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.ManualRetry,
                    sourceGeneration);
                var host = CreateHost(hostObject);
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                SceneEntryPresentationRegistry.ReadModel.Changed += ReplaceSessionAndThrow;

                var thrown = Assert.Throws<InvalidOperationException>(
                    () => installer.Install(host));

                Assert.That(thrown, Is.SameAs(installException));
                var session = SceneEntryPresentationRegistry.Current;
                Assert.That(newerToken.IsValid, Is.True);
                Assert.That(newerToken, Is.Not.EqualTo(failedToken));
                Assert.That(session.Token, Is.EqualTo(newerToken));
                Assert.That(
                    session.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.WaitingRuntimeReady));
                Assert.That(session.FailureReason, Is.Empty);
            }
            finally
            {
                SceneEntryPresentationRegistry.ReadModel.Changed -= ReplaceSessionAndThrow;
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrowBeforeRegistration_DoesNotMutateSceneEntrySession()
        {
            var root = new GameObject(nameof(
                DestinationInstallThrowBeforeRegistration_DoesNotMutateSceneEntrySession));
            try
            {
                var sourceGeneration = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                    8101,
                    "pre-registration-failure-source");
                var token = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.ManualRetry,
                    sourceGeneration);
                var installer = root.AddComponent<GameplayUiFlowInstaller>();

                Assert.Throws<ArgumentNullException>(() => installer.Install(null));

                var session = SceneEntryPresentationRegistry.Current;
                Assert.That(session.Token, Is.EqualTo(token));
                Assert.That(
                    session.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Loading));
                Assert.That(session.DestinationSceneGeneration, Is.Zero);
                Assert.That(session.FailureReason, Is.Empty);
            }
            finally
            {
                DestroySupportObjects(root);
            }
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrow_PreservesAlreadyFailedSession()
        {
            AssertDestinationRegistrationSubscriberStateIsPreserved(
                "existing failure",
                snapshot => SceneEntryPresentationRegistry.TryFailHoldingCover(
                    snapshot.Token,
                    "existing failure"),
                session =>
                {
                    Assert.That(
                        session.Phase,
                        Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                    Assert.That(session.FailureReason, Is.EqualTo("existing failure"));
                });
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrow_PreservesCompletedSession()
        {
            AssertDestinationRegistrationSubscriberStateIsPreserved(
                "completed-session-install-failure",
                snapshot =>
                {
                    Assert.That(
                        SceneEntryPresentationRegistry.TryAdvance(
                            snapshot.Token,
                            SceneEntryPresentationPhase.EntryIrisClosed),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryAdvance(
                            snapshot.Token,
                            SceneEntryPresentationPhase.Opening),
                        Is.True);
                    return SceneEntryPresentationRegistry.TryComplete(snapshot.Token);
                },
                session =>
                {
                    Assert.That(session.IsActive, Is.False);
                    Assert.That(
                        session.Phase,
                        Is.EqualTo(SceneEntryPresentationPhase.Completed));
                    Assert.That(session.FailureReason, Is.Empty);
                });
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrowWithoutSceneEntry_DoesNotCreateFailureSession()
        {
            var hostObject = new GameObject(nameof(
                DestinationInstallThrowWithoutSceneEntry_DoesNotCreateFailureSession));
            try
            {
                var host = CreateHost(hostObject);
                var installer = CreateInstallerWithMissingAudioDependency(hostObject);

                Assert.Throws<InvalidOperationException>(() => installer.Install(host));

                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Inactive));
                Assert.That(
                    SceneEntryPresentationRegistry.Current.FailureReason,
                    Is.Null.Or.Empty);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DestinationInstallThrows_PreservesOriginalExceptionInstance_WhenFailureReportingThrows()
        {
            var hostObject = new GameObject(nameof(
                DestinationInstallThrows_PreservesOriginalExceptionInstance_WhenFailureReportingThrows));
            var installException = new InvalidOperationException("primary-install-failure");
            var reportException = new InvalidOperationException("scene-entry-report-failure");
            var sceneEntryToken = default(SceneEntrySessionToken);
            void ThrowAtRegistrationAndReporting(SceneEntryPresentationSnapshot snapshot)
            {
                if (snapshot.Token != sceneEntryToken)
                {
                    return;
                }

                if (snapshot.Phase == SceneEntryPresentationPhase.WaitingRuntimeReady)
                {
                    throw installException;
                }

                if (snapshot.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    throw reportException;
                }
            }

            try
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    8101,
                    "reporting-failure-source");
                var terminalClaim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(terminalClaim.Accepted, Is.True);
                Assert.That(
                    authority.TryBindTransition(
                        terminalClaim.Token,
                        transitionId: 8103,
                        TerminalDestinationKind.ReloadedGameplay),
                    Is.True);
                Assert.That(
                    authority.TryAdvancePhase(
                        terminalClaim.Token,
                        TerminalSessionPhase.WaitingDestinationReady),
                    Is.True);
                sceneEntryToken = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.DeathRetry,
                    sourceGeneration);
                var host = CreateHost(hostObject);
                var destinationGeneration = authority.CurrentSceneGeneration;
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                SceneEntryPresentationRegistry.ReadModel.Changed +=
                    ThrowAtRegistrationAndReporting;

                var thrown = Assert.Throws<InvalidOperationException>(
                    () => installer.Install(host));

                Assert.That(thrown, Is.SameAs(installException));
                Assert.That(
                    thrown.Data["SceneEntryFailureReportingFailure"],
                    Is.SameAs(reportException));
                AssertFailedSceneEntry(sceneEntryToken, destinationGeneration);
                Assert.That(
                    TerminalSessionRegistry.Current.Phase,
                    Is.EqualTo(TerminalSessionPhase.FailedHoldingCover));
                Assert.That(
                    TerminalSessionRegistry.Current.FailureReason,
                    Does.Contain("primary-install-failure"));
            }
            finally
            {
                SceneEntryPresentationRegistry.ReadModel.Changed -=
                    ThrowAtRegistrationAndReporting;
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SuccessfulInstall_DoesNotReportSceneEntryFailure()
        {
            var hostObject = new GameObject(nameof(
                SuccessfulInstall_DoesNotReportSceneEntryFailure));
            try
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    8101,
                    "successful-destination-install-source");
                var token = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.ManualRetry,
                    sourceGeneration);
                var host = CreateHost(hostObject);
                var destinationGeneration = authority.CurrentSceneGeneration;
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);

                Assert.DoesNotThrow(() => installer.Install(host));

                var session = SceneEntryPresentationRegistry.Current;
                Assert.That(session.IsActive, Is.True);
                Assert.That(session.Token, Is.EqualTo(token));
                Assert.That(
                    session.DestinationSceneGeneration,
                    Is.EqualTo(destinationGeneration));
                Assert.That(
                    session.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.WaitingRuntimeReady));
                Assert.That(session.FailureReason, Is.Empty);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_TerminalSessionClosesPopupAndRejectsBackUntilCompletion()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_TerminalPopupOwnership");
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);
                installer.HudView.ClickPause();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(1));

                var terminalToken = ClaimTerminalSession(
                    TerminalTransitionKind.Defeat,
                    TerminalDestinationKind.ReloadedGameplay,
                    sceneHandle: 901);

                Assert.That(installer.PopupController.PopupCount, Is.Zero);
                Assert.That(installer.Coordinator.HandleBackRequested(), Is.False);
                Assert.That(installer.Coordinator.RequestPausePopup(), Is.False);
                Assert.That(
                    TerminalSessionRegistry.TryAdvance(
                        terminalToken,
                        TerminalSessionPhase.Revealing),
                    Is.True);
                Assert.That(TerminalSessionRegistry.TryComplete(terminalToken), Is.True);
                Assert.That(installer.Coordinator.HandleBackRequested(), Is.True);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_TerminalCompletion_RestoresHudPauseWithoutBackInput()
        {
            var hostObject = new GameObject(nameof(
                GameplayUiFlowInstaller_TerminalCompletion_RestoresHudPauseWithoutBackInput));
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                // EditMode must preserve the production OnEnable-before-Install subscription order.
                var onEnable = typeof(GameplayUiFlowInstaller).GetMethod(
                    "OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onEnable, Is.Not.Null);
                onEnable.Invoke(installer, null);
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                var pauseButtonField = installer.HudView.GetType().GetField(
                    "_pauseButton", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pauseButtonField, Is.Not.Null);
                var pauseButton = (UnityEngine.UI.Button)pauseButtonField.GetValue(installer.HudView);
                Assert.That(pauseButton, Is.Not.Null);
                Assert.That(pauseButton.interactable, Is.True);

                var terminalToken = ClaimTerminalSession(
                    TerminalTransitionKind.Defeat,
                    TerminalDestinationKind.ReloadedGameplay,
                    sceneHandle: 902);
                Assert.That(
                    TerminalSessionRegistry.TryAdvance(terminalToken, TerminalSessionPhase.Revealing),
                    Is.True);
                Assert.That(installer.HudView.ViewModel.IsPauseButtonEnabled, Is.False);
                Assert.That(TerminalSessionRegistry.TryComplete(terminalToken), Is.True);

                // Do not open via Back first: doing so resynchronizes and masks the stale HUD state.
                Assert.That(installer.HudView.ViewModel.IsPauseButtonEnabled, Is.True);
                Assert.That(pauseButton.interactable, Is.True);
                installer.HudView.ClickPause();
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_LevelFailedTerminalCompletion_KeepsSelectionHiddenUntilInput()
        {
            var hostObject = new GameObject(nameof(
                GameplayUiFlowInstaller_LevelFailedTerminalCompletion_KeepsSelectionHiddenUntilInput));
            try
            {
                var host = CreateHost(hostObject);
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                var terminalToken = ClaimTerminalSession(
                    TerminalTransitionKind.Defeat,
                    TerminalDestinationKind.SameSceneLevelFailed,
                    sceneHandle: 903);
                var restartRequest = new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Retry,
                    "level-failed-selection-visibility");
                installer.ScreenController.SetRoot(new ScreenRequest(
                    ScreenId.LevelFailed,
                    new LevelFailedScreenPayload(restartRequest, terminalToken),
                    "level-failed-selection-visibility"));

                var view = installer.LevelFailedScreenView;
                var navigationGroup = GetNavigationGroup(view);
                Assert.That(view, Is.Not.Null);
                Assert.That(navigationGroup.SelectedIndex, Is.EqualTo(1));
                AssertSelectionFrameVisibility(
                    navigationGroup,
                    restartVisible: false,
                    mainVisible: false);

                Assert.That(
                    TerminalSessionRegistry.TryAdvance(
                        terminalToken,
                        TerminalSessionPhase.Revealing),
                    Is.True);
                Assert.That(TerminalSessionRegistry.TryComplete(terminalToken), Is.True);

                Assert.That(navigationGroup.SelectedIndex, Is.EqualTo(1));
                AssertSelectionFrameVisibility(
                    navigationGroup,
                    restartVisible: false,
                    mainVisible: false);

                var mainRequestCount = 0;
                view.MainRequested += () => mainRequestCount++;
                var navigationRouter = hostObject.GetComponent<UiNavigationInputRouter>();
                Assert.That(navigationRouter, Is.Not.Null);
                Assert.That(navigationRouter.DispatchSubmit(), Is.True);
                Assert.That(mainRequestCount, Is.Zero);
                AssertSelectionFrameVisibility(
                    navigationGroup,
                    restartVisible: false,
                    mainVisible: true);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_TerminalSessionClosesExpandedTmpDropdownAndClearsSelection()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_TerminalDropdownOwnership");
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);
                installer.HudView.ClickPause();
                installer.PausePopupView.ClickSettings();

                var dropdown = installer.SettingsScreenView.GetComponentInChildren<TMP_Dropdown>(true);
                Assert.That(dropdown, Is.Not.Null);
                var liveList = new GameObject(
                    "Dropdown List",
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                var blocker = new GameObject(
                    "Blocker",
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                typeof(TMP_Dropdown)
                    .GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(dropdown, liveList);
                typeof(TMP_Dropdown)
                    .GetField("m_Blocker", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(dropdown, blocker);
                Assert.That(dropdown.IsExpanded, Is.True);
                var eventSystem = EventSystem.current;
                if (eventSystem == null)
                {
                    eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
                }

                eventSystem.SetSelectedGameObject(dropdown.gameObject);

                ClaimTerminalSession(
                    TerminalTransitionKind.Victory,
                    TerminalDestinationKind.SameSceneStageResult,
                    sceneHandle: 902);

                Assert.That(dropdown.IsExpanded, Is.False);
                Assert.That(liveList == null || !liveList.activeSelf, Is.True);
                Assert.That(blocker == null || !blocker.activeSelf, Is.True);
                Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
                Assert.That(installer.Coordinator.HandleBackRequested(), Is.False);
                Assert.That(installer.Coordinator.HandlePopupBackdropClicked(), Is.False);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        private static TerminalSessionToken ClaimTerminalSession(
            TerminalTransitionKind terminalKind,
            TerminalDestinationKind destinationKind,
            int sceneHandle)
        {
            var authority = TerminalSessionRegistry.Authority;
            var sourceGeneration = authority.CurrentSceneGeneration > 0
                ? authority.CurrentSceneGeneration
                : authority.RegisterSceneBootstrap(
                    sceneHandle,
                    "gameplay-ui-flow-integration-test");
            var claim = authority.TryClaim(new TerminalClaimRequest(
                terminalKind,
                sourceGeneration,
                destinationKind));
            Assert.That(claim.Accepted, Is.True);
            return claim.Token;
        }

        private static UiSelectableButtonGroup GetNavigationGroup(LevelFailedScreenView view)
        {
            Assert.That(view, Is.Not.Null);
            var field = typeof(LevelFailedScreenView).GetField(
                "_navigationGroup",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var group = field.GetValue(view) as UiSelectableButtonGroup;
            Assert.That(group, Is.Not.Null);
            Assert.That(group.SlotCount, Is.EqualTo(2));
            return group;
        }

        private static void AssertSelectionFrameVisibility(
            UiSelectableButtonGroup group,
            bool restartVisible,
            bool mainVisible)
        {
            Assert.That(group.GetSlot(0)?.SelectionFrame, Is.Not.Null);
            Assert.That(group.GetSlot(1)?.SelectionFrame, Is.Not.Null);
            Assert.That(
                group.GetSlot(0).SelectionFrame.gameObject.activeSelf,
                Is.EqualTo(restartVisible));
            Assert.That(
                group.GetSlot(1).SelectionFrame.gameObject.activeSelf,
                Is.EqualTo(mainVisible));
        }

        private static void AssertDestinationInstallFailureTerminalizesSceneEntry(
            SceneTransitionIntent intent)
        {
            var hostObject = new GameObject($"{intent}-destination-install-failure");
            try
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    8101,
                    "destination-install-failure-source");
                var sceneEntryToken = PrepareLoadingSceneEntry(intent, sourceGeneration);
                var host = CreateHost(hostObject);
                var destinationGeneration = authority.CurrentSceneGeneration;
                var installer = CreateInstallerWithMissingAudioDependency(hostObject);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => installer.Install(host));

                Assert.That(
                    exception.Message,
                    Does.Contain("GameplayUiFlowInstaller requires a co-located AudioRuntimeInstaller"));
                AssertFailedSceneEntry(sceneEntryToken, destinationGeneration);
                Assert.That(TerminalSessionRegistry.IsActive, Is.False);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        private static void AssertDestinationRegistrationSubscriberStateIsPreserved(
            string installFailureMessage,
            System.Func<SceneEntryPresentationSnapshot, bool> transitionBeforeThrow,
            System.Action<SceneEntryPresentationSnapshot> assertPreserved)
        {
            var hostObject = new GameObject(installFailureMessage);
            var installException = new InvalidOperationException(installFailureMessage);
            var armed = true;
            var token = default(SceneEntrySessionToken);
            void TransitionAndThrow(SceneEntryPresentationSnapshot snapshot)
            {
                if (!armed ||
                    snapshot.Token != token ||
                    snapshot.Phase != SceneEntryPresentationPhase.WaitingRuntimeReady)
                {
                    return;
                }

                armed = false;
                Assert.That(transitionBeforeThrow(snapshot), Is.True);
                throw installException;
            }

            try
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    8101,
                    "preserved-session-source");
                token = PrepareLoadingSceneEntry(
                    SceneTransitionIntent.ManualRetry,
                    sourceGeneration);
                var host = CreateHost(hostObject);
                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                SceneEntryPresentationRegistry.ReadModel.Changed += TransitionAndThrow;

                var thrown = Assert.Throws<InvalidOperationException>(
                    () => installer.Install(host));

                Assert.That(thrown, Is.SameAs(installException));
                assertPreserved(SceneEntryPresentationRegistry.Current);
            }
            finally
            {
                SceneEntryPresentationRegistry.ReadModel.Changed -= TransitionAndThrow;
                DestroySupportObjects(hostObject);
            }
        }

        private static SceneEntrySessionToken PrepareLoadingSceneEntry(
            SceneTransitionIntent intent,
            long sourceGeneration)
        {
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    intent,
                    StageId.CreateOrThrow("stage-0-1"),
                    sourceGeneration,
                    out var token),
                Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.TryBindTransition(token, transitionId: 8103),
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

        private static GameplaySceneHost CreateHost(GameObject hostObject)
        {
            var host = hostObject.AddComponent<GameplaySceneHost>();
            host.Initialize(CreateConfiguration(new[]
            {
                CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
            }));
            return host;
        }

        private static GameplayUiFlowInstaller CreateInstallerWithMissingAudioDependency(
            GameObject hostObject)
        {
            var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
            UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
            Object.DestroyImmediate(hostObject.GetComponent<Game.Shared.Audio.AudioRuntimeInstaller>());
            return installer;
        }

        private static void AssertFailedSceneEntry(
            SceneEntrySessionToken expectedToken,
            long expectedDestinationGeneration)
        {
            var session = SceneEntryPresentationRegistry.Current;
            Assert.That(session.IsActive, Is.True);
            Assert.That(session.Token, Is.EqualTo(expectedToken));
            Assert.That(
                session.DestinationSceneGeneration,
                Is.EqualTo(expectedDestinationGeneration));
            Assert.That(
                session.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
            Assert.That(
                session.FailureReason,
                Does.Contain("DESTINATION_ENTRY_INSTALL_FAILED"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_PreservesHudShellState_ThroughScreenAndPausePopup()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_PreservesHudShellState_ThroughScreenAndPausePopup");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.HudController.RootViewModel.IsDimmed, Is.False);
                Assert.That(installer.HudController.RootViewModel.IsPauseButtonEnabled, Is.True);

                installer.HudView.ClickPause();
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_HudPause_SettingsBack_ReturnsThroughFreshPausePopup_AndResumesOnlyOnResume()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_HudPause_SettingsBack_ReturnsThroughFreshPausePopup_AndResumesOnlyOnResume");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                installer.HudView.ClickPause();
                var originalPausePopup = installer.PausePopupView;

                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudController.RootViewModel.IsDimmed, Is.True);
                Assert.That(installer.HudController.RootViewModel.IsPauseButtonEnabled, Is.False);

                originalPausePopup.ClickSettings();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(installer.HudView.IsVisible, Is.False);
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudController.RootViewModel.IsDimmed, Is.True);
                Assert.That(installer.HudController.RootViewModel.IsPauseButtonEnabled, Is.False);

                installer.SettingsScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.PausePopupView, Is.Not.Null);
                Assert.That(installer.PausePopupView, Is.Not.SameAs(originalPausePopup));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudController.RootViewModel.IsDimmed, Is.True);
                Assert.That(installer.HudController.RootViewModel.IsPauseButtonEnabled, Is.False);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);
                Assert.That(installer.HudController.RootViewModel.IsDimmed, Is.False);
                Assert.That(installer.HudController.RootViewModel.IsPauseButtonEnabled, Is.True);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_ComposesConfirmPolicyWithScreenTransitions()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_ComposesConfirmPolicyWithScreenTransitions");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                var confirmCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestConfirmPopup(
                    new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
                    confirmCompletions.Add), Is.True);
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);
                Assert.That(installer.Coordinator.CurrentBlockSnapshot.BlocksScreenInteraction, Is.True);

                Assert.That(installer.Coordinator.HandleBackRequested(), Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(confirmCompletions, Has.Count.EqualTo(1));
                Assert.That(confirmCompletions[0].CloseReason, Is.EqualTo(PopupCloseReason.Back));
                Assert.That(confirmCompletions[0].CompletionKind, Is.EqualTo(PopupCompletionKind.Cancelled));

                var transitionConfirmCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestConfirmPopup(
                    new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
                    transitionConfirmCompletions.Add), Is.True);

                Assert.That(installer.Coordinator.OpenSettingsScreen(), Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(transitionConfirmCompletions, Has.Count.EqualTo(1));
                Assert.That(transitionConfirmCompletions[0].CloseReason, Is.EqualTo(PopupCloseReason.ScreenTransition));

            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_BareStageBackedHostRejectsUncorrelatedStageClearFallback()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_RunSingleTick_TransitionsStageClearIntoCanonicalStageResultScreen");
            StageContentEntry contentEntry = null;
            StagePresentationDefinition presentationDefinition = null;

            try
            {
                StageLaunchContextStore.Clear();
                contentEntry = CreateStageContentEntry(out presentationDefinition);
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Up),
                    },
                    CreateSingleCellObjective(new SurfaceCell(FaceId.Floor, 0, 0)),
                    contentEntry));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);

                var exception = Assert.Throws<System.InvalidOperationException>(
                    () => host.InputHost.RunSingleTick());

                Assert.That(
                    exception.Message,
                    Does.Contain("canonical terminal arbiter"));
                Assert.That(host.CurrentObjectiveResult.IsCleared, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentMinimalStageCompletion, Is.Null);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
            }
            finally
            {
                StageLaunchContextStore.Clear();
                DestroySupportObjects(hostObject);
                DestroyImmediateIfExists(contentEntry);
                DestroyImmediateIfExists(presentationDefinition);
            }
        }

        private static void DestroySupportObjects(GameObject hostObject)
        {
            if (hostObject != null)
            {
                var installer = hostObject.GetComponent<GameplayUiFlowInstaller>();
                if (installer != null)
                {
                    var onDestroy = typeof(GameplayUiFlowInstaller).GetMethod(
                        "OnDestroy",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(onDestroy, Is.Not.Null);
                    onDestroy.Invoke(installer, null);
                }
            }

            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (hostObject != null)
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        private static GameplaySceneHostConfiguration CreateConfiguration(
            EntityState[] initialEntities,
            StageObjectiveRuntimeDefinition objectiveRuntimeDefinition = null,
            StageContentEntry stageContentEntry = null)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                StageContentEntry = stageContentEntry,
                ObjectiveRuntimeDefinition = objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
            };
        }

        private static StageContentEntry CreateStageContentEntry(
            out StagePresentationDefinition presentationDefinition)
        {
            var stageId = StageId.CreateOrThrow("ui-flow-clear");
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.AssignStageId(stageId);

            presentationDefinition = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentationDefinition, "displayNameKey", StageDisplayNameKeys.ForStage(stageId));

            entry.AssignPresentationDefinition(presentationDefinition);
            return entry;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void DestroyImmediateIfExists(Object value)
        {
            if (value != null)
            {
                Object.DestroyImmediate(value);
            }
        }

        private static StageObjectiveRuntimeDefinition CreateSingleCellObjective(SurfaceCell goalCell)
        {
            var zone = new StageZoneRuntimeDefinition(
                "goal",
                goalCell.face,
                new[]
                {
                    new StageZoneRuntimeRegion(goalCell.PlanarPosition, goalCell.PlanarPosition),
                });

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequireAllConditions,
                10,
                new[] { zone },
                new[]
                {
                    new StageObjectiveConditionRuntimeDefinitionEntry(
                        new PlayerAtAnyZoneConditionRuntimeDefinition(
                            "primary-goal",
                            "Primary Goal",
                            10,
                            new[] { zone },
                            requireAlive: true),
                        required: true,
                        StageObjectiveConditionRole.PrimaryGoal,
                        "primary-goal"),
                });
        }

        private static EntityState CreatePlayerEntity(SurfaceCell position, Direction facing)
        {
            return new EntityState
            {
                entityId = 10,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }
    }
}
