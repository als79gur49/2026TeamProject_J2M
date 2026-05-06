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
                if (request.RequestKind != TilePresentationRequestKind.ButtonActivated)
                {
                    continue;
                }

                requests.Add(new TileFeatureAudioRequest(
                    TileFeatureAudioCue.ButtonActivated,
                    request.TileId,
                    request.Cell,
                    request.SourceEntityId,
                    request.OwnerEntityId,
                    request.TeamId,
                    new AudioPlaybackContext(
                        ownerEntityId: request.OwnerEntityId > 0 ? request.OwnerEntityId : null,
                        debugTag: TileFeatureAudioCueCatalog.Format(TileFeatureAudioCue.ButtonActivated))));
            }

            return requests.Count == 0 ? Array.Empty<TileFeatureAudioRequest>() : requests;
        }
    }
}
