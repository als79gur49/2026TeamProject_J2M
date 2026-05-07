using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.GravityFieldAudio
{
    public sealed class GravityFieldAudioRequestPlanner
    {
        public IReadOnlyList<GravityFieldAudioRequest> BuildRequests(
            IReadOnlyList<GravityFieldPresentationRequest> gravityFieldPresentationRequests)
        {
            if (gravityFieldPresentationRequests == null)
            {
                throw new ArgumentNullException(nameof(gravityFieldPresentationRequests));
            }

            if (gravityFieldPresentationRequests.Count == 0)
            {
                return Array.Empty<GravityFieldAudioRequest>();
            }

            var requests = new List<GravityFieldAudioRequest>();
            for (var i = 0; i < gravityFieldPresentationRequests.Count; i++)
            {
                var request = gravityFieldPresentationRequests[i];
                if (!TryMapCue(request.RequestKind, out var cue))
                {
                    continue;
                }

                requests.Add(new GravityFieldAudioRequest(
                    cue,
                    request.EmitterEntityId,
                    request.Cell,
                    new AudioPlaybackContext(
                        ownerEntityId: request.EmitterEntityId > 0 ? request.EmitterEntityId : null,
                        debugTag: GravityFieldAudioCueCatalog.Format(cue))));
            }

            return requests.Count == 0 ? Array.Empty<GravityFieldAudioRequest>() : requests;
        }

        private static bool TryMapCue(
            GravityFieldPresentationRequestKind requestKind,
            out GravityFieldAudioCue cue)
        {
            switch (requestKind)
            {
                case GravityFieldPresentationRequestKind.Activated:
                    cue = GravityFieldAudioCue.Activated;
                    return true;
                case GravityFieldPresentationRequestKind.Expired:
                    cue = GravityFieldAudioCue.Expired;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }
    }
}
