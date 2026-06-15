using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.PresentationContracts;

namespace Game.Feature.Gameplay.PresentationPlanning
{
    public enum PresentationPlaybackPolicyHintKind
    {
        None = 0,
        OneShot = 1,
        Track = 2,
        Barrier = 3,
    }

    public enum PresentationTopologyCueKey
    {
        None = 0,
        Transition = 1,
    }

    public readonly struct PresentationCueKey : IEquatable<PresentationCueKey>
    {
        public PresentationCueKey(PresentationDomain domain, int localKey, int variantKey = 0)
        {
            Domain = domain;
            LocalKey = Math.Max(0, localKey);
            VariantKey = Math.Max(0, variantKey);
        }

        public PresentationDomain Domain { get; }

        public int LocalKey { get; }

        public int VariantKey { get; }

        public bool IsValid => Domain != PresentationDomain.None && LocalKey > 0;

        public bool Equals(PresentationCueKey other)
        {
            return Domain == other.Domain &&
                   LocalKey == other.LocalKey &&
                   VariantKey == other.VariantKey;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationCueKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Domain;
                hash = (hash * 397) ^ LocalKey;
                hash = (hash * 397) ^ VariantKey;
                return hash;
            }
        }
    }

    public readonly struct PresentationPlaybackPolicyHint : IEquatable<PresentationPlaybackPolicyHint>
    {
        public PresentationPlaybackPolicyHint(
            PresentationPlaybackPolicyHintKind kind,
            bool blocking = false,
            int dedupeKey = 0,
            int cancellationKey = 0,
            int cooldownKey = 0)
        {
            Kind = kind;
            Blocking = blocking;
            DedupeKey = Math.Max(0, dedupeKey);
            CancellationKey = Math.Max(0, cancellationKey);
            CooldownKey = Math.Max(0, cooldownKey);
        }

        public PresentationPlaybackPolicyHintKind Kind { get; }

        public bool Blocking { get; }

        public int DedupeKey { get; }

        public int CancellationKey { get; }

        public int CooldownKey { get; }

        public static PresentationPlaybackPolicyHint OneShot()
        {
            return new PresentationPlaybackPolicyHint(PresentationPlaybackPolicyHintKind.OneShot);
        }

        public static PresentationPlaybackPolicyHint Track(bool blocking = false)
        {
            return new PresentationPlaybackPolicyHint(PresentationPlaybackPolicyHintKind.Track, blocking);
        }

        public bool Equals(PresentationPlaybackPolicyHint other)
        {
            return Kind == other.Kind &&
                   Blocking == other.Blocking &&
                   DedupeKey == other.DedupeKey &&
                   CancellationKey == other.CancellationKey &&
                   CooldownKey == other.CooldownKey;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationPlaybackPolicyHint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ Blocking.GetHashCode();
                hash = (hash * 397) ^ DedupeKey;
                hash = (hash * 397) ^ CancellationKey;
                hash = (hash * 397) ^ CooldownKey;
                return hash;
            }
        }
    }

    public readonly struct PresentationCue : IEquatable<PresentationCue>
    {
        public PresentationCue(
            PresentationDomain domain,
            PresentationCueKey key,
            PresentationSource source,
            PresentationTarget target,
            PresentationAnchor anchor,
            PresentationPlaybackPolicyHint policyHint = default,
            PresentationTopologyTransitionPayload topologyPayload = default)
        {
            Domain = domain;
            Key = key;
            Source = source;
            Target = target;
            Anchor = anchor;
            PolicyHint = policyHint;
            TopologyPayload = topologyPayload;
        }

        public PresentationDomain Domain { get; }

        public PresentationCueKey Key { get; }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public PresentationPlaybackPolicyHint PolicyHint { get; }

        public PresentationTopologyTransitionPayload TopologyPayload { get; }

        public bool Equals(PresentationCue other)
        {
            return Domain == other.Domain &&
                   Key.Equals(other.Key) &&
                   Source.Equals(other.Source) &&
                   Target.Equals(other.Target) &&
                   Anchor.Equals(other.Anchor) &&
                   PolicyHint.Equals(other.PolicyHint) &&
                   TopologyPayload.Equals(other.TopologyPayload);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationCue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Domain;
                hash = (hash * 397) ^ Key.GetHashCode();
                hash = (hash * 397) ^ Source.GetHashCode();
                hash = (hash * 397) ^ Target.GetHashCode();
                hash = (hash * 397) ^ Anchor.GetHashCode();
                hash = (hash * 397) ^ PolicyHint.GetHashCode();
                hash = (hash * 397) ^ TopologyPayload.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct PresentationCueFrameDiagnostics
    {
        public PresentationCueFrameDiagnostics(int sourceFactCount, int plannedCueCount, int plannerCount)
        {
            SourceFactCount = Math.Max(0, sourceFactCount);
            PlannedCueCount = Math.Max(0, plannedCueCount);
            PlannerCount = Math.Max(0, plannerCount);
        }

        public int SourceFactCount { get; }

        public int PlannedCueCount { get; }

        public int PlannerCount { get; }
    }

    public sealed class PresentationCueFrame
    {
        private static readonly IReadOnlyList<PresentationCue> EmptyCues =
            new ReadOnlyCollection<PresentationCue>(new List<PresentationCue>());

        private readonly IReadOnlyList<PresentationCue> _cues;

        public PresentationCueFrame(
            int tickIndex,
            IReadOnlyList<PresentationCue> cues,
            PresentationCueFrameDiagnostics diagnostics)
        {
            TickIndex = tickIndex;
            _cues = new ReadOnlyCollection<PresentationCue>(
                new List<PresentationCue>(cues ?? EmptyCues));
            Diagnostics = diagnostics;
        }

        public int TickIndex { get; }

        public IReadOnlyList<PresentationCue> Cues => _cues;

        public PresentationCueFrameDiagnostics Diagnostics { get; }

        public static PresentationCueFrame Empty(int tickIndex, int sourceFactCount = 0)
        {
            return new PresentationCueFrame(
                tickIndex,
                EmptyCues,
                new PresentationCueFrameDiagnostics(sourceFactCount, 0, 0));
        }
    }

    public sealed class PresentationCueFrameBuilder
    {
        private readonly List<PresentationCue> _cues = new();
        private readonly int _sourceFactCount;
        private int _plannerCount;

        public PresentationCueFrameBuilder(int tickIndex, int sourceFactCount)
        {
            TickIndex = tickIndex;
            _sourceFactCount = Math.Max(0, sourceFactCount);
        }

        public int TickIndex { get; }

        public int Count => _cues.Count;

        public void Add(PresentationCue cue)
        {
            if (cue.Domain == PresentationDomain.None || !cue.Key.IsValid)
            {
                return;
            }

            _cues.Add(cue);
        }

        public void MarkPlannerRan()
        {
            _plannerCount++;
        }

        public PresentationCueFrame Build()
        {
            return new PresentationCueFrame(
                TickIndex,
                _cues,
                new PresentationCueFrameDiagnostics(_sourceFactCount, _cues.Count, _plannerCount));
        }
    }

    public interface IPresentationCuePlanner
    {
        void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder);
    }

    public sealed class TopologyCuePlanner : IPresentationCuePlanner
    {
        private static readonly PresentationCueKey TransitionCueKey =
            new(PresentationDomain.Topology, (int)PresentationTopologyCueKey.Transition);

        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (fact.Kind != PresentationFactKind.Topology)
                {
                    continue;
                }

                builder.Add(new PresentationCue(
                    PresentationDomain.Topology,
                    TransitionCueKey,
                    fact.Source,
                    PresentationTarget.Topology(),
                    PresentationAnchor.ForTopologyOrbit(),
                    PresentationPlaybackPolicyHint.Track(blocking: true),
                    fact.TopologyPayload));
            }
        }
    }

    public sealed class PresentationCuePlannerSet
    {
        private static readonly IReadOnlyList<IPresentationCuePlanner> EmptyPlanners =
            new ReadOnlyCollection<IPresentationCuePlanner>(new List<IPresentationCuePlanner>());

        private readonly IReadOnlyList<IPresentationCuePlanner> _planners;

        public PresentationCuePlannerSet(IReadOnlyList<IPresentationCuePlanner> planners = null)
        {
            _planners = new ReadOnlyCollection<IPresentationCuePlanner>(
                new List<IPresentationCuePlanner>(planners ?? EmptyPlanners));
        }

        public IReadOnlyList<IPresentationCuePlanner> Planners => _planners;

        public PresentationCueFrame Plan(PresentationFactFrame facts)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            var builder = new PresentationCueFrameBuilder(
                facts.TickIndex,
                facts.Diagnostics.ExtractedFactCount);

            for (var i = 0; i < _planners.Count; i++)
            {
                var planner = _planners[i];
                if (planner == null)
                {
                    continue;
                }

                planner.Plan(in facts, builder);
                builder.MarkPlannerRan();
            }

            return builder.Build();
        }
    }
}
