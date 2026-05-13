using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.TileFeatureAudio
{
    public enum TileFeatureAudioCue
    {
        None = 0,
        ButtonActivated = 1,
        DestroyTileTriggered = 2,
        SlideTileRedirected = 3,
        BarricadeBlocked = 4,
        BarricadeCrushed = 5,
        ExitOpened = 6,
        ExitEntered = 7,
        MoonBlockGenerated = 8,
        DestroyTileActivated = 9,
        DestroyTileDeactivated = 10,
        BarricadeActivated = 11,
        BarricadeDeactivated = 12,
    }

    public readonly struct TileFeatureAudioRequest
    {
        public TileFeatureAudioRequest(
            TileFeatureAudioCue cue,
            int tileId,
            SurfaceCell cell,
            int sourceEntityId,
            int ownerEntityId,
            int teamId,
            in AudioPlaybackContext context,
            int targetEntityId = 0)
        {
            Cue = cue;
            TileId = tileId;
            Cell = cell;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
            Context = context;
            TargetEntityId = targetEntityId;
        }

        public TileFeatureAudioCue Cue { get; }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }

        public AudioPlaybackContext Context { get; }

        public int TargetEntityId { get; }
    }

    public static class TileFeatureAudioCueCatalog
    {
        private static readonly TileFeatureAudioCue[] RequiredCues =
        {
            TileFeatureAudioCue.ButtonActivated,
        };

        public static IReadOnlyList<TileFeatureAudioCue> RequiredOneShotV1 => RequiredCues;

        public static string Format(TileFeatureAudioCue cue)
        {
            return cue switch
            {
                TileFeatureAudioCue.None => nameof(TileFeatureAudioCue.None),
                TileFeatureAudioCue.ButtonActivated => nameof(TileFeatureAudioCue.ButtonActivated),
                TileFeatureAudioCue.DestroyTileTriggered => nameof(TileFeatureAudioCue.DestroyTileTriggered),
                TileFeatureAudioCue.SlideTileRedirected => nameof(TileFeatureAudioCue.SlideTileRedirected),
                TileFeatureAudioCue.BarricadeBlocked => nameof(TileFeatureAudioCue.BarricadeBlocked),
                TileFeatureAudioCue.BarricadeCrushed => nameof(TileFeatureAudioCue.BarricadeCrushed),
                TileFeatureAudioCue.ExitOpened => nameof(TileFeatureAudioCue.ExitOpened),
                TileFeatureAudioCue.ExitEntered => nameof(TileFeatureAudioCue.ExitEntered),
                TileFeatureAudioCue.MoonBlockGenerated => nameof(TileFeatureAudioCue.MoonBlockGenerated),
                TileFeatureAudioCue.DestroyTileActivated => nameof(TileFeatureAudioCue.DestroyTileActivated),
                TileFeatureAudioCue.DestroyTileDeactivated => nameof(TileFeatureAudioCue.DestroyTileDeactivated),
                TileFeatureAudioCue.BarricadeActivated => nameof(TileFeatureAudioCue.BarricadeActivated),
                TileFeatureAudioCue.BarricadeDeactivated => nameof(TileFeatureAudioCue.BarricadeDeactivated),
                _ => throw new System.ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported tile feature audio cue."),
            };
        }
    }
}
