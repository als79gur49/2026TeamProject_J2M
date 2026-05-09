using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.TileFeatureAudio
{
    public sealed class TileFeatureAudioRequestPlanner
    {
        public IReadOnlyList<TileFeatureAudioRequest> BuildRequests(
            IReadOnlyList<TilePresentationRequest> tilePresentationRequests)
        {
            if (tilePresentationRequests == null)
            {
                throw new ArgumentNullException(nameof(tilePresentationRequests));
            }

            if (tilePresentationRequests.Count == 0)
            {
                return Array.Empty<TileFeatureAudioRequest>();
            }

            var requests = new List<TileFeatureAudioRequest>();
            for (var i = 0; i < tilePresentationRequests.Count; i++)
            {
                var request = tilePresentationRequests[i];
                if (!TryMapCue(request.RequestKind, out var cue))
                {
                    continue;
                }

                requests.Add(new TileFeatureAudioRequest(
                    cue,
                    request.TileId,
                    request.Cell,
                    request.SourceEntityId,
                    request.OwnerEntityId,
                    request.TeamId,
                    new AudioPlaybackContext(
                        ownerEntityId: request.OwnerEntityId > 0 ? request.OwnerEntityId : null,
                        debugTag: TileFeatureAudioCueCatalog.Format(cue)),
                    request.TargetEntityId));
            }

            return requests.Count == 0 ? Array.Empty<TileFeatureAudioRequest>() : requests;
        }

        private static bool TryMapCue(TilePresentationRequestKind requestKind, out TileFeatureAudioCue cue)
        {
            switch (requestKind)
            {
                case TilePresentationRequestKind.ButtonActivated:
                    cue = TileFeatureAudioCue.ButtonActivated;
                    return true;
                case TilePresentationRequestKind.DestroyTileTriggered:
                    cue = TileFeatureAudioCue.DestroyTileTriggered;
                    return true;
                case TilePresentationRequestKind.SlideTileRedirected:
                    cue = TileFeatureAudioCue.SlideTileRedirected;
                    return true;
                case TilePresentationRequestKind.BarricadeBlocked:
                    cue = TileFeatureAudioCue.BarricadeBlocked;
                    return true;
                case TilePresentationRequestKind.BarricadeCrushed:
                    cue = TileFeatureAudioCue.BarricadeCrushed;
                    return true;
                case TilePresentationRequestKind.ExitOpened:
                    cue = TileFeatureAudioCue.ExitOpened;
                    return true;
                case TilePresentationRequestKind.ExitEntered:
                    cue = TileFeatureAudioCue.ExitEntered;
                    return true;
                case TilePresentationRequestKind.MoonBlockGenerated:
                    cue = TileFeatureAudioCue.MoonBlockGenerated;
                    return true;
                case TilePresentationRequestKind.MoonBlockGeneratorBlocked:
                    cue = TileFeatureAudioCue.MoonBlockGeneratorBlocked;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }
    }
}
