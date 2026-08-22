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
            EditorDirectPlayLaunchOwnershipStore.ResetForTests();
            CampaignLaunchHandoffSessionStore.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.ResetForTests();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayLaunchOwnershipStore.ResetForTests();
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
        public void DirectPlayConsumeThenExit_ClearsOwnedRuntimeContext()
        {
            BeginDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.True);

            StageEditorDirectPlayLauncher.HandlePlayModeStateChangedForTests(
                UnityEditor.PlayModeStateChange.ExitingPlayMode);

            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out _), Is.False);
            Assert.That(EditorDirectPlayLaunchOwnershipStore.TryPeek(out _), Is.False);
        }

        [Test]
        public void DirectPlayConsumeThenExit_AllowsNextLaunch()
        {
            var first = BeginDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.True);

            StageEditorDirectPlayLauncher.HandlePlayModeStateChangedForTests(
                UnityEditor.PlayModeStateChange.ExitingPlayMode);
            StageEditorDirectPlayLauncher.HandlePlayModeStateChangedForTests(
                UnityEditor.PlayModeStateChange.EnteredEditMode);

            Assert.DoesNotThrow(() => StageEditorDirectPlayLauncher.ThrowIfLaunchIsAlreadyInProgress(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false));
            var second = BeginDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            var duplicate = Assert.Throws<InvalidOperationException>(() =>
                StageEditorDirectPlayLauncher.ThrowIfLaunchIsAlreadyInProgress(
                    isPlaying: false,
                    isPlayingOrWillChangePlaymode: false));

            Assert.That(second.Equals(first), Is.False);
            Assert.That(duplicate?.Message, Does.Contain("rejected"));
        }

        [Test]
        public void ProductionDirectPlay_Exit_AllowsRelaunch()
        {
            AssertModeAllowsRelaunch(EditorDirectPlayMode.CampaignProductionSlot);
        }

        [Test]
        public void TempDirectPlay_Exit_AllowsRelaunch()
        {
            AssertModeAllowsRelaunch(EditorDirectPlayMode.CampaignTempSlot);
        }

        [Test]
        public void TempDirectPlay_AdvanceCarriesOwnershipAndExitClearsUpdatedContextAndTempSave()
        {
            var original = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.CampaignTempSlot,
                "stage-0-1");
            Assert.That(
                StageLaunchContextStore.TryConsume(original.ExpectedRuntimeContext, out _),
                Is.True);
            CreateTemporaryCampaignFiles();
            var nextStageId = StageId.CreateOrThrow("stage-0-2");

            EditorDirectPlayContextStore.SetCurrent(
                CreateDirectPlayContext(EditorDirectPlayMode.CampaignTempSlot, nextStageId));

            Assert.That(EditorDirectPlayLaunchOwnershipStore.TryPeek(out var carried), Is.True);
            Assert.That(carried.Mode, Is.EqualTo(EditorDirectPlayMode.CampaignTempSlot));
            Assert.That(carried.StageId, Is.EqualTo(nextStageId));
            Assert.That(carried.ExpectedRuntimeContext.Token, Is.EqualTo(original.ExpectedRuntimeContext.Token));

            StageEditorDirectPlayLauncher.HandlePlayModeStateChangedForTests(
                UnityEditor.PlayModeStateChange.ExitingPlayMode);

            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out _), Is.False);
            Assert.That(EditorDirectPlayLaunchOwnershipStore.TryPeek(out _), Is.False);
            AssertTemporaryCampaignFilesCleared();
        }

        [Test]
        public void TempDirectPlay_InFlightAdvanceTransfersExactLaunchOwnershipAndExitClearsTempSave()
        {
            var original = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.CampaignTempSlot,
                "stage-0-1");
            Assert.That(
                StageLaunchContextStore.TryConsume(original.ExpectedRuntimeContext, out _),
                Is.True);
            CreateTemporaryCampaignFiles();
            var nextStageId = StageId.CreateOrThrow("stage-0-2");
            var continuingContext = EditorDirectPlayContext.CreateCampaignTempSlot(
                nextStageId,
                CampaignSaveSlotPolicy.DefaultRemainingChances);
            EditorDirectPlayContextStore.SetCurrent(continuingContext);
            var request = new StageNavigationRequest(
                nextStageId,
                StageNavigationKind.NextStage,
                "campaign-stage-flow",
                default,
                SceneTransitionIntent.StageAdvance,
                continuingContext);
            var launchContext = StageLaunchContext.CreatePendinglessReload(request);

            Assert.That(StageLaunchContextStore.TrySetCurrent(launchContext), Is.True);
            Assert.That(EditorDirectPlayLaunchOwnershipStore.TryPeek(out var transferred), Is.True);
            Assert.That(transferred.ExpectedRuntimeContext, Is.EqualTo(launchContext));

            var result = StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(transferred);

            Assert.That(
                result.RuntimeContextResult,
                Is.EqualTo(OwnedDirectPlayRuntimeCleanupResult.ExactContextCleared));
            Assert.That(result.EditorContextCleared, Is.True);
            Assert.That(result.OwnershipReleased, Is.True);
            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out _), Is.False);
            Assert.That(EditorDirectPlayLaunchOwnershipStore.TryPeek(out _), Is.False);
            AssertTemporaryCampaignFilesCleared();
        }

        [Test]
        public void NonCampaignDirectPlay_Exit_AllowsRelaunch()
        {
            AssertModeAllowsRelaunch(EditorDirectPlayMode.NonCampaign);
        }

        [Test]
        public void DirectPlayExit_DoesNotClearNewerRuntimeContext()
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            var newer = CreateContext(
                Guid.NewGuid(),
                0,
                "stage-1-1",
                StageNavigationKind.Continue,
                "editor-direct-play");
            ReplaceRuntimeContext(ownership.ExpectedRuntimeContext, newer);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateNonCampaign(newer.StageId));

            var result = StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(result.RuntimeContextResult, Is.EqualTo(
                OwnedDirectPlayRuntimeCleanupResult.DifferentContextPreserved));
            Assert.That(StageLaunchContextStore.IsCurrent(newer), Is.True);
        }

        [Test]
        public void DirectPlayExit_DoesNotClearSameStageDifferentToken()
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            var newer = CreateContext(
                Guid.NewGuid(),
                0,
                "stage-0-1",
                StageNavigationKind.Continue,
                "editor-direct-play");
            ReplaceRuntimeContext(ownership.ExpectedRuntimeContext, newer);

            StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(StageLaunchContextStore.IsCurrent(newer), Is.True);
        }

        [Test]
        public void DirectPlayExit_DoesNotClearSameTokenDifferentSlot()
        {
            AssertRuntimeMismatchIsPreserved(ownership => CreateContext(
                ownership.ExpectedRuntimeContext.Token,
                1,
                ownership.ExpectedRuntimeContext.StageId.Value,
                ownership.ExpectedRuntimeContext.NavigationKind,
                ownership.ExpectedRuntimeContext.Source));
        }

        [Test]
        public void DirectPlayExit_DoesNotClearSameTokenDifferentStage()
        {
            AssertRuntimeMismatchIsPreserved(ownership => CreateContext(
                ownership.ExpectedRuntimeContext.Token,
                ownership.ExpectedRuntimeContext.SlotNumber,
                "stage-1-1",
                ownership.ExpectedRuntimeContext.NavigationKind,
                ownership.ExpectedRuntimeContext.Source));
        }

        [Test]
        public void DirectPlayExit_DoesNotClearSameTokenDifferentNavigation()
        {
            AssertRuntimeMismatchIsPreserved(ownership => CreateContext(
                ownership.ExpectedRuntimeContext.Token,
                ownership.ExpectedRuntimeContext.SlotNumber,
                ownership.ExpectedRuntimeContext.StageId.Value,
                StageNavigationKind.Retry,
                ownership.ExpectedRuntimeContext.Source));
        }

        [Test]
        public void DirectPlayExit_DoesNotClearSameTokenDifferentSource()
        {
            AssertRuntimeMismatchIsPreserved(ownership => CreateContext(
                ownership.ExpectedRuntimeContext.Token,
                ownership.ExpectedRuntimeContext.SlotNumber,
                ownership.ExpectedRuntimeContext.StageId.Value,
                ownership.ExpectedRuntimeContext.NavigationKind,
                "newer-editor-direct-play"));
        }

        [Test]
        public void DirectPlayExit_DoesNotClearDifferentModeRuntimeContext()
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            EditorDirectPlayContextStore.SetCurrent(
                CreateDirectPlayContext(EditorDirectPlayMode.CampaignTempSlot, ownership.StageId));

            var result = StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(result.RuntimeContextResult, Is.EqualTo(
                OwnedDirectPlayRuntimeCleanupResult.DifferentContextPreserved));
            Assert.That(StageLaunchContextStore.IsCurrent(ownership.ExpectedRuntimeContext), Is.True);
        }

        [Test]
        public void DirectPlayExit_DoesNotClearNormalCampaignPending()
        {
            var ownership = BeginDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            var campaignPending = BeginHandoff(
                1,
                "stage-1-1",
                StageNavigationKind.Continue,
                "main-menu");

            StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(CampaignLaunchHandoffSessionStore.Instance.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(campaignPending));
        }

        [Test]
        public void DirectPlayWithNormalPending_ExitPreservesCampaignPending()
        {
            DirectPlayExit_DoesNotClearNormalCampaignPending();
        }

        [Test]
        public void DirectPlayExit_DoesNotClearNormalCampaignStageLaunchContext()
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            var handoff = BeginHandoff(
                1,
                "stage-1-1",
                StageNavigationKind.Continue,
                "main-menu");
            var campaignContext = StageLaunchContext.FromHandoff(handoff);
            ReplaceRuntimeContext(ownership.ExpectedRuntimeContext, campaignContext);

            StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(StageLaunchContextStore.IsCurrent(campaignContext), Is.True);
            Assert.That(CampaignLaunchHandoffSessionStore.Instance.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(handoff));
        }

        [Test]
        public void DirectPlayCancelledBeforeConsume_ClearsOnlyMatchingPrime()
        {
            var ownership = BeginDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");

            var result = StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(result.MatchingPrimeCleared, Is.True);
            Assert.That(result.RuntimeContextResult, Is.EqualTo(
                OwnedDirectPlayRuntimeCleanupResult.NoCurrentContext));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        public void DirectPlayExit_MalformedOwnership_DoesNotClearRuntimeOrPrime()
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            var runtimeContext = new StageLaunchContext(
                Guid.NewGuid(),
                0,
                stageId,
                StageNavigationKind.Continue,
                "editor-direct-play");
            Assert.That(StageLaunchContextStore.TrySetCurrent(runtimeContext), Is.True);

            var result = StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(default);

            Assert.That(result.RuntimeContextResult, Is.EqualTo(
                OwnedDirectPlayRuntimeCleanupResult.MalformedExpectedOwnership));
            Assert.That(StageLaunchContextStore.IsCurrent(runtimeContext), Is.True);
        }

        [Test]
        public void DirectPlayExit_AfterPrimeAlreadyConsumed_DoesNotRecreatePrime()
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");

            StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
        }

        [Test]
        public void DirectPlayExit_WithDifferentPrime_PreservesNewerPrime()
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            Assert.That(
                StageLaunchContextStore.TryClear(ownership.ExpectedRuntimeContext),
                Is.True);
            var newerPrime = new StageLaunchContext(
                Guid.NewGuid(),
                0,
                StageId.CreateOrThrow("stage-1-1"),
                StageNavigationKind.Continue,
                "editor-direct-play");
            StageLaunchContextStore.PrimePendingEditorDirectPlay(newerPrime);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateNonCampaign(newerPrime.StageId));

            var result = StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(result.DifferentPrimePreserved, Is.True);
            Assert.That(
                StageLaunchContextStore.TryPeekPendingEditorDirectPlayContext(out var currentPrime),
                Is.True);
            Assert.That(currentPrime, Is.EqualTo(newerPrime));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().StageId, Is.EqualTo(newerPrime.StageId));
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
                    CampaignSaveSlotPolicy.DefaultRemainingChances));

            Assert.That(exception?.Message, Does.Contain("rejected"));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneBeforeDuplicate.handle));
            Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(firstContext));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().StageId, Is.EqualTo(firstStage));
        }

        [Test]
        public void DirectPlayDuplicateGuard_EditorOwnershipRecord_RejectsUntilExitCleanup()
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            Assert.That(StageLaunchContextStore.TryClear(ownership.ExpectedRuntimeContext), Is.True);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                StageEditorDirectPlayLauncher.ThrowIfLaunchIsAlreadyInProgress(
                    isPlaying: false,
                    isPlayingOrWillChangePlaymode: false));

            Assert.That(exception?.Message, Does.Contain("rejected"));
            Assert.That(EditorDirectPlayLaunchOwnershipStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.EqualTo(ownership));
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
        public void PendinglessLaunchContext_CarriesCampaignProductionDirectPlayProvenanceAcrossRequestDecoration()
        {
            var completedStageId = StageId.CreateOrThrow("stage-0-1");
            var nextStageId = StageId.CreateOrThrow("stage-0-2");
            var directPlayContext = new EditorDirectPlayContext(
                EditorDirectPlayMode.CampaignProductionSlot,
                completedStageId,
                remainingChances: 2,
                suppressCampaignFlow: false);
            var request = new StageNavigationRequest(
                    nextStageId,
                    StageNavigationKind.NextStage,
                    "campaign-auto-next")
                .WithTransitionHint(StageTransitionHint.ForKind(StageTransitionKind.StageClearNext))
                .WithTransitionIntent(SceneTransitionIntent.StageAdvance)
                .WithEditorDirectPlayContext(directPlayContext);

            var launchContext = StageLaunchContext.CreatePendinglessReload(request);

            Assert.That(request.EditorDirectPlayContext.Mode, Is.EqualTo(EditorDirectPlayMode.CampaignProductionSlot));
            Assert.That(request.EditorDirectPlayContext.StageId, Is.EqualTo(nextStageId));
            Assert.That(launchContext.EditorDirectPlayContext, Is.EqualTo(request.EditorDirectPlayContext));
            Assert.That(launchContext.Matches(request), Is.True);
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
        [TestCase(StageNavigationKind.Retry, "demo-stage-control-start-stage", true)]
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

        private static void AssertModeAllowsRelaunch(EditorDirectPlayMode mode)
        {
            var first = BeginConsumedDirectPlayOwnership(mode, "stage-0-1");

            StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(first);

            Assert.DoesNotThrow(() => StageEditorDirectPlayLauncher.ThrowIfLaunchIsAlreadyInProgress(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false));
            var second = BeginDirectPlayOwnership(mode, "stage-0-1");
            Assert.That(second.Equals(first), Is.False);
        }

        private static void AssertRuntimeMismatchIsPreserved(
            Func<EditorDirectPlayLaunchOwnershipRecord, StageLaunchContext> createNewer)
        {
            var ownership = BeginConsumedDirectPlayOwnership(
                EditorDirectPlayMode.NonCampaign,
                "stage-0-1");
            var newer = createNewer(ownership);
            ReplaceRuntimeContext(ownership.ExpectedRuntimeContext, newer);

            var result = StageEditorDirectPlayLauncher.CleanupOwnedDirectPlayForTests(ownership);

            Assert.That(result.RuntimeContextResult, Is.EqualTo(
                OwnedDirectPlayRuntimeCleanupResult.DifferentContextPreserved));
            Assert.That(StageLaunchContextStore.IsCurrent(newer), Is.True);
        }

        private static EditorDirectPlayLaunchOwnershipRecord BeginConsumedDirectPlayOwnership(
            EditorDirectPlayMode mode,
            string stage)
        {
            var ownership = BeginDirectPlayOwnership(mode, stage);
            Assert.That(StageLaunchContextStore.TryGetCurrent(out var consumedStageId), Is.True);
            Assert.That(consumedStageId, Is.EqualTo(ownership.StageId));
            Assert.That(
                StageLaunchContextStore.IsCurrent(ownership.ExpectedRuntimeContext),
                Is.True);
            return ownership;
        }

        private static EditorDirectPlayLaunchOwnershipRecord BeginDirectPlayOwnership(
            EditorDirectPlayMode mode,
            string stage)
        {
            var stageId = StageId.CreateOrThrow(stage);
            EditorDirectPlayContextStore.SetCurrent(CreateDirectPlayContext(mode, stageId));
            var expectedRuntimeContext = StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
            var ownership = new EditorDirectPlayLaunchOwnershipRecord(mode, expectedRuntimeContext);
            Assert.That(EditorDirectPlayLaunchOwnershipStore.TrySetCurrent(ownership), Is.True);
            return ownership;
        }

        private static void ReplaceRuntimeContext(
            StageLaunchContext expectedCurrent,
            StageLaunchContext replacement)
        {
            Assert.That(StageLaunchContextStore.TryClear(expectedCurrent), Is.True);
            Assert.That(StageLaunchContextStore.TrySetCurrent(replacement), Is.True);
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
                        CampaignSaveSlotPolicy.DefaultRemainingChances,
                        suppressCampaignFlow: false);
                case EditorDirectPlayMode.CampaignTempSlot:
                    return EditorDirectPlayContext.CreateCampaignTempSlot(
                        stageId,
                        CampaignSaveSlotPolicy.DefaultRemainingChances);
                case EditorDirectPlayMode.NonCampaign:
                    return EditorDirectPlayContext.CreateNonCampaign(stageId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported DirectPlay mode.");
            }
        }

        private static void CreateTemporaryCampaignFiles()
        {
            var pathProvider = new TemporaryCampaignSavePathProvider();
            System.IO.Directory.CreateDirectory(pathProvider.SaveRootPath);
            System.IO.File.WriteAllText(
                pathProvider.GetSaveFilePath(FileCampaignProfileRepository.ProfileFileName),
                "{}");
            System.IO.File.WriteAllText(
                pathProvider.GetSaveFilePath(CampaignLocalLaunchStateRepository.FileName),
                "{}");
        }

        private static void AssertTemporaryCampaignFilesCleared()
        {
            var pathProvider = new TemporaryCampaignSavePathProvider();
            Assert.That(
                System.IO.File.Exists(pathProvider.GetSaveFilePath(FileCampaignProfileRepository.ProfileFileName)),
                Is.False);
            Assert.That(
                System.IO.File.Exists(pathProvider.GetSaveFilePath(CampaignLocalLaunchStateRepository.FileName)),
                Is.False);
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
