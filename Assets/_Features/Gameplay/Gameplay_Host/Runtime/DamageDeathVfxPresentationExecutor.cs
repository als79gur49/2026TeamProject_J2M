using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Feature.Gameplay.Vfx;

namespace Game.Feature.Gameplay.Host
{
    public enum DamageDeathVfxExecutionMode
    {
        LegacyExtension = 0,
        OrchestrationExecutor = 1,
    }

    internal enum DamageDeathVfxExecutionOwner
    {
        None = 0,
        LegacyExtension = 1,
        OrchestrationExecutor = 2,
    }

    internal enum GameplayVfxPlaybackResultKind
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        Requested = 4,
        Succeeded = 5,
        LegacyOwnerActive = 6,
    }

    internal enum DamageDeathVfxSuppressionReason
    {
        None = 0,
        LegacyOwnerSkippedByPolicy = 1,
        Duplicate = 2,
        SameTickDamageHitSuppressedByDeath = 3,
        TargetMissing = 4,
        AnchorMissing = 5,
        BindingMissing = 6,
        PortMissing = 7,
    }

    internal readonly struct DamageDeathVfxPlaybackKey : IEquatable<DamageDeathVfxPlaybackKey>
    {
        public DamageDeathVfxPlaybackKey(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int sourceEntityId,
            int targetEntityId,
            PresentationVfxCueKey cueKey)
        {
            TickIndex = Math.Max(0, tickIndex);
            SemanticSource = semanticSource;
            SourceEntityId = Math.Max(0, sourceEntityId);
            TargetEntityId = Math.Max(0, targetEntityId);
            CueKey = cueKey;
        }

        public int TickIndex { get; }

        public PresentationSemanticSource SemanticSource { get; }

        public int SourceEntityId { get; }

        public int TargetEntityId { get; }

        public PresentationVfxCueKey CueKey { get; }

        public bool Equals(DamageDeathVfxPlaybackKey other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   SourceEntityId == other.SourceEntityId &&
                   TargetEntityId == other.TargetEntityId &&
                   CueKey == other.CueKey;
        }

        public override bool Equals(object obj)
        {
            return obj is DamageDeathVfxPlaybackKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)SemanticSource;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ (int)CueKey;
                return hash;
            }
        }
    }

    internal readonly struct DamageDeathVfxOwnershipDiagnostics
    {
        public DamageDeathVfxOwnershipDiagnostics(
            DamageDeathVfxExecutionMode mode,
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            DamageDeathVfxExecutionOwner lastExecutionOwner)
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

        public DamageDeathVfxExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public DamageDeathVfxExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class DamageDeathVfxExecutionGuard
    {
        private readonly HashSet<DamageDeathVfxPlaybackKey> _claimedKeys = new();
        private DamageDeathVfxExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private DamageDeathVfxExecutionOwner _lastExecutionOwner;

        public DamageDeathVfxExecutionGuard(
            DamageDeathVfxExecutionMode mode = DamageDeathVfxExecutionMode.LegacyExtension)
        {
            _mode = NormalizeMode(mode);
        }

        public DamageDeathVfxOwnershipDiagnostics Diagnostics =>
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

        public void Configure(DamageDeathVfxExecutionMode mode)
        {
            _mode = NormalizeMode(mode);
        }

        public void RecordSkippedByPolicy(DamageDeathVfxExecutionOwner skippedOwner)
        {
            if (skippedOwner == DamageDeathVfxExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOwner), "VFX execution owner must be explicit.");
            }

            RecordAttempt(skippedOwner);
            RecordPolicySkip(skippedOwner);
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
            _lastExecutionOwner = DamageDeathVfxExecutionOwner.None;
        }

        public bool TryBeginExecution(
            DamageDeathVfxExecutionOwner owner,
            in DamageDeathVfxPlaybackKey key)
        {
            if (owner == DamageDeathVfxExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "VFX execution owner must be explicit.");
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
            if (owner == DamageDeathVfxExecutionOwner.LegacyExtension)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static DamageDeathVfxExecutionMode NormalizeMode(DamageDeathVfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(DamageDeathVfxExecutionMode), mode)
                ? mode
                : DamageDeathVfxExecutionMode.LegacyExtension;
        }

        private bool IsOwnerAllowed(DamageDeathVfxExecutionOwner owner)
        {
            return (_mode == DamageDeathVfxExecutionMode.LegacyExtension &&
                    owner == DamageDeathVfxExecutionOwner.LegacyExtension) ||
                   (_mode == DamageDeathVfxExecutionMode.OrchestrationExecutor &&
                    owner == DamageDeathVfxExecutionOwner.OrchestrationExecutor);
        }

        private void RecordAttempt(DamageDeathVfxExecutionOwner owner)
        {
            if (owner == DamageDeathVfxExecutionOwner.LegacyExtension)
            {
                _legacyAttemptCount++;
            }
            else if (owner == DamageDeathVfxExecutionOwner.OrchestrationExecutor)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(DamageDeathVfxExecutionOwner owner)
        {
            if (owner == DamageDeathVfxExecutionOwner.LegacyExtension)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (owner == DamageDeathVfxExecutionOwner.OrchestrationExecutor)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
            }
        }
    }

    internal readonly struct GameplayVfxPlaybackRequest
    {
        public GameplayVfxPlaybackRequest(
            DamageDeathVfxPlaybackKey ownershipKey,
            PresentationVfxCueKey cueKey,
            GameplayVfxCueId cueId,
            int tickIndex,
            int sequenceId,
            int presentationSeed,
            int sourceEntityId,
            PresentationTarget target,
            PresentationAnchor presentationAnchor,
            VfxAnchor vfxAnchor)
        {
            OwnershipKey = ownershipKey;
            CueKey = cueKey;
            CueId = cueId;
            TickIndex = Math.Max(0, tickIndex);
            SequenceId = Math.Max(0, sequenceId);
            PresentationSeed = Math.Max(0, presentationSeed);
            SourceEntityId = Math.Max(0, sourceEntityId);
            Target = target;
            PresentationAnchor = presentationAnchor;
            VfxAnchor = vfxAnchor;
        }

        public DamageDeathVfxPlaybackKey OwnershipKey { get; }

        public PresentationVfxCueKey CueKey { get; }

        public GameplayVfxCueId CueId { get; }

        public int TickIndex { get; }

        public int SequenceId { get; }

        public int PresentationSeed { get; }

        public int SourceEntityId { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor PresentationAnchor { get; }

        public VfxAnchor VfxAnchor { get; }
    }

    internal readonly struct GameplayVfxPlaybackResult
    {
        public GameplayVfxPlaybackResult(GameplayVfxPlaybackResultKind kind)
        {
            Kind = kind;
        }

        public GameplayVfxPlaybackResultKind Kind { get; }
    }

    internal readonly struct DamageDeathVfxExecutorDiagnostics
    {
        private static readonly IReadOnlyList<DamageDeathVfxSemanticDiagnostics> EmptySemanticDiagnostics =
            Array.Empty<DamageDeathVfxSemanticDiagnostics>();

        public DamageDeathVfxExecutorDiagnostics(
            int observedCueCount,
            int legacyOwnerNoOpCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int duplicateSuppressedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int missingPortCount)
            : this(
                DamageDeathVfxExecutionMode.LegacyExtension,
                false,
                legacyOwnerNoOpCount,
                observedCueCount,
                legacyOwnerNoOpCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                duplicateSuppressedCount,
                playbackRequestedCount,
                playbackSucceededCount,
                missingPortCount,
                damageCuePlannedCount: 0,
                deathCuePlannedCount: 0,
                damagePlaybackRequestedCount: 0,
                deathPlaybackRequestedCount: 0,
                sameTickDamageHitSuppressedByDeathCount: 0,
                cleanupRequestedCount: 0,
                cleanupSucceededCount: 0,
                lastTickIndex: 0,
                lastCueKey: PresentationVfxCueKey.None,
                lastTargetEntityId: 0,
                lastSuppressionReason: DamageDeathVfxSuppressionReason.None,
                semanticDiagnostics: EmptySemanticDiagnostics)
        {
        }

        public DamageDeathVfxExecutorDiagnostics(
            DamageDeathVfxExecutionMode currentMode,
            bool isProductionDefaultOwner,
            int legacyOwnerSkippedByPolicyCount,
            int observedCueCount,
            int legacyOwnerNoOpCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int duplicateSuppressedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int missingPortCount,
            int damageCuePlannedCount,
            int deathCuePlannedCount,
            int damagePlaybackRequestedCount,
            int deathPlaybackRequestedCount,
            int sameTickDamageHitSuppressedByDeathCount,
            int cleanupRequestedCount,
            int cleanupSucceededCount,
            int lastTickIndex,
            PresentationVfxCueKey lastCueKey,
            int lastTargetEntityId,
            DamageDeathVfxSuppressionReason lastSuppressionReason,
            IReadOnlyList<DamageDeathVfxSemanticDiagnostics> semanticDiagnostics)
        {
            CurrentMode = Enum.IsDefined(typeof(DamageDeathVfxExecutionMode), currentMode)
                ? currentMode
                : DamageDeathVfxExecutionMode.LegacyExtension;
            IsProductionDefaultOwner = isProductionDefaultOwner;
            LegacyOwnerSkippedByPolicyCount = Math.Max(0, legacyOwnerSkippedByPolicyCount);
            ObservedCueCount = Math.Max(0, observedCueCount);
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            PortMissingCount = MissingPortCount;
            DamageCuePlannedCount = Math.Max(0, damageCuePlannedCount);
            DeathCuePlannedCount = Math.Max(0, deathCuePlannedCount);
            DamagePlaybackRequestedCount = Math.Max(0, damagePlaybackRequestedCount);
            DeathPlaybackRequestedCount = Math.Max(0, deathPlaybackRequestedCount);
            SameTickDamageHitSuppressedByDeathCount = Math.Max(0, sameTickDamageHitSuppressedByDeathCount);
            CleanupRequestedCount = Math.Max(0, cleanupRequestedCount);
            CleanupSucceededCount = Math.Max(0, cleanupSucceededCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastTargetEntityId = Math.Max(0, lastTargetEntityId);
            LastSuppressionReason = lastSuppressionReason;
            SemanticDiagnostics = semanticDiagnostics ?? EmptySemanticDiagnostics;
        }

        public DamageDeathVfxExecutionMode CurrentMode { get; }

        public bool IsProductionDefaultOwner { get; }

        public int LegacyOwnerSkippedByPolicyCount { get; }

        public int ObservedCueCount { get; }

        public int LegacyOwnerNoOpCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int MissingPortCount { get; }

        public int PortMissingCount { get; }

        public int DamageCuePlannedCount { get; }

        public int DeathCuePlannedCount { get; }

        public int DamagePlaybackRequestedCount { get; }

        public int DeathPlaybackRequestedCount { get; }

        public int SameTickDamageHitSuppressedByDeathCount { get; }

        public int CleanupRequestedCount { get; }

        public int CleanupSucceededCount { get; }

        public int LastTickIndex { get; }

        public PresentationVfxCueKey LastCueKey { get; }

        public int LastTargetEntityId { get; }

        public DamageDeathVfxSuppressionReason LastSuppressionReason { get; }

        public IReadOnlyList<DamageDeathVfxSemanticDiagnostics> SemanticDiagnostics { get; }
    }

    internal readonly struct DamageDeathVfxSemanticDiagnostics
    {
        public DamageDeathVfxSemanticDiagnostics(
            PresentationVfxCueKey cueKey,
            int plannedCount,
            int requestedCount,
            int succeededCount,
            int duplicateSuppressedCount,
            int lastDedupeKey,
            int lastTargetEntityId,
            PresentationAnchorKind lastAnchorKind)
        {
            CueKey = cueKey;
            PlannedCount = Math.Max(0, plannedCount);
            RequestedCount = Math.Max(0, requestedCount);
            SucceededCount = Math.Max(0, succeededCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            LastDedupeKey = Math.Max(0, lastDedupeKey);
            LastTargetEntityId = Math.Max(0, lastTargetEntityId);
            LastAnchorKind = lastAnchorKind;
        }

        public PresentationVfxCueKey CueKey { get; }

        public int PlannedCount { get; }

        public int RequestedCount { get; }

        public int SucceededCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int LastDedupeKey { get; }

        public int LastTargetEntityId { get; }

        public PresentationAnchorKind LastAnchorKind { get; }
    }

    internal sealed class DamageDeathVfxSemanticTelemetryCounter
    {
        public int PlannedCount;
        public int RequestedCount;
        public int SucceededCount;
        public int DuplicateSuppressedCount;
        public int LastDedupeKey;
        public int LastTargetEntityId;
        public PresentationAnchorKind LastAnchorKind;

        public DamageDeathVfxSemanticDiagnostics ToDiagnostics(PresentationVfxCueKey cueKey)
        {
            return new DamageDeathVfxSemanticDiagnostics(
                cueKey,
                PlannedCount,
                RequestedCount,
                SucceededCount,
                DuplicateSuppressedCount,
                LastDedupeKey,
                LastTargetEntityId,
                LastAnchorKind);
        }
    }

    internal static class DamageDeathVfxProductionTelemetryBuilder
    {
        public static DamageDeathVfxExecutorDiagnostics ResolveExecutorDiagnostics(
            GameplayPresentationPipeline pipeline)
        {
            if (pipeline == null)
            {
                return default;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayVfxPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        public static void RecordSameTickDamageHitSuppressedByDeath(
            GameplayPresentationPipeline pipeline,
            int suppressedByDeathCount)
        {
            if (pipeline == null || suppressedByDeathCount <= 0)
            {
                return;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayVfxPresentationExecutor executor)
                {
                    executor.RecordSameTickDamageHitSuppressedByDeath(suppressedByDeathCount);
                }
            }
        }
    }

    internal interface IDamageDeathVfxPlaybackPort
    {
        bool TryPlayDamageDeathVfx(
            in GameplayVfxPlaybackRequest request,
            out GameplayVfxPlaybackResult result);

        void UpdatePresentation(float deltaTime);

        void ResetSession();

        void HardCleanup();
    }

    internal delegate GameplayPresentationPipeline DamageDeathVfxExecutionPipelineFactory(
        DamageDeathVfxExecutionMode mode,
        IDamageDeathVfxPlaybackPort playbackPort,
        DamageDeathVfxExecutionGuard executionGuard);

    internal sealed class DamageDeathGameplayVfxPlaybackPortAdapter : IDamageDeathVfxPlaybackPort
    {
        private readonly IGameplayVfxPlaybackPort _playbackPort;

        public DamageDeathGameplayVfxPlaybackPortAdapter(IGameplayVfxPlaybackPort playbackPort)
        {
            _playbackPort = playbackPort;
        }

        public bool TryPlayDamageDeathVfx(
            in GameplayVfxPlaybackRequest request,
            out GameplayVfxPlaybackResult result)
        {
            if (_playbackPort == null)
            {
                result = new GameplayVfxPlaybackResult(GameplayVfxPlaybackResultKind.BindingMissing);
                return false;
            }

            var gameplayRequest = new GameplayVfxRequest(
                tickIndex: request.TickIndex,
                sequenceId: request.SequenceId,
                presentationSeed: request.PresentationSeed,
                sourceEntityId: request.SourceEntityId,
                cueId: request.CueId,
                anchor: request.VfxAnchor,
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);
            _playbackPort.Play(gameplayRequest);
            result = new GameplayVfxPlaybackResult(GameplayVfxPlaybackResultKind.Succeeded);
            return true;
        }

        public void UpdatePresentation(float deltaTime)
        {
        }

        public void ResetSession()
        {
        }

        public void HardCleanup()
        {
        }
    }

    internal sealed class GameplayVfxPresentationExecutor : IPresentationVfxExecutor
    {
        private readonly IDamageDeathVfxPlaybackPort _playbackPort;
        private readonly DamageDeathVfxExecutionMode _mode;
        private readonly DamageDeathVfxExecutionGuard _executionGuard;
        private readonly Dictionary<PresentationVfxCueKey, DamageDeathVfxSemanticTelemetryCounter> _semanticCounters =
            new();
        private bool _hasRoutedRequest;
        private int _observedCueCount;
        private int _legacyOwnerNoOpCount;
        private int _targetMissingCount;
        private int _anchorMissingCount;
        private int _bindingMissingCount;
        private int _duplicateSuppressedCount;
        private int _playbackRequestedCount;
        private int _playbackSucceededCount;
        private int _missingPortCount;
        private int _damageCuePlannedCount;
        private int _deathCuePlannedCount;
        private int _damagePlaybackRequestedCount;
        private int _deathPlaybackRequestedCount;
        private int _sameTickDamageHitSuppressedByDeathCount;
        private int _cleanupRequestedCount;
        private int _cleanupSucceededCount;
        private int _lastTickIndex;
        private PresentationVfxCueKey _lastCueKey;
        private int _lastTargetEntityId;
        private DamageDeathVfxSuppressionReason _lastSuppressionReason;

        public GameplayVfxPresentationExecutor(
            IDamageDeathVfxPlaybackPort playbackPort = null,
            DamageDeathVfxExecutionMode mode = DamageDeathVfxExecutionMode.LegacyExtension,
            DamageDeathVfxExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
            _executionGuard = executionGuard;
        }

        public DamageDeathVfxExecutorDiagnostics Diagnostics { get; private set; }

        public void RecordSameTickDamageHitSuppressedByDeath(int count)
        {
            if (count <= 0)
            {
                return;
            }

            _sameTickDamageHitSuppressedByDeathCount += count;
            _lastSuppressionReason = DamageDeathVfxSuppressionReason.SameTickDamageHitSuppressedByDeath;
            RefreshDiagnostics();
        }

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
            var duplicateSuppressedCount = 0;
            var playbackRequestedCount = 0;
            var playbackSucceededCount = 0;
            var missingPortCount = 0;

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var cue = plan.Cues[i].Cue;
                if (!IsDamageDeathVfxCue(cue))
                {
                    continue;
                }

                observedCueCount++;
                if (cue.Key.TryGetVfxCueKey(out var observedCueKey))
                {
                    RecordPlannedCue(
                        observedCueKey,
                        plan.Cues[i].Policy.DedupeKey,
                        cue.Source.TickIndex,
                        cue.Target.EntityId,
                        cue.Anchor.Kind);
                }

                if (_mode != DamageDeathVfxExecutionMode.OrchestrationExecutor)
                {
                    legacyOwnerNoOpCount++;
                    _lastSuppressionReason = DamageDeathVfxSuppressionReason.LegacyOwnerSkippedByPolicy;
                    continue;
                }

                if (!TryCreateRequest(cue, out var request, out var missingKind))
                {
                    if (missingKind == GameplayVfxPlaybackResultKind.TargetMissing)
                    {
                        targetMissingCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.TargetMissing;
                    }
                    else if (missingKind == GameplayVfxPlaybackResultKind.BindingMissing)
                    {
                        bindingMissingCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.BindingMissing;
                    }
                    else
                    {
                        anchorMissingCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.AnchorMissing;
                    }

                    continue;
                }

                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateSuppressedCount++;
                        GetOrCreateSemanticCounter(request.CueKey).DuplicateSuppressedCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.Duplicate;
                    }
                    else
                    {
                        legacyOwnerNoOpCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.LegacyOwnerSkippedByPolicy;
                    }

                    continue;
                }

                if (_playbackPort == null)
                {
                    missingPortCount++;
                    _lastSuppressionReason = DamageDeathVfxSuppressionReason.PortMissing;
                    continue;
                }

                playbackRequestedCount++;
                RecordRequestedCue(request.CueKey);
                _hasRoutedRequest = true;
                _playbackPort.TryPlayDamageDeathVfx(request, out var result);
                switch (result.Kind)
                {
                    case GameplayVfxPlaybackResultKind.Succeeded:
                        playbackSucceededCount++;
                        GetOrCreateSemanticCounter(request.CueKey).SucceededCount++;
                        break;
                    case GameplayVfxPlaybackResultKind.BindingMissing:
                        bindingMissingCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.BindingMissing;
                        break;
                    case GameplayVfxPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.AnchorMissing;
                        break;
                    case GameplayVfxPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.TargetMissing;
                        break;
                    case GameplayVfxPlaybackResultKind.LegacyOwnerActive:
                        legacyOwnerNoOpCount++;
                        _lastSuppressionReason = DamageDeathVfxSuppressionReason.LegacyOwnerSkippedByPolicy;
                        break;
                }
            }

            _observedCueCount += observedCueCount;
            _legacyOwnerNoOpCount += legacyOwnerNoOpCount;
            _targetMissingCount += targetMissingCount;
            _anchorMissingCount += anchorMissingCount;
            _bindingMissingCount += bindingMissingCount;
            _duplicateSuppressedCount += duplicateSuppressedCount;
            _playbackRequestedCount += playbackRequestedCount;
            _playbackSucceededCount += playbackSucceededCount;
            _missingPortCount += missingPortCount;
            RefreshDiagnostics();
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_mode == DamageDeathVfxExecutionMode.OrchestrationExecutor &&
                _hasRoutedRequest)
            {
                _playbackPort?.UpdatePresentation(deltaTime);
            }
        }

        public void ResetSession()
        {
            _hasRoutedRequest = false;
            ClearTelemetry();
            _executionGuard?.ResetSession();
            if (_mode == DamageDeathVfxExecutionMode.OrchestrationExecutor)
            {
                _cleanupRequestedCount++;
                _playbackPort?.ResetSession();
                _cleanupSucceededCount++;
            }
            RefreshDiagnostics();
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            ClearTelemetry();
            _executionGuard?.ResetSession();
            if (_mode == DamageDeathVfxExecutionMode.OrchestrationExecutor)
            {
                _cleanupRequestedCount++;
                _playbackPort?.HardCleanup();
                _cleanupSucceededCount++;
            }
            RefreshDiagnostics();
        }

        private bool TryClaimExecution(in DamageDeathVfxPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    DamageDeathVfxExecutionOwner.OrchestrationExecutor,
                    key);
            }

            return _mode == DamageDeathVfxExecutionMode.OrchestrationExecutor;
        }

        private static bool IsDamageDeathVfxCue(PresentationCue cue)
        {
            return cue.Domain == PresentationDomain.Vfx &&
                   cue.Key.TryGetVfxCueKey(out var cueKey) &&
                   (cueKey == PresentationVfxCueKey.DamageHit ||
                    cueKey == PresentationVfxCueKey.EnemyDeath);
        }

        private void RecordPlannedCue(
            PresentationVfxCueKey cueKey,
            int dedupeKey,
            int tickIndex,
            int targetEntityId,
            PresentationAnchorKind anchorKind)
        {
            var counter = GetOrCreateSemanticCounter(cueKey);
            counter.PlannedCount++;
            counter.LastDedupeKey = dedupeKey;
            counter.LastTargetEntityId = Math.Max(0, targetEntityId);
            counter.LastAnchorKind = anchorKind;
            _lastTickIndex = Math.Max(0, tickIndex);
            _lastCueKey = cueKey;
            _lastTargetEntityId = Math.Max(0, targetEntityId);
            if (cueKey == PresentationVfxCueKey.DamageHit)
            {
                _damageCuePlannedCount++;
            }
            else if (cueKey == PresentationVfxCueKey.EnemyDeath)
            {
                _deathCuePlannedCount++;
            }
        }

        private void RecordRequestedCue(PresentationVfxCueKey cueKey)
        {
            var counter = GetOrCreateSemanticCounter(cueKey);
            counter.RequestedCount++;
            if (cueKey == PresentationVfxCueKey.DamageHit)
            {
                _damagePlaybackRequestedCount++;
            }
            else if (cueKey == PresentationVfxCueKey.EnemyDeath)
            {
                _deathPlaybackRequestedCount++;
            }
        }

        private DamageDeathVfxSemanticTelemetryCounter GetOrCreateSemanticCounter(PresentationVfxCueKey cueKey)
        {
            if (!_semanticCounters.TryGetValue(cueKey, out var counter))
            {
                counter = new DamageDeathVfxSemanticTelemetryCounter();
                _semanticCounters.Add(cueKey, counter);
            }

            return counter;
        }

        private void RefreshDiagnostics()
        {
            Diagnostics = new DamageDeathVfxExecutorDiagnostics(
                currentMode: _mode,
                isProductionDefaultOwner: _mode == DamageDeathVfxExecutionMode.OrchestrationExecutor,
                legacyOwnerSkippedByPolicyCount: _executionGuard?.Diagnostics.SkippedLegacyBecauseExecutorOwnerCount ?? 0,
                observedCueCount: _observedCueCount,
                legacyOwnerNoOpCount: _legacyOwnerNoOpCount,
                targetMissingCount: _targetMissingCount,
                anchorMissingCount: _anchorMissingCount,
                bindingMissingCount: _bindingMissingCount,
                duplicateSuppressedCount: _duplicateSuppressedCount,
                playbackRequestedCount: _playbackRequestedCount,
                playbackSucceededCount: _playbackSucceededCount,
                missingPortCount: _missingPortCount,
                damageCuePlannedCount: _damageCuePlannedCount,
                deathCuePlannedCount: _deathCuePlannedCount,
                damagePlaybackRequestedCount: _damagePlaybackRequestedCount,
                deathPlaybackRequestedCount: _deathPlaybackRequestedCount,
                sameTickDamageHitSuppressedByDeathCount: _sameTickDamageHitSuppressedByDeathCount,
                cleanupRequestedCount: _cleanupRequestedCount,
                cleanupSucceededCount: _cleanupSucceededCount,
                lastTickIndex: _lastTickIndex,
                lastCueKey: _lastCueKey,
                lastTargetEntityId: _lastTargetEntityId,
                lastSuppressionReason: _lastSuppressionReason,
                semanticDiagnostics: BuildSemanticDiagnostics());
        }

        private IReadOnlyList<DamageDeathVfxSemanticDiagnostics> BuildSemanticDiagnostics()
        {
            if (_semanticCounters.Count == 0)
            {
                return Array.Empty<DamageDeathVfxSemanticDiagnostics>();
            }

            var diagnostics = new List<DamageDeathVfxSemanticDiagnostics>(_semanticCounters.Count);
            if (_semanticCounters.TryGetValue(PresentationVfxCueKey.DamageHit, out var damage))
            {
                diagnostics.Add(damage.ToDiagnostics(PresentationVfxCueKey.DamageHit));
            }

            if (_semanticCounters.TryGetValue(PresentationVfxCueKey.EnemyDeath, out var death))
            {
                diagnostics.Add(death.ToDiagnostics(PresentationVfxCueKey.EnemyDeath));
            }

            return diagnostics;
        }

        private void ClearTelemetry()
        {
            _semanticCounters.Clear();
            _observedCueCount = 0;
            _legacyOwnerNoOpCount = 0;
            _targetMissingCount = 0;
            _anchorMissingCount = 0;
            _bindingMissingCount = 0;
            _duplicateSuppressedCount = 0;
            _playbackRequestedCount = 0;
            _playbackSucceededCount = 0;
            _missingPortCount = 0;
            _damageCuePlannedCount = 0;
            _deathCuePlannedCount = 0;
            _damagePlaybackRequestedCount = 0;
            _deathPlaybackRequestedCount = 0;
            _sameTickDamageHitSuppressedByDeathCount = 0;
            _cleanupRequestedCount = 0;
            _cleanupSucceededCount = 0;
            _lastTickIndex = 0;
            _lastCueKey = PresentationVfxCueKey.None;
            _lastTargetEntityId = 0;
            _lastSuppressionReason = DamageDeathVfxSuppressionReason.None;
            Diagnostics = default;
        }

        private static bool TryCreateRequest(
            PresentationCue cue,
            out GameplayVfxPlaybackRequest request,
            out GameplayVfxPlaybackResultKind missingKind)
        {
            request = default;
            missingKind = GameplayVfxPlaybackResultKind.None;
            if (!cue.Key.TryGetVfxCueKey(out var cueKey) ||
                !TryMapCueId(cueKey, out var cueId))
            {
                missingKind = GameplayVfxPlaybackResultKind.BindingMissing;
                return false;
            }

            if (cue.Target.Kind != PresentationTargetKind.Entity ||
                cue.Target.EntityId <= 0)
            {
                missingKind = GameplayVfxPlaybackResultKind.TargetMissing;
                return false;
            }

            if (!TryMapAnchor(cue.Anchor, cue.Target.EntityId, out var vfxAnchor))
            {
                missingKind = GameplayVfxPlaybackResultKind.AnchorMissing;
                return false;
            }

            var key = new DamageDeathVfxPlaybackKey(
                cue.Source.TickIndex,
                cue.Source.SemanticSource,
                cue.Source.SourceEntityId,
                cue.Target.EntityId,
                cueKey);
            request = new GameplayVfxPlaybackRequest(
                key,
                cueKey,
                cueId,
                cue.Source.TickIndex,
                cue.Target.EntityId,
                cue.Source.SourceSequence > 0 ? cue.Source.SourceSequence : cue.Target.EntityId,
                cue.Source.SourceEntityId > 0 ? cue.Source.SourceEntityId : cue.Target.EntityId,
                cue.Target,
                cue.Anchor,
                vfxAnchor);
            return true;
        }

        private static bool TryMapCueId(PresentationVfxCueKey cueKey, out GameplayVfxCueId cueId)
        {
            switch (cueKey)
            {
                case PresentationVfxCueKey.DamageHit:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.Damage);
                    return true;
                case PresentationVfxCueKey.EnemyDeath:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.Death);
                    return true;
                default:
                    cueId = GameplayVfxCueId.None;
                    return false;
            }
        }

        private static bool TryMapAnchor(
            PresentationAnchor anchor,
            int targetEntityId,
            out VfxAnchor vfxAnchor)
        {
            switch (anchor.Kind)
            {
                case PresentationAnchorKind.EntityCenter:
                case PresentationAnchorKind.EntityVisualRoot:
                    vfxAnchor = VfxAnchor.ForEntity(
                        anchor.Target.EntityId > 0 ? anchor.Target.EntityId : targetEntityId,
                        VfxAnchorSlot.EntityCenter);
                    return vfxAnchor.EntityId > 0;
                case PresentationAnchorKind.SurfaceCellCenter:
                    var cell = anchor.Target.Cell;
                    vfxAnchor = VfxAnchor.ForCell(
                        cell,
                        new CubeTopologyState(cell.face),
                        VfxAnchorSlot.CellCenter);
                    return true;
                default:
                    vfxAnchor = default;
                    return false;
            }
        }

        private static DamageDeathVfxExecutionMode NormalizeMode(DamageDeathVfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(DamageDeathVfxExecutionMode), mode)
                ? mode
                : DamageDeathVfxExecutionMode.LegacyExtension;
        }
    }
}
