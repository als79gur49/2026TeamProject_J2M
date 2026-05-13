using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.PlayerLocomotionAudio
{
    public enum PlayerLocomotionAudioCue
    {
        None = 0,
        WalkStep = 1,
    }

    public static class PlayerLocomotionAudioCueCatalog
    {
        private static readonly PlayerLocomotionAudioCue[] RequiredCues =
        {
            PlayerLocomotionAudioCue.WalkStep,
        };

        public static IReadOnlyList<PlayerLocomotionAudioCue> RequiredOneShotV1 => RequiredCues;

        public static string Format(PlayerLocomotionAudioCue cue)
        {
            return cue switch
            {
                PlayerLocomotionAudioCue.None => nameof(PlayerLocomotionAudioCue.None),
                PlayerLocomotionAudioCue.WalkStep => nameof(PlayerLocomotionAudioCue.WalkStep),
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported player locomotion audio cue."),
            };
        }
    }
}
