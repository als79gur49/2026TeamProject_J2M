using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct EnemyAirbornePresentationKey : System.IEquatable<EnemyAirbornePresentationKey>
    {
        public EnemyAirbornePresentationKey(
            int entityId,
            int sequence,
            EnemyJumpPhase phase,
            int retryCount,
            int landingTick)
        {
            EntityId = entityId;
            Sequence = sequence;
            Phase = phase;
            RetryCount = retryCount;
            LandingTick = landingTick;
        }

        public int EntityId { get; }

        public int Sequence { get; }

        public EnemyJumpPhase Phase { get; }

        public int RetryCount { get; }

        public int LandingTick { get; }

        public static EnemyAirbornePresentationKey FromSignal(TickEnemyJumpPresentationSignal signal)
        {
            return new EnemyAirbornePresentationKey(
                signal.EntityId,
                signal.Sequence,
                signal.Phase,
                signal.RetryCount,
                signal.LandingTick);
        }

        public bool Equals(EnemyAirbornePresentationKey other)
        {
            return EntityId == other.EntityId &&
                   Sequence == other.Sequence &&
                   Phase == other.Phase &&
                   RetryCount == other.RetryCount &&
                   LandingTick == other.LandingTick;
        }

        public override bool Equals(object obj)
        {
            return obj is EnemyAirbornePresentationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = EntityId;
                hashCode = (hashCode * 397) ^ Sequence;
                hashCode = (hashCode * 397) ^ (int)Phase;
                hashCode = (hashCode * 397) ^ RetryCount;
                hashCode = (hashCode * 397) ^ LandingTick;
                return hashCode;
            }
        }

        public static bool operator ==(EnemyAirbornePresentationKey left, EnemyAirbornePresentationKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EnemyAirbornePresentationKey left, EnemyAirbornePresentationKey right)
        {
            return !left.Equals(right);
        }
    }

    internal readonly struct KinematicPresentationPose
    {
        public KinematicPresentationPose(
            GameplayEntityPose localPose,
            MotionMode motionMode,
            TickKinematicMotionTerminalKind terminalKind)
        {
            LocalPose = localPose;
            MotionMode = motionMode;
            TerminalKind = terminalKind;
        }

        public GameplayEntityPose LocalPose { get; }

        public MotionMode MotionMode { get; }

        public TickKinematicMotionTerminalKind TerminalKind { get; }

        public bool IsActiveLocomotion =>
            TerminalKind == TickKinematicMotionTerminalKind.None &&
            (MotionMode == MotionMode.Voluntary ||
             MotionMode == MotionMode.Charge);
    }

    internal readonly struct PlayerContinuousLocomotionPresentationPose
    {
        public PlayerContinuousLocomotionPresentationPose(
            GameplayEntityPose localPose,
            ContinuousLocomotionMode mode,
            TickKinematicMotionTerminalKind terminalKind)
        {
            LocalPose = localPose;
            Mode = mode;
            TerminalKind = terminalKind;
        }

        public GameplayEntityPose LocalPose { get; }

        public ContinuousLocomotionMode Mode { get; }

        public TickKinematicMotionTerminalKind TerminalKind { get; }

        public bool IsActiveLocomotion =>
            TerminalKind == TickKinematicMotionTerminalKind.None &&
            (Mode == ContinuousLocomotionMode.Moving ||
             Mode == ContinuousLocomotionMode.AlignToAnchor);
    }

    internal sealed class PlayerFlipResultTurnTrackEntry
    {
        public PlayerFlipResultTurnTrackEntry(
            int actionSequence,
            int startTick,
            Direction contactFacing,
            Direction resultFacing,
            RotationTrack track)
        {
            ActionSequence = actionSequence;
            StartTick = startTick;
            ContactFacing = contactFacing;
            ResultFacing = resultFacing;
            Track = track ?? throw new System.ArgumentNullException(nameof(track));
        }

        public int ActionSequence { get; }

        public int StartTick { get; }

        public Direction ContactFacing { get; }

        public Direction ResultFacing { get; }

        public RotationTrack Track { get; }
    }

    internal readonly struct PresentationVisibilityCandidate
    {
        public PresentationVisibilityCandidate(
            int entityId,
            TickVisibilityChangeKind changeKind,
            SurfaceCell sourceCell,
            CubeTopologyState sourceTopology,
            Direction facing,
            int priority)
        {
            EntityId = entityId;
            ChangeKind = changeKind;
            SourceCell = sourceCell;
            SourceTopology = sourceTopology;
            Facing = facing;
            Priority = priority;
        }

        public int EntityId { get; }

        public TickVisibilityChangeKind ChangeKind { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState SourceTopology { get; }

        public Direction Facing { get; }

        public int Priority { get; }

        public bool IsGenericDetachOrRemove =>
            ChangeKind == TickVisibilityChangeKind.Detach ||
            ChangeKind == TickVisibilityChangeKind.Remove;

        public static PresentationVisibilityCandidate FromChange(
            TickVisibilityChange change,
            int priority)
        {
            return new PresentationVisibilityCandidate(
                change.EntityId,
                change.ChangeKind,
                change.Cell,
                change.Topology,
                change.Facing,
                priority);
        }
    }

    internal readonly struct ResolvedEntityPresentationVisibility
    {
        public ResolvedEntityPresentationVisibility(
            int entityId,
            TickVisibilityChangeKind sourceKind,
            bool isVisible,
            bool isCompleted,
            VisibilityTrackSample trackSample)
        {
            EntityId = entityId;
            SourceKind = sourceKind;
            IsVisible = isVisible;
            IsCompleted = isCompleted;
            TrackSample = trackSample;
        }

        public int EntityId { get; }

        public TickVisibilityChangeKind SourceKind { get; }

        public bool IsVisible { get; }

        public bool IsCompleted { get; }

        public VisibilityTrackSample TrackSample { get; }
    }

    internal sealed class PresentationVisibilityCandidateSet
    {
        private readonly Dictionary<int, PresentationVisibilityCandidate> _highestPriorityByEntityId = new();
        private readonly List<PresentationVisibilityCandidate> _orderedCandidates = new();

        public IReadOnlyList<PresentationVisibilityCandidate> OrderedCandidates => _orderedCandidates;

        public IReadOnlyDictionary<int, PresentationVisibilityCandidate> HighestPriorityByEntityId =>
            _highestPriorityByEntityId;

        public void Clear()
        {
            _highestPriorityByEntityId.Clear();
            _orderedCandidates.Clear();
        }

        public void Add(PresentationVisibilityCandidate candidate)
        {
            if (_highestPriorityByEntityId.TryGetValue(candidate.EntityId, out var existing) &&
                existing.Priority >= candidate.Priority)
            {
                return;
            }

            _highestPriorityByEntityId[candidate.EntityId] = candidate;
            for (var i = 0; i < _orderedCandidates.Count; i++)
            {
                if (_orderedCandidates[i].EntityId == candidate.EntityId)
                {
                    _orderedCandidates[i] = candidate;
                    return;
                }
            }

            _orderedCandidates.Add(candidate);
        }
    }

    internal sealed class GameplayPresentationTrackState
    {
        private readonly List<int> _completedFlipInteractionTrackIds = new();
        private readonly List<int> _completedJumpTrackIds = new();
        private readonly List<int> _completedJumpWindupRotationTrackIds = new();
        private readonly List<int> _completedMotionTrackIds = new();
        private readonly List<int> _completedMotionVisualScaleEntityIds = new();
        private readonly List<int> _completedPlayerDeathDisplacementTrackIds = new();
        private readonly List<int> _completedPlayerFlipResultTurnTrackIds = new();
        private readonly List<int> _completedOriginalViewMotionTrackIds = new();
        private readonly List<FlipInteractionResetRequest> _flipInteractionResetRequests = new();
        private readonly Dictionary<int, FlipInteractionTrack> _flipInteractionTracks = new();
        private readonly HashSet<PresentationMotionInstanceKey> _completedPresentationMotionKeys = new();
        private readonly HashSet<int> _contactDelayedRetainedEntityIds = new();
        private readonly HashSet<int> _deathPresentationPlayingEntityIds = new();
        private readonly HashSet<int> _deferredExitRetainedEntityIds = new();
        private readonly Dictionary<int, EnemyAirbornePresentationKey> _activeAirborneJumpTrackKeys = new();
        private readonly HashSet<EnemyAirbornePresentationKey> _completedAirborneJumpTrackKeys = new();
        private readonly HashSet<int> _jumpLandingCompletionHoldEntityIds = new();
        private readonly HashSet<int> _jumpTopologySuspendedEntityIds = new();
        private readonly List<int> _completedTransitionVisibilityStateIds = new();
        private readonly List<int> _completedVisibilityTrackIds = new();
        private readonly Dictionary<int, JumpTrack> _jumpTracks = new();
        private readonly Dictionary<int, RotationTrack> _jumpWindupRotationTracks = new();
        private readonly Dictionary<int, PlayerFlipResultTurnTrackEntry> _playerFlipResultTurnTracks = new();
        private readonly Dictionary<int, KinematicPresentationPose> _enemyKinematicPresentationPoseOverrides = new();
        private readonly Dictionary<int, PlayerContinuousLocomotionPresentationPose>
            _playerContinuousLocomotionPresentationPoseOverrides = new();
        private readonly Dictionary<int, Vector3> _glidePresentationOffsetsByEntityId = new();
        private readonly Dictionary<int, MotionTrack> _localMotionTracks = new();
        private readonly HashSet<int> _motionVisualScaleEntityIds = new();
        private readonly Dictionary<int, GameplayEntityPose> _playerDeathHoldPoses = new();
        private readonly HashSet<int> _playerDeathHoldSignalEntityIds = new();
        private readonly Dictionary<int, PlayerDeathDisplacementTrack> _playerDeathDisplacementTracks = new();
        private readonly HashSet<int> _presentationEventTargetEntityIds = new();
        private readonly Dictionary<int, PresentationMotionTrack> _originalViewMotionTracks = new();
        private readonly Dictionary<int, TickPlayerLocomotionPresentationSignal> _playerLocomotionSignalsByEntityId = new();
        private readonly PresentationVisibilityCandidateSet _presentationVisibilityCandidates = new();
        private readonly Dictionary<int, ResolvedEntityPresentationVisibility>
            _resolvedEntityPresentationVisibilityByEntityId = new();
        private readonly HashSet<int> _visibleEntityIds = new();
        private readonly Dictionary<int, VisibilityTrack> _visibilityTracks = new();
        private readonly BoxMotionProductionTelemetryState _boxMotionTelemetry = new();

        public List<int> CompletedFlipInteractionTrackIds => _completedFlipInteractionTrackIds;

        public List<int> CompletedJumpTrackIds => _completedJumpTrackIds;

        public List<int> CompletedJumpWindupRotationTrackIds => _completedJumpWindupRotationTrackIds;

        public List<int> CompletedMotionTrackIds => _completedMotionTrackIds;

        public List<int> CompletedMotionVisualScaleEntityIds => _completedMotionVisualScaleEntityIds;

        public List<int> CompletedPlayerDeathDisplacementTrackIds => _completedPlayerDeathDisplacementTrackIds;

        public List<int> CompletedPlayerFlipResultTurnTrackIds => _completedPlayerFlipResultTurnTrackIds;

        public List<int> CompletedOriginalViewMotionTrackIds => _completedOriginalViewMotionTrackIds;

        public List<int> CompletedTransitionVisibilityStateIds => _completedTransitionVisibilityStateIds;

        public List<int> CompletedVisibilityTrackIds => _completedVisibilityTrackIds;

        public List<FlipInteractionResetRequest> FlipInteractionResetRequests => _flipInteractionResetRequests;

        public Dictionary<int, FlipInteractionTrack> FlipInteractionTracks => _flipInteractionTracks;

        public HashSet<PresentationMotionInstanceKey> CompletedPresentationMotionKeys => _completedPresentationMotionKeys;

        public HashSet<int> ContactDelayedRetainedEntityIds => _contactDelayedRetainedEntityIds;

        public HashSet<int> DeathPresentationPlayingEntityIds => _deathPresentationPlayingEntityIds;

        public HashSet<int> DeferredExitRetainedEntityIds => _deferredExitRetainedEntityIds;

        public Dictionary<int, EnemyAirbornePresentationKey> ActiveAirborneJumpTrackKeys => _activeAirborneJumpTrackKeys;

        public HashSet<EnemyAirbornePresentationKey> CompletedAirborneJumpTrackKeys => _completedAirborneJumpTrackKeys;

        public HashSet<int> JumpLandingCompletionHoldEntityIds => _jumpLandingCompletionHoldEntityIds;

        public HashSet<int> JumpTopologySuspendedEntityIds => _jumpTopologySuspendedEntityIds;

        public Dictionary<int, JumpTrack> JumpTracks => _jumpTracks;

        public Dictionary<int, RotationTrack> JumpWindupRotationTracks => _jumpWindupRotationTracks;

        public Dictionary<int, PlayerFlipResultTurnTrackEntry> PlayerFlipResultTurnTracks => _playerFlipResultTurnTracks;

        public Dictionary<int, KinematicPresentationPose> EnemyKinematicPresentationPoseOverrides =>
            _enemyKinematicPresentationPoseOverrides;

        public Dictionary<int, PlayerContinuousLocomotionPresentationPose>
            PlayerContinuousLocomotionPresentationPoseOverrides =>
                _playerContinuousLocomotionPresentationPoseOverrides;

        public Dictionary<int, Vector3> GlidePresentationOffsetsByEntityId => _glidePresentationOffsetsByEntityId;

        public Dictionary<int, MotionTrack> LocalMotionTracks => _localMotionTracks;

        public HashSet<int> MotionVisualScaleEntityIds => _motionVisualScaleEntityIds;

        public Dictionary<int, GameplayEntityPose> PlayerDeathHoldPoses => _playerDeathHoldPoses;

        public HashSet<int> PlayerDeathHoldSignalEntityIds => _playerDeathHoldSignalEntityIds;

        public Dictionary<int, PlayerDeathDisplacementTrack> PlayerDeathDisplacementTracks => _playerDeathDisplacementTracks;

        public HashSet<int> PresentationEventTargetEntityIds => _presentationEventTargetEntityIds;

        public Dictionary<int, PresentationMotionTrack> OriginalViewMotionTracks => _originalViewMotionTracks;

        public Dictionary<int, TickPlayerLocomotionPresentationSignal> PlayerLocomotionSignalsByEntityId =>
            _playerLocomotionSignalsByEntityId;

        public PresentationVisibilityCandidateSet PresentationVisibilityCandidates => _presentationVisibilityCandidates;

        public Dictionary<int, ResolvedEntityPresentationVisibility> ResolvedEntityPresentationVisibilityByEntityId =>
            _resolvedEntityPresentationVisibilityByEntityId;

        public HashSet<int> VisibleEntityIds => _visibleEntityIds;

        public Dictionary<int, VisibilityTrack> VisibilityTracks => _visibilityTracks;

        public BoxMotionProductionTelemetryState BoxMotionTelemetry => _boxMotionTelemetry;

        public void ClearAirborneJumpTrackKeys(int entityId)
        {
            _activeAirborneJumpTrackKeys.Remove(entityId);
            _completedAirborneJumpTrackKeys.RemoveWhere(key => key.EntityId == entityId);
        }

        public void ResetSession()
        {
            _completedFlipInteractionTrackIds.Clear();
            _completedPresentationMotionKeys.Clear();
            _completedJumpTrackIds.Clear();
            _completedJumpWindupRotationTrackIds.Clear();
            _completedMotionTrackIds.Clear();
            _completedMotionVisualScaleEntityIds.Clear();
            _completedPlayerDeathDisplacementTrackIds.Clear();
            _completedPlayerFlipResultTurnTrackIds.Clear();
            _completedOriginalViewMotionTrackIds.Clear();
            _flipInteractionResetRequests.Clear();
            _flipInteractionTracks.Clear();
            _contactDelayedRetainedEntityIds.Clear();
            _deathPresentationPlayingEntityIds.Clear();
            _deferredExitRetainedEntityIds.Clear();
            _activeAirborneJumpTrackKeys.Clear();
            _completedAirborneJumpTrackKeys.Clear();
            _jumpLandingCompletionHoldEntityIds.Clear();
            _jumpTopologySuspendedEntityIds.Clear();
            _completedTransitionVisibilityStateIds.Clear();
            _completedVisibilityTrackIds.Clear();
            _jumpTracks.Clear();
            _jumpWindupRotationTracks.Clear();
            _playerFlipResultTurnTracks.Clear();
            _enemyKinematicPresentationPoseOverrides.Clear();
            _playerContinuousLocomotionPresentationPoseOverrides.Clear();
            _glidePresentationOffsetsByEntityId.Clear();
            _localMotionTracks.Clear();
            _motionVisualScaleEntityIds.Clear();
            _playerDeathHoldPoses.Clear();
            _playerDeathHoldSignalEntityIds.Clear();
            _playerDeathDisplacementTracks.Clear();
            _presentationEventTargetEntityIds.Clear();
            _originalViewMotionTracks.Clear();
            _playerLocomotionSignalsByEntityId.Clear();
            _presentationVisibilityCandidates.Clear();
            _resolvedEntityPresentationVisibilityByEntityId.Clear();
            _visibleEntityIds.Clear();
            _visibilityTracks.Clear();
        }
    }
}
