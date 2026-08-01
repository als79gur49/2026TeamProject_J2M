using NUnit.Framework;
using UnityEngine;
using Guid = System.Guid;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class TerminalSessionAuthorityTests
    {
        [Test]
        public void ClaimsAcrossSceneGenerationsUseDifferentGloballyCorrelatedTokens()
        {
            var authority = new PersistentTerminalSessionAuthority(authorityGeneration: 41);
            var sceneA = authority.RegisterSceneBootstrap(101, "SceneA");
            var claimA = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Victory,
                sceneA,
                TerminalDestinationKind.SameSceneStageResult));

            Assert.That(claimA.Accepted, Is.True);
            Assert.That(authority.TryAdvancePhase(claimA.Token, TerminalSessionPhase.Revealing), Is.True);
            Assert.That(authority.TryComplete(claimA.Token), Is.True);

            var sceneB = authority.RegisterSceneBootstrap(102, "SceneB");
            var claimB = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Defeat,
                sceneB,
                TerminalDestinationKind.ReloadedGameplay));

            Assert.That(sceneB, Is.GreaterThan(sceneA));
            Assert.That(claimB.Accepted, Is.True);
            Assert.That(claimB.Token, Is.Not.EqualTo(claimA.Token));
            Assert.That(claimB.Token.Sequence, Is.GreaterThan(claimA.Token.Sequence));
        }

        [Test]
        public void EqualSequencesFromDifferentAuthorityGenerationsAreNotEqual()
        {
            var oldToken = new TerminalSessionToken(authorityGeneration: 7, sequence: 1);
            var currentToken = new TerminalSessionToken(authorityGeneration: 8, sequence: 1);

            Assert.That(oldToken, Is.Not.EqualTo(currentToken));
            Assert.That(oldToken.GetHashCode(), Is.EqualTo(System.HashCode.Combine(7L, 1L)));
            Assert.That(currentToken.GetHashCode(), Is.EqualTo(System.HashCode.Combine(8L, 1L)));
        }

        [Test]
        public void StaleReadinessFromSceneAIsRejectedWithoutMutatingSceneBSession()
        {
            var authority = new PersistentTerminalSessionAuthority(authorityGeneration: 51);
            var sceneA = authority.RegisterSceneBootstrap(201, "SceneA");
            var claimA = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Victory,
                sceneA,
                TerminalDestinationKind.SameSceneStageResult));
            Assert.That(authority.TryAdvancePhase(claimA.Token, TerminalSessionPhase.Revealing), Is.True);
            Assert.That(authority.TryComplete(claimA.Token), Is.True);

            var sceneB = authority.RegisterSceneBootstrap(202, "SceneB");
            var claimB = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Victory,
                sceneB,
                TerminalDestinationKind.SameSceneStageResult));
            Assert.That(authority.TryAdvancePhase(claimB.Token, TerminalSessionPhase.Iris), Is.True);
            Assert.That(authority.TryAdvancePhase(claimB.Token, TerminalSessionPhase.Black), Is.True);
            Assert.That(
                authority.TryAdvancePhase(
                    claimB.Token,
                    TerminalSessionPhase.WaitingSameSceneDestination),
                Is.True);
            var before = authority.Current;

            var staleSignal = new DestinationReadinessSignal(
                claimA.Token,
                transitionId: 0,
                sourceSceneGeneration: sceneA,
                destinationSceneGeneration: sceneA,
                TerminalDestinationKind.SameSceneStageResult,
                TerminalSessionPhase.WaitingSameSceneDestination,
                TerminalDestinationProvenance.SameSceneStageResult,
                DestinationReadinessOutcome.Ready);

            Assert.That(authority.CanAcceptDestinationEvent(staleSignal), Is.False);
            Assert.That(authority.Current.Token, Is.EqualTo(before.Token));
            Assert.That(authority.Current.Phase, Is.EqualTo(before.Phase));
            Assert.That(authority.Current.DestinationKind, Is.EqualTo(before.DestinationKind));
        }

        [Test]
        public void WrongPhaseReadinessIsRejectedAndCannotLatchForLaterPhase()
        {
            var authority = new PersistentTerminalSessionAuthority(authorityGeneration: 61);
            var scene = authority.RegisterSceneBootstrap(301, "Scene");
            var claim = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Victory,
                scene,
                TerminalDestinationKind.SameSceneStageResult));
            var early = new DestinationReadinessSignal(
                claim.Token,
                transitionId: 0,
                sourceSceneGeneration: scene,
                destinationSceneGeneration: scene,
                TerminalDestinationKind.SameSceneStageResult,
                TerminalSessionPhase.WaitingSameSceneDestination,
                TerminalDestinationProvenance.SameSceneStageResult,
                DestinationReadinessOutcome.Ready);

            Assert.That(authority.CanAcceptDestinationEvent(early), Is.False);
            Assert.That(authority.TryAdvancePhase(claim.Token, TerminalSessionPhase.Iris), Is.True);
            Assert.That(authority.TryAdvancePhase(claim.Token, TerminalSessionPhase.Black), Is.True);
            Assert.That(
                authority.TryAdvancePhase(
                    claim.Token,
                    TerminalSessionPhase.WaitingSameSceneDestination),
                Is.True);
            Assert.That(authority.Current.Phase, Is.EqualTo(TerminalSessionPhase.WaitingSameSceneDestination));
        }

        [TestCase(DestinationReadinessOutcome.Failed)]
        [TestCase(DestinationReadinessOutcome.Cancelled)]
        public void ReloadedDestinationFailureConvergesToFailedHoldingCover(
            DestinationReadinessOutcome outcome)
        {
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            try
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(401, "SceneA");
                var claim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(
                    authority.TryBindTransition(
                        claim.Token,
                        transitionId: 91,
                        TerminalDestinationKind.ReloadedGameplay),
                    Is.True);
                Assert.That(authority.TryAdvancePhase(claim.Token, TerminalSessionPhase.Activating), Is.True);
                var destinationGeneration = authority.RegisterSceneBootstrap(402, "SceneB");
                Assert.That(
                    authority.TryAdvancePhase(
                        claim.Token,
                        TerminalSessionPhase.WaitingDestinationReady),
                    Is.True);

                var signal = new DestinationReadinessSignal(
                    claim.Token,
                    transitionId: 91,
                    sourceGeneration,
                    destinationGeneration,
                    TerminalDestinationKind.ReloadedGameplay,
                    TerminalSessionPhase.WaitingDestinationReady,
                    TerminalDestinationProvenance.ReloadedGameplayBootstrap,
                    outcome,
                    "Destination installer failed.");

                Assert.That(TerminalDestinationReadiness.Signal(signal), Is.True);
                Assert.That(authority.IsActive, Is.True);
                Assert.That(authority.Phase, Is.EqualTo(TerminalSessionPhase.FailedHoldingCover));
                Assert.That(authority.Current.FailureReason, Does.Contain("Destination installer failed"));
                Assert.That(TerminalDestinationReadiness.IsReady(claim.Token), Is.False);
            }
            finally
            {
                TerminalDestinationReadiness.ResetForTests();
                TerminalSessionRegistry.ResetForTests();
            }
        }

        [Test]
        public void SceneEntrySession_RequiresOrderedCorrelatedReadinessBeforeCompletion()
        {
            SceneEntryPresentationRegistry.ResetForTests();
            var cameraObject = new GameObject("EntryOutputCamera", typeof(Camera));
            try
            {
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.ManualRetry,
                        StageId.CreateOrThrow("stage-2-1"),
                        sourceSceneGeneration: 11,
                        out var token),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.ManualRetry));
                Assert.That(
                    SceneEntryPresentationRegistry.TryBindTransition(token, transitionId: 701),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.Opening),
                    Is.False,
                    "Entry lifecycle must not skip persistent-cover and runtime-readiness phases.");
                Assert.That(
                    SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                        token,
                        destinationSceneGeneration: 12),
                    Is.False,
                    "Destination readiness must not be accepted before persistent-cover Loading.");
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
                Assert.That(
                    SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                        token,
                        destinationSceneGeneration: 12),
                    Is.True);

                var beforeStaleReadiness = SceneEntryPresentationRegistry.Current;
                var stale = new SceneEntryRuntimeReady(
                    new SceneEntrySessionToken(token.Value + 1),
                    transitionId: 701,
                    destinationSceneGeneration: 12,
                    playerEntityId: 10,
                    cameraObject.GetComponent<Camera>(),
                    SceneEntryRuntimeReadyProvenance.ProductionGameplayBootstrap);
                Assert.That(SceneEntryPresentationRegistry.CanAcceptRuntimeReady(stale), Is.False);
                Assert.That(SceneEntryPresentationRegistry.Current.Token, Is.EqualTo(beforeStaleReadiness.Token));
                Assert.That(SceneEntryPresentationRegistry.Current.Phase, Is.EqualTo(beforeStaleReadiness.Phase));

                var ready = new SceneEntryRuntimeReady(
                    token,
                    transitionId: 701,
                    destinationSceneGeneration: 12,
                    playerEntityId: 10,
                    cameraObject.GetComponent<Camera>(),
                    SceneEntryRuntimeReadyProvenance.ProductionGameplayBootstrap);
                Assert.That(SceneEntryPresentationRegistry.CanAcceptRuntimeReady(ready), Is.True);
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
                Assert.That(SceneEntryPresentationRegistry.TryComplete(token), Is.True);
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Completed));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                SceneEntryPresentationRegistry.ResetForTests();
            }
        }

        [Test]
        public void GameplayEntrySession_CapturesMinimalLaunchProvenanceWithoutSaveDocument()
        {
            SceneEntryPresentationRegistry.ResetForTests();
            try
            {
                var launchToken = Guid.NewGuid();
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.GameplayEntry,
                        StageId.CreateOrThrow("stage-0-1"),
                        sourceSceneGeneration: 41,
                        launchProvenance: "main-menu-continue",
                        launchSlotNumber: 2,
                        launchToken,
                        out var token),
                    Is.True);

                var snapshot = SceneEntryPresentationRegistry.Current;
                Assert.That(snapshot.Token, Is.EqualTo(token));
                Assert.That(snapshot.TransitionIntent, Is.EqualTo(SceneTransitionIntent.GameplayEntry));
                Assert.That(snapshot.DestinationStageId.Value, Is.EqualTo("stage-0-1"));
                Assert.That(snapshot.SourceSceneGeneration, Is.EqualTo(41));
                Assert.That(snapshot.LaunchProvenance, Is.EqualTo("main-menu-continue"));
                Assert.That(snapshot.LaunchSlotNumber, Is.EqualTo(2));
                Assert.That(snapshot.LaunchToken, Is.EqualTo(launchToken));
            }
            finally
            {
                SceneEntryPresentationRegistry.ResetForTests();
            }
        }

        [Test]
        public void SceneEntrySession_StaleReadinessRenderAndOpeningCallbacksCannotMutateCurrentRetry()
        {
            SceneEntryPresentationRegistry.ResetForTests();
            var cameraObject = new GameObject("CurrentRetryOutputCamera", typeof(Camera));
            try
            {
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.ManualRetry,
                        StageId.CreateOrThrow("stage-2-1"),
                        sourceSceneGeneration: 21,
                        out var oldToken),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryBindTransition(oldToken, 801),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        oldToken,
                        SceneEntryPresentationPhase.PersistentCoverReady),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        oldToken,
                        SceneEntryPresentationPhase.Loading),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryRegisterDestinationScene(oldToken, 22),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        oldToken,
                        SceneEntryPresentationPhase.EntryIrisClosed),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        oldToken,
                        SceneEntryPresentationPhase.Opening),
                    Is.True);
                Assert.That(SceneEntryPresentationRegistry.TryComplete(oldToken), Is.True);

                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.DemoStageRelaunch,
                        StageId.CreateOrThrow("stage-2-2"),
                        sourceSceneGeneration: 31,
                        out var currentToken),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryBindTransition(currentToken, 802),
                    Is.True);
                var currentBeforeStaleCallbacks = SceneEntryPresentationRegistry.Current;

                var staleReadiness = new SceneEntryRuntimeReady(
                    oldToken,
                    transitionId: 801,
                    destinationSceneGeneration: 22,
                    playerEntityId: 10,
                    cameraObject.GetComponent<Camera>(),
                    SceneEntryRuntimeReadyProvenance.ProductionGameplayBootstrap);
                Assert.That(
                    SceneEntryPresentationRegistry.CanAcceptRuntimeReady(staleReadiness),
                    Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        oldToken,
                        SceneEntryPresentationPhase.EntryIrisClosed),
                    Is.False,
                    "A stale closed-Iris render acknowledgement must not release the current cover.");
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        oldToken,
                        SceneEntryPresentationPhase.Opening),
                    Is.False,
                    "A stale opening completion must not release current gameplay input.");
                Assert.That(SceneEntryPresentationRegistry.TryComplete(oldToken), Is.False);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Token,
                    Is.EqualTo(currentBeforeStaleCallbacks.Token));
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(currentBeforeStaleCallbacks.Phase));
                Assert.That(
                    SceneEntryPresentationRegistry.Current.TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.DemoStageRelaunch));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                SceneEntryPresentationRegistry.ResetForTests();
            }
        }
    }
}
