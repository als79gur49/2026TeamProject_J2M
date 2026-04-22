using Game.Shared.Audio;

namespace Game.Feature.Gameplay.ActionAudio
{
    public enum GameplayActionKind
    {
        Push = 0,
        Flip = 1,
    }

    public enum GameplayActionAudioMoment
    {
        Windup = 0,
        Execute = 1,
        Contact = 2,
        ImpactEnemy = 3,
        Blocked = 4,
        Recovery = 5,
    }

    public readonly struct GameplayActionAudioRequest
    {
        public GameplayActionAudioRequest(
            int ownerEntityId,
            GameplayActionKind action,
            GameplayActionAudioMoment moment,
            in AudioPlaybackContext context)
        {
            OwnerEntityId = ownerEntityId;
            Action = action;
            Moment = moment;
            Context = context;
        }

        public int OwnerEntityId { get; }

        public GameplayActionKind Action { get; }

        public GameplayActionAudioMoment Moment { get; }

        public AudioPlaybackContext Context { get; }
    }

    internal static class GameplayActionAudioMomentCatalog
    {
        public static readonly GameplayActionAudioMoment[] OrderedMoments =
        {
            GameplayActionAudioMoment.Windup,
            GameplayActionAudioMoment.Execute,
            GameplayActionAudioMoment.Contact,
            GameplayActionAudioMoment.ImpactEnemy,
            GameplayActionAudioMoment.Blocked,
            GameplayActionAudioMoment.Recovery,
        };
    }

    internal static class GameplayActionAudioDebugTag
    {
        public static string Format(GameplayActionKind action, GameplayActionAudioMoment moment)
        {
            return $"Action:{action}:{moment}";
        }
    }
}
