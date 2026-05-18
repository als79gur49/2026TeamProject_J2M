using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
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
        MoonBlockGeneratorBlocked = 9,
        DestroyTileActivated = 10,
        DestroyTileDeactivated = 11,
        BarricadeActivated = 12,
        BarricadeDeactivated = 13,
        TileFeatureOnBurst = 14,
        TileFeatureOffBurst = 15,
    }

    public enum TileFeatureAudioBurstKind
    {
        None = 0,
        On = 1,
        Off = 2,
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
            int targetEntityId = 0,
            MoonBlockGeneratorBlockedPayload moonBlockGeneratorBlockedPayload = default,
            int count = 1,
            int tickIndex = 0,
            TileFeatureAudioBurstKind burstKind = TileFeatureAudioBurstKind.None,
            TileFeatureAudioCue representativeCue = TileFeatureAudioCue.None)
        {
            Cue = cue;
            TileId = tileId;
            Cell = cell;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
            Context = context;
            TargetEntityId = targetEntityId;
            MoonBlockGeneratorBlockedPayload = moonBlockGeneratorBlockedPayload;
            Count = Math.Max(1, count);
            TickIndex = tickIndex;
            BurstKind = burstKind;
            RepresentativeCue = representativeCue == TileFeatureAudioCue.None ? cue : representativeCue;
        }

        public TileFeatureAudioCue Cue { get; }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }

        public AudioPlaybackContext Context { get; }

        public int TargetEntityId { get; }

        public MoonBlockGeneratorBlockedPayload MoonBlockGeneratorBlockedPayload { get; }

        public int Count { get; }

        public int TickIndex { get; }

        public TileFeatureAudioBurstKind BurstKind { get; }

        public TileFeatureAudioCue RepresentativeCue { get; }
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
                TileFeatureAudioCue.MoonBlockGeneratorBlocked => nameof(TileFeatureAudioCue.MoonBlockGeneratorBlocked),
                TileFeatureAudioCue.DestroyTileActivated => nameof(TileFeatureAudioCue.DestroyTileActivated),
                TileFeatureAudioCue.DestroyTileDeactivated => nameof(TileFeatureAudioCue.DestroyTileDeactivated),
                TileFeatureAudioCue.BarricadeActivated => nameof(TileFeatureAudioCue.BarricadeActivated),
                TileFeatureAudioCue.BarricadeDeactivated => nameof(TileFeatureAudioCue.BarricadeDeactivated),
                TileFeatureAudioCue.TileFeatureOnBurst => nameof(TileFeatureAudioCue.TileFeatureOnBurst),
                TileFeatureAudioCue.TileFeatureOffBurst => nameof(TileFeatureAudioCue.TileFeatureOffBurst),
                _ => throw new System.ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported tile feature audio cue."),
            };
        }
    }
}
