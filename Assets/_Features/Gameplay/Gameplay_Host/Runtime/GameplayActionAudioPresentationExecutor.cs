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
    }

    internal enum ActionAudioTelemetryFailureReason
    {
        None = 0,
        OwnerViewMissing = 1,
        AuthoringMissing = 2,
        ProfileMissing = 3,
        BindingMissing = 4,
        UnsupportedMoment = 5,
        PortMissing = 6,
        OptionalProfileEntryMissing = 9,
    }

    internal enum ActionAudioTelemetryCleanupReason
    {
        None = 0,
        ResetSession = 1,
        HardCleanupPresentationExtensions = 2,
    }

    internal readonly struct ActionAudioProductionTelemetrySnapshot
    {
        public ActionAudioProductionTelemetrySnapshot(
            int lastTickIndex,
            PresentationActionAudioCueKey lastCueKey,
            int lastDedupeKey,
            int lastOwnerEntityId,
            GameplayActionKind lastAction,
            GameplayActionAudioMoment lastMoment,
            PresentationActionAudioOutcomeKind lastOutcome,
            ActionAudioTelemetryFailureReason lastFailureReason,
            ActionAudioTelemetryCleanupReason lastCleanupReason,
            int observedCueCount,
            int requestPlannedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int optionalProfileEntryMissingNoOpCount,
            int ownerViewMissingCount,
            int authoringMissingCount,
            int profileMissingCount,
            int bindingMissingCount,
            int unsupportedMomentCount,
            int portMissingCount)
        {
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastOwnerEntityId = Math.Max(0, lastOwnerEntityId);
            LastAction = lastAction;
            LastMoment = lastMoment;
            LastOutcome = lastOutcome;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
            ObservedCueCount = Math.Max(0, observedCueCount);
            RequestPlannedCount = Math.Max(0, requestPlannedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            OptionalProfileEntryMissingNoOpCount = Math.Max(0, optionalProfileEntryMissingNoOpCount);
            OwnerViewMissingCount = Math.Max(0, ownerViewMissingCount);
            AuthoringMissingCount = Math.Max(0, authoringMissingCount);
            ProfileMissingCount = Math.Max(0, profileMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            UnsupportedMomentCount = Math.Max(0, unsupportedMomentCount);
            PortMissingCount = Math.Max(0, portMissingCount);
        }

        public int LastTickIndex { get; }
        public PresentationActionAudioCueKey LastCueKey { get; }
        public int LastDedupeKey { get; }
        public int LastOwnerEntityId { get; }
        public GameplayActionKind LastAction { get; }
        public GameplayActionAudioMoment LastMoment { get; }
        public PresentationActionAudioOutcomeKind LastOutcome { get; }
        public ActionAudioTelemetryFailureReason LastFailureReason { get; }
        public ActionAudioTelemetryCleanupReason LastCleanupReason { get; }
        public int ObservedCueCount { get; }
        public int RequestPlannedCount { get; }
        public int PlaybackRequestedCount { get; }
        public int PlaybackSucceededCount { get; }
        public int OptionalProfileEntryMissingNoOpCount { get; }
        public int OwnerViewMissingCount { get; }
        public int AuthoringMissingCount { get; }
        public int ProfileMissingCount { get; }
        public int BindingMissingCount { get; }
        public int UnsupportedMomentCount { get; }
        public int PortMissingCount { get; }
    }

    internal static class ActionAudioProductionTelemetryBuilder
    {
        public static GameplayActionAudioExecutorDiagnostics ResolveExecutorDiagnostics(GameplayPresentationPipeline pipeline)
        {
            if (pipeline == null)
            {
                return default;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayActionAudioPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        public static ActionAudioProductionTelemetrySnapshot Build(GameplayPresentationPipeline pipeline)
        {
            var executor = ResolveExecutorDiagnostics(pipeline);

            return new ActionAudioProductionTelemetrySnapshot(
                executor.LastTickIndex,
                executor.LastCueKey,
                executor.LastDedupeKey,
                executor.LastOwnerEntityId,
                executor.LastAction,
                executor.LastMoment,
                executor.LastOutcome,
                executor.LastFailureReason,
                executor.LastCleanupReason,
                executor.ObservedCueCount,
                executor.RequestPlannedCount,
                executor.PlaybackRequestedCount,
                executor.PlaybackSucceededCount,
                executor.OptionalProfileEntryMissingNoOpCount,
                executor.OwnerViewMissingCount,
                executor.AuthoringMissingCount,
                executor.ProfileMissingCount,
                executor.BindingMissingCount,
                executor.UnsupportedMomentCount,
                executor.PortMissingCount);
        }
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
            int requestPlannedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int optionalProfileEntryMissingNoOpCount,
            int lastTickIndex = 0,
            PresentationActionAudioCueKey lastCueKey = PresentationActionAudioCueKey.None,
            int lastDedupeKey = 0,
            int lastOwnerEntityId = 0,
            GameplayActionKind lastAction = default,
            GameplayActionAudioMoment lastMoment = default,
            PresentationActionAudioOutcomeKind lastOutcome = PresentationActionAudioOutcomeKind.None,
            ActionAudioTelemetryFailureReason lastFailureReason =
                ActionAudioTelemetryFailureReason.None,
            ActionAudioTelemetryCleanupReason lastCleanupReason =
                ActionAudioTelemetryCleanupReason.None)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            OwnerViewMissingCount = Math.Max(0, ownerViewMissingCount);
            AuthoringMissingCount = Math.Max(0, authoringMissingCount);
            ProfileMissingCount = Math.Max(0, profileMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            UnsupportedMomentCount = Math.Max(0, unsupportedMomentCount);
            PortMissingCount = Math.Max(0, portMissingCount);
            RequestPlannedCount = Math.Max(0, requestPlannedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            OptionalProfileEntryMissingNoOpCount = Math.Max(0, optionalProfileEntryMissingNoOpCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastOwnerEntityId = Math.Max(0, lastOwnerEntityId);
            LastAction = lastAction;
            LastMoment = lastMoment;
            LastOutcome = lastOutcome;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
        }

        public int ObservedCueCount { get; }

        public int OwnerViewMissingCount { get; }

        public int AuthoringMissingCount { get; }

        public int ProfileMissingCount { get; }

        public int BindingMissingCount { get; }

        public int UnsupportedMomentCount { get; }

        public int PortMissingCount { get; }

        public int RequestPlannedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int OptionalProfileEntryMissingNoOpCount { get; }

        public int LastTickIndex { get; }

        public PresentationActionAudioCueKey LastCueKey { get; }

        public int LastDedupeKey { get; }

        public int LastOwnerEntityId { get; }

        public GameplayActionKind LastAction { get; }

        public GameplayActionAudioMoment LastMoment { get; }

        public PresentationActionAudioOutcomeKind LastOutcome { get; }

        public ActionAudioTelemetryFailureReason LastFailureReason { get; }

        public ActionAudioTelemetryCleanupReason LastCleanupReason { get; }
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
        IGameplayActionAudioPlaybackPort playbackPort);

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

        public GameplayActionAudioPresentationExecutor(IGameplayActionAudioPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
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
            var requestPlannedCount = 0;
            var playbackRequestedCount = 0;
            var playbackSucceededCount = 0;
            var optionalProfileEntryMissingNoOpCount = 0;
            var lastTickIndex = 0;
            var lastCueKey = PresentationActionAudioCueKey.None;
            var lastDedupeKey = 0;
            var lastOwnerEntityId = 0;
            var lastAction = default(GameplayActionKind);
            var lastMoment = default(GameplayActionAudioMoment);
            var lastOutcome = PresentationActionAudioOutcomeKind.None;
            var lastFailureReason = ActionAudioTelemetryFailureReason.None;

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var playbackCue = plan.Cues[i];
                var cue = playbackCue.Cue;
                if (!IsActionAudioCue(cue))
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
                    ref lastAction,
                    ref lastMoment,
                    ref lastOutcome);

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
                        ref playbackSucceededCount,
                        ref optionalProfileEntryMissingNoOpCount);
                    lastFailureReason = ToTelemetryFailureReason(missingKind);
                    continue;
                }

                requestPlannedCount++;
                if (_playbackPort == null)
                {
                    portMissingCount++;
                    lastFailureReason = ActionAudioTelemetryFailureReason.PortMissing;
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
                    ref playbackSucceededCount,
                    ref optionalProfileEntryMissingNoOpCount);
                lastFailureReason = ToTelemetryFailureReason(result.Kind);
            }

            Diagnostics = new GameplayActionAudioExecutorDiagnostics(
                observedCueCount,
                ownerViewMissingCount,
                authoringMissingCount,
                profileMissingCount,
                bindingMissingCount,
                unsupportedMomentCount,
                portMissingCount,
                requestPlannedCount,
                playbackRequestedCount,
                playbackSucceededCount,
                optionalProfileEntryMissingNoOpCount,
                lastTickIndex,
                lastCueKey,
                lastDedupeKey,
                lastOwnerEntityId,
                lastAction,
                lastMoment,
                lastOutcome,
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
            Diagnostics = new GameplayActionAudioExecutorDiagnostics(
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
                lastCleanupReason: ActionAudioTelemetryCleanupReason.ResetSession);
            _playbackPort?.ResetSession();
        }

        public void HardCleanup()
        {
            Diagnostics = new GameplayActionAudioExecutorDiagnostics(
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
                lastCleanupReason: ActionAudioTelemetryCleanupReason.HardCleanupPresentationExtensions);
            _playbackPort?.HardCleanup();
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

        private static void CaptureLastCue(
            in PresentationPlaybackCue playbackCue,
            ref int lastTickIndex,
            ref PresentationActionAudioCueKey lastCueKey,
            ref int lastDedupeKey,
            ref int lastOwnerEntityId,
            ref GameplayActionKind lastAction,
            ref GameplayActionAudioMoment lastMoment,
            ref PresentationActionAudioOutcomeKind lastOutcome)
        {
            var cue = playbackCue.Cue;
            lastTickIndex = cue.Source.TickIndex;
            lastDedupeKey = playbackCue.Policy.DedupeKey;
            lastOwnerEntityId = cue.ActionAudioPayload.OwnerEntityId;
            lastOutcome = cue.ActionAudioPayload.OutcomeKind;
            lastCueKey = cue.Key.TryGetActionAudioCueKey(out var cueKey)
                ? cueKey
                : PresentationActionAudioCueKey.None;
            lastAction = TryResolveAction(cue.ActionAudioPayload.ActionKind, out var action)
                ? action
                : default;
            lastMoment = TryResolveMoment(cue.ActionAudioPayload.Moment, out var moment)
                ? moment
                : default;
        }

        private static void RecordResult(
            GameplayActionAudioPlaybackResultKind resultKind,
            ref int ownerViewMissingCount,
            ref int authoringMissingCount,
            ref int profileMissingCount,
            ref int bindingMissingCount,
            ref int unsupportedMomentCount,
            ref int portMissingCount,
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
            }
        }

        private static ActionAudioTelemetryFailureReason ToTelemetryFailureReason(
            GameplayActionAudioPlaybackResultKind resultKind)
        {
            switch (resultKind)
            {
                case GameplayActionAudioPlaybackResultKind.OwnerViewMissing:
                    return ActionAudioTelemetryFailureReason.OwnerViewMissing;
                case GameplayActionAudioPlaybackResultKind.AuthoringMissing:
                    return ActionAudioTelemetryFailureReason.AuthoringMissing;
                case GameplayActionAudioPlaybackResultKind.ProfileMissing:
                    return ActionAudioTelemetryFailureReason.ProfileMissing;
                case GameplayActionAudioPlaybackResultKind.BindingMissing:
                    return ActionAudioTelemetryFailureReason.BindingMissing;
                case GameplayActionAudioPlaybackResultKind.UnsupportedMoment:
                    return ActionAudioTelemetryFailureReason.UnsupportedMoment;
                case GameplayActionAudioPlaybackResultKind.PortMissing:
                    return ActionAudioTelemetryFailureReason.PortMissing;
                case GameplayActionAudioPlaybackResultKind.OptionalProfileEntryMissing:
                    return ActionAudioTelemetryFailureReason.OptionalProfileEntryMissing;
                default:
                    return ActionAudioTelemetryFailureReason.None;
            }
        }
    }
}
