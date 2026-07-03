using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
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

    internal readonly struct PresentationPoseCandidate
    {
        public PresentationPoseCandidate(
            PresentationEntityKey entity,
            PresentationOwnerRole ownerRole,
            PresentationPoseSourceKind sourceKind,
            PresentationPoseChannel channel,
            GameplayEntityPose pose,
            int sourceTick,
            bool isTerminal,
            bool isActiveLocomotion)
        {
            Entity = entity;
            OwnerRole = ownerRole;
            SourceKind = sourceKind;
            Channel = channel;
            Pose = pose;
            SourceTick = sourceTick;
            IsTerminal = isTerminal;
            IsActiveLocomotion = isActiveLocomotion;
        }

        public PresentationEntityKey Entity { get; }

        public PresentationOwnerRole OwnerRole { get; }

        public PresentationPoseSourceKind SourceKind { get; }

        public PresentationPoseChannel Channel { get; }

        public GameplayEntityPose Pose { get; }

        public int SourceTick { get; }

        public bool IsTerminal { get; }

        public bool IsActiveLocomotion { get; }
    }

    internal readonly struct PresentationPoseRejection
    {
        public PresentationPoseRejection(
            PresentationEntityKey entity,
            PresentationOwnerRole ownerRole,
            PresentationPoseSourceKind sourceKind,
            PresentationPoseChannel channel,
            PresentationPoseRejectionReason reason)
        {
            Entity = entity;
            OwnerRole = ownerRole;
            SourceKind = sourceKind;
            Channel = channel;
            Reason = reason;
        }

        public PresentationEntityKey Entity { get; }

        public PresentationOwnerRole OwnerRole { get; }

        public PresentationPoseSourceKind SourceKind { get; }

        public PresentationPoseChannel Channel { get; }

        public PresentationPoseRejectionReason Reason { get; }
    }

    internal readonly struct ResolvedEntityPresentationFrame
    {
        public ResolvedEntityPresentationFrame(
            PresentationEntityKey entity,
            PresentationOwnerRole ownerRole,
            GameplayEntityPose basePose,
            PresentationPoseProvenance provenance,
            bool isActiveLocomotion)
        {
            Entity = entity;
            OwnerRole = ownerRole;
            BasePose = basePose;
            Provenance = provenance;
            IsActiveLocomotion = isActiveLocomotion;
        }

        public PresentationEntityKey Entity { get; }

        public PresentationOwnerRole OwnerRole { get; }

        public GameplayEntityPose BasePose { get; }

        public PresentationPoseProvenance Provenance { get; }

        public bool IsActiveLocomotion { get; }
    }

    internal readonly struct ResolvedEntityPresentationAdditiveLocalOffset
    {
        public ResolvedEntityPresentationAdditiveLocalOffset(
            PresentationEntityKey entity,
            PresentationOwnerRole ownerRole,
            Vector3 offset,
            PresentationPoseProvenance provenance)
        {
            Entity = entity;
            OwnerRole = ownerRole;
            Offset = offset;
            Provenance = provenance;
        }

        public PresentationEntityKey Entity { get; }

        public PresentationOwnerRole OwnerRole { get; }

        public PresentationPoseChannel Channel => PresentationPoseChannel.AdditiveLocalOffset;

        public Vector3 Offset { get; }

        public PresentationPoseProvenance Provenance { get; }
    }

    internal readonly struct ResolvedEntityPresentationAdditiveRotation
    {
        public ResolvedEntityPresentationAdditiveRotation(
            PresentationEntityKey entity,
            PresentationOwnerRole ownerRole,
            Quaternion rotation,
            PresentationPoseProvenance provenance)
        {
            Entity = entity;
            OwnerRole = ownerRole;
            Rotation = rotation;
            Provenance = provenance;
        }

        public PresentationEntityKey Entity { get; }

        public PresentationOwnerRole OwnerRole { get; }

        public PresentationPoseChannel Channel => PresentationPoseChannel.AdditiveRotation;

        public Quaternion Rotation { get; }

        public PresentationPoseProvenance Provenance { get; }
    }

    internal sealed class ResolvedPresentationFrameSet
    {
        private readonly Dictionary<PresentationEntityKey, ResolvedEntityPresentationFrame> _framesByEntity = new();
        private readonly List<int> _entityIds = new();
        private readonly List<PresentationPoseRejection> _rejections = new();

        public IReadOnlyList<int> EntityIds => _entityIds;

        public IReadOnlyList<PresentationPoseRejection> Rejections => _rejections;

        public void Clear()
        {
            _framesByEntity.Clear();
            _entityIds.Clear();
            _rejections.Clear();
        }

        public bool TryGetFrame(int entityId, out ResolvedEntityPresentationFrame frame)
        {
            return _framesByEntity.TryGetValue(new PresentationEntityKey(entityId), out frame);
        }

        public void SetFrame(in ResolvedEntityPresentationFrame frame)
        {
            if (!_framesByEntity.ContainsKey(frame.Entity))
            {
                _entityIds.Add(frame.Entity.EntityId);
            }

            _framesByEntity[frame.Entity] = frame;
        }

        public void AddRejection(in PresentationPoseRejection rejection)
        {
            _rejections.Add(rejection);
        }
    }

    internal sealed class ResolvedPresentationChannelSet
    {
        private readonly Dictionary<PresentationEntityKey, ResolvedEntityPresentationAdditiveLocalOffset>
            _additiveLocalOffsetsByEntity = new();
        private readonly Dictionary<PresentationEntityKey, ResolvedEntityPresentationAdditiveRotation>
            _additiveRotationsByEntity = new();
        private readonly List<int> _entityIds = new();

        public IReadOnlyList<int> EntityIds => _entityIds;

        public void Clear()
        {
            _additiveLocalOffsetsByEntity.Clear();
            _additiveRotationsByEntity.Clear();
            _entityIds.Clear();
        }

        public bool TryGetAdditiveLocalOffset(
            int entityId,
            out ResolvedEntityPresentationAdditiveLocalOffset channel)
        {
            return _additiveLocalOffsetsByEntity.TryGetValue(new PresentationEntityKey(entityId), out channel);
        }

        public bool TryGetAdditiveRotation(
            int entityId,
            out ResolvedEntityPresentationAdditiveRotation channel)
        {
            return _additiveRotationsByEntity.TryGetValue(new PresentationEntityKey(entityId), out channel);
        }

        public void SetAdditiveLocalOffset(in ResolvedEntityPresentationAdditiveLocalOffset channel)
        {
            if (_additiveLocalOffsetsByEntity.TryGetValue(channel.Entity, out var existing) &&
                existing.Provenance.BaseSource != channel.Provenance.BaseSource)
            {
                throw new System.InvalidOperationException(
                    $"Additive local offset for entity {channel.Entity.EntityId} is already resolved from {existing.Provenance.BaseSource}; cannot also resolve {channel.Provenance.BaseSource}.");
            }

            AddEntityId(channel.Entity);
            _additiveLocalOffsetsByEntity[channel.Entity] = channel;
        }

        public void SetAdditiveRotation(in ResolvedEntityPresentationAdditiveRotation channel)
        {
            AddEntityId(channel.Entity);
            _additiveRotationsByEntity[channel.Entity] = channel;
        }

        private void AddEntityId(PresentationEntityKey entity)
        {
            if (!_additiveLocalOffsetsByEntity.ContainsKey(entity) &&
                !_additiveRotationsByEntity.ContainsKey(entity))
            {
                _entityIds.Add(entity.EntityId);
            }
        }
    }

    internal sealed class PresentationResolvedChannelResolver
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        public PresentationResolvedChannelResolver(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState)
        {
            _stateStore = stateStore ?? throw new System.ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new System.ArgumentNullException(nameof(trackState));
        }

        public void ResolveAdditiveChannels(
            float deltaTime,
            bool hasActiveBoardRotationTween,
            int sourceTick,
            ResolvedPresentationFrameSet resolvedFrames,
            ResolvedPresentationChannelSet channelSet)
        {
            if (resolvedFrames == null)
            {
                throw new System.ArgumentNullException(nameof(resolvedFrames));
            }

            if (channelSet == null)
            {
                throw new System.ArgumentNullException(nameof(channelSet));
            }

            foreach (var pair in _trackState.JumpTracks)
            {
                var entityId = pair.Key;
                var jumpTrack = pair.Value;
                if (jumpTrack == null ||
                    !jumpTrack.HasClip ||
                    !resolvedFrames.TryGetFrame(entityId, out var resolvedFrame) ||
                    resolvedFrame.Provenance.TerminalSource != PresentationPoseSourceKind.None ||
                    resolvedFrame.OwnerRole != PresentationOwnerRole.Enemy)
                {
                    continue;
                }

                var freezeJumpTrack =
                    hasActiveBoardRotationTween ||
                    _trackState.JumpTopologySuspendedEntityIds.Contains(entityId);
                var sampledPose = freezeJumpTrack
                    ? jumpTrack.CurrentPose
                    : jumpTrack.SampleAndAdvance(deltaTime, resolvedFrame.BasePose);
                if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedState))
                {
                    _stateStore.JumpDetachedVisibilityStates[entityId] =
                        new JumpDetachedVisibilityState(
                            jumpDetachedState.JumpPhase,
                            sampledPose,
                            jumpDetachedState.AuthoritativeCell);
                }

                var entity = new PresentationEntityKey(entityId);
                var provenance = new PresentationPoseProvenance(
                    PresentationOwnerRole.Enemy,
                    PresentationPoseSourceKind.Jump,
                    PresentationPoseSourceKind.None,
                    sourceTick);
                channelSet.SetAdditiveLocalOffset(new ResolvedEntityPresentationAdditiveLocalOffset(
                    entity,
                    PresentationOwnerRole.Enemy,
                    sampledPose.Position - resolvedFrame.BasePose.Position,
                    provenance));
                channelSet.SetAdditiveRotation(new ResolvedEntityPresentationAdditiveRotation(
                    entity,
                    PresentationOwnerRole.Enemy,
                    sampledPose.Rotation,
                    provenance));

                if (!freezeJumpTrack && !jumpTrack.HasClip)
                {
                    _trackState.CompletedJumpTrackIds.Add(entityId);
                }
            }

            foreach (var pair in _trackState.JumpWindupRotationTracks)
            {
                var entityId = pair.Key;
                var jumpWindupRotationTrack = pair.Value;
                if (jumpWindupRotationTrack == null ||
                    !jumpWindupRotationTrack.HasClips ||
                    !resolvedFrames.TryGetFrame(entityId, out var resolvedFrame) ||
                    resolvedFrame.Provenance.TerminalSource != PresentationPoseSourceKind.None ||
                    resolvedFrame.OwnerRole != PresentationOwnerRole.Enemy ||
                    channelSet.TryGetAdditiveRotation(entityId, out _))
                {
                    continue;
                }

                var rotation = jumpWindupRotationTrack.SampleAndAdvance(deltaTime, resolvedFrame.BasePose.Rotation);
                var entity = new PresentationEntityKey(entityId);
                var provenance = new PresentationPoseProvenance(
                    PresentationOwnerRole.Enemy,
                    PresentationPoseSourceKind.EnemyJumpWindup,
                    PresentationPoseSourceKind.None,
                    sourceTick);
                channelSet.SetAdditiveRotation(new ResolvedEntityPresentationAdditiveRotation(
                    entity,
                    PresentationOwnerRole.Enemy,
                    rotation,
                    provenance));

                if (!jumpWindupRotationTrack.HasClips)
                {
                    _trackState.CompletedJumpWindupRotationTrackIds.Add(entityId);
                }
            }

            foreach (var pair in _trackState.PlayerFlipResultTurnTracks)
            {
                var entityId = pair.Key;
                var playerFlipResultTurnTrack = pair.Value;
                if (playerFlipResultTurnTrack == null ||
                    !playerFlipResultTurnTrack.Track.HasClips ||
                    !resolvedFrames.TryGetFrame(entityId, out var resolvedFrame))
                {
                    continue;
                }

                var rotation = playerFlipResultTurnTrack.Track.SampleAndAdvance(
                    deltaTime,
                    resolvedFrame.BasePose.Rotation);
                var entity = new PresentationEntityKey(entityId);
                var provenance = new PresentationPoseProvenance(
                    resolvedFrame.OwnerRole,
                    PresentationPoseSourceKind.FlipResultTurn,
                    PresentationPoseSourceKind.None,
                    sourceTick);
                channelSet.SetAdditiveRotation(new ResolvedEntityPresentationAdditiveRotation(
                    entity,
                    resolvedFrame.OwnerRole,
                    rotation,
                    provenance));

                if (!playerFlipResultTurnTrack.Track.HasClips)
                {
                    _trackState.CompletedPlayerFlipResultTurnTrackIds.Add(entityId);
                }
            }

            foreach (var pair in _trackState.GlidePresentationOffsetsByEntityId)
            {
                var entityId = pair.Key;
                if (!resolvedFrames.TryGetFrame(entityId, out var resolvedFrame) ||
                    resolvedFrame.Provenance.TerminalSource != PresentationPoseSourceKind.None ||
                    resolvedFrame.OwnerRole != PresentationOwnerRole.Enemy ||
                    _trackState.DeferredExitRetainedEntityIds.Contains(entityId) ||
                    _trackState.ContactDelayedRetainedEntityIds.Contains(entityId) ||
                    _trackState.DeathPresentationPlayingEntityIds.Contains(entityId))
                {
                    continue;
                }

                var entity = new PresentationEntityKey(entityId);
                var provenance = new PresentationPoseProvenance(
                    PresentationOwnerRole.Enemy,
                    PresentationPoseSourceKind.GlideOffset,
                    PresentationPoseSourceKind.None,
                    sourceTick);
                channelSet.SetAdditiveLocalOffset(new ResolvedEntityPresentationAdditiveLocalOffset(
                    entity,
                    PresentationOwnerRole.Enemy,
                    pair.Value,
                    provenance));
            }
        }
    }

    internal readonly struct PresentationVisibilityFallbackInputs
    {
        public PresentationVisibilityFallbackInputs(
            bool hasPresentationPoseOverride,
            bool hasPlayerDeathHoldPose,
            bool hasCommittedLocalTargetPose,
            bool hasActiveLocalMotion,
            bool hasActiveOriginalViewMotion,
            bool isDeferredExitRetained,
            bool isContactDelayedRetained,
            bool isDeathPresentationPlaying,
            bool hasResolvedVisibility,
            bool isResolvedVisible,
            bool hasTransitionVisibility)
        {
            HasPresentationPoseOverride = hasPresentationPoseOverride;
            HasPlayerDeathHoldPose = hasPlayerDeathHoldPose;
            HasCommittedLocalTargetPose = hasCommittedLocalTargetPose;
            HasActiveLocalMotion = hasActiveLocalMotion;
            HasActiveOriginalViewMotion = hasActiveOriginalViewMotion;
            IsDeferredExitRetained = isDeferredExitRetained;
            IsContactDelayedRetained = isContactDelayedRetained;
            IsDeathPresentationPlaying = isDeathPresentationPlaying;
            HasResolvedVisibility = hasResolvedVisibility;
            IsResolvedVisible = isResolvedVisible;
            HasTransitionVisibility = hasTransitionVisibility;
        }

        public bool HasPresentationPoseOverride { get; }

        public bool HasPlayerDeathHoldPose { get; }

        public bool HasCommittedLocalTargetPose { get; }

        public bool HasActiveLocalMotion { get; }

        public bool HasActiveOriginalViewMotion { get; }

        public bool IsDeferredExitRetained { get; }

        public bool IsContactDelayedRetained { get; }

        public bool IsDeathPresentationPlaying { get; }

        public bool HasResolvedVisibility { get; }

        public bool IsResolvedVisible { get; }

        public bool HasTransitionVisibility { get; }
    }

    internal static class PresentationVisibilityFallbackResolver
    {
        public static bool Resolve(in PresentationVisibilityFallbackInputs inputs)
        {
            return inputs.HasPresentationPoseOverride ||
                   inputs.HasPlayerDeathHoldPose ||
                   inputs.HasCommittedLocalTargetPose ||
                   inputs.HasActiveLocalMotion ||
                   inputs.HasActiveOriginalViewMotion ||
                   inputs.IsDeferredExitRetained ||
                   inputs.IsContactDelayedRetained ||
                   inputs.IsDeathPresentationPlaying ||
                   (inputs.HasResolvedVisibility && inputs.IsResolvedVisible) ||
                   inputs.HasTransitionVisibility;
        }
    }

    internal sealed class PresentationVisibilityCandidateCollector
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private readonly PresentationVisibilityCandidateWinnerResolver _winnerResolver = new();

        public PresentationVisibilityCandidateCollector(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState)
        {
            _stateStore = stateStore ?? throw new System.ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new System.ArgumentNullException(nameof(trackState));
        }

        public void CollectJumpDetachedVisibility(
            CubeTopologyState topology,
            int sourceTick,
            ResolvedPresentationFrameSet resolvedFrames,
            PresentationVisibilityCandidateSet candidateSet)
        {
            if (resolvedFrames == null)
            {
                throw new System.ArgumentNullException(nameof(resolvedFrames));
            }

            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            foreach (var pair in _stateStore.JumpDetachedVisibilityStates)
            {
                var entityId = pair.Key;
                var state = pair.Value;
                if (!resolvedFrames.TryGetFrame(entityId, out var resolvedFrame) ||
                    resolvedFrame.Provenance.TerminalSource != PresentationPoseSourceKind.None ||
                    resolvedFrame.OwnerRole != PresentationOwnerRole.Enemy ||
                    _trackState.DeferredExitRetainedEntityIds.Contains(entityId) ||
                    _trackState.ContactDelayedRetainedEntityIds.Contains(entityId) ||
                    _trackState.DeathPresentationPlayingEntityIds.Contains(entityId))
                {
                    continue;
                }

                var provenance = new PresentationVisibilityProvenance(
                    PresentationVisibilitySourceKind.JumpDetached,
                    PresentationOwnerRole.Enemy,
                    state.AuthoritativeCell,
                    state.AuthoritativeFace,
                    sourceTick);
                candidateSet.AddCandidate(new PresentationVisibilityCandidate(
                    new PresentationEntityKey(entityId),
                    state.JumpPhase == EnemyJumpPhase.Airborne &&
                    topology.IsFaceActive(state.AuthoritativeFace),
                    provenance,
                    priority: 200,
                    isFallback: false,
                    isStatefulTrackSample: false,
                    isHighPrioritySuppressionSource: false));
            }
        }

        public void CollectPostResolveVisibilityCarriers(
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            int sourceTick,
            PresentationVisibilityCandidateSet candidateSet)
        {
            if (visibilityChanges == null)
            {
                throw new System.ArgumentNullException(nameof(visibilityChanges));
            }

            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (!TryCreateGenericVisibilityCandidate(change, sourceTick, out var candidate))
                {
                    continue;
                }

                candidateSet.AddCandidate(candidate);
            }
        }

        public void CollectGenericVisibilitySpawnOnly(
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            int sourceTick,
            PresentationVisibilityCandidateSet candidateSet)
        {
            if (visibilityChanges == null)
            {
                throw new System.ArgumentNullException(nameof(visibilityChanges));
            }

            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            HashSet<int> suppressedSpawnEntityIds = null;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.EntityId <= 0 ||
                    change.ChangeKind != TickVisibilityChangeKind.Detach &&
                    change.ChangeKind != TickVisibilityChangeKind.Remove)
                {
                    continue;
                }

                suppressedSpawnEntityIds ??= new HashSet<int>();
                suppressedSpawnEntityIds.Add(change.EntityId);
            }

            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind != TickVisibilityChangeKind.Spawn ||
                    suppressedSpawnEntityIds != null &&
                    suppressedSpawnEntityIds.Contains(change.EntityId) ||
                    !TryCreateGenericVisibilityCandidate(change, sourceTick, out var candidate))
                {
                    continue;
                }

                candidateSet.AddCandidate(candidate);
            }
        }

        public void CollectRetainedDeathOrExitVisibility(
            int sourceTick,
            ResolvedPresentationFrameSet resolvedFrames,
            PresentationVisibilityCandidateSet candidateSet)
        {
            if (resolvedFrames == null)
            {
                throw new System.ArgumentNullException(nameof(resolvedFrames));
            }

            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            for (var i = 0; i < resolvedFrames.EntityIds.Count; i++)
            {
                var entityId = resolvedFrames.EntityIds[i];
                if (!resolvedFrames.TryGetFrame(entityId, out var resolvedFrame) ||
                    resolvedFrame.Provenance.TerminalSource != PresentationPoseSourceKind.PlayerDeathHold)
                {
                    continue;
                }

                candidateSet.AddCandidate(CreateVisibilityCandidate(
                    entityId,
                    resolvedFrame.OwnerRole,
                    PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression,
                    sourceTick,
                    priority: 900));
            }

            CollectRetainedDeathOrExitOwnerSet(
                _trackState.DeferredExitRetainedEntityIds,
                sourceTick,
                resolvedFrames,
                candidateSet);
            CollectRetainedDeathOrExitOwnerSet(
                _trackState.ContactDelayedRetainedEntityIds,
                sourceTick,
                resolvedFrames,
                candidateSet);
            CollectRetainedDeathOrExitOwnerSet(
                _trackState.DeathPresentationPlayingEntityIds,
                sourceTick,
                resolvedFrames,
                candidateSet);
        }

        public void CollectTransitionEntityVisibility(
            int sourceTick,
            PresentationVisibilityCandidateSet candidateSet)
        {
            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            foreach (var pair in _stateStore.TransitionVisibilityStates)
            {
                var entityId = pair.Key;
                if (entityId <= 0)
                {
                    continue;
                }

                var state = pair.Value;
                var provenance = new PresentationVisibilityProvenance(
                    PresentationVisibilitySourceKind.TransitionEntityVisibility,
                    ResolveOwnerRole(entityId),
                    null,
                    state.SurfaceFace,
                    sourceTick);
                candidateSet.AddCandidate(new PresentationVisibilityCandidate(
                    new PresentationEntityKey(entityId),
                    isVisible: true,
                    provenance,
                    priority: 600,
                    isFallback: false,
                    isStatefulTrackSample: false,
                    isHighPrioritySuppressionSource: false));
            }
        }

        public void CollectVisibilityTrackSamples(
            float deltaTime,
            int sourceTick,
            ResolvedPresentationFrameSet resolvedFrames,
            PresentationVisibilityCandidateSet candidateSet)
        {
            if (deltaTime < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (resolvedFrames == null)
            {
                throw new System.ArgumentNullException(nameof(resolvedFrames));
            }

            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            foreach (var pair in _trackState.VisibilityTracks)
            {
                var entityId = pair.Key;
                var visibilityTrack = pair.Value;
                if (visibilityTrack == null ||
                    !resolvedFrames.TryGetFrame(entityId, out var resolvedFrame))
                {
                    continue;
                }

                var hasPlayerDeathHoldPose =
                    resolvedFrame.Provenance.TerminalSource == PresentationPoseSourceKind.PlayerDeathHold;
                if (hasPlayerDeathHoldPose)
                {
                    continue;
                }

                var hasPresentationPoseOverride = IsLivePresentationPoseOverride(resolvedFrame.Provenance.BaseSource);
                var hasActiveLocalMotion = _trackState.LocalMotionTracks.TryGetValue(
                    entityId,
                    out var activeMotionTrack) &&
                    activeMotionTrack.HasClips;
                var hasActiveOriginalViewMotion = _trackState.OriginalViewMotionTracks.TryGetValue(
                    entityId,
                    out var activeOriginalViewMotionTrack) &&
                    !activeOriginalViewMotionTrack.IsComplete;
                var isDeferredExitRetained =
                    _trackState.DeferredExitRetainedEntityIds.Contains(entityId) &&
                    hasActiveLocalMotion &&
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId);
                var isContactDelayedRetained =
                    _trackState.ContactDelayedRetainedEntityIds.Contains(entityId) &&
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId);
                var isDeathPresentationPlaying =
                    _trackState.DeathPresentationPlayingEntityIds.Contains(entityId) &&
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId);
                var hasResolvedVisibility = _winnerResolver.TryResolveCandidateWinner(
                    candidateSet,
                    entityId,
                    out var resolvedEntityVisibility);
                var fallbackVisibility = PresentationVisibilityFallbackResolver.Resolve(
                    new PresentationVisibilityFallbackInputs(
                        hasPresentationPoseOverride,
                        hasPlayerDeathHoldPose,
                        _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId),
                        hasActiveLocalMotion,
                        hasActiveOriginalViewMotion,
                        isDeferredExitRetained,
                        isContactDelayedRetained,
                        isDeathPresentationPlaying,
                        hasResolvedVisibility,
                        hasResolvedVisibility && resolvedEntityVisibility.IsVisible,
                        _stateStore.TransitionVisibilityStates.ContainsKey(entityId)));
                var sampledVisibility = visibilityTrack.SampleWithoutAdvance(deltaTime, fallbackVisibility);
                var provenance = new PresentationVisibilityProvenance(
                    PresentationVisibilitySourceKind.VisibilityTrackSample,
                    resolvedFrame.OwnerRole,
                    null,
                    null,
                    sourceTick);
                candidateSet.AddCandidate(new PresentationVisibilityCandidate(
                    new PresentationEntityKey(entityId),
                    sampledVisibility,
                    provenance,
                    priority: 500,
                    isFallback: false,
                    isStatefulTrackSample: true,
                    isHighPrioritySuppressionSource: false));
            }
        }

        private static bool IsLivePresentationPoseOverride(PresentationPoseSourceKind sourceKind)
        {
            return sourceKind == PresentationPoseSourceKind.PlayerContinuousLocomotion ||
                   sourceKind == PresentationPoseSourceKind.EnemyKinematicMotion;
        }

        private bool TryCreateGenericVisibilityCandidate(
            TickVisibilityChange change,
            int sourceTick,
            out PresentationVisibilityCandidate candidate)
        {
            var sourceKind = ResolveGenericVisibilitySourceKind(change.ChangeKind);
            if (sourceKind == PresentationVisibilitySourceKind.None || change.EntityId <= 0)
            {
                candidate = default;
                return false;
            }

            var provenance = new PresentationVisibilityProvenance(
                sourceKind,
                ResolveOwnerRole(change.EntityId),
                change.Cell,
                change.Cell.face,
                sourceTick);
            candidate = new PresentationVisibilityCandidate(
                new PresentationEntityKey(change.EntityId),
                change.ChangeKind == TickVisibilityChangeKind.Spawn,
                provenance,
                ResolveGenericVisibilityPriority(change.ChangeKind),
                isFallback: false,
                isStatefulTrackSample: false,
                isHighPrioritySuppressionSource: false);
            return true;
        }

        private static PresentationVisibilitySourceKind ResolveGenericVisibilitySourceKind(
            TickVisibilityChangeKind changeKind)
        {
            return changeKind switch
            {
                TickVisibilityChangeKind.Spawn => PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                TickVisibilityChangeKind.Detach => PresentationVisibilitySourceKind.GenericVisibilityDetach,
                TickVisibilityChangeKind.Remove => PresentationVisibilitySourceKind.GenericVisibilityRemove,
                _ => PresentationVisibilitySourceKind.None,
            };
        }

        private static int ResolveGenericVisibilityPriority(TickVisibilityChangeKind changeKind)
        {
            return changeKind switch
            {
                TickVisibilityChangeKind.Spawn => 200,
                TickVisibilityChangeKind.Detach => 300,
                TickVisibilityChangeKind.Remove => 400,
                _ => 0,
            };
        }

        private void CollectRetainedDeathOrExitOwnerSet(
            HashSet<int> entityIds,
            int sourceTick,
            ResolvedPresentationFrameSet resolvedFrames,
            PresentationVisibilityCandidateSet candidateSet)
        {
            foreach (var entityId in entityIds)
            {
                if (!_stateStore.RetainedLocalTargetPoses.ContainsKey(entityId) ||
                    HasRetainedDeathOrExitCandidate(candidateSet, entityId))
                {
                    continue;
                }

                var ownerRole = resolvedFrames.TryGetFrame(entityId, out var resolvedFrame)
                    ? resolvedFrame.OwnerRole
                    : ResolveOwnerRole(entityId);
                candidateSet.AddCandidate(CreateVisibilityCandidate(
                    entityId,
                    ownerRole,
                    PresentationVisibilitySourceKind.RetainedDeathOrExit,
                    sourceTick,
                    priority: 800));
            }
        }

        private static bool HasRetainedDeathOrExitCandidate(
            PresentationVisibilityCandidateSet candidateSet,
            int entityId)
        {
            if (!candidateSet.TryGetCandidates(entityId, out var candidates))
            {
                return false;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Provenance.SourceKind == PresentationVisibilitySourceKind.RetainedDeathOrExit)
                {
                    return true;
                }
            }

            return false;
        }

        private static PresentationVisibilityCandidate CreateVisibilityCandidate(
            int entityId,
            PresentationOwnerRole ownerRole,
            PresentationVisibilitySourceKind sourceKind,
            int sourceTick,
            int priority)
        {
            return new PresentationVisibilityCandidate(
                new PresentationEntityKey(entityId),
                isVisible: true,
                new PresentationVisibilityProvenance(
                    sourceKind,
                    ownerRole,
                    null,
                    null,
                    sourceTick),
                priority,
                isFallback: false,
                isStatefulTrackSample: false,
                isHighPrioritySuppressionSource: true);
        }

        private PresentationOwnerRole ResolveOwnerRole(int entityId)
        {
            if (_stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType))
            {
                if (entityType == EntityType.Box)
                {
                    return PresentationOwnerRole.Box;
                }

                if (entityType == EntityType.Unit &&
                    _stateStore.UnitRolesByEntityId.TryGetValue(entityId, out var unitRole))
                {
                    return unitRole switch
                    {
                        UnitRole.Player => PresentationOwnerRole.Player,
                        UnitRole.Enemy => PresentationOwnerRole.Enemy,
                        _ => PresentationOwnerRole.NeutralUnit,
                    };
                }
            }

            return PresentationOwnerRole.Unknown;
        }
    }

    internal sealed class PresentationResolvedVisibilityResolver
    {
        private readonly PresentationVisibilityCandidateWinnerResolver _winnerResolver = new();

        public void ResolveCandidates(
            PresentationVisibilityCandidateSet candidateSet,
            ResolvedPresentationVisibilitySet visibilitySet)
        {
            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            if (visibilitySet == null)
            {
                throw new System.ArgumentNullException(nameof(visibilitySet));
            }

            for (var i = 0; i < candidateSet.EntityIds.Count; i++)
            {
                var entityId = candidateSet.EntityIds[i];
                if (!candidateSet.TryGetCandidates(entityId, out var candidates) ||
                    candidates.Count == 0)
                {
                    continue;
                }

                var winner = _winnerResolver.ResolveWinner(candidates);
                visibilitySet.SetVisibility(new ResolvedEntityPresentationVisibility(
                    winner.EntityKey,
                    winner.IsVisible,
                    winner.Provenance));
            }
        }
    }

    internal sealed class PresentationVisibilityCandidateWinnerResolver
    {
        public bool TryResolveCandidateWinner(
            PresentationVisibilityCandidateSet candidateSet,
            int entityId,
            out PresentationVisibilityCandidate candidate)
        {
            if (candidateSet == null)
            {
                throw new System.ArgumentNullException(nameof(candidateSet));
            }

            if (!candidateSet.TryGetCandidates(entityId, out var candidates) ||
                candidates.Count == 0)
            {
                candidate = default;
                return false;
            }

            candidate = ResolveWinner(candidates);
            return true;
        }

        public PresentationVisibilityCandidate ResolveWinner(
            IReadOnlyList<PresentationVisibilityCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new System.ArgumentNullException(nameof(candidates));
            }

            if (candidates.Count == 0)
            {
                throw new System.ArgumentException("At least one visibility candidate is required.", nameof(candidates));
            }

            var winner = candidates[0];
            for (var i = 1; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (Compare(candidate, winner) > 0)
                {
                    winner = candidate;
                }
            }

            return winner;
        }

        private static int Compare(
            in PresentationVisibilityCandidate candidate,
            in PresentationVisibilityCandidate winner)
        {
            var priorityCompare = candidate.Priority.CompareTo(winner.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            var sourceRankCompare = ResolveSourceRank(candidate.Provenance.SourceKind)
                .CompareTo(ResolveSourceRank(winner.Provenance.SourceKind));
            if (sourceRankCompare != 0)
            {
                return sourceRankCompare;
            }

            var lifetimeCompare = candidate.Provenance.LifetimeToken.CompareTo(winner.Provenance.LifetimeToken);
            if (lifetimeCompare != 0)
            {
                return lifetimeCompare;
            }

            var ownerCompare = ((int)candidate.Provenance.OwnerRole).CompareTo((int)winner.Provenance.OwnerRole);
            if (ownerCompare != 0)
            {
                return ownerCompare;
            }

            var visibilityCompare = candidate.IsVisible.CompareTo(winner.IsVisible);
            if (visibilityCompare != 0)
            {
                return visibilityCompare;
            }

            var fallbackCompare = winner.IsFallback.CompareTo(candidate.IsFallback);
            if (fallbackCompare != 0)
            {
                return fallbackCompare;
            }

            return winner.IsStatefulTrackSample.CompareTo(candidate.IsStatefulTrackSample);
        }

        private static int ResolveSourceRank(PresentationVisibilitySourceKind sourceKind)
        {
            return sourceKind switch
            {
                PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression => 900,
                PresentationVisibilitySourceKind.VisibilityTrackSample => 700,
                PresentationVisibilitySourceKind.RetainedDeathOrExit => 650,
                PresentationVisibilitySourceKind.TransitionEntityVisibility => 600,
                PresentationVisibilitySourceKind.GenericVisibilityRemove => 500,
                PresentationVisibilitySourceKind.GenericVisibilityDetach => 400,
                PresentationVisibilitySourceKind.JumpDetached => 300,
                PresentationVisibilitySourceKind.GenericVisibilitySpawn => 200,
                PresentationVisibilitySourceKind.GenericVisibility => 100,
                PresentationVisibilitySourceKind.CommittedMotionFallback => 50,
                _ => 0,
            };
        }
    }

    internal sealed class PresentationPoseCandidateCollector
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private readonly HashSet<int> _candidateEntityIds = new();
        private readonly List<int> _candidateEntityIdBuffer = new();

        public PresentationPoseCandidateCollector(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState)
        {
            _stateStore = stateStore ?? throw new System.ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new System.ArgumentNullException(nameof(trackState));
        }

        public IReadOnlyList<int> CollectEntityIds()
        {
            _candidateEntityIds.Clear();
            _candidateEntityIdBuffer.Clear();

            AddEntityIds(_stateStore.CommittedLocalTargetPoses);
            AddEntityIds(_stateStore.TransitionVisibilityStates);
            AddEntityIds(_stateStore.JumpDetachedVisibilityStates);
            AddEntityIds(_stateStore.RetainedLocalTargetPoses);
            AddEntityIds(_trackState.PlayerContinuousLocomotionPresentationPoseOverrides);
            AddEntityIds(_trackState.EnemyKinematicPresentationPoseOverrides);
            AddEntityIds(_trackState.PlayerDeathHoldPoses);

            _candidateEntityIdBuffer.Sort();
            return _candidateEntityIdBuffer;
        }

        public void CollectCandidatesForEntity(
            int entityId,
            int sourceTick,
            List<PresentationPoseCandidate> candidates)
        {
            candidates.Clear();
            var key = new PresentationEntityKey(entityId);
            var ownerRole = ResolveOwnerRole(entityId);

            if (_trackState.PlayerContinuousLocomotionPresentationPoseOverrides.TryGetValue(
                    entityId,
                    out var continuousPose))
            {
                candidates.Add(new PresentationPoseCandidate(
                    key,
                    ownerRole,
                    PresentationPoseSourceKind.PlayerContinuousLocomotion,
                    PresentationPoseChannel.BasePose,
                    continuousPose.LocalPose,
                    sourceTick,
                    isTerminal: false,
                    continuousPose.IsActiveLocomotion));
            }

            if (_trackState.EnemyKinematicPresentationPoseOverrides.TryGetValue(entityId, out var kinematicPose))
            {
                candidates.Add(new PresentationPoseCandidate(
                    key,
                    ownerRole,
                    PresentationPoseSourceKind.EnemyKinematicMotion,
                    PresentationPoseChannel.BasePose,
                    kinematicPose.LocalPose,
                    sourceTick,
                    isTerminal: false,
                    kinematicPose.IsActiveLocomotion));
            }

            if (_stateStore.CommittedLocalTargetPoses.TryGetValue(entityId, out var committedPose))
            {
                candidates.Add(new PresentationPoseCandidate(
                    key,
                    ownerRole,
                    PresentationPoseSourceKind.CommittedPose,
                    PresentationPoseChannel.BasePose,
                    committedPose,
                    sourceTick,
                    isTerminal: false,
                    isActiveLocomotion: false));
            }

            if (_stateStore.TransitionVisibilityStates.TryGetValue(entityId, out var transitionVisibilityState))
            {
                candidates.Add(new PresentationPoseCandidate(
                    key,
                    ownerRole,
                    PresentationPoseSourceKind.TransitionVisibilityPose,
                    PresentationPoseChannel.BasePose,
                    transitionVisibilityState.LocalPose,
                    sourceTick,
                    isTerminal: false,
                    isActiveLocomotion: false));
            }

            if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedVisibilityState))
            {
                candidates.Add(new PresentationPoseCandidate(
                    key,
                    ownerRole,
                    PresentationPoseSourceKind.JumpDetachedPose,
                    PresentationPoseChannel.BasePose,
                    jumpDetachedVisibilityState.LocalPose,
                    sourceTick,
                    isTerminal: false,
                    isActiveLocomotion: false));
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(entityId, out var retainedPose))
            {
                candidates.Add(new PresentationPoseCandidate(
                    key,
                    ownerRole,
                    PresentationPoseSourceKind.RetainedPose,
                    PresentationPoseChannel.BasePose,
                    retainedPose,
                    sourceTick,
                    isTerminal: false,
                    isActiveLocomotion: false));
            }

            if (_trackState.PlayerDeathHoldPoses.TryGetValue(entityId, out var playerDeathHoldPose))
            {
                candidates.Add(new PresentationPoseCandidate(
                    key,
                    ownerRole,
                    PresentationPoseSourceKind.PlayerDeathHold,
                    PresentationPoseChannel.TerminalHold,
                    playerDeathHoldPose,
                    sourceTick,
                    isTerminal: true,
                    isActiveLocomotion: false));
            }
        }

        private PresentationOwnerRole ResolveOwnerRole(int entityId)
        {
            if (_stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType))
            {
                if (entityType == EntityType.Box)
                {
                    return PresentationOwnerRole.Box;
                }

                if (entityType == EntityType.Unit &&
                    _stateStore.UnitRolesByEntityId.TryGetValue(entityId, out var unitRole))
                {
                    if (EntityRolePolicy.IsPlayerUnit(entityType, unitRole))
                    {
                        return PresentationOwnerRole.Player;
                    }

                    if (EntityRolePolicy.IsEnemyUnit(entityType, unitRole))
                    {
                        return PresentationOwnerRole.Enemy;
                    }

                    if (TryResolveRoleSpecificLaneOwner(entityId, out var laneOwnerRole))
                    {
                        return laneOwnerRole;
                    }

                    return PresentationOwnerRole.NeutralUnit;
                }
            }

            if (TryResolveRoleSpecificLaneOwner(entityId, out var roleSpecificLaneOwner))
            {
                return roleSpecificLaneOwner;
            }

            return PresentationOwnerRole.Unknown;
        }

        private bool TryResolveRoleSpecificLaneOwner(int entityId, out PresentationOwnerRole ownerRole)
        {
            if (_trackState.PlayerDeathHoldSignalEntityIds.Contains(entityId) ||
                _trackState.PlayerDeathHoldPoses.ContainsKey(entityId) ||
                _trackState.PlayerContinuousLocomotionPresentationPoseOverrides.ContainsKey(entityId))
            {
                ownerRole = PresentationOwnerRole.Player;
                return true;
            }

            if (_trackState.EnemyKinematicPresentationPoseOverrides.ContainsKey(entityId))
            {
                ownerRole = PresentationOwnerRole.Enemy;
                return true;
            }

            ownerRole = PresentationOwnerRole.Unknown;
            return false;
        }

        private void AddEntityId(int entityId)
        {
            if (_candidateEntityIds.Add(entityId))
            {
                _candidateEntityIdBuffer.Add(entityId);
            }
        }

        private void AddEntityIds<TValue>(Dictionary<int, TValue> source)
        {
            foreach (var pair in source)
            {
                AddEntityId(pair.Key);
            }
        }
    }

    internal sealed class PresentationBasePoseFrameResolver
    {
        private readonly PresentationPoseCandidateCollector _collector;
        private readonly List<PresentationPoseCandidate> _candidateBuffer = new();

        public PresentationBasePoseFrameResolver(PresentationPoseCandidateCollector collector)
        {
            _collector = collector ?? throw new System.ArgumentNullException(nameof(collector));
        }

        public void Resolve(int sourceTick, ResolvedPresentationFrameSet frameSet)
        {
            if (frameSet == null)
            {
                throw new System.ArgumentNullException(nameof(frameSet));
            }

            frameSet.Clear();
            var entityIds = _collector.CollectEntityIds();
            for (var i = 0; i < entityIds.Count; i++)
            {
                var entityId = entityIds[i];
                _collector.CollectCandidatesForEntity(entityId, sourceTick, _candidateBuffer);
                if (TryResolveFrame(_candidateBuffer, frameSet, out var frame))
                {
                    frameSet.SetFrame(frame);
                }
            }
        }

        private static bool TryResolveFrame(
            List<PresentationPoseCandidate> candidates,
            ResolvedPresentationFrameSet frameSet,
            out ResolvedEntityPresentationFrame frame)
        {
            frame = default;
            if (candidates.Count == 0)
            {
                return false;
            }

            PresentationPoseCandidate? terminalCandidate = null;
            PresentationPoseCandidate? liveCandidate = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!PresentationPoseCompatibilityPolicy.IsCompatible(
                        candidate.OwnerRole,
                        candidate.SourceKind,
                        candidate.Channel,
                        out var rejectionReason))
                {
                    frameSet.AddRejection(new PresentationPoseRejection(
                        candidate.Entity,
                        candidate.OwnerRole,
                        candidate.SourceKind,
                        candidate.Channel,
                        rejectionReason));
                    continue;
                }

                if (candidate.IsTerminal)
                {
                    terminalCandidate = SelectTerminalCandidate(terminalCandidate, candidate);
                    continue;
                }

                liveCandidate = SelectLiveCandidate(liveCandidate, candidate);
            }

            var selected = terminalCandidate ?? liveCandidate;
            if (!selected.HasValue)
            {
                return false;
            }

            var selectedCandidate = selected.Value;
            var terminalSource = terminalCandidate.HasValue
                ? selectedCandidate.SourceKind
                : PresentationPoseSourceKind.None;
            frame = new ResolvedEntityPresentationFrame(
                selectedCandidate.Entity,
                selectedCandidate.OwnerRole,
                selectedCandidate.Pose,
                new PresentationPoseProvenance(
                    selectedCandidate.OwnerRole,
                    selectedCandidate.SourceKind,
                    terminalSource,
                    selectedCandidate.SourceTick),
                selectedCandidate.IsActiveLocomotion);
            return true;
        }

        private static PresentationPoseCandidate SelectTerminalCandidate(
            PresentationPoseCandidate? current,
            PresentationPoseCandidate candidate)
        {
            if (!current.HasValue)
            {
                return candidate;
            }

            return ResolveTerminalPriority(candidate.SourceKind) < ResolveTerminalPriority(current.Value.SourceKind)
                ? candidate
                : current.Value;
        }

        private static PresentationPoseCandidate SelectLiveCandidate(
            PresentationPoseCandidate? current,
            PresentationPoseCandidate candidate)
        {
            if (!current.HasValue)
            {
                return candidate;
            }

            return ResolveLivePriority(candidate.OwnerRole, candidate.SourceKind) <
                   ResolveLivePriority(current.Value.OwnerRole, current.Value.SourceKind)
                ? candidate
                : current.Value;
        }

        private static int ResolveTerminalPriority(PresentationPoseSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case PresentationPoseSourceKind.PlayerDeathHold:
                case PresentationPoseSourceKind.EnemyDeathHold:
                    return 0;
                case PresentationPoseSourceKind.PresentedPose:
                    return 1;
                default:
                    return 10;
            }
        }

        private static int ResolveLivePriority(
            PresentationOwnerRole ownerRole,
            PresentationPoseSourceKind sourceKind)
        {
            if (ownerRole == PresentationOwnerRole.Player &&
                sourceKind == PresentationPoseSourceKind.PlayerContinuousLocomotion)
            {
                return 0;
            }

            if (ownerRole == PresentationOwnerRole.Enemy &&
                sourceKind == PresentationPoseSourceKind.EnemyKinematicMotion)
            {
                return 0;
            }

            switch (sourceKind)
            {
                case PresentationPoseSourceKind.CommittedPose:
                    return 10;
                case PresentationPoseSourceKind.TransitionVisibilityPose:
                    return 11;
                case PresentationPoseSourceKind.JumpDetachedPose:
                    return 12;
                case PresentationPoseSourceKind.RetainedPose:
                    return 13;
                default:
                    return 100;
            }
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

        public HashSet<int> VisibleEntityIds => _visibleEntityIds;

        public Dictionary<int, VisibilityTrack> VisibilityTracks => _visibilityTracks;

        public BoxMotionProductionTelemetryState BoxMotionTelemetry => _boxMotionTelemetry;

        public void ClearAirborneJumpTrackKeys(int entityId)
        {
            _activeAirborneJumpTrackKeys.Remove(entityId);
            _completedAirborneJumpTrackKeys.RemoveWhere(key => key.EntityId == entityId);
        }

        public void ClearPlayerTerminalHold(int entityId)
        {
            _playerDeathHoldPoses.Remove(entityId);
            _playerDeathHoldSignalEntityIds.Remove(entityId);
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
            _visibleEntityIds.Clear();
            _visibilityTracks.Clear();
        }
    }
}
