using System;
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
            int correlationId,
            int entityId,
            bool usesTickFallback)
        {
            Kind = kind;
            CorrelationId = correlationId;
            EntityId = entityId;
            UsesTickFallback = usesTickFallback;
        }

        public PresentationMotionKind Kind { get; }

        public int CorrelationId { get; }

        public int EntityId { get; }

        public bool UsesTickFallback { get; }

        public bool Equals(PresentationMotionInstanceKey other)
        {
            return Kind == other.Kind &&
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
            return HashCode.Combine(Kind, CorrelationId, EntityId, UsesTickFallback);
        }

        public static PresentationMotionInstanceKey CreateFlipImpactStay(
            in FlipImpactStayMotionCommand command)
        {
            return new PresentationMotionInstanceKey(
                PresentationMotionKind.FlipImpactStay,
                command.SourceActionPlanId > 0 ? command.SourceActionPlanId : command.PresentationSeed,
                command.BoxEntityId,
                command.SourceActionPlanId <= 0);
        }

        public static PresentationMotionInstanceKey CreateFlipImpactStay(
            in FlipImpactPresentationSignal signal,
            int tickIndexFallback)
        {
            return new PresentationMotionInstanceKey(
                PresentationMotionKind.FlipImpactStay,
                signal.SourceActionPlanId > 0 ? signal.SourceActionPlanId : tickIndexFallback,
                signal.BoxEntityId,
                signal.SourceActionPlanId <= 0);
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

        public static PresentationMotionTrack CreateFlipImpactStay(in FlipImpactStayMotionCommand command)
        {
            return Create(FlipImpactStayPresentationMotionCommandAdapter.ToPresentationMotionCommand(command));
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
            in FlipImpactStayMotionCommand command)
        {
            var sourcePose = command.SourcePose;
            var impactPose = command.ImpactPose;
            return new PresentationMotionCommand(
                command.BoxEntityId,
                PresentationMotionKind.FlipImpactStay,
                PresentationMotionInstanceKey.CreateFlipImpactStay(command),
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
