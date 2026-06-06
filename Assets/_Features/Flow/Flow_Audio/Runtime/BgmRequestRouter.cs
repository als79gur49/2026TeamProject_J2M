using System;
using System.Collections.Generic;
using Game.Shared.Audio;

namespace Game.Feature.Flow.Audio
{
    public enum BgmRequestSourceKind
    {
        SceneDefault = 0,
        StageGameplay = 1,
        StageResult = 2,
        Cutscene = 3,
    }

    public enum BgmRequestPriority
    {
        SceneDefault = 100,
        StageGameplay = 300,
        StageResult = 400,
        Cutscene = 500,
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
        private readonly IBgmFlowCoordinator coordinator;
        private readonly Dictionary<BgmRequestSourceKind, BgmFlowRequest> requests = new();
        private BgmFlowRequest? activeRequest;

        public BgmRequestRouter(IBgmFlowCoordinator coordinator)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public BgmFlowRequest? ActiveRequest => activeRequest;

        public void Submit(BgmFlowRequest request)
        {
            requests[request.SourceKind] = request;
            ApplyHighestPriorityRequest();
        }

        private void ApplyHighestPriorityRequest()
        {
            if (!TryGetHighestPriorityRequest(out var request))
            {
                return;
            }

            activeRequest = request;
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
}
