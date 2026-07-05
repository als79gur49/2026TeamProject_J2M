using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    public enum PlayerActionAnimationExecutionMode
    {
        OrchestrationAnimationExecutor = 1,
    }

    internal static class PlayerActionAnimationExecutionDefaults
    {
        public const PlayerActionAnimationExecutionMode ProductionDefault =
            PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor;
    }

    internal enum PlayerActionAnimationExecutionOwner
    {
        None = 0,
        OrchestrationAnimationExecutor = 1,
    }

    internal enum GameplayAnimationPlaybackResultKind
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        DriverMissing = 4,
        AnimatorMissing = 5,
        Requested = 6,
        Applied = 7,
        IgnoredByPolicy = 8,
    }

    internal enum PlayerActionAnimationTelemetryFailureReason
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        DriverMissing = 4,
        AnimatorMissing = 5,
        PortMissing = 6,
        DuplicateSuppressed = 7,
        IgnoredByPolicy = 8,
    }

    internal enum PlayerActionAnimationTelemetryCleanupReason
    {
        None = 0,
        ResetSession = 1,
        HardCleanupPresentationExtensions = 2,
    }

    internal readonly struct PlayerActionAnimationSemanticDiagnostics
    {
        public PlayerActionAnimationSemanticDiagnostics(
            PresentationAnimationCueKey cueKey,
            int plannedCount,
            int observedCount,
            int requestedCount,
            int appliedCount,
            int ignoredCount)
        {
            CueKey = cueKey;
            PlannedCount = Math.Max(0, plannedCount);
            ObservedCount = Math.Max(0, observedCount);
            RequestedCount = Math.Max(0, requestedCount);
            AppliedCount = Math.Max(0, appliedCount);
            IgnoredCount = Math.Max(0, ignoredCount);
        }

        public PresentationAnimationCueKey CueKey { get; }

        public int PlannedCount { get; }

        public int ObservedCount { get; }

        public int RequestedCount { get; }

        public int AppliedCount { get; }

        public int IgnoredCount { get; }
    }

    internal readonly struct PlayerActionAnimationProductionTelemetrySnapshot
    {
        public PlayerActionAnimationProductionTelemetrySnapshot(
            PlayerActionAnimationExecutionMode currentMode,
            bool isProductionDefaultOwner,
            PlayerActionAnimationExecutionMode productionDefaultMode,
            int lastTickIndex,
            PresentationAnimationCueKey lastCueKey,
            int lastDedupeKey,
            int lastPlayerEntityId,
            PresentationAnimationActionKind lastActionKind,
            PresentationAnimationPhaseKind lastPhaseKind,
            PresentationAnimationOutcomeKind lastOutcomeKind,
            PlayerActionAnimationTelemetryFailureReason lastFailureReason,
            PlayerActionAnimationTelemetryCleanupReason lastCleanupReason,
            int plannedCueCount,
            int executorOwnerAttemptCount,
            int executorOwnerExecutedCount,
            int duplicateOwnerAttemptCount,
            int duplicateSuppressedCount,
            int observedCueCount,
            int playbackCommandRequestedCount,
            int playbackCommandAppliedCount,
            int playbackCommandIgnoredByPolicyCount,
            int executeCueMappedToRecoveryCommandCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int driverMissingCount,
            int animatorMissingCount,
            int portMissingCount,
            IReadOnlyList<PlayerActionAnimationSemanticDiagnostics> semanticDiagnostics = null)
        {
            CurrentMode = currentMode;
            IsProductionDefaultOwner = isProductionDefaultOwner;
            ProductionDefaultMode = productionDefaultMode;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastPlayerEntityId = Math.Max(0, lastPlayerEntityId);
            LastActionKind = lastActionKind;
            LastPhaseKind = lastPhaseKind;
            LastOutcomeKind = lastOutcomeKind;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
            PlannedCueCount = Math.Max(0, plannedCueCount);
            ExecutorOwnerAttemptCount = Math.Max(0, executorOwnerAttemptCount);
            ExecutorOwnerExecutedCount = Math.Max(0, executorOwnerExecutedCount);
            DuplicateOwnerAttemptCount = Math.Max(0, duplicateOwnerAttemptCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            ObservedCueCount = Math.Max(0, observedCueCount);
            PlaybackCommandRequestedCount = Math.Max(0, playbackCommandRequestedCount);
            PlaybackCommandAppliedCount = Math.Max(0, playbackCommandAppliedCount);
            PlaybackCommandIgnoredByPolicyCount = Math.Max(0, playbackCommandIgnoredByPolicyCount);
            ExecuteCueMappedToRecoveryCommandCount = Math.Max(0, executeCueMappedToRecoveryCommandCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            AnimatorMissingCount = Math.Max(0, animatorMissingCount);
            PortMissingCount = Math.Max(0, portMissingCount);
            SemanticDiagnostics = semanticDiagnostics ?? Array.Empty<PlayerActionAnimationSemanticDiagnostics>();
        }

        public PlayerActionAnimationExecutionMode CurrentMode { get; }
        public bool IsProductionDefaultOwner { get; }
        public PlayerActionAnimationExecutionMode ProductionDefaultMode { get; }
        public int LastTickIndex { get; }
        public PresentationAnimationCueKey LastCueKey { get; }
        public int LastDedupeKey { get; }
        public int LastPlayerEntityId { get; }
        public PresentationAnimationActionKind LastActionKind { get; }
        public PresentationAnimationPhaseKind LastPhaseKind { get; }
        public PresentationAnimationOutcomeKind LastOutcomeKind { get; }
        public PlayerActionAnimationTelemetryFailureReason LastFailureReason { get; }
        public PlayerActionAnimationTelemetryCleanupReason LastCleanupReason { get; }
        public int PlannedCueCount { get; }
        public int ExecutorOwnerAttemptCount { get; }
        public int ExecutorOwnerExecutedCount { get; }
        public int DuplicateOwnerAttemptCount { get; }
        public int DuplicateSuppressedCount { get; }
        public int ObservedCueCount { get; }
        public int PlaybackCommandRequestedCount { get; }
        public int PlaybackCommandAppliedCount { get; }
        public int PlaybackCommandIgnoredByPolicyCount { get; }
        public int ExecuteCueMappedToRecoveryCommandCount { get; }
        public int TargetMissingCount { get; }
        public int AnchorMissingCount { get; }
        public int BindingMissingCount { get; }
        public int DriverMissingCount { get; }
        public int AnimatorMissingCount { get; }
        public int PortMissingCount { get; }

        public IReadOnlyList<PlayerActionAnimationSemanticDiagnostics> SemanticDiagnostics { get; }
    }

    internal static class PlayerActionAnimationProductionTelemetryBuilder
    {
        public static GameplayAnimationExecutorDiagnostics ResolveExecutorDiagnostics(GameplayPresentationPipeline pipeline)
        {
            if (pipeline == null)
            {
                return default;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayAnimationPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        public static PlayerActionAnimationProductionTelemetrySnapshot Build(
            PlayerActionAnimationExecutionMode mode,
            PlayerActionAnimationOwnershipDiagnostics ownership,
            GameplayPresentationPipeline pipeline)
        {
            var executor = ResolveExecutorDiagnostics(pipeline);

            return new PlayerActionAnimationProductionTelemetrySnapshot(
                mode,
                mode == PlayerActionAnimationExecutionDefaults.ProductionDefault,
                PlayerActionAnimationExecutionDefaults.ProductionDefault,
                executor.LastTickIndex,
                executor.LastCueKey,
                executor.LastDedupeKey,
                executor.LastPlayerEntityId,
                executor.LastActionKind,
                executor.LastPhaseKind,
                executor.LastOutcomeKind,
                executor.LastFailureReason,
                executor.LastCleanupReason,
                ownership.PlannedCueCount,
                ownership.ExecutorAttemptCount,
                ownership.ExecutedByExecutorCount,
                ownership.DuplicateAttemptCount,
                Math.Max(executor.DuplicateSuppressedCount, ownership.DuplicateAttemptCount),
                executor.ObservedCueCount,
                executor.CommandRequestedCount,
                executor.CommandAppliedCount,
                executor.CommandIgnoredByPolicyCount,
                executor.ExecuteCueMappedToRecoveryCommandCount,
                executor.TargetMissingCount,
                executor.AnchorMissingCount,
                executor.BindingMissingCount,
                executor.DriverMissingCount,
                executor.AnimatorMissingCount,
                executor.MissingPortCount,
                executor.SemanticDiagnostics);
        }
    }

    internal readonly struct PlayerActionAnimationPlaybackKey : IEquatable<PlayerActionAnimationPlaybackKey>
    {
        public PlayerActionAnimationPlaybackKey(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int playerEntityId,
            PresentationAnimationCueKey cueKey,
            PresentationAnimationActionKind actionKind,
            PresentationAnimationPhaseKind phaseKind,
            int sourceSequenceId,
            int sourceActionPlanId)
        {
            TickIndex = Math.Max(0, tickIndex);
            SemanticSource = semanticSource;
            PlayerEntityId = Math.Max(0, playerEntityId);
            CueKey = cueKey;
            ActionKind = actionKind;
            PhaseKind = phaseKind;
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            SourceActionPlanId = Math.Max(0, sourceActionPlanId);
        }

        public int TickIndex { get; }

        public PresentationSemanticSource SemanticSource { get; }

        public int PlayerEntityId { get; }

        public PresentationAnimationCueKey CueKey { get; }

        public PresentationAnimationActionKind ActionKind { get; }

        public PresentationAnimationPhaseKind PhaseKind { get; }

        public int SourceSequenceId { get; }

        public int SourceActionPlanId { get; }

        public bool Equals(PlayerActionAnimationPlaybackKey other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   PlayerEntityId == other.PlayerEntityId &&
                   CueKey == other.CueKey &&
                   ActionKind == other.ActionKind &&
                   PhaseKind == other.PhaseKind &&
                   SourceSequenceId == other.SourceSequenceId &&
                   SourceActionPlanId == other.SourceActionPlanId;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerActionAnimationPlaybackKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)SemanticSource;
                hash = (hash * 397) ^ PlayerEntityId;
                hash = (hash * 397) ^ (int)CueKey;
                hash = (hash * 397) ^ (int)ActionKind;
                hash = (hash * 397) ^ (int)PhaseKind;
                hash = (hash * 397) ^ SourceSequenceId;
                hash = (hash * 397) ^ SourceActionPlanId;
                return hash;
            }
        }
    }

    internal readonly struct PlayerActionAnimationOwnershipDiagnostics
    {
        public PlayerActionAnimationOwnershipDiagnostics(
            PlayerActionAnimationExecutionMode mode,
            int plannedCueCount,
            int executorAttemptCount,
            int executedByExecutorCount,
            int duplicateAttemptCount,
            PlayerActionAnimationExecutionOwner lastExecutionOwner)
        {
            Mode = mode;
            PlannedCueCount = Math.Max(0, plannedCueCount);
            ExecutorAttemptCount = Math.Max(0, executorAttemptCount);
            ExecutedByExecutorCount = Math.Max(0, executedByExecutorCount);
            DuplicateAttemptCount = Math.Max(0, duplicateAttemptCount);
            LastExecutionOwner = lastExecutionOwner;
        }

        public PlayerActionAnimationExecutionMode Mode { get; }

        public int PlannedCueCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int DuplicateAttemptCount { get; }

        public PlayerActionAnimationExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class PlayerActionAnimationExecutionGuard
    {
        private readonly HashSet<PlayerActionAnimationPlaybackKey> _claimedKeys = new();
        private PlayerActionAnimationExecutionMode _mode;
        private int _plannedCueCount;
        private int _executorAttemptCount;
        private int _executedByExecutorCount;
        private int _duplicateAttemptCount;
        private PlayerActionAnimationExecutionOwner _lastExecutionOwner;

        public PlayerActionAnimationExecutionGuard(
            PlayerActionAnimationExecutionMode mode = PlayerActionAnimationExecutionDefaults.ProductionDefault)
        {
            _mode = NormalizeMode(mode);
        }

        public PlayerActionAnimationOwnershipDiagnostics Diagnostics =>
            new(
                _mode,
                _plannedCueCount,
                _executorAttemptCount,
                _executedByExecutorCount,
                _duplicateAttemptCount,
                _lastExecutionOwner);

        public void Configure(PlayerActionAnimationExecutionMode mode)
        {
            _mode = NormalizeMode(mode);
        }

        public void ResetSession()
        {
            _claimedKeys.Clear();
            _plannedCueCount = 0;
            _executorAttemptCount = 0;
            _executedByExecutorCount = 0;
            _duplicateAttemptCount = 0;
            _lastExecutionOwner = PlayerActionAnimationExecutionOwner.None;
        }

        public void RecordPlanned(int cueCount)
        {
            _plannedCueCount += Math.Max(0, cueCount);
        }

        public bool TryBeginExecution(
            PlayerActionAnimationExecutionOwner owner,
            in PlayerActionAnimationPlaybackKey key)
        {
            if (owner == PlayerActionAnimationExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "Player action animation owner must be explicit.");
            }

            RecordAttempt(owner);
            if (_claimedKeys.Contains(key))
            {
                _duplicateAttemptCount++;
                return false;
            }

            if (!IsOwnerAllowed(owner))
            {
                return false;
            }

            _claimedKeys.Add(key);
            _lastExecutionOwner = owner;
            _executedByExecutorCount++;

            return true;
        }

        private static PlayerActionAnimationExecutionMode NormalizeMode(PlayerActionAnimationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), mode)
                ? mode
                : PlayerActionAnimationExecutionDefaults.ProductionDefault;
        }

        private bool IsOwnerAllowed(PlayerActionAnimationExecutionOwner owner)
        {
            return _mode == PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor &&
                   owner == PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor;
        }

        private void RecordAttempt(PlayerActionAnimationExecutionOwner owner)
        {
            if (owner == PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor)
            {
                _executorAttemptCount++;
            }
        }
    }

    internal readonly struct GameplayAnimationPlaybackRequest
    {
        public GameplayAnimationPlaybackRequest(
            PlayerActionAnimationPlaybackKey ownershipKey,
            PresentationAnimationCueKey cueKey,
            PresentationAnimationPayload animationPayload,
            PresentationTarget target,
            PresentationAnchor anchor)
        {
            OwnershipKey = ownershipKey;
            CueKey = cueKey;
            AnimationPayload = animationPayload;
            Target = target;
            Anchor = anchor;
        }

        public PlayerActionAnimationPlaybackKey OwnershipKey { get; }

        public PresentationAnimationCueKey CueKey { get; }

        public PresentationAnimationPayload AnimationPayload { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public int TickIndex => OwnershipKey.TickIndex;

        public int PlayerEntityId => AnimationPayload.EntityId;
    }

    internal readonly struct GameplayAnimationPlaybackResult
    {
        public GameplayAnimationPlaybackResult(
            GameplayAnimationPlaybackResultKind kind,
            bool executeCueMappedToRecoveryCommand = false)
        {
            Kind = kind;
            ExecuteCueMappedToRecoveryCommand = executeCueMappedToRecoveryCommand;
        }

        public GameplayAnimationPlaybackResultKind Kind { get; }

        public bool ExecuteCueMappedToRecoveryCommand { get; }
    }

    internal readonly struct GameplayAnimationExecutorDiagnostics
    {
        public GameplayAnimationExecutorDiagnostics(
            int observedCueCount,
            int ownerPolicyIgnoredCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int driverMissingCount,
            int animatorMissingCount,
            int duplicateSuppressedCount,
            int commandRequestedCount,
            int commandAppliedCount,
            int commandIgnoredByPolicyCount,
            int missingPortCount,
            int executeCueMappedToRecoveryCommandCount,
            int lastTickIndex = 0,
            PresentationAnimationCueKey lastCueKey = PresentationAnimationCueKey.None,
            int lastDedupeKey = 0,
            int lastPlayerEntityId = 0,
            PresentationAnimationActionKind lastActionKind = PresentationAnimationActionKind.None,
            PresentationAnimationPhaseKind lastPhaseKind = PresentationAnimationPhaseKind.None,
            PresentationAnimationOutcomeKind lastOutcomeKind = PresentationAnimationOutcomeKind.None,
            PlayerActionAnimationTelemetryFailureReason lastFailureReason =
                PlayerActionAnimationTelemetryFailureReason.None,
            PlayerActionAnimationTelemetryCleanupReason lastCleanupReason =
                PlayerActionAnimationTelemetryCleanupReason.None,
            IReadOnlyList<PlayerActionAnimationSemanticDiagnostics> semanticDiagnostics = null)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            OwnerPolicyIgnoredCount = Math.Max(0, ownerPolicyIgnoredCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            AnimatorMissingCount = Math.Max(0, animatorMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            CommandRequestedCount = Math.Max(0, commandRequestedCount);
            CommandAppliedCount = Math.Max(0, commandAppliedCount);
            CommandIgnoredByPolicyCount = Math.Max(0, commandIgnoredByPolicyCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            ExecuteCueMappedToRecoveryCommandCount = Math.Max(0, executeCueMappedToRecoveryCommandCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastPlayerEntityId = Math.Max(0, lastPlayerEntityId);
            LastActionKind = lastActionKind;
            LastPhaseKind = lastPhaseKind;
            LastOutcomeKind = lastOutcomeKind;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
            SemanticDiagnostics = semanticDiagnostics ?? Array.Empty<PlayerActionAnimationSemanticDiagnostics>();
        }

        public int ObservedCueCount { get; }

        public int OwnerPolicyIgnoredCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DriverMissingCount { get; }

        public int AnimatorMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int CommandRequestedCount { get; }

        public int CommandAppliedCount { get; }

        public int CommandIgnoredByPolicyCount { get; }

        public int MissingPortCount { get; }

        public int ExecuteCueMappedToRecoveryCommandCount { get; }

        public int LastTickIndex { get; }

        public PresentationAnimationCueKey LastCueKey { get; }

        public int LastDedupeKey { get; }

        public int LastPlayerEntityId { get; }

        public PresentationAnimationActionKind LastActionKind { get; }

        public PresentationAnimationPhaseKind LastPhaseKind { get; }

        public PresentationAnimationOutcomeKind LastOutcomeKind { get; }

        public PlayerActionAnimationTelemetryFailureReason LastFailureReason { get; }

        public PlayerActionAnimationTelemetryCleanupReason LastCleanupReason { get; }

        public IReadOnlyList<PlayerActionAnimationSemanticDiagnostics> SemanticDiagnostics { get; }
    }

    internal sealed class PlayerActionAnimationSemanticTelemetryAccumulator
    {
        private static readonly PresentationAnimationCueKey[] OrderedCueKeys =
        {
            PresentationAnimationCueKey.PlayerPushWindup,
            PresentationAnimationCueKey.PlayerPushExecute,
            PresentationAnimationCueKey.PlayerPushRecovery,
            PresentationAnimationCueKey.PlayerPushBlocked,
            PresentationAnimationCueKey.PlayerPushImpactContact,
            PresentationAnimationCueKey.PlayerPushFailed,
            PresentationAnimationCueKey.PlayerFlipWindup,
            PresentationAnimationCueKey.PlayerFlipExecute,
            PresentationAnimationCueKey.PlayerFlipRecovery,
            PresentationAnimationCueKey.PlayerFlipBlocked,
            PresentationAnimationCueKey.PlayerFlipImpactContact,
            PresentationAnimationCueKey.PlayerFlipFailed,
        };

        private readonly Dictionary<PresentationAnimationCueKey, Counter> _counters = new();

        public PlayerActionAnimationSemanticTelemetryAccumulator(
            IReadOnlyList<PresentationAnimationCuePlanningCount> plannedCounts = null)
        {
            for (var i = 0; i < OrderedCueKeys.Length; i++)
            {
                _counters[OrderedCueKeys[i]] = new Counter();
            }

            if (plannedCounts == null)
            {
                return;
            }

            for (var i = 0; i < plannedCounts.Count; i++)
            {
                var planned = plannedCounts[i];
                if (_counters.TryGetValue(planned.CueKey, out var counter))
                {
                    counter.PlannedCount += planned.PlannedCount;
                }
            }
        }

        public void RecordObserved(PresentationAnimationCueKey cueKey)
        {
            if (_counters.TryGetValue(cueKey, out var counter))
            {
                counter.ObservedCount++;
            }
        }

        public void RecordRequested(PresentationAnimationCueKey cueKey)
        {
            if (_counters.TryGetValue(cueKey, out var counter))
            {
                counter.RequestedCount++;
            }
        }

        public void RecordApplied(PresentationAnimationCueKey cueKey)
        {
            if (_counters.TryGetValue(cueKey, out var counter))
            {
                counter.AppliedCount++;
            }
        }

        public void RecordIgnored(PresentationAnimationCueKey cueKey)
        {
            if (_counters.TryGetValue(cueKey, out var counter))
            {
                counter.IgnoredCount++;
            }
        }

        public IReadOnlyList<PlayerActionAnimationSemanticDiagnostics> ToDiagnostics()
        {
            var diagnostics = new PlayerActionAnimationSemanticDiagnostics[OrderedCueKeys.Length];
            for (var i = 0; i < OrderedCueKeys.Length; i++)
            {
                var cueKey = OrderedCueKeys[i];
                var counter = _counters[cueKey];
                diagnostics[i] = new PlayerActionAnimationSemanticDiagnostics(
                    cueKey,
                    counter.PlannedCount,
                    counter.ObservedCount,
                    counter.RequestedCount,
                    counter.AppliedCount,
                    counter.IgnoredCount);
            }

            return diagnostics;
        }

        private sealed class Counter
        {
            public int PlannedCount;
            public int ObservedCount;
            public int RequestedCount;
            public int AppliedCount;
            public int IgnoredCount;
        }
    }

    internal interface IGameplayAnimationPlaybackPort
    {
        bool TryPlayPlayerActionAnimation(
            in GameplayAnimationPlaybackRequest request,
            out GameplayAnimationPlaybackResult result);

        void ResetSession();

        void HardCleanup();
    }

    internal delegate GameplayPresentationPipeline PlayerActionAnimationExecutionPipelineFactory(
        PlayerActionAnimationExecutionMode mode,
        IGameplayAnimationPlaybackPort playbackPort,
        PlayerActionAnimationExecutionGuard executionGuard);

    internal sealed class GameplayAnimationPresentationExecutor : IPresentationAnimationExecutor
    {
        private readonly IGameplayAnimationPlaybackPort _playbackPort;
        private readonly PlayerActionAnimationExecutionMode _mode;
        private readonly PlayerActionAnimationExecutionGuard _executionGuard;

        public GameplayAnimationPresentationExecutor(
            IGameplayAnimationPlaybackPort playbackPort = null,
            PlayerActionAnimationExecutionMode mode = PlayerActionAnimationExecutionDefaults.ProductionDefault,
            PlayerActionAnimationExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
            _executionGuard = executionGuard;
        }

        public GameplayAnimationExecutorDiagnostics Diagnostics { get; private set; }

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

            var observedCueCount = 0;
            var ownerPolicyIgnoredCount = 0;
            var targetMissingCount = 0;
            var anchorMissingCount = 0;
            var bindingMissingCount = 0;
            var driverMissingCount = 0;
            var animatorMissingCount = 0;
            var duplicateSuppressedCount = 0;
            var commandRequestedCount = 0;
            var commandAppliedCount = 0;
            var commandIgnoredByPolicyCount = 0;
            var missingPortCount = 0;
            var executeCueMappedToRecoveryCommandCount = 0;
            var lastTickIndex = 0;
            var lastCueKey = PresentationAnimationCueKey.None;
            var lastDedupeKey = 0;
            var lastPlayerEntityId = 0;
            var lastActionKind = PresentationAnimationActionKind.None;
            var lastPhaseKind = PresentationAnimationPhaseKind.None;
            var lastOutcomeKind = PresentationAnimationOutcomeKind.None;
            var lastFailureReason = PlayerActionAnimationTelemetryFailureReason.None;
            var semanticTelemetry =
                new PlayerActionAnimationSemanticTelemetryAccumulator(
                    plan.Diagnostics.PlayerActionAnimationPlannedCounts);

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var playbackCue = plan.Cues[i];
                if (!IsPlayerActionAnimationCue(playbackCue))
                {
                    continue;
                }

                observedCueCount++;
                var currentCueKey = playbackCue.Cue.Key.TryGetAnimationCueKey(out var resolvedCueKey)
                    ? resolvedCueKey
                    : PresentationAnimationCueKey.None;
                semanticTelemetry.RecordObserved(currentCueKey);
                CaptureLastCue(
                    playbackCue,
                    ref lastTickIndex,
                    ref lastCueKey,
                    ref lastDedupeKey,
                    ref lastPlayerEntityId,
                    ref lastActionKind,
                    ref lastPhaseKind,
                    ref lastOutcomeKind);
                if (!TryCreateRequest(playbackCue, out var request, out var missingKind))
                {
                    RecordMissing(
                        missingKind,
                        ref targetMissingCount,
                        ref anchorMissingCount,
                        ref bindingMissingCount,
                        ref driverMissingCount,
                        ref animatorMissingCount);
                    lastFailureReason = ToTelemetryFailureReason(missingKind);
                    semanticTelemetry.RecordIgnored(currentCueKey);
                    continue;
                }

                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateSuppressedCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.DuplicateSuppressed;
                    }
                    else
                    {
                        commandIgnoredByPolicyCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.IgnoredByPolicy;
                    }

                    semanticTelemetry.RecordIgnored(currentCueKey);
                    continue;
                }

                if (_playbackPort == null)
                {
                    missingPortCount++;
                    lastFailureReason = PlayerActionAnimationTelemetryFailureReason.PortMissing;
                    semanticTelemetry.RecordIgnored(currentCueKey);
                    continue;
                }

                commandRequestedCount++;
                semanticTelemetry.RecordRequested(currentCueKey);
                _playbackPort.TryPlayPlayerActionAnimation(request, out var result);
                if (result.ExecuteCueMappedToRecoveryCommand)
                {
                    executeCueMappedToRecoveryCommandCount++;
                }

                switch (result.Kind)
                {
                    case GameplayAnimationPlaybackResultKind.Applied:
                        commandAppliedCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.None;
                        semanticTelemetry.RecordApplied(currentCueKey);
                        break;
                    case GameplayAnimationPlaybackResultKind.Requested:
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.None;
                        break;
                    case GameplayAnimationPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.TargetMissing;
                        semanticTelemetry.RecordIgnored(currentCueKey);
                        break;
                    case GameplayAnimationPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.AnchorMissing;
                        semanticTelemetry.RecordIgnored(currentCueKey);
                        break;
                    case GameplayAnimationPlaybackResultKind.BindingMissing:
                        bindingMissingCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.BindingMissing;
                        semanticTelemetry.RecordIgnored(currentCueKey);
                        break;
                    case GameplayAnimationPlaybackResultKind.DriverMissing:
                        driverMissingCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.DriverMissing;
                        semanticTelemetry.RecordIgnored(currentCueKey);
                        break;
                    case GameplayAnimationPlaybackResultKind.AnimatorMissing:
                        animatorMissingCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.AnimatorMissing;
                        semanticTelemetry.RecordIgnored(currentCueKey);
                        break;
                    case GameplayAnimationPlaybackResultKind.IgnoredByPolicy:
                        commandIgnoredByPolicyCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.IgnoredByPolicy;
                        semanticTelemetry.RecordIgnored(currentCueKey);
                        break;
                    case GameplayAnimationPlaybackResultKind.None:
                    default:
                        commandIgnoredByPolicyCount++;
                        lastFailureReason = PlayerActionAnimationTelemetryFailureReason.IgnoredByPolicy;
                        semanticTelemetry.RecordIgnored(currentCueKey);
                        break;
                }
            }

            Diagnostics = new GameplayAnimationExecutorDiagnostics(
                observedCueCount,
                ownerPolicyIgnoredCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                driverMissingCount,
                animatorMissingCount,
                duplicateSuppressedCount,
                commandRequestedCount,
                commandAppliedCount,
                commandIgnoredByPolicyCount,
                missingPortCount,
                executeCueMappedToRecoveryCommandCount,
                lastTickIndex,
                lastCueKey,
                lastDedupeKey,
                lastPlayerEntityId,
                lastActionKind,
                lastPhaseKind,
                lastOutcomeKind,
                lastFailureReason,
                semanticDiagnostics: semanticTelemetry.ToDiagnostics());
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }
        }

        public void ResetSession()
        {
            Diagnostics = new GameplayAnimationExecutorDiagnostics(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                lastCleanupReason: PlayerActionAnimationTelemetryCleanupReason.ResetSession);
            _playbackPort?.ResetSession();
        }

        public void HardCleanup()
        {
            Diagnostics = new GameplayAnimationExecutorDiagnostics(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                lastCleanupReason: PlayerActionAnimationTelemetryCleanupReason.HardCleanupPresentationExtensions);
            _playbackPort?.HardCleanup();
        }

        private bool TryClaimExecution(in PlayerActionAnimationPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor,
                    key);
            }

            return true;
        }

        private static bool IsPlayerActionAnimationCue(in PresentationPlaybackCue playbackCue)
        {
            return playbackCue.Cue.Domain == PresentationDomain.Animation &&
                   playbackCue.Cue.Key.TryGetAnimationCueKey(out _) &&
                   playbackCue.Cue.AnimationPayload.Kind == PresentationAnimationFactKind.PlayerAction;
        }

        private static bool TryCreateRequest(
            in PresentationPlaybackCue playbackCue,
            out GameplayAnimationPlaybackRequest request,
            out GameplayAnimationPlaybackResultKind missingKind)
        {
            request = default;
            missingKind = GameplayAnimationPlaybackResultKind.None;
            var cue = playbackCue.Cue;
            if (!cue.Key.TryGetAnimationCueKey(out var cueKey) ||
                !cue.AnimationPayload.IsValid)
            {
                missingKind = GameplayAnimationPlaybackResultKind.BindingMissing;
                return false;
            }

            if (cue.Target.Kind != PresentationTargetKind.Entity ||
                cue.Target.EntityId <= 0 ||
                cue.AnimationPayload.EntityId <= 0)
            {
                missingKind = GameplayAnimationPlaybackResultKind.TargetMissing;
                return false;
            }

            if (cue.Anchor.Kind != PresentationAnchorKind.EntityVisualRoot &&
                cue.Anchor.Kind != PresentationAnchorKind.EntityCenter)
            {
                missingKind = GameplayAnimationPlaybackResultKind.AnchorMissing;
                return false;
            }

            var key = new PlayerActionAnimationPlaybackKey(
                cue.Source.TickIndex,
                cue.Source.SemanticSource,
                cue.Target.EntityId,
                cueKey,
                cue.AnimationPayload.ActionKind,
                cue.AnimationPayload.PhaseKind,
                cue.AnimationPayload.SourceSequenceId,
                cue.AnimationPayload.SourceActionPlanId);
            request = new GameplayAnimationPlaybackRequest(
                key,
                cueKey,
                cue.AnimationPayload,
                cue.Target,
                cue.Anchor);
            return true;
        }

        private static void RecordMissing(
            GameplayAnimationPlaybackResultKind missingKind,
            ref int targetMissingCount,
            ref int anchorMissingCount,
            ref int bindingMissingCount,
            ref int driverMissingCount,
            ref int animatorMissingCount)
        {
            switch (missingKind)
            {
                case GameplayAnimationPlaybackResultKind.TargetMissing:
                    targetMissingCount++;
                    break;
                case GameplayAnimationPlaybackResultKind.AnchorMissing:
                    anchorMissingCount++;
                    break;
                case GameplayAnimationPlaybackResultKind.DriverMissing:
                    driverMissingCount++;
                    break;
                case GameplayAnimationPlaybackResultKind.AnimatorMissing:
                    animatorMissingCount++;
                    break;
                default:
                    bindingMissingCount++;
                    break;
            }
        }

        private static void CaptureLastCue(
            in PresentationPlaybackCue playbackCue,
            ref int lastTickIndex,
            ref PresentationAnimationCueKey lastCueKey,
            ref int lastDedupeKey,
            ref int lastPlayerEntityId,
            ref PresentationAnimationActionKind lastActionKind,
            ref PresentationAnimationPhaseKind lastPhaseKind,
            ref PresentationAnimationOutcomeKind lastOutcomeKind)
        {
            var cue = playbackCue.Cue;
            lastTickIndex = cue.Source.TickIndex;
            lastDedupeKey = playbackCue.Policy.DedupeKey;
            lastPlayerEntityId = Math.Max(cue.Target.EntityId, cue.AnimationPayload.EntityId);
            lastActionKind = cue.AnimationPayload.ActionKind;
            lastPhaseKind = cue.AnimationPayload.PhaseKind;
            lastOutcomeKind = cue.AnimationPayload.OutcomeKind;
            lastCueKey = cue.Key.TryGetAnimationCueKey(out var cueKey)
                ? cueKey
                : PresentationAnimationCueKey.None;
        }

        private static PlayerActionAnimationTelemetryFailureReason ToTelemetryFailureReason(
            GameplayAnimationPlaybackResultKind resultKind)
        {
            switch (resultKind)
            {
                case GameplayAnimationPlaybackResultKind.TargetMissing:
                    return PlayerActionAnimationTelemetryFailureReason.TargetMissing;
                case GameplayAnimationPlaybackResultKind.AnchorMissing:
                    return PlayerActionAnimationTelemetryFailureReason.AnchorMissing;
                case GameplayAnimationPlaybackResultKind.DriverMissing:
                    return PlayerActionAnimationTelemetryFailureReason.DriverMissing;
                case GameplayAnimationPlaybackResultKind.AnimatorMissing:
                    return PlayerActionAnimationTelemetryFailureReason.AnimatorMissing;
                default:
                    return PlayerActionAnimationTelemetryFailureReason.BindingMissing;
            }
        }

        private static PlayerActionAnimationExecutionMode NormalizeMode(PlayerActionAnimationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), mode)
                ? mode
                : PlayerActionAnimationExecutionDefaults.ProductionDefault;
        }
    }

    internal sealed class GameplayAnimationSyncPlaybackPort : IGameplayAnimationPlaybackPort
    {
        private readonly GameplayAnimationSyncCoordinator _animationSync;
        private readonly Func<int, PlayerActionKind, float> _resolvePlayerMotionDurationSeconds;
        private readonly GameplayPresentationStateStore _stateStore;

        public GameplayAnimationSyncPlaybackPort(
            GameplayAnimationSyncCoordinator animationSync,
            GameplayPresentationStateStore stateStore,
            Func<int, PlayerActionKind, float> resolvePlayerMotionDurationSeconds = null)
        {
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _resolvePlayerMotionDurationSeconds = resolvePlayerMotionDurationSeconds;
        }

        public bool TryPlayPlayerActionAnimation(
            in GameplayAnimationPlaybackRequest request,
            out GameplayAnimationPlaybackResult result)
        {
            return _animationSync.TryApplyPlayerActionAnimationPlayback(
                request,
                _stateStore.ViewsByEntityId,
                _resolvePlayerMotionDurationSeconds,
                out result);
        }

        public void ResetSession()
        {
        }

        public void HardCleanup()
        {
        }

    }
}
