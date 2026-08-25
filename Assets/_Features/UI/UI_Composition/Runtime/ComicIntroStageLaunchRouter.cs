using System;
using System.Threading;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class ComicIntroStageLaunchRouter : IStageLaunchRouter
    {
        private readonly IStageLaunchRouter _inner;
        private readonly ICampaignLaunchHandoffStore _launchHandoffStore;
        private readonly IComicSequenceOpaqueHandoffCancellationOwner
            _opaqueHandoffCancellationOwner;
        private readonly IComicSequenceTransitionAudioHandoffOwner
            _transitionAudioHandoffOwner;
        private readonly IComicIntroOutroFlow _comicFlow;
        private readonly SlotComicProgressStore _progressStore;

        public ComicIntroStageLaunchRouter(
            IStageLaunchRouter inner,
            ICampaignSaveQuery saveSlotStore,
            ICampaignComicProgressPort comicProgressPort,
            ICampaignLaunchHandoffStore launchHandoffStore,
            IComicIntroOutroFlow comicFlow)
            : this(
                inner,
                new SlotComicProgressStore(
                    saveSlotStore,
                    comicProgressPort),
                launchHandoffStore,
                comicFlow)
        {
        }

        internal ComicIntroStageLaunchRouter(
            IStageLaunchRouter inner,
            SlotComicProgressStore progressStore,
            ICampaignLaunchHandoffStore launchHandoffStore,
            IComicIntroOutroFlow comicFlow)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
            _launchHandoffStore = launchHandoffStore ?? throw new ArgumentNullException(nameof(launchHandoffStore));
            _comicFlow = comicFlow ?? throw new ArgumentNullException(nameof(comicFlow));
            _opaqueHandoffCancellationOwner =
                comicFlow as IComicSequenceOpaqueHandoffCancellationOwner;
            _transitionAudioHandoffOwner =
                comicFlow as IComicSequenceTransitionAudioHandoffOwner;
        }

        public void Launch(StageNavigationRequest request)
        {
            var routePolicy = SceneTransitionRoutePolicyCatalog.RequireDestination(
                SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent),
                SceneTransitionDestinationKind.Gameplay);
            if (routePolicy.Intent != SceneTransitionIntent.GameplayEntry)
            {
                throw new InvalidOperationException(
                    $"Comic sequence stage launch accepts GameplayEntry, not {routePolicy.Intent}.");
            }

            if (!_launchHandoffStore.TryPeek(out var handoff))
            {
                throw new InvalidOperationException(
                    "Main menu comic sequence launch requires a pending campaign launch handoff.");
            }

            if (!handoff.Matches(request))
            {
                throw new InvalidOperationException(
                    "Main menu comic sequence launch request does not match the pending campaign launch handoff.");
            }

            if (!_comicFlow.HasIntroSequence ||
                _progressStore.IsIntroComicCompleted(handoff.SlotNumber))
            {
                LaunchOrClear(request, handoff);
                return;
            }

            if (!SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.ComicIntroToGameplay,
                    request.StageId,
                    TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                    handoff.Source,
                    handoff.SlotNumber,
                    handoff.Token,
                    out var entryToken))
            {
                TryClearCurrentHandoff(handoff);
                throw new InvalidOperationException(
                    "Intro comic sequence could not claim the ComicIntroToGameplay destination session.");
            }

            var terminalClaimed = 0;
            try
            {
                _comicFlow.PresentIntro(result =>
                {
                    if (Volatile.Read(ref terminalClaimed) != 0 ||
                        Interlocked.CompareExchange(ref terminalClaimed, 1, 0) != 0)
                    {
                        return;
                    }

                    if (!IsCurrentHandoff(handoff))
                    {
                        TryCancelCapturedIntroClaimIfStillClaimed(
                            entryToken,
                            request.StageId);
                        _opaqueHandoffCancellationOwner?
                            .TryReleaseCancelledIntroOpaqueOwner();
                        return;
                    }

                    if (!IsCurrentEntrySession(entryToken, request.StageId))
                    {
                        return;
                    }

                    switch (result.Kind)
                    {
                        case ComicSequenceResultKind.Completed:
                            try
                            {
                                LaunchOrClear(
                                    request.WithTransitionIntent(SceneTransitionIntent.ComicIntroToGameplay),
                                    handoff);
                                _transitionAudioHandoffOwner?
                                    .CommitAudioFocusToTransition();
                                _progressStore.MarkIntroComicCompleted(handoff.SlotNumber);
                            }
                            catch (Exception exception)
                            {
                                TryClearCurrentHandoff(handoff);
                                TryFailCurrentClaimIfStillClaimed(
                                    entryToken,
                                    request.StageId,
                                    BuildRoutingFailureMessage(exception));
                                Debug.LogException(exception);
                            }

                            return;

                        case ComicSequenceResultKind.Failed:
                        case ComicSequenceResultKind.Cancelled:
                        default:
                            TryClearCurrentHandoff(handoff);
                            if (result.Kind == ComicSequenceResultKind.Cancelled)
                            {
                                SceneEntryPresentationRegistry.TryCancelClaim(entryToken);
                            }
                            else
                            {
                                SceneEntryPresentationRegistry.TryFailHoldingCover(
                                    entryToken,
                                    string.IsNullOrWhiteSpace(result.Message)
                                        ? "Intro comic sequence failed while holding its opaque owner."
                                        : result.Message);
                            }

                            return;
                    }
                });
            }
            catch
            {
                SceneEntryPresentationRegistry.TryCancelClaim(entryToken);
                TryClearCurrentHandoff(handoff);
                throw;
            }
        }

        private void LaunchOrClear(
            StageNavigationRequest request,
            CampaignLaunchHandoff handoff)
        {
            try
            {
                _inner.Launch(request);
            }
            catch
            {
                TryClearCurrentHandoff(handoff);
                throw;
            }
        }

        private bool IsCurrentHandoff(CampaignLaunchHandoff expected)
        {
            return expected != null &&
                   expected.Token != Guid.Empty &&
                   CampaignSaveSlotPolicy.IsValidSlotNumber(expected.SlotNumber) &&
                   expected.StageId.IsValid &&
                   expected.NavigationKind != StageNavigationKind.None &&
                   !string.IsNullOrWhiteSpace(expected.Source) &&
                   _launchHandoffStore.TryPeek(out var current) &&
                   expected.Matches(current);
        }

        private void TryClearCurrentHandoff(CampaignLaunchHandoff expected)
        {
            if (IsCurrentHandoff(expected))
            {
                _launchHandoffStore.TryClear(expected.Token);
            }
        }

        private static bool IsCurrentEntrySession(
            SceneEntrySessionToken token,
            StageId destinationStageId)
        {
            var current = SceneEntryPresentationRegistry.Current;
            return current.IsActive &&
                   current.Token == token &&
                   current.TransitionIntent ==
                   SceneTransitionIntent.ComicIntroToGameplay &&
                   current.DestinationStageId.Equals(destinationStageId) &&
                   current.Phase == SceneEntryPresentationPhase.Claimed;
        }

        private static void TryFailCurrentClaimIfStillClaimed(
            SceneEntrySessionToken token,
            StageId destinationStageId,
            string failureReason)
        {
            var current = SceneEntryPresentationRegistry.Current;
            if (!current.IsActive ||
                current.Token != token ||
                current.TransitionIntent != SceneTransitionIntent.ComicIntroToGameplay ||
                !current.DestinationStageId.Equals(destinationStageId) ||
                current.Phase != SceneEntryPresentationPhase.Claimed)
            {
                return;
            }

            SceneEntryPresentationRegistry.TryFailHoldingCover(token, failureReason);
        }

        private static void TryCancelCapturedIntroClaimIfStillClaimed(
            SceneEntrySessionToken token,
            StageId destinationStageId)
        {
            var current = SceneEntryPresentationRegistry.Current;
            if (!current.IsActive ||
                current.Token != token ||
                current.TransitionIntent != SceneTransitionIntent.ComicIntroToGameplay ||
                !current.DestinationStageId.Equals(destinationStageId) ||
                current.Phase != SceneEntryPresentationPhase.Claimed)
            {
                return;
            }

            SceneEntryPresentationRegistry.TryCancelClaim(token);
        }

        private static string BuildRoutingFailureMessage(Exception exception)
        {
            const string message =
                "Intro completed, but gameplay routing failed while holding its opaque owner.";
            return exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? message
                : $"{message} {exception.Message}";
        }
    }
}
