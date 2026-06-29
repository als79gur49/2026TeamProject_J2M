using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    internal enum BoxMotionPresentationExecutionOwner
    {
        None = 0,
        CurrentExecutor = 1,
    }

    internal enum GameplayMotionPlaybackResultKind
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        DriverMissing = 4,
        Requested = 5,
        Started = 6,
        DuplicateActive = 7,
    }

    internal enum BoxMotionTelemetryFailureReason
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        DriverMissing = 4,
        PortMissing = 5,
        UnsupportedSemantic = 6,
        DuplicateRejected = 7,
        DuplicateActive = 8,
    }

    internal enum BoxMotionTelemetryCleanupReason
    {
        None = 0,
        PresentInitial = 1,
        ResetSession = 2,
        HardCleanupPresentationExtensions = 3,
        StageTerminal = 4,
        HostTeardown = 5,
    }

    internal readonly struct BoxMotionSemanticDiagnostics
    {
        public BoxMotionSemanticDiagnostics(
            PresentationMotionFactKind semantic,
            PresentationMotionCueKey cueKey,
            int plannedCount,
            int requestedCount,
            int startedCount,
            int completedCount,
            int duplicateRejectedCount,
            int missingDependencyCount,
            int cleanupCount,
            int lastTickIndex,
            int lastEntityId,
            int lastDedupeKey)
        {
            Semantic = semantic;
            CueKey = cueKey;
            PlannedCount = Math.Max(0, plannedCount);
            RequestedCount = Math.Max(0, requestedCount);
            StartedCount = Math.Max(0, startedCount);
            CompletedCount = Math.Max(0, completedCount);
            DuplicateRejectedCount = Math.Max(0, duplicateRejectedCount);
            MissingDependencyCount = Math.Max(0, missingDependencyCount);
            CleanupCount = Math.Max(0, cleanupCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastEntityId = Math.Max(0, lastEntityId);
            LastDedupeKey = lastDedupeKey;
        }

        public PresentationMotionFactKind Semantic { get; }

        public PresentationMotionCueKey CueKey { get; }

        public int PlannedCount { get; }

        public int RequestedCount { get; }

        public int StartedCount { get; }

        public int CompletedCount { get; }

        public int DuplicateRejectedCount { get; }

        public int MissingDependencyCount { get; }

        public int CleanupCount { get; }

        public int LastTickIndex { get; }

        public int LastEntityId { get; }

        public int LastDedupeKey { get; }
    }

    internal readonly struct BoxMotionCleanupDiagnostics
    {
        public BoxMotionCleanupDiagnostics(
            int cleanupRequestedCount,
            int cleanupSucceededCount,
            int visualRootPositionResetCount,
            int visualRootRotationResetCount,
            int flipDriverResetCount,
            int staleTrackClearedCount,
            int staleCompletedKeyClearedCount,
            BoxMotionTelemetryCleanupReason lastCleanupReason)
        {
            CleanupRequestedCount = Math.Max(0, cleanupRequestedCount);
            CleanupSucceededCount = Math.Max(0, cleanupSucceededCount);
            VisualRootPositionResetCount = Math.Max(0, visualRootPositionResetCount);
            VisualRootRotationResetCount = Math.Max(0, visualRootRotationResetCount);
            FlipDriverResetCount = Math.Max(0, flipDriverResetCount);
            StaleTrackClearedCount = Math.Max(0, staleTrackClearedCount);
            StaleCompletedKeyClearedCount = Math.Max(0, staleCompletedKeyClearedCount);
            LastCleanupReason = lastCleanupReason;
        }

        public int CleanupRequestedCount { get; }

        public int CleanupSucceededCount { get; }

        public int VisualRootPositionResetCount { get; }

        public int VisualRootRotationResetCount { get; }

        public int FlipDriverResetCount { get; }

        public int StaleTrackClearedCount { get; }

        public int StaleCompletedKeyClearedCount { get; }

        public BoxMotionTelemetryCleanupReason LastCleanupReason { get; }
    }

    internal readonly struct BoxFlipInteractionResetResult
    {
        public BoxFlipInteractionResetResult(
            int requestCount,
            int succeededCount,
            int visualRootPositionResetCount,
            int visualRootRotationResetCount)
        {
            RequestCount = Math.Max(0, requestCount);
            SucceededCount = Math.Max(0, succeededCount);
            VisualRootPositionResetCount = Math.Max(0, visualRootPositionResetCount);
            VisualRootRotationResetCount = Math.Max(0, visualRootRotationResetCount);
        }

        public int RequestCount { get; }

        public int SucceededCount { get; }

        public int VisualRootPositionResetCount { get; }

        public int VisualRootRotationResetCount { get; }

        public BoxFlipInteractionResetResult Add(BoxFlipInteractionResetResult other)
        {
            return new BoxFlipInteractionResetResult(
                RequestCount + other.RequestCount,
                SucceededCount + other.SucceededCount,
                VisualRootPositionResetCount + other.VisualRootPositionResetCount,
                VisualRootRotationResetCount + other.VisualRootRotationResetCount);
        }
    }

    internal sealed class BoxMotionProductionTelemetryState
    {
        private readonly SemanticCounter[] _semanticCounters =
        {
            new(PresentationMotionFactKind.BoxSlide, PresentationMotionCueKey.BoxSlide),
            new(PresentationMotionFactKind.BoxFlip, PresentationMotionCueKey.BoxFlip),
            new(PresentationMotionFactKind.BoxFlipImpact, PresentationMotionCueKey.BoxFlipImpact),
        };

        private int _playbackTrackCompletedCount;
        private int _playbackTrackIgnoredCount;
        private int _cleanupRequestedCount;
        private int _cleanupSucceededCount;
        private int _visualRootPositionResetCount;
        private int _visualRootRotationResetCount;
        private int _flipDriverResetCount;
        private int _staleTrackClearedCount;
        private int _staleCompletedKeyClearedCount;
        private BoxMotionTelemetryCleanupReason _lastCleanupReason;

        public int PlaybackTrackCompletedCount => _playbackTrackCompletedCount;

        public int PlaybackTrackCanceledCount => 0;

        public int PlaybackTrackIgnoredCount => _playbackTrackIgnoredCount;

        public BoxMotionCleanupDiagnostics CleanupDiagnostics =>
            new(
                _cleanupRequestedCount,
                _cleanupSucceededCount,
                _visualRootPositionResetCount,
                _visualRootRotationResetCount,
                _flipDriverResetCount,
                _staleTrackClearedCount,
                _staleCompletedKeyClearedCount,
                _lastCleanupReason);

        public IReadOnlyList<BoxMotionSemanticDiagnostics> SemanticDiagnostics
        {
            get
            {
                var result = new BoxMotionSemanticDiagnostics[_semanticCounters.Length];
                for (var i = 0; i < _semanticCounters.Length; i++)
                {
                    result[i] = _semanticCounters[i].ToDiagnostics();
                }

                return result;
            }
        }

        public void RecordTrackCompleted(PresentationMotionFactKind semantic, int tickIndex, int entityId, int dedupeKey)
        {
            _playbackTrackCompletedCount++;
            GetCounter(semantic)?.RecordCompleted(tickIndex, entityId, dedupeKey);
        }

        public void RecordTrackIgnored(PresentationMotionFactKind semantic, int tickIndex, int entityId, int dedupeKey)
        {
            _playbackTrackIgnoredCount++;
            GetCounter(semantic)?.RecordMissing(tickIndex, entityId, dedupeKey);
        }

        public void RecordCleanup(
            BoxMotionTelemetryCleanupReason reason,
            BoxFlipInteractionResetResult resetResult,
            int staleTrackClearedCount,
            int staleCompletedKeyClearedCount)
        {
            _cleanupRequestedCount++;
            _cleanupSucceededCount++;
            _lastCleanupReason = reason;
            _flipDriverResetCount += resetResult.SucceededCount;
            _visualRootPositionResetCount += resetResult.VisualRootPositionResetCount;
            _visualRootRotationResetCount += resetResult.VisualRootRotationResetCount;
            _staleTrackClearedCount += Math.Max(0, staleTrackClearedCount);
            _staleCompletedKeyClearedCount += Math.Max(0, staleCompletedKeyClearedCount);
        }

        public void RecordPoseReset(BoxFlipInteractionResetResult resetResult)
        {
            _flipDriverResetCount += resetResult.SucceededCount;
            _visualRootPositionResetCount += resetResult.VisualRootPositionResetCount;
            _visualRootRotationResetCount += resetResult.VisualRootRotationResetCount;
        }

        private SemanticCounter GetCounter(PresentationMotionFactKind semantic)
        {
            for (var i = 0; i < _semanticCounters.Length; i++)
            {
                if (_semanticCounters[i].Semantic == semantic)
                {
                    return _semanticCounters[i];
                }
            }

            return null;
        }

        private sealed class SemanticCounter
        {
            private int _completedCount;
            private int _missingDependencyCount;
            private int _cleanupCount;
            private int _lastTickIndex;
            private int _lastEntityId;
            private int _lastDedupeKey;

            public SemanticCounter(PresentationMotionFactKind semantic, PresentationMotionCueKey cueKey)
            {
                Semantic = semantic;
                CueKey = cueKey;
            }

            public PresentationMotionFactKind Semantic { get; }

            private PresentationMotionCueKey CueKey { get; }

            public void RecordCompleted(int tickIndex, int entityId, int dedupeKey)
            {
                _completedCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public void RecordMissing(int tickIndex, int entityId, int dedupeKey)
            {
                _missingDependencyCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public void RecordCleanup(int tickIndex, int entityId, int dedupeKey)
            {
                _cleanupCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public BoxMotionSemanticDiagnostics ToDiagnostics()
            {
                return new BoxMotionSemanticDiagnostics(
                    Semantic,
                    CueKey,
                    plannedCount: 0,
                    requestedCount: 0,
                    startedCount: 0,
                    _completedCount,
                    duplicateRejectedCount: 0,
                    _missingDependencyCount,
                    _cleanupCount,
                    _lastTickIndex,
                    _lastEntityId,
                    _lastDedupeKey);
            }

            private void RecordLast(int tickIndex, int entityId, int dedupeKey)
            {
                _lastTickIndex = Math.Max(0, tickIndex);
                _lastEntityId = Math.Max(0, entityId);
                _lastDedupeKey = dedupeKey;
            }
        }
    }

    internal readonly struct BoxMotionPlaybackKey : IEquatable<BoxMotionPlaybackKey>
    {
        public BoxMotionPlaybackKey(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int entityId,
            PresentationMotionCueKey cueKey,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            int sourceActionPlanId,
            int sourceSequenceId)
        {
            TickIndex = Math.Max(0, tickIndex);
            SemanticSource = semanticSource;
            EntityId = Math.Max(0, entityId);
            CueKey = cueKey;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            SourceActionPlanId = Math.Max(0, sourceActionPlanId);
            SourceSequenceId = Math.Max(0, sourceSequenceId);
        }

        public int TickIndex { get; }

        public PresentationSemanticSource SemanticSource { get; }

        public int EntityId { get; }

        public PresentationMotionCueKey CueKey { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public int SourceActionPlanId { get; }

        public int SourceSequenceId { get; }

        public bool Equals(BoxMotionPlaybackKey other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   EntityId == other.EntityId &&
                   CueKey == other.CueKey &&
                   SourceCell.Equals(other.SourceCell) &&
                   DestinationCell.Equals(other.DestinationCell) &&
                   SourceActionPlanId == other.SourceActionPlanId &&
                   SourceSequenceId == other.SourceSequenceId;
        }

        public override bool Equals(object obj)
        {
            return obj is BoxMotionPlaybackKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)SemanticSource;
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ (int)CueKey;
                hash = (hash * 397) ^ SourceCell.GetHashCode();
                hash = (hash * 397) ^ DestinationCell.GetHashCode();
                hash = (hash * 397) ^ SourceActionPlanId;
                hash = (hash * 397) ^ SourceSequenceId;
                return hash;
            }
        }
    }

    internal readonly struct BoxMotionOwnershipDiagnostics
    {
        public BoxMotionOwnershipDiagnostics(
            int executorAttemptCount,
            int executedByExecutorCount,
            int duplicateAttemptCount,
            BoxMotionPresentationExecutionOwner lastExecutionOwner)
        {
            ExecutorAttemptCount = Math.Max(0, executorAttemptCount);
            ExecutedByExecutorCount = Math.Max(0, executedByExecutorCount);
            DuplicateAttemptCount = Math.Max(0, duplicateAttemptCount);
            LastExecutionOwner = lastExecutionOwner;
        }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int DuplicateAttemptCount { get; }

        public BoxMotionPresentationExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class BoxMotionExecutionGuard
    {
        private readonly HashSet<BoxMotionPlaybackKey> _claimedKeys = new();
        private int _executorAttemptCount;
        private int _executedByExecutorCount;
        private int _duplicateAttemptCount;
        private BoxMotionPresentationExecutionOwner _lastExecutionOwner;

        public BoxMotionOwnershipDiagnostics Diagnostics =>
            new(
                _executorAttemptCount,
                _executedByExecutorCount,
                _duplicateAttemptCount,
                _lastExecutionOwner);

        public void ResetSession()
        {
            _claimedKeys.Clear();
            _executorAttemptCount = 0;
            _executedByExecutorCount = 0;
            _duplicateAttemptCount = 0;
            _lastExecutionOwner = BoxMotionPresentationExecutionOwner.None;
        }

        public bool TryBeginExecution(in BoxMotionPlaybackKey key)
        {
            _executorAttemptCount++;
            if (_claimedKeys.Contains(key))
            {
                _duplicateAttemptCount++;
                return false;
            }

            _claimedKeys.Add(key);
            _lastExecutionOwner = BoxMotionPresentationExecutionOwner.CurrentExecutor;
            _executedByExecutorCount++;

            return true;
        }
    }

    internal readonly struct GameplayMotionPlaybackRequest
    {
        public GameplayMotionPlaybackRequest(
            BoxMotionPlaybackKey ownershipKey,
            PresentationMotionCueKey cueKey,
            PresentationMotionPayload motionPayload,
            PresentationTarget target,
            PresentationAnchor anchor)
        {
            OwnershipKey = ownershipKey;
            CueKey = cueKey;
            MotionPayload = motionPayload;
            Target = target;
            Anchor = anchor;
        }

        public BoxMotionPlaybackKey OwnershipKey { get; }

        public PresentationMotionCueKey CueKey { get; }

        public PresentationMotionPayload MotionPayload { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public int TickIndex => OwnershipKey.TickIndex;

        public int EntityId => MotionPayload.EntityId;
    }

    internal readonly struct GameplayMotionPlaybackResult
    {
        public GameplayMotionPlaybackResult(GameplayMotionPlaybackResultKind kind)
        {
            Kind = kind;
        }

        public GameplayMotionPlaybackResultKind Kind { get; }
    }

    internal readonly struct GameplayMotionExecutorDiagnostics
    {
        public GameplayMotionExecutorDiagnostics(
            int observedTrackCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int driverMissingCount,
            int duplicateRejectedCount,
            int playbackRequestedCount,
            int trackStartedCount,
            int missingPortCount,
            int lastTickIndex = 0,
            PresentationMotionCueKey lastCueKey = PresentationMotionCueKey.None,
            int lastDedupeKey = 0,
            int lastTargetEntityId = 0,
            PresentationMotionFactKind lastMotionFactKind = PresentationMotionFactKind.None,
            BoxMotionTelemetryFailureReason lastFailureReason = BoxMotionTelemetryFailureReason.None,
            IReadOnlyList<BoxMotionSemanticDiagnostics> semanticDiagnostics = null,
            int unsupportedSemanticCount = 0)
        {
            ObservedTrackCount = Math.Max(0, observedTrackCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            DuplicateRejectedCount = Math.Max(0, duplicateRejectedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            TrackStartedCount = Math.Max(0, trackStartedCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            IsCurrentProductionOwner = true;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastTargetEntityId = Math.Max(0, lastTargetEntityId);
            LastMotionFactKind = lastMotionFactKind;
            LastFailureReason = lastFailureReason;
            SemanticDiagnostics = semanticDiagnostics ?? Array.Empty<BoxMotionSemanticDiagnostics>();
            UnsupportedSemanticCount = Math.Max(0, unsupportedSemanticCount);
        }

        public int ObservedTrackCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DriverMissingCount { get; }

        public int DuplicateRejectedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int TrackStartedCount { get; }

        public int MissingPortCount { get; }

        public bool IsCurrentProductionOwner { get; }

        public int LastTickIndex { get; }

        public PresentationMotionCueKey LastCueKey { get; }

        public int LastDedupeKey { get; }

        public int LastTargetEntityId { get; }

        public PresentationMotionFactKind LastMotionFactKind { get; }

        public BoxMotionTelemetryFailureReason LastFailureReason { get; }

        public IReadOnlyList<BoxMotionSemanticDiagnostics> SemanticDiagnostics { get; }

        public int UnsupportedSemanticCount { get; }
    }

    internal interface IGameplayMotionPlaybackPort
    {
        bool TryPlayBoxMotion(
            in GameplayMotionPlaybackRequest request,
            out GameplayMotionPlaybackResult result);

        void UpdatePresentation(float deltaTime);

        void ResetSession();

        void HardCleanup();
    }

    internal readonly struct GameplayMotionTrackPlannerPlaybackPortDiagnostics
    {
        public GameplayMotionTrackPlannerPlaybackPortDiagnostics(
            int beginTickContextCount,
            int tryPlayCallCount,
            int startedCount,
            int requestedCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int driverMissingCount,
            int duplicateActiveCount,
            int resetSessionCallCount,
            int hardCleanupCallCount,
            bool hasTickContext,
            int completedCount = 0)
        {
            BeginTickContextCount = Math.Max(0, beginTickContextCount);
            TryPlayCallCount = Math.Max(0, tryPlayCallCount);
            StartedCount = Math.Max(0, startedCount);
            RequestedCount = Math.Max(0, requestedCount);
            CompletedCount = Math.Max(0, completedCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            DuplicateActiveCount = Math.Max(0, duplicateActiveCount);
            ResetSessionCallCount = Math.Max(0, resetSessionCallCount);
            HardCleanupCallCount = Math.Max(0, hardCleanupCallCount);
            HasTickContext = hasTickContext;
        }

        public int BeginTickContextCount { get; }

        public int TryPlayCallCount { get; }

        public int StartedCount { get; }

        public int RequestedCount { get; }

        public int CompletedCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DriverMissingCount { get; }

        public int DuplicateActiveCount { get; }

        public int ResetSessionCallCount { get; }

        public int HardCleanupCallCount { get; }

        public bool HasTickContext { get; }
    }

    internal delegate GameplayPresentationPipeline BoxMotionExecutionPipelineFactory(
        IGameplayMotionPlaybackPort playbackPort,
        BoxMotionExecutionGuard executionGuard);

    internal sealed class GameplayMotionPresentationExecutor : IPresentationMotionExecutor
    {
        private readonly IGameplayMotionPlaybackPort _playbackPort;
        private readonly BoxMotionExecutionGuard _executionGuard;
        private bool _hasRoutedRequest;

        public GameplayMotionPresentationExecutor(
            IGameplayMotionPlaybackPort playbackPort = null,
            BoxMotionExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _executionGuard = executionGuard;
            Diagnostics = CreateEmptyDiagnostics();
        }

        public GameplayMotionExecutorDiagnostics Diagnostics { get; private set; }

        public void Prepare(PresentationPlaybackPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
        }

        public void Play(PresentationPlaybackPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var observedTrackCount = 0;
            var targetMissingCount = 0;
            var anchorMissingCount = 0;
            var bindingMissingCount = 0;
            var driverMissingCount = 0;
            var duplicateRejectedCount = 0;
            var playbackRequestedCount = 0;
            var trackStartedCount = 0;
            var missingPortCount = 0;
            var unsupportedSemanticCount = 0;
            var semanticCounters = CreateSemanticCounters();
            var lastTickIndex = 0;
            var lastCueKey = PresentationMotionCueKey.None;
            var lastDedupeKey = 0;
            var lastTargetEntityId = 0;
            var lastMotionFactKind = PresentationMotionFactKind.None;
            var lastFailureReason = BoxMotionTelemetryFailureReason.None;

            for (var i = 0; i < plan.Tracks.Count; i++)
            {
                var track = plan.Tracks[i];
                if (!IsBoxMotionTrack(track))
                {
                    continue;
                }

                observedTrackCount++;
                var cueKey = ResolveCueKey(track);
                var semantic = ToMotionFactKind(cueKey);
                var dedupeKey = ResolveDedupeKey(track);
                var targetEntityId = track.Cue.Target.EntityId;
                RecordSemanticPlanned(
                    semanticCounters,
                    semantic,
                    track.Cue.Source.TickIndex,
                    targetEntityId,
                    dedupeKey);
                RecordLastContext(
                    track.Cue.Source.TickIndex,
                    cueKey,
                    dedupeKey,
                    targetEntityId,
                    semantic,
                    BoxMotionTelemetryFailureReason.None,
                    ref lastTickIndex,
                    ref lastCueKey,
                    ref lastDedupeKey,
                    ref lastTargetEntityId,
                    ref lastMotionFactKind,
                    ref lastFailureReason);
                if (!TryCreateRequest(track, out var request, out var missingKind))
                {
                    RecordMissing(
                        missingKind,
                        ref targetMissingCount,
                        ref anchorMissingCount,
                        ref bindingMissingCount,
                        ref driverMissingCount);
                    RecordSemanticMissing(
                        semanticCounters,
                        semantic,
                        track.Cue.Source.TickIndex,
                        targetEntityId,
                        dedupeKey);
                    RecordLastContext(
                        track.Cue.Source.TickIndex,
                        cueKey,
                        dedupeKey,
                        targetEntityId,
                        semantic,
                        ToFailureReason(missingKind),
                        ref lastTickIndex,
                        ref lastCueKey,
                        ref lastDedupeKey,
                        ref lastTargetEntityId,
                        ref lastMotionFactKind,
                        ref lastFailureReason);
                    continue;
                }

                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateRejectedCount++;
                        RecordSemanticDuplicate(
                            semanticCounters,
                            semantic,
                            request.TickIndex,
                            request.EntityId,
                            dedupeKey);
                        RecordLastContext(
                            request.TickIndex,
                            request.CueKey,
                            dedupeKey,
                            request.EntityId,
                            semantic,
                            BoxMotionTelemetryFailureReason.DuplicateRejected,
                            ref lastTickIndex,
                            ref lastCueKey,
                            ref lastDedupeKey,
                            ref lastTargetEntityId,
                            ref lastMotionFactKind,
                            ref lastFailureReason);
                    }
                    continue;
                }

                if (_playbackPort == null)
                {
                    missingPortCount++;
                    RecordSemanticMissing(
                        semanticCounters,
                        semantic,
                        request.TickIndex,
                        request.EntityId,
                        dedupeKey);
                    RecordLastContext(
                        request.TickIndex,
                        request.CueKey,
                        dedupeKey,
                        request.EntityId,
                        semantic,
                        BoxMotionTelemetryFailureReason.PortMissing,
                        ref lastTickIndex,
                        ref lastCueKey,
                        ref lastDedupeKey,
                        ref lastTargetEntityId,
                        ref lastMotionFactKind,
                        ref lastFailureReason);
                    continue;
                }

                playbackRequestedCount++;
                RecordSemanticRequested(
                    semanticCounters,
                    semantic,
                    request.TickIndex,
                    request.EntityId,
                    dedupeKey);
                _hasRoutedRequest = true;
                _playbackPort.TryPlayBoxMotion(request, out var result);
                switch (result.Kind)
                {
                    case GameplayMotionPlaybackResultKind.Started:
                    case GameplayMotionPlaybackResultKind.Requested:
                        trackStartedCount++;
                        RecordSemanticStarted(
                            semanticCounters,
                            semantic,
                            request.TickIndex,
                            request.EntityId,
                            dedupeKey);
                        RecordLastContext(
                            request.TickIndex,
                            request.CueKey,
                            dedupeKey,
                            request.EntityId,
                            semantic,
                            BoxMotionTelemetryFailureReason.None,
                            ref lastTickIndex,
                            ref lastCueKey,
                            ref lastDedupeKey,
                            ref lastTargetEntityId,
                            ref lastMotionFactKind,
                            ref lastFailureReason);
                        break;
                    case GameplayMotionPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        RecordSemanticMissing(semanticCounters, semantic, request.TickIndex, request.EntityId, dedupeKey);
                        RecordLastContext(request.TickIndex, request.CueKey, dedupeKey, request.EntityId, semantic, BoxMotionTelemetryFailureReason.TargetMissing, ref lastTickIndex, ref lastCueKey, ref lastDedupeKey, ref lastTargetEntityId, ref lastMotionFactKind, ref lastFailureReason);
                        break;
                    case GameplayMotionPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        RecordSemanticMissing(semanticCounters, semantic, request.TickIndex, request.EntityId, dedupeKey);
                        RecordLastContext(request.TickIndex, request.CueKey, dedupeKey, request.EntityId, semantic, BoxMotionTelemetryFailureReason.AnchorMissing, ref lastTickIndex, ref lastCueKey, ref lastDedupeKey, ref lastTargetEntityId, ref lastMotionFactKind, ref lastFailureReason);
                        break;
                    case GameplayMotionPlaybackResultKind.BindingMissing:
                        bindingMissingCount++;
                        RecordSemanticMissing(semanticCounters, semantic, request.TickIndex, request.EntityId, dedupeKey);
                        RecordLastContext(request.TickIndex, request.CueKey, dedupeKey, request.EntityId, semantic, BoxMotionTelemetryFailureReason.BindingMissing, ref lastTickIndex, ref lastCueKey, ref lastDedupeKey, ref lastTargetEntityId, ref lastMotionFactKind, ref lastFailureReason);
                        break;
                    case GameplayMotionPlaybackResultKind.DriverMissing:
                        driverMissingCount++;
                        RecordSemanticMissing(semanticCounters, semantic, request.TickIndex, request.EntityId, dedupeKey);
                        RecordLastContext(request.TickIndex, request.CueKey, dedupeKey, request.EntityId, semantic, BoxMotionTelemetryFailureReason.DriverMissing, ref lastTickIndex, ref lastCueKey, ref lastDedupeKey, ref lastTargetEntityId, ref lastMotionFactKind, ref lastFailureReason);
                        break;
                    case GameplayMotionPlaybackResultKind.DuplicateActive:
                        duplicateRejectedCount++;
                        RecordSemanticMissing(semanticCounters, semantic, request.TickIndex, request.EntityId, dedupeKey);
                        RecordLastContext(request.TickIndex, request.CueKey, dedupeKey, request.EntityId, semantic, BoxMotionTelemetryFailureReason.DuplicateActive, ref lastTickIndex, ref lastCueKey, ref lastDedupeKey, ref lastTargetEntityId, ref lastMotionFactKind, ref lastFailureReason);
                        break;
                    default:
                        unsupportedSemanticCount++;
                        RecordSemanticMissing(semanticCounters, semantic, request.TickIndex, request.EntityId, dedupeKey);
                        RecordLastContext(request.TickIndex, request.CueKey, dedupeKey, request.EntityId, semantic, BoxMotionTelemetryFailureReason.UnsupportedSemantic, ref lastTickIndex, ref lastCueKey, ref lastDedupeKey, ref lastTargetEntityId, ref lastMotionFactKind, ref lastFailureReason);
                        break;
                }
            }

            Diagnostics = new GameplayMotionExecutorDiagnostics(
                observedTrackCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                driverMissingCount,
                duplicateRejectedCount,
                playbackRequestedCount,
                trackStartedCount,
                missingPortCount,
                lastTickIndex,
                lastCueKey,
                lastDedupeKey,
                lastTargetEntityId,
                lastMotionFactKind,
                lastFailureReason,
                BuildSemanticDiagnostics(semanticCounters),
                unsupportedSemanticCount);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_hasRoutedRequest)
            {
                _playbackPort?.UpdatePresentation(deltaTime);
            }
        }

        public void ResetSession()
        {
            _hasRoutedRequest = false;
            Diagnostics = CreateEmptyDiagnostics();
            _playbackPort?.ResetSession();
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            Diagnostics = CreateEmptyDiagnostics();
            _playbackPort?.HardCleanup();
        }

        private static GameplayMotionExecutorDiagnostics CreateEmptyDiagnostics()
        {
            return new GameplayMotionExecutorDiagnostics(
                observedTrackCount: 0,
                targetMissingCount: 0,
                anchorMissingCount: 0,
                bindingMissingCount: 0,
                driverMissingCount: 0,
                duplicateRejectedCount: 0,
                playbackRequestedCount: 0,
                trackStartedCount: 0,
                missingPortCount: 0);
        }

        private bool TryClaimExecution(in BoxMotionPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(key);
            }

            return true;
        }

        private static bool IsBoxMotionTrack(in PresentationPlaybackTrack track)
        {
            return track.Cue.Domain == PresentationDomain.Motion &&
                   track.Cue.Key.TryGetMotionCueKey(out var cueKey) &&
                   (cueKey == PresentationMotionCueKey.BoxSlide ||
                    cueKey == PresentationMotionCueKey.BoxFlip ||
                    cueKey == PresentationMotionCueKey.BoxFlipImpact);
        }

        private static bool TryCreateRequest(
            in PresentationPlaybackTrack track,
            out GameplayMotionPlaybackRequest request,
            out GameplayMotionPlaybackResultKind missingKind)
        {
            request = default;
            missingKind = GameplayMotionPlaybackResultKind.None;
            var cue = track.Cue;
            if (!cue.Key.TryGetMotionCueKey(out var cueKey))
            {
                missingKind = GameplayMotionPlaybackResultKind.BindingMissing;
                return false;
            }

            if (cue.Target.Kind != PresentationTargetKind.Entity ||
                cue.Target.EntityId <= 0 ||
                !cue.MotionPayload.IsValid)
            {
                missingKind = GameplayMotionPlaybackResultKind.TargetMissing;
                return false;
            }

            if (cue.Anchor.Kind != PresentationAnchorKind.EntityVisualRoot &&
                cue.Anchor.Kind != PresentationAnchorKind.EntityCenter)
            {
                missingKind = GameplayMotionPlaybackResultKind.AnchorMissing;
                return false;
            }

            var key = new BoxMotionPlaybackKey(
                cue.Source.TickIndex,
                cue.Source.SemanticSource,
                cue.Target.EntityId,
                cueKey,
                cue.MotionPayload.SourceCell,
                cue.MotionPayload.DestinationCell,
                cue.MotionPayload.SourceActionPlanId,
                cue.MotionPayload.SourceSequenceId);
            request = new GameplayMotionPlaybackRequest(
                key,
                cueKey,
                cue.MotionPayload,
                cue.Target,
                cue.Anchor);
            return true;
        }

        private static void RecordMissing(
            GameplayMotionPlaybackResultKind missingKind,
            ref int targetMissingCount,
            ref int anchorMissingCount,
            ref int bindingMissingCount,
            ref int driverMissingCount)
        {
            switch (missingKind)
            {
                case GameplayMotionPlaybackResultKind.TargetMissing:
                    targetMissingCount++;
                    break;
                case GameplayMotionPlaybackResultKind.AnchorMissing:
                    anchorMissingCount++;
                    break;
                case GameplayMotionPlaybackResultKind.DriverMissing:
                    driverMissingCount++;
                    break;
                default:
                    bindingMissingCount++;
                    break;
            }
        }

        private static PresentationMotionCueKey ResolveCueKey(in PresentationPlaybackTrack track)
        {
            return track.Cue.Key.TryGetMotionCueKey(out var cueKey)
                ? cueKey
                : PresentationMotionCueKey.None;
        }

        private static int ResolveDedupeKey(in PresentationPlaybackTrack track)
        {
            return track.Policy.DedupeKey != 0
                ? track.Policy.DedupeKey
                : track.Cue.PolicyHint.DedupeKey;
        }

        private static PresentationMotionFactKind ToMotionFactKind(PresentationMotionCueKey cueKey)
        {
            switch (cueKey)
            {
                case PresentationMotionCueKey.BoxSlide:
                    return PresentationMotionFactKind.BoxSlide;
                case PresentationMotionCueKey.BoxFlip:
                    return PresentationMotionFactKind.BoxFlip;
                case PresentationMotionCueKey.BoxFlipImpact:
                    return PresentationMotionFactKind.BoxFlipImpact;
                default:
                    return PresentationMotionFactKind.None;
            }
        }

        private static BoxMotionTelemetryFailureReason ToFailureReason(GameplayMotionPlaybackResultKind resultKind)
        {
            switch (resultKind)
            {
                case GameplayMotionPlaybackResultKind.TargetMissing:
                    return BoxMotionTelemetryFailureReason.TargetMissing;
                case GameplayMotionPlaybackResultKind.AnchorMissing:
                    return BoxMotionTelemetryFailureReason.AnchorMissing;
                case GameplayMotionPlaybackResultKind.DriverMissing:
                    return BoxMotionTelemetryFailureReason.DriverMissing;
                case GameplayMotionPlaybackResultKind.DuplicateActive:
                    return BoxMotionTelemetryFailureReason.DuplicateActive;
                default:
                    return BoxMotionTelemetryFailureReason.BindingMissing;
            }
        }

        private static SemanticPlaybackCounter[] CreateSemanticCounters()
        {
            return new[]
            {
                new SemanticPlaybackCounter(PresentationMotionFactKind.BoxSlide, PresentationMotionCueKey.BoxSlide),
                new SemanticPlaybackCounter(PresentationMotionFactKind.BoxFlip, PresentationMotionCueKey.BoxFlip),
                new SemanticPlaybackCounter(PresentationMotionFactKind.BoxFlipImpact, PresentationMotionCueKey.BoxFlipImpact),
            };
        }

        private static IReadOnlyList<BoxMotionSemanticDiagnostics> BuildSemanticDiagnostics(
            IReadOnlyList<SemanticPlaybackCounter> counters)
        {
            var result = new BoxMotionSemanticDiagnostics[counters.Count];
            for (var i = 0; i < counters.Count; i++)
            {
                result[i] = counters[i].ToDiagnostics();
            }

            return result;
        }

        private static void RecordSemanticPlanned(
            IReadOnlyList<SemanticPlaybackCounter> counters,
            PresentationMotionFactKind semantic,
            int tickIndex,
            int entityId,
            int dedupeKey)
        {
            GetCounter(counters, semantic)?.RecordPlanned(tickIndex, entityId, dedupeKey);
        }

        private static void RecordSemanticRequested(
            IReadOnlyList<SemanticPlaybackCounter> counters,
            PresentationMotionFactKind semantic,
            int tickIndex,
            int entityId,
            int dedupeKey)
        {
            GetCounter(counters, semantic)?.RecordRequested(tickIndex, entityId, dedupeKey);
        }

        private static void RecordSemanticStarted(
            IReadOnlyList<SemanticPlaybackCounter> counters,
            PresentationMotionFactKind semantic,
            int tickIndex,
            int entityId,
            int dedupeKey)
        {
            GetCounter(counters, semantic)?.RecordStarted(tickIndex, entityId, dedupeKey);
        }

        private static void RecordSemanticDuplicate(
            IReadOnlyList<SemanticPlaybackCounter> counters,
            PresentationMotionFactKind semantic,
            int tickIndex,
            int entityId,
            int dedupeKey)
        {
            GetCounter(counters, semantic)?.RecordDuplicate(tickIndex, entityId, dedupeKey);
        }

        private static void RecordSemanticMissing(
            IReadOnlyList<SemanticPlaybackCounter> counters,
            PresentationMotionFactKind semantic,
            int tickIndex,
            int entityId,
            int dedupeKey)
        {
            GetCounter(counters, semantic)?.RecordMissing(tickIndex, entityId, dedupeKey);
        }

        private static SemanticPlaybackCounter GetCounter(
            IReadOnlyList<SemanticPlaybackCounter> counters,
            PresentationMotionFactKind semantic)
        {
            for (var i = 0; i < counters.Count; i++)
            {
                if (counters[i].Semantic == semantic)
                {
                    return counters[i];
                }
            }

            return null;
        }

        private static void RecordLastContext(
            int tickIndex,
            PresentationMotionCueKey cueKey,
            int dedupeKey,
            int targetEntityId,
            PresentationMotionFactKind semantic,
            BoxMotionTelemetryFailureReason failureReason,
            ref int lastTickIndex,
            ref PresentationMotionCueKey lastCueKey,
            ref int lastDedupeKey,
            ref int lastTargetEntityId,
            ref PresentationMotionFactKind lastMotionFactKind,
            ref BoxMotionTelemetryFailureReason lastFailureReason)
        {
            lastTickIndex = Math.Max(0, tickIndex);
            lastCueKey = cueKey;
            lastDedupeKey = dedupeKey;
            lastTargetEntityId = Math.Max(0, targetEntityId);
            lastMotionFactKind = semantic;
            lastFailureReason = failureReason;
        }

        private sealed class SemanticPlaybackCounter
        {
            private int _plannedCount;
            private int _requestedCount;
            private int _startedCount;
            private int _duplicateRejectedCount;
            private int _missingDependencyCount;
            private int _lastTickIndex;
            private int _lastEntityId;
            private int _lastDedupeKey;

            public SemanticPlaybackCounter(PresentationMotionFactKind semantic, PresentationMotionCueKey cueKey)
            {
                Semantic = semantic;
                CueKey = cueKey;
            }

            public PresentationMotionFactKind Semantic { get; }

            private PresentationMotionCueKey CueKey { get; }

            public void RecordPlanned(int tickIndex, int entityId, int dedupeKey)
            {
                _plannedCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public void RecordRequested(int tickIndex, int entityId, int dedupeKey)
            {
                _requestedCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public void RecordStarted(int tickIndex, int entityId, int dedupeKey)
            {
                _startedCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public void RecordDuplicate(int tickIndex, int entityId, int dedupeKey)
            {
                _duplicateRejectedCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public void RecordMissing(int tickIndex, int entityId, int dedupeKey)
            {
                _missingDependencyCount++;
                RecordLast(tickIndex, entityId, dedupeKey);
            }

            public BoxMotionSemanticDiagnostics ToDiagnostics()
            {
                return new BoxMotionSemanticDiagnostics(
                    Semantic,
                    CueKey,
                    _plannedCount,
                    _requestedCount,
                    _startedCount,
                    completedCount: 0,
                    _duplicateRejectedCount,
                    _missingDependencyCount,
                    cleanupCount: 0,
                    _lastTickIndex,
                    _lastEntityId,
                    _lastDedupeKey);
            }

            private void RecordLast(int tickIndex, int entityId, int dedupeKey)
            {
                _lastTickIndex = Math.Max(0, tickIndex);
                _lastEntityId = Math.Max(0, entityId);
                _lastDedupeKey = dedupeKey;
            }
        }

    }

    internal sealed class GameplayMotionTrackPlannerPlaybackPort : IGameplayMotionPlaybackPort
    {
        private readonly GameplayTrackPlanner _trackPlanner;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private TickResult _result;
        private IReadOnlyDictionary<int, GameplayEntityPose> _previousCommittedLocalTargetPoses;
        private CubeTopologyState _previousCommittedTopology;
        private GameplayCubeProjector _projector;
        private GameplayTimingProfile _timingProfile;
        private int _beginTickContextCount;
        private int _tryPlayCallCount;
        private int _startedCount;
        private int _requestedCount;
        private int _targetMissingCount;
        private int _anchorMissingCount;
        private int _bindingMissingCount;
        private int _driverMissingCount;
        private int _duplicateActiveCount;
        private int _resetSessionCallCount;
        private int _hardCleanupCallCount;

        public GameplayMotionTrackPlannerPlaybackPort(
            GameplayTrackPlanner trackPlanner,
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState = null)
        {
            _trackPlanner = trackPlanner ?? throw new ArgumentNullException(nameof(trackPlanner));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState;
        }

        public void BeginTickContext(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            _result = result;
            _previousCommittedLocalTargetPoses = previousCommittedLocalTargetPoses;
            _previousCommittedTopology = previousCommittedTopology;
            _projector = projector;
            _timingProfile = timingProfile;
            _beginTickContextCount++;
        }

        internal GameplayMotionTrackPlannerPlaybackPortDiagnostics Diagnostics =>
            new(
                _beginTickContextCount,
                _tryPlayCallCount,
                _startedCount,
                _requestedCount,
                _targetMissingCount,
                _anchorMissingCount,
                _bindingMissingCount,
                _driverMissingCount,
                _duplicateActiveCount,
                _resetSessionCallCount,
                _hardCleanupCallCount,
                _result != null ||
                _previousCommittedLocalTargetPoses != null ||
                _projector != null ||
                _timingProfile != null,
                _trackState?.BoxMotionTelemetry.PlaybackTrackCompletedCount ?? 0);

        public bool TryPlayBoxMotion(
            in GameplayMotionPlaybackRequest request,
            out GameplayMotionPlaybackResult result)
        {
            _tryPlayCallCount++;
            if (_result == null ||
                _previousCommittedLocalTargetPoses == null ||
                _projector == null ||
                _timingProfile == null)
            {
                result = new GameplayMotionPlaybackResult(GameplayMotionPlaybackResultKind.BindingMissing);
                RecordResult(result.Kind);
                return false;
            }

            if (request.Target.Kind != PresentationTargetKind.Entity ||
                request.Target.EntityId <= 0)
            {
                result = new GameplayMotionPlaybackResult(GameplayMotionPlaybackResultKind.TargetMissing);
                RecordResult(result.Kind);
                return false;
            }

            if (request.Anchor.Kind != PresentationAnchorKind.EntityVisualRoot &&
                request.Anchor.Kind != PresentationAnchorKind.EntityCenter)
            {
                result = new GameplayMotionPlaybackResult(GameplayMotionPlaybackResultKind.AnchorMissing);
                RecordResult(result.Kind);
                return false;
            }

            if (!_stateStore.ViewsByEntityId.TryGetValue(request.Target.EntityId, out var view) ||
                view == null)
            {
                result = new GameplayMotionPlaybackResult(GameplayMotionPlaybackResultKind.BindingMissing);
                RecordResult(result.Kind);
                return false;
            }

            if (request.CueKey == PresentationMotionCueKey.BoxFlipImpact &&
                !view.TryGetComponent<BoxFlipInteractionDriver>(out _))
            {
                result = new GameplayMotionPlaybackResult(GameplayMotionPlaybackResultKind.DriverMissing);
                RecordResult(result.Kind);
                return false;
            }

            var started = _trackPlanner.TryRequestBoxMotionPlayback(
                request,
                _result,
                _previousCommittedLocalTargetPoses,
                _previousCommittedTopology,
                _projector,
                _timingProfile,
                out var resultKind);
            result = new GameplayMotionPlaybackResult(resultKind);
            RecordResult(result.Kind);
            return started;
        }

        public void UpdatePresentation(float deltaTime)
        {
        }

        public void ResetSession()
        {
            _resetSessionCallCount++;
            ClearTickContext();
        }

        public void HardCleanup()
        {
            _hardCleanupCallCount++;
            ClearTickContext();
        }

        private void RecordResult(GameplayMotionPlaybackResultKind kind)
        {
            switch (kind)
            {
                case GameplayMotionPlaybackResultKind.Started:
                    _startedCount++;
                    break;
                case GameplayMotionPlaybackResultKind.Requested:
                    _requestedCount++;
                    break;
                case GameplayMotionPlaybackResultKind.TargetMissing:
                    _targetMissingCount++;
                    break;
                case GameplayMotionPlaybackResultKind.AnchorMissing:
                    _anchorMissingCount++;
                    break;
                case GameplayMotionPlaybackResultKind.BindingMissing:
                    _bindingMissingCount++;
                    break;
                case GameplayMotionPlaybackResultKind.DriverMissing:
                    _driverMissingCount++;
                    break;
                case GameplayMotionPlaybackResultKind.DuplicateActive:
                    _duplicateActiveCount++;
                    break;
            }
        }

        private void ClearTickContext()
        {
            _result = null;
            _previousCommittedLocalTargetPoses = null;
            _previousCommittedTopology = default;
            _projector = null;
            _timingProfile = null;
        }
    }
}
