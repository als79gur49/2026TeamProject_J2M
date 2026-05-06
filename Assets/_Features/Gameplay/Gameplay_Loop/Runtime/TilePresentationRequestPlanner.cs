using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public enum TilePresentationRequestKind
    {
        ButtonActivated = 0,
        DestroyTileTriggered = 1,
        SlideTileRedirected = 2,
        BarricadeBlocked = 3,
        BarricadeCrushed = 4,
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
            int teamId,
            int targetEntityId = 0,
            Direction direction = Direction.None)
        {
            RequestKind = requestKind;
            TileId = tileId;
            Cell = cell;
            TileFeatureKind = tileFeatureKind;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
            TargetEntityId = targetEntityId;
            Direction = direction;
        }

        public TilePresentationRequestKind RequestKind { get; }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public TileFeatureKind TileFeatureKind { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }

        public int TargetEntityId { get; }

        public Direction Direction { get; }
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
                if (!TryMapRequestKind(tileEvent.EventKind, out var requestKind))
                {
                    continue;
                }

                requests.Add(new TilePresentationRequest(
                    requestKind,
                    tileEvent.TileId,
                    tileEvent.Cell,
                    tileEvent.TileFeatureKind,
                    tileEvent.SourceEntityId,
                    tileEvent.OwnerEntityId,
                    tileEvent.TeamId,
                    tileEvent.TargetEntityId,
                    tileEvent.Direction));
            }

            return requests.Count == 0 ? Array.Empty<TilePresentationRequest>() : requests;
        }

        private static bool TryMapRequestKind(
            TilePresentationEventKind eventKind,
            out TilePresentationRequestKind requestKind)
        {
            switch (eventKind)
            {
                case TilePresentationEventKind.ButtonActivated:
                    requestKind = TilePresentationRequestKind.ButtonActivated;
                    return true;
                case TilePresentationEventKind.DestroyTileTriggered:
                    requestKind = TilePresentationRequestKind.DestroyTileTriggered;
                    return true;
                case TilePresentationEventKind.SlideTileRedirected:
                    requestKind = TilePresentationRequestKind.SlideTileRedirected;
                    return true;
                case TilePresentationEventKind.BarricadeBlocked:
                    requestKind = TilePresentationRequestKind.BarricadeBlocked;
                    return true;
                case TilePresentationEventKind.BarricadeCrushed:
                    requestKind = TilePresentationRequestKind.BarricadeCrushed;
                    return true;
                default:
                    requestKind = default;
                    return false;
            }
        }
    }
}
