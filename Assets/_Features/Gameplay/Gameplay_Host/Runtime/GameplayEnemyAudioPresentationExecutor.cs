using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    public enum EnemyAudioExecutionMode
    {
        LegacyEnemyAudioController = 0,
        OrchestrationEnemyAudioBridge = 1,
    }

    internal enum EnemyAudioExecutionOwner
    {
        None = 0,
        LegacyEnemyAudioController = 1,
        OrchestrationEnemyAudioBridge = 2,
    }

    internal enum GameplayEnemyAudioPlaybackResultKind
    {
        None = 0,
        OwnerViewMissing = 1,
        AuthoringMissing = 2,
        ProfileMissing = 3,
        BindingMissing = 4,
        UnsupportedSemantic = 5,
        UnsupportedLoopSemantic = 6,
        PortMissing = 7,
        Requested = 8,
        Succeeded = 9,
        OptionalProfileEntryMissing = 10,
        LegacyOwnerActive = 11,
    }

    internal enum EnemyAudioTelemetryFailureReason
    {
        None = 0,
        OwnerViewMissing = 1,
        AuthoringMissing = 2,
        ProfileMissing = 3,
        BindingMissing = 4,
        UnsupportedSemantic = 5,
        UnsupportedLoopSemantic = 6,
        PortMissing = 7,
        DuplicateSuppressed = 8,
        LegacyOwnerActive = 9,
        OptionalProfileEntryMissing = 10,
    }

    internal enum EnemyAudioTelemetryCleanupReason
    {
        None = 0,
        ResetSession = 1,
        HardCleanupPresentationExtensions = 2,
    }

    internal readonly struct EnemyAudioProductionTelemetrySnapshot
    {
        public EnemyAudioProductionTelemetrySnapshot(
            EnemyAudioExecutionMode currentMode,
            bool isProductionDefaultOwner,
            EnemyAudioExecutionMode productionDefaultMode,
            EnemyAudioExecutionMode rollbackMode,
            int lastTickIndex,
            PresentationEnemyAudioCueKey lastCueKey,
            int lastDedupeKey,
            int lastOwnerEntityId,
            PresentationEnemyAudioOriginKind lastOriginKind,
            PresentationEnemyAudioPhase lastPhase,
            EnemyAudioTelemetryFailureReason lastFailureReason,
            EnemyAudioTelemetryCleanupReason lastCleanupReason,
            int legacyOwnerAttemptCount,
            int legacyOwnerSkippedByPolicyCount,
            int executorOwnerAttemptCount,
            int executorOwnerExecutedCount,
            int duplicateOwnerAttemptCount,
            int duplicateSuppressedCount,
            int observedCueCount,
            int requestPlannedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int optionalProfileEntryMissingNoOpCount,
            int ownerViewMissingCount,
            int authoringMissingCount,
            int profileMissingCount,
            int bindingMissingCount,
            int unsupportedSemanticCount,
            int unsupportedLoopSemanticCount,
            int portMissingCount)
        {
            CurrentMode = currentMode;
            IsProductionDefaultOwner = isProductionDefaultOwner;
            ProductionDefaultMode = productionDefaultMode;
            RollbackMode = rollbackMode;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastOwnerEntityId = Math.Max(0, lastOwnerEntityId);
            LastOriginKind = lastOriginKind;
            LastPhase = lastPhase;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
            LegacyOwnerAttemptCount = Math.Max(0, legacyOwnerAttemptCount);
            LegacyOwnerSkippedByPolicyCount = Math.Max(0, legacyOwnerSkippedByPolicyCount);
            ExecutorOwnerAttemptCount = Math.Max(0, executorOwnerAttemptCount);
            ExecutorOwnerExecutedCount = Math.Max(0, executorOwnerExecutedCount);
            DuplicateOwnerAttemptCount = Math.Max(0, duplicateOwnerAttemptCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            ObservedCueCount = Math.Max(0, observedCueCount);
            RequestPlannedCount = Math.Max(0, requestPlannedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            OptionalProfileEntryMissingNoOpCount = Math.Max(0, optionalProfileEntryMissingNoOpCount);
            OwnerViewMissingCount = Math.Max(0, ownerViewMissingCount);
            AuthoringMissingCount = Math.Max(0, authoringMissingCount);
            ProfileMissingCount = Math.Max(0, profileMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            UnsupportedSemanticCount = Math.Max(0, unsupportedSemanticCount);
            UnsupportedLoopSemanticCount = Math.Max(0, unsupportedLoopSemanticCount);
            PortMissingCount = Math.Max(0, portMissingCount);
        }

        public EnemyAudioExecutionMode CurrentMode { get; }
        public bool IsProductionDefaultOwner { get; }
        public EnemyAudioExecutionMode ProductionDefaultMode { get; }
        public EnemyAudioExecutionMode RollbackMode { get; }
        public int LastTickIndex { get; }
        public PresentationEnemyAudioCueKey LastCueKey { get; }
        public int LastDedupeKey { get; }
        public int LastOwnerEntityId { get; }
        public PresentationEnemyAudioOriginKind LastOriginKind { get; }
        public PresentationEnemyAudioPhase LastPhase { get; }
        public EnemyAudioTelemetryFailureReason LastFailureReason { get; }
        public EnemyAudioTelemetryCleanupReason LastCleanupReason { get; }
        public int LegacyOwnerAttemptCount { get; }
        public int LegacyOwnerSkippedByPolicyCount { get; }
        public int ExecutorOwnerAttemptCount { get; }
        public int ExecutorOwnerExecutedCount { get; }
        public int DuplicateOwnerAttemptCount { get; }
        public int DuplicateSuppressedCount { get; }
        public int ObservedCueCount { get; }
        public int RequestPlannedCount { get; }
        public int PlaybackRequestedCount { get; }
        public int PlaybackSucceededCount { get; }
        public int OptionalProfileEntryMissingNoOpCount { get; }
        public int OwnerViewMissingCount { get; }
        public int AuthoringMissingCount { get; }
        public int ProfileMissingCount { get; }
        public int BindingMissingCount { get; }
        public int UnsupportedSemanticCount { get; }
        public int UnsupportedLoopSemanticCount { get; }
        public int PortMissingCount { get; }
    }

    internal static class EnemyAudioProductionTelemetryBuilder
    {
        public static GameplayEnemyAudioExecutorDiagnostics ResolveExecutorDiagnostics(GameplayPresentationPipeline pipeline)
        {
            if (pipeline == null)
            {
                return default;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayEnemyAudioPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        public static EnemyAudioProductionTelemetrySnapshot Build(
            EnemyAudioExecutionMode mode,
            EnemyAudioOwnershipDiagnostics ownership,
            GameplayPresentationPipeline pipeline)
        {
            var executor = ResolveExecutorDiagnostics(pipeline);
            var duplicateSuppressedCount = Math.Max(
                executor.DuplicateSuppressedCount,
                ownership.DuplicateAttemptCount);
            var lastFailureReason =
                duplicateSuppressedCount > executor.DuplicateSuppressedCount &&
                executor.LastFailureReason == EnemyAudioTelemetryFailureReason.None
                    ? EnemyAudioTelemetryFailureReason.DuplicateSuppressed
                    : executor.LastFailureReason;

            return new EnemyAudioProductionTelemetrySnapshot(
                mode,
                mode == EnemyAudioExecutionMode.LegacyEnemyAudioController,
                EnemyAudioExecutionMode.LegacyEnemyAudioController,
                EnemyAudioExecutionMode.LegacyEnemyAudioController,
                executor.LastTickIndex,
                executor.LastCueKey,
                executor.LastDedupeKey,
                executor.LastOwnerEntityId,
                executor.LastOriginKind,
                executor.LastPhase,
                lastFailureReason,
                executor.LastCleanupReason,
                ownership.LegacyAttemptCount,
                ownership.SkippedLegacyBecauseExecutorOwnerCount,
                ownership.ExecutorAttemptCount,
                ownership.ExecutedByExecutorCount,
                ownership.DuplicateAttemptCount,
                duplicateSuppressedCount,
                executor.ObservedCueCount,
                executor.RequestPlannedCount,
                executor.PlaybackRequestedCount,
                executor.PlaybackSucceededCount,
                executor.OptionalProfileEntryMissingNoOpCount,
                executor.OwnerViewMissingCount,
                executor.AuthoringMissingCount,
                executor.ProfileMissingCount,
                executor.BindingMissingCount,
                executor.UnsupportedSemanticCount,
                executor.UnsupportedLoopSemanticCount,
                executor.PortMissingCount);
        }
    }

    internal readonly struct EnemyAudioPlaybackKey : IEquatable<EnemyAudioPlaybackKey>
    {
        public EnemyAudioPlaybackKey(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int ownerEntityId,
            PresentationEnemyAudioCueKey cueKey,
            PresentationEnemyAudioOriginKind originKind,
            PresentationEnemyAudioPhase phase,
            int sourceSequenceId,
            int targetEntityId,
            int impactTick,
            int impactId,
            int presentationKey,
            int orderIndex)
        {
            TickIndex = Math.Max(0, tickIndex);
            SemanticSource = semanticSource;
            OwnerEntityId = Math.Max(0, ownerEntityId);
            CueKey = cueKey;
            OriginKind = originKind;
            Phase = phase;
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            TargetEntityId = Math.Max(0, targetEntityId);
            ImpactTick = Math.Max(0, impactTick);
            ImpactId = Math.Max(0, impactId);
            PresentationKey = Math.Max(0, presentationKey);
            OrderIndex = Math.Max(0, orderIndex);
        }

        public int TickIndex { get; }

        public PresentationSemanticSource SemanticSource { get; }

        public int OwnerEntityId { get; }

        public PresentationEnemyAudioCueKey CueKey { get; }

        public PresentationEnemyAudioOriginKind OriginKind { get; }

        public PresentationEnemyAudioPhase Phase { get; }

        public int SourceSequenceId { get; }

        public int TargetEntityId { get; }

        public int ImpactTick { get; }

        public int ImpactId { get; }

        public int PresentationKey { get; }

        public int OrderIndex { get; }

        public bool Equals(EnemyAudioPlaybackKey other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   OwnerEntityId == other.OwnerEntityId &&
                   CueKey == other.CueKey &&
                   OriginKind == other.OriginKind &&
                   Phase == other.Phase &&
                   SourceSequenceId == other.SourceSequenceId &&
                   TargetEntityId == other.TargetEntityId &&
                   ImpactTick == other.ImpactTick &&
                   ImpactId == other.ImpactId &&
                   PresentationKey == other.PresentationKey;
        }

        public override bool Equals(object obj)
        {
            return obj is EnemyAudioPlaybackKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)SemanticSource;
                hash = (hash * 397) ^ OwnerEntityId;
                hash = (hash * 397) ^ (int)CueKey;
                hash = (hash * 397) ^ (int)OriginKind;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ SourceSequenceId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ ImpactTick;
                hash = (hash * 397) ^ ImpactId;
                hash = (hash * 397) ^ PresentationKey;
                return hash;
            }
        }
    }

    internal readonly struct EnemyAudioOwnershipDiagnostics
    {
        public EnemyAudioOwnershipDiagnostics(
            EnemyAudioExecutionMode mode,
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            EnemyAudioExecutionOwner lastExecutionOwner)
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

        public EnemyAudioExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public EnemyAudioExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class EnemyAudioExecutionGuard
    {
        private readonly HashSet<EnemyAudioPlaybackKey> _claimedKeys = new();
        private EnemyAudioExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private EnemyAudioExecutionOwner _lastExecutionOwner;

        public EnemyAudioExecutionGuard(
            EnemyAudioExecutionMode mode = EnemyAudioExecutionMode.LegacyEnemyAudioController)
        {
            _mode = NormalizeMode(mode);
        }

        public EnemyAudioOwnershipDiagnostics Diagnostics =>
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

        public void Configure(EnemyAudioExecutionMode mode)
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
            _lastExecutionOwner = EnemyAudioExecutionOwner.None;
        }

        public void RecordSkippedByPolicy(EnemyAudioExecutionOwner skippedOwner)
        {
            if (skippedOwner == EnemyAudioExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOwner), "Enemy audio owner must be explicit.");
            }

            RecordAttempt(skippedOwner);
            RecordPolicySkip(skippedOwner);
        }

        public bool TryBeginExecution(
            EnemyAudioExecutionOwner owner,
            in EnemyAudioPlaybackKey key)
        {
            if (owner == EnemyAudioExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "Enemy audio owner must be explicit.");
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
            if (owner == EnemyAudioExecutionOwner.LegacyEnemyAudioController)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static EnemyAudioExecutionMode NormalizeMode(EnemyAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyAudioExecutionMode), mode)
                ? mode
                : EnemyAudioExecutionMode.LegacyEnemyAudioController;
        }

        private bool IsOwnerAllowed(EnemyAudioExecutionOwner owner)
        {
            return (_mode == EnemyAudioExecutionMode.LegacyEnemyAudioController &&
                    owner == EnemyAudioExecutionOwner.LegacyEnemyAudioController) ||
                   (_mode == EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge &&
                    owner == EnemyAudioExecutionOwner.OrchestrationEnemyAudioBridge);
        }

        private void RecordAttempt(EnemyAudioExecutionOwner owner)
        {
            if (owner == EnemyAudioExecutionOwner.LegacyEnemyAudioController)
            {
                _legacyAttemptCount++;
            }
            else if (owner == EnemyAudioExecutionOwner.OrchestrationEnemyAudioBridge)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(EnemyAudioExecutionOwner owner)
        {
            if (owner == EnemyAudioExecutionOwner.LegacyEnemyAudioController)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (owner == EnemyAudioExecutionOwner.OrchestrationEnemyAudioBridge)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
            }
        }
    }

    internal readonly struct GameplayEnemyAudioPlaybackRequest
    {
        public GameplayEnemyAudioPlaybackRequest(
            EnemyAudioPlaybackKey ownershipKey,
            PresentationEnemyAudioCueKey cueKey,
            EnemyAudioCue cue,
            PresentationSource source,
            PresentationTarget target,
            PresentationAnchor anchor,
            PresentationEnemyAudioPayload enemyAudioPayload,
            AudioPlaybackContext context,
            float delaySeconds)
        {
            OwnershipKey = ownershipKey;
            CueKey = cueKey;
            Cue = cue;
            Source = source;
            Target = target;
            Anchor = anchor;
            EnemyAudioPayload = enemyAudioPayload;
            Context = context;
            DelaySeconds = Math.Max(0f, delaySeconds);
        }

        public EnemyAudioPlaybackKey OwnershipKey { get; }

        public PresentationEnemyAudioCueKey CueKey { get; }

        public EnemyAudioCue Cue { get; }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public PresentationEnemyAudioPayload EnemyAudioPayload { get; }

        public AudioPlaybackContext Context { get; }

        public float DelaySeconds { get; }

        public int TickIndex => OwnershipKey.TickIndex;

        public int OwnerEntityId => OwnershipKey.OwnerEntityId;

        public EnemyAudioRequest ToEnemyAudioRequest()
        {
            var identity = EnemyAudioPayload.ImpactId > 0 ||
                           EnemyAudioPayload.ImpactTick > 0 ||
                           EnemyAudioPayload.PresentationKey > 0
                ? new EnemyAudioRequestIdentity(
                    true,
                    OwnerEntityId,
                    EnemyAudioPayload.TargetCell,
                    EnemyAudioPayload.ImpactTick,
                    EnemyAudioPayload.ImpactId,
                    EnemyAudioPayload.PresentationKey)
                : default;
            return new EnemyAudioRequest(
                OwnerEntityId,
                Cue,
                Context,
                DelaySeconds,
                identity);
        }
    }

    internal readonly struct GameplayEnemyAudioPlaybackResult
    {
        public GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind kind)
        {
            Kind = kind;
        }

        public GameplayEnemyAudioPlaybackResultKind Kind { get; }
    }

    internal readonly struct GameplayEnemyAudioExecutorDiagnostics
    {
        public GameplayEnemyAudioExecutorDiagnostics(
            int observedCueCount,
            int ownerViewMissingCount,
            int authoringMissingCount,
            int profileMissingCount,
            int bindingMissingCount,
            int unsupportedSemanticCount,
            int unsupportedLoopSemanticCount,
            int portMissingCount,
            int duplicateSuppressedCount,
            int legacyOwnerNoOpCount,
            int requestPlannedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int optionalProfileEntryMissingNoOpCount,
            int lastTickIndex = 0,
            PresentationEnemyAudioCueKey lastCueKey = PresentationEnemyAudioCueKey.None,
            int lastDedupeKey = 0,
            int lastOwnerEntityId = 0,
            PresentationEnemyAudioOriginKind lastOriginKind = PresentationEnemyAudioOriginKind.None,
            PresentationEnemyAudioPhase lastPhase = PresentationEnemyAudioPhase.None,
            EnemyAudioTelemetryFailureReason lastFailureReason =
                EnemyAudioTelemetryFailureReason.None,
            EnemyAudioTelemetryCleanupReason lastCleanupReason =
                EnemyAudioTelemetryCleanupReason.None)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            OwnerViewMissingCount = Math.Max(0, ownerViewMissingCount);
            AuthoringMissingCount = Math.Max(0, authoringMissingCount);
            ProfileMissingCount = Math.Max(0, profileMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            UnsupportedSemanticCount = Math.Max(0, unsupportedSemanticCount);
            UnsupportedLoopSemanticCount = Math.Max(0, unsupportedLoopSemanticCount);
            PortMissingCount = Math.Max(0, portMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
            RequestPlannedCount = Math.Max(0, requestPlannedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            OptionalProfileEntryMissingNoOpCount = Math.Max(0, optionalProfileEntryMissingNoOpCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastOwnerEntityId = Math.Max(0, lastOwnerEntityId);
            LastOriginKind = lastOriginKind;
            LastPhase = lastPhase;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
        }

        public int ObservedCueCount { get; }

        public int OwnerViewMissingCount { get; }

        public int AuthoringMissingCount { get; }

        public int ProfileMissingCount { get; }

        public int BindingMissingCount { get; }

        public int UnsupportedSemanticCount { get; }

        public int UnsupportedLoopSemanticCount { get; }

        public int PortMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int LegacyOwnerNoOpCount { get; }

        public int RequestPlannedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int OptionalProfileEntryMissingNoOpCount { get; }

        public int LastTickIndex { get; }

        public PresentationEnemyAudioCueKey LastCueKey { get; }

        public int LastDedupeKey { get; }

        public int LastOwnerEntityId { get; }

        public PresentationEnemyAudioOriginKind LastOriginKind { get; }

        public PresentationEnemyAudioPhase LastPhase { get; }

        public EnemyAudioTelemetryFailureReason LastFailureReason { get; }

        public EnemyAudioTelemetryCleanupReason LastCleanupReason { get; }
    }

    internal interface IGameplayEnemyAudioPlaybackPort
    {
        bool TryPlayEnemyAudio(
            in GameplayEnemyAudioPlaybackRequest request,
            out GameplayEnemyAudioPlaybackResult result);

        void ResetSession();

        void HardCleanup();
    }

    internal delegate GameplayPresentationPipeline EnemyAudioExecutionPipelineFactory(
        EnemyAudioExecutionMode mode,
        IGameplayEnemyAudioPlaybackPort playbackPort,
        EnemyAudioExecutionGuard executionGuard);

    internal sealed class GameplayEnemyAudioPlaybackPortAdapter : IGameplayEnemyAudioPlaybackPort
    {
        private readonly EnemyAudioPresentationController _controller;

        public GameplayEnemyAudioPlaybackPortAdapter(EnemyAudioPresentationController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public bool TryPlayEnemyAudio(
            in GameplayEnemyAudioPlaybackRequest request,
            out GameplayEnemyAudioPlaybackResult result)
        {
            return _controller.TryPlayBridgeRequest(request, out result);
        }

        public void ResetSession()
        {
        }

        public void HardCleanup()
        {
        }
    }

    internal sealed class GameplayEnemyAudioPresentationExecutor : IPresentationExecutor
    {
        private readonly IGameplayEnemyAudioPlaybackPort _playbackPort;
        private readonly EnemyAudioExecutionMode _mode;
        private readonly EnemyAudioExecutionGuard _executionGuard;

        public GameplayEnemyAudioPresentationExecutor(
            IGameplayEnemyAudioPlaybackPort playbackPort = null,
            EnemyAudioExecutionMode mode = EnemyAudioExecutionMode.LegacyEnemyAudioController,
            EnemyAudioExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
            _executionGuard = executionGuard;
        }

        public GameplayEnemyAudioExecutorDiagnostics Diagnostics { get; private set; }

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
            var ownerViewMissingCount = 0;
            var authoringMissingCount = 0;
            var profileMissingCount = 0;
            var bindingMissingCount = 0;
            var unsupportedSemanticCount = 0;
            var unsupportedLoopSemanticCount = 0;
            var portMissingCount = 0;
            var duplicateSuppressedCount = 0;
            var legacyOwnerNoOpCount = 0;
            var requestPlannedCount = 0;
            var playbackRequestedCount = 0;
            var playbackSucceededCount = 0;
            var optionalProfileEntryMissingNoOpCount = 0;
            var lastTickIndex = 0;
            var lastCueKey = PresentationEnemyAudioCueKey.None;
            var lastDedupeKey = 0;
            var lastOwnerEntityId = 0;
            var lastOriginKind = PresentationEnemyAudioOriginKind.None;
            var lastPhase = PresentationEnemyAudioPhase.None;
            var lastFailureReason = EnemyAudioTelemetryFailureReason.None;

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var playbackCue = plan.Cues[i];
                var cue = playbackCue.Cue;
                if (!IsEnemyAudioCue(cue))
                {
                    continue;
                }

                observedCueCount++;
                CaptureLastCue(
                    playbackCue,
                    ref lastTickIndex,
                    ref lastCueKey,
                    ref lastDedupeKey,
                    ref lastOwnerEntityId,
                    ref lastOriginKind,
                    ref lastPhase);
                if (_mode != EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge)
                {
                    legacyOwnerNoOpCount++;
                    lastFailureReason = EnemyAudioTelemetryFailureReason.LegacyOwnerActive;
                    continue;
                }

                if (!TryCreateRequest(cue, i, out var request, out var missingKind))
                {
                    RecordResult(
                        missingKind,
                        ref ownerViewMissingCount,
                        ref authoringMissingCount,
                        ref profileMissingCount,
                        ref bindingMissingCount,
                        ref unsupportedSemanticCount,
                        ref unsupportedLoopSemanticCount,
                        ref portMissingCount,
                        ref legacyOwnerNoOpCount,
                        ref playbackSucceededCount,
                        ref optionalProfileEntryMissingNoOpCount);
                    lastFailureReason = ToTelemetryFailureReason(missingKind);
                    continue;
                }

                requestPlannedCount++;
                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateSuppressedCount++;
                        lastFailureReason = EnemyAudioTelemetryFailureReason.DuplicateSuppressed;
                    }
                    else
                    {
                        legacyOwnerNoOpCount++;
                        lastFailureReason = EnemyAudioTelemetryFailureReason.LegacyOwnerActive;
                    }

                    continue;
                }

                if (_playbackPort == null)
                {
                    portMissingCount++;
                    lastFailureReason = EnemyAudioTelemetryFailureReason.PortMissing;
                    continue;
                }

                playbackRequestedCount++;
                _playbackPort.TryPlayEnemyAudio(request, out var result);
                RecordResult(
                    result.Kind,
                    ref ownerViewMissingCount,
                    ref authoringMissingCount,
                    ref profileMissingCount,
                    ref bindingMissingCount,
                    ref unsupportedSemanticCount,
                    ref unsupportedLoopSemanticCount,
                    ref portMissingCount,
                    ref legacyOwnerNoOpCount,
                    ref playbackSucceededCount,
                    ref optionalProfileEntryMissingNoOpCount);
                lastFailureReason = ToTelemetryFailureReason(result.Kind);
            }

            Diagnostics = new GameplayEnemyAudioExecutorDiagnostics(
                observedCueCount,
                ownerViewMissingCount,
                authoringMissingCount,
                profileMissingCount,
                bindingMissingCount,
                unsupportedSemanticCount,
                unsupportedLoopSemanticCount,
                portMissingCount,
                duplicateSuppressedCount,
                legacyOwnerNoOpCount,
                requestPlannedCount,
                playbackRequestedCount,
                playbackSucceededCount,
                optionalProfileEntryMissingNoOpCount,
                lastTickIndex,
                lastCueKey,
                lastDedupeKey,
                lastOwnerEntityId,
                lastOriginKind,
                lastPhase,
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
            Diagnostics = new GameplayEnemyAudioExecutorDiagnostics(
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
                lastCleanupReason: EnemyAudioTelemetryCleanupReason.ResetSession);
            _executionGuard?.ResetSession();
            _playbackPort?.ResetSession();
        }

        public void HardCleanup()
        {
            Diagnostics = new GameplayEnemyAudioExecutorDiagnostics(
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
                lastCleanupReason: EnemyAudioTelemetryCleanupReason.HardCleanupPresentationExtensions);
            _executionGuard?.ResetSession();
            _playbackPort?.HardCleanup();
        }

        private bool TryClaimExecution(in EnemyAudioPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    EnemyAudioExecutionOwner.OrchestrationEnemyAudioBridge,
                    key);
            }

            return _mode == EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge;
        }

        private static bool IsEnemyAudioCue(PresentationCue cue)
        {
            return cue.Domain == PresentationDomain.EnemyAudio &&
                   cue.Key.Domain == PresentationDomain.EnemyAudio &&
                   cue.Key.LocalKey > 0;
        }

        private static bool TryCreateRequest(
            PresentationCue cue,
            int orderIndex,
            out GameplayEnemyAudioPlaybackRequest request,
            out GameplayEnemyAudioPlaybackResultKind missingKind)
        {
            request = default;
            missingKind = GameplayEnemyAudioPlaybackResultKind.None;
            if (!cue.Key.TryGetEnemyAudioCueKey(out var cueKey) ||
                !Enum.IsDefined(typeof(EnemyAudioCue), (int)cueKey))
            {
                missingKind = GameplayEnemyAudioPlaybackResultKind.UnsupportedSemantic;
                return false;
            }

            if (cueKey == PresentationEnemyAudioCueKey.ChargeActiveLoop)
            {
                missingKind = GameplayEnemyAudioPlaybackResultKind.UnsupportedLoopSemantic;
                return false;
            }

            if (!cue.EnemyAudioPayload.IsValid ||
                cue.EnemyAudioPayload.OwnerEntityId <= 0)
            {
                missingKind = GameplayEnemyAudioPlaybackResultKind.OwnerViewMissing;
                return false;
            }

            var payload = cue.EnemyAudioPayload;
            var key = new EnemyAudioPlaybackKey(
                cue.Source.TickIndex,
                cue.Source.SemanticSource,
                payload.OwnerEntityId,
                cueKey,
                payload.OriginKind,
                payload.Phase,
                payload.SourceSequenceId,
                payload.TargetEntityId,
                payload.ImpactTick,
                payload.ImpactId,
                payload.PresentationKey,
                orderIndex);
            var enemyCue = (EnemyAudioCue)(int)cueKey;
            request = new GameplayEnemyAudioPlaybackRequest(
                key,
                cueKey,
                enemyCue,
                cue.Source,
                cue.Target,
                cue.Anchor,
                payload,
                new AudioPlaybackContext(
                    ownerEntityId: payload.OwnerEntityId,
                    debugTag: EnemyAudioCueCatalog.Format(enemyCue)),
                delaySeconds: 0f);
            return true;
        }

        private static void RecordResult(
            GameplayEnemyAudioPlaybackResultKind resultKind,
            ref int ownerViewMissingCount,
            ref int authoringMissingCount,
            ref int profileMissingCount,
            ref int bindingMissingCount,
            ref int unsupportedSemanticCount,
            ref int unsupportedLoopSemanticCount,
            ref int portMissingCount,
            ref int legacyOwnerNoOpCount,
            ref int playbackSucceededCount,
            ref int optionalProfileEntryMissingNoOpCount)
        {
            switch (resultKind)
            {
                case GameplayEnemyAudioPlaybackResultKind.OwnerViewMissing:
                    ownerViewMissingCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.AuthoringMissing:
                    authoringMissingCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.ProfileMissing:
                    profileMissingCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.BindingMissing:
                    bindingMissingCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.UnsupportedSemantic:
                    unsupportedSemanticCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.UnsupportedLoopSemantic:
                    unsupportedLoopSemanticCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.PortMissing:
                    portMissingCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.OptionalProfileEntryMissing:
                    optionalProfileEntryMissingNoOpCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.Succeeded:
                    playbackSucceededCount++;
                    break;
                case GameplayEnemyAudioPlaybackResultKind.LegacyOwnerActive:
                    legacyOwnerNoOpCount++;
                    break;
            }
        }

        private static void CaptureLastCue(
            in PresentationPlaybackCue playbackCue,
            ref int lastTickIndex,
            ref PresentationEnemyAudioCueKey lastCueKey,
            ref int lastDedupeKey,
            ref int lastOwnerEntityId,
            ref PresentationEnemyAudioOriginKind lastOriginKind,
            ref PresentationEnemyAudioPhase lastPhase)
        {
            var cue = playbackCue.Cue;
            lastTickIndex = cue.Source.TickIndex;
            lastDedupeKey = playbackCue.Policy.DedupeKey;
            lastOwnerEntityId = cue.EnemyAudioPayload.OwnerEntityId;
            lastOriginKind = cue.EnemyAudioPayload.OriginKind;
            lastPhase = cue.EnemyAudioPayload.Phase;
            lastCueKey = cue.Key.TryGetEnemyAudioCueKey(out var cueKey)
                ? cueKey
                : PresentationEnemyAudioCueKey.None;
        }

        private static EnemyAudioTelemetryFailureReason ToTelemetryFailureReason(
            GameplayEnemyAudioPlaybackResultKind resultKind)
        {
            switch (resultKind)
            {
                case GameplayEnemyAudioPlaybackResultKind.OwnerViewMissing:
                    return EnemyAudioTelemetryFailureReason.OwnerViewMissing;
                case GameplayEnemyAudioPlaybackResultKind.AuthoringMissing:
                    return EnemyAudioTelemetryFailureReason.AuthoringMissing;
                case GameplayEnemyAudioPlaybackResultKind.ProfileMissing:
                    return EnemyAudioTelemetryFailureReason.ProfileMissing;
                case GameplayEnemyAudioPlaybackResultKind.BindingMissing:
                    return EnemyAudioTelemetryFailureReason.BindingMissing;
                case GameplayEnemyAudioPlaybackResultKind.UnsupportedSemantic:
                    return EnemyAudioTelemetryFailureReason.UnsupportedSemantic;
                case GameplayEnemyAudioPlaybackResultKind.UnsupportedLoopSemantic:
                    return EnemyAudioTelemetryFailureReason.UnsupportedLoopSemantic;
                case GameplayEnemyAudioPlaybackResultKind.PortMissing:
                    return EnemyAudioTelemetryFailureReason.PortMissing;
                case GameplayEnemyAudioPlaybackResultKind.OptionalProfileEntryMissing:
                    return EnemyAudioTelemetryFailureReason.OptionalProfileEntryMissing;
                case GameplayEnemyAudioPlaybackResultKind.LegacyOwnerActive:
                    return EnemyAudioTelemetryFailureReason.LegacyOwnerActive;
                default:
                    return EnemyAudioTelemetryFailureReason.None;
            }
        }

        private static EnemyAudioExecutionMode NormalizeMode(EnemyAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyAudioExecutionMode), mode)
                ? mode
                : EnemyAudioExecutionMode.LegacyEnemyAudioController;
        }
    }
}
