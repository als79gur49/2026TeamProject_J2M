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
            VfxPersistentKey persistentKey = default)
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
        }

        public int TickIndex { get; }

        public int SequenceId { get; }

        public int PresentationSeed { get; }

        public GameplayVfxCueId CueId { get; }

        public VfxAnchor Anchor { get; }

        public VfxTimingKind Timing { get; }

        public VfxPlaybackMode PlaybackMode { get; }

        public VfxStopPolicy StopPolicy { get; }

        public bool IsPersistent { get; }

        public VfxPersistentKey PersistentKey { get; }

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
            return persistentCompare != 0 ? persistentCompare : PersistentKey.CompareTo(other.PersistentKey);
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
                && PersistentKey.Equals(other.PersistentKey);
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
                return hash;
            }
        }
    }
}
