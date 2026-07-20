using System;
using Game.Feature.Stages;

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
                LaunchOrClear(request, handoff.Token);
                return;
            }

            try
            {
                _player.PlayIntro(result =>
                {
                    if (result.Kind == CinematicPlaybackCompletionKind.Failed ||
                        result.Kind == CinematicPlaybackCompletionKind.Cancelled)
                    {
                        _launchHandoffStore.TryClear(handoff.Token);
                        return;
                    }

                    try
                    {
                        _progressStore.MarkIntroPlayed(handoff.SlotNumber);
                        LaunchOrClear(request, handoff.Token);
                    }
                    catch
                    {
                        _launchHandoffStore.TryClear(handoff.Token);
                        throw;
                    }
                });
            }
            catch
            {
                _launchHandoffStore.TryClear(handoff.Token);
                throw;
            }
        }

        private void LaunchOrClear(StageNavigationRequest request, Guid token)
        {
            try
            {
                _inner.Launch(request);
            }
            catch
            {
                _launchHandoffStore.TryClear(token);
                throw;
            }
        }
    }
}
