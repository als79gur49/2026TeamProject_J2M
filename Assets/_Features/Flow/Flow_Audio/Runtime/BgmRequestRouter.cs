using System;
using System.Collections.Generic;
using Game.Shared.Audio;

namespace Game.Feature.Flow.Audio
{
    public enum BgmRequestSourceKind
    {
        SceneDefault = 0,
        StageGameplay = 1,
    }

    public enum BgmRequestPriority
    {
        SceneDefault = 100,
        StageGameplay = 300,
    }

    public readonly struct BgmFlowRequest
    {
        private BgmFlowRequest(
            BgmRequestSourceKind sourceKind,
            BgmRequestPriority priority,
            BgmProfile profile,
            bool stopBgm)
        {
            SourceKind = sourceKind;
            Priority = priority;
            Profile = profile;
            StopBgm = stopBgm;
        }

        public BgmRequestSourceKind SourceKind { get; }

        public BgmRequestPriority Priority { get; }

        public BgmProfile Profile { get; }

        public bool StopBgm { get; }

        public bool HasProfile => Profile != null;

        public static BgmFlowRequest ProfileRequest(
            BgmRequestSourceKind sourceKind,
            BgmRequestPriority priority,
            BgmProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new BgmFlowRequest(sourceKind, priority, profile, stopBgm: false);
        }

        public static BgmFlowRequest StopRequest(
            BgmRequestSourceKind sourceKind,
            BgmRequestPriority priority)
        {
            return new BgmFlowRequest(sourceKind, priority, null, stopBgm: true);
        }
    }

    public sealed class BgmRequestRouter
    {
        private const string PlaybackSuppressionAlreadyActiveMessage =
            "BgmRequestRouter supports only one active playback suppression lease.";

        private readonly IBgmFlowCoordinator coordinator;
        private readonly Dictionary<BgmRequestSourceKind, BgmFlowRequest> requests = new();
        private BgmFlowRequest? activeRequest;
        private long nextPlaybackSuppressionToken;
        private long activePlaybackSuppressionToken;

        public BgmRequestRouter(IBgmFlowCoordinator coordinator)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public BgmFlowRequest? ActiveRequest => activeRequest;

        public void Submit(BgmFlowRequest request)
        {
            requests[request.SourceKind] = request;
            if (activePlaybackSuppressionToken != 0)
            {
                SelectHighestPriorityRequest();
                return;
            }

            ApplyHighestPriorityRequest();
        }

        public BgmPlaybackSuppressionLease BeginPlaybackSuppression()
        {
            if (activePlaybackSuppressionToken != 0)
            {
                throw new InvalidOperationException(
                    PlaybackSuppressionAlreadyActiveMessage);
            }

            var token = ++nextPlaybackSuppressionToken;
            coordinator.StopCurrent();
            activePlaybackSuppressionToken = token;
            return new BgmPlaybackSuppressionLease(this, token);
        }

        private void ApplyHighestPriorityRequest()
        {
            if (!SelectHighestPriorityRequest())
            {
                return;
            }

            var request = activeRequest.Value;
            if (request.HasProfile)
            {
                coordinator.RequestSceneDefault(request.Profile);
                return;
            }

            if (request.StopBgm)
            {
                coordinator.StopCurrent();
            }
        }

        private bool SelectHighestPriorityRequest()
        {
            if (!TryGetHighestPriorityRequest(out var request))
            {
                activeRequest = null;
                return false;
            }

            activeRequest = request;
            return true;
        }

        internal void ReleasePlaybackSuppression(
            long token,
            bool restoreCurrentSelection)
        {
            if (token == 0 || activePlaybackSuppressionToken != token)
            {
                return;
            }

            activePlaybackSuppressionToken = 0;
            if (restoreCurrentSelection)
            {
                ApplyHighestPriorityRequest();
            }
        }

        private bool TryGetHighestPriorityRequest(out BgmFlowRequest request)
        {
            request = default;
            var hasRequest = false;
            foreach (var candidate in requests.Values)
            {
                if (!hasRequest || candidate.Priority > request.Priority)
                {
                    request = candidate;
                    hasRequest = true;
                }
            }

            return hasRequest;
        }
    }

    public sealed class BgmPlaybackSuppressionLease : IDisposable
    {
        private BgmRequestRouter owner;
        private readonly long token;

        internal BgmPlaybackSuppressionLease(
            BgmRequestRouter owner,
            long token)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.token = token;
        }

        public void ReleaseWithoutRestore()
        {
            Release(restoreCurrentSelection: false);
        }

        public void Dispose()
        {
            Release(restoreCurrentSelection: true);
        }

        private void Release(bool restoreCurrentSelection)
        {
            var currentOwner = owner;
            if (currentOwner == null)
            {
                return;
            }

            owner = null;
            currentOwner.ReleasePlaybackSuppression(
                token,
                restoreCurrentSelection);
        }
    }
}
