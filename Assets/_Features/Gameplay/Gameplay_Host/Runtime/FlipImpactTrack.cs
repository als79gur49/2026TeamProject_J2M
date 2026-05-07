using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct FlipImpactInstanceKey : IEquatable<FlipImpactInstanceKey>
    {
        public FlipImpactInstanceKey(int correlationId, int boxEntityId, bool usesTickFallback)
        {
            CorrelationId = correlationId;
            BoxEntityId = boxEntityId;
            UsesTickFallback = usesTickFallback;
        }

        public int CorrelationId { get; }

        public int BoxEntityId { get; }

        public bool UsesTickFallback { get; }

        public bool Equals(FlipImpactInstanceKey other)
        {
            return CorrelationId == other.CorrelationId &&
                   BoxEntityId == other.BoxEntityId &&
                   UsesTickFallback == other.UsesTickFallback;
        }

        public override bool Equals(object obj)
        {
            return obj is FlipImpactInstanceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CorrelationId, BoxEntityId, UsesTickFallback);
        }

        public static FlipImpactInstanceKey Create(FlipImpactPresentationSignal signal, int tickIndexFallback)
        {
            if (signal.SourceActionPlanId > 0)
            {
                return new FlipImpactInstanceKey(signal.SourceActionPlanId, signal.BoxEntityId, usesTickFallback: false);
            }

            return new FlipImpactInstanceKey(tickIndexFallback, signal.BoxEntityId, usesTickFallback: true);
        }
    }

    internal sealed class FlipImpactTrack
    {
        private readonly PresentationMotionTrack _track;
        private readonly FlipImpactPresentationDisposition _disposition;
        private readonly float _contactNormalizedTime;

        private FlipImpactTrack(
            PresentationMotionTrack track,
            FlipImpactPresentationSignal signal,
            FlipImpactPresentationDisposition disposition,
            float contactNormalizedTime)
        {
            _track = track ?? throw new ArgumentNullException(nameof(track));
            Signal = signal;
            _disposition = disposition;
            _contactNormalizedTime = Mathf.Clamp01(contactNormalizedTime);
        }

        public static FlipImpactTrack CreateStay(in FlipImpactStayMotionCommand command)
        {
            var signal = new FlipImpactPresentationSignal(
                command.SourceActionPlanId,
                command.BoxEntityId,
                command.ImpactTargetEntityId,
                command.ActorEntityId,
                command.SourceCell,
                command.ImpactCell,
                command.Topology,
                command.SourceFacing,
                command.ImpactFacing,
                FlipImpactPresentationDisposition.Stay,
                hasLandingCell: false);
            return new FlipImpactTrack(
                PresentationMotionTrack.CreateFlipImpactStay(command),
                signal,
                FlipImpactPresentationDisposition.Stay,
                command.ContactNormalizedTime);
        }

        public FlipImpactInstanceKey InstanceKey => _track.InstanceKey.ToFlipImpactInstanceKey();

        public FlipImpactPresentationSignal Signal { get; }

        public float DurationSeconds => _track.DurationSeconds;

        public float ElapsedSeconds => _track.ElapsedSeconds;

        public float NormalizedTime => _track.NormalizedTime;

        public bool IsComplete => _track.IsComplete;

        public GameplayEntityPose SourcePose => _track.SourcePose;

        public GameplayEntityPose ImpactPose => _track.ContactPose;

        public FlipImpactPresentationDisposition Disposition => _disposition;

        public float ContactNormalizedTime => _contactNormalizedTime;

        public float DestroyBreakNormalizedTime
        {
            get
            {
                var denominator = 0.0001f;
                return Mathf.Clamp01((NormalizedTime - ContactNormalizedTime) / denominator);
            }
        }

        public GameplayEntityPose ContactPose => _track.ContactPose;

        public void Advance(float deltaTime)
        {
            _track.Advance(deltaTime);
        }

        public GameplayEntityPose Sample()
        {
            return _track.Sample().LocalPose;
        }

        public Vector3 SampleVisualScaleMultiplier()
        {
            return _track.Sample().VisualScaleMultiplier;
        }
    }
}
