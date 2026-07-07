using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    public sealed class CinematicStageLaunchRouter : IStageLaunchRouter
    {
        private readonly IStageLaunchRouter _inner;
        private readonly IPendingLaunchSlotProvider _pendingLaunchSlotProvider;
        private readonly ISlotCinematicPlayer _player;
        private readonly SlotCinematicProgressStore _progressStore;

        public CinematicStageLaunchRouter(
            IStageLaunchRouter inner,
            SaveSlotStore saveSlotStore,
            IPendingLaunchSlotProvider pendingLaunchSlotProvider,
            ISlotCinematicPlayer player)
            : this(inner, new SlotCinematicProgressStore(saveSlotStore), pendingLaunchSlotProvider, player)
        {
        }

        internal CinematicStageLaunchRouter(
            IStageLaunchRouter inner,
            SlotCinematicProgressStore progressStore,
            IPendingLaunchSlotProvider pendingLaunchSlotProvider,
            ISlotCinematicPlayer player)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
            _pendingLaunchSlotProvider = pendingLaunchSlotProvider ?? throw new ArgumentNullException(nameof(pendingLaunchSlotProvider));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void Launch(StageNavigationRequest request)
        {
            if (!_pendingLaunchSlotProvider.TryGetPendingLaunchSlot(out var slotNumber) ||
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
