using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    internal enum EnemyPresentationExecutionOwner
    {
        None = 0,
        CurrentExecutor = 1,
    }

    internal enum GameplayEnemyPresentationPlaybackResultKind
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        MapperMissing = 4,
        DriverMissing = 5,
        AnimatorMissing = 6,
        Requested = 7,
        Applied = 8,
        IgnoredByPolicy = 9,
    }

    internal enum GameplayEnemyPresentationCommandMappingKind
    {
        None = 0,
        Jump = 1,
        Charge = 2,
        Death = 3,
    }

    internal enum EnemyPresentationTelemetryFailureReason
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        MapperMissing = 4,
        DriverMissing = 5,
        AnimatorMissing = 6,
        PortMissing = 7,
        DuplicateRejected = 8,
        IgnoredByPolicy = 9,
    }

    internal enum EnemyPresentationTelemetryCleanupReason
    {
        None = 0,
        ResetSession = 1,
        HardCleanupPresentationExtensions = 2,
    }

    internal readonly struct EnemyPresentationProductionTelemetrySnapshot
    {
        public EnemyPresentationProductionTelemetrySnapshot(
            bool isProductionDefaultOwner,
            int lastTickIndex,
            PresentationAnimationCueKey lastCueKey,
            int lastDedupeKey,
            int lastEnemyEntityId,
            PresentationEnemyPresentationKind lastPresentationKind,
            PresentationEnemyPresentationPhase lastPresentationPhase,
            PresentationEnemyPresentationOutcome lastPresentationOutcome,
            EnemyPresentationTelemetryFailureReason lastFailureReason,
            EnemyPresentationTelemetryCleanupReason lastCleanupReason,
            int executorOwnerAttemptCount,
            int executorOwnerExecutedCount,
            int duplicateOwnerAttemptCount,
            int duplicateRejectedCount,
            int observedCueCount,
            int playbackCommandRequestedCount,
            int playbackCommandAppliedCount,
            int playbackCommandIgnoredByPolicyCount,
            int enemyJumpCueMappedToDriverCommandCount,
            int enemyChargeCueMappedToDriverCommandCount,
            int enemyDeathCueMappedToDriverCommandCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int mapperMissingCount,
            int driverMissingCount,
            int animatorMissingCount,
            int portMissingCount)
        {
            IsProductionDefaultOwner = isProductionDefaultOwner;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastEnemyEntityId = Math.Max(0, lastEnemyEntityId);
            LastPresentationKind = lastPresentationKind;
            LastPresentationPhase = lastPresentationPhase;
            LastPresentationOutcome = lastPresentationOutcome;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
            ExecutorOwnerAttemptCount = Math.Max(0, executorOwnerAttemptCount);
            ExecutorOwnerExecutedCount = Math.Max(0, executorOwnerExecutedCount);
            DuplicateOwnerAttemptCount = Math.Max(0, duplicateOwnerAttemptCount);
            DuplicateRejectedCount = Math.Max(0, duplicateRejectedCount);
            ObservedCueCount = Math.Max(0, observedCueCount);
            PlaybackCommandRequestedCount = Math.Max(0, playbackCommandRequestedCount);
            PlaybackCommandAppliedCount = Math.Max(0, playbackCommandAppliedCount);
            PlaybackCommandIgnoredByPolicyCount = Math.Max(0, playbackCommandIgnoredByPolicyCount);
            EnemyJumpCueMappedToDriverCommandCount = Math.Max(0, enemyJumpCueMappedToDriverCommandCount);
            EnemyChargeCueMappedToDriverCommandCount = Math.Max(0, enemyChargeCueMappedToDriverCommandCount);
            EnemyDeathCueMappedToDriverCommandCount = Math.Max(0, enemyDeathCueMappedToDriverCommandCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            MapperMissingCount = Math.Max(0, mapperMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            AnimatorMissingCount = Math.Max(0, animatorMissingCount);
            PortMissingCount = Math.Max(0, portMissingCount);
        }

        public bool IsProductionDefaultOwner { get; }
        public int LastTickIndex { get; }
        public PresentationAnimationCueKey LastCueKey { get; }
        public int LastDedupeKey { get; }
        public int LastEnemyEntityId { get; }
        public PresentationEnemyPresentationKind LastPresentationKind { get; }
        public PresentationEnemyPresentationPhase LastPresentationPhase { get; }
        public PresentationEnemyPresentationOutcome LastPresentationOutcome { get; }
        public EnemyPresentationTelemetryFailureReason LastFailureReason { get; }
        public EnemyPresentationTelemetryCleanupReason LastCleanupReason { get; }
        public int ExecutorOwnerAttemptCount { get; }
        public int ExecutorOwnerExecutedCount { get; }
        public int DuplicateOwnerAttemptCount { get; }
        public int DuplicateRejectedCount { get; }
        public int ObservedCueCount { get; }
        public int PlaybackCommandRequestedCount { get; }
        public int PlaybackCommandAppliedCount { get; }
        public int PlaybackCommandIgnoredByPolicyCount { get; }
        public int EnemyJumpCueMappedToDriverCommandCount { get; }
        public int EnemyChargeCueMappedToDriverCommandCount { get; }
        public int EnemyDeathCueMappedToDriverCommandCount { get; }
        public int TargetMissingCount { get; }
        public int AnchorMissingCount { get; }
        public int BindingMissingCount { get; }
        public int MapperMissingCount { get; }
        public int DriverMissingCount { get; }
        public int AnimatorMissingCount { get; }
        public int PortMissingCount { get; }
    }

    internal static class EnemyPresentationProductionTelemetryBuilder
    {
        public static GameplayEnemyPresentationExecutorDiagnostics ResolveExecutorDiagnostics(GameplayPresentationPipeline pipeline)
        {
            if (pipeline == null)
            {
                return default;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayEnemyPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        public static EnemyPresentationProductionTelemetrySnapshot Build(
            EnemyPresentationOwnershipDiagnostics ownership,
            GameplayPresentationPipeline pipeline)
        {
            var executor = ResolveExecutorDiagnostics(pipeline);
            var duplicateRejectedCount = Math.Max(
                executor.DuplicateRejectedCount,
                ownership.DuplicateAttemptCount);
            var lastFailureReason =
                duplicateRejectedCount > executor.DuplicateRejectedCount &&
                executor.LastFailureReason == EnemyPresentationTelemetryFailureReason.None
                    ? EnemyPresentationTelemetryFailureReason.DuplicateRejected
                    : executor.LastFailureReason;

            return new EnemyPresentationProductionTelemetrySnapshot(
                true,
                executor.LastTickIndex,
                executor.LastCueKey,
                executor.LastDedupeKey,
                executor.LastEnemyEntityId,
                executor.LastPresentationKind,
                executor.LastPresentationPhase,
                executor.LastPresentationOutcome,
                lastFailureReason,
                executor.LastCleanupReason,
                ownership.ExecutorAttemptCount,
                ownership.ExecutedByExecutorCount,
                ownership.DuplicateAttemptCount,
                duplicateRejectedCount,
                executor.ObservedCueCount,
                executor.CommandRequestedCount,
                executor.CommandAppliedCount,
                executor.CommandIgnoredByPolicyCount,
                executor.EnemyJumpCueMappedToDriverCommandCount,
                executor.EnemyChargeCueMappedToDriverCommandCount,
                executor.EnemyDeathCueMappedToDriverCommandCount,
                executor.TargetMissingCount,
                executor.AnchorMissingCount,
                executor.BindingMissingCount,
                executor.MapperMissingCount,
                executor.DriverMissingCount,
                executor.AnimatorMissingCount,
                executor.MissingPortCount);
        }
    }

    internal readonly struct EnemyPresentationPlaybackKey : IEquatable<EnemyPresentationPlaybackKey>
    {
        public EnemyPresentationPlaybackKey(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int enemyEntityId,
            PresentationAnimationCueKey cueKey,
            PresentationEnemyPresentationKind kind,
            PresentationEnemyPresentationPhase phase,
            int sourceSequenceId)
        {
            TickIndex = Math.Max(0, tickIndex);
            SemanticSource = semanticSource;
            EnemyEntityId = Math.Max(0, enemyEntityId);
            CueKey = cueKey;
            Kind = kind;
            Phase = phase;
            SourceSequenceId = Math.Max(0, sourceSequenceId);
        }

        public int TickIndex { get; }

        public PresentationSemanticSource SemanticSource { get; }

        public int EnemyEntityId { get; }

        public PresentationAnimationCueKey CueKey { get; }

        public PresentationEnemyPresentationKind Kind { get; }

        public PresentationEnemyPresentationPhase Phase { get; }

        public int SourceSequenceId { get; }

        public bool Equals(EnemyPresentationPlaybackKey other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   EnemyEntityId == other.EnemyEntityId &&
                   CueKey == other.CueKey &&
                   Kind == other.Kind &&
                   Phase == other.Phase &&
                   SourceSequenceId == other.SourceSequenceId;
        }

        public override bool Equals(object obj)
        {
            return obj is EnemyPresentationPlaybackKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)SemanticSource;
                hash = (hash * 397) ^ EnemyEntityId;
                hash = (hash * 397) ^ (int)CueKey;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ SourceSequenceId;
                return hash;
            }
        }
    }

    internal readonly struct EnemyPresentationOwnershipDiagnostics
    {
        public EnemyPresentationOwnershipDiagnostics(
            int executorAttemptCount,
            int executedByExecutorCount,
            int duplicateAttemptCount,
            EnemyPresentationExecutionOwner lastExecutionOwner)
        {
            ExecutorAttemptCount = Math.Max(0, executorAttemptCount);
            ExecutedByExecutorCount = Math.Max(0, executedByExecutorCount);
            DuplicateAttemptCount = Math.Max(0, duplicateAttemptCount);
            LastExecutionOwner = lastExecutionOwner;
        }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int DuplicateAttemptCount { get; }

        public EnemyPresentationExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class EnemyPresentationExecutionGuard
    {
        private readonly HashSet<EnemyPresentationPlaybackKey> _claimedKeys = new();
        private int _executorAttemptCount;
        private int _executedByExecutorCount;
        private int _duplicateAttemptCount;
        private EnemyPresentationExecutionOwner _lastExecutionOwner;

        public EnemyPresentationOwnershipDiagnostics Diagnostics =>
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
            _lastExecutionOwner = EnemyPresentationExecutionOwner.None;
        }

        public bool TryBeginExecution(
            EnemyPresentationExecutionOwner owner,
            in EnemyPresentationPlaybackKey key)
        {
            if (owner == EnemyPresentationExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "Enemy presentation owner must be explicit.");
            }

            RecordAttempt(owner);
            if (_claimedKeys.Contains(key))
            {
                _duplicateAttemptCount++;
                return false;
            }

            _claimedKeys.Add(key);
            _lastExecutionOwner = owner;
            _executedByExecutorCount++;

            return true;
        }

        private void RecordAttempt(EnemyPresentationExecutionOwner owner)
        {
            if (owner == EnemyPresentationExecutionOwner.CurrentExecutor)
            {
                _executorAttemptCount++;
            }
        }
    }

    internal readonly struct GameplayEnemyPresentationPlaybackRequest
    {
        public GameplayEnemyPresentationPlaybackRequest(
            EnemyPresentationPlaybackKey ownershipKey,
            PresentationAnimationCueKey cueKey,
            PresentationEnemyPayload enemyPayload,
            PresentationAnimationPayload animationPayload,
            PresentationTarget target,
            PresentationAnchor anchor)
        {
            OwnershipKey = ownershipKey;
            CueKey = cueKey;
            EnemyPayload = enemyPayload;
            AnimationPayload = animationPayload;
            Target = target;
            Anchor = anchor;
        }

        public EnemyPresentationPlaybackKey OwnershipKey { get; }

        public PresentationAnimationCueKey CueKey { get; }

        public PresentationEnemyPayload EnemyPayload { get; }

        public PresentationAnimationPayload AnimationPayload { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public int TickIndex => OwnershipKey.TickIndex;

        public int EnemyEntityId => EnemyPayload.EnemyEntityId;
    }

    internal readonly struct GameplayEnemyPresentationPlaybackResult
    {
        public GameplayEnemyPresentationPlaybackResult(GameplayEnemyPresentationPlaybackResultKind kind)
            : this(kind, GameplayEnemyPresentationCommandMappingKind.None)
        {
        }

        public GameplayEnemyPresentationPlaybackResult(
            GameplayEnemyPresentationPlaybackResultKind kind,
            GameplayEnemyPresentationCommandMappingKind commandMappingKind)
        {
            Kind = kind;
            CommandMappingKind = commandMappingKind;
        }

        public GameplayEnemyPresentationPlaybackResultKind Kind { get; }

        public GameplayEnemyPresentationCommandMappingKind CommandMappingKind { get; }
    }

    internal readonly struct GameplayEnemyPresentationExecutorDiagnostics
    {
        public GameplayEnemyPresentationExecutorDiagnostics(
            int observedCueCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int mapperMissingCount,
            int driverMissingCount,
            int animatorMissingCount,
            int duplicateRejectedCount,
            int commandRequestedCount,
            int commandAppliedCount,
            int commandIgnoredByPolicyCount,
            int missingPortCount,
            int enemyJumpCueMappedToDriverCommandCount,
            int enemyChargeCueMappedToDriverCommandCount,
            int enemyDeathCueMappedToDriverCommandCount,
            int lastTickIndex = 0,
            PresentationAnimationCueKey lastCueKey = PresentationAnimationCueKey.None,
            int lastDedupeKey = 0,
            int lastEnemyEntityId = 0,
            PresentationEnemyPresentationKind lastPresentationKind = PresentationEnemyPresentationKind.None,
            PresentationEnemyPresentationPhase lastPresentationPhase = PresentationEnemyPresentationPhase.None,
            PresentationEnemyPresentationOutcome lastPresentationOutcome = PresentationEnemyPresentationOutcome.None,
            EnemyPresentationTelemetryFailureReason lastFailureReason =
                EnemyPresentationTelemetryFailureReason.None,
            EnemyPresentationTelemetryCleanupReason lastCleanupReason =
                EnemyPresentationTelemetryCleanupReason.None)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            MapperMissingCount = Math.Max(0, mapperMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            AnimatorMissingCount = Math.Max(0, animatorMissingCount);
            DuplicateRejectedCount = Math.Max(0, duplicateRejectedCount);
            CommandRequestedCount = Math.Max(0, commandRequestedCount);
            CommandAppliedCount = Math.Max(0, commandAppliedCount);
            CommandIgnoredByPolicyCount = Math.Max(0, commandIgnoredByPolicyCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            EnemyJumpCueMappedToDriverCommandCount = Math.Max(0, enemyJumpCueMappedToDriverCommandCount);
            EnemyChargeCueMappedToDriverCommandCount = Math.Max(0, enemyChargeCueMappedToDriverCommandCount);
            EnemyDeathCueMappedToDriverCommandCount = Math.Max(0, enemyDeathCueMappedToDriverCommandCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastEnemyEntityId = Math.Max(0, lastEnemyEntityId);
            LastPresentationKind = lastPresentationKind;
            LastPresentationPhase = lastPresentationPhase;
            LastPresentationOutcome = lastPresentationOutcome;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
        }

        public int ObservedCueCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int MapperMissingCount { get; }

        public int DriverMissingCount { get; }

        public int AnimatorMissingCount { get; }

        public int DuplicateRejectedCount { get; }

        public int CommandRequestedCount { get; }

        public int CommandAppliedCount { get; }

        public int CommandIgnoredByPolicyCount { get; }

        public int MissingPortCount { get; }

        public int EnemyJumpCueMappedToDriverCommandCount { get; }

        public int EnemyChargeCueMappedToDriverCommandCount { get; }

        public int EnemyDeathCueMappedToDriverCommandCount { get; }

        public int LastTickIndex { get; }

        public PresentationAnimationCueKey LastCueKey { get; }

        public int LastDedupeKey { get; }

        public int LastEnemyEntityId { get; }

        public PresentationEnemyPresentationKind LastPresentationKind { get; }

        public PresentationEnemyPresentationPhase LastPresentationPhase { get; }

        public PresentationEnemyPresentationOutcome LastPresentationOutcome { get; }

        public EnemyPresentationTelemetryFailureReason LastFailureReason { get; }

        public EnemyPresentationTelemetryCleanupReason LastCleanupReason { get; }
    }

    internal interface IGameplayEnemyPresentationPlaybackPort
    {
        bool TryPlayEnemyPresentation(
            in GameplayEnemyPresentationPlaybackRequest request,
            out GameplayEnemyPresentationPlaybackResult result);

        void ResetSession();

        void HardCleanup();
    }

    internal delegate GameplayPresentationPipeline EnemyPresentationExecutionPipelineFactory(
        IGameplayEnemyPresentationPlaybackPort playbackPort,
        EnemyPresentationExecutionGuard executionGuard);

    internal sealed class GameplayEnemyPresentationExecutor : IPresentationAnimationExecutor
    {
        private readonly IGameplayEnemyPresentationPlaybackPort _playbackPort;
        private readonly EnemyPresentationExecutionGuard _executionGuard;

        public GameplayEnemyPresentationExecutor(
            IGameplayEnemyPresentationPlaybackPort playbackPort = null,
            EnemyPresentationExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _executionGuard = executionGuard;
        }

        public GameplayEnemyPresentationExecutorDiagnostics Diagnostics { get; private set; }

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
            var targetMissingCount = 0;
            var anchorMissingCount = 0;
            var bindingMissingCount = 0;
            var mapperMissingCount = 0;
            var driverMissingCount = 0;
            var animatorMissingCount = 0;
            var duplicateRejectedCount = 0;
            var commandRequestedCount = 0;
            var commandAppliedCount = 0;
            var commandIgnoredByPolicyCount = 0;
            var missingPortCount = 0;
            var enemyJumpCueMappedToDriverCommandCount = 0;
            var enemyChargeCueMappedToDriverCommandCount = 0;
            var enemyDeathCueMappedToDriverCommandCount = 0;
            var lastTickIndex = 0;
            var lastCueKey = PresentationAnimationCueKey.None;
            var lastDedupeKey = 0;
            var lastEnemyEntityId = 0;
            var lastPresentationKind = PresentationEnemyPresentationKind.None;
            var lastPresentationPhase = PresentationEnemyPresentationPhase.None;
            var lastPresentationOutcome = PresentationEnemyPresentationOutcome.None;
            var lastFailureReason = EnemyPresentationTelemetryFailureReason.None;

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var playbackCue = plan.Cues[i];
                if (!IsEnemyPresentationCue(playbackCue))
                {
                    continue;
                }

                observedCueCount++;
                CaptureLastCue(
                    playbackCue,
                    ref lastTickIndex,
                    ref lastCueKey,
                    ref lastDedupeKey,
                    ref lastEnemyEntityId,
                    ref lastPresentationKind,
                    ref lastPresentationPhase,
                    ref lastPresentationOutcome);
                if (!TryCreateRequest(playbackCue, out var request, out var missingKind))
                {
                    RecordMissing(
                        missingKind,
                        ref targetMissingCount,
                        ref anchorMissingCount,
                        ref bindingMissingCount,
                        ref mapperMissingCount,
                        ref driverMissingCount,
                        ref animatorMissingCount);
                    lastFailureReason = ToTelemetryFailureReason(missingKind);
                    continue;
                }

                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateRejectedCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.DuplicateRejected;
                    }
                    else
                    {
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.DuplicateRejected;
                    }

                    continue;
                }

                if (_playbackPort == null)
                {
                    missingPortCount++;
                    lastFailureReason = EnemyPresentationTelemetryFailureReason.PortMissing;
                    continue;
                }

                commandRequestedCount++;
                _playbackPort.TryPlayEnemyPresentation(request, out var result);
                RecordCommandMapping(
                    result.CommandMappingKind,
                    ref enemyJumpCueMappedToDriverCommandCount,
                    ref enemyChargeCueMappedToDriverCommandCount,
                    ref enemyDeathCueMappedToDriverCommandCount);
                switch (result.Kind)
                {
                    case GameplayEnemyPresentationPlaybackResultKind.Applied:
                    case GameplayEnemyPresentationPlaybackResultKind.Requested:
                        commandAppliedCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.None;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.TargetMissing;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.AnchorMissing;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.BindingMissing:
                        bindingMissingCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.BindingMissing;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.MapperMissing:
                        mapperMissingCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.MapperMissing;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.DriverMissing:
                        driverMissingCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.DriverMissing;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.AnimatorMissing:
                        animatorMissingCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.AnimatorMissing;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.IgnoredByPolicy:
                        commandIgnoredByPolicyCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.IgnoredByPolicy;
                        break;
                }
            }

            Diagnostics = new GameplayEnemyPresentationExecutorDiagnostics(
                observedCueCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                mapperMissingCount,
                driverMissingCount,
                animatorMissingCount,
                duplicateRejectedCount,
                commandRequestedCount,
                commandAppliedCount,
                commandIgnoredByPolicyCount,
                missingPortCount,
                enemyJumpCueMappedToDriverCommandCount,
                enemyChargeCueMappedToDriverCommandCount,
                enemyDeathCueMappedToDriverCommandCount,
                lastTickIndex,
                lastCueKey,
                lastDedupeKey,
                lastEnemyEntityId,
                lastPresentationKind,
                lastPresentationPhase,
                lastPresentationOutcome,
                lastFailureReason);
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
            Diagnostics = new GameplayEnemyPresentationExecutorDiagnostics(
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
                0,
                0,
                lastCleanupReason: EnemyPresentationTelemetryCleanupReason.ResetSession);
            _playbackPort?.ResetSession();
        }

        public void HardCleanup()
        {
            Diagnostics = new GameplayEnemyPresentationExecutorDiagnostics(
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
                0,
                0,
                lastCleanupReason: EnemyPresentationTelemetryCleanupReason.HardCleanupPresentationExtensions);
            _playbackPort?.HardCleanup();
        }

        private bool TryClaimExecution(in EnemyPresentationPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    EnemyPresentationExecutionOwner.CurrentExecutor,
                    key);
            }

            return true;
        }

        private static bool IsEnemyPresentationCue(in PresentationPlaybackCue playbackCue)
        {
            return playbackCue.Cue.Domain == PresentationDomain.Animation &&
                   playbackCue.Cue.Key.TryGetAnimationCueKey(out var cueKey) &&
                   IsEnemyPresentationCueKey(cueKey) &&
                   playbackCue.Cue.EnemyPayload.IsValid;
        }

        private static bool IsEnemyPresentationCueKey(PresentationAnimationCueKey cueKey)
        {
            switch (cueKey)
            {
                case PresentationAnimationCueKey.EnemyJumpWindup:
                case PresentationAnimationCueKey.EnemyJumpAirborne:
                case PresentationAnimationCueKey.EnemyJumpLand:
                case PresentationAnimationCueKey.EnemyChargeWindup:
                case PresentationAnimationCueKey.EnemyChargeActive:
                case PresentationAnimationCueKey.EnemyChargeRecover:
                case PresentationAnimationCueKey.EnemyDeath:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryCreateRequest(
            in PresentationPlaybackCue playbackCue,
            out GameplayEnemyPresentationPlaybackRequest request,
            out GameplayEnemyPresentationPlaybackResultKind missingKind)
        {
            request = default;
            missingKind = GameplayEnemyPresentationPlaybackResultKind.None;
            var cue = playbackCue.Cue;
            if (!cue.Key.TryGetAnimationCueKey(out var cueKey) ||
                !IsEnemyPresentationCueKey(cueKey) ||
                !cue.EnemyPayload.IsValid)
            {
                missingKind = GameplayEnemyPresentationPlaybackResultKind.BindingMissing;
                return false;
            }

            if (cue.Target.Kind != PresentationTargetKind.Entity ||
                cue.Target.EntityId <= 0 ||
                cue.EnemyPayload.EnemyEntityId <= 0)
            {
                missingKind = GameplayEnemyPresentationPlaybackResultKind.TargetMissing;
                return false;
            }

            if (cue.Anchor.Kind != PresentationAnchorKind.EntityVisualRoot &&
                cue.Anchor.Kind != PresentationAnchorKind.EntityCenter)
            {
                missingKind = GameplayEnemyPresentationPlaybackResultKind.AnchorMissing;
                return false;
            }

            var key = new EnemyPresentationPlaybackKey(
                cue.Source.TickIndex,
                cue.Source.SemanticSource,
                cue.Target.EntityId,
                cueKey,
                cue.EnemyPayload.Kind,
                cue.EnemyPayload.Phase,
                cue.EnemyPayload.SourceSequenceId);
            request = new GameplayEnemyPresentationPlaybackRequest(
                key,
                cueKey,
                cue.EnemyPayload,
                cue.AnimationPayload,
                cue.Target,
                cue.Anchor);
            return true;
        }

        private static void RecordMissing(
            GameplayEnemyPresentationPlaybackResultKind missingKind,
            ref int targetMissingCount,
            ref int anchorMissingCount,
            ref int bindingMissingCount,
            ref int mapperMissingCount,
            ref int driverMissingCount,
            ref int animatorMissingCount)
        {
            switch (missingKind)
            {
                case GameplayEnemyPresentationPlaybackResultKind.TargetMissing:
                    targetMissingCount++;
                    break;
                case GameplayEnemyPresentationPlaybackResultKind.AnchorMissing:
                    anchorMissingCount++;
                    break;
                case GameplayEnemyPresentationPlaybackResultKind.MapperMissing:
                    mapperMissingCount++;
                    break;
                case GameplayEnemyPresentationPlaybackResultKind.DriverMissing:
                    driverMissingCount++;
                    break;
                case GameplayEnemyPresentationPlaybackResultKind.AnimatorMissing:
                    animatorMissingCount++;
                    break;
                default:
                    bindingMissingCount++;
                    break;
            }
        }

        private static void RecordCommandMapping(
            GameplayEnemyPresentationCommandMappingKind mappingKind,
            ref int enemyJumpCueMappedToDriverCommandCount,
            ref int enemyChargeCueMappedToDriverCommandCount,
            ref int enemyDeathCueMappedToDriverCommandCount)
        {
            switch (mappingKind)
            {
                case GameplayEnemyPresentationCommandMappingKind.Jump:
                    enemyJumpCueMappedToDriverCommandCount++;
                    break;
                case GameplayEnemyPresentationCommandMappingKind.Charge:
                    enemyChargeCueMappedToDriverCommandCount++;
                    break;
                case GameplayEnemyPresentationCommandMappingKind.Death:
                    enemyDeathCueMappedToDriverCommandCount++;
                    break;
            }
        }

        private static void CaptureLastCue(
            in PresentationPlaybackCue playbackCue,
            ref int lastTickIndex,
            ref PresentationAnimationCueKey lastCueKey,
            ref int lastDedupeKey,
            ref int lastEnemyEntityId,
            ref PresentationEnemyPresentationKind lastPresentationKind,
            ref PresentationEnemyPresentationPhase lastPresentationPhase,
            ref PresentationEnemyPresentationOutcome lastPresentationOutcome)
        {
            var cue = playbackCue.Cue;
            lastTickIndex = cue.Source.TickIndex;
            lastDedupeKey = playbackCue.Policy.DedupeKey;
            lastEnemyEntityId = Math.Max(cue.Target.EntityId, cue.EnemyPayload.EnemyEntityId);
            lastPresentationKind = cue.EnemyPayload.Kind;
            lastPresentationPhase = cue.EnemyPayload.Phase;
            lastPresentationOutcome = cue.EnemyPayload.Outcome;
            lastCueKey = cue.Key.TryGetAnimationCueKey(out var cueKey)
                ? cueKey
                : PresentationAnimationCueKey.None;
        }

        private static EnemyPresentationTelemetryFailureReason ToTelemetryFailureReason(
            GameplayEnemyPresentationPlaybackResultKind resultKind)
        {
            switch (resultKind)
            {
                case GameplayEnemyPresentationPlaybackResultKind.TargetMissing:
                    return EnemyPresentationTelemetryFailureReason.TargetMissing;
                case GameplayEnemyPresentationPlaybackResultKind.AnchorMissing:
                    return EnemyPresentationTelemetryFailureReason.AnchorMissing;
                case GameplayEnemyPresentationPlaybackResultKind.MapperMissing:
                    return EnemyPresentationTelemetryFailureReason.MapperMissing;
                case GameplayEnemyPresentationPlaybackResultKind.DriverMissing:
                    return EnemyPresentationTelemetryFailureReason.DriverMissing;
                case GameplayEnemyPresentationPlaybackResultKind.AnimatorMissing:
                    return EnemyPresentationTelemetryFailureReason.AnimatorMissing;
                default:
                    return EnemyPresentationTelemetryFailureReason.BindingMissing;
            }
        }
    }

    internal sealed class GameplayEnemyPresentationSyncPlaybackPort : IGameplayEnemyPresentationPlaybackPort
    {
        private readonly GameplayAnimationSyncCoordinator _animationSync;
        private readonly GameplayPresentationStateStore _stateStore;

        public GameplayEnemyPresentationSyncPlaybackPort(
            GameplayAnimationSyncCoordinator animationSync,
            GameplayPresentationStateStore stateStore)
        {
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public bool TryPlayEnemyPresentation(
            in GameplayEnemyPresentationPlaybackRequest request,
            out GameplayEnemyPresentationPlaybackResult result)
        {
            return _animationSync.TryApplyEnemyPresentationPlayback(
                request,
                _stateStore.ViewsByEntityId,
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
