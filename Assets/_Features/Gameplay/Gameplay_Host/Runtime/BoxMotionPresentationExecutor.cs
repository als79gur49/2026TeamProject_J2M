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
    public enum BoxMotionPresentationExecutionMode
    {
        LegacyTrackPlanner = 0,
        OrchestrationMotionExecutor = 1,
    }

    internal enum BoxMotionPresentationExecutionOwner
    {
        None = 0,
        LegacyTrackPlanner = 1,
        OrchestrationMotionExecutor = 2,
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
        LegacyOwnerActive = 7,
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
            BoxMotionPresentationExecutionMode mode,
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            BoxMotionPresentationExecutionOwner lastExecutionOwner)
        {
            Mode = mode;
            LegacyAttemptCount = Math.Max(0, legacyAttemptCount);
            ExecutorAttemptCount = Math.Max(0, executorAttemptCount);
            ExecutedByLegacyCount = Math.Max(0, executedByLegacyCount);
            ExecutedByExecutorCount = Math.Max(0, executedByExecutorCount);
            SkippedLegacyBecauseExecutorOwnerCount = Math.Max(0, skippedLegacyBecauseExecutorOwnerCount);
            SkippedExecutorBecauseLegacyOwnerCount = Math.Max(0, skippedExecutorBecauseLegacyOwnerCount);
            DuplicateAttemptCount = Math.Max(0, duplicateAttemptCount);
            LastExecutionOwner = lastExecutionOwner;
        }

        public BoxMotionPresentationExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public BoxMotionPresentationExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class BoxMotionExecutionGuard
    {
        private readonly HashSet<BoxMotionPlaybackKey> _claimedKeys = new();
        private BoxMotionPresentationExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private BoxMotionPresentationExecutionOwner _lastExecutionOwner;

        public BoxMotionExecutionGuard(
            BoxMotionPresentationExecutionMode mode = BoxMotionPresentationExecutionMode.LegacyTrackPlanner)
        {
            _mode = NormalizeMode(mode);
        }

        public BoxMotionOwnershipDiagnostics Diagnostics =>
            new(
                _mode,
                _legacyAttemptCount,
                _executorAttemptCount,
                _executedByLegacyCount,
                _executedByExecutorCount,
                _skippedLegacyBecauseExecutorOwnerCount,
                _skippedExecutorBecauseLegacyOwnerCount,
                _duplicateAttemptCount,
                _lastExecutionOwner);

        public void Configure(BoxMotionPresentationExecutionMode mode)
        {
            _mode = NormalizeMode(mode);
        }

        public void ResetSession()
        {
            _claimedKeys.Clear();
            _legacyAttemptCount = 0;
            _executorAttemptCount = 0;
            _executedByLegacyCount = 0;
            _executedByExecutorCount = 0;
            _skippedLegacyBecauseExecutorOwnerCount = 0;
            _skippedExecutorBecauseLegacyOwnerCount = 0;
            _duplicateAttemptCount = 0;
            _lastExecutionOwner = BoxMotionPresentationExecutionOwner.None;
        }

        public void RecordSkippedByPolicy(BoxMotionPresentationExecutionOwner skippedOwner)
        {
            if (skippedOwner == BoxMotionPresentationExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOwner), "Box motion owner must be explicit.");
            }

            RecordAttempt(skippedOwner);
            RecordPolicySkip(skippedOwner);
        }

        public bool TryBeginExecution(
            BoxMotionPresentationExecutionOwner owner,
            in BoxMotionPlaybackKey key)
        {
            if (owner == BoxMotionPresentationExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "Box motion owner must be explicit.");
            }

            RecordAttempt(owner);
            if (_claimedKeys.Contains(key))
            {
                _duplicateAttemptCount++;
                RecordPolicySkip(owner);
                return false;
            }

            if (!IsOwnerAllowed(owner))
            {
                RecordPolicySkip(owner);
                return false;
            }

            _claimedKeys.Add(key);
            _lastExecutionOwner = owner;
            if (owner == BoxMotionPresentationExecutionOwner.LegacyTrackPlanner)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static BoxMotionPresentationExecutionMode NormalizeMode(BoxMotionPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(BoxMotionPresentationExecutionMode), mode)
                ? mode
                : BoxMotionPresentationExecutionMode.LegacyTrackPlanner;
        }

        private bool IsOwnerAllowed(BoxMotionPresentationExecutionOwner owner)
        {
            return (_mode == BoxMotionPresentationExecutionMode.LegacyTrackPlanner &&
                    owner == BoxMotionPresentationExecutionOwner.LegacyTrackPlanner) ||
                   (_mode == BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor &&
                    owner == BoxMotionPresentationExecutionOwner.OrchestrationMotionExecutor);
        }

        private void RecordAttempt(BoxMotionPresentationExecutionOwner owner)
        {
            if (owner == BoxMotionPresentationExecutionOwner.LegacyTrackPlanner)
            {
                _legacyAttemptCount++;
            }
            else if (owner == BoxMotionPresentationExecutionOwner.OrchestrationMotionExecutor)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(BoxMotionPresentationExecutionOwner owner)
        {
            if (owner == BoxMotionPresentationExecutionOwner.LegacyTrackPlanner)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (owner == BoxMotionPresentationExecutionOwner.OrchestrationMotionExecutor)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
            }
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
            int legacyOwnerNoOpCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int driverMissingCount,
            int duplicateSuppressedCount,
            int playbackRequestedCount,
            int trackStartedCount,
            int missingPortCount)
        {
            ObservedTrackCount = Math.Max(0, observedTrackCount);
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            TrackStartedCount = Math.Max(0, trackStartedCount);
            MissingPortCount = Math.Max(0, missingPortCount);
        }

        public int ObservedTrackCount { get; }

        public int LegacyOwnerNoOpCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DriverMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int TrackStartedCount { get; }

        public int MissingPortCount { get; }
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
            int legacyOwnerActiveCount,
            int resetSessionCallCount,
            int hardCleanupCallCount,
            bool hasTickContext)
        {
            BeginTickContextCount = Math.Max(0, beginTickContextCount);
            TryPlayCallCount = Math.Max(0, tryPlayCallCount);
            StartedCount = Math.Max(0, startedCount);
            RequestedCount = Math.Max(0, requestedCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            LegacyOwnerActiveCount = Math.Max(0, legacyOwnerActiveCount);
            ResetSessionCallCount = Math.Max(0, resetSessionCallCount);
            HardCleanupCallCount = Math.Max(0, hardCleanupCallCount);
            HasTickContext = hasTickContext;
        }

        public int BeginTickContextCount { get; }

        public int TryPlayCallCount { get; }

        public int StartedCount { get; }

        public int RequestedCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DriverMissingCount { get; }

        public int LegacyOwnerActiveCount { get; }

        public int ResetSessionCallCount { get; }

        public int HardCleanupCallCount { get; }

        public bool HasTickContext { get; }
    }

    internal delegate GameplayPresentationPipeline BoxMotionExecutionPipelineFactory(
        BoxMotionPresentationExecutionMode mode,
        IGameplayMotionPlaybackPort playbackPort,
        BoxMotionExecutionGuard executionGuard);

    internal sealed class GameplayMotionPresentationExecutor : IPresentationMotionExecutor
    {
        private readonly IGameplayMotionPlaybackPort _playbackPort;
        private readonly BoxMotionPresentationExecutionMode _mode;
        private readonly BoxMotionExecutionGuard _executionGuard;
        private bool _hasRoutedRequest;

        public GameplayMotionPresentationExecutor(
            IGameplayMotionPlaybackPort playbackPort = null,
            BoxMotionPresentationExecutionMode mode = BoxMotionPresentationExecutionMode.LegacyTrackPlanner,
            BoxMotionExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
            _executionGuard = executionGuard;
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
            var legacyOwnerNoOpCount = 0;
            var targetMissingCount = 0;
            var anchorMissingCount = 0;
            var bindingMissingCount = 0;
            var driverMissingCount = 0;
            var duplicateSuppressedCount = 0;
            var playbackRequestedCount = 0;
            var trackStartedCount = 0;
            var missingPortCount = 0;

            for (var i = 0; i < plan.Tracks.Count; i++)
            {
                var track = plan.Tracks[i];
                if (!IsBoxMotionTrack(track))
                {
                    continue;
                }

                observedTrackCount++;
                if (_mode != BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor)
                {
                    legacyOwnerNoOpCount++;
                    continue;
                }

                if (!TryCreateRequest(track, out var request, out var missingKind))
                {
                    RecordMissing(
                        missingKind,
                        ref targetMissingCount,
                        ref anchorMissingCount,
                        ref bindingMissingCount,
                        ref driverMissingCount);
                    continue;
                }

                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateSuppressedCount++;
                    }
                    else
                    {
                        legacyOwnerNoOpCount++;
                    }

                    continue;
                }

                if (_playbackPort == null)
                {
                    missingPortCount++;
                    continue;
                }

                playbackRequestedCount++;
                _hasRoutedRequest = true;
                _playbackPort.TryPlayBoxMotion(request, out var result);
                switch (result.Kind)
                {
                    case GameplayMotionPlaybackResultKind.Started:
                    case GameplayMotionPlaybackResultKind.Requested:
                        trackStartedCount++;
                        break;
                    case GameplayMotionPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        break;
                    case GameplayMotionPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        break;
                    case GameplayMotionPlaybackResultKind.BindingMissing:
                        bindingMissingCount++;
                        break;
                    case GameplayMotionPlaybackResultKind.DriverMissing:
                        driverMissingCount++;
                        break;
                    case GameplayMotionPlaybackResultKind.LegacyOwnerActive:
                        legacyOwnerNoOpCount++;
                        break;
                }
            }

            Diagnostics = new GameplayMotionExecutorDiagnostics(
                observedTrackCount,
                legacyOwnerNoOpCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                driverMissingCount,
                duplicateSuppressedCount,
                playbackRequestedCount,
                trackStartedCount,
                missingPortCount);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_mode == BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor &&
                _hasRoutedRequest)
            {
                _playbackPort?.UpdatePresentation(deltaTime);
            }
        }

        public void ResetSession()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            if (_mode == BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor)
            {
                _playbackPort?.ResetSession();
            }
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            if (_mode == BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor)
            {
                _playbackPort?.HardCleanup();
            }
        }

        private bool TryClaimExecution(in BoxMotionPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    BoxMotionPresentationExecutionOwner.OrchestrationMotionExecutor,
                    key);
            }

            return _mode == BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor;
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

        private static BoxMotionPresentationExecutionMode NormalizeMode(BoxMotionPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(BoxMotionPresentationExecutionMode), mode)
                ? mode
                : BoxMotionPresentationExecutionMode.LegacyTrackPlanner;
        }
    }

    internal sealed class GameplayMotionTrackPlannerPlaybackPort : IGameplayMotionPlaybackPort
    {
        private readonly GameplayTrackPlanner _trackPlanner;
        private readonly GameplayPresentationStateStore _stateStore;
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
        private int _legacyOwnerActiveCount;
        private int _resetSessionCallCount;
        private int _hardCleanupCallCount;

        public GameplayMotionTrackPlannerPlaybackPort(
            GameplayTrackPlanner trackPlanner,
            GameplayPresentationStateStore stateStore)
        {
            _trackPlanner = trackPlanner ?? throw new ArgumentNullException(nameof(trackPlanner));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
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
                _legacyOwnerActiveCount,
                _resetSessionCallCount,
                _hardCleanupCallCount,
                _result != null ||
                _previousCommittedLocalTargetPoses != null ||
                _projector != null ||
                _timingProfile != null);

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

            if ((request.CueKey == PresentationMotionCueKey.BoxFlip ||
                 request.CueKey == PresentationMotionCueKey.BoxFlipImpact) &&
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
                case GameplayMotionPlaybackResultKind.LegacyOwnerActive:
                    _legacyOwnerActiveCount++;
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
