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
        LegacyAnimationSync = 0,
        OrchestrationAnimationExecutor = 1,
    }

    internal enum PlayerActionAnimationExecutionOwner
    {
        None = 0,
        LegacyAnimationSync = 1,
        OrchestrationAnimationExecutor = 2,
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
        LegacyOwnerActive = 8,
        IgnoredByPolicy = 9,
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
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            PlayerActionAnimationExecutionOwner lastExecutionOwner)
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

        public PlayerActionAnimationExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public PlayerActionAnimationExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class PlayerActionAnimationExecutionGuard
    {
        private readonly HashSet<PlayerActionAnimationPlaybackKey> _claimedKeys = new();
        private PlayerActionAnimationExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private PlayerActionAnimationExecutionOwner _lastExecutionOwner;

        public PlayerActionAnimationExecutionGuard(
            PlayerActionAnimationExecutionMode mode = PlayerActionAnimationExecutionMode.LegacyAnimationSync)
        {
            _mode = NormalizeMode(mode);
        }

        public PlayerActionAnimationOwnershipDiagnostics Diagnostics =>
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

        public void Configure(PlayerActionAnimationExecutionMode mode)
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
            _lastExecutionOwner = PlayerActionAnimationExecutionOwner.None;
        }

        public void RecordSkippedByPolicy(PlayerActionAnimationExecutionOwner skippedOwner)
        {
            if (skippedOwner == PlayerActionAnimationExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOwner), "Player action animation owner must be explicit.");
            }

            RecordAttempt(skippedOwner);
            RecordPolicySkip(skippedOwner);
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
            if (owner == PlayerActionAnimationExecutionOwner.LegacyAnimationSync)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static PlayerActionAnimationExecutionMode NormalizeMode(PlayerActionAnimationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), mode)
                ? mode
                : PlayerActionAnimationExecutionMode.LegacyAnimationSync;
        }

        private bool IsOwnerAllowed(PlayerActionAnimationExecutionOwner owner)
        {
            return (_mode == PlayerActionAnimationExecutionMode.LegacyAnimationSync &&
                    owner == PlayerActionAnimationExecutionOwner.LegacyAnimationSync) ||
                   (_mode == PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor &&
                    owner == PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor);
        }

        private void RecordAttempt(PlayerActionAnimationExecutionOwner owner)
        {
            if (owner == PlayerActionAnimationExecutionOwner.LegacyAnimationSync)
            {
                _legacyAttemptCount++;
            }
            else if (owner == PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(PlayerActionAnimationExecutionOwner owner)
        {
            if (owner == PlayerActionAnimationExecutionOwner.LegacyAnimationSync)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (owner == PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
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
            bool executeCueMappedToLegacyCommand = false)
        {
            Kind = kind;
            ExecuteCueMappedToLegacyCommand = executeCueMappedToLegacyCommand;
        }

        public GameplayAnimationPlaybackResultKind Kind { get; }

        public bool ExecuteCueMappedToLegacyCommand { get; }
    }

    internal readonly struct GameplayAnimationExecutorDiagnostics
    {
        public GameplayAnimationExecutorDiagnostics(
            int observedCueCount,
            int legacyOwnerNoOpCount,
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
            int executeCueMappedToLegacyCommandCount)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
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
            ExecuteCueMappedToLegacyCommandCount = Math.Max(0, executeCueMappedToLegacyCommandCount);
        }

        public int ObservedCueCount { get; }

        public int LegacyOwnerNoOpCount { get; }

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

        public int ExecuteCueMappedToLegacyCommandCount { get; }
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
            PlayerActionAnimationExecutionMode mode = PlayerActionAnimationExecutionMode.LegacyAnimationSync,
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
            var legacyOwnerNoOpCount = 0;
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
            var executeCueMappedToLegacyCommandCount = 0;

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var playbackCue = plan.Cues[i];
                if (!IsPlayerActionAnimationCue(playbackCue))
                {
                    continue;
                }

                observedCueCount++;
                if (_mode != PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor)
                {
                    legacyOwnerNoOpCount++;
                    continue;
                }

                if (!TryCreateRequest(playbackCue, out var request, out var missingKind))
                {
                    RecordMissing(
                        missingKind,
                        ref targetMissingCount,
                        ref anchorMissingCount,
                        ref bindingMissingCount,
                        ref driverMissingCount,
                        ref animatorMissingCount);
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

                commandRequestedCount++;
                _playbackPort.TryPlayPlayerActionAnimation(request, out var result);
                if (result.ExecuteCueMappedToLegacyCommand)
                {
                    executeCueMappedToLegacyCommandCount++;
                }

                switch (result.Kind)
                {
                    case GameplayAnimationPlaybackResultKind.Applied:
                    case GameplayAnimationPlaybackResultKind.Requested:
                        commandAppliedCount++;
                        break;
                    case GameplayAnimationPlaybackResultKind.TargetMissing:
                        targetMissingCount++;
                        break;
                    case GameplayAnimationPlaybackResultKind.AnchorMissing:
                        anchorMissingCount++;
                        break;
                    case GameplayAnimationPlaybackResultKind.BindingMissing:
                        bindingMissingCount++;
                        break;
                    case GameplayAnimationPlaybackResultKind.DriverMissing:
                        driverMissingCount++;
                        break;
                    case GameplayAnimationPlaybackResultKind.AnimatorMissing:
                        animatorMissingCount++;
                        break;
                    case GameplayAnimationPlaybackResultKind.LegacyOwnerActive:
                        legacyOwnerNoOpCount++;
                        break;
                    case GameplayAnimationPlaybackResultKind.IgnoredByPolicy:
                        commandIgnoredByPolicyCount++;
                        break;
                }
            }

            Diagnostics = new GameplayAnimationExecutorDiagnostics(
                observedCueCount,
                legacyOwnerNoOpCount,
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
                executeCueMappedToLegacyCommandCount);
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
            Diagnostics = default;
            if (_mode == PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor)
            {
                _playbackPort?.ResetSession();
            }
        }

        public void HardCleanup()
        {
            Diagnostics = default;
            if (_mode == PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor)
            {
                _playbackPort?.HardCleanup();
            }
        }

        private bool TryClaimExecution(in PlayerActionAnimationPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor,
                    key);
            }

            return _mode == PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor;
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

        private static PlayerActionAnimationExecutionMode NormalizeMode(PlayerActionAnimationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), mode)
                ? mode
                : PlayerActionAnimationExecutionMode.LegacyAnimationSync;
        }
    }

    internal sealed class GameplayAnimationSyncPlaybackPort : IGameplayAnimationPlaybackPort
    {
        private readonly GameplayAnimationSyncCoordinator _animationSync;
        private readonly GameplayPresentationStateStore _stateStore;

        public GameplayAnimationSyncPlaybackPort(
            GameplayAnimationSyncCoordinator animationSync,
            GameplayPresentationStateStore stateStore)
        {
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public bool TryPlayPlayerActionAnimation(
            in GameplayAnimationPlaybackRequest request,
            out GameplayAnimationPlaybackResult result)
        {
            return _animationSync.TryApplyPlayerActionAnimationPlayback(
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
