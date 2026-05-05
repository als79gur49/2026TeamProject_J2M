using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public enum TilePresentationRequestKind
    {
        ButtonActivated = 0,
    }

    public readonly struct TilePresentationRequest
    {
        public TilePresentationRequest(
            TilePresentationRequestKind requestKind,
            int tileId,
            SurfaceCell cell,
            TileFeatureKind tileFeatureKind,
            int sourceEntityId,
            int ownerEntityId,
            int teamId)
        {
            RequestKind = requestKind;
            TileId = tileId;
            Cell = cell;
            TileFeatureKind = tileFeatureKind;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
        }

        public TilePresentationRequestKind RequestKind { get; }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public TileFeatureKind TileFeatureKind { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }
    }

    public sealed class TilePresentationRequestPlanner
    {
        public IReadOnlyList<TilePresentationRequest> BuildRequests(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var tileEvents = presentationData.TileEvents;
            if (tileEvents.Count == 0)
            {
                return Array.Empty<TilePresentationRequest>();
            }

            var requests = new List<TilePresentationRequest>();
            for (var i = 0; i < tileEvents.Count; i++)
            {
                var tileEvent = tileEvents[i];
                if (tileEvent.EventKind != TilePresentationEventKind.ButtonActivated)
                {
                    continue;
                }

                requests.Add(new TilePresentationRequest(
                    TilePresentationRequestKind.ButtonActivated,
                    tileEvent.TileId,
                    tileEvent.Cell,
                    tileEvent.TileFeatureKind,
                    tileEvent.SourceEntityId,
                    tileEvent.OwnerEntityId,
                    tileEvent.TeamId));
            }

            return requests.Count == 0 ? Array.Empty<TilePresentationRequest>() : requests;
        }
    }
}
