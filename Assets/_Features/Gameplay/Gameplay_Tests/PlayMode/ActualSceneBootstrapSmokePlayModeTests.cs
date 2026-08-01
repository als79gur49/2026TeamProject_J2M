using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System;
using System.Reflection;
using System.Security.Cryptography;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.Vfx.Host;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class ActualSceneBootstrapSmokePlayModeTests : InputTestFixture
    {
        private const int FirstTickSmokeCount = 5;
        private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";
#if UNITY_EDITOR
        private const string StageBackedGameplaySceneInstallerGuid = "41909c3f1846cad878e314473f74442c";
        private const string StageBackedGameplaySceneInstallerBasePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs";
        private const string StageBackedGameplaySceneInstallerBaseGuid = "c22c31beb01e4098b026132a77fcc93d";
#endif
        private bool _ownsIsolatedInputFixture;

        [SetUp]
        public override void Setup()
        {
            var testName = TestContext.CurrentContext.Test.Name;
            _ownsIsolatedInputFixture =
                testName.Contains("TerminalProductionStageResultInput") ||
                testName.Contains("TerminalStageEntryOpening") ||
                testName.Contains("TerminalGameClearPlayerE2E");
            if (_ownsIsolatedInputFixture)
            {
                base.Setup();
            }
        }

        [TearDown]
        public override void TearDown()
        {
            if (_ownsIsolatedInputFixture)
            {
                base.TearDown();
                _ownsIsolatedInputFixture = false;
            }
        }

        [UnityTearDown]
        public IEnumerator CleanupAfterTest()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            CampaignChanceHudDiagnostics.Clear();
            CampaignChanceHudDiagnostics.IsEnabled = false;
            CampaignChanceHudDiagnostics.LogToUnityConsole = false;
            CampaignLaunchHandoffSessionStore.ResetForTests();
            yield return CleanupSceneRuntime();
            foreach (var installer in Object.FindObjectsByType<GameplayUiFlowInstaller>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Object.Destroy(installer.gameObject);
            }

            foreach (var coordinator in Object.FindObjectsByType<SceneTransitionCoordinator>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Object.Destroy(coordinator.gameObject);
            }

            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            GameplayEntryTransitionVisualSnapshotRegistry.ResetForTests();
            MainMenuTransitionVisualPolicy.ResetForTests();
            ResultTransitionVisualSnapshotRegistry.ResetForTests();
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            yield return null;
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioSceneStage0_1_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-0-1"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioSceneStage1_1_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-1-1"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioScene_ResolvesFirstStageAndInstallsUiAudio()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-0-1"));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M3Continue_ActualMainMenuUsesBlueCanonicalGameplayEntryLifecycle()
        {
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            var saveStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            var originalSlots = saveStore.LoadAllWithReport().Slots
                .Select(slot => slot.Clone())
                .ToArray();
            var activeSlotProvider =
                CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(saveStore);
            var hadActiveSlot =
                activeSlotProvider.TryGetActiveSlotNumber(out var originalActiveSlot);
            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-0-1"),
                    CurrentLevelGroupId = "level-0",
                    RemainingChances = 3,
                    IntroPlayed = true,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });

                yield return LoadScene(MainMenuScenePath);
                yield return null;

                var mainMenu = Object.FindFirstObjectByType<MainMenuUiFlowInstaller>();
                Assert.That(mainMenu, Is.Not.Null);
                Assert.That(mainMenu.Controller, Is.Not.Null);
                var coordinator = SceneTransitionCoordinator.Instance;
                var acceptedBefore = coordinator.AcceptedTransitionCount;
                mainMenu.Controller.Continue(1);

                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
                var claimed = SceneEntryPresentationRegistry.Current;
                Assert.That(
                    claimed.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.GameplayEntry));
                Assert.That(claimed.DestinationStageId.Value, Is.EqualTo("stage-0-1"));
                Assert.That(claimed.LaunchProvenance, Is.EqualTo("main-menu-continue"));
                Assert.That(claimed.LaunchSlotNumber, Is.EqualTo(1));
                Assert.That(claimed.LaunchToken, Is.Not.EqualTo(Guid.Empty));
                Assert.That(
                    claimed.SourceSceneGeneration,
                    Is.EqualTo(mainMenu.SourceSceneGenerationForTests));
                Assert.That(mainMenu.IsGameplayEntryInteractionBlocked, Is.True);
                Assert.That(mainMenu.MainMenuScreenView.CanHandleUiNavigation, Is.False);
                Assert.That(
                    coordinator.AcceptedTransitionCount - acceptedBefore,
                    Is.EqualTo(1));

                mainMenu.Controller.Continue(1);
                Assert.That(
                    coordinator.AcceptedTransitionCount - acceptedBefore,
                    Is.EqualTo(1),
                    "Duplicate Continue must not claim or load a second transition.");

                var sourceRoot = Object.FindObjectsByType<GameplayUiCanvasRootView>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single(candidate =>
                        candidate.name == "MainMenuGameplayEntryIrisSource");
                var sourceIris = sourceRoot.TerminalIrisOverlayView;
                var persistentCover =
                    Object.FindFirstObjectByType<SceneTransitionOverlayShellView>(
                        FindObjectsInactive.Include);
                Assert.That(persistentCover, Is.Not.Null);
                var visual = GameplayEntryTransitionVisualSnapshotRegistry.Require(
                    claimed.Token,
                    SceneTransitionIntent.GameplayEntry);
                AssertColorsEquivalent(
                    sourceIris.RuntimeMaterialForTests.GetColor("_OuterColor"),
                    visual.SourceCloseColor,
                    "Main Menu source Iris must use the authored GameplayEntry blue.");
                var sawSourceOpaqueRender = false;
                var sawPersistentOpaqueRender = false;
                GameplaySceneHost destinationHost = null;
                GameplayUiFlowInstaller destinationInstaller = null;
                var sawOpening = false;
                var deadline = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (sourceIris != null &&
                        sourceIris.HasRenderedEntryClosedFrame)
                    {
                        sawSourceOpaqueRender = true;
                    }

                    if (persistentCover != null &&
                        persistentCover.HasRenderedOpaqueFrame &&
                        persistentCover.HasAcknowledgedOpaqueFrame)
                    {
                        sawPersistentOpaqueRender = true;
                        AssertColorsEquivalent(
                            persistentCover.PersistentCoverColorForTests,
                            visual.HoldColor,
                            "Persistent hold must preserve the authored GameplayEntry blue.");
                    }

                    destinationHost = Object.FindFirstObjectByType<GameplaySceneHost>();
                    destinationInstaller = destinationHost != null
                        ? destinationHost.GetComponent<GameplayUiFlowInstaller>()
                        : null;
                    var session = SceneEntryPresentationRegistry.Current;
                    if (destinationHost != null &&
                        destinationInstaller != null &&
                        session.IsActive &&
                        session.Phase == SceneEntryPresentationPhase.Opening)
                    {
                        sawOpening = true;
                        Assert.That(
                            destinationInstaller.RootView.TerminalIrisOverlayView
                                .HasRenderedEntryClosedFrame,
                            Is.True);
                        AssertColorsEquivalent(
                            destinationInstaller.RootView.TerminalIrisOverlayView
                                .RuntimeMaterialForTests.GetColor("_OuterColor"),
                            visual.DestinationOpenColor,
                            "Destination Entry Iris must preserve the authored GameplayEntry blue.");
                        var blockedTickIndex = destinationHost.TickRunner.NextTickIndex;
                        Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Null);
                        Assert.That(
                            destinationHost.TickRunner.NextTickIndex,
                            Is.EqualTo(blockedTickIndex));
                    }

                    if (!session.IsActive &&
                        session.Phase == SceneEntryPresentationPhase.Completed)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(sawSourceOpaqueRender, Is.True);
                Assert.That(sawPersistentOpaqueRender, Is.True);
                Assert.That(sawOpening, Is.True);
                Assert.That(destinationHost, Is.Not.Null);
                Assert.That(destinationInstaller, Is.Not.Null);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Completed));
                destinationHost.InputHost.SetAutoAdvanceTicks(false);
                var releasedTickIndex = destinationHost.TickRunner.NextTickIndex;
                Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(
                    destinationHost.TickRunner.NextTickIndex,
                    Is.EqualTo(releasedTickIndex + 1));
            }
            finally
            {
                saveStore.ClearAll();
                for (var i = 0; i < originalSlots.Length; i++)
                {
                    if (!originalSlots[i].IsEmpty)
                    {
                        saveStore.SaveSlot(originalSlots[i]);
                    }
                }

                if (hadActiveSlot)
                {
                    activeSlotProvider.SetActiveSlot(originalActiveSlot);
                }
                else
                {
                    activeSlotProvider.ClearActiveSlot();
                }
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalProductionBootstrap_CampaignActive_RequiresAuthorityPortAndArbiter()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            var saveStore = new SaveSlotStore(
                EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            var activeSlot = new ActiveSlotProvider(
                EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            saveStore.ClearAll();
            activeSlot.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "level-01",
                RemainingChances = 2,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            activeSlot.SetActiveSlot(1);
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateCampaignTempSlot(
                    stageId,
                    remainingChances: 2));

            yield return LoadScene(UIAudioScenePath);

            var installer = Object.FindObjectsByType<StageBackedGameplaySceneInstaller>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single();
            var uiInstaller = installer.GetComponent<GameplayUiFlowInstaller>();
            Assert.That(installer.CampaignRuntimeActive, Is.True);
            Assert.That(installer.TerminalOutcomesEnabled, Is.True);
            Assert.That(installer.HasCampaignFlowController, Is.True);
            Assert.That(uiInstaller, Is.Not.Null);
            Assert.That(
                uiInstaller.TryGetTerminalSessionAuthority(
                    out var readModel,
                    out var authority),
                Is.True);
            Assert.That(readModel, Is.SameAs(authority));
            Assert.That(authority, Is.SameAs(TerminalSessionRegistry.Authority));
            Assert.That(uiInstaller.TryGetTerminalTransitionPort(out var port), Is.True);
            Assert.That(port, Is.Not.Null);
            Assert.That(
                TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                Is.GreaterThan(0));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalProductionSceneHandoff_LoadSceneAsyncSingle_BlocksNewHostUntilReveal()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
            yield return LoadScene(UIAudioScenePath);

            var sourceHost = Object.FindObjectsByType<GameplaySceneHost>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single();
            var sourceInstaller = Object.FindObjectsByType<StageBackedGameplaySceneInstaller>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single();
            Assert.That(sourceInstaller.CampaignRuntimeActive, Is.False);
            Assert.That(sourceInstaller.TerminalOutcomesEnabled, Is.False);
            sourceHost.InputHost.SetAutoAdvanceTicks(false);
            var sourceHostId = sourceHost.GetInstanceID();
            var authority = TerminalSessionRegistry.Authority;
            var sourceGeneration = authority.CurrentSceneGeneration;
            Assert.That(sourceGeneration, Is.GreaterThan(0));
            ResultTransitionVisualSnapshotRegistry.ResetForTests();

            var claim = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Defeat,
                sourceGeneration,
                TerminalDestinationKind.ReloadedGameplay));
            Assert.That(claim.Accepted, Is.True);

            var uiInstaller = Object.FindObjectsByType<GameplayUiFlowInstaller>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single();
            Assert.That(
                uiInstaller.TryGetTerminalTransitionPort(out var transitionPort),
                Is.True);
            Assert.That(
                transitionPort.TryBegin(
                    new TerminalTransitionRequest(
                        TerminalTransitionKind.Defeat,
                        sourceHost.InputHost.PlayerEntityId,
                        claim.Token,
                        TerminalTransitionDestinationMode.SceneHandoff),
                    out var irisPlayback),
                Is.True);
            var gameplayTerminalPort = transitionPort as GameplayTerminalTransitionPort;
            Assert.That(gameplayTerminalPort, Is.Not.Null);
            gameplayTerminalPort.Tick(irisPlayback.Preset.BlackAt);
            Assert.That(irisPlayback.State, Is.EqualTo(TerminalTransitionState.Black));
            Assert.That(
                uiInstaller.RootView.TerminalIrisOverlayView
                    .RuntimeMaterialForTests.GetColor("_OuterColor"),
                Is.EqualTo(Color.black));
            Assert.That(
                ResultTransitionVisualSnapshotRegistry.TryGet(claim.Token, out _),
                Is.False,
                "DeathRetry must consume the authored black Retry policy, not Victory ResultDimVisualSnapshot.");
            if (HasGraphicsDevice())
            {
                yield return null;
                yield return AssertProductionScreenBlackCoverage("source-iris-full-black");
            }

            var hint = StageTransitionHint.ForChanceLost(
                    new StageTransitionChanceLostPayload(
                        previousRemainingChances: 2,
                        currentRemainingChances: 1,
                        totalChances: 3,
                        currentStageId: stageId,
                        retryStageId: stageId,
                        deathCount: 1,
                        source: "campaign-death-retry",
                        title: "Chance Lost",
                        message: "Retrying."))
                .WithTerminalClaim(claim.Token);
            var navigationRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "campaign-death-retry",
                hint,
                SceneTransitionIntent.DeathRetry);
            StageLaunchContextStore.Clear();
            var coordinator = SceneTransitionCoordinator.Instance;
            Assert.That(
                coordinator.TryStartStageTransition(
                    navigationRequest,
                    "UIAudioScene"),
                Is.True);
            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.Current.TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.DeathRetry));

            GameplaySceneHost destinationHost = null;
            var destinationDeadline = Time.realtimeSinceStartup + 12f;
            while (destinationHost == null &&
                   Time.realtimeSinceStartup < destinationDeadline)
            {
                var hosts = Object.FindObjectsByType<GameplaySceneHost>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (var i = 0; i < hosts.Length; i++)
                {
                    if (hosts[i] != null && hosts[i].GetInstanceID() != sourceHostId)
                    {
                        destinationHost = hosts[i];
                        break;
                    }
                }

                yield return null;
            }

            Assert.That(destinationHost, Is.Not.Null, "Production destination host was not installed.");
            Assert.That(authority.IsActive, Is.True, "Reveal completed before destination ownership could be inspected.");
            Assert.That(authority.ActiveToken, Is.EqualTo(claim.Token));
            Assert.That(
                authority.Current.DestinationSceneGeneration,
                Is.GreaterThan(sourceGeneration));

            destinationHost.InputHost.SetAutoAdvanceTicks(false);
            var tickCountBeforeReveal = 0;
            destinationHost.InputHost.TickCompleted += _ => tickCountBeforeReveal++;
            var nextTickBeforeReveal = destinationHost.TickRunner.NextTickIndex;
            destinationHost.InputHost.SetRawMoveInput(Vector2.right);
            destinationHost.InputHost.BufferPush();
            destinationHost.InputHost.BufferFlip();

            Assert.That(
                destinationHost.InputHost.AdvanceTime(
                    destinationHost.TimingProfile.SimulationTickIntervalSeconds * 10f),
                Is.Zero);
            Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Null);
            Assert.That(destinationHost.InputHost.PreviewPushDirection(), Is.EqualTo(Direction.None));
            var uiAccess = (object)destinationHost.UiAccess;
            var commandGateway = uiAccess
                .GetType()
                .GetProperty("CommandGateway")
                .GetValue(uiAccess);
            var setHeldMove = commandGateway.GetType().GetMethod("SetHeldMoveDirection");
            Assert.That(setHeldMove, Is.Not.Null);
            var directionType = setHeldMove.GetParameters().Single().ParameterType;
            var publicAdmission = setHeldMove.Invoke(
                commandGateway,
                new[] { Enum.ToObject(directionType, 2) });
            Assert.That(
                (bool)publicAdmission.GetType().GetProperty("Accepted").GetValue(publicAdmission),
                Is.False);
            Assert.That(
                Convert.ToInt32(
                    publicAdmission.GetType().GetProperty("RejectionReason").GetValue(publicAdmission)),
                Is.EqualTo(7),
                "Public admission must return RejectedTerminalSession.");
            Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(nextTickBeforeReveal));
            Assert.That(tickCountBeforeReveal, Is.Zero);
            if (HasGraphicsDevice())
            {
                yield return AssertProductionScreenBlackCoverage("destination-bootstrap-before-reveal");
                Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(nextTickBeforeReveal));
                Assert.That(tickCountBeforeReveal, Is.Zero);
            }

            var revealDeadline = Time.realtimeSinceStartup + 5f;
            while (authority.IsActive &&
                   Time.realtimeSinceStartup < revealDeadline)
            {
                Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(nextTickBeforeReveal));
                Assert.That(tickCountBeforeReveal, Is.Zero);
                yield return null;
            }

            Assert.That(authority.IsActive, Is.False, "Persistent reveal did not complete.");
            Assert.That(authority.Phase, Is.EqualTo(TerminalSessionPhase.Completed));
            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
            Assert.That(
                SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.Completed));
            Assert.That(
                SceneEntryPresentationRegistry.Current.TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.DeathRetry));
            Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(tickCountBeforeReveal, Is.EqualTo(1));
            Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(nextTickBeforeReveal + 1));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M2PauseRetry_ActualSceneReloadUsesRenderedEntryIrisAndReleasesInputOnce()
        {
            var previousTimeScale = Time.timeScale;
            var stageId = StageId.CreateOrThrow("stage-1-1");
            try
            {
                StageLaunchContextStore.SetCurrent(stageId);
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateNonCampaign(stageId));
                yield return LoadScene(UIAudioScenePath);

                var sourceHost = Object.FindFirstObjectByType<GameplaySceneHost>();
                var sourceHostId = sourceHost.GetInstanceID();
                var sourceInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                var flow = sourceInstaller.Coordinator;
                StageLaunchContextStore.Clear();
                Assert.That(flow.RequestPausePopup(), Is.True);
                Time.timeScale = 0f;
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(sourceInstaller.PausePopupView, Is.Not.Null);

                sourceInstaller.PausePopupView.ClickRetry();
                Assert.That(sourceInstaller.PausePopupView.IsVisible, Is.True);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
                var claimed = SceneEntryPresentationRegistry.Current;
                Assert.That(
                    claimed.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.ManualRetry));
                Assert.That(claimed.DestinationStageId, Is.EqualTo(stageId));

                var duplicate = new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Retry,
                    "pause-retry-duplicate",
                    StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                    SceneTransitionIntent.ManualRetry);
                Assert.That(flow.TryLaunchStage(duplicate), Is.False);

                var persistentCover =
                    Object.FindFirstObjectByType<SceneTransitionOverlayShellView>(
                        FindObjectsInactive.Include);
                GameplaySceneHost destinationHost = null;
                GameplayUiFlowInstaller destinationInstaller = null;
                var sawOpening = false;
                var openingTickIndex = -1;
                var deadline = Time.realtimeSinceStartup + 12f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    destinationHost = Object.FindObjectsByType<GameplaySceneHost>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .FirstOrDefault(candidate =>
                            candidate.GetInstanceID() != sourceHostId);
                    destinationInstaller = destinationHost != null
                        ? destinationHost.GetComponent<GameplayUiFlowInstaller>()
                        : null;
                    var session = SceneEntryPresentationRegistry.Current;
                    if (destinationHost != null &&
                        session.IsActive &&
                        session.Phase == SceneEntryPresentationPhase.Opening)
                    {
                        sawOpening = true;
                        openingTickIndex = destinationHost.TickRunner.NextTickIndex;
                        Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Null);
                        Assert.That(
                            destinationHost.TickRunner.NextTickIndex,
                            Is.EqualTo(openingTickIndex));
                        var entryIris =
                            destinationInstaller.RootView.TerminalIrisOverlayView;
                        Assert.That(entryIris.HasRenderedEntryClosedFrame, Is.True);
                        Assert.That(
                            entryIris.RuntimeMaterialForTests.GetColor("_OuterColor"),
                            Is.EqualTo(Color.black));
                    }

                    if (!session.IsActive &&
                        session.Phase == SceneEntryPresentationPhase.Completed)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(destinationHost, Is.Not.Null);
                Assert.That(destinationInstaller, Is.Not.Null);
                Assert.That(sawOpening, Is.True);
                Assert.That(persistentCover, Is.Not.Null);
                Assert.That(persistentCover.HasAcknowledgedOpaqueFrame, Is.True);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.ManualRetry));
                Assert.That(Time.timeScale, Is.EqualTo(1f));

                destinationHost.InputHost.SetAutoAdvanceTicks(false);
                var releasedTickIndex = destinationHost.TickRunner.NextTickIndex;
                Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(
                    destinationHost.TickRunner.NextTickIndex,
                    Is.EqualTo(releasedTickIndex + 1));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M4PauseMainMenu_ActualSceneUsesRenderedNeutralLifecycleAndUnscaledRelease()
        {
            var previousTimeScale = Time.timeScale;
            var stageId = StageId.CreateOrThrow("stage-1-1");
            try
            {
                StageLaunchContextStore.SetCurrent(stageId);
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateNonCampaign(stageId));
                yield return LoadScene(UIAudioScenePath);

                var sourceInstaller =
                    Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                StageLaunchContextStore.Clear();
                Assert.That(sourceInstaller.Coordinator.RequestPausePopup(), Is.True);
                Time.timeScale = 0f;
                sourceInstaller.PausePopupView.ClickMainMenu();

                Assert.That(sourceInstaller.PausePopupView.IsVisible, Is.True);
                Assert.That(MainMenuEntryPresentationRegistry.IsActive, Is.True);
                var claimed = MainMenuEntryPresentationRegistry.Current;
                Assert.That(
                    claimed.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.ReturnToMainMenu));
                Assert.That(
                    sourceInstaller.Coordinator.RequestPausePopup(),
                    Is.False,
                    "The active Main Menu session must reject duplicate source interaction.");

                var persistentCover =
                    Object.FindFirstObjectByType<SceneTransitionOverlayShellView>(
                        FindObjectsInactive.Include);
                var sawPersistentOpaqueRender = false;
                var sawDestinationClosedRender = false;
                var sawOpeningBlocked = false;
                MainMenuUiFlowInstaller destinationInstaller = null;
                var deadline = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (persistentCover != null &&
                        persistentCover.HasRenderedOpaqueFrame &&
                        persistentCover.HasAcknowledgedOpaqueFrame)
                    {
                        sawPersistentOpaqueRender = true;
                        AssertColorsEquivalent(
                            persistentCover.PersistentCoverColorForTests,
                            Color.black,
                            "ReturnToMainMenu persistent hold must remain neutral black.");
                    }

                    destinationInstaller =
                        Object.FindFirstObjectByType<MainMenuUiFlowInstaller>();
                    var session = MainMenuEntryPresentationRegistry.Current;
                    if (destinationInstaller != null &&
                        session.IsActive &&
                        session.Phase == SceneEntryPresentationPhase.Opening)
                    {
                        sawOpeningBlocked = true;
                        Assert.That(
                            destinationInstaller
                                .IsGameplayEntryInteractionBlocked,
                            Is.True);
                        var destinationIris =
                            Object.FindObjectsByType<GameplayUiCanvasRootView>(
                                    FindObjectsInactive.Include,
                                    FindObjectsSortMode.None)
                                .Single(candidate =>
                                    candidate.name ==
                                    "MainMenuDestinationIris")
                                .TerminalIrisOverlayView;
                        sawDestinationClosedRender =
                            destinationIris.HasRenderedEntryClosedFrame;
                        AssertColorsEquivalent(
                            destinationIris.RuntimeMaterialForTests
                                .GetColor("_OuterColor"),
                            Color.black,
                            "Main Menu destination Iris must preserve neutral black.");
                    }

                    if (!session.IsActive &&
                        session.Phase ==
                        SceneEntryPresentationPhase.Completed)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(sawPersistentOpaqueRender, Is.True);
                Assert.That(sawDestinationClosedRender, Is.True);
                Assert.That(sawOpeningBlocked, Is.True);
                Assert.That(destinationInstaller, Is.Not.Null);
                Assert.That(MainMenuEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(
                    MainMenuEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Completed));
                Assert.That(
                    destinationInstaller.IsGameplayEntryInteractionBlocked,
                    Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M4LevelFailedAndGameClearDirect_ActualScenesReuseMainMenuDestinationLifecycle()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            var saveStore =
                CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            var activeSlotProvider =
                CampaignSaveCompositionProvider
                    .CreateProductionActiveSlotProvider(saveStore);
            var hadActiveSlot =
                activeSlotProvider.TryGetActiveSlotNumber(
                    out var originalActiveSlot);
            activeSlotProvider.ClearActiveSlot();
            var sources = new[]
            {
                ScreenId.LevelFailed,
                ScreenId.GameClear,
            };
            try
            {
                foreach (var source in sources)
                {
                    StageLaunchContextStore.SetCurrent(stageId);
                    EditorDirectPlayContextStore.SetCurrent(
                        EditorDirectPlayContext.CreateNonCampaign(stageId));
                    yield return LoadScene(UIAudioScenePath);

                    var sourceInstaller =
                        Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                    StageLaunchContextStore.Clear();
                    if (source == ScreenId.LevelFailed)
                    {
                        sourceInstaller.ScreenController.SetRoot(
                            new ScreenRequest(
                                ScreenId.LevelFailed,
                                new LevelFailedScreenPayload(
                                    "Level Failed",
                                    "M4 direct menu route",
                                    "Restart",
                                    "Main",
                                    new StageNavigationRequest(
                                        stageId,
                                        StageNavigationKind.Retry,
                                        "m4-level-failed-retry",
                                        transitionIntent:
                                        SceneTransitionIntent.ManualRetry)),
                                "m4-level-failed-main"));
                        sourceInstaller.LevelFailedScreenView.ClickMain();
                    }
                    else
                    {
                        sourceInstaller.ScreenController.SetRoot(
                            new ScreenRequest(
                                ScreenId.GameClear,
                                GameClearScreenPayload.Default,
                                "m4-game-clear-main"));
                        sourceInstaller.GameClearScreenView.ClickMain();
                    }

                    Assert.That(
                        MainMenuEntryPresentationRegistry.IsActive,
                        Is.True);
                    Assert.That(
                        MainMenuEntryPresentationRegistry.Current
                            .TransitionIntent,
                        Is.EqualTo(SceneTransitionIntent.ReturnToMainMenu),
                        source.ToString());
                    var deadline = Time.realtimeSinceStartup + 15f;
                    while (MainMenuEntryPresentationRegistry.IsActive &&
                           Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                    }

                    Assert.That(
                        MainMenuEntryPresentationRegistry.IsActive,
                        Is.False,
                        $"{source} did not complete its Main Menu destination lifecycle.");
                    Assert.That(
                        MainMenuEntryPresentationRegistry.Current.Phase,
                        Is.EqualTo(SceneEntryPresentationPhase.Completed));
                    var destination =
                        Object.FindFirstObjectByType<MainMenuUiFlowInstaller>();
                    Assert.That(destination, Is.Not.Null);
                    Assert.That(
                        destination.IsGameplayEntryInteractionBlocked,
                        Is.False);
                }
            }
            finally
            {
                if (hadActiveSlot)
                {
                    activeSlotProvider.SetActiveSlot(originalActiveSlot);
                }
                else
                {
                    activeSlotProvider.ClearActiveSlot();
                }
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M4IntroSkip_ActualMainMenuTransfersRenderedOpaqueOwnerToGameplay()
        {
            return RunIntroCinematicActualScene(
                CinematicPlaybackCompletionKind.Skipped);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M5IntroNormal_ActualMainMenuTransfersRenderedOpaqueOwnerToGameplay()
        {
            return RunIntroCinematicActualScene(
                CinematicPlaybackCompletionKind.Completed);
        }

        private IEnumerator RunIntroCinematicActualScene(
            CinematicPlaybackCompletionKind completionKind)
        {
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            var saveStore =
                CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            var originalSlots = saveStore.LoadAllWithReport().Slots
                .Select(slot => slot.Clone())
                .ToArray();
            var activeSlotProvider =
                CampaignSaveCompositionProvider
                    .CreateProductionActiveSlotProvider(saveStore);
            var hadActiveSlot =
                activeSlotProvider.TryGetActiveSlotNumber(
                    out var originalActiveSlot);
            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                yield return LoadScene(MainMenuScenePath);
                yield return null;

                var mainMenu =
                    Object.FindFirstObjectByType<MainMenuUiFlowInstaller>();
                mainMenu.Controller.Continue(1);
                var overlay =
                    Object.FindFirstObjectByType<CinematicVideoOverlayView>(
                        FindObjectsInactive.Include);
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.IsPlaying, Is.True);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.CinematicToGameplay));

                overlay.AdvanceFadeForTesting(10f);
                overlay.NotifyPreparedFirstFrame();
                overlay.AdvanceFadeForTesting(10f);
                Assert.That(
                    overlay.CurrentPresentationState,
                    Is.EqualTo(CinematicPresentationState.Playing));
                if (completionKind == CinematicPlaybackCompletionKind.Skipped)
                {
                    overlay.RequestSkip();
                }
                else
                {
                    overlay.CompleteForTesting(completionKind);
                }
                overlay.AdvanceFadeForTesting(10f);
                Assert.That(
                    overlay.AcknowledgeOpaqueRenderForTesting(),
                    Is.True);
                Assert.That(
                    CinematicOpaqueHandoffRegistry.Current.Phase,
                    Is.EqualTo(
                        CinematicOpaqueHandoffPhase
                            .CinematicOpaqueRendered));

                var sawPersistentRender = false;
                var sawGameplayOpening = false;
                GameplaySceneHost destinationHost = null;
                var deadline = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    var persistentCover =
                        Object.FindFirstObjectByType<
                            SceneTransitionOverlayShellView>(
                            FindObjectsInactive.Include);
                    if (persistentCover != null &&
                        persistentCover.HasRenderedOpaqueFrame &&
                        persistentCover.HasAcknowledgedOpaqueFrame)
                    {
                        sawPersistentRender = true;
                    }

                    destinationHost =
                        Object.FindFirstObjectByType<GameplaySceneHost>();
                    var session = SceneEntryPresentationRegistry.Current;
                    if (destinationHost != null &&
                        session.IsActive &&
                        session.Phase ==
                        SceneEntryPresentationPhase.Opening)
                    {
                        sawGameplayOpening = true;
                        Assert.That(
                            destinationHost.InputHost.RunSingleTick(),
                            Is.Null);
                    }

                    if (!session.IsActive &&
                        session.Phase ==
                        SceneEntryPresentationPhase.Completed)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(sawPersistentRender, Is.True);
                Assert.That(sawGameplayOpening, Is.True);
                Assert.That(destinationHost, Is.Not.Null);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(CinematicOpaqueHandoffRegistry.IsActive, Is.False);
                Assert.That(overlay == null || !overlay.gameObject.activeSelf, Is.True);
            }
            finally
            {
                saveStore.ClearAll();
                foreach (var slot in originalSlots)
                {
                    if (!slot.IsEmpty)
                    {
                        saveStore.SaveSlot(slot);
                    }
                }

                if (hadActiveSlot)
                {
                    activeSlotProvider.SetActiveSlot(originalActiveSlot);
                }
                else
                {
                    activeSlotProvider.ClearActiveSlot();
                }
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M4OutroSkip_ActualGameClearTransfersRenderedOpaqueOwnerToMainMenu()
        {
            return RunOutroCinematicActualScene(
                CinematicPlaybackCompletionKind.Skipped);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M5OutroNormal_ActualGameClearTransfersRenderedOpaqueOwnerToMainMenu()
        {
            return RunOutroCinematicActualScene(
                CinematicPlaybackCompletionKind.Completed);
        }

        private IEnumerator RunOutroCinematicActualScene(
            CinematicPlaybackCompletionKind completionKind)
        {
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            var saveStore =
                CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            var originalSlots = saveStore.LoadAllWithReport().Slots
                .Select(slot => slot.Clone())
                .ToArray();
            var activeSlotProvider =
                CampaignSaveCompositionProvider
                    .CreateProductionActiveSlotProvider(saveStore);
            var hadActiveSlot =
                activeSlotProvider.TryGetActiveSlotNumber(
                    out var originalActiveSlot);
            var stageId = StageId.CreateOrThrow("stage-1-1");
            try
            {
                saveStore.ClearAll();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = stageId,
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = 3,
                    IntroPlayed = true,
                    OutroPlayed = false,
                    CampaignCompleted = true,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                activeSlotProvider.SetActiveSlot(1);
                StageLaunchContextStore.SetCurrent(stageId);
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateNonCampaign(stageId));
                yield return LoadScene(UIAudioScenePath);

                var source =
                    Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                StageLaunchContextStore.Clear();
                source.ScreenController.SetRoot(new ScreenRequest(
                    ScreenId.GameClear,
                    GameClearScreenPayload.Default,
                    "m4-outro-skip"));
                source.GameClearScreenView.ClickMain();

                var overlay =
                    Object.FindFirstObjectByType<CinematicVideoOverlayView>(
                        FindObjectsInactive.Include);
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.IsPlaying, Is.True);
                Assert.That(MainMenuEntryPresentationRegistry.IsActive, Is.True);
                Assert.That(
                    MainMenuEntryPresentationRegistry.Current.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.CinematicToMainMenu));

                overlay.AdvanceFadeForTesting(10f);
                overlay.NotifyPreparedFirstFrame();
                overlay.AdvanceFadeForTesting(10f);
                if (completionKind == CinematicPlaybackCompletionKind.Skipped)
                {
                    overlay.RequestSkip();
                }
                else
                {
                    overlay.CompleteForTesting(completionKind);
                }
                overlay.AdvanceFadeForTesting(10f);
                Assert.That(
                    overlay.AcknowledgeOpaqueRenderForTesting(),
                    Is.True);

                var sawPersistentRender = false;
                var sawMenuOpening = false;
                MainMenuUiFlowInstaller destination = null;
                var deadline = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    var persistentCover =
                        Object.FindFirstObjectByType<
                            SceneTransitionOverlayShellView>(
                            FindObjectsInactive.Include);
                    if (persistentCover != null &&
                        persistentCover.HasRenderedOpaqueFrame &&
                        persistentCover.HasAcknowledgedOpaqueFrame)
                    {
                        sawPersistentRender = true;
                    }

                    destination =
                        Object.FindFirstObjectByType<MainMenuUiFlowInstaller>();
                    var session =
                        MainMenuEntryPresentationRegistry.Current;
                    if (destination != null &&
                        session.IsActive &&
                        session.Phase ==
                        SceneEntryPresentationPhase.Opening)
                    {
                        sawMenuOpening = true;
                        Assert.That(
                            destination.IsGameplayEntryInteractionBlocked,
                            Is.True);
                    }

                    if (!session.IsActive &&
                        session.Phase ==
                        SceneEntryPresentationPhase.Completed)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(sawPersistentRender, Is.True);
                Assert.That(sawMenuOpening, Is.True);
                Assert.That(destination, Is.Not.Null);
                Assert.That(MainMenuEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(CinematicOpaqueHandoffRegistry.IsActive, Is.False);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.True);
            }
            finally
            {
                saveStore.ClearAll();
                foreach (var slot in originalSlots)
                {
                    if (!slot.IsEmpty)
                    {
                        saveStore.SaveSlot(slot);
                    }
                }

                if (hadActiveSlot)
                {
                    activeSlotProvider.SetActiveSlot(originalActiveSlot);
                }
                else
                {
                    activeSlotProvider.ClearActiveSlot();
                }
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M2LevelFailedRestart_PreservesSourceScreenUntilOpaqueAndCompletesCanonicalEntry()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateNonCampaign(stageId));
            yield return LoadScene(UIAudioScenePath);

            var sourceHost = Object.FindFirstObjectByType<GameplaySceneHost>();
            var sourceHostId = sourceHost.GetInstanceID();
            var sourceInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
            StageLaunchContextStore.Clear();
            var restartRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "level-failed-restart-level",
                StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart),
                SceneTransitionIntent.ManualRetry);
            sourceInstaller.ScreenController.SetRoot(new ScreenRequest(
                ScreenId.LevelFailed,
                new LevelFailedScreenPayload(
                    "Level Failed",
                    "Retry lifecycle",
                    "Restart Level",
                    "Main",
                    restartRequest),
                "m2-level-failed"));
            var sourceScreen = sourceInstaller.LevelFailedScreenView;
            Assert.That(sourceScreen, Is.Not.Null);
            Assert.That(sourceScreen.IsVisible, Is.True);

            sourceScreen.ClickRestartLevel();
            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.Current.TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ManualRetry));
            Assert.That(sourceScreen.IsVisible, Is.True);
            Assert.That(
                sourceInstaller.Coordinator.TryLaunchStage(restartRequest),
                Is.False,
                "A repeated LevelFailed Restart must not claim a second session or load.");

            var sourceOpaqueDeadline = Time.realtimeSinceStartup + 4f;
            while (SceneEntryPresentationRegistry.Current.Phase !=
                   SceneEntryPresentationPhase.Loading &&
                   Time.realtimeSinceStartup < sourceOpaqueDeadline)
            {
                yield return null;
            }

            Assert.That(
                SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.Loading));
            Assert.That(sourceScreen != null && sourceScreen.IsVisible, Is.True);

            GameplaySceneHost destinationHost = null;
            var deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                destinationHost = Object.FindObjectsByType<GameplaySceneHost>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        candidate.GetInstanceID() != sourceHostId);
                if (destinationHost != null &&
                    !SceneEntryPresentationRegistry.IsActive &&
                    SceneEntryPresentationRegistry.Current.Phase ==
                    SceneEntryPresentationPhase.Completed)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(destinationHost, Is.Not.Null);
            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
            Assert.That(
                SceneEntryPresentationRegistry.Current.TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ManualRetry));
            destinationHost.InputHost.SetAutoAdvanceTicks(false);
            Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Not.Null);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M2DemoStageRelaunch_KeepsIntentAndUsesSharedRetryEntryExecutor()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateNonCampaign(stageId));
            yield return LoadScene(UIAudioScenePath);

            var sourceHost = Object.FindFirstObjectByType<GameplaySceneHost>();
            var sourceHostId = sourceHost.GetInstanceID();
            var sourceInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
            var coordinator = SceneTransitionCoordinator.Instance;
            StageLaunchContextStore.Clear();
            var request = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "demo-stage-control-start-stage",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.DemoStageRelaunch);

            Assert.That(sourceInstaller.Coordinator.TryLaunchStage(request), Is.True);
            Assert.That(
                coordinator.LastResolvedRoutePolicy?.Intent,
                Is.EqualTo(SceneTransitionIntent.DemoStageRelaunch));
            Assert.That(
                SceneEntryPresentationRegistry.Current.TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.DemoStageRelaunch));
            Assert.That(sourceInstaller.Coordinator.TryLaunchStage(request), Is.False);

            GameplaySceneHost destinationHost = null;
            var deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                destinationHost = Object.FindObjectsByType<GameplaySceneHost>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        candidate.GetInstanceID() != sourceHostId);
                if (destinationHost != null &&
                    !SceneEntryPresentationRegistry.IsActive &&
                    SceneEntryPresentationRegistry.Current.Phase ==
                    SceneEntryPresentationPhase.Completed)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(destinationHost, Is.Not.Null);
            Assert.That(
                SceneEntryPresentationRegistry.Current.TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.DemoStageRelaunch));
            destinationHost.InputHost.SetAutoAdvanceTicks(false);
            Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Not.Null);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalProductionOffcenterFocus_ActualVictoryUsesOutputCameraAndPixelAperture()
        {
            if (!HasGraphicsDevice())
            {
                Assert.Ignore(
                    "Off-center aperture pixel evidence runs in the dedicated UNITY_GRAPHICS=1 terminal-production-offcenter-focus lane.");
            }
            var positions = new[]
            {
                new KeyValuePair<string, Vector2>("Left", new Vector2(0.2f, 0.5f)),
                new KeyValuePair<string, Vector2>("Right", new Vector2(0.8f, 0.5f)),
                new KeyValuePair<string, Vector2>("Top", new Vector2(0.5f, 0.8f)),
                new KeyValuePair<string, Vector2>("Bottom", new Vector2(0.5f, 0.2f)),
            };
            var outputRoot = ReadCommandLineValue("-terminalIrisQualityOutput");
            Assert.That(outputRoot, Is.Not.Null.And.Not.Empty);
            var outputDirectory = Path.Combine(outputRoot, "production-offcenter");
            Directory.CreateDirectory(outputDirectory);
            var evidenceBundleId =
                ReadCommandLineValue("-terminalIrisEvidenceBundleId");
            var evidenceRunId =
                ReadCommandLineValue("-terminalIrisEvidenceRunId");
            Assert.That(evidenceBundleId, Is.Not.Null.And.Not.Empty);
            Assert.That(evidenceRunId, Is.Not.Null.And.Not.Empty);
            var failures = new List<string>();
            var sampleIds = new HashSet<string>(StringComparer.Ordinal);
            var artifactPaths = new HashSet<string>(StringComparer.Ordinal);
            var maximumCenterErrorPixels = 0f;
            var maximumRmsRadialErrorPixels = 0f;
            var maximumRadialErrorPixels = 0f;
            var maximumP99RadialErrorPixels = 0f;
            var fallbackCount = 0;
            var csvRows = new List<string>
            {
                "evidence_bundle_id,evidence_run_id,sample_id,trace_row_id,direction,run," +
                "terminal_token,transition_id,scene_generation," +
                "destination_generation,player_entity_id,player_view_instance_id," +
                "player_world_x,player_world_y,player_world_z,player_view_x," +
                "player_view_y,player_view_z,expected_x,expected_y," +
                "camera_instance_id,camera_name,camera_type,camera_pixel_rect," +
                "target_texture_instance_id,target_width,target_height," +
                "world_to_camera_hash,projection_hash,raw_viewport_x,raw_viewport_y," +
                "raw_depth,projected_x,projected_y,projection_success,was_clamped," +
                "clamped_x,clamped_y,fallback_reason,request_x,request_y," +
                "request_focus_valid,focus_source,session_x,session_y," +
                "session_focus_valid,overlay_instance_id,overlay_x,overlay_y," +
                "runtime_material_id,image_material_id,material_for_rendering_id," +
                "material_x,material_y,material_application_frame,render_frame," +
                "render_sequence_id,artifact_path,capture_sha256," +
                "measured_x,measured_y,center_error_viewport,center_error_pixels," +
                "circle_radius_pixels,rms_radial_error_pixels,max_radial_error_pixels," +
                "p99_radial_error_pixels,unexpected_components,opaque_pinholes," +
                "transparent_artifacts,verdict",
            };
            var traceRows = new List<string>
            {
                "trace_row_id,evidence_bundle_id,evidence_run_id,sample_id," +
                "projected_x,projected_y,request_x,request_y,session_x,session_y," +
                "overlay_x,overlay_y,material_x,material_y,overlay_instance_id," +
                "material_instance_id,camera_instance_id,projection_success," +
                "fallback_reason",
            };

            for (var i = 0; i < positions.Length; i++)
            {
                for (var run = 1; run <= 3; run++)
                {
                    var identity = $"{positions[i].Key} run {run}";
                    var sampleId =
                        $"{evidenceRunId}-{positions[i].Key.ToLowerInvariant()}-{run}";
                    var artifactRelativePath =
                        $"production-offcenter/{positions[i].Key.ToLowerInvariant()}-" +
                        $"{run}-{sampleId}-isolated.png";
                    var traceRowId = sampleId + "-focus-trace";
                    Assert.That(sampleIds.Add(sampleId), Is.True, identity);
                    Assert.That(artifactPaths.Add(artifactRelativePath), Is.True, identity);
                    TerminalDestinationReadiness.ResetForTests();
                    TerminalSessionRegistry.ResetForTests();
                    PrepareCampaignStage(StageId.CreateOrThrow("stage-1-1"));
                    yield return LoadScene(UIAudioScenePath);
                    Assert.That(
                        StageLaunchContextStore.TryPeek(out var directPlayBootstrapContext),
                        Is.True);
                    Assert.That(
                        StageLaunchContextStore.TryConsume(directPlayBootstrapContext, out _),
                        Is.True,
                        "Production campaign bootstrap consumes its exact launch context.");

                    var host = Object.FindObjectsByType<GameplaySceneHost>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .Single();
                    var uiInstaller = Object.FindObjectsByType<GameplayUiFlowInstaller>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .Single();
                    Assert.That(host.OutputCamera, Is.Not.Null, identity);
                    Assert.That(
                        host.ViewRegistry.TryGetView(
                            host.InputHost.PlayerEntityId,
                            out var playerView),
                        Is.True);
                    Assert.That(playerView, Is.Not.Null);
                    host.InputHost.SetAutoAdvanceTicks(false);

                    MoveRendererEnvelopeToViewport(
                        host.OutputCamera,
                        playerView,
                        positions[i].Value);
                    var projectedCenter = ProjectRendererEnvelopeCenter(
                        host.OutputCamera,
                        playerView);

                    Assert.That(
                        uiInstaller.TryForceClearCurrentStageForDiagnostics(
                            out var forceClearMessage),
                        Is.True,
                        $"{identity}: {forceClearMessage}");
                    Assert.That(
                        uiInstaller.TryGetTerminalTransitionPort(out var transitionPort),
                        Is.True);
                    var productionPort = transitionPort as GameplayTerminalTransitionPort;
                    Assert.That(productionPort, Is.Not.Null);
                    var playback = productionPort.CurrentPlayback;
                    Assert.That(playback, Is.Not.Null);
                    var diagnostics = productionPort.LastFocusCaptureDiagnostics;
                    var transport = productionPort.LastFocusTransportDiagnostics;

                    productionPort.Tick(playback.Preset.FocusDuration);
                    AssertPlayerBoundsInsideAperture(
                        host.OutputCamera,
                        playerView,
                        diagnostics.CapturedCenter,
                        playback.CurrentRadius);
                    uiInstaller.enabled = false;
                    yield return null;
                    var irisView = uiInstaller.RootView.TerminalIrisOverlayView;
                    var materialCenterVector =
                        irisView.RuntimeMaterialForTests.GetVector("_Center");
                    var materialCenter = new Vector2(
                        materialCenterVector.x,
                        materialCenterVector.y);
                    var contour = CaptureTransparentApertureContour(
                        uiInstaller.RootView.GetComponent<Canvas>(),
                        host.OutputCamera,
                        irisView,
                        irisView.RuntimeMaterialForTests.GetColor("_OuterColor"),
                        positions[i].Value,
                        Path.Combine(
                            outputRoot,
                            artifactRelativePath),
                        out var renderWidth,
                        out var renderHeight);
                    var captureSha256 = Sha256File(
                        Path.Combine(outputRoot, artifactRelativePath));
                    var centerErrorViewport = Vector2.Distance(
                        contour.FittedCenterViewport,
                        positions[i].Value);
                    var session = transport.Session;
                    var camera = diagnostics.OutputCamera;
                    var targetTexture = camera != null ? camera.targetTexture : null;
                    fallbackCount += diagnostics.IsFallback ? 1 : 0;
                    maximumCenterErrorPixels =
                        Mathf.Max(maximumCenterErrorPixels, contour.CenterErrorPixels);
                    maximumRmsRadialErrorPixels =
                        Mathf.Max(
                            maximumRmsRadialErrorPixels,
                            contour.RmsRadialErrorPixels);
                    maximumRadialErrorPixels =
                        Mathf.Max(
                            maximumRadialErrorPixels,
                            contour.MaximumRadialErrorPixels);
                    maximumP99RadialErrorPixels =
                        Mathf.Max(
                            maximumP99RadialErrorPixels,
                            contour.P99RadialErrorPixels);

                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "projection success",
                        diagnostics.Succeeded);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "fallback reason None",
                        diagnostics.FailureReason == TerminalFocusCaptureFailureReason.None &&
                        !diagnostics.IsFallback);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "projected center",
                        Vector2.Distance(projectedCenter, positions[i].Value) <= 0.025f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "request center drift",
                        Vector2.Distance(
                            diagnostics.CapturedCenter,
                            transport.RequestCenter) <= 0.0001f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "session/playback center drift",
                        Vector2.Distance(
                            transport.RequestCenter,
                            transport.PlaybackSnapshotCenter) <= 0.0001f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "overlay center drift",
                        Vector2.Distance(
                            transport.PlaybackSnapshotCenter,
                            irisView.LastAppliedCenterForDiagnostics) <= 0.0001f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "material center drift",
                        Vector2.Distance(
                            irisView.LastAppliedCenterForDiagnostics,
                            materialCenter) <= 0.0001f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "measured center <= 1 render pixel",
                        contour.CenterErrorPixels <= 1f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "circle RMS",
                        contour.RmsRadialErrorPixels <= 0.5f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "circle maximum",
                        contour.MaximumRadialErrorPixels <= 1f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "circle P99",
                        contour.P99RadialErrorPixels <= 0.75f);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "disconnected component",
                        contour.UnexpectedTransparentComponentCount == 0);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "opaque pinhole",
                        contour.OpaquePinholePixelCount == 0);
                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "transparent artifact",
                        contour.TransparentArtifactPixelCount == 0);

                    var verdict = failures.Any(failure => failure.StartsWith(identity + ":"))
                        ? "FAIL"
                        : "PASS";
                    csvRows.Add(
                        ProductionFocusCsvRow(
                            evidenceBundleId,
                            evidenceRunId,
                            sampleId,
                            traceRowId,
                            positions[i].Key,
                            run,
                            session,
                            diagnostics,
                            transport,
                            irisView,
                            positions[i].Value,
                            materialCenter,
                            contour,
                            centerErrorViewport,
                            renderWidth,
                            renderHeight,
                            targetTexture,
                            artifactRelativePath,
                            captureSha256,
                            verdict));
                    traceRows.Add(
                        ProductionFocusTraceCsvRow(
                            traceRowId,
                            evidenceBundleId,
                            evidenceRunId,
                            sampleId,
                            diagnostics,
                            transport,
                            irisView,
                            materialCenter));
                    UnityEngine.Debug.Log(
                        $"TerminalFocusEvidence bundle={evidenceBundleId} " +
                        $"runId={evidenceRunId} sampleId={sampleId} " +
                        $"direction={positions[i].Key} repetition={run} " +
                        $"transitionId={session.TransitionId} token={session.Token} " +
                        $"projected={diagnostics.ProjectedCenter} request={transport.RequestCenter} " +
                        $"session={transport.PlaybackSnapshotCenter} " +
                        $"overlay={irisView.LastAppliedCenterForDiagnostics} " +
                        $"material={materialCenter} measured={contour.FittedCenterViewport} " +
                        $"centerErrorPixels={contour.CenterErrorPixels:F6} " +
                        $"rms={contour.RmsRadialErrorPixels:F6} " +
                        $"max={contour.MaximumRadialErrorPixels:F6} " +
                        $"p99={contour.P99RadialErrorPixels:F6} " +
                        $"fallbackReason={diagnostics.FailureReason} verdict={verdict}");

                    uiInstaller.enabled = true;
                    productionPort.Tick(
                        playback.Preset.HoldDuration +
                        playback.Preset.CloseDuration);
                    var completionDeadline = Time.realtimeSinceStartup + 2f;
                    while (TerminalSessionRegistry.IsActive &&
                           Time.realtimeSinceStartup < completionDeadline)
                    {
                        yield return null;
                    }

                    CollectFocusEvidenceFailure(
                        failures,
                        identity,
                        "terminal session completion",
                        !TerminalSessionRegistry.IsActive);
                }
            }

            File.WriteAllLines(
                Path.Combine(outputDirectory, "off-center-focus-trace.csv"),
                csvRows);
            File.WriteAllLines(
                Path.Combine(outputDirectory, "focus-transport-trace.csv"),
                traceRows);
            Assert.That(sampleIds.Count, Is.EqualTo(12));
            Assert.That(artifactPaths.Count, Is.EqualTo(12));
            File.WriteAllText(
                Path.Combine(outputDirectory, "off-center-summary.json"),
                "{\n" +
                "  \"schemaVersion\": 2,\n" +
                $"  \"evidenceBundleId\": \"{JsonEscape(evidenceBundleId)}\",\n" +
                $"  \"evidenceRunId\": \"{JsonEscape(evidenceRunId)}\",\n" +
                $"  \"sampleCount\": {sampleIds.Count},\n" +
                $"  \"uniqueSampleCount\": {sampleIds.Count},\n" +
                $"  \"uniqueArtifactPathCount\": {artifactPaths.Count},\n" +
                "  \"directionRepetitionMatrix\": \"Leftx3,Rightx3,Topx3,Bottomx3\",\n" +
                $"  \"maximumCenterErrorPixels\": {EvidenceFloat(maximumCenterErrorPixels)},\n" +
                $"  \"maximumRmsRadialErrorPixels\": {EvidenceFloat(maximumRmsRadialErrorPixels)},\n" +
                $"  \"maximumRadialErrorPixels\": {EvidenceFloat(maximumRadialErrorPixels)},\n" +
                $"  \"maximumP99RadialErrorPixels\": {EvidenceFloat(maximumP99RadialErrorPixels)},\n" +
                $"  \"fallbackCount\": {fallbackCount},\n" +
                "  \"terminalTokenUniquenessClaim\": false,\n" +
                "  \"canonicalCorrelation\": \"EvidenceRunId+SampleId\"\n" +
                "}\n");
            WriteProductionFocusEnvironment(
                outputDirectory,
                $"isolated-render-target={Screen.width}x{Screen.height}");
            Assert.That(
                failures,
                Is.Empty,
                "Aggregate production off-center evidence failures:\n" +
                string.Join("\n", failures));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalVictoryBlueHandoff_ActualVictoryCreatesResultAndCleansBlockers()
        {
            yield return BeginActualVictoryAndWaitForStageResult();

            var uiInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
            var irisView = uiInstaller.RootView.TerminalIrisOverlayView;
            var irisGroup = irisView.GetComponent<CanvasGroup>();
            var irisImage = irisView.GetComponentInChildren<Image>(true);
            var stageResult = uiInstaller.StageResultScreenView;
            var continueButton = stageResult.GetComponentInChildren<Button>(true);
            var eventSystem = EventSystem.current;
            var transitionCoordinator = Object.FindFirstObjectByType<SceneTransitionCoordinator>();

            Assert.That(uiInstaller.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(stageResult, Is.Not.Null);
            Assert.That(stageResult.IsVisible, Is.True);
            Assert.That(continueButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(continueButton.interactable, Is.True);
            Assert.That(irisView.IsVisible, Is.False);
            Assert.That(irisView.BlocksRaycasts, Is.False);
            Assert.That(irisView.gameObject.activeSelf, Is.False);
            Assert.That(irisGroup.alpha, Is.Zero);
            Assert.That(irisGroup.blocksRaycasts, Is.False);
            Assert.That(irisGroup.interactable, Is.False);
            Assert.That(irisImage.raycastTarget, Is.False);
            Assert.That(TerminalSessionRegistry.IsActive, Is.False);
            Assert.That(TerminalSessionRegistry.Current.Phase, Is.EqualTo(TerminalSessionPhase.Completed));
            Assert.That(uiInstaller.Coordinator.CurrentBlockSnapshot.BlocksScreenInteraction, Is.False);
            Assert.That(uiInstaller.Coordinator.CurrentBlockSnapshot.BlocksLowerLayerPointer, Is.False);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(continueButton.gameObject));
            Assert.That(
                transitionCoordinator == null || !transitionCoordinator.IsTransitionInProgress,
                Is.True,
                "Same-scene result handoff must not request persistent scene-transition takeover.");

            var requiredEvents = new[]
            {
                TerminalTraceEvent.VictoryClaimAccepted,
                TerminalTraceEvent.IrisStarted,
                TerminalTraceEvent.BlackReached,
                TerminalTraceEvent.StageClearGateReleased,
                TerminalTraceEvent.StageClearedPublished,
                TerminalTraceEvent.DestinationMutationAdmitted,
                TerminalTraceEvent.StageResultCreated,
                TerminalTraceEvent.PayloadBound,
                TerminalTraceEvent.DestinationReadyEmitted,
                TerminalTraceEvent.DestinationReadyAccepted,
                TerminalTraceEvent.ResultBackdropReady,
                TerminalTraceEvent.ResultBackdropHandoff,
                TerminalTraceEvent.OverlayHidden,
                TerminalTraceEvent.ResultContentEntranceStarted,
                TerminalTraceEvent.ResultInteractionReady,
                TerminalTraceEvent.SessionCompleted,
            };
            var trace = TerminalRuntimeTrace.Snapshot;
            var previousIndex = -1;
            for (var i = 0; i < requiredEvents.Length; i++)
            {
                var currentIndex = FindTraceIndex(trace, requiredEvents[i]);
                Assert.That(
                    currentIndex,
                    Is.GreaterThan(previousIndex),
                    $"{requiredEvents[i]} missing or out of order. Trace: {FormatTrace(trace)}");
                previousIndex = currentIndex;
            }

            Assert.That(
                trace.Any(record =>
                    record.Event == TerminalTraceEvent.DestinationReadyRejected ||
                    record.Event == TerminalTraceEvent.RevealRequested ||
                    record.Event == TerminalTraceEvent.RevealStarted ||
                    record.Event == TerminalTraceEvent.RevealCompleted),
                Is.False,
                FormatTrace(trace));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalResultHandoffCover_ActualVictoryAlphaTimelineIsMonotonic()
        {
            var outputRoot = ReadCommandLineValue("-terminalIrisQualityOutput");
            var persistentCoverCapturePath = string.IsNullOrEmpty(outputRoot)
                ? null
                : Path.Combine(
                    outputRoot,
                    "final-close-frames",
                    "victory",
                    "persistent-cover-first-rendered.png");
            GameplayUiFlowInstaller uiInstaller = null;
            yield return BeginActualVictory(
                StageId.CreateOrThrow("stage-1-1"),
                installer => uiInstaller = installer);

            var style = Resources.Load<ResultTransitionVisualStyle>(
                "UI/Transitions/ResultTransitionVisualStyle");
            Assert.That(style, Is.Not.Null);
            var snapshot = style.CreateDimSnapshot();
            var previousCoverAlpha = 1f;
            var previousContentAlpha = 0f;
            var sawOpaqueCover = false;
            var sawIntermediateCover = false;
            var sawIntermediateContent = false;
            var capturedPersistentCover = false;
            var sampledFrames = 0;
            var deadline = Time.realtimeSinceStartup + 5f;

            while (TerminalSessionRegistry.IsActive &&
                   Time.realtimeSinceStartup < deadline)
            {
                var result = uiInstaller.StageResultScreenView;
                if (result != null)
                {
                    sampledFrames++;
                    var coverAlpha = result.HandoffCoverAlpha;
                    var contentAlpha = result.ContentAlpha;
                    Assert.That(
                        result.BackdropColor,
                        Is.EqualTo(snapshot.FinalColor),
                        "ResultBackdrop must remain at the immutable final snapshot color for the whole handoff.");
                    Assert.That(
                        coverAlpha,
                        Is.LessThanOrEqualTo(previousCoverAlpha + 0.0001f),
                        $"ResultHandoffCover alpha increased on sampled frame {sampledFrames}.");
                    Assert.That(
                        contentAlpha,
                        Is.GreaterThanOrEqualTo(previousContentAlpha - 0.0001f),
                        $"ContentRoot alpha decreased on sampled frame {sampledFrames}.");

                    var session = TerminalSessionRegistry.Current;
                    if (session.Phase == TerminalSessionPhase.WaitingSameSceneDestination)
                    {
                        Assert.That(contentAlpha, Is.Zero.Within(0.0001f));
                    }

                    if (coverAlpha >= 0.999f)
                    {
                        sawOpaqueCover = true;
                        if (!capturedPersistentCover &&
                            HasGraphicsDevice() &&
                            !string.IsNullOrEmpty(persistentCoverCapturePath))
                        {
                            yield return null;
                            WriteProductionScreenPng(persistentCoverCapturePath);
                            capturedPersistentCover = true;
                        }
                    }

                    if (coverAlpha > 0f && coverAlpha < 0.999f)
                    {
                        sawIntermediateCover = true;
                    }

                    if (contentAlpha > 0f && contentAlpha < 0.999f)
                    {
                        sawIntermediateContent = true;
                    }

                    previousCoverAlpha = coverAlpha;
                    previousContentAlpha = contentAlpha;
                }

                yield return null;
            }

            var stageResult = uiInstaller.StageResultScreenView;
            Assert.That(TerminalSessionRegistry.IsActive, Is.False);
            Assert.That(sampledFrames, Is.GreaterThan(2));
            Assert.That(sawOpaqueCover, Is.True);
            if (HasGraphicsDevice() && !string.IsNullOrEmpty(persistentCoverCapturePath))
            {
                Assert.That(capturedPersistentCover, Is.True);
            }
            var trace = TerminalRuntimeTrace.Snapshot;
            Assert.That(
                FindTraceIndex(trace, TerminalTraceEvent.ResultBackdropReady),
                Is.LessThan(FindTraceIndex(trace, TerminalTraceEvent.OverlayHidden)),
                FormatTrace(trace));
            Assert.That(sawIntermediateCover, Is.True);
            Assert.That(sawIntermediateContent, Is.True);
            Assert.That(stageResult.BackdropColor, Is.EqualTo(snapshot.FinalColor));
            Assert.That(stageResult.HandoffCoverAlpha, Is.Zero);
            Assert.That(stageResult.IsHandoffCoverActive, Is.False);
            Assert.That(stageResult.ContentAlpha, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(stageResult.IsInteractionReady, Is.True);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalProductionStageResultInput_PointerClickNavigatesExactlyOnce()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return BeginActualVictoryAndWaitForStageResult();

                var uiInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                var button = uiInstaller.StageResultScreenView.GetComponentInChildren<Button>(true);
                var eventSystem = EventSystem.current;
                var coordinator = SceneTransitionCoordinator.Instance;
                var baselineAccepted = coordinator.AcceptedTransitionCount;
                var clickObserved = 0;
                button.onClick.AddListener(() => clickObserved++);
                var screenPoint = RectTransformUtility.WorldToScreenPoint(
                    null,
                    ((RectTransform)button.transform).TransformPoint(
                        ((RectTransform)button.transform).rect.center));
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = screenPoint,
                };
                var raycastResults = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, raycastResults);
                Assert.That(
                    raycastResults.Any(result =>
                        result.gameObject == button.gameObject ||
                        result.gameObject.transform.IsChildOf(button.transform)),
                    Is.True,
                    "Actual EventSystem raycast must reach the StageResult continue button.");

                QueueMouseState(mouse, screenPoint, leftPressed: false);
                yield return null;
                QueueMouseState(mouse, screenPoint, leftPressed: true);
                yield return null;
                QueueMouseState(mouse, screenPoint, leftPressed: false);
                yield return null;

                var inputDeadline = Time.realtimeSinceStartup + 1f;
                while (clickObserved == 0 &&
                       Time.realtimeSinceStartup < inputDeadline)
                {
                    yield return null;
                }

                Assert.That(clickObserved, Is.EqualTo(1), "Actual Button.onClick must be raised exactly once.");
                Assert.That(
                    coordinator.AcceptedTransitionCount - baselineAccepted,
                    Is.EqualTo(1),
                    $"Navigation request was not accepted. inProgress={coordinator.IsTransitionInProgress}");
                Assert.That(coordinator.IsTransitionInProgress, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalProductionStageResultInput_KeyboardSubmitNavigatesExactlyOnce()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return BeginActualVictoryAndWaitForStageResult();

                var uiInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                var button = uiInstaller.StageResultScreenView.GetComponentInChildren<Button>(true);
                var inputRouter = uiInstaller.GetComponent<UiNavigationInputRouter>();
                var coordinator = SceneTransitionCoordinator.Instance;
                var baselineAccepted = coordinator.AcceptedTransitionCount;
                var baselineSubmit = inputRouter.SubmitPerformedCount;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(button.gameObject));
                var submitAction = Object.FindFirstObjectByType<GameplaySceneHost>()
                    .InputHost.Actions.FindAction("UI/Submit", throwIfNotFound: true);
                Assert.That(submitAction.enabled, Is.True, "UI/Submit must remain enabled after terminal completion.");

                QueueKeyboardEnterState(keyboard, pressed: true);
                yield return null;
                QueueKeyboardEnterState(keyboard, pressed: false);
                yield return null;

                var inputDeadline = Time.realtimeSinceStartup + 1f;
                while (inputRouter.SubmitPerformedCount == baselineSubmit &&
                       Time.realtimeSinceStartup < inputDeadline)
                {
                    yield return null;
                }

                Assert.That(
                    inputRouter.SubmitPerformedCount - baselineSubmit,
                    Is.EqualTo(1),
                    $"Actual Submit action did not reach the UI router. actionEnabled={submitAction.enabled}");
                Assert.That(inputRouter.LastSubmitDispatchResult, Is.True);
                Assert.That(
                    coordinator.AcceptedTransitionCount - baselineAccepted,
                    Is.EqualTo(1),
                    $"Navigation request was not accepted. inProgress={coordinator.IsTransitionInProgress}");
                Assert.That(coordinator.IsTransitionInProgress, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalStageEntryOpening_ActualStageResultLoadsNewSceneAndReleasesGameplay()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return BeginActualVictoryAndWaitForStageResult();

                var sourceHost = Object.FindFirstObjectByType<GameplaySceneHost>();
                var sourceHostId = sourceHost.GetInstanceID();
                var sourceInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                var nextButton = sourceInstaller.StageResultScreenView.GetComponentInChildren<Button>(true);
                var eventSystem = EventSystem.current;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(
                    null,
                    ((RectTransform)nextButton.transform).TransformPoint(
                        ((RectTransform)nextButton.transform).rect.center));

                QueueMouseState(mouse, screenPoint, leftPressed: false);
                yield return null;
                QueueMouseState(mouse, screenPoint, leftPressed: true);
                yield return null;
                QueueMouseState(mouse, screenPoint, leftPressed: false);
                yield return null;

                var entryClaimDeadline = Time.realtimeSinceStartup + 1f;
                while (!SceneEntryPresentationRegistry.IsActive &&
                       Time.realtimeSinceStartup < entryClaimDeadline)
                {
                    yield return null;
                }

                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
                var claimed = SceneEntryPresentationRegistry.Current;
                Assert.That(claimed.Token.IsValid, Is.True);
                Assert.That(claimed.DestinationStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-1")));
                var persistentCover = Object.FindFirstObjectByType<SceneTransitionOverlayShellView>(
                    FindObjectsInactive.Include);
                Assert.That(persistentCover, Is.Not.Null);
                var previousPersistentAlpha = persistentCover.PersistentCoverOpacityForTests;
                var sawIntermediatePersistentAlpha =
                    previousPersistentAlpha > 0f && previousPersistentAlpha < 1f;
                var sawOpaquePersistentFrame = false;

                GameplaySceneHost destinationHost = null;
                GameplayUiFlowInstaller destinationInstaller = null;
                var deadline = Time.realtimeSinceStartup + 12f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    destinationHost = Object.FindObjectsByType<GameplaySceneHost>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .FirstOrDefault(candidate => candidate.GetInstanceID() != sourceHostId);
                    destinationInstaller = destinationHost != null
                        ? destinationHost.GetComponent<GameplayUiFlowInstaller>()
                        : null;
                    var persistentAlpha = persistentCover.PersistentCoverOpacityForTests;
                    if (!sawOpaquePersistentFrame)
                    {
                        Assert.That(
                            persistentAlpha,
                            Is.GreaterThanOrEqualTo(previousPersistentAlpha - 0.0001f),
                            "Victory persistent cover alpha must increase monotonically before its opaque rendered frame.");
                    }

                    if (persistentAlpha > 0f && persistentAlpha < 0.999f)
                    {
                        sawIntermediatePersistentAlpha = true;
                    }

                    if (persistentAlpha >= 0.999f &&
                        persistentCover.HasRenderedOpaqueFrame)
                    {
                        sawOpaquePersistentFrame = true;
                    }

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

                Assert.That(destinationHost, Is.Not.Null, "The production Single load did not install the destination gameplay host.");
                Assert.That(destinationInstaller, Is.Not.Null);
                Assert.That(sawIntermediatePersistentAlpha, Is.True);
                Assert.That(sawOpaquePersistentFrame, Is.True);
                Assert.That(persistentCover.HasAcknowledgedOpaqueFrame, Is.True);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Opening),
                    SceneEntryPresentationRegistry.Current.FailureReason);

                var openingTickIndex = destinationHost.TickRunner.NextTickIndex;
                Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Null);
                Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(openingTickIndex));

                var focusSource = new GameplayTerminalFocusTargetSource(
                    destinationHost.ViewRegistry,
                    destinationHost.OutputCamera);
                Assert.That(
                    focusSource.TryCapture(destinationHost.PlayerEntityId, out var focus),
                    Is.True,
                    focusSource.LastCaptureDiagnostics.FailureReason.ToString());
                Assert.That(focus.IsFallback, Is.False);

                var entryIris = destinationInstaller.RootView.TerminalIrisOverlayView;
                var materialCenterVector = entryIris.RuntimeMaterialForTests.GetVector("_Center");
                var materialCenter = new Vector2(materialCenterVector.x, materialCenterVector.y);
                Assert.That(
                    materialCenter.x,
                    Is.EqualTo(destinationInstaller.EntryFocusCenterForTests.x).Within(0.0001f));
                Assert.That(
                    materialCenter.y,
                    Is.EqualTo(destinationInstaller.EntryFocusCenterForTests.y).Within(0.0001f));
                Assert.That(
                    Vector2.Distance(materialCenter, focus.NormalizedCenter),
                    Is.LessThanOrEqualTo(0.04f),
                    "The production entry capture must remain player-centered while the initial player presentation settles.");
                Assert.That(entryIris.IsVisible, Is.True);
                Assert.That(entryIris.BlocksRaycasts, Is.True);

                for (var i = 0; i < 2; i++)
                {
                    yield return null;
                    Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(openingTickIndex));
                }

                deadline = Time.realtimeSinceStartup + 5f;
                while (SceneEntryPresentationRegistry.IsActive &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Completed));
                Assert.That(entryIris.IsVisible, Is.False);
                Assert.That(entryIris.BlocksRaycasts, Is.False);

                destinationHost.InputHost.SetAutoAdvanceTicks(false);
                var releasedTickIndex = destinationHost.TickRunner.NextTickIndex;
                var firstTick = destinationHost.InputHost.RunSingleTick();
                Assert.That(firstTick, Is.Not.Null);
                Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(releasedTickIndex + 1));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalResultTimeScaleZero_FullVictoryEntryFlowCompletes()
        {
            var previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                yield return BeginActualVictoryAndWaitForStageResult();

                var sourceHost = Object.FindFirstObjectByType<GameplaySceneHost>();
                var sourceHostId = sourceHost.GetInstanceID();
                var sourceInstaller = Object.FindFirstObjectByType<GameplayUiFlowInstaller>();
                var nextButton = sourceInstaller.StageResultScreenView
                    .GetComponentInChildren<Button>(true);
                Assert.That(nextButton.interactable, Is.True);
                nextButton.onClick.Invoke();

                var deadline = Time.realtimeSinceStartup + 12f;
                GameplaySceneHost destinationHost = null;
                while (Time.realtimeSinceStartup < deadline)
                {
                    destinationHost = Object.FindObjectsByType<GameplaySceneHost>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .FirstOrDefault(candidate => candidate.GetInstanceID() != sourceHostId);
                    if (destinationHost != null &&
                        !SceneEntryPresentationRegistry.IsActive &&
                        SceneEntryPresentationRegistry.Current.Phase ==
                        SceneEntryPresentationPhase.Completed)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(destinationHost, Is.Not.Null);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Completed));
                destinationHost.InputHost.SetAutoAdvanceTicks(false);
                var tickIndex = destinationHost.TickRunner.NextTickIndex;
                Assert.That(destinationHost.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(destinationHost.TickRunner.NextTickIndex, Is.EqualTo(tickIndex + 1));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalGameClearPlayerE2E_ActualFinalVictoryPointerMainExactlyOnce()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                GameplayUiFlowInstaller uiInstaller = null;
                yield return BeginActualVictory(
                    StageId.CreateOrThrow("stage-4-2"),
                    installer => uiInstaller = installer);

                var deadline = Time.realtimeSinceStartup + 5f;
                while (TerminalSessionRegistry.IsActive &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(TerminalSessionRegistry.IsActive, Is.False);
                Assert.That(uiInstaller.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.GameClear));
                var gameClear = uiInstaller.GameClearScreenView;
                var button = gameClear.GetComponentInChildren<Button>(true);
                var style = Resources.Load<ResultTransitionVisualStyle>(
                    "UI/Transitions/ResultTransitionVisualStyle");
                var snapshot = style.CreateDimSnapshot();
                Assert.That(gameClear.IsInteractionReady, Is.True);
                Assert.That(gameClear.BackdropColor, Is.EqualTo(snapshot.FinalColor));
                Assert.That(gameClear.HandoffCoverAlpha, Is.Zero);
                Assert.That(gameClear.IsHandoffCoverActive, Is.False);
                Assert.That(gameClear.ContentAlpha, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(button.interactable, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(button.gameObject));
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);

                var mainClickCount = 0;
                button.onClick.AddListener(() => mainClickCount++);
                var rect = (RectTransform)button.transform;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center));
                var pointerData = new PointerEventData(EventSystem.current)
                {
                    position = screenPoint,
                };
                var raycasts = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, raycasts);
                Assert.That(
                    raycasts.Any(result =>
                        result.gameObject == button.gameObject ||
                        result.gameObject.transform.IsChildOf(button.transform)),
                    Is.True);

                QueueMouseState(mouse, screenPoint, leftPressed: false);
                yield return null;
                QueueMouseState(mouse, screenPoint, leftPressed: true);
                yield return null;
                QueueMouseState(mouse, screenPoint, leftPressed: false);
                yield return null;
                Assert.That(mainClickCount, Is.EqualTo(1));
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalGameClearPlayerE2E_ActualFinalVictoryKeyboardMainExactlyOnce()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                GameplayUiFlowInstaller uiInstaller = null;
                yield return BeginActualVictory(
                    StageId.CreateOrThrow("stage-4-2"),
                    installer => uiInstaller = installer);

                var deadline = Time.realtimeSinceStartup + 5f;
                while (TerminalSessionRegistry.IsActive &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(TerminalSessionRegistry.IsActive, Is.False);
                Assert.That(uiInstaller.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.GameClear));
                var gameClear = uiInstaller.GameClearScreenView;
                var button = gameClear.GetComponentInChildren<Button>(true);
                var inputRouter = uiInstaller.GetComponent<UiNavigationInputRouter>();
                var submitAction = Object.FindFirstObjectByType<GameplaySceneHost>()
                    .InputHost.Actions.FindAction("UI/Submit", throwIfNotFound: true);
                Assert.That(gameClear.IsInteractionReady, Is.True);
                Assert.That(button.interactable, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(button.gameObject));
                Assert.That(submitAction.enabled, Is.True);

                var baselineSubmit = inputRouter.SubmitPerformedCount;
                var mainDispatchCount = 0;
                gameClear.MainRequested += () => mainDispatchCount++;
                QueueKeyboardEnterState(keyboard, pressed: true);
                yield return null;
                QueueKeyboardEnterState(keyboard, pressed: false);
                yield return null;

                deadline = Time.realtimeSinceStartup + 1f;
                while (inputRouter.SubmitPerformedCount == baselineSubmit &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(inputRouter.SubmitPerformedCount - baselineSubmit, Is.EqualTo(1));
                Assert.That(inputRouter.LastSubmitDispatchResult, Is.True);
                Assert.That(mainDispatchCount, Is.EqualTo(1));
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalBluePixelContinuity_RenderTextureOwnersAreEquivalent()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production prefab continuity capture requires the Editor asset database.");
            yield break;
#else
            if (!HasGraphicsDevice())
            {
                Assert.Ignore("RenderTexture pixel continuity requires a graphics device.");
            }

            const int width = 320;
            const int height = 180;
            var style = Resources.Load<ResultTransitionVisualStyle>(
                "UI/Transitions/ResultTransitionVisualStyle");
            var rootPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
            var transitionShellPrefab = Resources.Load<SceneTransitionOverlayShellView>(
                "UI/Transitions/SceneTransitionOverlayShell");
            var stageResultPrefab = AssetDatabase.LoadAssetAtPath<StageResultScreenView>(
                "Assets/_Features/UI/UI_Screens/Prefabs/StageResultScreen.prefab");

            Assert.That(style, Is.Not.Null);
            Assert.That(rootPrefab, Is.Not.Null);
            Assert.That(transitionShellPrefab, Is.Not.Null);
            Assert.That(stageResultPrefab, Is.Not.Null);

            var cameraObject = new GameObject("BlueContinuityCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            GameObject activeOwner = null;
            try
            {
                Assert.That(target.Create(), Is.True);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.91f, 0.07f, 0.53f, 1f);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.cullingMask = 1 << 5;
                camera.targetTexture = target;

                activeOwner = Object.Instantiate(rootPrefab);
                SetLayerRecursively(activeOwner, 5);
                var rootView = activeOwner.GetComponent<GameplayUiCanvasRootView>();
                rootView.EnsureHierarchy();
                ConfigureCanvasForLinearCapture(
                    activeOwner.GetComponent<Canvas>(),
                    camera);
                rootView.TerminalIrisOverlayView.ConfigureDimSnapshot(
                    style.CreateDimSnapshot());
                rootView.TerminalIrisOverlayView.Show();
                rootView.TerminalIrisOverlayView.ApplyClosedEntry(
                    new Vector2(0.2f, 0.8f),
                    rootView
                        .RequireTerminalIrisMotionProfile()
                        .CreateResolver()
                        .ResolveStageEntryOpen());
                yield return null;
                var irisClosed = CaptureLinearPixels(camera, target);
                Object.Destroy(activeOwner);
                activeOwner = null;
                yield return null;

                activeOwner = new GameObject(
                    "ResultBackdropCapture",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));
                SetLayerRecursively(activeOwner, 5);
                ConfigureCanvasForLinearCapture(activeOwner.GetComponent<Canvas>(), camera);
                var resultView = Object.Instantiate(
                    stageResultPrefab,
                    activeOwner.transform,
                    false);
                SetLayerRecursively(resultView.gameObject, 5);
                resultView.ConfigureDimSnapshot(
                    style.CreateDimSnapshot(),
                    style.CreateRuntimeSnapshot());
                resultView.PrepareOpaqueHandoff();
                resultView.SetIsCurrent(true);
                yield return null;
                var resultBackdrop = CaptureLinearPixels(camera, target);
                Assert.That(resultView.IsHandoffCoverRendered, Is.True);
                Assert.That(resultView.BeginHandoffFade(), Is.True);
                var runtimeStyle = style.CreateRuntimeSnapshot();
                var previousTimelineFrame = resultBackdrop;
                var timelineFrameCount = 0;
                var maximumMeanLuminanceDelta = 0f;
                var maximumP99LuminanceDelta = 0f;
                var maximumPixelDelta = 0f;
                var timelineBlackPixelCount = 0;
                while (!resultView.IsHandoffFadeComplete && timelineFrameCount < 120)
                {
                    resultView.AdvanceResultTransition(1f / 60f);
                    yield return null;
                    var currentTimelineFrame = CaptureLinearPixels(camera, target);
                    var luminanceDelta = CompareLinearLuminanceFrames(
                        previousTimelineFrame,
                        currentTimelineFrame);
                    var pixelDelta = CompareLinearFrames(
                        previousTimelineFrame,
                        currentTimelineFrame,
                        width,
                        height);
                    maximumMeanLuminanceDelta = Mathf.Max(
                        maximumMeanLuminanceDelta,
                        luminanceDelta.MeanDelta);
                    maximumP99LuminanceDelta = Mathf.Max(
                        maximumP99LuminanceDelta,
                        luminanceDelta.P99Delta);
                    maximumPixelDelta = Mathf.Max(
                        maximumPixelDelta,
                        pixelDelta.MaxDelta);
                    timelineBlackPixelCount += CountUnexpectedBlackPixels(
                        currentTimelineFrame);
                    previousTimelineFrame = currentTimelineFrame;
                    timelineFrameCount++;
                }

                Assert.That(resultView.IsHandoffFadeComplete, Is.True);
                Assert.That(timelineFrameCount, Is.GreaterThan(2));
                UnityEngine.Debug.Log(
                    "ResultHandoffContinuousBrightness " +
                    $"frames={timelineFrameCount} " +
                    $"meanFrameDelta={maximumMeanLuminanceDelta:F8} " +
                    $"p99Delta={maximumP99LuminanceDelta:F8} " +
                    $"maxPixelDelta={maximumPixelDelta:F8} " +
                    $"blackPixelCount={timelineBlackPixelCount} " +
                    $"meanTolerance={runtimeStyle.BrightnessMeanDeltaTolerance:F8} " +
                    $"p99Tolerance={runtimeStyle.BrightnessP99DeltaTolerance:F8}");
                Assert.That(
                    maximumMeanLuminanceDelta,
                    Is.LessThanOrEqualTo(runtimeStyle.BrightnessMeanDeltaTolerance));
                Assert.That(
                    maximumP99LuminanceDelta,
                    Is.LessThanOrEqualTo(runtimeStyle.BrightnessP99DeltaTolerance));
                Assert.That(timelineBlackPixelCount, Is.Zero);
                Object.Destroy(activeOwner);
                activeOwner = null;
                yield return null;

                var shell = Object.Instantiate(transitionShellPrefab);
                activeOwner = shell.gameObject;
                SetLayerRecursively(activeOwner, 5);
                ConfigureCanvasForLinearCapture(
                    activeOwner.GetComponentInChildren<Canvas>(true),
                    camera);
                shell.RequestOpaqueTakeover(style.CreateDimSnapshot().OpaqueColor);
                shell.HideVisual();
                yield return null;
                var persistentCover = CaptureLinearPixels(camera, target);
                Object.Destroy(activeOwner);
                activeOwner = null;
                yield return null;

                activeOwner = Object.Instantiate(rootPrefab);
                SetLayerRecursively(activeOwner, 5);
                rootView = activeOwner.GetComponent<GameplayUiCanvasRootView>();
                rootView.EnsureHierarchy();
                ConfigureCanvasForLinearCapture(
                    activeOwner.GetComponent<Canvas>(),
                    camera);
                rootView.TerminalIrisOverlayView.ConfigureDimSnapshot(
                    style.CreateDimSnapshot());
                rootView.TerminalIrisOverlayView.Show();
                rootView.TerminalIrisOverlayView.ApplyClosedEntry(
                    new Vector2(0.8f, 0.2f),
                    rootView
                        .RequireTerminalIrisMotionProfile()
                        .CreateResolver()
                        .ResolveStageEntryOpen());
                yield return null;
                var entryIrisClosed = CaptureLinearPixels(camera, target);

                var irisBackdrop = CompareLinearFrames(
                    irisClosed,
                    resultBackdrop,
                    width,
                    height);
                var backdropPersistent = CompareLinearFrames(
                    resultBackdrop,
                    persistentCover,
                    width,
                    height);
                var persistentEntry = CompareLinearFrames(
                    persistentCover,
                    entryIrisClosed,
                    width,
                    height);
                var blackPixelCount =
                    CountUnexpectedBlackPixels(irisClosed) +
                    CountUnexpectedBlackPixels(resultBackdrop) +
                    CountUnexpectedBlackPixels(persistentCover) +
                    CountUnexpectedBlackPixels(entryIrisClosed);
                UnityEngine.Debug.Log(
                    "BluePixelContinuity " +
                    $"IrisClosed_ResultBackdrop max={irisBackdrop.MaxDelta:F8} mean={irisBackdrop.MeanDelta:F8}; " +
                    $"ResultBackdrop_PersistentCover max={backdropPersistent.MaxDelta:F8} mean={backdropPersistent.MeanDelta:F8}; " +
                    $"PersistentCover_EntryIrisClosed max={persistentEntry.MaxDelta:F8} mean={persistentEntry.MeanDelta:F8}; " +
                    $"blackPixelCount={blackPixelCount}; tolerance={style.PixelComparisonTolerance:F8}");

                Assert.That(
                    irisBackdrop.MaxDelta,
                    Is.LessThanOrEqualTo(style.PixelComparisonTolerance));
                Assert.That(
                    backdropPersistent.MaxDelta,
                    Is.LessThanOrEqualTo(style.PixelComparisonTolerance));
                Assert.That(
                    persistentEntry.MaxDelta,
                    Is.LessThanOrEqualTo(style.PixelComparisonTolerance));
                Assert.That(
                    blackPixelCount,
                    Is.Zero,
                    "Opaque-blue handoff owners must not introduce a black transition pixel.");
            }
            finally
            {
                if (activeOwner != null)
                {
                    Object.Destroy(activeOwner);
                }

                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(cameraObject);
            }
#endif
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator M3GameplayEntryPixelContinuity_AuthoredOwnersRemainEquivalent()
        {
#if !UNITY_EDITOR
            Assert.Ignore("GameplayEntry continuity capture requires the Editor asset database.");
            yield break;
#else
            if (!HasGraphicsDevice())
            {
                Assert.Ignore("GameplayEntry RenderTexture continuity requires a graphics device.");
            }

            var profile = Resources.Load<TerminalIrisMotionProfile>(
                "UI/Transitions/TerminalIrisMotionProfile");
            var rootPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
            var transitionShellPrefab = Resources.Load<SceneTransitionOverlayShellView>(
                "UI/Transitions/SceneTransitionOverlayShell");
            Assert.That(profile, Is.Not.Null);
            Assert.That(rootPrefab, Is.Not.Null);
            Assert.That(transitionShellPrefab, Is.Not.Null);
            profile.ValidateOrThrow();
            var visual = profile.CreateGameplayEntryTransitionVisualSnapshot();
            var closePreset = profile.CreateResolver().ResolveGameplayEntryClose();
            var openPreset = profile.CreateResolver().ResolveStageEntryOpen();
            var resolutions = new[]
            {
                new Vector2Int(320, 180),
                new Vector2Int(320, 240),
            };

            foreach (var resolution in resolutions)
            {
                var evidenceDirectory = Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath,
                    "..",
                    "TestLogs",
                    "M3GameplayEntry",
                    "PixelContinuity",
                    $"{resolution.x}x{resolution.y}"));
                var cameraObject = new GameObject(
                    $"M3GameplayEntryCamera-{resolution.x}x{resolution.y}",
                    typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                var target = new RenderTexture(
                    resolution.x,
                    resolution.y,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                GameObject activeOwner = null;
                TerminalTransitionPlayback sourcePlayback = null;
                try
                {
                    Assert.That(target.Create(), Is.True);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.91f, 0.07f, 0.53f, 1f);
                    camera.orthographic = true;
                    camera.transform.position = new Vector3(0f, 0f, -10f);
                    camera.cullingMask = 1 << 5;
                    camera.targetTexture = target;

                    activeOwner = Object.Instantiate(rootPrefab);
                    SetLayerRecursively(activeOwner, 5);
                    var rootView = activeOwner.GetComponent<GameplayUiCanvasRootView>();
                    rootView.EnsureHierarchy();
                    rootView.HudLayer.gameObject.SetActive(false);
                    rootView.ScreenLayer.gameObject.SetActive(false);
                    rootView.PopupLayer.gameObject.SetActive(false);
                    ConfigureCanvasForLinearCapture(
                        activeOwner.GetComponent<Canvas>(),
                        camera);
                    var sourceIris = rootView.TerminalIrisOverlayView;
                    sourceIris.ConfigureTransitionColor(visual.SourceCloseColor);
                    sourcePlayback = new TerminalTransitionPlayback(closePreset);
                    var fullyRevealedRadius = sourceIris.CalculateFullyRevealedRadius(
                        closePreset.FallbackCenter,
                        0f);
                    sourcePlayback.ConfigureFullyRevealedRadii(
                        fullyRevealedRadius,
                        fullyRevealedRadius);
                    Assert.That(
                        sourcePlayback.TryBegin(
                            new TerminalTransitionRequest(
                                TerminalTransitionKind.Defeat,
                                focusEntityId: 1,
                                new TerminalSessionToken(1, 1),
                                TerminalTransitionDestinationMode.SceneHandoff),
                            new TerminalFocusTarget(
                                closePreset.FallbackCenter,
                                closePreset.FallbackRadius,
                                isFallback: true)),
                        Is.True);
                    sourceIris.Show();
                    sourceIris.Apply(sourcePlayback);
                    yield return null;
                    WriteLinearCapture(
                        CaptureLinearPixels(camera, target),
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "01-main-menu-source-pre-close.png");

                    sourcePlayback.Advance(
                        closePreset.FocusDuration +
                        closePreset.HoldDuration +
                        closePreset.CloseDuration * 0.9f);
                    sourceIris.Apply(sourcePlayback);
                    yield return null;
                    WriteLinearCapture(
                        CaptureLinearPixels(camera, target),
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "02-last-changing-close.png");

                    sourcePlayback.Advance(closePreset.CloseDuration);
                    sourceIris.Apply(sourcePlayback);
                    Assert.That(
                        sourcePlayback.State,
                        Is.EqualTo(TerminalTransitionState.Black));
                    sourceIris.RequestClosedRenderAcknowledgement();
                    yield return null;
                    var sourceClosed = CaptureLinearPixels(camera, target);
                    Assert.That(sourceIris.HasRenderedEntryClosedFrame, Is.True);
                    WriteLinearCapture(
                        sourceClosed,
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "03-source-exact-opaque.png");

                    Object.Destroy(activeOwner);
                    activeOwner = null;
                    sourcePlayback.Dispose();
                    sourcePlayback = null;
                    yield return null;

                    var shell = Object.Instantiate(transitionShellPrefab);
                    activeOwner = shell.gameObject;
                    SetLayerRecursively(activeOwner, 5);
                    ConfigureCanvasForLinearCapture(
                        activeOwner.GetComponentInChildren<Canvas>(true),
                        camera);
                    shell.RequestOpaqueTakeover(visual.HoldColor);
                    shell.HideVisual();
                    yield return null;
                    var persistentHold = CaptureLinearPixels(camera, target);
                    WriteLinearCapture(
                        persistentHold,
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "04-persistent-hold-first-rendered.png");

                    Object.Destroy(activeOwner);
                    activeOwner = null;
                    yield return null;

                    activeOwner = Object.Instantiate(rootPrefab);
                    SetLayerRecursively(activeOwner, 5);
                    rootView = activeOwner.GetComponent<GameplayUiCanvasRootView>();
                    rootView.EnsureHierarchy();
                    rootView.HudLayer.gameObject.SetActive(false);
                    rootView.ScreenLayer.gameObject.SetActive(false);
                    rootView.PopupLayer.gameObject.SetActive(false);
                    ConfigureCanvasForLinearCapture(
                        activeOwner.GetComponent<Canvas>(),
                        camera);
                    var destinationIris = rootView.TerminalIrisOverlayView;
                    destinationIris.ConfigureTransitionColor(
                        visual.DestinationOpenColor);
                    destinationIris.Show();
                    var destinationCenter = new Vector2(0.73f, 0.31f);
                    destinationIris.ApplyClosedEntry(destinationCenter, openPreset);
                    yield return null;
                    var destinationClosed = CaptureLinearPixels(camera, target);
                    Assert.That(
                        destinationIris.HasRenderedEntryClosedFrame,
                        Is.True);
                    WriteLinearCapture(
                        destinationClosed,
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "05-destination-closed-cover-released.png");

                    var openingRadius = destinationIris.CalculateFullyRevealedRadius(
                        destinationCenter,
                        openPreset.FullOpenMargin);
                    destinationIris.ApplyEntryRadius(
                        openingRadius * 0.08f,
                        openPreset.FinalClosedOvershootPixels * 0.92f);
                    yield return null;
                    WriteLinearCapture(
                        CaptureLinearPixels(camera, target),
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "06-opening-first-visible.png");
                    destinationIris.Hide();
                    yield return null;
                    WriteLinearCapture(
                        CaptureLinearPixels(camera, target),
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "07-opening-complete.png");

                    var sourceToHold = CompareLinearFrames(
                        sourceClosed,
                        persistentHold,
                        resolution.x,
                        resolution.y);
                    var holdToDestination = CompareLinearFrames(
                        persistentHold,
                        destinationClosed,
                        resolution.x,
                        resolution.y);
                    var sourceToHoldLuminance = CompareLinearLuminanceFrames(
                        sourceClosed,
                        persistentHold);
                    var holdToDestinationLuminance =
                        CompareLinearLuminanceFrames(
                            persistentHold,
                            destinationClosed);
                    var transparentPixelCount =
                        CountTransparentPixels(sourceClosed) +
                        CountTransparentPixels(persistentHold) +
                        CountTransparentPixels(destinationClosed);
                    const float channelTolerance = 2f / 255f;
                    var unexpectedChromaShift =
                        CountPixelsExceedingRgbDelta(
                            sourceClosed,
                            persistentHold,
                            channelTolerance) +
                        CountPixelsExceedingRgbDelta(
                            persistentHold,
                            destinationClosed,
                            channelTolerance);
                    var unexpectedBlackPixels =
                        CountUnexpectedBlackPixels(sourceClosed) +
                        CountUnexpectedBlackPixels(persistentHold) +
                        CountUnexpectedBlackPixels(destinationClosed);
                    UnityEngine.Debug.Log(
                        "M3GameplayEntryPixelContinuity " +
                        $"path={evidenceDirectory} " +
                        $"resolution={resolution.x}x{resolution.y} " +
                        $"sourceToHoldChanged={CountChangedPixels(sourceClosed, persistentHold)} " +
                        $"sourceToHoldMax={sourceToHold.MaxDelta:F8} " +
                        $"sourceToHoldMean={sourceToHold.MeanDelta:F8} " +
                        $"sourceToHoldMeanLuminance={sourceToHoldLuminance.MeanDelta:F8} " +
                        $"sourceToHoldP99Luminance={sourceToHoldLuminance.P99Delta:F8} " +
                        $"holdToDestinationChanged={CountChangedPixels(persistentHold, destinationClosed)} " +
                        $"holdToDestinationMax={holdToDestination.MaxDelta:F8} " +
                        $"holdToDestinationMean={holdToDestination.MeanDelta:F8} " +
                        $"holdToDestinationMeanLuminance={holdToDestinationLuminance.MeanDelta:F8} " +
                        $"holdToDestinationP99Luminance={holdToDestinationLuminance.P99Delta:F8} " +
                        $"transparentPixelCount={transparentPixelCount} " +
                        $"unexpectedBlackPixels={unexpectedBlackPixels} " +
                        $"unexpectedChromaShift={unexpectedChromaShift}");
                    File.WriteAllText(
                        Path.Combine(
                            evidenceDirectory,
                            "continuity-metrics.txt"),
                        "same_color_channel_tolerance=2/255\n" +
                        $"source_to_hold_changed_pixel_count={CountChangedPixels(sourceClosed, persistentHold)}\n" +
                        $"source_to_hold_max_channel_delta={sourceToHold.MaxDelta:F8}\n" +
                        $"source_to_hold_mean_channel_delta={sourceToHold.MeanDelta:F8}\n" +
                        $"source_to_hold_mean_luminance_delta={sourceToHoldLuminance.MeanDelta:F8}\n" +
                        $"source_to_hold_p99_luminance_delta={sourceToHoldLuminance.P99Delta:F8}\n" +
                        $"hold_to_destination_changed_pixel_count={CountChangedPixels(persistentHold, destinationClosed)}\n" +
                        $"hold_to_destination_max_channel_delta={holdToDestination.MaxDelta:F8}\n" +
                        $"hold_to_destination_mean_channel_delta={holdToDestination.MeanDelta:F8}\n" +
                        $"hold_to_destination_mean_luminance_delta={holdToDestinationLuminance.MeanDelta:F8}\n" +
                        $"hold_to_destination_p99_luminance_delta={holdToDestinationLuminance.P99Delta:F8}\n" +
                        $"transparent_pixel_count={transparentPixelCount}\n" +
                        $"unexpected_black_pixel_count={unexpectedBlackPixels}\n" +
                        $"unexpected_chroma_shift_count={unexpectedChromaShift}\n");
                    Assert.That(transparentPixelCount, Is.Zero);
                    Assert.That(unexpectedBlackPixels, Is.Zero);
                    Assert.That(unexpectedChromaShift, Is.Zero);
                }
                finally
                {
                    sourcePlayback?.Dispose();
                    if (activeOwner != null)
                    {
                        Object.Destroy(activeOwner);
                    }

                    camera.targetTexture = null;
                    target.Release();
                    Object.Destroy(target);
                    Object.Destroy(cameraObject);
                }

                yield return null;
            }
#endif
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator M4NeutralBlackPixelContinuity_AuthoredOwnersRemainEquivalent()
        {
#if !UNITY_EDITOR
            Assert.Ignore("M4 continuity capture requires the Editor asset database.");
            yield break;
#else
            if (!HasGraphicsDevice())
            {
                Assert.Ignore("M4 RenderTexture continuity requires a graphics device.");
            }

            var profile = Resources.Load<TerminalIrisMotionProfile>(
                "UI/Transitions/TerminalIrisMotionProfile");
            var rootPrefab =
                Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
            var shellPrefab =
                Resources.Load<SceneTransitionOverlayShellView>(
                    "UI/Transitions/SceneTransitionOverlayShell");
            Assert.That(profile, Is.Not.Null);
            Assert.That(rootPrefab, Is.Not.Null);
            Assert.That(shellPrefab, Is.Not.Null);
            var generation =
                TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                    9404,
                    "M4PixelContinuity");
            Assert.That(
                MainMenuEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.ReturnToMainMenu,
                    generation,
                    "m4-pixel-continuity",
                    out var token),
                Is.True);
            MainMenuTransitionVisualPolicy.Capture(
                token,
                SceneTransitionRoutePolicyCatalog.ResolveProduction(
                    SceneTransitionIntent.ReturnToMainMenu));
            var visual = MainMenuTransitionVisualPolicy.Require(
                token,
                SceneTransitionIntent.ReturnToMainMenu);
            var cinematicVisual =
                TerminalIrisMotionProfile
                    .CreateCinematicTransitionVisualSnapshot(Color.black);
            AssertColorsEquivalent(
                cinematicVisual.HoldColor,
                visual.HoldColor,
                "Cinematic and Main Menu policies must share authored black.");
            var closePreset =
                profile.CreateResolver().ResolveGameplayEntryClose();
            var openPreset =
                profile.CreateResolver().ResolveStageEntryOpen();

            foreach (var resolution in new[]
                     {
                         new Vector2Int(320, 180),
                         new Vector2Int(320, 240),
                     })
            {
                var evidenceDirectory = Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath,
                    "..",
                    "TestLogs",
                    "M4MainMenuCinematic",
                    "PixelContinuity",
                    $"{resolution.x}x{resolution.y}"));
                var cameraObject = new GameObject(
                    $"M4Camera-{resolution.x}x{resolution.y}",
                    typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                var target = new RenderTexture(
                    resolution.x,
                    resolution.y,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                GameObject owner = null;
                TerminalTransitionPlayback playback = null;
                try
                {
                    Assert.That(target.Create(), Is.True);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor =
                        new Color(0.91f, 0.07f, 0.53f, 1f);
                    camera.orthographic = true;
                    camera.transform.position = new Vector3(0f, 0f, -10f);
                    camera.cullingMask = 1 << 5;
                    camera.targetTexture = target;

                    owner = Object.Instantiate(rootPrefab);
                    SetLayerRecursively(owner, 5);
                    var root =
                        owner.GetComponent<GameplayUiCanvasRootView>();
                    root.EnsureHierarchy();
                    root.HudLayer.gameObject.SetActive(false);
                    root.ScreenLayer.gameObject.SetActive(false);
                    root.PopupLayer.gameObject.SetActive(false);
                    ConfigureCanvasForLinearCapture(
                        owner.GetComponent<Canvas>(),
                        camera);
                    var sourceIris = root.TerminalIrisOverlayView;
                    sourceIris.ConfigureTransitionColor(
                        visual.SourceCloseColor);
                    playback = new TerminalTransitionPlayback(closePreset);
                    var radius = sourceIris.CalculateFullyRevealedRadius(
                        closePreset.FallbackCenter,
                        0f);
                    playback.ConfigureFullyRevealedRadii(radius, radius);
                    Assert.That(
                        playback.TryBegin(
                            new TerminalTransitionRequest(
                                TerminalTransitionKind.Defeat,
                                1,
                                new TerminalSessionToken(1, 1),
                                TerminalTransitionDestinationMode.SceneHandoff),
                            new TerminalFocusTarget(
                                closePreset.FallbackCenter,
                                closePreset.FallbackRadius,
                                true)),
                        Is.True);
                    sourceIris.Show();
                    playback.Advance(
                        closePreset.FocusDuration +
                        closePreset.HoldDuration +
                        closePreset.CloseDuration);
                    sourceIris.Apply(playback);
                    sourceIris.RequestClosedRenderAcknowledgement();
                    yield return null;
                    var sourceClosed = CaptureLinearPixels(camera, target);
                    WriteLinearCapture(
                        sourceClosed,
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "01-source-exact-opaque.png");

                    Object.Destroy(owner);
                    owner = null;
                    playback.Dispose();
                    playback = null;
                    yield return null;

                    var shell = Object.Instantiate(shellPrefab);
                    owner = shell.gameObject;
                    SetLayerRecursively(owner, 5);
                    ConfigureCanvasForLinearCapture(
                        owner.GetComponentInChildren<Canvas>(true),
                        camera);
                    shell.RequestOpaqueTakeover(visual.HoldColor);
                    shell.HideVisual();
                    yield return null;
                    var persistent = CaptureLinearPixels(camera, target);
                    WriteLinearCapture(
                        persistent,
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "02-persistent-cover.png");

                    Object.Destroy(owner);
                    owner = Object.Instantiate(rootPrefab);
                    yield return null;
                    SetLayerRecursively(owner, 5);
                    root = owner.GetComponent<GameplayUiCanvasRootView>();
                    root.EnsureHierarchy();
                    root.HudLayer.gameObject.SetActive(false);
                    root.ScreenLayer.gameObject.SetActive(false);
                    root.PopupLayer.gameObject.SetActive(false);
                    ConfigureCanvasForLinearCapture(
                        owner.GetComponent<Canvas>(),
                        camera);
                    var destinationIris = root.TerminalIrisOverlayView;
                    destinationIris.ConfigureTransitionColor(
                        visual.DestinationOpenColor);
                    destinationIris.Show();
                    destinationIris.ApplyClosedEntry(
                        new Vector2(0.5f, 0.5f),
                        openPreset);
                    yield return null;
                    var destinationClosed =
                        CaptureLinearPixels(camera, target);
                    WriteLinearCapture(
                        destinationClosed,
                        resolution.x,
                        resolution.y,
                        evidenceDirectory,
                        "03-main-menu-closed.png");

                    var sourceToCover = CompareLinearFrames(
                        sourceClosed,
                        persistent,
                        resolution.x,
                        resolution.y);
                    var coverToMenu = CompareLinearFrames(
                        persistent,
                        destinationClosed,
                        resolution.x,
                        resolution.y);
                    var sourceToCoverLuminance =
                        CompareLinearLuminanceFrames(
                            sourceClosed,
                            persistent);
                    var coverToMenuLuminance =
                        CompareLinearLuminanceFrames(
                            persistent,
                            destinationClosed);
                    const float channelTolerance = 2f / 255f;
                    var transparentPixels =
                        CountTransparentPixels(sourceClosed) +
                        CountTransparentPixels(persistent) +
                        CountTransparentPixels(destinationClosed);
                    var chromaShift =
                        CountPixelsExceedingRgbDelta(
                            sourceClosed,
                            persistent,
                            channelTolerance) +
                        CountPixelsExceedingRgbDelta(
                            persistent,
                            destinationClosed,
                            channelTolerance);
                    UnityEngine.Debug.Log(
                        "M4NeutralBlackPixelContinuity " +
                        $"path={evidenceDirectory} " +
                        $"resolution={resolution.x}x{resolution.y} " +
                        $"sourceToCoverMax={sourceToCover.MaxDelta:F8} " +
                        $"coverToMenuMax={coverToMenu.MaxDelta:F8} " +
                        $"transparentPixelCount={transparentPixels} " +
                        $"unexpectedChromaShift={chromaShift}");
                    File.WriteAllText(
                        Path.Combine(
                            evidenceDirectory,
                            "continuity-metrics.txt"),
                        "same_color_channel_tolerance=2/255\n" +
                        $"source_to_cover_changed_pixel_count={CountChangedPixels(sourceClosed, persistent)}\n" +
                        $"source_to_cover_max_channel_delta={sourceToCover.MaxDelta:F8}\n" +
                        $"source_to_cover_mean_channel_delta={sourceToCover.MeanDelta:F8}\n" +
                        $"source_to_cover_mean_luminance_delta={sourceToCoverLuminance.MeanDelta:F8}\n" +
                        $"source_to_cover_p99_luminance_delta={sourceToCoverLuminance.P99Delta:F8}\n" +
                        $"cover_to_menu_changed_pixel_count={CountChangedPixels(persistent, destinationClosed)}\n" +
                        $"cover_to_menu_max_channel_delta={coverToMenu.MaxDelta:F8}\n" +
                        $"cover_to_menu_mean_channel_delta={coverToMenu.MeanDelta:F8}\n" +
                        $"cover_to_menu_mean_luminance_delta={coverToMenuLuminance.MeanDelta:F8}\n" +
                        $"cover_to_menu_p99_luminance_delta={coverToMenuLuminance.P99Delta:F8}\n" +
                        $"transparent_pixel_count={transparentPixels}\n" +
                        $"unexpected_chroma_shift_count={chromaShift}\n");
                    Assert.That(transparentPixels, Is.Zero);
                    Assert.That(chromaShift, Is.Zero);
                    Assert.That(
                        sourceToCover.MaxDelta,
                        Is.LessThanOrEqualTo(channelTolerance));
                    Assert.That(
                        coverToMenu.MaxDelta,
                        Is.LessThanOrEqualTo(channelTolerance));
                }
                finally
                {
                    playback?.Dispose();
                    if (owner != null)
                    {
                        Object.Destroy(owner);
                    }

                    camera.targetTexture = null;
                    target.Release();
                    Object.Destroy(target);
                    Object.Destroy(cameraObject);
                }

                yield return null;
            }

            MainMenuTransitionVisualPolicy.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
#endif
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioSceneStage1_1_DirectPlayEvidence_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-1-1"),
                assertDirectPlayEvidence: true);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioScene_DamageDeathVfxProductionPort_ReachesConcreteRuntimeWithoutMissingPort()
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
            yield return LoadScene(UIAudioScenePath);

            var host = Object.FindObjectsByType<GameplaySceneHost>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single();
            AssertDamageDeathVfxBootstrap(UIAudioScenePath, host);
            var runtime = host.GetComponent<GameplayVfxProductionRuntime>();
            Assert.That(runtime, Is.Not.Null);

            var baselineTick = host.InputHost.RunSingleTick();
            yield return null;
            Assert.That(baselineTick, Is.Not.Null, "UIAudioScene must produce a baseline tick before Damage/Death VFX presentation injection.");

            var diagnosticsBefore = host.Presenter.DamageDeathVfxExecutorDiagnostics;
            var classifiedPlaybackBefore =
                diagnosticsBefore.PlaybackSucceededCount +
                diagnosticsBefore.BindingMissingCount +
                diagnosticsBefore.TargetMissingCount +
                diagnosticsBefore.AnchorMissingCount;
            var runtimeRequestBefore = runtime.DamageDeathPlaybackRequestCount;
            var plannedBefore = runtime.LastPlannedRequestCount;
            var result = CreateDamageDeathVfxTickResult(
                tickIndex: 803,
                topology: host.Presenter.CurrentTopology,
                finalEntities: baselineTick.FinalEntities.ToArray(),
                enemyDamageEntityId: 40,
                includeBoxDestroy: true);

            host.Presenter.Present(result);
            yield return null;

            var diagnosticsAfter = host.Presenter.DamageDeathVfxExecutorDiagnostics;
            Assert.That(diagnosticsAfter.IsProductionDefaultOwner, Is.True);
            Assert.That(
                diagnosticsAfter.PlaybackRequestedCount - diagnosticsBefore.PlaybackRequestedCount,
                Is.EqualTo(1));
            Assert.That(
                diagnosticsAfter.PortMissingCount - diagnosticsBefore.PortMissingCount,
                Is.Zero);
            Assert.That(
                diagnosticsAfter.PlaybackSucceededCount +
                diagnosticsAfter.BindingMissingCount +
                diagnosticsAfter.TargetMissingCount +
                diagnosticsAfter.AnchorMissingCount -
                classifiedPlaybackBefore,
                Is.EqualTo(1));
            Assert.That(
                runtime.DamageDeathPlaybackRequestCount - runtimeRequestBefore,
                Is.EqualTo(1));
            Assert.That(
                runtime.LastPlannedRequestCount,
                Is.GreaterThanOrEqualTo(plannedBefore));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualSceneBootstrap_UIAudioScene_TopologyRuntimeGate_ProductionBridgeLockCleanupAndDeterminism()
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
            yield return LoadScene(UIAudioScenePath);

            var host = Object.FindObjectsByType<GameplaySceneHost>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single();
            AssertAudioBootstrap(UIAudioScenePath, host);
            AssertUiBootstrap(UIAudioScenePath);
            AssertTopologyBootstrap(UIAudioScenePath, host);
            AssertPresentationDefaultBootstrap(UIAudioScenePath, host);

            var boardSurface = host.BoardRoot.BoardSurfaceRenderer;
            var cameraRig = host.GetComponent<GameplayCameraRig>();
            var postFx = host.GetComponent<TopologyTransitionPostFxController>();
            Assert.That(boardSurface, Is.Not.Null, "UIAudioScene must expose the board surface renderer.");
            Assert.That(boardSurface.SteadyTileCount, Is.GreaterThan(0), "UIAudioScene must render steady board-surface content.");
            Assert.That(cameraRig, Is.Not.Null, "UIAudioScene must expose the camera orbit rig.");
            Assert.That(postFx, Is.Not.Null, "UIAudioScene must expose topology post-fx.");

            var sourceTopology = host.Presenter.CurrentTopology;
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var baselineTick = host.InputHost.RunSingleTick();
            yield return null;
            Assert.That(baselineTick, Is.Not.Null, "UIAudioScene must produce a baseline tick before topology presentation injection.");
            var finalEntities = baselineTick.FinalEntities.ToArray();
            var eventLog = new[] { "TopologyRuntimeGate|BeforePresentation" };
            var result = CreateTopologyTransitionTickResult(
                tickIndex: 701,
                sourceTopology,
                destinationTopology,
                CubeRotationKind.Forward,
                finalEntities,
                eventLog,
                determinismHash: "TOPOLOGY-RUNTIME-GATE");

            host.Presenter.Present(result);
            yield return null;

            var startTelemetry = host.Presenter.TopologyProductionTelemetrySnapshot;
            Assert.That(startTelemetry.IsProductionDefaultOwner, Is.True);
            Assert.That(startTelemetry.ExecutorOwnerExecutedCount, Is.EqualTo(1));
            Assert.That(startTelemetry.ObservedTrackCount, Is.EqualTo(1));
            Assert.That(startTelemetry.RouteCount, Is.EqualTo(1));
            Assert.That(startTelemetry.HasBlockingPresentation, Is.True);
            Assert.That(startTelemetry.IsTopologyTransitionActive, Is.True);
            Assert.That(startTelemetry.BlockingSnapshot.HasActiveBlockingPresentation, Is.True);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.True);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(boardSurface.IsTopologyTransitionActive, Is.True);
            Assert.That(boardSurface.TransitionTileCount, Is.GreaterThan(0));
            Assert.That(host.InputHost.RunSingleTick(), Is.Null, "Topology presentation lock must block input ticks.");

            host.Presenter.Present(result);
            Assert.That(
                host.Presenter.TopologyPresentationOwnershipDiagnostics.DuplicateAttemptCount,
                Is.EqualTo(1),
                "Repeated topology presentation for the same tick/source must be duplicate-suppressed.");

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);
            yield return null;

            var midState = host.Presenter.CurrentTopologyTransitionVisualState;
            Assert.That(midState.IsActive, Is.True);
            Assert.That(midState.Progress01, Is.GreaterThan(0f));
            Assert.That(midState.Progress01, Is.LessThan(1f));
            Assert.That(Quaternion.Angle(cameraRig.PresentedTopologyOrbit, Quaternion.identity), Is.GreaterThan(0.01f));
            Assert.That(float.IsNaN(cameraRig.TopologyTransitionShakeLocalPosition.x), Is.False);
            if (postFx.MotionBlurOverride != null)
            {
                Assert.That(postFx.MotionBlurOverride.intensity.value, Is.GreaterThanOrEqualTo(0f));
            }

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);
            yield return null;

            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(boardSurface.IsTopologyTransitionActive, Is.False);
            Assert.That(host.Presenter.TopologyProductionTelemetrySnapshot.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            if (postFx.MotionBlurOverride != null)
            {
                Assert.That(postFx.MotionBlurOverride.intensity.value, Is.EqualTo(0f).Within(0.0001f));
            }

            var unlockedTick = host.InputHost.RunSingleTick();
            Assert.That(unlockedTick, Is.Not.Null, "Topology lock must release after completion.");

            var repeatResult = CreateTopologyTransitionTickResult(
                tickIndex: 702,
                destinationTopology,
                sourceTopology,
                CubeRotationKind.Backward,
                finalEntities,
                eventLog,
                determinismHash: "TOPOLOGY-RUNTIME-GATE-REPEAT");
            host.Presenter.Present(repeatResult);
            yield return null;
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);

            host.Presenter.PresentInitial(finalEntities, sourceTopology);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(boardSurface.IsTopologyTransitionActive, Is.False);

            host.Presenter.Present(repeatResult);
            yield return null;
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            host.Presenter.DebugHardCleanupPresentationExtensions();
            yield return null;
            Assert.That(host.Presenter.TopologyProductionTelemetrySnapshot.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);

            Assert.That(result.DeterminismHash, Is.EqualTo("TOPOLOGY-RUNTIME-GATE"));
            Assert.That(result.EventLog, Is.EqualTo(eventLog));
            Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
        }

        private static IEnumerator AssertSceneBootstrapFirstFiveTicks(
            string scenePath,
            StageId stageId,
            bool assertDirectPlayEvidence = false)
        {
            var bootstrapRuntimeErrorCount = 0;

            void CountBootstrapRuntimeErrors(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception)
                {
                    bootstrapRuntimeErrorCount++;
                }
            }

            CampaignChanceHudDiagnostics.Clear();
            CampaignChanceHudDiagnostics.IsEnabled = assertDirectPlayEvidence;
            Application.logMessageReceived += CountBootstrapRuntimeErrors;
            try
            {
                StageLaunchContextStore.SetCurrent(stageId);
                EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
                yield return LoadScene(scenePath);

                Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(stageId), scenePath);

                var hosts = Object.FindObjectsByType<GameplaySceneHost>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                Assert.That(hosts, Has.Length.EqualTo(1), $"{scenePath} must have exactly one active GameplaySceneHost.");
                var host = hosts[0];
                Assert.That(host.InputHost, Is.Not.Null, $"{scenePath} must install GameplayInputHost.");
                Assert.That(host.Presenter, Is.Not.Null, $"{scenePath} must install GameplayTickViewPresenter.");
                Assert.That(host.TickRunner, Is.Not.Null, $"{scenePath} must initialize TickRunner.");
                Assert.That(host.WorldState, Is.Not.Null, $"{scenePath} must reach the initial gameplay state.");
                Assert.That(host.BoardRoot, Is.Not.Null, $"{scenePath} must create the runtime board root.");
                Assert.That(host.UiAccess, Is.Not.Null, $"{scenePath} must expose UIAccess as the read/intent seam.");

                AssertAudioBootstrap(scenePath, host);
                AssertUiBootstrap(scenePath);
                AssertTopologyBootstrap(scenePath, host);
                AssertDamageDeathVfxBootstrap(scenePath, host);
                AssertPresentationDefaultBootstrap(scenePath, host);
                if (assertDirectPlayEvidence)
                {
                    AssertStage1_1DirectPlayEvidence(scenePath, stageId, host);
                }

                var hashes = new string[FirstTickSmokeCount];
                for (var i = 0; i < FirstTickSmokeCount; i++)
                {
                    var result = host.InputHost.RunSingleTick();
                    yield return null;
                    Assert.That(result, Is.Not.Null, $"{scenePath} tick {i + 1} returned null.");
                    hashes[i] = string.IsNullOrEmpty(result.DeterminismHash) ? "<empty>" : result.DeterminismHash;
                }

                Assert.That(
                    bootstrapRuntimeErrorCount,
                    Is.Zero,
                    $"{scenePath} bootstrap/runtime console error count must be zero.");
                TestContext.WriteLine($"{scenePath} first-five determinism hashes: {string.Join(", ", hashes)}");
            }
            finally
            {
                Application.logMessageReceived -= CountBootstrapRuntimeErrors;
                CampaignChanceHudDiagnostics.IsEnabled = false;
            }
        }

        private static void AssertAudioBootstrap(string scenePath, GameplaySceneHost host)
        {
            var audioInstaller = host.GetComponent<AudioRuntimeInstaller>();
            Assert.That(audioInstaller, Is.Not.Null, $"{scenePath} must keep AudioRuntimeInstaller co-located on the gameplay root.");
            Assert.That(
                audioInstaller.BindingMode,
                Is.EqualTo(AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime),
                scenePath);
            Assert.That(audioInstaller.AudioService, Is.Not.Null, $"{scenePath} must install an audio service before first gameplay playback.");
            Assert.That(host.GetComponent<GlobalAudioFlowBootstrap>(), Is.Not.Null, $"{scenePath} must keep persistent BGM bootstrap co-located.");
            Assert.That(
                Object.FindObjectsByType<GlobalAudioFlowRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(1),
                $"{scenePath} must not create duplicate persistent BGM roots.");
            Assert.That(
                host.Presenter.CoreGameplaySfxRoute,
                Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor),
                $"{scenePath} must boot Core SFX with the production orchestration owner.");
            Assert.That(
                host.Presenter.CoreGameplaySfxExecutorDiagnostics.IsProductionDefaultOwner,
                Is.True,
                $"{scenePath} must report Core SFX production default owner telemetry at bootstrap.");
        }

        private static void AssertUiBootstrap(string scenePath)
        {
            var installers = Object.FindObjectsByType<GameplayUiFlowInstaller>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.That(installers, Has.Length.EqualTo(1), $"{scenePath} must have exactly one GameplayUiFlowInstaller.");
            var installer = installers[0];
            Assert.That(installer.RootView, Is.Not.Null, $"{scenePath} must create the runtime Gameplay UI canvas root.");
            Assert.That(installer.RootView.HudView, Is.Not.Null, $"{scenePath} must mount the HUD prefab under the runtime UI root.");
            Assert.That(installer.RootView.ScreenLayerView, Is.Not.Null, $"{scenePath} must create the screen layer runtime.");
            Assert.That(installer.RootView.PopupLayerView, Is.Not.Null, $"{scenePath} must create the popup layer runtime.");
            Assert.That(installer.PresentationSource, Is.Not.Null, $"{scenePath} must install the mapped UI presentation source.");
        }

        private static void AssertTopologyBootstrap(string scenePath, GameplaySceneHost host)
        {
            Assert.That(host.GetComponent<GameplayCameraTopologyAuthoring>(), Is.Not.Null, $"{scenePath} must keep scene camera topology authoring on the gameplay root.");
            Assert.That(host.GetComponent<GameplayCameraRig>(), Is.Not.Null, $"{scenePath} must install GameplayCameraRig.");
            Assert.That(host.GetComponent<TopologyTransitionPostFxController>(), Is.Not.Null, $"{scenePath} must install topology post-fx controller.");
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False, $"{scenePath} should not start stuck in a topology presentation lock.");
        }

        private static void AssertDamageDeathVfxBootstrap(string scenePath, GameplaySceneHost host)
        {
            Assert.That(
                host.GetComponent<GameplayVfxProductionRuntime>(),
                Is.Not.Null,
                $"{scenePath} must keep Gameplay_Vfx production runtime on the gameplay root.");
        }

        private static void AssertPresentationDefaultBootstrap(string scenePath, GameplaySceneHost host)
        {
            Assert.That(
                host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner,
                Is.True,
                $"{scenePath} must boot Box motion with the production orchestration owner.");
            Assert.That(
                host.Presenter.TopologyProductionTelemetrySnapshot.IsProductionDefaultOwner,
                Is.True,
                $"{scenePath} must boot topology visuals with the production playback port owner.");
            Assert.That(
                host.Presenter.PlayerActionAnimationExecutionMode,
                Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor),
                $"{scenePath} must boot player action animation with the production orchestration owner.");
        }

        private static void AssertStage1_1DirectPlayEvidence(
            string scenePath,
            StageId stageId,
            GameplaySceneHost host)
        {
            Assert.That(stageId.Value, Is.EqualTo("stage-1-1"), "This evidence smoke is scoped to stage-1-1.");
            Assert.That(scenePath, Is.EqualTo(UIAudioScenePath));
            Assert.That(stageId.Value, Is.Not.EqualTo("legacy-stage-5-1"));
            Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(stageId), "requested id must be stage-1-1.");

            var resolveRecord = CampaignChanceHudDiagnostics.Snapshot()
                .SingleOrDefault(record => record.Kind == CampaignChanceHudDiagnosticKind.StageResolve);
            Assert.That(resolveRecord, Is.Not.Null, "Runtime bootstrap must record a StageResolve diagnostic.");
            Assert.That(resolveRecord.LaunchStageId, Is.EqualTo("stage-1-1"), "resolved launch id must match the requested id.");
            Assert.That(resolveRecord.ResolvedStageId, Is.EqualTo("stage-1-1"), "alias use must be false for canonical stage-1-1.");

            var installerRecord = CampaignChanceHudDiagnostics.Snapshot()
                .SingleOrDefault(record => record.Kind == CampaignChanceHudDiagnosticKind.Installer);
            Assert.That(installerRecord, Is.Not.Null, "Runtime bootstrap must record installer diagnostics.");
            Assert.That(installerRecord.LaunchStageId, Is.EqualTo("stage-1-1"));
            Assert.That(installerRecord.ResolvedStageId, Is.EqualTo("stage-1-1"));
            Assert.That(installerRecord.SuppressCampaignFlow, Is.True);
            Assert.That(installerRecord.CampaignRuntimeActive, Is.False);

            Assert.That(host.WorldState, Is.Not.Null, "first gameplay state reached.");
            Assert.That(host.TickRunner.NextTickIndex, Is.GreaterThanOrEqualTo(1), "initial presentation refresh reached before manual tick smoke.");
            AssertSceneInstallerIntegrity(scenePath);
        }

        private static void AssertSceneInstallerIntegrity(string scenePath)
        {
            var installers = Object.FindObjectsByType<StageBackedGameplaySceneInstaller>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(installers, Has.Length.EqualTo(1), $"{scenePath} must have exactly one StageBackedGameplaySceneInstaller.");

#if UNITY_EDITOR
            Assert.That(CountMissingScripts(), Is.Zero, $"{scenePath} must not contain missing MonoBehaviour scripts.");
            var script = MonoScript.FromMonoBehaviour(installers[0]);
            Assert.That(script, Is.Not.Null, $"{scenePath} concrete installer script must resolve.");
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)),
                Is.EqualTo(StageBackedGameplaySceneInstallerGuid),
                "concrete StageBackedGameplaySceneInstaller script GUID must stay stable.");
            Assert.That(
                AssetDatabase.AssetPathToGUID(StageBackedGameplaySceneInstallerBasePath),
                Is.EqualTo(StageBackedGameplaySceneInstallerBaseGuid),
                "base StageBackedGameplaySceneInstallerBase script GUID must stay stable.");
#endif
        }

#if UNITY_EDITOR
        private static int CountMissingScripts()
        {
            var total = 0;
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                total += CountMissingScripts(root);
            }

            return total;
        }

        private static int CountMissingScripts(GameObject root)
        {
            var total = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            foreach (Transform child in root.transform)
            {
                total += CountMissingScripts(child.gameObject);
            }

            return total;
        }
#endif

        private static TickResult CreateTopologyTransitionTickResult(
            int tickIndex,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            IReadOnlyList<EntityState> finalEntities,
            IReadOnlyList<string> eventLog,
            string determinismHash)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                new TickTopologyMotion(sourceTopology, destinationTopology, rotationKind),
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());

            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetSerializedField(typeof(TickResult), result, "<PresentationData>k__BackingField", presentationData);
            SetSerializedField(typeof(TickResult), result, "<FinalTopology>k__BackingField", destinationTopology);
            SetSerializedField(typeof(TickResult), result, "<DeterminismHash>k__BackingField", determinismHash);
            SetSerializedField(typeof(TickResult), result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetSerializedField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities ?? Array.Empty<EntityState>())));
            SetSerializedField(
                typeof(TickResult),
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string>(eventLog ?? Array.Empty<string>())));
            return result;
        }

        private static TickResult CreateDamageDeathVfxTickResult(
            int tickIndex,
            CubeTopologyState topology,
            IReadOnlyList<EntityState> finalEntities,
            int enemyDamageEntityId,
            bool includeBoxDestroy)
        {
            var exits = new List<TickEntityExitPresentationSignal>();
            if (includeBoxDestroy)
            {
                exits.Add(new TickEntityExitPresentationSignal(
                    80,
                    TickEntityExitCause.BoxDestroy,
                    new SurfaceCell(FaceId.Floor, 2, 2),
                    topology,
                    Direction.Up,
                    EntityType.Box,
                    sourceActorEntityId: 10,
                    presentationSeed: 9080));
            }

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
                    new TickEnemyDamagePresentationSignal(
                        enemyDamageEntityId,
                        tookDamageThisTick: true,
                        damageAmount: 2),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                exits);

            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetSerializedField(typeof(TickResult), result, "<PresentationData>k__BackingField", presentationData);
            SetSerializedField(typeof(TickResult), result, "<FinalTopology>k__BackingField", topology);
            SetSerializedField(typeof(TickResult), result, "<DeterminismHash>k__BackingField", "DAMAGE-DEATH-VFX-PORT-WIRING");
            SetSerializedField(typeof(TickResult), result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetSerializedField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities ?? Array.Empty<EntityState>())));
            SetSerializedField(
                typeof(TickResult),
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string>(new[] { "DamageDeathVfxPortWiring|Injected" })));
            return result;
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private static IEnumerator LoadScene(string scenePath)
        {
#if UNITY_EDITOR
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
#endif
            Assert.That(operation, Is.Not.Null, $"Failed to start loading scene '{scenePath}'.");
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        private static void PrepareCampaignStage(StageId stageId)
        {
            var saveStore = new SaveSlotStore(
                EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            var activeSlot = new ActiveSlotProvider(
                EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            saveStore.ClearAll();
            activeSlot.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "level-01",
                RemainingChances = 2,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            activeSlot.SetActiveSlot(1);
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateCampaignTempSlot(
                    stageId,
                    remainingChances: 2));
        }

        private static IEnumerator BeginActualVictoryAndWaitForStageResult()
        {
            GameplayUiFlowInstaller uiInstaller = null;
            yield return BeginActualVictory(
                StageId.CreateOrThrow("stage-1-1"),
                installer => uiInstaller = installer);

            var deadline = Time.realtimeSinceStartup + 5f;
            while (TerminalSessionRegistry.IsActive &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                TerminalSessionRegistry.IsActive,
                Is.False,
                $"Same-scene result handoff did not complete. Trace: {FormatTrace(TerminalRuntimeTrace.Snapshot)}");
            Assert.That(uiInstaller.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            yield return new WaitForSecondsRealtime(0.1f);
        }

        private static IEnumerator BeginActualVictory(
            StageId stageId,
            Action<GameplayUiFlowInstaller> onStarted)
        {
            PrepareCampaignStage(stageId);
            yield return LoadScene(UIAudioScenePath);
            Assert.That(
                StageLaunchContextStore.TryPeek(out var directPlayBootstrapContext),
                Is.True,
                "Temp DirectPlay must own the scene-bootstrap launch context.");
            Assert.That(
                directPlayBootstrapContext.Source,
                Is.EqualTo("editor-direct-play"));
            Assert.That(
                StageLaunchContextStore.TryConsume(
                    directPlayBootstrapContext,
                    out var consumedBootstrapContext),
                Is.True,
                "Production campaign bootstrap consumes its exact launch context before result navigation.");
            Assert.That(consumedBootstrapContext, Is.EqualTo(directPlayBootstrapContext));

            var uiInstaller = Object.FindObjectsByType<GameplayUiFlowInstaller>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single();
            Assert.That(
                uiInstaller.TryForceClearCurrentStageForDiagnostics(out var forceClearMessage),
                Is.True,
                forceClearMessage);
            onStarted?.Invoke(uiInstaller);
        }

        private static void MoveViewToViewport(
            Camera camera,
            GameplayEntityView view,
            Vector2 targetViewport)
        {
            var bounds = CalculateRendererBounds(view);
            var currentViewport = camera.WorldToViewportPoint(bounds.center);
            Assert.That(currentViewport.z, Is.GreaterThan(0f));
            var targetWorld = camera.ViewportToWorldPoint(
                new Vector3(targetViewport.x, targetViewport.y, currentViewport.z));
            view.transform.position += targetWorld - bounds.center;
        }

        private static Vector2 ProjectRendererBoundsCenter(
            Camera camera,
            GameplayEntityView view)
        {
            var bounds = CalculateRendererBounds(view);
            var viewport = camera.WorldToViewportPoint(bounds.center);
            return new Vector2(viewport.x, viewport.y);
        }

        private static void MoveRendererEnvelopeToViewport(
            Camera camera,
            GameplayEntityView view,
            Vector2 targetViewport)
        {
            for (var iteration = 0; iteration < 4; iteration++)
            {
                var current = ProjectRendererEnvelopeCenter(camera, view);
                if (Vector2.Distance(current, targetViewport) <= 0.00001f)
                {
                    return;
                }

                var depth = camera.WorldToViewportPoint(
                    CalculateRendererBounds(view).center).z;
                Assert.That(depth, Is.GreaterThan(0f));
                var currentWorld = camera.ViewportToWorldPoint(
                    new Vector3(current.x, current.y, depth));
                var targetWorld = camera.ViewportToWorldPoint(
                    new Vector3(targetViewport.x, targetViewport.y, depth));
                view.transform.position += targetWorld - currentWorld;
            }
        }

        private static Vector2 ProjectRendererEnvelopeCenter(
            Camera camera,
            GameplayEntityView view)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var bounds = renderers[rendererIndex].bounds;
                var center = bounds.center;
                var extents = bounds.extents;
                for (var x = -1; x <= 1; x += 2)
                {
                    for (var y = -1; y <= 1; y += 2)
                    {
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var viewport = camera.WorldToViewportPoint(
                                center + Vector3.Scale(
                                    extents,
                                    new Vector3(x, y, z)));
                            minimum = Vector2.Min(minimum, viewport);
                            maximum = Vector2.Max(maximum, viewport);
                        }
                    }
                }
            }

            return (minimum + maximum) * 0.5f;
        }

        private static void QueueMouseState(
            Mouse mouse,
            Vector2 position,
            bool leftPressed)
        {
            var state = new MouseState { position = position };
            if (leftPressed)
            {
                state.WithButton(MouseButton.Left);
            }

            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static void QueueKeyboardEnterState(Keyboard keyboard, bool pressed)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                pressed
                    ? new KeyboardState(Key.Enter)
                    : new KeyboardState());
            InputSystem.Update();
        }

        private static Bounds CalculateRendererBounds(GameplayEntityView view)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void AssertPlayerBoundsInsideAperture(
            Camera camera,
            GameplayEntityView view,
            Vector2 apertureCenter,
            float apertureRadius)
        {
            var bounds = CalculateRendererBounds(view);
            var extents = bounds.extents;
            var aspect = camera.pixelHeight > 0
                ? (float)camera.pixelWidth / camera.pixelHeight
                : 1f;
            var maxDistance = 0f;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var world = bounds.center +
                                    Vector3.Scale(extents, new Vector3(x, y, z));
                        var viewport3 = camera.WorldToViewportPoint(world);
                        var delta = new Vector2(
                            (viewport3.x - apertureCenter.x) * aspect,
                            viewport3.y - apertureCenter.y);
                        maxDistance = Mathf.Max(maxDistance, delta.magnitude);
                    }
                }
            }

            Assert.That(maxDistance, Is.LessThanOrEqualTo(apertureRadius + 0.002f));
        }

        private static TerminalIrisContourAnalysis CaptureTransparentApertureContour(
            Canvas canvas,
            Camera camera,
            TerminalIrisOverlayView irisView,
            Color opaqueCover,
            Vector2 expectedCenter,
            string capturePath,
            out int renderWidth,
            out int renderHeight)
        {
            Assert.That(canvas, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            renderWidth = width;
            renderHeight = height;
            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                true);
            var previous = RenderTexture.active;
            var irisRect = (RectTransform)irisView.transform;
            var previousParent = irisRect.parent;
            var previousSiblingIndex = irisRect.GetSiblingIndex();
            var previousAnchorMin = irisRect.anchorMin;
            var previousAnchorMax = irisRect.anchorMax;
            var previousOffsetMin = irisRect.offsetMin;
            var previousOffsetMax = irisRect.offsetMax;
            var previousPivot = irisRect.pivot;
            var previousLocalRotation = irisRect.localRotation;
            var previousLocalScale = irisRect.localScale;
            var irisTransforms = irisRect.GetComponentsInChildren<Transform>(true);
            var previousLayers = irisTransforms
                .Select(item => item.gameObject.layer)
                .ToArray();
            const int evidenceLayer = 30;
            var evidenceRoot = new GameObject(
                "TerminalIrisContourEvidenceCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            evidenceRoot.layer = evidenceLayer;
            var evidenceCanvas = evidenceRoot.GetComponent<Canvas>();
            var evidenceCameraObject = new GameObject(
                "TerminalIrisContourEvidenceCamera",
                typeof(Camera));
            evidenceCameraObject.layer = evidenceLayer;
            var evidenceCamera = evidenceCameraObject.GetComponent<Camera>();
            try
            {
                Assert.That(target.Create(), Is.True);
                for (var index = 0; index < irisTransforms.Length; index++)
                {
                    irisTransforms[index].gameObject.layer = evidenceLayer;
                }

                evidenceCamera.clearFlags = CameraClearFlags.SolidColor;
                evidenceCamera.backgroundColor = Color.white;
                evidenceCamera.cullingMask = 1 << evidenceLayer;
                evidenceCamera.targetTexture = target;
                evidenceCamera.orthographic = true;
                evidenceCamera.transform.position = new Vector3(0f, 0f, -10f);

                evidenceCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                evidenceCanvas.worldCamera = evidenceCamera;
                evidenceCanvas.planeDistance = 1f;
                irisRect.SetParent(evidenceRoot.transform, false);
                irisRect.anchorMin = Vector2.zero;
                irisRect.anchorMax = Vector2.one;
                irisRect.offsetMin = Vector2.zero;
                irisRect.offsetMax = Vector2.zero;
                irisRect.pivot = new Vector2(0.5f, 0.5f);
                irisRect.localRotation = Quaternion.identity;
                irisRect.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                evidenceCamera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(capturePath, texture.EncodeToPNG());
                var pixels = texture.GetPixels32();
                var coverage = TerminalIrisEvidenceAnalyzer.ResolveCoverage(
                    pixels,
                    Color.white,
                    opaqueCover);
                return TerminalIrisEvidenceAnalyzer.AnalyzeCoverage(
                    coverage,
                    width,
                    height,
                    expectedCenter);
            }
            finally
            {
                irisRect.SetParent(previousParent, false);
                irisRect.SetSiblingIndex(previousSiblingIndex);
                irisRect.anchorMin = previousAnchorMin;
                irisRect.anchorMax = previousAnchorMax;
                irisRect.offsetMin = previousOffsetMin;
                irisRect.offsetMax = previousOffsetMax;
                irisRect.pivot = previousPivot;
                irisRect.localRotation = previousLocalRotation;
                irisRect.localScale = previousLocalScale;
                for (var index = 0; index < irisTransforms.Length; index++)
                {
                    if (irisTransforms[index] != null)
                    {
                        irisTransforms[index].gameObject.layer = previousLayers[index];
                    }
                }

                RenderTexture.active = previous;
                evidenceCamera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(evidenceCameraObject);
                Object.DestroyImmediate(evidenceRoot);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void CollectFocusEvidenceFailure(
            ICollection<string> failures,
            string identity,
            string contract,
            bool passed)
        {
            if (!passed)
            {
                failures.Add($"{identity}: {contract}");
            }
        }

        private static string ProductionFocusCsvRow(
            string evidenceBundleId,
            string evidenceRunId,
            string sampleId,
            string traceRowId,
            string direction,
            int run,
            TerminalSessionSnapshot session,
            TerminalFocusCaptureDiagnostics capture,
            TerminalFocusTransportDiagnostics transport,
            TerminalIrisOverlayView irisView,
            Vector2 expectedCenter,
            Vector2 materialCenter,
            TerminalIrisContourAnalysis contour,
            float centerErrorViewport,
            int renderWidth,
            int renderHeight,
            RenderTexture targetTexture,
            string artifactPath,
            string captureSha256,
            string verdict)
        {
            var camera = capture.OutputCamera;
            var pixelRect = camera != null ? camera.pixelRect : default;
            return string.Join(
                ",",
                Csv(evidenceBundleId),
                Csv(evidenceRunId),
                Csv(sampleId),
                Csv(traceRowId),
                Csv(direction),
                run.ToString(CultureInfo.InvariantCulture),
                Csv(session.Token.ToString()),
                session.TransitionId.ToString(CultureInfo.InvariantCulture),
                session.SourceSceneGeneration.ToString(CultureInfo.InvariantCulture),
                session.DestinationSceneGeneration.ToString(CultureInfo.InvariantCulture),
                capture.EntityId.ToString(CultureInfo.InvariantCulture),
                (capture.View != null ? capture.View.GetInstanceID() : 0)
                .ToString(CultureInfo.InvariantCulture),
                EvidenceFloat(capture.PlayerWorldPosition.x),
                EvidenceFloat(capture.PlayerWorldPosition.y),
                EvidenceFloat(capture.PlayerWorldPosition.z),
                EvidenceFloat(capture.PlayerViewPosition.x),
                EvidenceFloat(capture.PlayerViewPosition.y),
                EvidenceFloat(capture.PlayerViewPosition.z),
                EvidenceFloat(expectedCenter.x),
                EvidenceFloat(expectedCenter.y),
                (camera != null ? camera.GetInstanceID() : 0)
                .ToString(CultureInfo.InvariantCulture),
                Csv(camera != null ? camera.name : string.Empty),
                Csv(camera != null ? camera.cameraType.ToString() : string.Empty),
                Csv(
                    $"{EvidenceFloat(pixelRect.x)}:{EvidenceFloat(pixelRect.y)}:" +
                    $"{EvidenceFloat(pixelRect.width)}:{EvidenceFloat(pixelRect.height)}"),
                (targetTexture != null ? targetTexture.GetInstanceID() : 0)
                .ToString(CultureInfo.InvariantCulture),
                renderWidth.ToString(CultureInfo.InvariantCulture),
                renderHeight.ToString(CultureInfo.InvariantCulture),
                camera != null ? MatrixHash(camera.worldToCameraMatrix) : "0",
                camera != null ? MatrixHash(camera.projectionMatrix) : "0",
                EvidenceFloat(capture.RawWorldToViewportPoint.x),
                EvidenceFloat(capture.RawWorldToViewportPoint.y),
                EvidenceFloat(capture.RawWorldToViewportPoint.z),
                EvidenceFloat(capture.ProjectedCenter.x),
                EvidenceFloat(capture.ProjectedCenter.y),
                capture.Succeeded ? "true" : "false",
                capture.WasClamped ? "true" : "false",
                EvidenceFloat(capture.CapturedCenter.x),
                EvidenceFloat(capture.CapturedCenter.y),
                capture.FailureReason.ToString(),
                EvidenceFloat(transport.RequestCenter.x),
                EvidenceFloat(transport.RequestCenter.y),
                transport.RequestFocusValid ? "true" : "false",
                transport.FocusSource.ToString(),
                EvidenceFloat(transport.PlaybackSnapshotCenter.x),
                EvidenceFloat(transport.PlaybackSnapshotCenter.y),
                transport.PlaybackFocusValid ? "true" : "false",
                irisView.GetInstanceID().ToString(CultureInfo.InvariantCulture),
                EvidenceFloat(irisView.LastAppliedCenterForDiagnostics.x),
                EvidenceFloat(irisView.LastAppliedCenterForDiagnostics.y),
                irisView.RuntimeMaterialInstanceIdForDiagnostics
                    .ToString(CultureInfo.InvariantCulture),
                irisView.ImageMaterialInstanceIdForDiagnostics
                    .ToString(CultureInfo.InvariantCulture),
                irisView.MaterialForRenderingInstanceIdForDiagnostics
                    .ToString(CultureInfo.InvariantCulture),
                EvidenceFloat(materialCenter.x),
                EvidenceFloat(materialCenter.y),
                irisView.LastMaterialApplicationFrameForDiagnostics
                    .ToString(CultureInfo.InvariantCulture),
                irisView.LastVisibleRenderFrameForDiagnostics
                    .ToString(CultureInfo.InvariantCulture),
                Csv(
                    sampleId + "-render-" +
                    irisView.LastVisibleRenderFrameForDiagnostics.ToString(
                        CultureInfo.InvariantCulture)),
                Csv(artifactPath),
                captureSha256,
                EvidenceFloat(contour.FittedCenterViewport.x),
                EvidenceFloat(contour.FittedCenterViewport.y),
                EvidenceFloat(centerErrorViewport),
                EvidenceFloat(contour.CenterErrorPixels),
                EvidenceFloat(contour.FittedRadiusPixels),
                EvidenceFloat(contour.RmsRadialErrorPixels),
                EvidenceFloat(contour.MaximumRadialErrorPixels),
                EvidenceFloat(contour.P99RadialErrorPixels),
                contour.UnexpectedTransparentComponentCount
                    .ToString(CultureInfo.InvariantCulture),
                contour.OpaquePinholePixelCount.ToString(CultureInfo.InvariantCulture),
                contour.TransparentArtifactPixelCount.ToString(CultureInfo.InvariantCulture),
                verdict);
        }

        private static string ProductionFocusTraceCsvRow(
            string traceRowId,
            string evidenceBundleId,
            string evidenceRunId,
            string sampleId,
            TerminalFocusCaptureDiagnostics capture,
            TerminalFocusTransportDiagnostics transport,
            TerminalIrisOverlayView irisView,
            Vector2 materialCenter)
        {
            var camera = capture.OutputCamera;
            return string.Join(
                ",",
                Csv(traceRowId),
                Csv(evidenceBundleId),
                Csv(evidenceRunId),
                Csv(sampleId),
                EvidenceFloat(capture.ProjectedCenter.x),
                EvidenceFloat(capture.ProjectedCenter.y),
                EvidenceFloat(transport.RequestCenter.x),
                EvidenceFloat(transport.RequestCenter.y),
                EvidenceFloat(transport.PlaybackSnapshotCenter.x),
                EvidenceFloat(transport.PlaybackSnapshotCenter.y),
                EvidenceFloat(irisView.LastAppliedCenterForDiagnostics.x),
                EvidenceFloat(irisView.LastAppliedCenterForDiagnostics.y),
                EvidenceFloat(materialCenter.x),
                EvidenceFloat(materialCenter.y),
                irisView.GetInstanceID().ToString(CultureInfo.InvariantCulture),
                irisView.RuntimeMaterialInstanceIdForDiagnostics
                    .ToString(CultureInfo.InvariantCulture),
                (camera != null ? camera.GetInstanceID() : 0)
                    .ToString(CultureInfo.InvariantCulture),
                capture.Succeeded ? "true" : "false",
                capture.FailureReason.ToString());
        }

        private static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return string.Concat(
                    sha.ComputeHash(stream).Select(value => value.ToString("x2")));
            }
        }

        private static void WriteProductionFocusEnvironment(
            string outputDirectory,
            string renderTargets)
        {
            File.WriteAllText(
                Path.Combine(outputDirectory, "graphics-environment.json"),
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                $"  \"unityVersion\": \"{JsonEscape(Application.unityVersion)}\",\n" +
                $"  \"operatingSystem\": \"{JsonEscape(SystemInfo.operatingSystem)}\",\n" +
                $"  \"graphicsDeviceName\": \"{JsonEscape(SystemInfo.graphicsDeviceName)}\",\n" +
                $"  \"graphicsDeviceType\": \"{SystemInfo.graphicsDeviceType}\",\n" +
                $"  \"graphicsDeviceVersion\": \"{JsonEscape(SystemInfo.graphicsDeviceVersion)}\",\n" +
                $"  \"graphicsMemorySizeMB\": {SystemInfo.graphicsMemorySize},\n" +
                $"  \"screenResolution\": \"{Screen.width}x{Screen.height}\",\n" +
                $"  \"renderTargets\": \"{JsonEscape(renderTargets)}\",\n" +
                $"  \"targetFrameRate\": {Application.targetFrameRate},\n" +
                $"  \"colorSpace\": \"{QualitySettings.activeColorSpace}\"\n" +
                "}\n");
        }

        private static string JsonEscape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string ReadCommandLineValue(string key)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], key, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }

        private static string MatrixHash(Matrix4x4 matrix)
        {
            unchecked
            {
                var hash = 1469598103934665603UL;
                for (var row = 0; row < 4; row++)
                {
                    for (var column = 0; column < 4; column++)
                    {
                        hash ^= (uint)matrix[row, column].GetHashCode();
                        hash *= 1099511628211UL;
                    }
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        private static string EvidenceFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static void ConfigureCanvasForLinearCapture(
            Canvas canvas,
            Camera camera)
        {
            Assert.That(canvas, Is.Not.Null);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.targetDisplay = 0;
            Canvas.ForceUpdateCanvases();
        }

        private static Color[] CaptureLinearPixels(
            Camera camera,
            RenderTexture target)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            var texture = new Texture2D(
                target.width,
                target.height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(
                    new Rect(0f, 0f, target.width, target.height),
                    0,
                    0);
                texture.Apply();
                return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
            }
        }

        private static string WriteLinearCapture(
            Color[] pixels,
            int width,
            int height,
            string evidenceDirectory,
            string fileName)
        {
            Assert.That(pixels, Has.Length.EqualTo(width * height));
            Directory.CreateDirectory(evidenceDirectory);
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            try
            {
                texture.SetPixels(pixels);
                texture.Apply();
                var path = Path.Combine(evidenceDirectory, fileName);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return path;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static LinearFrameDelta CompareLinearFrames(
            IReadOnlyList<Color> left,
            IReadOnlyList<Color> right,
            int width,
            int height)
        {
            Assert.That(left.Count, Is.EqualTo(width * height));
            Assert.That(right.Count, Is.EqualTo(left.Count));
            var maxDelta = 0f;
            double totalDelta = 0d;
            var channelCount = 0;
            for (var i = 0; i < left.Count; i++)
            {
                var deltaR = Mathf.Abs(left[i].r - right[i].r);
                var deltaG = Mathf.Abs(left[i].g - right[i].g);
                var deltaB = Mathf.Abs(left[i].b - right[i].b);
                var deltaA = Mathf.Abs(left[i].a - right[i].a);
                maxDelta = Mathf.Max(
                    maxDelta,
                    Mathf.Max(
                        Mathf.Max(deltaR, deltaG),
                        Mathf.Max(deltaB, deltaA)));
                totalDelta += deltaR + deltaG + deltaB + deltaA;
                channelCount += 4;
            }

            return new LinearFrameDelta(
                maxDelta,
                channelCount > 0 ? (float)(totalDelta / channelCount) : 0f);
        }

        private static int CountUnexpectedBlackPixels(IReadOnlyList<Color> pixels)
        {
            var count = 0;
            for (var i = 0; i < pixels.Count; i++)
            {
                if (pixels[i].r <= 0.02f &&
                    pixels[i].g <= 0.02f &&
                    pixels[i].b <= 0.02f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountTransparentPixels(IReadOnlyList<Color> pixels)
        {
            var count = 0;
            for (var i = 0; i < pixels.Count; i++)
            {
                if (pixels[i].a < 0.999f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountChangedPixels(
            IReadOnlyList<Color> left,
            IReadOnlyList<Color> right)
        {
            Assert.That(right.Count, Is.EqualTo(left.Count));
            var count = 0;
            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPixelsExceedingRgbDelta(
            IReadOnlyList<Color> left,
            IReadOnlyList<Color> right,
            float tolerance)
        {
            Assert.That(right.Count, Is.EqualTo(left.Count));
            var count = 0;
            for (var i = 0; i < left.Count; i++)
            {
                if (Mathf.Abs(left[i].r - right[i].r) > tolerance ||
                    Mathf.Abs(left[i].g - right[i].g) > tolerance ||
                    Mathf.Abs(left[i].b - right[i].b) > tolerance)
                {
                    count++;
                }
            }

            return count;
        }

        private static LuminanceFrameDelta CompareLinearLuminanceFrames(
            IReadOnlyList<Color> left,
            IReadOnlyList<Color> right)
        {
            Assert.That(right.Count, Is.EqualTo(left.Count));
            var deltas = new float[left.Count];
            double total = 0d;
            for (var i = 0; i < left.Count; i++)
            {
                var leftLuminance =
                    left[i].r * 0.2126f +
                    left[i].g * 0.7152f +
                    left[i].b * 0.0722f;
                var rightLuminance =
                    right[i].r * 0.2126f +
                    right[i].g * 0.7152f +
                    right[i].b * 0.0722f;
                deltas[i] = Mathf.Abs(leftLuminance - rightLuminance);
                total += deltas[i];
            }

            Array.Sort(deltas);
            var p99Index = deltas.Length > 0
                ? Mathf.Clamp(Mathf.CeilToInt(deltas.Length * 0.99f) - 1, 0, deltas.Length - 1)
                : 0;
            return new LuminanceFrameDelta(
                deltas.Length > 0 ? (float)(total / deltas.Length) : 0f,
                deltas.Length > 0 ? deltas[p99Index] : 0f);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private readonly struct LinearFrameDelta
        {
            public LinearFrameDelta(float maxDelta, float meanDelta)
            {
                MaxDelta = maxDelta;
                MeanDelta = meanDelta;
            }

            public float MaxDelta { get; }
            public float MeanDelta { get; }
        }

        private readonly struct LuminanceFrameDelta
        {
            public LuminanceFrameDelta(float meanDelta, float p99Delta)
            {
                MeanDelta = meanDelta;
                P99Delta = p99Delta;
            }

            public float MeanDelta { get; }

            public float P99Delta { get; }
        }

        private static int FindTraceIndex(
            IReadOnlyList<TerminalTraceRecord> trace,
            TerminalTraceEvent traceEvent)
        {
            for (var i = 0; i < trace.Count; i++)
            {
                if (trace[i].Event == traceEvent)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string FormatTrace(IReadOnlyList<TerminalTraceRecord> trace)
        {
            return string.Join(
                " -> ",
                trace.Select(record =>
                    $"{record.Event}[{record.AuthorityPhase},accepted={record.Accepted},reason={record.Reason}]"));
        }

        private static bool HasGraphicsDevice()
        {
            return !Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(
                    argument,
                    "-nographics",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerator AssertProductionScreenBlackCoverage(string checkpoint)
        {
            Canvas.ForceUpdateCanvases();

            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                Assert.That(target.Create(), Is.True, checkpoint);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                const int gridStride = 16;
                var exposedPixelCount = 0;
                var sampledPixelCount = 0;
                for (var y = 0; y < height; y += gridStride)
                {
                    for (var x = 0; x < width; x += gridStride)
                    {
                        sampledPixelCount++;
                        if (!IsBlack(texture.GetPixel(x, y)))
                        {
                            exposedPixelCount++;
                        }
                    }
                }

                var representativePoints = new[]
                {
                    new Vector2Int(2, 2),
                    new Vector2Int(width - 3, 2),
                    new Vector2Int(2, height - 3),
                    new Vector2Int(width - 3, height - 3),
                    new Vector2Int(width / 2, height / 2),
                    new Vector2Int(width / 2, 2),
                    new Vector2Int(width / 2, height - 3),
                    new Vector2Int(2, height / 2),
                    new Vector2Int(width - 3, height / 2),
                    new Vector2Int(width / 4, height / 3),
                    new Vector2Int(width * 3 / 4, height * 2 / 3),
                };
                for (var i = 0; i < representativePoints.Length; i++)
                {
                    var point = representativePoints[i];
                    sampledPixelCount++;
                    if (!IsBlack(texture.GetPixel(
                            Mathf.Clamp(point.x, 0, width - 1),
                            Mathf.Clamp(point.y, 0, height - 1))))
                    {
                        exposedPixelCount++;
                    }
                }

                Assert.That(
                    exposedPixelCount,
                    Is.Zero,
                    $"{checkpoint}: gameplay exposure pixel count must be zero across " +
                    $"{sampledPixelCount} actual production screen samples.");
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }

            yield break;
        }

        private static void WriteProductionScreenPng(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var target = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32);
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false);
            var previous = RenderTexture.active;
            try
            {
                Assert.That(target.Create(), Is.True);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }
        }

        private static bool IsBlack(Color color)
        {
            return color.r <= 0.02f &&
                   color.g <= 0.02f &&
                   color.b <= 0.02f;
        }

        private static void AssertColorsEquivalent(
            Color actual,
            Color expected,
            string message)
        {
            const float tolerance = 0.0001f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), message);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), message);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), message);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance), message);
        }

        private static IEnumerator CleanupSceneRuntime()
        {
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    while (!unload.isDone)
                    {
                        yield return null;
                    }
                }
            }

            foreach (var root in Object.FindObjectsByType<GlobalAudioFlowRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            foreach (var root in Object.FindObjectsByType<AudioRuntimeRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            yield return null;
        }
    }
}
