using System;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.EnemyAudio
{
    public enum EnemyAudioCue
    {
        None = 0,
        Move = 1,
        Death = 2,
        Windup = 3,
        Landing = 4,
        Active = 5,
        Recover = 6,
    }

    public readonly struct EnemyAudioRequest
    {
        public EnemyAudioRequest(
            int ownerEntityId,
            EnemyAudioCue cue,
            in AudioPlaybackContext context,
            float delaySeconds = 0f)
        {
            OwnerEntityId = ownerEntityId;
            Cue = cue;
            Context = context;
            DelaySeconds = Math.Max(0f, delaySeconds);
        }

        public int OwnerEntityId { get; }

        public EnemyAudioCue Cue { get; }

        public AudioPlaybackContext Context { get; }

        public float DelaySeconds { get; }
    }

    public static class EnemyAudioCueCatalog
    {
        public static string Format(EnemyAudioCue cue)
        {
            return cue switch
            {
                EnemyAudioCue.None => nameof(EnemyAudioCue.None),
                EnemyAudioCue.Move => nameof(EnemyAudioCue.Move),
                EnemyAudioCue.Death => nameof(EnemyAudioCue.Death),
                EnemyAudioCue.Windup => nameof(EnemyAudioCue.Windup),
                EnemyAudioCue.Landing => nameof(EnemyAudioCue.Landing),
                EnemyAudioCue.Active => nameof(EnemyAudioCue.Active),
                EnemyAudioCue.Recover => nameof(EnemyAudioCue.Recover),
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported enemy audio cue."),
            };
        }
    }
}
