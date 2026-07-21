using System;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageLaunchContextOwnershipTests
    {
        [SetUp]
        public void SetUp()
        {
            StageLaunchContextStore.ResetForTests();
            CampaignLaunchHandoffSessionStore.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.ResetForTests();
            EditorDirectPlayContextStore.Clear();
            CampaignLaunchHandoffSessionStore.ResetForTests();
        }

        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.NonCampaign)]
        public void DirectPlayPrime_SurvivesSubsystemRegistration(EditorDirectPlayMode mode)
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            EditorDirectPlayContextStore.SetCurrent(CreateDirectPlayContext(mode, stageId));
            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);

            StageLaunchContextStore.ResetRuntimeStateForTests();

            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var primedStageId), Is.True);
            Assert.That(primedStageId, Is.EqualTo(stageId));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode, Is.EqualTo(mode));
        }

        [Test]
        public void RuntimeStageLaunchContext_IsClearedBySubsystemRegistration()
        {
            var context = CreateContext(
                Guid.NewGuid(),
                1,
                "stage-0-1",
                StageNavigationKind.Continue,
                "runtime-reset");
            Assert.That(StageLaunchContextStore.TrySetCurrent(context), Is.True);

            StageLaunchContextStore.ResetRuntimeStateForTests();

            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        public void ExplicitDirectPlayCleanup_ClearsPrime()
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);

            StageLaunchContextStore.Clear();

            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
        }

        [Test]
        public void ConsumedDirectPlayPrime_DoesNotPersistAfterUse()
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);

            Assert.That(StageLaunchContextStore.TryGetCurrent(out var consumedStageId), Is.True);

            Assert.That(consumedStageId, Is.EqualTo(stageId));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
            Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(stageId));
        }

        [Test]
        public void DirectPlayCleanup_DoesNotClearNormalPendingUnlessExplicitPolicy()
        {
            var pending = BeginHandoff(
                1,
                "stage-0-1",
                StageNavigationKind.Continue,
                "normal-pending");
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateNonCampaign(pending.StageId));
            StageLaunchContextStore.PrimePendingEditorDirectPlay(pending.StageId);

            EditorDirectPlayContextStore.Clear();
            StageLaunchContextStore.Clear();

            Assert.That(CampaignLaunchHandoffSessionStore.Instance.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(pending));
        }

        [Test]
        public void ExplicitEditorCleanup_DoesNotClearNormalRuntimeOwnerUnlessRequested()
        {
            var runtimeContext = CreateContext(
                Guid.NewGuid(),
                1,
                "stage-0-1",
                StageNavigationKind.Continue,
                "runtime-owner");
            Assert.That(StageLaunchContextStore.TrySetCurrent(runtimeContext), Is.True);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateNonCampaign(runtimeContext.StageId));

            EditorDirectPlayContextStore.Clear();

            Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(runtimeContext));
        }

        [Test]
        public void DirectPlayDuplicateGuard_PlayModeEntry_PreservesFirstPrimeAndContext()
        {
            var firstStage = StageId.CreateOrThrow("stage-0-1");
            var firstContext = EditorDirectPlayContext.CreateNonCampaign(firstStage);
            EditorDirectPlayContextStore.SetCurrent(firstContext);
            StageLaunchContextStore.PrimePendingEditorDirectPlay(firstStage);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                StageEditorDirectPlayLauncher.ThrowIfLaunchIsAlreadyInProgress(
                    isPlaying: false,
                    isPlayingOrWillChangePlaymode: true));

            Assert.That(exception?.Message, Does.Contain("rejected"));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().StageId, Is.EqualTo(firstStage));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var primedStage), Is.True);
            Assert.That(primedStage, Is.EqualTo(firstStage));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void DirectPlayDuplicateGuard_PlayModeState_RejectsWithoutCreatingLaunchState(
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                StageEditorDirectPlayLauncher.ThrowIfLaunchIsAlreadyInProgress(
                    isPlaying,
                    isPlayingOrWillChangePlaymode));

            Assert.That(exception?.Message, Does.Contain("rejected"));
            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
            Assert.That(
                EditorDirectPlayContextStore.GetCurrentOrNone().Mode,
                Is.EqualTo(EditorDirectPlayMode.None));
        }

        [Test]
        public void DirectPlayDuplicateGuard_TransitionOwner_RejectsBeforeSecondSceneLoadAndPreservesOwner()
        {
            var firstStage = StageId.CreateOrThrow("stage-0-1");
            var firstContext = new StageLaunchContext(
                Guid.NewGuid(),
                0,
                firstStage,
                StageNavigationKind.Continue,
                "editor-direct-play");
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(firstStage));
            Assert.That(StageLaunchContextStore.TrySetCurrent(firstContext), Is.True);
            var sceneBeforeDuplicate = SceneManager.GetActiveScene();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                StageEditorDirectPlayLauncher.LaunchStage(
                    StageId.CreateOrThrow("stage-1-1"),
                    EditorDirectPlayMode.NonCampaign,
                    SaveSlotStore.DefaultRemainingChances));

            Assert.That(exception?.Message, Does.Contain("rejected"));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneBeforeDuplicate.handle));
            Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(firstContext));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().StageId, Is.EqualTo(firstStage));
        }

        [Test]
        public void StageLaunchContext_CarriesFullHandoffIdentity()
        {
            var handoff = BeginHandoff(1, "stage-0-1", StageNavigationKind.Continue, "main-menu");

            var context = StageLaunchContext.FromHandoff(handoff);

            Assert.That(context.Token, Is.EqualTo(handoff.Token));
            Assert.That(context.SlotNumber, Is.EqualTo(1));
            Assert.That(context.StageId, Is.EqualTo(handoff.StageId));
            Assert.That(context.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
            Assert.That(context.Source, Is.EqualTo("main-menu"));
            Assert.That(context.Matches(handoff), Is.True);
        }

        [Test]
        public void StageLaunchContext_DoesNotAllowSilentOverwrite()
        {
            var first = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "first");
            var second = CreateContext(Guid.NewGuid(), 2, "stage-0-2", StageNavigationKind.Continue, "second");

            Assert.That(StageLaunchContextStore.TrySetCurrent(first), Is.True);
            Assert.That(StageLaunchContextStore.TrySetCurrent(second), Is.False);
            Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(first));
        }

        [Test]
        public void TrySetCurrent_SameExactContext_IsRejected()
        {
            var context = CreateContext(
                Guid.NewGuid(),
                1,
                "stage-0-1",
                StageNavigationKind.Continue,
                "same-exact");

            Assert.That(StageLaunchContextStore.TrySetCurrent(context), Is.True);
            Assert.That(StageLaunchContextStore.TrySetCurrent(context), Is.False);
            Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(context));
        }

        [Test]
        public void TrySetCurrent_DifferentContext_IsRejected()
        {
            var first = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "first");
            var second = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "second");

            Assert.That(StageLaunchContextStore.TrySetCurrent(first), Is.True);
            Assert.That(StageLaunchContextStore.TrySetCurrent(second), Is.False);
            Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(first));
        }

        [Test]
        public void SameStageDifferentToken_IsDifferentContext()
        {
            var first = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "same");
            var second = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "same");

            Assert.That(first.Equals(second), Is.False);
            Assert.That(StageLaunchContextStore.TrySetCurrent(first), Is.True);
            Assert.That(StageLaunchContextStore.IsCurrent(second), Is.False);
        }

        [Test]
        public void SameTokenDifferentMetadata_IsRejected()
        {
            var token = Guid.NewGuid();
            var first = CreateContext(token, 1, "stage-0-1", StageNavigationKind.Continue, "same");
            var changedSlot = CreateContext(token, 2, "stage-0-1", StageNavigationKind.Continue, "same");
            var changedStage = CreateContext(token, 1, "stage-0-2", StageNavigationKind.Continue, "same");
            var changedNavigation = CreateContext(token, 1, "stage-0-1", StageNavigationKind.Retry, "same");
            var changedSource = CreateContext(token, 1, "stage-0-1", StageNavigationKind.Continue, "changed");

            Assert.That(StageLaunchContextStore.TrySetCurrent(first), Is.True);
            Assert.That(StageLaunchContextStore.IsCurrent(changedSlot), Is.False);
            Assert.That(StageLaunchContextStore.IsCurrent(changedStage), Is.False);
            Assert.That(StageLaunchContextStore.IsCurrent(changedNavigation), Is.False);
            Assert.That(StageLaunchContextStore.IsCurrent(changedSource), Is.False);
        }

        [Test]
        public void TryClear_WrongContextDoesNotClearCurrent()
        {
            var current = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "current");
            var wrong = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "current");
            StageLaunchContextStore.TrySetCurrent(current);

            Assert.That(StageLaunchContextStore.TryClear(wrong), Is.False);
            Assert.That(StageLaunchContextStore.IsCurrent(current), Is.True);
        }

        [Test]
        public void TryConsume_WrongContextDoesNotConsumeCurrent()
        {
            var current = CreateContext(Guid.NewGuid(), 1, "stage-0-1", StageNavigationKind.Continue, "current");
            var wrong = CreateContext(current.Token, 2, "stage-0-1", StageNavigationKind.Continue, "current");
            StageLaunchContextStore.TrySetCurrent(current);

            Assert.That(StageLaunchContextStore.TryConsume(wrong, out _), Is.False);
            Assert.That(StageLaunchContextStore.IsCurrent(current), Is.True);
        }

        [Test]
        public void AsyncLoadFailure_ClearsOnlyMatchingContextAndPending()
        {
            var store = new RecordingHandoffStore();
            Assert.That(store.TryBegin(1, StageId.CreateOrThrow("stage-0-1"), StageNavigationKind.Continue, "async", out var handoff), Is.True);
            var context = StageLaunchContext.FromHandoff(handoff);
            StageLaunchContextStore.TrySetCurrent(context);
            var callbacks = new StageLoadCallbackOwnership(context, handoff, store);

            Assert.That(callbacks.CompleteFailure(new InvalidOperationException("failed")), Is.True);
            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            Assert.That(store.TryPeek(out _), Is.False);
            Assert.That(store.ClearCount, Is.EqualTo(1));
        }

        [Test]
        public void LateLoadFailure_DoesNotClearNewerContextOrPending()
        {
            var store = new RecordingHandoffStore();
            store.TryBegin(1, StageId.CreateOrThrow("stage-0-1"), StageNavigationKind.Continue, "old", out var oldHandoff);
            var oldContext = StageLaunchContext.FromHandoff(oldHandoff);
            var callbacks = new StageLoadCallbackOwnership(oldContext, oldHandoff, store);
            store.TryClear(oldHandoff.Token);
            StageLaunchContextStore.TrySetCurrent(oldContext);
            StageLaunchContextStore.TryClear(oldContext);

            store.TryBegin(2, StageId.CreateOrThrow("stage-0-1"), StageNavigationKind.Continue, "new", out var newHandoff);
            var newContext = StageLaunchContext.FromHandoff(newHandoff);
            StageLaunchContextStore.TrySetCurrent(newContext);

            Assert.That(callbacks.CompleteFailure(new InvalidOperationException("late")), Is.False);
            Assert.That(StageLaunchContextStore.IsCurrent(newContext), Is.True);
            Assert.That(store.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending.Matches(newHandoff), Is.True);
        }

        [Test]
        public void DuplicateLoadFailure_CleansUpOnce()
        {
            var store = new RecordingHandoffStore();
            store.TryBegin(1, StageId.CreateOrThrow("stage-0-1"), StageNavigationKind.Continue, "duplicate", out var handoff);
            var context = StageLaunchContext.FromHandoff(handoff);
            StageLaunchContextStore.TrySetCurrent(context);
            var callbacks = new StageLoadCallbackOwnership(context, handoff, store);

            Assert.That(callbacks.CompleteFailure(new InvalidOperationException("first")), Is.True);
            Assert.That(callbacks.CompleteFailure(new InvalidOperationException("second")), Is.False);
            Assert.That(store.ClearCount, Is.EqualTo(1));
        }

        [Test]
        public void SuccessThenLateFailure_DoesNotClearInstallerOwnedOrNewerState()
        {
            var store = new RecordingHandoffStore();
            store.TryBegin(1, StageId.CreateOrThrow("stage-0-1"), StageNavigationKind.Continue, "success", out var handoff);
            var context = StageLaunchContext.FromHandoff(handoff);
            StageLaunchContextStore.TrySetCurrent(context);
            var callbacks = new StageLoadCallbackOwnership(context, handoff, store);

            Assert.That(callbacks.CompleteSuccess(), Is.True);
            Assert.That(callbacks.CompleteFailure(new InvalidOperationException("late")), Is.False);
            Assert.That(StageLaunchContextStore.IsCurrent(context), Is.True);
            Assert.That(store.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending.Matches(handoff), Is.True);
        }

        [TestCase(StageNavigationKind.Retry, "stage-result-retry", true)]
        [TestCase(StageNavigationKind.Retry, "pause-retry", true)]
        [TestCase(StageNavigationKind.Retry, "arbitrary", false)]
        [TestCase(StageNavigationKind.NextStage, "campaign-auto-next", true)]
        [TestCase(StageNavigationKind.NextStage, "arbitrary", false)]
        [TestCase(StageNavigationKind.Continue, "main-menu", false)]
        public void PendinglessFallback_IsRestrictedToExplicitNavigationAndSource(
            StageNavigationKind navigationKind,
            string source,
            bool expected)
        {
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                navigationKind,
                source);

            Assert.That(CampaignPendinglessLaunchPolicy.IsAllowed(request), Is.EqualTo(expected));
        }

        private static CampaignLaunchHandoff BeginHandoff(
            int slot,
            string stage,
            StageNavigationKind navigation,
            string source)
        {
            Assert.That(
                CampaignLaunchHandoffSessionStore.Instance.TryBegin(
                    slot,
                    StageId.CreateOrThrow(stage),
                    navigation,
                    source,
                    out var handoff),
                Is.True);
            return handoff;
        }

        private static StageLaunchContext CreateContext(
            Guid token,
            int slot,
            string stage,
            StageNavigationKind navigation,
            string source)
        {
            return new StageLaunchContext(
                token,
                slot,
                StageId.CreateOrThrow(stage),
                navigation,
                source);
        }

        private static EditorDirectPlayContext CreateDirectPlayContext(
            EditorDirectPlayMode mode,
            StageId stageId)
        {
            switch (mode)
            {
                case EditorDirectPlayMode.CampaignProductionSlot:
                    return new EditorDirectPlayContext(
                        mode,
                        stageId,
                        string.Empty,
                        string.Empty,
                        SaveSlotStore.DefaultRemainingChances,
                        suppressCampaignFlow: false);
                case EditorDirectPlayMode.CampaignTempSlot:
                    return EditorDirectPlayContext.CreateCampaignTempSlot(
                        stageId,
                        SaveSlotStore.DefaultRemainingChances);
                case EditorDirectPlayMode.NonCampaign:
                    return EditorDirectPlayContext.CreateNonCampaign(stageId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported DirectPlay mode.");
            }
        }

        private sealed class RecordingHandoffStore : ICampaignLaunchHandoffStore
        {
            private CampaignLaunchHandoff _pending;

            public int ClearCount { get; private set; }

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
                ClearCount++;
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
        }
    }
}
