using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;

namespace Game.Feature.Gameplay.PresentationPlayback
{
    public enum PresentationPlaybackUnitKind
    {
        None = 0,
        OneShot = 1,
        Track = 2,
    }

    public enum PresentationPlaybackInterruptMode
    {
        None = 0,
        IgnoreNew = 1,
        ReplaceExisting = 2,
        AllowOverlap = 3,
    }

    public readonly struct PresentationPlaybackPolicy : IEquatable<PresentationPlaybackPolicy>
    {
        public PresentationPlaybackPolicy(
            PresentationPlaybackUnitKind unitKind,
            bool blocking = false,
            int dedupeKey = 0,
            int cancellationKey = 0,
            int cooldownKey = 0,
            PresentationPlaybackInterruptMode interruptMode = PresentationPlaybackInterruptMode.None)
        {
            UnitKind = unitKind;
            Blocking = blocking;
            DedupeKey = Math.Max(0, dedupeKey);
            CancellationKey = Math.Max(0, cancellationKey);
            CooldownKey = Math.Max(0, cooldownKey);
            InterruptMode = interruptMode;
        }

        public PresentationPlaybackUnitKind UnitKind { get; }

        public bool Blocking { get; }

        public int DedupeKey { get; }

        public int CancellationKey { get; }

        public int CooldownKey { get; }

        public PresentationPlaybackInterruptMode InterruptMode { get; }

        public static PresentationPlaybackPolicy FromHint(PresentationPlaybackPolicyHint hint)
        {
            switch (hint.Kind)
            {
                case PresentationPlaybackPolicyHintKind.Track:
                    return new PresentationPlaybackPolicy(
                        PresentationPlaybackUnitKind.Track,
                        hint.Blocking,
                        hint.DedupeKey,
                        hint.CancellationKey,
                        hint.CooldownKey);
                case PresentationPlaybackPolicyHintKind.OneShot:
                case PresentationPlaybackPolicyHintKind.Barrier:
                case PresentationPlaybackPolicyHintKind.None:
                default:
                    return new PresentationPlaybackPolicy(
                        PresentationPlaybackUnitKind.OneShot,
                        hint.Blocking,
                        hint.DedupeKey,
                        hint.CancellationKey,
                        hint.CooldownKey);
            }
        }

        public bool Equals(PresentationPlaybackPolicy other)
        {
            return UnitKind == other.UnitKind &&
                   Blocking == other.Blocking &&
                   DedupeKey == other.DedupeKey &&
                   CancellationKey == other.CancellationKey &&
                   CooldownKey == other.CooldownKey &&
                   InterruptMode == other.InterruptMode;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationPlaybackPolicy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)UnitKind;
                hash = (hash * 397) ^ Blocking.GetHashCode();
                hash = (hash * 397) ^ DedupeKey;
                hash = (hash * 397) ^ CancellationKey;
                hash = (hash * 397) ^ CooldownKey;
                hash = (hash * 397) ^ (int)InterruptMode;
                return hash;
            }
        }
    }

    public readonly struct PresentationPlaybackCue : IEquatable<PresentationPlaybackCue>
    {
        public PresentationPlaybackCue(PresentationCue cue, PresentationPlaybackPolicy policy)
        {
            Cue = cue;
            Policy = policy;
        }

        public PresentationCue Cue { get; }

        public PresentationPlaybackPolicy Policy { get; }

        public bool Equals(PresentationPlaybackCue other)
        {
            return Cue.Equals(other.Cue) && Policy.Equals(other.Policy);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationPlaybackCue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Cue.GetHashCode() * 397) ^ Policy.GetHashCode();
            }
        }
    }

    public readonly struct PresentationPlaybackTrack : IEquatable<PresentationPlaybackTrack>
    {
        public PresentationPlaybackTrack(PresentationCue cue, PresentationPlaybackPolicy policy)
        {
            Cue = cue;
            Policy = policy;
        }

        public PresentationCue Cue { get; }

        public PresentationPlaybackPolicy Policy { get; }

        public bool Equals(PresentationPlaybackTrack other)
        {
            return Cue.Equals(other.Cue) && Policy.Equals(other.Policy);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationPlaybackTrack other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Cue.GetHashCode() * 397) ^ Policy.GetHashCode();
            }
        }
    }

    public readonly struct PresentationPlaybackBarrier : IEquatable<PresentationPlaybackBarrier>
    {
        public PresentationPlaybackBarrier(
            PresentationSource source,
            PresentationTarget target,
            int barrierKey,
            bool blocking)
        {
            Source = source;
            Target = target;
            BarrierKey = Math.Max(0, barrierKey);
            Blocking = blocking;
        }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public int BarrierKey { get; }

        public bool Blocking { get; }

        public bool Equals(PresentationPlaybackBarrier other)
        {
            return Source.Equals(other.Source) &&
                   Target.Equals(other.Target) &&
                   BarrierKey == other.BarrierKey &&
                   Blocking == other.Blocking;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationPlaybackBarrier other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Source.GetHashCode();
                hash = (hash * 397) ^ Target.GetHashCode();
                hash = (hash * 397) ^ BarrierKey;
                hash = (hash * 397) ^ Blocking.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct PresentationPlaybackDiagnostics
    {
        public PresentationPlaybackDiagnostics(
            int extractedFactCount,
            int plannedCueCount,
            int plannedTrackCount,
            int plannedBarrierCount,
            int routeCount,
            int suppressedCount,
            int deferredCount,
            int canceledCount,
            int missingBindingCount,
            int noOpSchedulerAcceptCount)
        {
            ExtractedFactCount = Math.Max(0, extractedFactCount);
            PlannedCueCount = Math.Max(0, plannedCueCount);
            PlannedTrackCount = Math.Max(0, plannedTrackCount);
            PlannedBarrierCount = Math.Max(0, plannedBarrierCount);
            RouteCount = Math.Max(0, routeCount);
            SuppressedCount = Math.Max(0, suppressedCount);
            DeferredCount = Math.Max(0, deferredCount);
            CanceledCount = Math.Max(0, canceledCount);
            MissingBindingCount = Math.Max(0, missingBindingCount);
            NoOpSchedulerAcceptCount = Math.Max(0, noOpSchedulerAcceptCount);
        }

        public int ExtractedFactCount { get; }

        public int PlannedCueCount { get; }

        public int PlannedTrackCount { get; }

        public int PlannedBarrierCount { get; }

        public int RouteCount { get; }

        public int SuppressedCount { get; }

        public int DeferredCount { get; }

        public int CanceledCount { get; }

        public int MissingBindingCount { get; }

        public int NoOpSchedulerAcceptCount { get; }

        public PresentationPlaybackDiagnostics WithNoOpSchedulerAcceptCount(int acceptCount)
        {
            return new PresentationPlaybackDiagnostics(
                ExtractedFactCount,
                PlannedCueCount,
                PlannedTrackCount,
                PlannedBarrierCount,
                RouteCount,
                SuppressedCount,
                DeferredCount,
                CanceledCount,
                MissingBindingCount,
                acceptCount);
        }
    }

    public sealed class PresentationPlaybackPlan
    {
        private static readonly IReadOnlyList<PresentationPlaybackCue> EmptyCues =
            new ReadOnlyCollection<PresentationPlaybackCue>(new List<PresentationPlaybackCue>());
        private static readonly IReadOnlyList<PresentationPlaybackTrack> EmptyTracks =
            new ReadOnlyCollection<PresentationPlaybackTrack>(new List<PresentationPlaybackTrack>());
        private static readonly IReadOnlyList<PresentationPlaybackBarrier> EmptyBarriers =
            new ReadOnlyCollection<PresentationPlaybackBarrier>(new List<PresentationPlaybackBarrier>());

        private readonly IReadOnlyList<PresentationPlaybackCue> _cues;
        private readonly IReadOnlyList<PresentationPlaybackTrack> _tracks;
        private readonly IReadOnlyList<PresentationPlaybackBarrier> _barriers;

        public PresentationPlaybackPlan(
            int tickIndex,
            IReadOnlyList<PresentationPlaybackCue> cues,
            IReadOnlyList<PresentationPlaybackTrack> tracks,
            IReadOnlyList<PresentationPlaybackBarrier> barriers,
            PresentationPlaybackDiagnostics diagnostics)
        {
            TickIndex = tickIndex;
            _cues = new ReadOnlyCollection<PresentationPlaybackCue>(
                new List<PresentationPlaybackCue>(cues ?? EmptyCues));
            _tracks = new ReadOnlyCollection<PresentationPlaybackTrack>(
                new List<PresentationPlaybackTrack>(tracks ?? EmptyTracks));
            _barriers = new ReadOnlyCollection<PresentationPlaybackBarrier>(
                new List<PresentationPlaybackBarrier>(barriers ?? EmptyBarriers));
            Diagnostics = diagnostics;
        }

        public int TickIndex { get; }

        public IReadOnlyList<PresentationPlaybackCue> Cues => _cues;

        public IReadOnlyList<PresentationPlaybackTrack> Tracks => _tracks;

        public IReadOnlyList<PresentationPlaybackBarrier> Barriers => _barriers;

        public PresentationPlaybackDiagnostics Diagnostics { get; }

        public static PresentationPlaybackPlan Empty(int tickIndex)
        {
            return new PresentationPlaybackPlan(
                tickIndex,
                EmptyCues,
                EmptyTracks,
                EmptyBarriers,
                new PresentationPlaybackDiagnostics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }

    public sealed class PresentationPlaybackPlanner
    {
        public PresentationPlaybackPlan Plan(PresentationCueFrame cueFrame)
        {
            if (cueFrame == null)
            {
                throw new ArgumentNullException(nameof(cueFrame));
            }

            var cues = new List<PresentationPlaybackCue>();
            var tracks = new List<PresentationPlaybackTrack>();
            var barriers = new List<PresentationPlaybackBarrier>();

            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                var policy = PresentationPlaybackPolicy.FromHint(cue.PolicyHint);

                if (policy.UnitKind == PresentationPlaybackUnitKind.Track)
                {
                    tracks.Add(new PresentationPlaybackTrack(cue, policy));
                }
                else
                {
                    cues.Add(new PresentationPlaybackCue(cue, policy));
                }

                if (cue.PolicyHint.Kind == PresentationPlaybackPolicyHintKind.Barrier ||
                    cue.PolicyHint.Blocking)
                {
                    barriers.Add(new PresentationPlaybackBarrier(
                        cue.Source,
                        cue.Target,
                        cue.PolicyHint.DedupeKey,
                        cue.PolicyHint.Blocking));
                }
            }

            return new PresentationPlaybackPlan(
                cueFrame.TickIndex,
                cues,
                tracks,
                barriers,
                new PresentationPlaybackDiagnostics(
                    cueFrame.Diagnostics.SourceFactCount,
                    cues.Count,
                    tracks.Count,
                    barriers.Count,
                    routeCount: 0,
                    suppressedCount: 0,
                    deferredCount: 0,
                    canceledCount: 0,
                    missingBindingCount: 0,
                    noOpSchedulerAcceptCount: 0));
        }
    }

    public sealed class PresentationPlaybackScheduler
    {
        private int _acceptCount;

        public PresentationPlaybackDiagnostics CurrentDiagnostics { get; private set; }

        public bool HasBlockingPresentation => false;

        public void Accept(PresentationPlaybackPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            _acceptCount++;
            CurrentDiagnostics = plan.Diagnostics.WithNoOpSchedulerAcceptCount(_acceptCount);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }
        }

        public void ResetSession()
        {
            _acceptCount = 0;
            CurrentDiagnostics = default;
        }

        public void HardCleanup()
        {
            ResetSession();
        }
    }

    public interface IPresentationExecutor
    {
        void Prepare(PresentationPlaybackPlan plan);

        void Play(PresentationPlaybackPlan plan);

        void Update(float deltaTime);

        void ResetSession();

        void HardCleanup();
    }

    public interface IPresentationMotionExecutor : IPresentationExecutor
    {
    }

    public interface IPresentationAnimationExecutor : IPresentationExecutor
    {
    }

    public interface IPresentationVfxExecutor : IPresentationExecutor
    {
    }

    public interface IPresentationSfxBridgeExecutor : IPresentationExecutor
    {
    }

    public interface IPresentationCameraExecutor : IPresentationExecutor
    {
    }

    public interface IPresentationTopologyExecutor : IPresentationExecutor
    {
    }

    public sealed class NoOpPresentationExecutor : IPresentationExecutor
    {
        public void Prepare(PresentationPlaybackPlan plan)
        {
        }

        public void Play(PresentationPlaybackPlan plan)
        {
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }
        }

        public void ResetSession()
        {
        }

        public void HardCleanup()
        {
        }
    }

    public sealed class NoOpPresentationExecutorRegistry
    {
        private static readonly IReadOnlyList<IPresentationExecutor> EmptyExecutors =
            new ReadOnlyCollection<IPresentationExecutor>(new List<IPresentationExecutor>());

        public IReadOnlyList<IPresentationExecutor> Executors => EmptyExecutors;
    }
}
