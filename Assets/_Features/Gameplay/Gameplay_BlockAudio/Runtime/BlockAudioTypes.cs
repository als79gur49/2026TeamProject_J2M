using System;
using System.Collections.Generic;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.BlockAudio
{
    public enum BlockAudioCue
    {
        None = 0,
        FlipLanding = 1,
        BoxSlideSolidStop = 2,
        BoxSlideStarted = 3,
    }

    public readonly struct BlockAudioRequest
    {
        public BlockAudioRequest(
            BlockAudioCue cue,
            int ownerEntityId,
            int sequenceId,
            float delaySeconds,
            in AudioPlaybackContext context)
        {
            Cue = cue;
            OwnerEntityId = ownerEntityId;
            SequenceId = sequenceId;
            DelaySeconds = Math.Max(0f, delaySeconds);
            Context = context;
        }

        public BlockAudioCue Cue { get; }

        public int OwnerEntityId { get; }

        public int SequenceId { get; }

        public float DelaySeconds { get; }

        public AudioPlaybackContext Context { get; }
    }

    public static class BlockAudioCueCatalog
    {
        private static readonly BlockAudioCue[] RequiredCues =
        {
            BlockAudioCue.FlipLanding,
            BlockAudioCue.BoxSlideSolidStop,
            BlockAudioCue.BoxSlideStarted,
        };

        public static IReadOnlyList<BlockAudioCue> RequiredOneShotV1 => RequiredCues;

        public static string Format(BlockAudioCue cue)
        {
            return cue switch
            {
                BlockAudioCue.None => nameof(BlockAudioCue.None),
                BlockAudioCue.FlipLanding => nameof(BlockAudioCue.FlipLanding),
                BlockAudioCue.BoxSlideSolidStop => nameof(BlockAudioCue.BoxSlideSolidStop),
                BlockAudioCue.BoxSlideStarted => nameof(BlockAudioCue.BoxSlideStarted),
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported block audio cue."),
            };
        }
    }
}
