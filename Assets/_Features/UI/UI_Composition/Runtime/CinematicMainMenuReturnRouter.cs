using System;
using System.Threading;
using Game.Feature.Stages;
using Game.Feature.UI.Flow;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class CinematicMainMenuReturnRouter : IMainMenuReturnRouter
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly Func<bool> _isFinalClearMainReturn;
        private readonly IMainMenuReturnRouter _inner;
        private readonly ICinematicSequencePlayer _player;
        private readonly SlotCinematicProgressStore _progressStore;

        public CinematicMainMenuReturnRouter(
            IMainMenuReturnRouter inner,
            ICampaignSaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            ICinematicSequencePlayer player,
            Func<bool> isFinalClearMainReturn)
            : this(inner, new SlotCinematicProgressStore(saveSlotStore), activeSlotProvider, player, isFinalClearMainReturn)
        {
        }

        internal CinematicMainMenuReturnRouter(
            IMainMenuReturnRouter inner,
            SlotCinematicProgressStore progressStore,
            ActiveSlotProvider activeSlotProvider,
            ICinematicSequencePlayer player,
            Func<bool> isFinalClearMainReturn)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _isFinalClearMainReturn = isFinalClearMainReturn ?? (() => false);
        }

        public void ReturnToMainMenu(SceneTransitionIntent transitionIntent)
        {
            var routePolicy = SceneTransitionRoutePolicyCatalog.RequireDestination(
                SceneTransitionRoutePolicyCatalog.ResolveProduction(transitionIntent),
                SceneTransitionDestinationKind.MainMenu);
            if (routePolicy.Intent != SceneTransitionIntent.ReturnToMainMenu)
            {
                throw new InvalidOperationException(
                    $"Cinematic main-menu return accepts ReturnToMainMenu, not {routePolicy.Intent}.");
            }

            if (!_isFinalClearMainReturn() ||
                !_activeSlotProvider.TryGetActiveSlotNumber(out var slotNumber) ||
                !_player.HasOutroContent ||
                _progressStore.IsOutroPlayed(slotNumber))
            {
                _inner.ReturnToMainMenu(transitionIntent);
                return;
            }

            if (!MainMenuEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.CinematicToMainMenu,
                    TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                    "game-clear-outro",
                    out var entryToken))
            {
                throw new InvalidOperationException(
                    "Outro cinematic could not claim the CinematicToMainMenu destination session.");
            }

            var terminalClaimed = 0;
            try
            {
                _player.PlayOutro(result =>
                {
                    var current = MainMenuEntryPresentationRegistry.Current;
                    if (Volatile.Read(ref terminalClaimed) != 0 ||
                        !current.IsActive ||
                        current.Token != entryToken ||
                        current.TransitionIntent !=
                        SceneTransitionIntent.CinematicToMainMenu ||
                        current.Phase != SceneEntryPresentationPhase.Claimed ||
                        Interlocked.CompareExchange(ref terminalClaimed, 1, 0) != 0)
                    {
                        return;
                    }

                    if (result.Kind == CinematicPlaybackCompletionKind.Cancelled)
                    {
                        MainMenuEntryPresentationRegistry.TryCancelClaim(entryToken);
                        return;
                    }

                    if (result.Kind == CinematicPlaybackCompletionKind.Failed)
                    {
                        MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                            entryToken,
                            string.IsNullOrWhiteSpace(result.Message)
                                ? "Outro cinematic failed while holding its opaque owner."
                                : result.Message);
                        return;
                    }

                    try
                    {
                        _inner.ReturnToMainMenu(SceneTransitionIntent.CinematicToMainMenu);
                        _progressStore.MarkOutroPlayed(slotNumber);
                    }
                    catch (Exception exception)
                    {
                        TryFailCurrentClaimIfStillClaimed(
                            entryToken,
                            BuildRoutingFailureMessage(exception));
                        Debug.LogException(exception);
                    }
                });
            }
            catch
            {
                MainMenuEntryPresentationRegistry.TryCancelClaim(entryToken);
                throw;
            }
        }

        private static void TryFailCurrentClaimIfStillClaimed(
            MainMenuEntrySessionToken token,
            string failureReason)
        {
            var current = MainMenuEntryPresentationRegistry.Current;
            if (!current.IsActive ||
                current.Token != token ||
                current.TransitionIntent != SceneTransitionIntent.CinematicToMainMenu ||
                current.Phase != SceneEntryPresentationPhase.Claimed)
            {
                return;
            }

            MainMenuEntryPresentationRegistry.TryFailHoldingCover(token, failureReason);
        }

        private static string BuildRoutingFailureMessage(Exception exception)
        {
            const string message =
                "Outro completed, but Main Menu routing failed while holding its opaque owner.";
            return exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? message
                : $"{message} {exception.Message}";
        }
    }
}
