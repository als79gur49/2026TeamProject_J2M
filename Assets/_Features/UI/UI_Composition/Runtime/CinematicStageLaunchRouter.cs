using System;
using System.Threading;
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
                LaunchOrClear(request, handoff);
                return;
            }

            var terminalClaimed = 0;
            try
            {
                _player.PlayIntro(result =>
                {
                    if (Volatile.Read(ref terminalClaimed) != 0 ||
                        !IsCurrentHandoff(handoff) ||
                        Interlocked.CompareExchange(ref terminalClaimed, 1, 0) != 0)
                    {
                        return;
                    }

                    switch (result.Kind)
                    {
                        case CinematicPlaybackCompletionKind.Completed:
                        case CinematicPlaybackCompletionKind.Skipped:
                            LaunchOrClear(request, handoff);
                            _progressStore.MarkIntroPlayed(handoff.SlotNumber);
                            return;

                        case CinematicPlaybackCompletionKind.Failed:
                        case CinematicPlaybackCompletionKind.Cancelled:
                        default:
                            TryClearCurrentHandoff(handoff);
                            return;
                    }
                });
            }
            catch
            {
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
    }
}
