using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    public enum ActionAudioExecutionMode
    {
        LegacyActionAudioController = 0,
        OrchestrationActionAudioBridge = 1,
    }

    internal enum ActionAudioExecutionOwner
    {
        None = 0,
        LegacyActionAudioController = 1,
        OrchestrationActionAudioBridge = 2,
    }

    internal enum GameplayActionAudioPlaybackResultKind
    {
        None = 0,
        OwnerViewMissing = 1,
        AuthoringMissing = 2,
        ProfileMissing = 3,
        BindingMissing = 4,
        UnsupportedMoment = 5,
        PortMissing = 6,
        Requested = 7,
        Succeeded = 8,
        OptionalProfileEntryMissing = 9,
        LegacyOwnerActive = 10,
    }

    internal readonly struct ActionAudioPlaybackKey : IEquatable<ActionAudioPlaybackKey>
    {
        public ActionAudioPlaybackKey(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int ownerEntityId,
            GameplayActionKind action,
            GameplayActionAudioMoment moment,
            int sourceSequenceId,
            int sourceActionPlanId,
            int targetEntityId,
            int orderIndex)
        {
            TickIndex = Math.Max(0, tickIndex);
            SemanticSource = semanticSource;
            OwnerEntityId = Math.Max(0, ownerEntityId);
            Action = action;
            Moment = moment;
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            SourceActionPlanId = Math.Max(0, sourceActionPlanId);
            TargetEntityId = Math.Max(0, targetEntityId);
            OrderIndex = Math.Max(0, orderIndex);
        }

        public int TickIndex { get; }

        public PresentationSemanticSource SemanticSource { get; }

        public int OwnerEntityId { get; }

        public GameplayActionKind Action { get; }

        public GameplayActionAudioMoment Moment { get; }

        public int SourceSequenceId { get; }

        public int SourceActionPlanId { get; }

        public int TargetEntityId { get; }

        public int OrderIndex { get; }

        public bool Equals(ActionAudioPlaybackKey other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   OwnerEntityId == other.OwnerEntityId &&
                   Action == other.Action &&
                   Moment == other.Moment &&
                   SourceSequenceId == other.SourceSequenceId &&
                   SourceActionPlanId == other.SourceActionPlanId &&
                   TargetEntityId == other.TargetEntityId &&
                   OrderIndex == other.OrderIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is ActionAudioPlaybackKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)SemanticSource;
                hash = (hash * 397) ^ OwnerEntityId;
                hash = (hash * 397) ^ (int)Action;
                hash = (hash * 397) ^ (int)Moment;
                hash = (hash * 397) ^ SourceSequenceId;
                hash = (hash * 397) ^ SourceActionPlanId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ OrderIndex;
                return hash;
            }
        }
    }

    internal readonly struct ActionAudioOwnershipDiagnostics
    {
        public ActionAudioOwnershipDiagnostics(
            ActionAudioExecutionMode mode,
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            ActionAudioExecutionOwner lastExecutionOwner)
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

        public ActionAudioExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public ActionAudioExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class ActionAudioExecutionGuard
    {
        private readonly HashSet<ActionAudioPlaybackKey> _claimedKeys = new();
        private ActionAudioExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private ActionAudioExecutionOwner _lastExecutionOwner;

        public ActionAudioExecutionGuard(
            ActionAudioExecutionMode mode = ActionAudioExecutionMode.LegacyActionAudioController)
        {
            _mode = NormalizeMode(mode);
        }

        public ActionAudioOwnershipDiagnostics Diagnostics =>
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

        public void Configure(ActionAudioExecutionMode mode)
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
            _lastExecutionOwner = ActionAudioExecutionOwner.None;
        }

        public void RecordSkippedByPolicy(ActionAudioExecutionOwner skippedOwner)
        {
            if (skippedOwner == ActionAudioExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOwner), "Action audio owner must be explicit.");
            }

            RecordAttempt(skippedOwner);
            RecordPolicySkip(skippedOwner);
        }

        public bool TryBeginExecution(
            ActionAudioExecutionOwner owner,
            in ActionAudioPlaybackKey key)
        {
            if (owner == ActionAudioExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "Action audio owner must be explicit.");
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
            if (owner == ActionAudioExecutionOwner.LegacyActionAudioController)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static ActionAudioExecutionMode NormalizeMode(ActionAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(ActionAudioExecutionMode), mode)
                ? mode
                : ActionAudioExecutionMode.LegacyActionAudioController;
        }

        private bool IsOwnerAllowed(ActionAudioExecutionOwner owner)
        {
            return (_mode == ActionAudioExecutionMode.LegacyActionAudioController &&
                    owner == ActionAudioExecutionOwner.LegacyActionAudioController) ||
                   (_mode == ActionAudioExecutionMode.OrchestrationActionAudioBridge &&
                    owner == ActionAudioExecutionOwner.OrchestrationActionAudioBridge);
        }

        private void RecordAttempt(ActionAudioExecutionOwner owner)
        {
            if (owner == ActionAudioExecutionOwner.LegacyActionAudioController)
            {
                _legacyAttemptCount++;
            }
            else if (owner == ActionAudioExecutionOwner.OrchestrationActionAudioBridge)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(ActionAudioExecutionOwner owner)
        {
            if (owner == ActionAudioExecutionOwner.LegacyActionAudioController)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (owner == ActionAudioExecutionOwner.OrchestrationActionAudioBridge)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
            }
        }
    }

    internal readonly struct GameplayActionAudioPlaybackRequest
    {
        public GameplayActionAudioPlaybackRequest(
            ActionAudioPlaybackKey ownershipKey,
            PresentationActionAudioCueKey cueKey,
            GameplayActionKind action,
            GameplayActionAudioMoment moment,
            PresentationSource source,
            PresentationTarget target,
            PresentationAnchor anchor,
            PresentationActionAudioPayload actionAudioPayload,
            AudioPlaybackContext context)
        {
            OwnershipKey = ownershipKey;
            CueKey = cueKey;
            Action = action;
            Moment = moment;
            Source = source;
            Target = target;
            Anchor = anchor;
            ActionAudioPayload = actionAudioPayload;
            Context = context;
        }

        public ActionAudioPlaybackKey OwnershipKey { get; }

        public PresentationActionAudioCueKey CueKey { get; }

        public GameplayActionKind Action { get; }

        public GameplayActionAudioMoment Moment { get; }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public PresentationActionAudioPayload ActionAudioPayload { get; }

        public AudioPlaybackContext Context { get; }

        public int TickIndex => OwnershipKey.TickIndex;

        public int OwnerEntityId => OwnershipKey.OwnerEntityId;
    }

    internal readonly struct GameplayActionAudioPlaybackResult
    {
        public GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind kind)
        {
            Kind = kind;
        }

        public GameplayActionAudioPlaybackResultKind Kind { get; }
    }

    internal readonly struct GameplayActionAudioExecutorDiagnostics
    {
        public GameplayActionAudioExecutorDiagnostics(
            int observedCueCount,
            int ownerViewMissingCount,
            int authoringMissingCount,
            int profileMissingCount,
            int bindingMissingCount,
            int unsupportedMomentCount,
            int portMissingCount,
            int duplicateSuppressedCount,
            int legacyOwnerNoOpCount,
            int requestPlannedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int optionalProfileEntryMissingNoOpCount)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            OwnerViewMissingCount = Math.Max(0, ownerViewMissingCount);
            AuthoringMissingCount = Math.Max(0, authoringMissingCount);
            ProfileMissingCount = Math.Max(0, profileMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            UnsupportedMomentCount = Math.Max(0, unsupportedMomentCount);
            PortMissingCount = Math.Max(0, portMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
            RequestPlannedCount = Math.Max(0, requestPlannedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            OptionalProfileEntryMissingNoOpCount = Math.Max(0, optionalProfileEntryMissingNoOpCount);
        }

        public int ObservedCueCount { get; }

        public int OwnerViewMissingCount { get; }

        public int AuthoringMissingCount { get; }

        public int ProfileMissingCount { get; }

        public int BindingMissingCount { get; }

        public int UnsupportedMomentCount { get; }

        public int PortMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int LegacyOwnerNoOpCount { get; }

        public int RequestPlannedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int OptionalProfileEntryMissingNoOpCount { get; }
    }

    internal interface IGameplayActionAudioPlaybackPort
    {
        bool TryPlayActionAudio(
            in GameplayActionAudioPlaybackRequest request,
            out GameplayActionAudioPlaybackResult result);

        void ResetSession();

        void HardCleanup();
    }

    internal delegate GameplayPresentationPipeline ActionAudioExecutionPipelineFactory(
        ActionAudioExecutionMode mode,
        IGameplayActionAudioPlaybackPort playbackPort,
        ActionAudioExecutionGuard executionGuard);

    internal sealed class GameplayActionAudioPlaybackPortAdapter : IGameplayActionAudioPlaybackPort
    {
        private readonly GameplayActionAudioPresentationController _controller;

        public GameplayActionAudioPlaybackPortAdapter(GameplayActionAudioPresentationController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public bool TryPlayActionAudio(
            in GameplayActionAudioPlaybackRequest request,
            out GameplayActionAudioPlaybackResult result)
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

    internal sealed class GameplayActionAudioPresentationExecutor : IPresentationExecutor
    {
        private readonly IGameplayActionAudioPlaybackPort _playbackPort;
        private readonly ActionAudioExecutionMode _mode;
        private readonly ActionAudioExecutionGuard _executionGuard;

        public GameplayActionAudioPresentationExecutor(
            IGameplayActionAudioPlaybackPort playbackPort = null,
            ActionAudioExecutionMode mode = ActionAudioExecutionMode.LegacyActionAudioController,
            ActionAudioExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
            _executionGuard = executionGuard;
        }

        public GameplayActionAudioExecutorDiagnostics Diagnostics { get; private set; }

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
            var unsupportedMomentCount = 0;
            var portMissingCount = 0;
            var duplicateSuppressedCount = 0;
            var legacyOwnerNoOpCount = 0;
            var requestPlannedCount = 0;
            var playbackRequestedCount = 0;
            var playbackSucceededCount = 0;
            var optionalProfileEntryMissingNoOpCount = 0;

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var playbackCue = plan.Cues[i];
                var cue = playbackCue.Cue;
                if (!IsActionAudioCue(cue))
                {
                    continue;
                }

                observedCueCount++;
                if (_mode != ActionAudioExecutionMode.OrchestrationActionAudioBridge)
                {
                    legacyOwnerNoOpCount++;
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
                        ref unsupportedMomentCount,
                        ref portMissingCount,
                        ref legacyOwnerNoOpCount,
                        ref playbackSucceededCount,
                        ref optionalProfileEntryMissingNoOpCount);
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
                    }
                    else
                    {
                        legacyOwnerNoOpCount++;
                    }

                    continue;
                }

                if (_playbackPort == null)
                {
                    portMissingCount++;
                    continue;
                }

                playbackRequestedCount++;
                _playbackPort.TryPlayActionAudio(request, out var result);
                RecordResult(
                    result.Kind,
                    ref ownerViewMissingCount,
                    ref authoringMissingCount,
                    ref profileMissingCount,
                    ref bindingMissingCount,
                    ref unsupportedMomentCount,
                    ref portMissingCount,
                    ref legacyOwnerNoOpCount,
                    ref playbackSucceededCount,
                    ref optionalProfileEntryMissingNoOpCount);
            }

            Diagnostics = new GameplayActionAudioExecutorDiagnostics(
                observedCueCount,
                ownerViewMissingCount,
                authoringMissingCount,
                profileMissingCount,
                bindingMissingCount,
                unsupportedMomentCount,
                portMissingCount,
                duplicateSuppressedCount,
                legacyOwnerNoOpCount,
                requestPlannedCount,
                playbackRequestedCount,
                playbackSucceededCount,
                optionalProfileEntryMissingNoOpCount);
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
            _executionGuard?.ResetSession();
            _playbackPort?.ResetSession();
        }

        public void HardCleanup()
        {
            Diagnostics = default;
            _executionGuard?.ResetSession();
            _playbackPort?.HardCleanup();
        }

        private bool TryClaimExecution(in ActionAudioPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    ActionAudioExecutionOwner.OrchestrationActionAudioBridge,
                    key);
            }

            return _mode == ActionAudioExecutionMode.OrchestrationActionAudioBridge;
        }

        private static bool IsActionAudioCue(PresentationCue cue)
        {
            return cue.Domain == PresentationDomain.ActionAudio &&
                   cue.Key.Domain == PresentationDomain.ActionAudio &&
                   cue.Key.LocalKey > 0;
        }

        private static bool TryCreateRequest(
            PresentationCue cue,
            int orderIndex,
            out GameplayActionAudioPlaybackRequest request,
            out GameplayActionAudioPlaybackResultKind missingKind)
        {
            request = default;
            missingKind = GameplayActionAudioPlaybackResultKind.None;
            if (!cue.Key.TryGetActionAudioCueKey(out var cueKey) ||
                !TryResolveAction(cue.ActionAudioPayload.ActionKind, out var action) ||
                !TryResolveMoment(cue.ActionAudioPayload.Moment, out var moment))
            {
                missingKind = GameplayActionAudioPlaybackResultKind.UnsupportedMoment;
                return false;
            }

            if (!cue.ActionAudioPayload.IsValid ||
                cue.ActionAudioPayload.OwnerEntityId <= 0)
            {
                missingKind = GameplayActionAudioPlaybackResultKind.OwnerViewMissing;
                return false;
            }

            var payload = cue.ActionAudioPayload;
            var key = new ActionAudioPlaybackKey(
                cue.Source.TickIndex,
                cue.Source.SemanticSource,
                payload.OwnerEntityId,
                action,
                moment,
                payload.SourceSequenceId,
                payload.SourceActionPlanId,
                payload.TargetEntityId,
                orderIndex);
            request = new GameplayActionAudioPlaybackRequest(
                key,
                cueKey,
                action,
                moment,
                cue.Source,
                cue.Target,
                cue.Anchor,
                payload,
                new AudioPlaybackContext(
                    ownerEntityId: payload.OwnerEntityId,
                    debugTag: FormatDebugTag(action, moment)));
            return true;
        }

        private static bool TryResolveAction(int actionKind, out GameplayActionKind action)
        {
            if (Enum.IsDefined(typeof(GameplayActionKind), actionKind))
            {
                action = (GameplayActionKind)actionKind;
                return true;
            }

            action = default;
            return false;
        }

        private static bool TryResolveMoment(int momentValue, out GameplayActionAudioMoment moment)
        {
            switch ((GameplayActionAudioMoment)momentValue)
            {
                case GameplayActionAudioMoment.Windup:
                case GameplayActionAudioMoment.AssistOutOfRange:
                case GameplayActionAudioMoment.NoTarget:
                case GameplayActionAudioMoment.Invalid:
                    moment = (GameplayActionAudioMoment)momentValue;
                    return true;
                default:
                    moment = default;
                    return false;
            }
        }

        private static string FormatDebugTag(GameplayActionKind action, GameplayActionAudioMoment moment)
        {
            return $"Action:{action}:{moment}";
        }

        private static void RecordResult(
            GameplayActionAudioPlaybackResultKind resultKind,
            ref int ownerViewMissingCount,
            ref int authoringMissingCount,
            ref int profileMissingCount,
            ref int bindingMissingCount,
            ref int unsupportedMomentCount,
            ref int portMissingCount,
            ref int legacyOwnerNoOpCount,
            ref int playbackSucceededCount,
            ref int optionalProfileEntryMissingNoOpCount)
        {
            switch (resultKind)
            {
                case GameplayActionAudioPlaybackResultKind.OwnerViewMissing:
                    ownerViewMissingCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.AuthoringMissing:
                    authoringMissingCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.ProfileMissing:
                    profileMissingCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.BindingMissing:
                    bindingMissingCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.UnsupportedMoment:
                    unsupportedMomentCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.PortMissing:
                    portMissingCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.OptionalProfileEntryMissing:
                    optionalProfileEntryMissingNoOpCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.Succeeded:
                case GameplayActionAudioPlaybackResultKind.Requested:
                    playbackSucceededCount++;
                    break;
                case GameplayActionAudioPlaybackResultKind.LegacyOwnerActive:
                    legacyOwnerNoOpCount++;
                    break;
            }
        }

        private static ActionAudioExecutionMode NormalizeMode(ActionAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(ActionAudioExecutionMode), mode)
                ? mode
                : ActionAudioExecutionMode.LegacyActionAudioController;
        }
    }
}
