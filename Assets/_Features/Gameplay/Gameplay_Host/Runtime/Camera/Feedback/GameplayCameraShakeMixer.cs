using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayCameraShakeMixer : ICameraShakeImpulseSink
    {
        public const float MaxLocalPositionMagnitude = 0.08f;
        public const float MaxLocalRotationDegrees = 3f;
        public const float ReducedPositionMultiplier = 0.5f;
        public const float ReducedRotationMultiplier = 0.4f;

        private const float SamePriorityResidualRatio = 0.5f;
        private const float AdjacentPriorityResidualRatio = 0.25f;
        private const float DistantPriorityResidualRatio = 0.1f;
        private const float ZeroEpsilon = 0.0000001f;

        private sealed class ActiveImpulse
        {
            public CameraShakeImpulseRequest Request;
            public CameraShakeProfileEntry ProfileEntry;
            public double ElapsedSeconds;
            public bool HasProducedVisibleSample;
            public bool ExpireAfterCurrentSample;
        }

        private readonly List<ActiveImpulse> _activeImpulses = new();
        private readonly HashSet<CameraShakeRequestIdentity> _acceptedIdentities = new();
        private readonly Dictionary<CameraShakeCooldownKey, double> _lastAcceptedTimes = new();
        private readonly CameraShakeImpulseEvaluator _evaluator = new();
        private IReadOnlyDictionary<CameraShakeProfileKey, CameraShakeProfileEntry> _profileEntries;
        private CameraShakeContribution _topologyContribution;
        private CameraMotionLevel _motionLevel = CameraMotionLevel.Full;
        private CameraShakeMixResult _currentResult = CameraShakeMixResult.Zero;
        private double _presentationTimeSeconds;
        private bool _isPaused;

        public CameraShakeMixResult CurrentResult => _currentResult;

        public CameraMotionLevel MotionLevel => _motionLevel;

        internal int ActiveImpulseCount => _activeImpulses.Count;

        internal int AcceptedIdentityCount => _acceptedIdentities.Count;

        internal double PresentationTimeSeconds => _presentationTimeSeconds;

        public void ConfigureProfile(GameplayCameraShakeProfile profile)
        {
            _profileEntries = profile?.CreateValidatedEntryMap();
            _activeImpulses.Clear();
            _acceptedIdentities.Clear();
            _lastAcceptedTimes.Clear();
            RecomputeCurrentResult();
        }

        public bool Submit(in CameraShakeImpulseRequest request)
        {
            if (_profileEntries == null ||
                !_profileEntries.TryGetValue(request.ProfileKey, out var profileEntry) ||
                request.Priority != profileEntry.Priority ||
                !Enum.IsDefined(typeof(CameraShakePriority), request.Priority))
            {
                return false;
            }

            var identity = request.Identity;
            if (!_acceptedIdentities.Add(identity))
            {
                return false;
            }

            if (_topologyContribution.IsActive && request.Priority <= CameraShakePriority.Medium)
            {
                return false;
            }

            var cooldownKey = new CameraShakeCooldownKey(request.Semantic, request.SourceEntityId);
            if (_lastAcceptedTimes.TryGetValue(cooldownKey, out var lastAcceptedTime) &&
                _presentationTimeSeconds < lastAcceptedTime + profileEntry.CooldownSeconds)
            {
                return false;
            }

            _lastAcceptedTimes[cooldownKey] = _presentationTimeSeconds;
            _activeImpulses.Add(new ActiveImpulse
            {
                Request = request,
                ProfileEntry = profileEntry,
                ElapsedSeconds = 0.0,
            });
            _activeImpulses.Sort(CompareActiveImpulses);
            RecomputeCurrentResult();
            return true;
        }

        public void SetTopologyContribution(in CameraShakeContribution contribution)
        {
            _topologyContribution = contribution.SourceKind == CameraShakeSourceKind.TopologyContinuous
                ? contribution
                : CameraShakeContribution.Inactive;

            if (_topologyContribution.IsActive)
            {
                _activeImpulses.RemoveAll(active => active.Request.Priority <= CameraShakePriority.Medium);
            }

            RecomputeCurrentResult();
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta time must be zero or greater.");
            }

            if (_isPaused)
            {
                _currentResult = CameraShakeMixResult.Zero;
                return;
            }

            _presentationTimeSeconds += deltaSeconds;
            for (var index = _activeImpulses.Count - 1; index >= 0; index--)
            {
                var active = _activeImpulses[index];
                if (active.ExpireAfterCurrentSample)
                {
                    _activeImpulses.RemoveAt(index);
                    continue;
                }

                var elapsedCandidate = active.ElapsedSeconds + deltaSeconds;
                if (elapsedCandidate < active.ProfileEntry.DurationSeconds)
                {
                    active.ElapsedSeconds = elapsedCandidate;
                    continue;
                }

                if (active.HasProducedVisibleSample || _motionLevel == CameraMotionLevel.Off)
                {
                    _activeImpulses.RemoveAt(index);
                    continue;
                }

                active.ElapsedSeconds = ResolveRepresentativeSampleTime(active.ProfileEntry);
                active.ExpireAfterCurrentSample = true;
            }

            RecomputeCurrentResult();
        }

        public void SetMotionLevel(CameraMotionLevel motionLevel)
        {
            if (!Enum.IsDefined(typeof(CameraMotionLevel), motionLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(motionLevel));
            }

            _motionLevel = motionLevel;
            RecomputeCurrentResult();
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (paused)
            {
                _activeImpulses.Clear();
                _topologyContribution = CameraShakeContribution.Inactive;
                _currentResult = CameraShakeMixResult.Zero;
            }
        }

        public void CancelAllGameplayImpulses()
        {
            _activeImpulses.Clear();
            RecomputeCurrentResult();
        }

        public void HardReset()
        {
            _activeImpulses.Clear();
            _acceptedIdentities.Clear();
            _lastAcceptedTimes.Clear();
            _topologyContribution = CameraShakeContribution.Inactive;
            _presentationTimeSeconds = 0.0;
            _isPaused = false;
            _currentResult = CameraShakeMixResult.Zero;
        }

        private void RecomputeCurrentResult()
        {
            if (_isPaused || _motionLevel == CameraMotionLevel.Off)
            {
                _currentResult = CameraShakeMixResult.Zero;
                return;
            }

            if (_topologyContribution.IsActive)
            {
                RecomputeWithTopology();
                return;
            }

            var gameplayMix = EvaluateGameplayMix(CameraShakePriority.Light);
            _currentResult = CreateCappedResult(
                gameplayMix.LocalPosition,
                gameplayMix.LocalRotationDegrees,
                gameplayMix.IsActive);
        }

        private void RecomputeWithTopology()
        {
            var heavyMix = EvaluateGameplayMix(CameraShakePriority.Heavy);
            if (!heavyMix.IsActive ||
                (heavyMix.LocalPosition.sqrMagnitude <= ZeroEpsilon &&
                 heavyMix.LocalRotationDegrees.sqrMagnitude <= ZeroEpsilon))
            {
                _currentResult = CreateTopologyOnlyResult();
                return;
            }

            var position = SelectAbsoluteDominant(
                _topologyContribution.LocalPosition,
                heavyMix.LocalPosition);
            var rotationDegrees = SelectAbsoluteDominant(
                _topologyContribution.LocalRotationDegrees,
                heavyMix.LocalRotationDegrees);
            _currentResult = CreateCappedResult(position, rotationDegrees, true);
        }

        private CameraShakeMixResult CreateTopologyOnlyResult()
        {
            if (_motionLevel == CameraMotionLevel.Full)
            {
                return new CameraShakeMixResult(
                    _topologyContribution.LocalPosition,
                    _topologyContribution.LocalRotationDegrees,
                    _topologyContribution.LocalRotation,
                    true);
            }

            var reducedPosition = _topologyContribution.LocalPosition * ReducedPositionMultiplier;
            var reducedRotation = _topologyContribution.LocalRotationDegrees * ReducedRotationMultiplier;
            return new CameraShakeMixResult(
                reducedPosition,
                reducedRotation,
                Quaternion.Euler(reducedRotation),
                true);
        }

        private CameraShakeContribution EvaluateGameplayMix(CameraShakePriority minimumPriority)
        {
            var position = Vector3.zero;
            var rotationDegrees = Vector3.zero;
            var hasActiveContribution = false;
            var primaryPriority = CameraShakePriority.Light;

            for (var index = 0; index < _activeImpulses.Count; index++)
            {
                var active = _activeImpulses[index];
                if (active.Request.Priority < minimumPriority)
                {
                    continue;
                }

                var contribution = _evaluator.Evaluate(
                    active.Request,
                    active.ProfileEntry,
                    active.ElapsedSeconds);
                if (!contribution.IsActive)
                {
                    continue;
                }

                if (HasVisibleMagnitude(contribution))
                {
                    active.HasProducedVisibleSample = true;
                }

                var weight = 1f;
                if (hasActiveContribution)
                {
                    weight = ResolveResidualRatio(primaryPriority, contribution.Priority);
                }
                else
                {
                    primaryPriority = contribution.Priority;
                }

                position += contribution.LocalPosition * weight;
                rotationDegrees += contribution.LocalRotationDegrees * weight;
                hasActiveContribution = true;
            }

            return CameraShakeContribution.Gameplay(
                primaryPriority,
                position,
                rotationDegrees,
                hasActiveContribution);
        }

        private static double ResolveRepresentativeSampleTime(CameraShakeProfileEntry profileEntry)
        {
            if (profileEntry.AttackSeconds > 0f &&
                profileEntry.AttackSeconds < profileEntry.DurationSeconds)
            {
                return profileEntry.AttackSeconds;
            }

            return profileEntry.DurationSeconds * 0.5;
        }

        private static bool HasVisibleMagnitude(in CameraShakeContribution contribution)
        {
            return contribution.LocalPosition.sqrMagnitude > ZeroEpsilon ||
                   contribution.LocalRotationDegrees.sqrMagnitude > ZeroEpsilon;
        }

        private CameraShakeMixResult CreateCappedResult(
            Vector3 position,
            Vector3 rotationDegrees,
            bool isActive)
        {
            if (!isActive)
            {
                return CameraShakeMixResult.Zero;
            }

            var positionMultiplier = _motionLevel == CameraMotionLevel.Reduced
                ? ReducedPositionMultiplier
                : 1f;
            var rotationMultiplier = _motionLevel == CameraMotionLevel.Reduced
                ? ReducedRotationMultiplier
                : 1f;
            var cappedPosition = ClampMagnitude(position * positionMultiplier, MaxLocalPositionMagnitude);
            var cappedRotation = ClampMagnitude(rotationDegrees * rotationMultiplier, MaxLocalRotationDegrees);
            return new CameraShakeMixResult(
                cappedPosition,
                cappedRotation,
                cappedRotation.sqrMagnitude <= ZeroEpsilon
                    ? Quaternion.identity
                    : Quaternion.Euler(cappedRotation),
                true);
        }

        private static int CompareActiveImpulses(ActiveImpulse left, ActiveImpulse right)
        {
            var priorityComparison = ((int)right.Request.Priority).CompareTo((int)left.Request.Priority);
            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            var semanticComparison = ((int)left.Request.Semantic).CompareTo((int)right.Request.Semantic);
            if (semanticComparison != 0)
            {
                return semanticComparison;
            }

            var sourceComparison = left.Request.SourceEntityId.CompareTo(right.Request.SourceEntityId);
            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            var sequenceComparison =
                left.Request.SequenceOrActionPlanId.CompareTo(right.Request.SequenceOrActionPlanId);
            if (sequenceComparison != 0)
            {
                return sequenceComparison;
            }

            return left.Request.TickIndex.CompareTo(right.Request.TickIndex);
        }

        private static float ResolveResidualRatio(
            CameraShakePriority primaryPriority,
            CameraShakePriority residualPriority)
        {
            var tierDifference = ((int)primaryPriority - (int)residualPriority) / 100;
            if (tierDifference <= 0)
            {
                return SamePriorityResidualRatio;
            }

            return tierDifference == 1
                ? AdjacentPriorityResidualRatio
                : DistantPriorityResidualRatio;
        }

        private static Vector3 SelectAbsoluteDominant(Vector3 first, Vector3 second)
        {
            return new Vector3(
                Mathf.Abs(second.x) > Mathf.Abs(first.x) ? second.x : first.x,
                Mathf.Abs(second.y) > Mathf.Abs(first.y) ? second.y : first.y,
                Mathf.Abs(second.z) > Mathf.Abs(first.z) ? second.z : first.z);
        }

        private static Vector3 ClampMagnitude(Vector3 value, float maximumMagnitude)
        {
            var magnitude = value.magnitude;
            return magnitude > maximumMagnitude && magnitude > ZeroEpsilon
                ? value * (maximumMagnitude / magnitude)
                : value;
        }
    }
}
