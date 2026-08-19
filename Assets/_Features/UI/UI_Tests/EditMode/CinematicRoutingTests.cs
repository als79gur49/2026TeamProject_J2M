using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.UI.Tests
{
    public sealed class CinematicRoutingTests
    {
        [SetUp]
        public void SetUp()
        {
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            CinematicOpaqueHandoffRegistry.ResetForTests();
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                9204,
                nameof(CinematicRoutingTests));
        }

        [TearDown]
        public void TearDown()
        {
            CinematicOpaqueHandoffRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
        }

        [Test]
        public void Intro_Completed_RoutesAndMarksProgressOnce()
        {
            using var harness = new IntroHarness(nameof(Intro_Completed_RoutesAndMarksProgressOnce));

            harness.Router.Launch(harness.Request);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.Player.PlayIntroCallCount, Is.EqualTo(1));
            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            Assert.That(
                harness.Route.Requests[0].TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.CinematicToGameplay));
            Assert.That(harness.SaveStore.LoadSlot(1).IntroPlayed, Is.True);
        }

        [TestCase(CinematicPlaybackCompletionKind.Failed)]
        [TestCase(CinematicPlaybackCompletionKind.Cancelled)]
        public void Intro_FailureOrCancellation_ClearsHandoffWithoutRouting(
            CinematicPlaybackCompletionKind completionKind)
        {
            using var harness = new IntroHarness(
                nameof(Intro_FailureOrCancellation_ClearsHandoffWithoutRouting));

            harness.Router.Launch(harness.Request);
            harness.Player.EmitIntro(completionKind);

            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.Route.Requests, Is.Empty);
            Assert.That(harness.SaveStore.LoadSlot(1).IntroPlayed, Is.False);
        }

        [Test]
        public void Intro_StaleHandoff_RejectsCallbackAndReleasesOpaqueOwner()
        {
            using var harness = new IntroHarness(
                nameof(Intro_StaleHandoff_RejectsCallbackAndReleasesOpaqueOwner));
            harness.Router.Launch(harness.Request);
            var replacement = new CampaignLaunchHandoff(
                1,
                harness.Request.StageId,
                harness.Request.NavigationKind,
                "replacement-owner",
                Guid.NewGuid());
            harness.HandoffStore.ForcePending(replacement);

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.Route.Requests, Is.Empty);
            Assert.That(harness.SaveStore.LoadSlot(1).IntroPlayed, Is.False);
            Assert.That(harness.Player.ReleaseCancelledOwnerCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(replacement));
        }

        [Test]
        public void Intro_MissingContent_DelegatesImmediately()
        {
            using var harness = new IntroHarness(nameof(Intro_MissingContent_DelegatesImmediately));
            harness.Player.HasIntroContentValue = false;

            harness.Router.Launch(harness.Request);

            Assert.That(harness.Player.PlayIntroCallCount, Is.Zero);
            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            Assert.That(
                harness.Route.Requests[0].TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.GameplayEntry));
        }

        [Test]
        public void Intro_AlreadyPlayed_DelegatesImmediately()
        {
            using var harness = new IntroHarness(nameof(Intro_AlreadyPlayed_DelegatesImmediately));
            harness.SaveStore.UpdateSlot(1, slot => slot.IntroPlayed = true);

            harness.Router.Launch(harness.Request);

            Assert.That(harness.Player.PlayIntroCallCount, Is.Zero);
            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            Assert.That(
                harness.Route.Requests[0].TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.GameplayEntry));
        }

        [Test]
        public void Intro_RouteThrowsAfterCompletion_FailsClaimAndDoesNotMarkProgress()
        {
            using var harness = new IntroHarness(
                nameof(Intro_RouteThrowsAfterCompletion_FailsClaimAndDoesNotMarkProgress));
            harness.Route.ExceptionToThrow = new InvalidOperationException("intro route rejected");
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex(
                "intro route rejected"));

            harness.Router.Launch(harness.Request);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            Assert.That(harness.SaveStore.LoadSlot(1).IntroPlayed, Is.False);
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
        }

        [Test]
        public void Outro_Completed_RoutesAndMarksProgressOnce()
        {
            using var harness = new OutroHarness(nameof(Outro_Completed_RoutesAndMarksProgressOnce));

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);
            harness.Player.EmitOutro(CinematicPlaybackCompletionKind.Completed);
            harness.Player.EmitOutro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.Player.PlayOutroCallCount, Is.EqualTo(1));
            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(
                harness.Route.LastTransitionIntent,
                Is.EqualTo(SceneTransitionIntent.CinematicToMainMenu));
            Assert.That(harness.SaveStore.LoadSlot(1).OutroPlayed, Is.True);
        }

        [TestCase(CinematicPlaybackCompletionKind.Failed)]
        [TestCase(CinematicPlaybackCompletionKind.Cancelled)]
        public void Outro_FailureOrCancellation_DoesNotRouteOrMarkProgress(
            CinematicPlaybackCompletionKind completionKind)
        {
            using var harness = new OutroHarness(
                nameof(Outro_FailureOrCancellation_DoesNotRouteOrMarkProgress));

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);
            harness.Player.EmitOutro(completionKind);

            Assert.That(harness.Route.ReturnCallCount, Is.Zero);
            Assert.That(harness.SaveStore.LoadSlot(1).OutroPlayed, Is.False);
        }

        [Test]
        public void Outro_MissingContent_DelegatesImmediately()
        {
            using var harness = new OutroHarness(nameof(Outro_MissingContent_DelegatesImmediately));
            harness.Player.HasOutroContentValue = false;

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);

            Assert.That(harness.Player.PlayOutroCallCount, Is.Zero);
            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(
                harness.Route.LastTransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ReturnToMainMenu));
        }

        [Test]
        public void Outro_NonFinalClear_DelegatesImmediately()
        {
            using var harness = new OutroHarness(
                nameof(Outro_NonFinalClear_DelegatesImmediately),
                isFinalClearMainReturn: false);

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);

            Assert.That(harness.Player.PlayOutroCallCount, Is.Zero);
            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(
                harness.Route.LastTransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ReturnToMainMenu));
        }

        [Test]
        public void Outro_AlreadyPlayed_DelegatesImmediately()
        {
            using var harness = new OutroHarness(nameof(Outro_AlreadyPlayed_DelegatesImmediately));
            harness.SaveStore.UpdateSlot(1, slot => slot.OutroPlayed = true);

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);

            Assert.That(harness.Player.PlayOutroCallCount, Is.Zero);
            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(
                harness.Route.LastTransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ReturnToMainMenu));
        }

        [Test]
        public void Outro_RouteThrowsAfterCompletion_FailsClaimAndDoesNotMarkProgress()
        {
            using var harness = new OutroHarness(
                nameof(Outro_RouteThrowsAfterCompletion_FailsClaimAndDoesNotMarkProgress));
            harness.Route.ExceptionToThrow = new InvalidOperationException("outro route rejected");
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex(
                "outro route rejected"));

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);
            harness.Player.EmitOutro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(harness.SaveStore.LoadSlot(1).OutroPlayed, Is.False);
            Assert.That(MainMenuEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
        }

        [Test]
        public void RuntimeComposition_HasNoRetiredVideoCinematicDependencies()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var retiredPaths = new[]
            {
                "Assets/3DM/VQ 인트로.mp4",
                "Assets/3DM/VQ 아웃트로.mp4",
                "Assets/_Features/UI/UI_Composition/Authoring/SlotCinematicDefinition_CampaignMain.asset",
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicVideoOverlayView.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/SlotCinematicDefinition.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicFlowCoordinator.cs",
            };

            foreach (var path in retiredPaths)
            {
                Assert.That(File.Exists(Path.Combine(projectRoot, path)), Is.False, path);
            }

            var runtimeRoot = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Features/UI/UI_Composition/Runtime");
            var retiredNamespace = "UnityEngine." + "Video";
            foreach (var path in Directory.GetFiles(runtimeRoot, "*.cs"))
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain(retiredNamespace), path);
            }

            var compositionAsmdef = File.ReadAllText(Path.Combine(
                UnityEngine.Application.dataPath,
                "_Features/UI/UI_Composition/UI.Composition.asmdef"));
            Assert.That(compositionAsmdef, Does.Not.Contain("VideoModule"));
        }

        private static SaveSlotData CreateSlot(int slotNumber, StageId stageId)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "test-group",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
            };
        }

        private readonly struct TestKeys
        {
            private TestKeys(string saveKey, string activeKey)
            {
                SaveKey = saveKey;
                ActiveKey = activeKey;
            }

            public string SaveKey { get; }
            public string ActiveKey { get; }

            public static TestKeys Create(string suffix)
            {
                var prefix =
                    "Game.Feature.UI.Tests.CinematicRouting." + suffix + "." +
                    Guid.NewGuid().ToString("N");
                return new TestKeys(prefix + ".Save", prefix + ".Active");
            }

            public void Clear()
            {
                PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.DeleteKey(ActiveKey);
                PlayerPrefs.Save();
            }
        }

        private sealed class ManualCinematicPlayer :
            ICinematicSequencePlayer,
            ICinematicOpaqueHandoffCancellationOwner
        {
            private Action<CinematicPlaybackCompletion> _introCompletion;
            private Action<CinematicPlaybackCompletion> _outroCompletion;

            public bool HasIntroContentValue { get; set; } = true;
            public bool HasOutroContentValue { get; set; } = true;
            public bool HasIntroContent => HasIntroContentValue;
            public bool HasOutroContent => HasOutroContentValue;
            public bool IsPlaying { get; private set; }
            public int PlayIntroCallCount { get; private set; }
            public int PlayOutroCallCount { get; private set; }
            public int ReleaseCancelledOwnerCount { get; private set; }

            public void PlayIntro(Action<CinematicPlaybackCompletion> completion)
            {
                PlayIntroCallCount++;
                IsPlaying = true;
                _introCompletion = completion;
            }

            public void PlayOutro(Action<CinematicPlaybackCompletion> completion)
            {
                PlayOutroCallCount++;
                IsPlaying = true;
                _outroCompletion = completion;
            }

            public void EmitIntro(CinematicPlaybackCompletionKind kind)
            {
                IsPlaying = false;
                _introCompletion?.Invoke(new CinematicPlaybackCompletion(kind));
            }

            public void EmitOutro(CinematicPlaybackCompletionKind kind)
            {
                IsPlaying = false;
                _outroCompletion?.Invoke(new CinematicPlaybackCompletion(kind));
            }

            public bool TryReleaseCancelledIntroOpaqueOwner()
            {
                ReleaseCancelledOwnerCount++;
                return true;
            }
        }

        private sealed class IntroHarness : IDisposable
        {
            private readonly TestKeys _keys;

            public IntroHarness(string testName)
            {
                _keys = TestKeys.Create(testName);
                SaveStore = new SaveSlotStore(_keys.SaveKey);
                HandoffStore = new TestHandoffStore();
                Route = new RecordingStageLaunchRouter();
                Player = new ManualCinematicPlayer();
                Request = new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "main-menu-new-game",
                    StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay),
                    SceneTransitionIntent.GameplayEntry);
                SaveStore.SaveSlot(CreateSlot(1, Request.StageId));
                Assert.That(
                    HandoffStore.TryBegin(
                        1,
                        Request.StageId,
                        Request.NavigationKind,
                        Request.Source,
                        out _),
                    Is.True);
                Router = new CinematicStageLaunchRouter(
                    Route,
                    SaveStore,
                    HandoffStore,
                    Player);
            }

            public SaveSlotStore SaveStore { get; }
            public TestHandoffStore HandoffStore { get; }
            public RecordingStageLaunchRouter Route { get; }
            public ManualCinematicPlayer Player { get; }
            public StageNavigationRequest Request { get; }
            public CinematicStageLaunchRouter Router { get; }

            public void Dispose()
            {
                _keys.Clear();
            }
        }

        private sealed class OutroHarness : IDisposable
        {
            private readonly TestKeys _keys;

            public OutroHarness(string testName, bool isFinalClearMainReturn = true)
            {
                _keys = TestKeys.Create(testName);
                SaveStore = new SaveSlotStore(_keys.SaveKey);
                var activeSlot = new ActiveSlotProvider(_keys.ActiveKey);
                SaveStore.SaveSlot(CreateSlot(1, StageId.CreateOrThrow("stage-4-1")));
                activeSlot.SetActiveSlot(1);
                Route = new RecordingMainMenuReturnRouter();
                Player = new ManualCinematicPlayer();
                Router = new CinematicMainMenuReturnRouter(
                    Route,
                    SaveStore,
                    activeSlot,
                    Player,
                    () => isFinalClearMainReturn);
            }

            public SaveSlotStore SaveStore { get; }
            public RecordingMainMenuReturnRouter Route { get; }
            public ManualCinematicPlayer Player { get; }
            public CinematicMainMenuReturnRouter Router { get; }

            public void Dispose()
            {
                _keys.Clear();
            }
        }

        private sealed class TestHandoffStore : ICampaignLaunchHandoffStore
        {
            private CampaignLaunchHandoff _pending;

            public bool TryBegin(
                int slotNumber,
                StageId stageId,
                StageNavigationKind navigationKind,
                string source,
                out CampaignLaunchHandoff handoff)
            {
                if (_pending != null)
                {
                    handoff = _pending;
                    return false;
                }

                _pending = new CampaignLaunchHandoff(
                    slotNumber,
                    stageId,
                    navigationKind,
                    source,
                    Guid.NewGuid());
                handoff = _pending;
                return true;
            }

            public bool TryPeek(out CampaignLaunchHandoff handoff)
            {
                handoff = _pending;
                return handoff != null;
            }

            public bool TryClear(Guid token)
            {
                if (_pending == null || _pending.Token != token)
                {
                    return false;
                }

                _pending = null;
                return true;
            }

            public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
            {
                if (_pending == null || _pending.Token != token)
                {
                    handoff = null;
                    return false;
                }

                handoff = _pending;
                _pending = null;
                return true;
            }

            public void ForcePending(CampaignLaunchHandoff handoff)
            {
                _pending = handoff;
            }
        }

        private sealed class RecordingStageLaunchRouter : IStageLaunchRouter
        {
            public List<StageNavigationRequest> Requests { get; } = new();
            public Exception ExceptionToThrow { get; set; }

            public void Launch(StageNavigationRequest request)
            {
                Requests.Add(request);
                if (ExceptionToThrow != null)
                {
                    throw ExceptionToThrow;
                }
            }
        }

        private sealed class RecordingMainMenuReturnRouter : IMainMenuReturnRouter
        {
            public int ReturnCallCount { get; private set; }
            public SceneTransitionIntent LastTransitionIntent { get; private set; }
            public Exception ExceptionToThrow { get; set; }

            public void ReturnToMainMenu(SceneTransitionIntent transitionIntent)
            {
                ReturnCallCount++;
                LastTransitionIntent = transitionIntent;
                if (ExceptionToThrow != null)
                {
                    throw ExceptionToThrow;
                }
            }
        }
    }
}
