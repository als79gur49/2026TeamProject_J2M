using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayCameraShakeProductionPlanner
    {
        private enum FlipCameraOutcomeKind
        {
            OrdinaryLanding = 1,
            HostileStay = 2,
            HostileDestroySelf = 3,
            HostileFollowThrough = 4,
        }

        private readonly struct FlipActionIdentity : IEquatable<FlipActionIdentity>
        {
            public FlipActionIdentity(int boxEntityId, int sourceActionPlanId)
            {
                BoxEntityId = boxEntityId;
                SourceActionPlanId = sourceActionPlanId;
            }

            public int BoxEntityId { get; }

            public int SourceActionPlanId { get; }

            public bool Equals(FlipActionIdentity other)
            {
                return BoxEntityId == other.BoxEntityId &&
                       SourceActionPlanId == other.SourceActionPlanId;
            }

            public override bool Equals(object obj)
            {
                return obj is FlipActionIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (BoxEntityId * 397) ^ SourceActionPlanId;
                }
            }

            public override string ToString()
            {
                return $"Box={BoxEntityId}, ActionPlan={SourceActionPlanId}";
            }
        }

        private readonly struct PendingCameraMilestone
        {
            public PendingCameraMilestone(
                in CameraShakeImpulseRequest request,
                float contactNormalizedTime,
                MotionTrackProgressSourceKind progressSource)
            {
                Request = request;
                ContactNormalizedTime = contactNormalizedTime;
                ProgressSource = progressSource;
            }

            public CameraShakeImpulseRequest Request { get; }

            public float ContactNormalizedTime { get; }

            public MotionTrackProgressSourceKind ProgressSource { get; }
        }

        private readonly struct PendingEnemyJumpLanding
        {
            public PendingEnemyJumpLanding(in CameraShakeImpulseRequest request)
            {
                Request = request;
            }

            public CameraShakeImpulseRequest Request { get; }
        }

        private readonly ICameraShakeImpulseSink _impulseSink;
        private readonly HashSet<CameraShakeRequestIdentity> _observedIdentities = new();
        private readonly Dictionary<CameraShakeRequestIdentity, PendingCameraMilestone> _pendingMilestones = new();
        private readonly Dictionary<CameraShakeRequestIdentity, PendingEnemyJumpLanding>
            _pendingEnemyJumpLandings = new();
        private readonly HashSet<CameraShakeRequestIdentity> _completedMilestoneIdentities = new();
        private readonly List<CameraShakeRequestIdentity> _orderedEnemyJumpLandingIdentities = new();
        private readonly Dictionary<FlipActionIdentity, FlipCameraOutcomeKind> _flipOutcomesByAction = new();
        private int _acceptedPlayerDamageImpactCount;
        private int _acceptedPlayerLethalImpactCount;
        private int _observedHeavyEnemyJumpLandingCount;
        private int _acceptedHeavyEnemyJumpLandingCount;
        private int _offscreenHeavyEnemyJumpLandingCount;
        private int _missingAnchorHeavyEnemyJumpLandingCount;
        private int _topologySuppressedHeavyEnemyJumpLandingCount;

        internal const float HeavyEnemyJumpLandingViewportMargin = 0.05f;

        public GameplayCameraShakeProductionPlanner(ICameraShakeImpulseSink impulseSink)
        {
            _impulseSink = impulseSink ?? throw new ArgumentNullException(nameof(impulseSink));
        }

        internal int PendingFlipLandingCount => CountPending(CameraShakeSemantic.FlipFloorLanding);

        internal int PendingFlipHostileImpactCount => CountPending(CameraShakeSemantic.FlipHostileImpact);

        internal int ObservedPlayerDamageImpactCount =>
            CountObserved(CameraShakeSemantic.PlayerDamageImpact);

        internal int ObservedPlayerLethalImpactCount =>
            CountObserved(CameraShakeSemantic.PlayerLethalImpact);

        internal int AcceptedPlayerDamageImpactCount => _acceptedPlayerDamageImpactCount;

        internal int AcceptedPlayerLethalImpactCount => _acceptedPlayerLethalImpactCount;

        internal int PendingHeavyEnemyJumpLandingCount => _pendingEnemyJumpLandings.Count;

        internal int ObservedHeavyEnemyJumpLandingCount => _observedHeavyEnemyJumpLandingCount;

        internal int AcceptedHeavyEnemyJumpLandingCount => _acceptedHeavyEnemyJumpLandingCount;

        internal int OffscreenHeavyEnemyJumpLandingCount => _offscreenHeavyEnemyJumpLandingCount;

        internal int MissingAnchorHeavyEnemyJumpLandingCount => _missingAnchorHeavyEnemyJumpLandingCount;

        internal int TopologySuppressedHeavyEnemyJumpLandingCount =>
            _topologySuppressedHeavyEnemyJumpLandingCount;

        internal int ObservedIdentityCount => _observedIdentities.Count;

        public bool Present(
            TickResult result,
            Func<int, TickEntityMotionKind, int, bool> hasActiveLocalMotionTrack,
            Func<int, TickEntityMotionKind, int, bool> hasLocalMotionTrack,
            Func<int, EnemyJumpLandingCameraFeedbackKind> resolveJumpLandingFeedback = null)
        {
            if (result?.PresentationData == null ||
                hasActiveLocalMotionTrack == null ||
                hasLocalMotionTrack == null)
            {
                return false;
            }

            var submittedMotion = Present(
                result.TickIndex,
                result.PresentationData.BoxSlideStartSignals,
                result.PresentationData.FlipImpactSignals,
                result.PresentationData.FlipFloorImpactSignals,
                hasActiveLocalMotionTrack,
                hasLocalMotionTrack);
            var submittedPlayerImpact = PresentPlayerImpacts(
                result.TickIndex,
                result.PresentationData.PlayerDamageSignals,
                result.PresentationData.PlayerDeathSignals);
            RegisterHeavyEnemyJumpLandings(
                result.TickIndex,
                result.PresentationData.EnemyJumpSignals,
                resolveJumpLandingFeedback);
            return submittedMotion || submittedPlayerImpact;
        }

        internal bool ObserveHeavyEnemyJumpLandingMilestones(
            Func<int, bool> hasActiveLandingCompletionTrack,
            Func<int, Vector3?> resolvePresentationAnchor,
            IGameplayCameraVisibilityPort visibilityPort,
            bool isTopologyTransitionActive)
        {
            if (_pendingEnemyJumpLandings.Count == 0)
            {
                return false;
            }

            _orderedEnemyJumpLandingIdentities.Clear();
            _orderedEnemyJumpLandingIdentities.AddRange(_pendingEnemyJumpLandings.Keys);
            _orderedEnemyJumpLandingIdentities.Sort(CompareRequestIdentities);
            _completedMilestoneIdentities.Clear();
            var submitted = false;

            for (var index = 0; index < _orderedEnemyJumpLandingIdentities.Count; index++)
            {
                var identity = _orderedEnemyJumpLandingIdentities[index];
                var pending = _pendingEnemyJumpLandings[identity];
                var request = pending.Request;
                if (hasActiveLandingCompletionTrack != null &&
                    hasActiveLandingCompletionTrack(request.SourceEntityId))
                {
                    continue;
                }

                var anchor = resolvePresentationAnchor?.Invoke(request.SourceEntityId);
                if (!anchor.HasValue || visibilityPort == null)
                {
                    _missingAnchorHeavyEnemyJumpLandingCount++;
                    _completedMilestoneIdentities.Add(identity);
                    continue;
                }

                if (!visibilityPort.TryProjectUnshakenWorldPoint(anchor.Value, out var viewportPoint) ||
                    !IsWithinViewportMargin(viewportPoint, HeavyEnemyJumpLandingViewportMargin))
                {
                    _offscreenHeavyEnemyJumpLandingCount++;
                    _completedMilestoneIdentities.Add(identity);
                    continue;
                }

                if (_impulseSink.Submit(request))
                {
                    _acceptedHeavyEnemyJumpLandingCount++;
                    submitted = true;
                }
                else if (isTopologyTransitionActive)
                {
                    _topologySuppressedHeavyEnemyJumpLandingCount++;
                }

                _completedMilestoneIdentities.Add(identity);
            }

            foreach (var identity in _completedMilestoneIdentities)
            {
                _pendingEnemyJumpLandings.Remove(identity);
            }

            _completedMilestoneIdentities.Clear();
            _orderedEnemyJumpLandingIdentities.Clear();
            return submitted;
        }

        internal bool Present(
            int tickIndex,
            IReadOnlyList<BoxSlideStartPresentationSignal> pushSlideStartSignals,
            IReadOnlyList<FlipFloorImpactPresentationSignal> flipFloorImpactSignals,
            Func<int, TickEntityMotionKind, int, bool> hasActiveLocalMotionTrack,
            Func<int, TickEntityMotionKind, int, bool> hasLocalMotionTrack)
        {
            return Present(
                tickIndex,
                pushSlideStartSignals,
                Array.Empty<FlipImpactPresentationSignal>(),
                flipFloorImpactSignals,
                hasActiveLocalMotionTrack,
                hasLocalMotionTrack);
        }

        internal bool Present(
            int tickIndex,
            IReadOnlyList<BoxSlideStartPresentationSignal> pushSlideStartSignals,
            IReadOnlyList<FlipImpactPresentationSignal> flipImpactSignals,
            IReadOnlyList<FlipFloorImpactPresentationSignal> flipFloorImpactSignals,
            Func<int, TickEntityMotionKind, int, bool> hasActiveLocalMotionTrack,
            Func<int, TickEntityMotionKind, int, bool> hasLocalMotionTrack)
        {
            if (tickIndex < 0 ||
                hasActiveLocalMotionTrack == null ||
                hasLocalMotionTrack == null)
            {
                return false;
            }

            var submittedPush = PresentPushSlideLaunches(
                tickIndex,
                pushSlideStartSignals,
                hasActiveLocalMotionTrack);
            RegisterOrdinaryFlipLandings(
                tickIndex,
                flipFloorImpactSignals,
                hasLocalMotionTrack);
            RegisterHostileFlipImpacts(
                tickIndex,
                flipImpactSignals,
                flipFloorImpactSignals);
            return submittedPush;
        }

        public bool ObserveMotionProgress(IReadOnlyList<MotionTrackProgressSample> progressSamples)
        {
            if (progressSamples == null ||
                progressSamples.Count == 0 ||
                _pendingMilestones.Count == 0)
            {
                return false;
            }

            var submittedMilestone = false;
            _completedMilestoneIdentities.Clear();
            for (var sampleIndex = 0; sampleIndex < progressSamples.Count; sampleIndex++)
            {
                var sample = progressSamples[sampleIndex];
                if (!sample.IsValid ||
                    sample.EntityId <= 0 ||
                    sample.MotionKind != TickEntityMotionKind.Flip)
                {
                    continue;
                }

                foreach (var pair in _pendingMilestones)
                {
                    if (_completedMilestoneIdentities.Contains(pair.Key))
                    {
                        continue;
                    }

                    var pending = pair.Value;
                    if (pending.ProgressSource != sample.SourceKind ||
                        pending.Request.SourceEntityId != sample.EntityId ||
                        pending.Request.SequenceOrActionPlanId != sample.SequenceOrActionPlanId ||
                        sample.PreviousNormalizedTime >= pending.ContactNormalizedTime ||
                        sample.CurrentNormalizedTime < pending.ContactNormalizedTime)
                    {
                        continue;
                    }

                    _impulseSink.Submit(pending.Request);
                    _completedMilestoneIdentities.Add(pair.Key);
                    submittedMilestone = true;
                }
            }

            foreach (var identity in _completedMilestoneIdentities)
            {
                _pendingMilestones.Remove(identity);
            }

            _completedMilestoneIdentities.Clear();
            return submittedMilestone;
        }

        public void ResetSession()
        {
            _observedIdentities.Clear();
            _pendingMilestones.Clear();
            _pendingEnemyJumpLandings.Clear();
            _completedMilestoneIdentities.Clear();
            _orderedEnemyJumpLandingIdentities.Clear();
            _flipOutcomesByAction.Clear();
            _acceptedPlayerDamageImpactCount = 0;
            _acceptedPlayerLethalImpactCount = 0;
            _observedHeavyEnemyJumpLandingCount = 0;
            _acceptedHeavyEnemyJumpLandingCount = 0;
            _offscreenHeavyEnemyJumpLandingCount = 0;
            _missingAnchorHeavyEnemyJumpLandingCount = 0;
            _topologySuppressedHeavyEnemyJumpLandingCount = 0;
        }

        public void HardCleanup()
        {
            ResetSession();
        }

        private int CountPending(CameraShakeSemantic semantic)
        {
            var count = 0;
            foreach (var pending in _pendingMilestones.Values)
            {
                if (pending.Request.Semantic == semantic)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountObserved(CameraShakeSemantic semantic)
        {
            var count = 0;
            foreach (var identity in _observedIdentities)
            {
                if (identity.Semantic == semantic)
                {
                    count++;
                }
            }

            return count;
        }

        internal bool RegisterHeavyEnemyJumpLandings(
            int tickIndex,
            IReadOnlyList<TickEnemyJumpPresentationSignal> signals,
            Func<int, EnemyJumpLandingCameraFeedbackKind> resolveJumpLandingFeedback)
        {
            if (tickIndex < 0 || signals == null || resolveJumpLandingFeedback == null)
            {
                return false;
            }

            var registered = false;
            for (var index = 0; index < signals.Count; index++)
            {
                var signal = signals[index];
                if (!signal.LandedThisTick ||
                    (signal.Outcome != TickEnemyJumpPresentationOutcome.Landed &&
                     signal.Outcome != TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded) ||
                    signal.EntityId <= 0 ||
                    signal.Sequence <= 0 ||
                    resolveJumpLandingFeedback(signal.EntityId) !=
                    EnemyJumpLandingCameraFeedbackKind.Heavy)
                {
                    continue;
                }

                var request = new CameraShakeImpulseRequest(
                    tickIndex,
                    CameraShakeSemantic.HeavyEnemyJumpLanding,
                    signal.EntityId,
                    signal.Sequence,
                    CameraShakePriority.Medium,
                    signal.PresentationTargetCell,
                    hasAnchor: true);
                if (!_observedIdentities.Add(request.Identity))
                {
                    continue;
                }

                _pendingEnemyJumpLandings.Add(
                    request.Identity,
                    new PendingEnemyJumpLanding(request));
                _observedHeavyEnemyJumpLandingCount++;
                registered = true;
            }

            return registered;
        }

        private static bool IsWithinViewportMargin(Vector3 viewportPoint, float margin)
        {
            return viewportPoint.z > 0f &&
                   viewportPoint.x >= -margin &&
                   viewportPoint.x <= 1f + margin &&
                   viewportPoint.y >= -margin &&
                   viewportPoint.y <= 1f + margin;
        }

        private static int CompareRequestIdentities(
            CameraShakeRequestIdentity left,
            CameraShakeRequestIdentity right)
        {
            var tickCompare = left.TickIndex.CompareTo(right.TickIndex);
            if (tickCompare != 0)
            {
                return tickCompare;
            }

            var semanticCompare = left.Semantic.CompareTo(right.Semantic);
            if (semanticCompare != 0)
            {
                return semanticCompare;
            }

            var entityCompare = left.SourceEntityId.CompareTo(right.SourceEntityId);
            return entityCompare != 0
                ? entityCompare
                : left.SequenceOrActionPlanId.CompareTo(right.SequenceOrActionPlanId);
        }

        internal bool PresentPlayerImpacts(
            int tickIndex,
            IReadOnlyList<TickPlayerDamagePresentationSignal> damageSignals,
            IReadOnlyList<TickPlayerDeathPresentationSignal> deathSignals)
        {
            if (tickIndex < 0)
            {
                return false;
            }

            var submitted = false;
            var lethalPlayerEntityIds = new HashSet<int>();
            if (deathSignals != null)
            {
                for (var index = 0; index < deathSignals.Count; index++)
                {
                    var signal = deathSignals[index];
                    if (!signal.DidDieThisTick || signal.EntityId <= 0)
                    {
                        continue;
                    }

                    lethalPlayerEntityIds.Add(signal.EntityId);
                    submitted |= SubmitPlayerImpact(
                        tickIndex,
                        signal.EntityId,
                        CameraShakeSemantic.PlayerLethalImpact,
                        CameraShakePriority.Heavy);
                }
            }

            if (damageSignals == null)
            {
                return submitted;
            }

            for (var index = 0; index < damageSignals.Count; index++)
            {
                var signal = damageSignals[index];
                if (!signal.TookDamageThisTick ||
                    signal.DamageAmount <= 0 ||
                    signal.EntityId <= 0 ||
                    lethalPlayerEntityIds.Contains(signal.EntityId))
                {
                    continue;
                }

                submitted |= SubmitPlayerImpact(
                    tickIndex,
                    signal.EntityId,
                    CameraShakeSemantic.PlayerDamageImpact,
                    CameraShakePriority.Light);
            }

            return submitted;
        }

        private bool SubmitPlayerImpact(
            int tickIndex,
            int playerEntityId,
            CameraShakeSemantic semantic,
            CameraShakePriority priority)
        {
            var request = new CameraShakeImpulseRequest(
                tickIndex,
                semantic,
                playerEntityId,
                sequenceOrActionPlanId: tickIndex,
                priority: priority,
                hasAnchor: false,
                hasDirection: false);
            if (!_observedIdentities.Add(request.Identity))
            {
                return false;
            }

            if (!_impulseSink.Submit(request))
            {
                return false;
            }

            if (semantic == CameraShakeSemantic.PlayerDamageImpact)
            {
                _acceptedPlayerDamageImpactCount++;
            }
            else if (semantic == CameraShakeSemantic.PlayerLethalImpact)
            {
                _acceptedPlayerLethalImpactCount++;
            }

            return true;
        }

        private bool PresentPushSlideLaunches(
            int tickIndex,
            IReadOnlyList<BoxSlideStartPresentationSignal> signals,
            Func<int, TickEntityMotionKind, int, bool> hasActiveLocalMotionTrack)
        {
            var submitted = false;
            if (signals == null)
            {
                return false;
            }

            for (var index = 0; index < signals.Count; index++)
            {
                var signal = signals[index];
                if (signal.BoxEntityId <= 0 ||
                    signal.SourceActionPlanId <= 0 ||
                    !hasActiveLocalMotionTrack(
                        signal.BoxEntityId,
                        TickEntityMotionKind.BoxSlide,
                        signal.SourceActionPlanId))
                {
                    continue;
                }

                var request = new CameraShakeImpulseRequest(
                    tickIndex,
                    CameraShakeSemantic.PushSlideLaunch,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    CameraShakePriority.Light,
                    signal.SourceCell,
                    hasAnchor: true);
                if (_observedIdentities.Add(request.Identity))
                {
                    _impulseSink.Submit(request);
                    submitted = true;
                }
            }

            return submitted;
        }

        private void RegisterOrdinaryFlipLandings(
            int tickIndex,
            IReadOnlyList<FlipFloorImpactPresentationSignal> signals,
            Func<int, TickEntityMotionKind, int, bool> hasLocalMotionTrack)
        {
            if (signals == null)
            {
                return;
            }

            for (var index = 0; index < signals.Count; index++)
            {
                var signal = signals[index];
                if (signal.Kind != FlipFloorImpactPresentationKind.Landing ||
                    signal.BoxEntityId <= 0 ||
                    signal.SourceActionPlanId <= 0 ||
                    signal.VisualContactNormalizedTime <= 0f ||
                    !hasLocalMotionTrack(
                        signal.BoxEntityId,
                        TickEntityMotionKind.Flip,
                        signal.SourceActionPlanId) ||
                    !TryRegisterFlipOutcome(
                        signal.BoxEntityId,
                        signal.SourceActionPlanId,
                        FlipCameraOutcomeKind.OrdinaryLanding))
                {
                    continue;
                }

                var request = new CameraShakeImpulseRequest(
                    tickIndex,
                    CameraShakeSemantic.FlipFloorLanding,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    CameraShakePriority.Medium,
                    signal.ContactCell,
                    hasAnchor: true);
                RegisterPending(
                    request,
                    signal.VisualContactNormalizedTime,
                    MotionTrackProgressSourceKind.LocalMotion);
            }
        }

        private void RegisterHostileFlipImpacts(
            int tickIndex,
            IReadOnlyList<FlipImpactPresentationSignal> impactSignals,
            IReadOnlyList<FlipFloorImpactPresentationSignal> floorSignals)
        {
            if (impactSignals != null)
            {
                for (var index = 0; index < impactSignals.Count; index++)
                {
                    var signal = impactSignals[index];
                    if (!TryResolveHostileContact(
                            signal.Disposition,
                            out var variant,
                            out var outcome,
                            out var progressSource,
                            out var priority) ||
                        signal.BoxEntityId <= 0 ||
                        signal.SourceActionPlanId <= 0 ||
                        !TryRegisterFlipOutcome(signal.BoxEntityId, signal.SourceActionPlanId, outcome))
                    {
                        continue;
                    }

                    var request = new CameraShakeImpulseRequest(
                        tickIndex,
                        CameraShakeSemantic.FlipHostileImpact,
                        signal.BoxEntityId,
                        signal.SourceActionPlanId,
                        priority,
                        signal.ImpactCell,
                        hasAnchor: true,
                        variant: variant);
                    RegisterPending(
                        request,
                        GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime,
                        progressSource);
                }
            }

            if (floorSignals == null)
            {
                return;
            }

            for (var index = 0; index < floorSignals.Count; index++)
            {
                var signal = floorSignals[index];
                const CameraShakeVariant variant = CameraShakeVariant.FlipHostileFollowThrough;
                if (signal.Kind != FlipFloorImpactPresentationKind.FollowThrough ||
                    signal.BoxEntityId <= 0 ||
                    signal.SourceActionPlanId <= 0 ||
                    signal.VisualContactNormalizedTime <= 0f ||
                    !TryRegisterFlipOutcome(
                        signal.BoxEntityId,
                        signal.SourceActionPlanId,
                        FlipCameraOutcomeKind.HostileFollowThrough))
                {
                    continue;
                }

                var request = new CameraShakeImpulseRequest(
                    tickIndex,
                    CameraShakeSemantic.FlipHostileImpact,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    CameraShakePriority.Heavy,
                    signal.ContactCell,
                    hasAnchor: true,
                    variant: variant);
                RegisterPending(
                    request,
                    signal.VisualContactNormalizedTime,
                    MotionTrackProgressSourceKind.LocalMotion);
            }
        }

        private void RegisterPending(
            in CameraShakeImpulseRequest request,
            float normalizedTime,
            MotionTrackProgressSourceKind progressSource)
        {
            if (!_observedIdentities.Add(request.Identity))
            {
                return;
            }

            _pendingMilestones.Add(
                request.Identity,
                new PendingCameraMilestone(request, normalizedTime, progressSource));
        }

        private bool TryRegisterFlipOutcome(
            int boxEntityId,
            int sourceActionPlanId,
            FlipCameraOutcomeKind outcome)
        {
            var identity = new FlipActionIdentity(boxEntityId, sourceActionPlanId);
            if (!_flipOutcomesByAction.TryGetValue(identity, out var existing))
            {
                _flipOutcomesByAction.Add(identity, outcome);
                return true;
            }

            if (existing == outcome)
            {
                return false;
            }

            throw new InvalidOperationException(
                $"Flip camera feedback received mutually exclusive outcomes for {identity}: " +
                $"'{existing}' and '{outcome}'.");
        }

        private static bool TryResolveHostileContact(
            FlipImpactPresentationDisposition disposition,
            out CameraShakeVariant variant,
            out FlipCameraOutcomeKind outcome,
            out MotionTrackProgressSourceKind progressSource,
            out CameraShakePriority priority)
        {
            switch (disposition)
            {
                case FlipImpactPresentationDisposition.Stay:
                    variant = CameraShakeVariant.FlipHostileStay;
                    outcome = FlipCameraOutcomeKind.HostileStay;
                    progressSource = MotionTrackProgressSourceKind.OriginalViewMotion;
                    priority = CameraShakePriority.Medium;
                    return true;

                case FlipImpactPresentationDisposition.DestroySelf:
                    variant = CameraShakeVariant.FlipHostileDestroySelf;
                    outcome = FlipCameraOutcomeKind.HostileDestroySelf;
                    progressSource = MotionTrackProgressSourceKind.FlipInteraction;
                    priority = CameraShakePriority.Medium;
                    return true;

                default:
                    variant = CameraShakeVariant.Default;
                    outcome = default;
                    progressSource = default;
                    priority = default;
                    return false;
            }
        }
    }
}
