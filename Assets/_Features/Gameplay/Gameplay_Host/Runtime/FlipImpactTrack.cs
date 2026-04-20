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
        private readonly GameplayEntityPose _impactPose;
        private readonly GameplayEntityPose _sourcePose;
        private readonly float _arcHeightWorld;
        private readonly FlipImpactPresentationDisposition _disposition;
        private readonly FlipImpactTimingSettings _timingSettings;
        private float _elapsedSeconds;

        public FlipImpactTrack(
            FlipImpactInstanceKey key,
            FlipImpactPresentationSignal signal,
            GameplayEntityPose sourcePose,
            GameplayEntityPose impactPose,
            float durationSeconds,
            float arcHeightWorld,
            FlipImpactTimingSettings timingSettings)
        {
            InstanceKey = key;
            Signal = signal;
            _sourcePose = sourcePose;
            _impactPose = impactPose;
            DurationSeconds = Mathf.Max(0.0001f, durationSeconds);
            _arcHeightWorld = Mathf.Max(0f, arcHeightWorld);
            _timingSettings = timingSettings;
            _disposition = signal.Disposition;
            _elapsedSeconds = 0f;
        }

        public FlipImpactInstanceKey InstanceKey { get; }

        public FlipImpactPresentationSignal Signal { get; }

        public float DurationSeconds { get; }

        public float ElapsedSeconds => _elapsedSeconds;

        public float NormalizedTime => Mathf.Clamp01(_elapsedSeconds / DurationSeconds);

        public bool IsComplete { get; private set; }

        public GameplayEntityPose SourcePose => _sourcePose;

        public GameplayEntityPose ImpactPose => _impactPose;

        public FlipImpactPresentationDisposition Disposition => _disposition;

        public float ContactNormalizedTime => _timingSettings.ContactNormalizedTime;

        public float DestroyBreakNormalizedTime
        {
            get
            {
                var denominator = Mathf.Max(0.0001f, _timingSettings.DestroyBreakNormalizedDuration);
                return Mathf.Clamp01((NormalizedTime - ContactNormalizedTime) / denominator);
            }
        }

        public GameplayEntityPose ContactPose => _impactPose;

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

        public GameplayEntityPose Sample()
        {
            var normalizedTime = NormalizedTime;
            if (_disposition == FlipImpactPresentationDisposition.Stay)
            {
                return SampleStay(normalizedTime);
            }

            if (normalizedTime <= ContactNormalizedTime)
            {
                return SamplePreContact(normalizedTime);
            }

            return _impactPose;
        }

        private GameplayEntityPose SampleStay(float normalizedTime)
        {
            if (normalizedTime <= ContactNormalizedTime)
            {
                return SamplePreContact(normalizedTime);
            }

            var holdEndNormalizedTime = Mathf.Min(
                1f,
                ContactNormalizedTime + _timingSettings.StayPostContactHoldNormalizedDuration);
            if (normalizedTime <= holdEndNormalizedTime)
            {
                return _impactPose;
            }

            var returnDenominator = Mathf.Max(0.0001f, 1f - holdEndNormalizedTime);
            var returnTime = Mathf.Clamp01((normalizedTime - holdEndNormalizedTime) / returnDenominator);
            return FlipArcSampler.Sample(
                _impactPose,
                _sourcePose,
                returnTime,
                _arcHeightWorld * _timingSettings.StayReturnArcHeightMultiplier);
        }

        private GameplayEntityPose SamplePreContact(float normalizedTime)
        {
            var preContactDenominator = Mathf.Max(0.0001f, ContactNormalizedTime);
            var contactTime = Mathf.Clamp01(normalizedTime / preContactDenominator);
            return FlipArcSampler.Sample(_sourcePose, _impactPose, contactTime, _arcHeightWorld);
        }
    }
}
