using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Feature.Gameplay.Vfx;

namespace Game.Feature.Gameplay.Host
{
    internal enum DamageDeathVfxExecutionOwner
    {
        None = 0,
        OrchestrationExecutor = 1,
    }

    internal enum GameplayVfxPlaybackResultKind
    {
        None = 0,
        TargetMissing = 1,
        AnchorMissing = 2,
        BindingMissing = 3,
        Requested = 4,
        Succeeded = 5,
    }

    internal enum DamageDeathVfxOmissionReason
    {
        None = 0,
        Duplicate = 1,
        SameTickDamageHitOmittedByDeath = 2,
        TargetMissing = 3,
        AnchorMissing = 4,
        BindingMissing = 5,
        PortMissing = 6,
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
            int executorAttemptCount,
            int executedByExecutorCount,
            int duplicateAttemptCount,
            DamageDeathVfxExecutionOwner lastExecutionOwner)
        {
            ExecutorAttemptCount = Math.Max(0, executorAttemptCount);
            ExecutedByExecutorCount = Math.Max(0, executedByExecutorCount);
            DuplicateAttemptCount = Math.Max(0, duplicateAttemptCount);
            LastExecutionOwner = lastExecutionOwner;
        }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int DuplicateAttemptCount { get; }

        public DamageDeathVfxExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class DamageDeathVfxExecutionGuard
    {
        private readonly HashSet<DamageDeathVfxPlaybackKey> _claimedKeys = new();
        private int _executorAttemptCount;
        private int _executedByExecutorCount;
        private int _duplicateAttemptCount;
        private DamageDeathVfxExecutionOwner _lastExecutionOwner;

        public DamageDeathVfxOwnershipDiagnostics Diagnostics =>
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
                return false;
            }

            _claimedKeys.Add(key);
            _lastExecutionOwner = owner;
            _executedByExecutorCount++;

            return true;
        }

        private void RecordAttempt(DamageDeathVfxExecutionOwner owner)
        {
            if (owner == DamageDeathVfxExecutionOwner.OrchestrationExecutor)
            {
                _executorAttemptCount++;
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
            VfxAnchor vfxAnchor,
            float delaySeconds = 0f)
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
            DelaySeconds = Math.Max(0f, delaySeconds);
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

        public float DelaySeconds { get; }
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
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int duplicateOmittedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int missingPortCount)
            : this(
                false,
                observedCueCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                duplicateOmittedCount,
                playbackRequestedCount,
                playbackSucceededCount,
                missingPortCount,
                damageCuePlannedCount: 0,
                deathCuePlannedCount: 0,
                damagePlaybackRequestedCount: 0,
                deathPlaybackRequestedCount: 0,
                sameTickDamageHitOmittedByDeathCount: 0,
                cleanupRequestedCount: 0,
                cleanupSucceededCount: 0,
                lastTickIndex: 0,
                lastCueKey: PresentationVfxCueKey.None,
                lastTargetEntityId: 0,
                lastOmissionReason: DamageDeathVfxOmissionReason.None,
                semanticDiagnostics: EmptySemanticDiagnostics)
        {
        }

        public DamageDeathVfxExecutorDiagnostics(
            bool isProductionDefaultOwner,
            int observedCueCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int duplicateOmittedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int missingPortCount,
            int damageCuePlannedCount,
            int deathCuePlannedCount,
            int damagePlaybackRequestedCount,
            int deathPlaybackRequestedCount,
            int sameTickDamageHitOmittedByDeathCount,
            int cleanupRequestedCount,
            int cleanupSucceededCount,
            int lastTickIndex,
            PresentationVfxCueKey lastCueKey,
            int lastTargetEntityId,
            DamageDeathVfxOmissionReason lastOmissionReason,
            IReadOnlyList<DamageDeathVfxSemanticDiagnostics> semanticDiagnostics)
        {
            IsProductionDefaultOwner = isProductionDefaultOwner;
            ObservedCueCount = Math.Max(0, observedCueCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DuplicateOmittedCount = Math.Max(0, duplicateOmittedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            PortMissingCount = MissingPortCount;
            DamageCuePlannedCount = Math.Max(0, damageCuePlannedCount);
            DeathCuePlannedCount = Math.Max(0, deathCuePlannedCount);
            DamagePlaybackRequestedCount = Math.Max(0, damagePlaybackRequestedCount);
            DeathPlaybackRequestedCount = Math.Max(0, deathPlaybackRequestedCount);
            SameTickDamageHitOmittedByDeathCount = Math.Max(0, sameTickDamageHitOmittedByDeathCount);
            CleanupRequestedCount = Math.Max(0, cleanupRequestedCount);
            CleanupSucceededCount = Math.Max(0, cleanupSucceededCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastTargetEntityId = Math.Max(0, lastTargetEntityId);
            LastOmissionReason = lastOmissionReason;
            SemanticDiagnostics = semanticDiagnostics ?? EmptySemanticDiagnostics;
        }

        public bool IsProductionDefaultOwner { get; }

        public int ObservedCueCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DuplicateOmittedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int MissingPortCount { get; }

        public int PortMissingCount { get; }

        public int DamageCuePlannedCount { get; }

        public int DeathCuePlannedCount { get; }

        public int DamagePlaybackRequestedCount { get; }

        public int DeathPlaybackRequestedCount { get; }

        public int SameTickDamageHitOmittedByDeathCount { get; }

        public int CleanupRequestedCount { get; }

        public int CleanupSucceededCount { get; }

        public int LastTickIndex { get; }

        public PresentationVfxCueKey LastCueKey { get; }

        public int LastTargetEntityId { get; }

        public DamageDeathVfxOmissionReason LastOmissionReason { get; }

        public IReadOnlyList<DamageDeathVfxSemanticDiagnostics> SemanticDiagnostics { get; }
    }

    internal readonly struct DamageDeathVfxSemanticDiagnostics
    {
        public DamageDeathVfxSemanticDiagnostics(
            PresentationVfxCueKey cueKey,
            int plannedCount,
            int requestedCount,
            int succeededCount,
            int duplicateOmittedCount,
            int lastDedupeKey,
            int lastTargetEntityId,
            PresentationAnchorKind lastAnchorKind)
        {
            CueKey = cueKey;
            PlannedCount = Math.Max(0, plannedCount);
            RequestedCount = Math.Max(0, requestedCount);
            SucceededCount = Math.Max(0, succeededCount);
            DuplicateOmittedCount = Math.Max(0, duplicateOmittedCount);
            LastDedupeKey = Math.Max(0, lastDedupeKey);
            LastTargetEntityId = Math.Max(0, lastTargetEntityId);
            LastAnchorKind = lastAnchorKind;
        }

        public PresentationVfxCueKey CueKey { get; }

        public int PlannedCount { get; }

        public int RequestedCount { get; }

        public int SucceededCount { get; }

        public int DuplicateOmittedCount { get; }

        public int LastDedupeKey { get; }

        public int LastTargetEntityId { get; }

        public PresentationAnchorKind LastAnchorKind { get; }
    }

    internal sealed class DamageDeathVfxSemanticTelemetryCounter
    {
        public int PlannedCount;
        public int RequestedCount;
        public int SucceededCount;
        public int DuplicateOmittedCount;
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
                DuplicateOmittedCount,
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

        public static void RecordSameTickDamageHitOmittedByDeath(
            GameplayPresentationPipeline pipeline,
            int omittedByDeathCount)
        {
            if (pipeline == null || omittedByDeathCount <= 0)
            {
                return;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayVfxPresentationExecutor executor)
                {
                    executor.RecordSameTickDamageHitOmittedByDeath(omittedByDeathCount);
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

    internal interface IDamageDeathGameplayVfxPlaybackRuntime
    {
        bool TryPlayDamageDeathVfx(
            in GameplayVfxRequest request,
            out GameplayVfxPlaybackResult result);
    }

    internal delegate GameplayPresentationPipeline DamageDeathVfxExecutionPipelineFactory(
        IDamageDeathVfxPlaybackPort playbackPort,
        DamageDeathVfxExecutionGuard executionGuard);

    internal sealed class DamageDeathGameplayVfxPlaybackPortAdapter : IDamageDeathVfxPlaybackPort
    {
        private readonly IDamageDeathGameplayVfxPlaybackRuntime _runtime;

        public DamageDeathGameplayVfxPlaybackPortAdapter(IDamageDeathGameplayVfxPlaybackRuntime runtime)
        {
            _runtime = runtime;
        }

        public bool TryPlayDamageDeathVfx(
            in GameplayVfxPlaybackRequest request,
            out GameplayVfxPlaybackResult result)
        {
            if (_runtime == null)
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
                timing: request.DelaySeconds > 0f
                    ? VfxTimingKind.Delayed
                    : VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None,
                delaySeconds: request.DelaySeconds);
            return _runtime.TryPlayDamageDeathVfx(gameplayRequest, out result);
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
        private readonly DamageDeathVfxExecutionGuard _executionGuard;
        private readonly GameplayTimingProfile _timingProfile;
        private readonly Dictionary<PresentationVfxCueKey, DamageDeathVfxSemanticTelemetryCounter> _semanticCounters =
            new();
        private bool _hasRoutedRequest;
        private int _observedCueCount;
        private int _targetMissingCount;
        private int _anchorMissingCount;
        private int _bindingMissingCount;
        private int _duplicateOmittedCount;
        private int _playbackRequestedCount;
        private int _playbackSucceededCount;
        private int _missingPortCount;
        private int _damageCuePlannedCount;
        private int _deathCuePlannedCount;
        private int _damagePlaybackRequestedCount;
        private int _deathPlaybackRequestedCount;
        private int _sameTickDamageHitOmittedByDeathCount;
        private int _cleanupRequestedCount;
        private int _cleanupSucceededCount;
        private int _lastTickIndex;
        private PresentationVfxCueKey _lastCueKey;
        private int _lastTargetEntityId;
        private DamageDeathVfxOmissionReason _lastOmissionReason;

        public GameplayVfxPresentationExecutor(
            IDamageDeathVfxPlaybackPort playbackPort = null,
            DamageDeathVfxExecutionGuard executionGuard = null,
            GameplayTimingProfile timingProfile = null)
        {
            _playbackPort = playbackPort;
            _executionGuard = executionGuard;
            _timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
        }

        public DamageDeathVfxExecutorDiagnostics Diagnostics { get; private set; }

        public void RecordSameTickDamageHitOmittedByDeath(int count)
        {
            if (count <= 0)
            {
                return;
            }

            _sameTickDamageHitOmittedByDeathCount += count;
            _lastOmissionReason = DamageDeathVfxOmissionReason.SameTickDamageHitOmittedByDeath;
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
            var targetMissingCount = 0;
            var anchorMissingCount = 0;
            var bindingMissingCount = 0;
            var duplicateOmittedCount = 0;
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

                if (!TryCreateRequest(cue, out var request, out var missingKind))
                {
                    if (missingKind == GameplayVfxPlaybackResultKind.TargetMissing)
                    {
                        targetMissingCount++;
                        _lastOmissionReason = DamageDeathVfxOmissionReason.TargetMissing;
                    }
                    else if (missingKind == GameplayVfxPlaybackResultKind.BindingMissing)
                    {
                        bindingMissingCount++;
                        _lastOmissionReason = DamageDeathVfxOmissionReason.BindingMissing;
                    }
                    else
                    {
                        anchorMissingCount++;
                        _lastOmissionReason = DamageDeathVfxOmissionReason.AnchorMissing;
                    }

                    continue;
                }

                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateOmittedCount++;
                        GetOrCreateSemanticCounter(request.CueKey).DuplicateOmittedCount++;
                        _lastOmissionReason = DamageDeathVfxOmissionReason.Duplicate;
                    }
                    else
                    {
                        duplicateOmittedCount++;
                        _lastOmissionReason = DamageDeathVfxOmissionReason.Duplicate;
                    }

                    continue;
                }

                if (_playbackPort == null)
                {
                    missingPortCount++;
                    _lastOmissionReason = DamageDeathVfxOmissionReason.PortMissing;
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
                        _lastOmissionReason = DamageDeathVfxOmissionReason.BindingMissing;
                        break;
                    case GameplayVfxPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        _lastOmissionReason = DamageDeathVfxOmissionReason.AnchorMissing;
                        break;
                    case GameplayVfxPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        _lastOmissionReason = DamageDeathVfxOmissionReason.TargetMissing;
                        break;
                }
            }

            _observedCueCount += observedCueCount;
            _targetMissingCount += targetMissingCount;
            _anchorMissingCount += anchorMissingCount;
            _bindingMissingCount += bindingMissingCount;
            _duplicateOmittedCount += duplicateOmittedCount;
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

            if (_hasRoutedRequest)
            {
                _playbackPort?.UpdatePresentation(deltaTime);
            }
        }

        public void ResetSession()
        {
            _hasRoutedRequest = false;
            ClearTelemetry();
            _executionGuard?.ResetSession();
            _cleanupRequestedCount++;
            _playbackPort?.ResetSession();
            _cleanupSucceededCount++;
            RefreshDiagnostics();
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            ClearTelemetry();
            _executionGuard?.ResetSession();
            _cleanupRequestedCount++;
            _playbackPort?.HardCleanup();
            _cleanupSucceededCount++;
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

            return true;
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
                isProductionDefaultOwner: true,
                observedCueCount: _observedCueCount,
                targetMissingCount: _targetMissingCount,
                anchorMissingCount: _anchorMissingCount,
                bindingMissingCount: _bindingMissingCount,
                duplicateOmittedCount: _duplicateOmittedCount,
                playbackRequestedCount: _playbackRequestedCount,
                playbackSucceededCount: _playbackSucceededCount,
                missingPortCount: _missingPortCount,
                damageCuePlannedCount: _damageCuePlannedCount,
                deathCuePlannedCount: _deathCuePlannedCount,
                damagePlaybackRequestedCount: _damagePlaybackRequestedCount,
                deathPlaybackRequestedCount: _deathPlaybackRequestedCount,
                sameTickDamageHitOmittedByDeathCount: _sameTickDamageHitOmittedByDeathCount,
                cleanupRequestedCount: _cleanupRequestedCount,
                cleanupSucceededCount: _cleanupSucceededCount,
                lastTickIndex: _lastTickIndex,
                lastCueKey: _lastCueKey,
                lastTargetEntityId: _lastTargetEntityId,
                lastOmissionReason: _lastOmissionReason,
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
            _targetMissingCount = 0;
            _anchorMissingCount = 0;
            _bindingMissingCount = 0;
            _duplicateOmittedCount = 0;
            _playbackRequestedCount = 0;
            _playbackSucceededCount = 0;
            _missingPortCount = 0;
            _damageCuePlannedCount = 0;
            _deathCuePlannedCount = 0;
            _damagePlaybackRequestedCount = 0;
            _deathPlaybackRequestedCount = 0;
            _sameTickDamageHitOmittedByDeathCount = 0;
            _cleanupRequestedCount = 0;
            _cleanupSucceededCount = 0;
            _lastTickIndex = 0;
            _lastCueKey = PresentationVfxCueKey.None;
            _lastTargetEntityId = 0;
            _lastOmissionReason = DamageDeathVfxOmissionReason.None;
            Diagnostics = default;
        }

        private bool TryCreateRequest(
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
                vfxAnchor,
                ResolveDelaySeconds(cue, _timingProfile));
            return true;
        }

        private static float ResolveDelaySeconds(
            PresentationCue cue,
            GameplayTimingProfile timingProfile)
        {
            if (cue.DelaySeconds > 0f)
            {
                return cue.DelaySeconds;
            }

            if (cue.Timing != (int)EntityExitPresentationTiming.AtContactTime ||
                cue.VisualContactNormalizedTime <= 0f)
            {
                return 0f;
            }

            var resolvedTimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            return resolvedTimingProfile.FlipMotionDurationSeconds * cue.VisualContactNormalizedTime;
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
                        anchor.HasTopology ? anchor.Topology : new CubeTopologyState(cell.face),
                        VfxAnchorSlot.CellCenter);
                    return true;
                default:
                    vfxAnchor = default;
                    return false;
            }
        }

    }
}
