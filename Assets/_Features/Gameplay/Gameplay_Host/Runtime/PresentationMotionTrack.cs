using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal enum PresentationMotionKind
    {
        None = 0,
        FlipImpactStay = 1,
    }

    internal enum PresentationMotionPhaseKind
    {
        None = 0,
        Arc = 1,
        Hold = 2,
    }

    internal enum PresentationMotionScalePolicy
    {
        None = 0,
        FlipImpactStay = 1,
    }

    internal enum PresentationMotionRotationPolicy
    {
        SamplePhase = 0,
    }

    internal enum PresentationMotionCompletionPolicy
    {
        None = 0,
        ResetToCompletionPose = 1,
    }

    internal enum PresentationMotionInteractionPolicy
    {
        None = 0,
        SuppressBoxInteractionOverlay = 1,
    }

    internal readonly struct PresentationMotionInstanceKey : IEquatable<PresentationMotionInstanceKey>
    {
        public PresentationMotionInstanceKey(
            PresentationMotionKind kind,
            int tickIndex,
            int correlationId,
            int entityId,
            bool usesTickFallback)
        {
            Kind = kind;
            TickIndex = Math.Max(0, tickIndex);
            CorrelationId = correlationId;
            EntityId = entityId;
            UsesTickFallback = usesTickFallback;
        }

        public PresentationMotionKind Kind { get; }

        public int TickIndex { get; }

        public int CorrelationId { get; }

        public int EntityId { get; }

        public bool UsesTickFallback { get; }

        public bool Equals(PresentationMotionInstanceKey other)
        {
            return Kind == other.Kind &&
                   TickIndex == other.TickIndex &&
                   CorrelationId == other.CorrelationId &&
                   EntityId == other.EntityId &&
                   UsesTickFallback == other.UsesTickFallback;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationMotionInstanceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, TickIndex, CorrelationId, EntityId, UsesTickFallback);
        }

        public static PresentationMotionInstanceKey CreateFlipImpactStay(
            in FlipImpactStayMotionCommand command,
            int tickIndex)
        {
            return new PresentationMotionInstanceKey(
                PresentationMotionKind.FlipImpactStay,
                tickIndex,
                command.SourceActionPlanId > 0 ? command.SourceActionPlanId : command.PresentationSeed,
                command.BoxEntityId,
                command.SourceActionPlanId <= 0);
        }

        public static PresentationMotionInstanceKey CreateFlipImpactStay(
            in FlipImpactPresentationSignal signal,
            int tickIndex)
        {
            return new PresentationMotionInstanceKey(
                PresentationMotionKind.FlipImpactStay,
                tickIndex,
                signal.SourceActionPlanId > 0 ? signal.SourceActionPlanId : tickIndex,
                signal.BoxEntityId,
                signal.SourceActionPlanId <= 0);
        }

    }

    internal sealed class PresentationMotionCompletionLedger
    {
        private readonly struct Scope : IEquatable<Scope>
        {
            public Scope(PresentationMotionKind kind, int entityId)
            {
                Kind = kind;
                EntityId = entityId;
            }

            public PresentationMotionKind Kind { get; }

            public int EntityId { get; }

            public bool Equals(Scope other)
            {
                return Kind == other.Kind && EntityId == other.EntityId;
            }

            public override bool Equals(object obj)
            {
                return obj is Scope other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Kind, EntityId);
            }
        }

        private readonly struct Correlation : IEquatable<Correlation>
        {
            public Correlation(int correlationId, bool usesTickFallback)
            {
                CorrelationId = correlationId;
                UsesTickFallback = usesTickFallback;
            }

            public int CorrelationId { get; }

            public bool UsesTickFallback { get; }

            public bool Equals(Correlation other)
            {
                return CorrelationId == other.CorrelationId &&
                       UsesTickFallback == other.UsesTickFallback;
            }

            public override bool Equals(object obj)
            {
                return obj is Correlation other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(CorrelationId, UsesTickFallback);
            }
        }

        private sealed class CompletionEntry
        {
            public int LatestTickIndex;
            public readonly HashSet<Correlation> Correlations = new();
        }

        private readonly Dictionary<Scope, CompletionEntry> _entries = new();
        private readonly List<Scope> _removeScopes = new();

        public int Count
        {
            get
            {
                var count = 0;
                foreach (var entry in _entries.Values)
                {
                    count += entry.Correlations.Count;
                }

                return count;
            }
        }

        internal int ScopeCount => _entries.Count;

        public bool IsCompleted(in PresentationMotionInstanceKey key)
        {
            var scope = new Scope(key.Kind, key.EntityId);
            if (!_entries.TryGetValue(scope, out var entry))
            {
                return false;
            }

            if (key.TickIndex < entry.LatestTickIndex)
            {
                return true;
            }

            return key.TickIndex == entry.LatestTickIndex &&
                   entry.Correlations.Contains(new Correlation(key.CorrelationId, key.UsesTickFallback));
        }

        public void RecordCompleted(in PresentationMotionInstanceKey key)
        {
            var scope = new Scope(key.Kind, key.EntityId);
            if (!_entries.TryGetValue(scope, out var entry))
            {
                entry = new CompletionEntry
                {
                    LatestTickIndex = key.TickIndex,
                };
                _entries.Add(scope, entry);
            }
            else if (key.TickIndex < entry.LatestTickIndex)
            {
                return;
            }
            else if (key.TickIndex > entry.LatestTickIndex)
            {
                entry.LatestTickIndex = key.TickIndex;
                entry.Correlations.Clear();
            }

            entry.Correlations.Add(new Correlation(key.CorrelationId, key.UsesTickFallback));
        }

        public int RemoveEntity(int entityId)
        {
            _removeScopes.Clear();
            var removedCount = 0;
            foreach (var scope in _entries.Keys)
            {
                if (scope.EntityId == entityId)
                {
                    _removeScopes.Add(scope);
                    removedCount += _entries[scope].Correlations.Count;
                }
            }

            for (var index = 0; index < _removeScopes.Count; index++)
            {
                _entries.Remove(_removeScopes[index]);
            }

            _removeScopes.Clear();
            return removedCount;
        }

        public bool ContainsEntity(int entityId)
        {
            foreach (var scope in _entries.Keys)
            {
                if (scope.EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            _entries.Clear();
            _removeScopes.Clear();
        }
    }

    internal readonly struct PresentationMotionPhase
    {
        public PresentationMotionPhase(
            PresentationMotionPhaseKind kind,
            float startNormalizedTime,
            float endNormalizedTime,
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float arcHeightWorld)
        {
            Kind = kind;
            StartNormalizedTime = Mathf.Clamp01(startNormalizedTime);
            EndNormalizedTime = Mathf.Clamp01(Mathf.Max(startNormalizedTime, endNormalizedTime));
            StartPose = startPose;
            EndPose = endPose;
            ArcHeightWorld = Mathf.Max(0f, arcHeightWorld);
        }

        public PresentationMotionPhaseKind Kind { get; }

        public float StartNormalizedTime { get; }

        public float EndNormalizedTime { get; }

        public GameplayEntityPose StartPose { get; }

        public GameplayEntityPose EndPose { get; }

        public float ArcHeightWorld { get; }

        public bool Contains(float normalizedTime)
        {
            return normalizedTime >= StartNormalizedTime &&
                   normalizedTime <= EndNormalizedTime;
        }

        public GameplayEntityPose Sample(float normalizedTime)
        {
            if (Kind == PresentationMotionPhaseKind.Hold)
            {
                return EndPose;
            }

            var denominator = Mathf.Max(0.0001f, EndNormalizedTime - StartNormalizedTime);
            var phaseTime = Mathf.Clamp01((normalizedTime - StartNormalizedTime) / denominator);
            return Kind == PresentationMotionPhaseKind.Arc
                ? FlipArcSampler.Sample(StartPose, EndPose, phaseTime, ArcHeightWorld)
                : EndPose;
        }
    }

    internal readonly struct PresentationMotionPhaseSet
    {
        public PresentationMotionPhaseSet(
            PresentationMotionPhase phase0,
            PresentationMotionPhase phase1,
            PresentationMotionPhase phase2)
        {
            Count = 3;
            Phase0 = phase0;
            Phase1 = phase1;
            Phase2 = phase2;
        }

        public int Count { get; }

        public PresentationMotionPhase Phase0 { get; }

        public PresentationMotionPhase Phase1 { get; }

        public PresentationMotionPhase Phase2 { get; }

        public GameplayEntityPose Sample(float normalizedTime, GameplayEntityPose fallbackPose)
        {
            if (Count <= 0)
            {
                return fallbackPose;
            }

            if (Phase0.Contains(normalizedTime))
            {
                return Phase0.Sample(normalizedTime);
            }

            if (Count > 1 && Phase1.Contains(normalizedTime))
            {
                return Phase1.Sample(normalizedTime);
            }

            if (Count > 2 && Phase2.Contains(normalizedTime))
            {
                return Phase2.Sample(normalizedTime);
            }

            return normalizedTime <= Phase0.StartNormalizedTime
                ? Phase0.StartPose
                : fallbackPose;
        }

        public static PresentationMotionPhaseSet CreateFlipImpactStay(
            GameplayEntityPose sourcePose,
            GameplayEntityPose contactPose,
            float contactNormalizedTime,
            float postContactHoldNormalizedDuration,
            float arcHeightWorld,
            float returnArcMultiplier)
        {
            var contactTime = Mathf.Clamp01(contactNormalizedTime);
            var holdEndTime = Mathf.Min(
                1f,
                contactTime + Mathf.Clamp01(postContactHoldNormalizedDuration));
            return new PresentationMotionPhaseSet(
                new PresentationMotionPhase(
                    PresentationMotionPhaseKind.Arc,
                    0f,
                    contactTime,
                    sourcePose,
                    contactPose,
                    arcHeightWorld),
                new PresentationMotionPhase(
                    PresentationMotionPhaseKind.Hold,
                    contactTime,
                    holdEndTime,
                    contactPose,
                    contactPose,
                    0f),
                new PresentationMotionPhase(
                    PresentationMotionPhaseKind.Arc,
                    holdEndTime,
                    1f,
                    contactPose,
                    sourcePose,
                    arcHeightWorld * Mathf.Max(0f, returnArcMultiplier)));
        }
    }

    internal readonly struct PresentationMotionCommand
    {
        public PresentationMotionCommand(
            int entityId,
            PresentationMotionKind kind,
            PresentationMotionInstanceKey instanceKey,
            GameplayEntityPose sourcePose,
            GameplayEntityPose contactPose,
            GameplayEntityPose completionPose,
            float durationSeconds,
            PresentationMotionPhaseSet phases,
            PresentationMotionScalePolicy scalePolicy,
            PresentationMotionRotationPolicy rotationPolicy,
            PresentationMotionCompletionPolicy completionPolicy,
            PresentationMotionInteractionPolicy interactionPolicy,
            int presentationSeed,
            bool hasRequiredFinalCell,
            SurfaceCell requiredFinalCell)
        {
            EntityId = entityId;
            Kind = kind;
            InstanceKey = instanceKey;
            SourcePose = sourcePose;
            ContactPose = contactPose;
            CompletionPose = completionPose;
            DurationSeconds = Mathf.Max(0.0001f, durationSeconds);
            Phases = phases;
            ScalePolicy = scalePolicy;
            RotationPolicy = rotationPolicy;
            CompletionPolicy = completionPolicy;
            InteractionPolicy = interactionPolicy;
            PresentationSeed = presentationSeed;
            HasRequiredFinalCell = hasRequiredFinalCell;
            RequiredFinalCell = requiredFinalCell;
        }

        public int EntityId { get; }

        public PresentationMotionKind Kind { get; }

        public PresentationMotionInstanceKey InstanceKey { get; }

        public GameplayEntityPose SourcePose { get; }

        public GameplayEntityPose ContactPose { get; }

        public GameplayEntityPose CompletionPose { get; }

        public float DurationSeconds { get; }

        public PresentationMotionPhaseSet Phases { get; }

        public PresentationMotionScalePolicy ScalePolicy { get; }

        public PresentationMotionRotationPolicy RotationPolicy { get; }

        public PresentationMotionCompletionPolicy CompletionPolicy { get; }

        public PresentationMotionInteractionPolicy InteractionPolicy { get; }

        public int PresentationSeed { get; }

        public bool HasRequiredFinalCell { get; }

        public SurfaceCell RequiredFinalCell { get; }
    }

    internal readonly struct PresentationMotionSample
    {
        public PresentationMotionSample(
            int entityId,
            GameplayEntityPose localPose,
            Vector3 visualScaleMultiplier,
            bool isComplete,
            GameplayEntityPose completionPose,
            bool suppressBoxInteractionOverlay)
        {
            EntityId = entityId;
            LocalPose = localPose;
            VisualScaleMultiplier = visualScaleMultiplier;
            IsComplete = isComplete;
            CompletionPose = completionPose;
            SuppressBoxInteractionOverlay = suppressBoxInteractionOverlay;
        }

        public int EntityId { get; }

        public GameplayEntityPose LocalPose { get; }

        public Vector3 VisualScaleMultiplier { get; }

        public bool IsComplete { get; }

        public GameplayEntityPose CompletionPose { get; }

        public bool SuppressBoxInteractionOverlay { get; }
    }

    internal sealed class PresentationMotionTrack
    {
        private readonly PresentationMotionCommand _command;
        private float _elapsedSeconds;
        private float _previousPresentedNormalizedTime;

        private PresentationMotionTrack(in PresentationMotionCommand command)
        {
            _command = command;
            _elapsedSeconds = 0f;
        }

        public PresentationMotionKind Kind => _command.Kind;

        public PresentationMotionInstanceKey InstanceKey => _command.InstanceKey;

        public int EntityId => _command.EntityId;

        public float DurationSeconds => _command.DurationSeconds;

        public float ElapsedSeconds => _elapsedSeconds;

        public float NormalizedTime => Mathf.Clamp01(_elapsedSeconds / DurationSeconds);

        public bool IsComplete { get; private set; }

        public GameplayEntityPose SourcePose => _command.SourcePose;

        public GameplayEntityPose ContactPose => _command.ContactPose;

        public GameplayEntityPose CompletionPose => _command.CompletionPose;

        public bool HasRequiredFinalCell => _command.HasRequiredFinalCell;

        public SurfaceCell RequiredFinalCell => _command.RequiredFinalCell;

        public PresentationMotionInteractionPolicy InteractionPolicy => _command.InteractionPolicy;

        public PresentationMotionPhaseSet Phases => _command.Phases;

        public static PresentationMotionTrack Create(in PresentationMotionCommand command)
        {
            return new PresentationMotionTrack(command);
        }

        public static PresentationMotionTrack CreateFlipImpactStay(
            in FlipImpactStayMotionCommand command,
            int tickIndex)
        {
            return Create(FlipImpactStayPresentationMotionCommandAdapter.ToPresentationMotionCommand(command, tickIndex));
        }

        public void Advance(float deltaTime)
        {
            if (IsComplete || deltaTime <= 0f)
            {
                return;
            }

            _elapsedSeconds = Mathf.Min(DurationSeconds, _elapsedSeconds + deltaTime);
            if (_elapsedSeconds >= DurationSeconds - 0.0001f)
            {
                IsComplete = true;
            }
        }

        public PresentationMotionSample Sample()
        {
            var normalizedTime = NormalizedTime;
            var pose = _command.Phases.Sample(normalizedTime, _command.CompletionPose);
            return new PresentationMotionSample(
                EntityId,
                pose,
                SampleVisualScaleMultiplier(normalizedTime),
                IsComplete,
                _command.CompletionPose,
                _command.InteractionPolicy == PresentationMotionInteractionPolicy.SuppressBoxInteractionOverlay);
        }

        internal bool TryCaptureProgress(out MotionTrackProgressSample progressSample)
        {
            if (_command.Kind != PresentationMotionKind.FlipImpactStay ||
                _command.InstanceKey.UsesTickFallback ||
                _command.InstanceKey.CorrelationId <= 0)
            {
                progressSample = default;
                return false;
            }

            var currentNormalizedTime = NormalizedTime;
            progressSample = new MotionTrackProgressSample(
                _command.InstanceKey.TickIndex,
                EntityId,
                TickEntityMotionKind.Flip,
                _previousPresentedNormalizedTime,
                currentNormalizedTime,
                _command.InstanceKey.CorrelationId,
                MotionTrackProgressSourceKind.OriginalViewMotion);
            _previousPresentedNormalizedTime = currentNormalizedTime;
            return true;
        }

        private Vector3 SampleVisualScaleMultiplier(float normalizedTime)
        {
            if (_command.ScalePolicy != PresentationMotionScalePolicy.FlipImpactStay)
            {
                return Vector3.one;
            }

            var contactTime = _command.Phases.Phase0.EndNormalizedTime;
            var holdEndTime = _command.Phases.Phase1.EndNormalizedTime;
            if (normalizedTime <= contactTime)
            {
                var preContactDenominator = Mathf.Max(0.0001f, contactTime);
                return BoxMotionVisualScaleSampler.SampleFlipFlight(
                    Mathf.Clamp01(normalizedTime / preContactDenominator));
            }

            if (normalizedTime <= holdEndTime)
            {
                var holdDenominator = Mathf.Max(0.0001f, holdEndTime - contactTime);
                return BoxMotionVisualScaleSampler.SampleFlipSettle(
                    Mathf.Clamp01((normalizedTime - contactTime) / holdDenominator));
            }

            return Vector3.one;
        }
    }

    internal static class FlipImpactStayPresentationMotionCommandAdapter
    {
        public static PresentationMotionCommand ToPresentationMotionCommand(
            in FlipImpactStayMotionCommand command,
            int tickIndex)
        {
            var sourcePose = command.SourcePose;
            var impactPose = command.ImpactPose;
            return new PresentationMotionCommand(
                command.BoxEntityId,
                PresentationMotionKind.FlipImpactStay,
                PresentationMotionInstanceKey.CreateFlipImpactStay(command, tickIndex),
                sourcePose,
                impactPose,
                sourcePose,
                command.DurationSeconds,
                PresentationMotionPhaseSet.CreateFlipImpactStay(
                    sourcePose,
                    impactPose,
                    command.ContactNormalizedTime,
                    command.PostContactHoldNormalizedDuration,
                    command.ArcHeightWorld,
                    command.ReturnArcMultiplier),
                PresentationMotionScalePolicy.FlipImpactStay,
                PresentationMotionRotationPolicy.SamplePhase,
                PresentationMotionCompletionPolicy.ResetToCompletionPose,
                PresentationMotionInteractionPolicy.SuppressBoxInteractionOverlay,
                command.PresentationSeed,
                hasRequiredFinalCell: true,
                command.SourceCell);
        }
    }
}
