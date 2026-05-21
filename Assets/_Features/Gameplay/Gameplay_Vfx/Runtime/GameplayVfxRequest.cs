using System;
using Game.Feature.Gameplay;

namespace Game.Feature.Gameplay.Vfx
{
    public enum GameplayVfxTopologyAnchorMode
    {
        Committed = 0,
        SourceDuringTransition = 1,
        DestinationAfterTransition = 2,
    }

    public enum GameplayVfxTopologyStopMode
    {
        Default = 0,
        HardClearAtTransitionStart = 1,
        TopologyHelperExempt = 2,
    }

    public enum GameplayVfxTopologySpawnMode
    {
        Default = 0,
        SuppressDuringTransition = 1,
        DeferUntilCompletionWithDelay = 2,
        TopologyHelperExempt = 3,
    }

    public enum GameplayVfxCompletionReplayPolicy
    {
        None = 0,
        SteadyStatePersistentLoop = 1,
    }

    public static class GameplayVfxTopologyHelperExemptionPolicy
    {
        public static bool IsTopologyHelperCue(GameplayVfxCueId cueId)
        {
            return cueId.Equals(GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail));
        }

        public static bool AllowsStopExemption(
            GameplayVfxCueId cueId,
            GameplayVfxTopologyStopMode stopMode)
        {
            return stopMode == GameplayVfxTopologyStopMode.TopologyHelperExempt &&
                   IsTopologyHelperCue(cueId);
        }

        public static bool AllowsSpawnExemption(
            GameplayVfxCueId cueId,
            GameplayVfxTopologySpawnMode spawnMode)
        {
            return spawnMode == GameplayVfxTopologySpawnMode.TopologyHelperExempt &&
                   IsTopologyHelperCue(cueId);
        }
    }

    public readonly struct GameplayVfxSoftSpawnPolicy : IEquatable<GameplayVfxSoftSpawnPolicy>
    {
        public GameplayVfxSoftSpawnPolicy(bool enabled, float delaySeconds)
        {
            Enabled = enabled;
            DelaySeconds = Math.Max(0f, delaySeconds);
        }

        public bool Enabled { get; }

        public float DelaySeconds { get; }

        public static GameplayVfxSoftSpawnPolicy Disabled => default;

        public static GameplayVfxSoftSpawnPolicy Delay(float delaySeconds)
        {
            return new GameplayVfxSoftSpawnPolicy(true, delaySeconds);
        }

        public bool Equals(GameplayVfxSoftSpawnPolicy other)
        {
            return Enabled == other.Enabled &&
                   DelaySeconds.Equals(other.DelaySeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayVfxSoftSpawnPolicy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Enabled.GetHashCode() * 397) ^ DelaySeconds.GetHashCode();
            }
        }
    }

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
            float delaySeconds = 0f,
            GameplayVfxTopologyAnchorMode topologyAnchorMode = GameplayVfxTopologyAnchorMode.Committed,
            GameplayVfxTopologyStopMode topologyStopMode = GameplayVfxTopologyStopMode.Default,
            GameplayVfxTopologySpawnMode topologySpawnMode = GameplayVfxTopologySpawnMode.Default,
            GameplayVfxSoftSpawnPolicy softSpawnPolicy = default,
            GameplayVfxCompletionReplayPolicy completionReplayPolicy = GameplayVfxCompletionReplayPolicy.None)
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
                delaySeconds,
                topologyAnchorMode,
                topologyStopMode,
                topologySpawnMode,
                softSpawnPolicy,
                completionReplayPolicy)
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
            float delaySeconds = 0f,
            GameplayVfxTopologyAnchorMode topologyAnchorMode = GameplayVfxTopologyAnchorMode.Committed,
            GameplayVfxTopologyStopMode topologyStopMode = GameplayVfxTopologyStopMode.Default,
            GameplayVfxTopologySpawnMode topologySpawnMode = GameplayVfxTopologySpawnMode.Default,
            GameplayVfxSoftSpawnPolicy softSpawnPolicy = default,
            GameplayVfxCompletionReplayPolicy completionReplayPolicy = GameplayVfxCompletionReplayPolicy.None)
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
            TopologyAnchorMode = topologyAnchorMode;
            TopologyStopMode = topologyStopMode;
            TopologySpawnMode = topologySpawnMode;
            SoftSpawnPolicy = softSpawnPolicy;
            CompletionReplayPolicy = completionReplayPolicy;
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

        public GameplayVfxTopologyAnchorMode TopologyAnchorMode { get; }

        public GameplayVfxTopologyStopMode TopologyStopMode { get; }

        public GameplayVfxTopologySpawnMode TopologySpawnMode { get; }

        public GameplayVfxSoftSpawnPolicy SoftSpawnPolicy { get; }

        public GameplayVfxCompletionReplayPolicy CompletionReplayPolicy { get; }

        public GameplayVfxRequest WithTopologyLifecycle(
            GameplayVfxTopologyStopMode stopMode,
            GameplayVfxTopologySpawnMode spawnMode)
        {
            return new GameplayVfxRequest(
                TickIndex,
                SequenceId,
                PresentationSeed,
                SourceEntityId,
                CueId,
                Anchor,
                Timing,
                IsPersistent,
                PersistentKey,
                StyleKey,
                DelaySeconds,
                TopologyAnchorMode,
                stopMode,
                spawnMode,
                SoftSpawnPolicy,
                CompletionReplayPolicy);
        }

        public GameplayVfxRequest WithCompletionReplayPolicy(GameplayVfxCompletionReplayPolicy replayPolicy)
        {
            return new GameplayVfxRequest(
                TickIndex,
                SequenceId,
                PresentationSeed,
                SourceEntityId,
                CueId,
                Anchor,
                Timing,
                IsPersistent,
                PersistentKey,
                StyleKey,
                DelaySeconds,
                TopologyAnchorMode,
                TopologyStopMode,
                TopologySpawnMode,
                SoftSpawnPolicy,
                replayPolicy);
        }

        public GameplayVfxRequest WithSoftSpawnDelay(float delaySeconds)
        {
            var policy = GameplayVfxSoftSpawnPolicy.Delay(delaySeconds);
            return new GameplayVfxRequest(
                TickIndex,
                SequenceId,
                PresentationSeed,
                SourceEntityId,
                CueId,
                Anchor,
                Timing,
                IsPersistent,
                PersistentKey,
                StyleKey,
                policy.DelaySeconds,
                TopologyAnchorMode,
                TopologyStopMode,
                GameplayVfxTopologySpawnMode.DeferUntilCompletionWithDelay,
                policy,
                CompletionReplayPolicy);
        }

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
            if (delayCompare != 0)
            {
                return delayCompare;
            }

            var persistentCompare = IsPersistent.CompareTo(other.IsPersistent);
            if (persistentCompare != 0)
            {
                return persistentCompare;
            }

            var topologyAnchorCompare = TopologyAnchorMode.CompareTo(other.TopologyAnchorMode);
            if (topologyAnchorCompare != 0)
            {
                return topologyAnchorCompare;
            }

            var topologyStopCompare = TopologyStopMode.CompareTo(other.TopologyStopMode);
            if (topologyStopCompare != 0)
            {
                return topologyStopCompare;
            }

            var topologySpawnCompare = TopologySpawnMode.CompareTo(other.TopologySpawnMode);
            if (topologySpawnCompare != 0)
            {
                return topologySpawnCompare;
            }

            var softSpawnEnabledCompare = SoftSpawnPolicy.Enabled.CompareTo(other.SoftSpawnPolicy.Enabled);
            if (softSpawnEnabledCompare != 0)
            {
                return softSpawnEnabledCompare;
            }

            var softSpawnDelayCompare = SoftSpawnPolicy.DelaySeconds.CompareTo(other.SoftSpawnPolicy.DelaySeconds);
            return softSpawnDelayCompare != 0
                ? softSpawnDelayCompare
                : CompletionReplayPolicy.CompareTo(other.CompletionReplayPolicy);
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
                && DelaySeconds.Equals(other.DelaySeconds)
                && TopologyAnchorMode == other.TopologyAnchorMode
                && TopologyStopMode == other.TopologyStopMode
                && TopologySpawnMode == other.TopologySpawnMode
                && SoftSpawnPolicy.Equals(other.SoftSpawnPolicy)
                && CompletionReplayPolicy == other.CompletionReplayPolicy;
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
                hash = (hash * 397) ^ (int)TopologyAnchorMode;
                hash = (hash * 397) ^ (int)TopologyStopMode;
                hash = (hash * 397) ^ (int)TopologySpawnMode;
                hash = (hash * 397) ^ SoftSpawnPolicy.GetHashCode();
                hash = (hash * 397) ^ (int)CompletionReplayPolicy;
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
                $"{nameof(DelaySeconds)}={DelaySeconds}, " +
                $"{nameof(TopologyAnchorMode)}={TopologyAnchorMode}, " +
                $"{nameof(TopologyStopMode)}={TopologyStopMode}, " +
                $"{nameof(TopologySpawnMode)}={TopologySpawnMode}, " +
                $"{nameof(SoftSpawnPolicy)}={SoftSpawnPolicy.DelaySeconds}, " +
                $"{nameof(CompletionReplayPolicy)}={CompletionReplayPolicy})";
        }
    }
}
