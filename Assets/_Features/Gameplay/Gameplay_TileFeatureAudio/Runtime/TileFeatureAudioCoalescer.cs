using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.TileFeatureAudio
{
    public sealed class TileFeatureAudioCoalescer
    {
        public IReadOnlyList<TileFeatureAudioRequest> Coalesce(
            IReadOnlyList<TileFeatureAudioRequest> requests,
            int tickIndex)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (requests.Count == 0)
            {
                return Array.Empty<TileFeatureAudioRequest>();
            }

            var output = new List<TileFeatureAudioRequest>();
            TileFeatureAudioRequest firstOn = default;
            TileFeatureAudioRequest firstOff = default;
            var onCount = 0;
            var offCount = 0;

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (TryGetBurstKind(request.Cue, out var burstKind))
                {
                    if (burstKind == TileFeatureAudioBurstKind.On)
                    {
                        if (onCount == 0)
                        {
                            firstOn = request;
                        }

                        onCount++;
                    }
                    else
                    {
                        if (offCount == 0)
                        {
                            firstOff = request;
                        }

                        offCount++;
                    }

                    continue;
                }

                output.Add(request);
            }

            if (onCount == 1)
            {
                output.Add(firstOn);
            }
            else if (onCount > 1)
            {
                output.Add(CreateBurstRequest(firstOn, TileFeatureAudioBurstKind.On, onCount, tickIndex));
            }

            if (offCount == 1)
            {
                output.Add(firstOff);
            }
            else if (offCount > 1)
            {
                output.Add(CreateBurstRequest(firstOff, TileFeatureAudioBurstKind.Off, offCount, tickIndex));
            }

            return output.Count == 0 ? Array.Empty<TileFeatureAudioRequest>() : output;
        }

        internal static bool TryGetBurstKind(TileFeatureAudioCue cue, out TileFeatureAudioBurstKind kind)
        {
            switch (cue)
            {
                case TileFeatureAudioCue.DestroyTileActivated:
                case TileFeatureAudioCue.BarricadeActivated:
                    kind = TileFeatureAudioBurstKind.On;
                    return true;
                case TileFeatureAudioCue.DestroyTileDeactivated:
                case TileFeatureAudioCue.BarricadeDeactivated:
                    kind = TileFeatureAudioBurstKind.Off;
                    return true;
                default:
                    kind = TileFeatureAudioBurstKind.None;
                    return false;
            }
        }

        internal static float ResolveBurstVolumeMultiplier(int count)
        {
            if (count <= 1)
            {
                return 1f;
            }

            return count <= 4 ? 1.05f : 1.1f;
        }

        private static TileFeatureAudioRequest CreateBurstRequest(
            in TileFeatureAudioRequest source,
            TileFeatureAudioBurstKind kind,
            int count,
            int tickIndex)
        {
            var cue = kind == TileFeatureAudioBurstKind.On
                ? TileFeatureAudioCue.TileFeatureOnBurst
                : TileFeatureAudioCue.TileFeatureOffBurst;
            var context = new AudioPlaybackContext(
                ResolveBurstVolumeMultiplier(count),
                ownerEntityId: source.OwnerEntityId > 0 ? source.OwnerEntityId : null,
                debugTag: TileFeatureAudioCueCatalog.Format(cue));

            return new TileFeatureAudioRequest(
                cue,
                source.TileId,
                source.Cell,
                source.SourceEntityId,
                source.OwnerEntityId,
                source.TeamId,
                context,
                source.TargetEntityId,
                source.MoonBlockGeneratorBlockedPayload,
                count,
                tickIndex,
                kind,
                source.Cue);
        }
    }
}
