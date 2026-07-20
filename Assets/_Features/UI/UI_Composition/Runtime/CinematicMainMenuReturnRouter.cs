using System;
using Game.Feature.Stages;
using Game.Feature.UI.Flow;

namespace Game.Feature.UI.Composition
{
    public sealed class CinematicMainMenuReturnRouter : IMainMenuReturnRouter
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly Func<bool> _isFinalClearMainReturn;
        private readonly IMainMenuReturnRouter _inner;
        private readonly ISlotCinematicPlayer _player;
        private readonly SlotCinematicProgressStore _progressStore;

        public CinematicMainMenuReturnRouter(
            IMainMenuReturnRouter inner,
            ICampaignSaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            ISlotCinematicPlayer player,
            Func<bool> isFinalClearMainReturn)
            : this(inner, new SlotCinematicProgressStore(saveSlotStore), activeSlotProvider, player, isFinalClearMainReturn)
        {
        }

        internal CinematicMainMenuReturnRouter(
            IMainMenuReturnRouter inner,
            SlotCinematicProgressStore progressStore,
            ActiveSlotProvider activeSlotProvider,
            ISlotCinematicPlayer player,
            Func<bool> isFinalClearMainReturn)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _isFinalClearMainReturn = isFinalClearMainReturn ?? (() => false);
        }

        public void ReturnToMainMenu()
        {
            if (!_isFinalClearMainReturn() ||
                !_activeSlotProvider.TryGetActiveSlotNumber(out var slotNumber) ||
                !_player.HasOutroClip ||
                _progressStore.IsOutroPlayed(slotNumber))
            {
                _inner.ReturnToMainMenu();
                return;
            }

            _player.PlayOutro(result =>
            {
                if (result.Kind == CinematicPlaybackCompletionKind.Cancelled)
                {
                    return;
                }

                if (result.Kind == CinematicPlaybackCompletionKind.Completed ||
                    result.Kind == CinematicPlaybackCompletionKind.Skipped)
                {
                    _progressStore.MarkOutroPlayed(slotNumber);
                }

                _inner.ReturnToMainMenu();
            });
        }
    }
}
