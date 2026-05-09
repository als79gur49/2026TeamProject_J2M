using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.GravityFieldAudio
{
    public enum GravityFieldAudioCue
    {
        None = 0,
        Activated = 1,
        Expired = 2,
        LockedBox = 3,
    }

    public readonly struct GravityFieldAudioRequest
    {
        public GravityFieldAudioRequest(
            GravityFieldAudioCue cue,
            int emitterEntityId,
            SurfaceCell cell,
            in AudioPlaybackContext context,
            int targetEntityId = 0,
            GravityFieldLockedBoxPayload lockedBoxPayload = default)
        {
            Cue = cue;
            EmitterEntityId = emitterEntityId;
            Cell = cell;
            Context = context;
            LockedBoxPayload = lockedBoxPayload;
            TargetEntityId = targetEntityId > 0
                ? targetEntityId
                : lockedBoxPayload.TargetEntityId;
        }

        public GravityFieldAudioCue Cue { get; }

        public int EmitterEntityId { get; }

        public SurfaceCell Cell { get; }

        public int TargetEntityId { get; }

        public GravityFieldLockedBoxPayload LockedBoxPayload { get; }

        public AudioPlaybackContext Context { get; }
    }

    public static class GravityFieldAudioCueCatalog
    {
        private static readonly GravityFieldAudioCue[] RequiredCues = { };

        public static IReadOnlyList<GravityFieldAudioCue> RequiredOneShotV1 => RequiredCues;

        public static string Format(GravityFieldAudioCue cue)
        {
            return cue switch
            {
                GravityFieldAudioCue.None => nameof(GravityFieldAudioCue.None),
                GravityFieldAudioCue.Activated => nameof(GravityFieldAudioCue.Activated),
                GravityFieldAudioCue.Expired => nameof(GravityFieldAudioCue.Expired),
                GravityFieldAudioCue.LockedBox => nameof(GravityFieldAudioCue.LockedBox),
                _ => throw new System.ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported GravityField audio cue."),
            };
        }
    }
}
