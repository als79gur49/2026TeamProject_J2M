using System;

namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct GameplayVfxRequest : IEquatable<GameplayVfxRequest>, IComparable<GameplayVfxRequest>
    {
        public GameplayVfxRequest(
            int tickIndex,
            int sequenceId,
            int presentationSeed,
            GameplayVfxCueId cueId,
            VfxAnchor anchor,
            VfxTimingKind timing,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            bool isPersistent = false,
            VfxPersistentKey persistentKey = default,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional)
        {
            TickIndex = tickIndex;
            SequenceId = sequenceId;
            PresentationSeed = presentationSeed;
            CueId = cueId;
            Anchor = anchor;
            Timing = timing;
            PlaybackMode = playbackMode;
            StopPolicy = stopPolicy;
            IsPersistent = isPersistent;
            PersistentKey = persistentKey;
            MissingAnchorPolicy = missingAnchorPolicy;
        }

        public int TickIndex { get; }

        public int SequenceId { get; }

        public int PresentationSeed { get; }

        public GameplayVfxCueId CueId { get; }

        public VfxAnchor Anchor { get; }

        public VfxTimingKind Timing { get; }

        // Foundation-stage execution hint. Binding/profile data may later own final playback policy.
        public VfxPlaybackMode PlaybackMode { get; }

        // Foundation-stage execution hint. Binding/profile data may later own final stop policy.
        public VfxStopPolicy StopPolicy { get; }

        public bool IsPersistent { get; }

        public VfxPersistentKey PersistentKey { get; }

        public VfxMissingAnchorPolicy MissingAnchorPolicy { get; }

        public int CompareTo(GameplayVfxRequest other)
        {
            var tickCompare = TickIndex.CompareTo(other.TickIndex);
            if (tickCompare != 0)
            {
                return tickCompare;
            }

            var familyCompare = CueId.Family.CompareTo(other.CueId.Family);
            if (familyCompare != 0)
            {
                return familyCompare;
            }

            var sequenceCompare = SequenceId.CompareTo(other.SequenceId);
            if (sequenceCompare != 0)
            {
                return sequenceCompare;
            }

            var cueCompare = CueId.Code.CompareTo(other.CueId.Code);
            if (cueCompare != 0)
            {
                return cueCompare;
            }

            var anchorCompare = Anchor.CompareTo(other.Anchor);
            if (anchorCompare != 0)
            {
                return anchorCompare;
            }

            var timingCompare = Timing.CompareTo(other.Timing);
            if (timingCompare != 0)
            {
                return timingCompare;
            }

            var playbackCompare = PlaybackMode.CompareTo(other.PlaybackMode);
            if (playbackCompare != 0)
            {
                return playbackCompare;
            }

            var stopCompare = StopPolicy.CompareTo(other.StopPolicy);
            if (stopCompare != 0)
            {
                return stopCompare;
            }

            var persistentCompare = IsPersistent.CompareTo(other.IsPersistent);
            if (persistentCompare != 0)
            {
                return persistentCompare;
            }

            var persistentKeyCompare = PersistentKey.CompareTo(other.PersistentKey);
            return persistentKeyCompare != 0
                ? persistentKeyCompare
                : MissingAnchorPolicy.CompareTo(other.MissingAnchorPolicy);
        }

        public bool Equals(GameplayVfxRequest other)
        {
            return TickIndex == other.TickIndex
                && SequenceId == other.SequenceId
                && PresentationSeed == other.PresentationSeed
                && CueId.Equals(other.CueId)
                && Anchor.Equals(other.Anchor)
                && Timing == other.Timing
                && PlaybackMode == other.PlaybackMode
                && StopPolicy == other.StopPolicy
                && IsPersistent == other.IsPersistent
                && PersistentKey.Equals(other.PersistentKey)
                && MissingAnchorPolicy == other.MissingAnchorPolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayVfxRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ SequenceId;
                hash = (hash * 397) ^ PresentationSeed;
                hash = (hash * 397) ^ CueId.GetHashCode();
                hash = (hash * 397) ^ Anchor.GetHashCode();
                hash = (hash * 397) ^ (int)Timing;
                hash = (hash * 397) ^ (int)PlaybackMode;
                hash = (hash * 397) ^ (int)StopPolicy;
                hash = (hash * 397) ^ IsPersistent.GetHashCode();
                hash = (hash * 397) ^ PersistentKey.GetHashCode();
                hash = (hash * 397) ^ (int)MissingAnchorPolicy;
                return hash;
            }
        }
    }
}
