using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    public enum EnemyPresentationExecutionMode
    {
        LegacyEnemyPresentationMapper = 0,
        OrchestrationEnemyPresentationExecutor = 1,
    }

    internal enum EnemyPresentationExecutionOwner
    {
        None = 0,
        LegacyEnemyPresentationMapper = 1,
        OrchestrationEnemyPresentationExecutor = 2,
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
        LegacyOwnerActive = 9,
        IgnoredByPolicy = 10,
    }

    internal enum GameplayEnemyPresentationLegacyCommandMappingKind
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
        DuplicateSuppressed = 8,
        LegacyOwnerActive = 9,
        IgnoredByPolicy = 10,
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
            EnemyPresentationExecutionMode currentMode,
            bool isProductionDefaultOwner,
            EnemyPresentationExecutionMode productionDefaultMode,
            EnemyPresentationExecutionMode rollbackMode,
            int lastTickIndex,
            PresentationAnimationCueKey lastCueKey,
            int lastDedupeKey,
            int lastEnemyEntityId,
            PresentationEnemyPresentationKind lastPresentationKind,
            PresentationEnemyPresentationPhase lastPresentationPhase,
            PresentationEnemyPresentationOutcome lastPresentationOutcome,
            EnemyPresentationTelemetryFailureReason lastFailureReason,
            EnemyPresentationTelemetryCleanupReason lastCleanupReason,
            int legacyOwnerAttemptCount,
            int legacyOwnerSkippedByPolicyCount,
            int executorOwnerAttemptCount,
            int executorOwnerExecutedCount,
            int duplicateOwnerAttemptCount,
            int duplicateSuppressedCount,
            int observedCueCount,
            int playbackCommandRequestedCount,
            int playbackCommandAppliedCount,
            int playbackCommandIgnoredByPolicyCount,
            int enemyJumpCueMappedToLegacyCommandCount,
            int enemyChargeCueMappedToLegacyCommandCount,
            int enemyDeathCueMappedToLegacyCommandCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int mapperMissingCount,
            int driverMissingCount,
            int animatorMissingCount,
            int portMissingCount)
        {
            CurrentMode = currentMode;
            IsProductionDefaultOwner = isProductionDefaultOwner;
            ProductionDefaultMode = productionDefaultMode;
            RollbackMode = rollbackMode;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastEnemyEntityId = Math.Max(0, lastEnemyEntityId);
            LastPresentationKind = lastPresentationKind;
            LastPresentationPhase = lastPresentationPhase;
            LastPresentationOutcome = lastPresentationOutcome;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
            LegacyOwnerAttemptCount = Math.Max(0, legacyOwnerAttemptCount);
            LegacyOwnerSkippedByPolicyCount = Math.Max(0, legacyOwnerSkippedByPolicyCount);
            ExecutorOwnerAttemptCount = Math.Max(0, executorOwnerAttemptCount);
            ExecutorOwnerExecutedCount = Math.Max(0, executorOwnerExecutedCount);
            DuplicateOwnerAttemptCount = Math.Max(0, duplicateOwnerAttemptCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            ObservedCueCount = Math.Max(0, observedCueCount);
            PlaybackCommandRequestedCount = Math.Max(0, playbackCommandRequestedCount);
            PlaybackCommandAppliedCount = Math.Max(0, playbackCommandAppliedCount);
            PlaybackCommandIgnoredByPolicyCount = Math.Max(0, playbackCommandIgnoredByPolicyCount);
            EnemyJumpCueMappedToLegacyCommandCount = Math.Max(0, enemyJumpCueMappedToLegacyCommandCount);
            EnemyChargeCueMappedToLegacyCommandCount = Math.Max(0, enemyChargeCueMappedToLegacyCommandCount);
            EnemyDeathCueMappedToLegacyCommandCount = Math.Max(0, enemyDeathCueMappedToLegacyCommandCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            MapperMissingCount = Math.Max(0, mapperMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            AnimatorMissingCount = Math.Max(0, animatorMissingCount);
            PortMissingCount = Math.Max(0, portMissingCount);
        }

        public EnemyPresentationExecutionMode CurrentMode { get; }
        public bool IsProductionDefaultOwner { get; }
        public EnemyPresentationExecutionMode ProductionDefaultMode { get; }
        public EnemyPresentationExecutionMode RollbackMode { get; }
        public int LastTickIndex { get; }
        public PresentationAnimationCueKey LastCueKey { get; }
        public int LastDedupeKey { get; }
        public int LastEnemyEntityId { get; }
        public PresentationEnemyPresentationKind LastPresentationKind { get; }
        public PresentationEnemyPresentationPhase LastPresentationPhase { get; }
        public PresentationEnemyPresentationOutcome LastPresentationOutcome { get; }
        public EnemyPresentationTelemetryFailureReason LastFailureReason { get; }
        public EnemyPresentationTelemetryCleanupReason LastCleanupReason { get; }
        public int LegacyOwnerAttemptCount { get; }
        public int LegacyOwnerSkippedByPolicyCount { get; }
        public int ExecutorOwnerAttemptCount { get; }
        public int ExecutorOwnerExecutedCount { get; }
        public int DuplicateOwnerAttemptCount { get; }
        public int DuplicateSuppressedCount { get; }
        public int ObservedCueCount { get; }
        public int PlaybackCommandRequestedCount { get; }
        public int PlaybackCommandAppliedCount { get; }
        public int PlaybackCommandIgnoredByPolicyCount { get; }
        public int EnemyJumpCueMappedToLegacyCommandCount { get; }
        public int EnemyChargeCueMappedToLegacyCommandCount { get; }
        public int EnemyDeathCueMappedToLegacyCommandCount { get; }
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
            EnemyPresentationExecutionMode mode,
            EnemyPresentationOwnershipDiagnostics ownership,
            GameplayPresentationPipeline pipeline)
        {
            var executor = ResolveExecutorDiagnostics(pipeline);
            var duplicateSuppressedCount = Math.Max(
                executor.DuplicateSuppressedCount,
                ownership.DuplicateAttemptCount);
            var lastFailureReason =
                duplicateSuppressedCount > executor.DuplicateSuppressedCount &&
                executor.LastFailureReason == EnemyPresentationTelemetryFailureReason.None
                    ? EnemyPresentationTelemetryFailureReason.DuplicateSuppressed
                    : executor.LastFailureReason;

            return new EnemyPresentationProductionTelemetrySnapshot(
                mode,
                mode == EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper,
                EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper,
                EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper,
                executor.LastTickIndex,
                executor.LastCueKey,
                executor.LastDedupeKey,
                executor.LastEnemyEntityId,
                executor.LastPresentationKind,
                executor.LastPresentationPhase,
                executor.LastPresentationOutcome,
                lastFailureReason,
                executor.LastCleanupReason,
                ownership.LegacyAttemptCount,
                ownership.SkippedLegacyBecauseExecutorOwnerCount,
                ownership.ExecutorAttemptCount,
                ownership.ExecutedByExecutorCount,
                ownership.DuplicateAttemptCount,
                duplicateSuppressedCount,
                executor.ObservedCueCount,
                executor.CommandRequestedCount,
                executor.CommandAppliedCount,
                executor.CommandIgnoredByPolicyCount,
                executor.EnemyJumpCueMappedToLegacyCommandCount,
                executor.EnemyChargeCueMappedToLegacyCommandCount,
                executor.EnemyDeathCueMappedToLegacyCommandCount,
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
            EnemyPresentationExecutionMode mode,
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            EnemyPresentationExecutionOwner lastExecutionOwner)
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

        public EnemyPresentationExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public EnemyPresentationExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class EnemyPresentationExecutionGuard
    {
        private readonly HashSet<EnemyPresentationPlaybackKey> _claimedKeys = new();
        private EnemyPresentationExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private EnemyPresentationExecutionOwner _lastExecutionOwner;

        public EnemyPresentationExecutionGuard(
            EnemyPresentationExecutionMode mode = EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper)
        {
            _mode = NormalizeMode(mode);
        }

        public EnemyPresentationOwnershipDiagnostics Diagnostics =>
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

        public void Configure(EnemyPresentationExecutionMode mode)
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
            _lastExecutionOwner = EnemyPresentationExecutionOwner.None;
        }

        public void RecordSkippedByPolicy(EnemyPresentationExecutionOwner skippedOwner)
        {
            if (skippedOwner == EnemyPresentationExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOwner), "Enemy presentation owner must be explicit.");
            }

            RecordAttempt(skippedOwner);
            RecordPolicySkip(skippedOwner);
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
            if (owner == EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static EnemyPresentationExecutionMode NormalizeMode(EnemyPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyPresentationExecutionMode), mode)
                ? mode
                : EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper;
        }

        private bool IsOwnerAllowed(EnemyPresentationExecutionOwner owner)
        {
            return (_mode == EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper &&
                    owner == EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper) ||
                   (_mode == EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor &&
                    owner == EnemyPresentationExecutionOwner.OrchestrationEnemyPresentationExecutor);
        }

        private void RecordAttempt(EnemyPresentationExecutionOwner owner)
        {
            if (owner == EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper)
            {
                _legacyAttemptCount++;
            }
            else if (owner == EnemyPresentationExecutionOwner.OrchestrationEnemyPresentationExecutor)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(EnemyPresentationExecutionOwner owner)
        {
            if (owner == EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (owner == EnemyPresentationExecutionOwner.OrchestrationEnemyPresentationExecutor)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
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
            : this(kind, GameplayEnemyPresentationLegacyCommandMappingKind.None)
        {
        }

        public GameplayEnemyPresentationPlaybackResult(
            GameplayEnemyPresentationPlaybackResultKind kind,
            GameplayEnemyPresentationLegacyCommandMappingKind legacyCommandMappingKind)
        {
            Kind = kind;
            LegacyCommandMappingKind = legacyCommandMappingKind;
        }

        public GameplayEnemyPresentationPlaybackResultKind Kind { get; }

        public GameplayEnemyPresentationLegacyCommandMappingKind LegacyCommandMappingKind { get; }
    }

    internal readonly struct GameplayEnemyPresentationExecutorDiagnostics
    {
        public GameplayEnemyPresentationExecutorDiagnostics(
            int observedCueCount,
            int legacyOwnerNoOpCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int mapperMissingCount,
            int driverMissingCount,
            int animatorMissingCount,
            int duplicateSuppressedCount,
            int commandRequestedCount,
            int commandAppliedCount,
            int commandIgnoredByPolicyCount,
            int missingPortCount,
            int enemyJumpCueMappedToLegacyCommandCount,
            int enemyChargeCueMappedToLegacyCommandCount,
            int enemyDeathCueMappedToLegacyCommandCount,
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
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            MapperMissingCount = Math.Max(0, mapperMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            AnimatorMissingCount = Math.Max(0, animatorMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            CommandRequestedCount = Math.Max(0, commandRequestedCount);
            CommandAppliedCount = Math.Max(0, commandAppliedCount);
            CommandIgnoredByPolicyCount = Math.Max(0, commandIgnoredByPolicyCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            EnemyJumpCueMappedToLegacyCommandCount = Math.Max(0, enemyJumpCueMappedToLegacyCommandCount);
            EnemyChargeCueMappedToLegacyCommandCount = Math.Max(0, enemyChargeCueMappedToLegacyCommandCount);
            EnemyDeathCueMappedToLegacyCommandCount = Math.Max(0, enemyDeathCueMappedToLegacyCommandCount);
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

        public int LegacyOwnerNoOpCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int MapperMissingCount { get; }

        public int DriverMissingCount { get; }

        public int AnimatorMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int CommandRequestedCount { get; }

        public int CommandAppliedCount { get; }

        public int CommandIgnoredByPolicyCount { get; }

        public int MissingPortCount { get; }

        public int EnemyJumpCueMappedToLegacyCommandCount { get; }

        public int EnemyChargeCueMappedToLegacyCommandCount { get; }

        public int EnemyDeathCueMappedToLegacyCommandCount { get; }

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
        EnemyPresentationExecutionMode mode,
        IGameplayEnemyPresentationPlaybackPort playbackPort,
        EnemyPresentationExecutionGuard executionGuard);

    internal sealed class GameplayEnemyPresentationExecutor : IPresentationAnimationExecutor
    {
        private readonly IGameplayEnemyPresentationPlaybackPort _playbackPort;
        private readonly EnemyPresentationExecutionMode _mode;
        private readonly EnemyPresentationExecutionGuard _executionGuard;

        public GameplayEnemyPresentationExecutor(
            IGameplayEnemyPresentationPlaybackPort playbackPort = null,
            EnemyPresentationExecutionMode mode = EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper,
            EnemyPresentationExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
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
            var legacyOwnerNoOpCount = 0;
            var targetMissingCount = 0;
            var anchorMissingCount = 0;
            var bindingMissingCount = 0;
            var mapperMissingCount = 0;
            var driverMissingCount = 0;
            var animatorMissingCount = 0;
            var duplicateSuppressedCount = 0;
            var commandRequestedCount = 0;
            var commandAppliedCount = 0;
            var commandIgnoredByPolicyCount = 0;
            var missingPortCount = 0;
            var enemyJumpCueMappedToLegacyCommandCount = 0;
            var enemyChargeCueMappedToLegacyCommandCount = 0;
            var enemyDeathCueMappedToLegacyCommandCount = 0;
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
                if (_mode != EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor)
                {
                    legacyOwnerNoOpCount++;
                    lastFailureReason = EnemyPresentationTelemetryFailureReason.LegacyOwnerActive;
                    continue;
                }

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
                        duplicateSuppressedCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.DuplicateSuppressed;
                    }
                    else
                    {
                        legacyOwnerNoOpCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.LegacyOwnerActive;
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
                RecordLegacyCommandMapping(
                    result.LegacyCommandMappingKind,
                    ref enemyJumpCueMappedToLegacyCommandCount,
                    ref enemyChargeCueMappedToLegacyCommandCount,
                    ref enemyDeathCueMappedToLegacyCommandCount);
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
                    case GameplayEnemyPresentationPlaybackResultKind.LegacyOwnerActive:
                        legacyOwnerNoOpCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.LegacyOwnerActive;
                        break;
                    case GameplayEnemyPresentationPlaybackResultKind.IgnoredByPolicy:
                        commandIgnoredByPolicyCount++;
                        lastFailureReason = EnemyPresentationTelemetryFailureReason.IgnoredByPolicy;
                        break;
                }
            }

            Diagnostics = new GameplayEnemyPresentationExecutorDiagnostics(
                observedCueCount,
                legacyOwnerNoOpCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                mapperMissingCount,
                driverMissingCount,
                animatorMissingCount,
                duplicateSuppressedCount,
                commandRequestedCount,
                commandAppliedCount,
                commandIgnoredByPolicyCount,
                missingPortCount,
                enemyJumpCueMappedToLegacyCommandCount,
                enemyChargeCueMappedToLegacyCommandCount,
                enemyDeathCueMappedToLegacyCommandCount,
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
                0,
                lastCleanupReason: EnemyPresentationTelemetryCleanupReason.ResetSession);
            if (_mode == EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor)
            {
                _playbackPort?.ResetSession();
            }
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
                0,
                lastCleanupReason: EnemyPresentationTelemetryCleanupReason.HardCleanupPresentationExtensions);
            if (_mode == EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor)
            {
                _playbackPort?.HardCleanup();
            }
        }

        private bool TryClaimExecution(in EnemyPresentationPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    EnemyPresentationExecutionOwner.OrchestrationEnemyPresentationExecutor,
                    key);
            }

            return _mode == EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor;
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

        private static void RecordLegacyCommandMapping(
            GameplayEnemyPresentationLegacyCommandMappingKind mappingKind,
            ref int enemyJumpCueMappedToLegacyCommandCount,
            ref int enemyChargeCueMappedToLegacyCommandCount,
            ref int enemyDeathCueMappedToLegacyCommandCount)
        {
            switch (mappingKind)
            {
                case GameplayEnemyPresentationLegacyCommandMappingKind.Jump:
                    enemyJumpCueMappedToLegacyCommandCount++;
                    break;
                case GameplayEnemyPresentationLegacyCommandMappingKind.Charge:
                    enemyChargeCueMappedToLegacyCommandCount++;
                    break;
                case GameplayEnemyPresentationLegacyCommandMappingKind.Death:
                    enemyDeathCueMappedToLegacyCommandCount++;
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

        private static EnemyPresentationExecutionMode NormalizeMode(EnemyPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyPresentationExecutionMode), mode)
                ? mode
                : EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper;
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
