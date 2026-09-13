using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.UI.Tests
{
    public sealed class ComicIntroOutroRoutingTests
    {
        [SetUp]
        public void SetUp()
        {
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            ComicSequenceOpaqueHandoffRegistry.ResetForTests();
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                9204,
                nameof(ComicIntroOutroRoutingTests));
        }

        [TearDown]
        public void TearDown()
        {
            ComicSequenceOpaqueHandoffRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
        }

        [Test]
        public void Intro_Completed_RoutesAndMarksProgressOnce()
        {
            using var harness = new IntroHarness(nameof(Intro_Completed_RoutesAndMarksProgressOnce));

            harness.Router.Launch(harness.Request);
            harness.ComicFlow.EmitIntro(ComicSequenceResultKind.Completed);
            harness.ComicFlow.EmitIntro(ComicSequenceResultKind.Completed);

            Assert.That(harness.ComicFlow.PresentIntroCallCount, Is.EqualTo(1));
            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            Assert.That(
                harness.Route.Requests[0].TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ComicIntroToGameplay));
            Assert.That(harness.SaveStore.LoadSlot(1).IntroComicCompleted, Is.True);
            Assert.That(
                harness.ComicFlow.CommitAudioFocusToTransitionCount,
                Is.EqualTo(1));
        }

        [TestCase(ComicSequenceResultKind.Failed)]
        [TestCase(ComicSequenceResultKind.Cancelled)]
        public void Intro_FailureOrCancellation_ClearsHandoffWithoutRouting(
            ComicSequenceResultKind completionKind)
        {
            using var harness = new IntroHarness(
                nameof(Intro_FailureOrCancellation_ClearsHandoffWithoutRouting));

            harness.Router.Launch(harness.Request);
            harness.ComicFlow.EmitIntro(completionKind);

            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.Route.Requests, Is.Empty);
            Assert.That(harness.SaveStore.LoadSlot(1).IntroComicCompleted, Is.False);
            Assert.That(
                harness.ComicFlow.CommitAudioFocusToTransitionCount,
                Is.Zero);
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

            harness.ComicFlow.EmitIntro(ComicSequenceResultKind.Completed);

            Assert.That(harness.Route.Requests, Is.Empty);
            Assert.That(harness.SaveStore.LoadSlot(1).IntroComicCompleted, Is.False);
            Assert.That(harness.ComicFlow.ReleaseCancelledOwnerCount, Is.EqualTo(1));
            Assert.That(
                harness.ComicFlow.CommitAudioFocusToTransitionCount,
                Is.Zero);
            Assert.That(harness.HandoffStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(replacement));
        }

        [Test]
        public void Intro_MissingContent_DelegatesImmediately()
        {
            using var harness = new IntroHarness(nameof(Intro_MissingContent_DelegatesImmediately));
            harness.ComicFlow.HasIntroSequenceValue = false;

            harness.Router.Launch(harness.Request);

            Assert.That(harness.ComicFlow.PresentIntroCallCount, Is.Zero);
            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            Assert.That(
                harness.Route.Requests[0].TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.GameplayEntry));
        }

        [Test]
        public void Intro_AlreadyCompleted_DelegatesImmediately()
        {
            using var harness = new IntroHarness(nameof(Intro_AlreadyCompleted_DelegatesImmediately));
            harness.SaveStore.MarkIntroComicCompleted(1);

            harness.Router.Launch(harness.Request);

            Assert.That(harness.ComicFlow.PresentIntroCallCount, Is.Zero);
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
            harness.ComicFlow.EmitIntro(ComicSequenceResultKind.Completed);

            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            Assert.That(harness.SaveStore.LoadSlot(1).IntroComicCompleted, Is.False);
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
            Assert.That(
                harness.ComicFlow.CommitAudioFocusToTransitionCount,
                Is.Zero);
        }

        [Test]
        public void Outro_Completed_RoutesAndMarksProgressOnce()
        {
            using var harness = new OutroHarness(nameof(Outro_Completed_RoutesAndMarksProgressOnce));

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);
            harness.ComicFlow.EmitOutro(ComicSequenceResultKind.Completed);
            harness.ComicFlow.EmitOutro(ComicSequenceResultKind.Completed);

            Assert.That(harness.ComicFlow.PresentOutroCallCount, Is.EqualTo(1));
            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(
                harness.Route.LastTransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ComicOutroToMainMenu));
            Assert.That(harness.SaveStore.LoadSlot(1).OutroComicCompleted, Is.True);
            Assert.That(
                harness.ComicFlow.CommitAudioFocusToTransitionCount,
                Is.EqualTo(1));
        }

        [TestCase(ComicSequenceResultKind.Failed)]
        [TestCase(ComicSequenceResultKind.Cancelled)]
        public void Outro_FailureOrCancellation_DoesNotRouteOrMarkProgress(
            ComicSequenceResultKind completionKind)
        {
            using var harness = new OutroHarness(
                nameof(Outro_FailureOrCancellation_DoesNotRouteOrMarkProgress));

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);
            harness.ComicFlow.EmitOutro(completionKind);

            Assert.That(harness.Route.ReturnCallCount, Is.Zero);
            Assert.That(harness.SaveStore.LoadSlot(1).OutroComicCompleted, Is.False);
            Assert.That(
                harness.ComicFlow.CommitAudioFocusToTransitionCount,
                Is.Zero);
        }

        [Test]
        public void Outro_MissingContent_DelegatesImmediately()
        {
            using var harness = new OutroHarness(nameof(Outro_MissingContent_DelegatesImmediately));
            harness.ComicFlow.HasOutroSequenceValue = false;

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);

            Assert.That(harness.ComicFlow.PresentOutroCallCount, Is.Zero);
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

            Assert.That(harness.ComicFlow.PresentOutroCallCount, Is.Zero);
            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(
                harness.Route.LastTransitionIntent,
                Is.EqualTo(SceneTransitionIntent.ReturnToMainMenu));
        }

        [Test]
        public void Outro_AlreadyCompleted_DelegatesImmediately()
        {
            using var harness = new OutroHarness(nameof(Outro_AlreadyCompleted_DelegatesImmediately));
            harness.SaveStore.MarkOutroComicCompleted(1);

            harness.Router.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);

            Assert.That(harness.ComicFlow.PresentOutroCallCount, Is.Zero);
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
            harness.ComicFlow.EmitOutro(ComicSequenceResultKind.Completed);

            Assert.That(harness.Route.ReturnCallCount, Is.EqualTo(1));
            Assert.That(harness.SaveStore.LoadSlot(1).OutroComicCompleted, Is.False);
            Assert.That(MainMenuEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
            Assert.That(
                harness.ComicFlow.CommitAudioFocusToTransitionCount,
                Is.Zero);
        }

        private static CampaignSlotSeedImportRequest CreateSlot(
            int slotNumber,
            StageId stageId)
        {
            return new CampaignSlotSeedImportRequest(
                slotNumber,
                stageId,
                "test-group",
                CampaignSaveSlotPolicy.DefaultRemainingChances,
                string.Empty);
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
                    "Game.Feature.UI.Tests.ComicIntroOutroRouting." + suffix + "." +
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

        private sealed class ManualComicFlow :
            IComicIntroOutroFlow,
            IComicSequenceOpaqueHandoffCancellationOwner,
            IComicSequenceTransitionAudioHandoffOwner
        {
            private Action<ComicSequenceResult> _introCompletion;
            private Action<ComicSequenceResult> _outroCompletion;

            public bool HasIntroSequenceValue { get; set; } = true;
            public bool HasOutroSequenceValue { get; set; } = true;
            public bool HasIntroSequence => HasIntroSequenceValue;
            public bool HasOutroSequence => HasOutroSequenceValue;
            public bool IsPresenting { get; private set; }
            public int PresentIntroCallCount { get; private set; }
            public int PresentOutroCallCount { get; private set; }
            public int ReleaseCancelledOwnerCount { get; private set; }
            public int CommitAudioFocusToTransitionCount { get; private set; }

            public void PresentIntro(Action<ComicSequenceResult> completion)
            {
                PresentIntroCallCount++;
                IsPresenting = true;
                _introCompletion = completion;
            }

            public void PresentOutro(Action<ComicSequenceResult> completion)
            {
                PresentOutroCallCount++;
                IsPresenting = true;
                _outroCompletion = completion;
            }

            public void EmitIntro(ComicSequenceResultKind kind)
            {
                IsPresenting = false;
                _introCompletion?.Invoke(new ComicSequenceResult(kind));
            }

            public void EmitOutro(ComicSequenceResultKind kind)
            {
                IsPresenting = false;
                _outroCompletion?.Invoke(new ComicSequenceResult(kind));
            }

            public bool TryReleaseCancelledIntroOpaqueOwner()
            {
                ReleaseCancelledOwnerCount++;
                return true;
            }

            public void CommitAudioFocusToTransition()
            {
                CommitAudioFocusToTransitionCount++;
            }
        }

        private sealed class IntroHarness : IDisposable
        {
            private readonly TestKeys _keys;

            public IntroHarness(string testName)
            {
                _keys = TestKeys.Create(testName);
                SaveStore = new TransientCampaignSaveSlotStore(_keys.SaveKey);
                HandoffStore = new TestHandoffStore();
                Route = new RecordingStageLaunchRouter();
                ComicFlow = new ManualComicFlow();
                Request = new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "main-menu-new-game",
                    StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay),
                    SceneTransitionIntent.GameplayEntry);
                SaveStore.ImportSlotSeed(CreateSlot(1, Request.StageId));
                Assert.That(
                    HandoffStore.TryBegin(
                        1,
                        Request.StageId,
                        Request.NavigationKind,
                        Request.Source,
                        out _),
                    Is.True);
                Router = new ComicIntroStageLaunchRouter(
                    Route,
                    SaveStore,
                    SaveStore,
                    HandoffStore,
                    ComicFlow);
            }

            public TransientCampaignSaveSlotStore SaveStore { get; }
            public TestHandoffStore HandoffStore { get; }
            public RecordingStageLaunchRouter Route { get; }
            public ManualComicFlow ComicFlow { get; }
            public StageNavigationRequest Request { get; }
            public ComicIntroStageLaunchRouter Router { get; }

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
                SaveStore = new TransientCampaignSaveSlotStore(_keys.SaveKey);
                var activeSlot = new ActiveSlotProvider(new TransientActiveSlotStorage(_keys.ActiveKey));
                SaveStore.ImportSlotSeed(CreateSlot(
                    1,
                    StageId.CreateOrThrow("stage-4-1")));
                activeSlot.SetActiveSlot(1);
                Route = new RecordingMainMenuReturnRouter();
                ComicFlow = new ManualComicFlow();
                Router = new ComicOutroMainMenuReturnRouter(
                    Route,
                    SaveStore,
                    SaveStore,
                    activeSlot,
                    ComicFlow,
                    () => isFinalClearMainReturn);
            }

            public TransientCampaignSaveSlotStore SaveStore { get; }
            public RecordingMainMenuReturnRouter Route { get; }
            public ManualComicFlow ComicFlow { get; }
            public ComicOutroMainMenuReturnRouter Router { get; }

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
