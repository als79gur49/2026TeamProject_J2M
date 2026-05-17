using System;
using Game.Feature.Gameplay;

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
            bool isPersistent = false,
            VfxPersistentKey persistentKey = default,
            VfxStyleKey styleKey = default,
            float delaySeconds = 0f)
            : this(
                tickIndex,
                sequenceId,
                presentationSeed,
                sourceEntityId: 0,
                cueId,
                anchor,
                timing,
                isPersistent,
                persistentKey,
                styleKey,
                delaySeconds)
        {
        }

        public GameplayVfxRequest(
            int tickIndex,
            int sequenceId,
            int presentationSeed,
            int sourceEntityId,
            GameplayVfxCueId cueId,
            VfxAnchor anchor,
            VfxTimingKind timing,
            bool isPersistent = false,
            VfxPersistentKey persistentKey = default,
            VfxStyleKey styleKey = default,
            float delaySeconds = 0f)
        {
            TickIndex = tickIndex;
            SequenceId = sequenceId;
            PresentationSeed = presentationSeed;
            SourceEntityId = sourceEntityId;
            CueId = cueId;
            Anchor = anchor;
            Timing = timing;
            IsPersistent = isPersistent;
            PersistentKey = persistentKey;
            StyleKey = styleKey;
            DelaySeconds = Math.Max(0f, delaySeconds);
        }

        public int TickIndex { get; }

        public int SequenceId { get; }

        public int PresentationSeed { get; }

        public int SourceEntityId { get; }

        public GameplayVfxCueId CueId { get; }

        public VfxAnchor Anchor { get; }

        public VfxTimingKind Timing { get; }

        public bool IsPersistent { get; }

        public VfxPersistentKey PersistentKey { get; }

        public VfxStyleKey StyleKey { get; }

        public float DelaySeconds { get; }

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

            var sourceEntityCompare = SourceEntityId.CompareTo(other.SourceEntityId);
            if (sourceEntityCompare != 0)
            {
                return sourceEntityCompare;
            }

            var anchorCompare = Anchor.CompareTo(other.Anchor);
            if (anchorCompare != 0)
            {
                return anchorCompare;
            }

            var persistentKeyCompare = PersistentKey.CompareTo(other.PersistentKey);
            if (persistentKeyCompare != 0)
            {
                return persistentKeyCompare;
            }

            var styleCompare = StyleKey.CompareTo(other.StyleKey);
            if (styleCompare != 0)
            {
                return styleCompare;
            }

            var seedCompare = PresentationSeed.CompareTo(other.PresentationSeed);
            if (seedCompare != 0)
            {
                return seedCompare;
            }

            var timingCompare = Timing.CompareTo(other.Timing);
            if (timingCompare != 0)
            {
                return timingCompare;
            }

            var delayCompare = DelaySeconds.CompareTo(other.DelaySeconds);
            return delayCompare != 0
                ? delayCompare
                : IsPersistent.CompareTo(other.IsPersistent);
        }

        public bool Equals(GameplayVfxRequest other)
        {
            return TickIndex == other.TickIndex
                && SequenceId == other.SequenceId
                && PresentationSeed == other.PresentationSeed
                && SourceEntityId == other.SourceEntityId
                && CueId.Equals(other.CueId)
                && Anchor.Equals(other.Anchor)
                && Timing == other.Timing
                && IsPersistent == other.IsPersistent
                && PersistentKey.Equals(other.PersistentKey)
                && StyleKey.Equals(other.StyleKey)
                && DelaySeconds.Equals(other.DelaySeconds);
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
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ CueId.GetHashCode();
                hash = (hash * 397) ^ Anchor.GetHashCode();
                hash = (hash * 397) ^ (int)Timing;
                hash = (hash * 397) ^ IsPersistent.GetHashCode();
                hash = (hash * 397) ^ PersistentKey.GetHashCode();
                hash = (hash * 397) ^ StyleKey.GetHashCode();
                hash = (hash * 397) ^ DelaySeconds.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return
                $"{nameof(GameplayVfxRequest)}(" +
                $"{nameof(TickIndex)}={TickIndex}, " +
                $"{nameof(SequenceId)}={SequenceId}, " +
                $"{nameof(PresentationSeed)}={PresentationSeed}, " +
                $"{nameof(SourceEntityId)}={SourceEntityId}, " +
                $"{nameof(CueId)}={CueId}, " +
                $"{nameof(Anchor)}={Anchor}, " +
                $"{nameof(Timing)}={Timing}, " +
                $"{nameof(IsPersistent)}={IsPersistent}, " +
                $"{nameof(PersistentKey)}={PersistentKey}, " +
                $"{nameof(StyleKey)}={StyleKey}, " +
                $"{nameof(DelaySeconds)}={DelaySeconds})";
        }
    }
}
