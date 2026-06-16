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

    internal readonly struct GameplayVfxExecutorDiagnostics
    {
        public GameplayVfxExecutorDiagnostics(
            int observedCueCount,
            int legacyOwnerNoOpCount,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int duplicateSuppressedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int missingPortCount)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            MissingPortCount = Math.Max(0, missingPortCount);
        }

        public int ObservedCueCount { get; }

        public int LegacyOwnerNoOpCount { get; }

        public int TargetMissingCount { get; }

        public int AnchorMissingCount { get; }

        public int BindingMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int MissingPortCount { get; }
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
        private bool _hasRoutedRequest;

        public GameplayVfxPresentationExecutor(
            IDamageDeathVfxPlaybackPort playbackPort = null,
            DamageDeathVfxExecutionMode mode = DamageDeathVfxExecutionMode.LegacyExtension,
            DamageDeathVfxExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
            _executionGuard = executionGuard;
        }

        public GameplayVfxExecutorDiagnostics Diagnostics { get; private set; }

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
                if (_mode != DamageDeathVfxExecutionMode.OrchestrationExecutor)
                {
                    legacyOwnerNoOpCount++;
                    continue;
                }

                if (!TryCreateRequest(cue, out var request, out var missingKind))
                {
                    if (missingKind == GameplayVfxPlaybackResultKind.TargetMissing)
                    {
                        targetMissingCount++;
                    }
                    else
                    {
                        anchorMissingCount++;
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
                _playbackPort.TryPlayDamageDeathVfx(request, out var result);
                switch (result.Kind)
                {
                    case GameplayVfxPlaybackResultKind.Succeeded:
                        playbackSucceededCount++;
                        break;
                    case GameplayVfxPlaybackResultKind.BindingMissing:
                        bindingMissingCount++;
                        break;
                    case GameplayVfxPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        break;
                    case GameplayVfxPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        break;
                    case GameplayVfxPlaybackResultKind.LegacyOwnerActive:
                        legacyOwnerNoOpCount++;
                        break;
                }
            }

            Diagnostics = new GameplayVfxExecutorDiagnostics(
                observedCueCount,
                legacyOwnerNoOpCount,
                targetMissingCount,
                anchorMissingCount,
                bindingMissingCount,
                duplicateSuppressedCount,
                playbackRequestedCount,
                playbackSucceededCount,
                missingPortCount);
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
            Diagnostics = default;
            _executionGuard?.ResetSession();
            if (_mode == DamageDeathVfxExecutionMode.OrchestrationExecutor)
            {
                _playbackPort?.ResetSession();
            }
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            _executionGuard?.ResetSession();
            if (_mode == DamageDeathVfxExecutionMode.OrchestrationExecutor)
            {
                _playbackPort?.HardCleanup();
            }
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
