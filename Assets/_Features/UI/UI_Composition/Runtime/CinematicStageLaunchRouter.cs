using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    public sealed class CinematicStageLaunchRouter : IStageLaunchRouter
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly IStageLaunchRouter _inner;
        private readonly ISlotCinematicPlayer _player;
        private readonly SlotCinematicProgressStore _progressStore;

        public CinematicStageLaunchRouter(
            IStageLaunchRouter inner,
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            ISlotCinematicPlayer player)
            : this(inner, new SlotCinematicProgressStore(saveSlotStore), activeSlotProvider, player)
        {
        }

        internal CinematicStageLaunchRouter(
            IStageLaunchRouter inner,
            SlotCinematicProgressStore progressStore,
            ActiveSlotProvider activeSlotProvider,
            ISlotCinematicPlayer player)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void Launch(StageNavigationRequest request)
        {
            if (!_activeSlotProvider.TryGetActiveSlotNumber(out var slotNumber) ||
                !_player.HasIntroClip ||
                _progressStore.IsIntroPlayed(slotNumber))
            {
                _inner.Launch(request);
                return;
            }

            _player.PlayIntro(() =>
            {
                _progressStore.MarkIntroPlayed(slotNumber);
                _inner.Launch(request);
            });
        }
    }
}
