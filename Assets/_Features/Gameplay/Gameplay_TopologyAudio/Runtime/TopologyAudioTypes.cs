using System;
using System.Collections.Generic;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.TopologyAudio
{
    public enum TopologyAudioCue
    {
        None = 0,
        MapRotateStarted = 1,
    }

    public readonly struct TopologyAudioRequest
    {
        public TopologyAudioRequest(
            TopologyAudioCue cue,
            int sequenceId,
            in AudioPlaybackContext context)
        {
            Cue = cue;
            SequenceId = sequenceId;
            Context = context;
        }

        public TopologyAudioCue Cue { get; }

        public int SequenceId { get; }

        public AudioPlaybackContext Context { get; }
    }

    public static class TopologyAudioCueCatalog
    {
        private static readonly TopologyAudioCue[] RequiredCues =
        {
            TopologyAudioCue.MapRotateStarted,
        };

        public static IReadOnlyList<TopologyAudioCue> RequiredOneShotV1 => RequiredCues;

        public static string Format(TopologyAudioCue cue)
        {
            return cue switch
            {
                TopologyAudioCue.None => nameof(TopologyAudioCue.None),
                TopologyAudioCue.MapRotateStarted => nameof(TopologyAudioCue.MapRotateStarted),
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported topology audio cue."),
            };
        }
    }
}
