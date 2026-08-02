using System;
using System.Threading;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class CinematicStageLaunchRouter : IStageLaunchRouter
    {
        private readonly IStageLaunchRouter _inner;
        private readonly ICampaignLaunchHandoffStore _launchHandoffStore;
        private readonly ISlotCinematicPlayer _player;
        private readonly SlotCinematicProgressStore _progressStore;

        public CinematicStageLaunchRouter(
            IStageLaunchRouter inner,
            ICampaignSaveSlotStore saveSlotStore,
            ICampaignLaunchHandoffStore launchHandoffStore,
            ISlotCinematicPlayer player)
            : this(inner, new SlotCinematicProgressStore(saveSlotStore), launchHandoffStore, player)
        {
        }

        internal CinematicStageLaunchRouter(
            IStageLaunchRouter inner,
            SlotCinematicProgressStore progressStore,
            ICampaignLaunchHandoffStore launchHandoffStore,
            ISlotCinematicPlayer player)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
            _launchHandoffStore = launchHandoffStore ?? throw new ArgumentNullException(nameof(launchHandoffStore));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void Launch(StageNavigationRequest request)
        {
            var routePolicy = SceneTransitionRoutePolicyCatalog.RequireDestination(
                SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent),
                SceneTransitionDestinationKind.Gameplay);
            if (routePolicy.Intent != SceneTransitionIntent.GameplayEntry)
            {
                throw new InvalidOperationException(
                    $"Cinematic stage launch accepts GameplayEntry, not {routePolicy.Intent}.");
            }

            if (!_launchHandoffStore.TryPeek(out var handoff))
            {
                throw new InvalidOperationException(
                    "Main menu cinematic launch requires a pending campaign launch handoff.");
            }

            if (!handoff.Matches(request))
            {
                throw new InvalidOperationException(
                    "Main menu cinematic launch request does not match the pending campaign launch handoff.");
            }

            if (!_player.HasIntroClip ||
                _progressStore.IsIntroPlayed(handoff.SlotNumber))
            {
                LaunchOrClear(request, handoff);
                return;
            }

            if (!SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.CinematicToGameplay,
                    request.StageId,
                    TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                    handoff.Source,
                    handoff.SlotNumber,
                    handoff.Token,
                    out var entryToken))
            {
                TryClearCurrentHandoff(handoff);
                throw new InvalidOperationException(
                    "Intro cinematic could not claim the CinematicToGameplay destination session.");
            }

            var terminalClaimed = 0;
            try
            {
                _player.PlayIntro(result =>
                {
                    if (Volatile.Read(ref terminalClaimed) != 0 ||
                        !IsCurrentHandoff(handoff) ||
                        !IsCurrentEntrySession(entryToken, request.StageId) ||
                        Interlocked.CompareExchange(ref terminalClaimed, 1, 0) != 0)
                    {
                        return;
                    }

                    switch (result.Kind)
                    {
                        case CinematicPlaybackCompletionKind.Completed:
                        case CinematicPlaybackCompletionKind.Skipped:
                            try
                            {
                                LaunchOrClear(
                                    request.WithTransitionIntent(SceneTransitionIntent.CinematicToGameplay),
                                    handoff);
                                _progressStore.MarkIntroPlayed(handoff.SlotNumber);
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

                        case CinematicPlaybackCompletionKind.Failed:
                        case CinematicPlaybackCompletionKind.Cancelled:
                        default:
                            TryClearCurrentHandoff(handoff);
                            if (result.Kind == CinematicPlaybackCompletionKind.Cancelled)
                            {
                                SceneEntryPresentationRegistry.TryCancelClaim(entryToken);
                            }
                            else
                            {
                                SceneEntryPresentationRegistry.TryFailHoldingCover(
                                    entryToken,
                                    string.IsNullOrWhiteSpace(result.Message)
                                        ? "Intro cinematic failed while holding its opaque owner."
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
                   SaveSlotStore.IsValidSlotNumber(expected.SlotNumber) &&
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
                   SceneTransitionIntent.CinematicToGameplay &&
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
                current.TransitionIntent != SceneTransitionIntent.CinematicToGameplay ||
                !current.DestinationStageId.Equals(destinationStageId) ||
                current.Phase != SceneEntryPresentationPhase.Claimed)
            {
                return;
            }

            SceneEntryPresentationRegistry.TryFailHoldingCover(token, failureReason);
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
