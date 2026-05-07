using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public enum GravityFieldPresentationRequestKind
    {
        Activated = 0,
        Expired = 1,
    }

    public readonly struct GravityFieldPresentationRequest
    {
        public GravityFieldPresentationRequest(
            GravityFieldPresentationRequestKind requestKind,
            int emitterEntityId,
            SurfaceCell cell)
        {
            RequestKind = requestKind;
            EmitterEntityId = emitterEntityId;
            Cell = cell;
        }

        public GravityFieldPresentationRequestKind RequestKind { get; }

        public int EmitterEntityId { get; }

        public SurfaceCell Cell { get; }
    }

    public sealed class GravityFieldPresentationRequestPlanner
    {
        public IReadOnlyList<GravityFieldPresentationRequest> BuildRequests(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var events = presentationData.GravityFieldEvents;
            if (events.Count == 0)
            {
                return Array.Empty<GravityFieldPresentationRequest>();
            }

            var requests = new List<GravityFieldPresentationRequest>();
            for (var i = 0; i < events.Count; i++)
            {
                var presentationEvent = events[i];
                if (!TryMapRequestKind(presentationEvent.EventKind, out var requestKind))
                {
                    continue;
                }

                requests.Add(new GravityFieldPresentationRequest(
                    requestKind,
                    presentationEvent.EmitterEntityId,
                    presentationEvent.Cell));
            }

            return requests.Count == 0 ? Array.Empty<GravityFieldPresentationRequest>() : requests;
        }

        private static bool TryMapRequestKind(
            GravityFieldPresentationEventKind eventKind,
            out GravityFieldPresentationRequestKind requestKind)
        {
            switch (eventKind)
            {
                case GravityFieldPresentationEventKind.Activated:
                    requestKind = GravityFieldPresentationRequestKind.Activated;
                    return true;
                case GravityFieldPresentationEventKind.Expired:
                    requestKind = GravityFieldPresentationRequestKind.Expired;
                    return true;
                default:
                    requestKind = default;
                    return false;
            }
        }
    }
}
