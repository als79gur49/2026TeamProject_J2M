using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.PlayerLocomotionAudio
{
    public enum PlayerLocomotionAudioCue
    {
        None = 0,
        WalkStep = 1,
        TopologyTransitionBlocked = 2,
    }

    public readonly struct PlayerLocomotionAudioRequest
    {
        public PlayerLocomotionAudioRequest(
            PlayerLocomotionAudioCue cue,
            int ownerEntityId,
            int sequenceId,
            float delaySeconds,
            in AudioPlaybackContext context,
            Direction gateDirection = Direction.None,
            SurfaceCell gateCandidateCell = default)
        {
            Cue = cue;
            OwnerEntityId = ownerEntityId;
            SequenceId = sequenceId;
            DelaySeconds = Math.Max(0f, delaySeconds);
            Context = context;
            GateDirection = gateDirection;
            GateCandidateCell = gateCandidateCell;
        }

        public PlayerLocomotionAudioCue Cue { get; }

        public int OwnerEntityId { get; }

        public int SequenceId { get; }

        public float DelaySeconds { get; }

        public AudioPlaybackContext Context { get; }

        public Direction GateDirection { get; }

        public SurfaceCell GateCandidateCell { get; }
    }

    public static class PlayerLocomotionAudioCueCatalog
    {
        private static readonly PlayerLocomotionAudioCue[] RequiredCues =
        {
            PlayerLocomotionAudioCue.WalkStep,
            PlayerLocomotionAudioCue.TopologyTransitionBlocked,
        };

        public static IReadOnlyList<PlayerLocomotionAudioCue> RequiredOneShotV1 => RequiredCues;

        public static string Format(PlayerLocomotionAudioCue cue)
        {
            return cue switch
            {
                PlayerLocomotionAudioCue.None => nameof(PlayerLocomotionAudioCue.None),
                PlayerLocomotionAudioCue.WalkStep => nameof(PlayerLocomotionAudioCue.WalkStep),
                PlayerLocomotionAudioCue.TopologyTransitionBlocked => nameof(PlayerLocomotionAudioCue.TopologyTransitionBlocked),
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported player locomotion audio cue."),
            };
        }
    }
}
