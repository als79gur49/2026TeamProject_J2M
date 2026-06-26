using System;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
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
        OptionalProfileEntryMissing = 8,
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
            int lastTickIndex,
            PresentationEnemyAudioCueKey lastCueKey,
            int lastDedupeKey,
            int lastOwnerEntityId,
            PresentationEnemyAudioOriginKind lastOriginKind,
            PresentationEnemyAudioPhase lastPhase,
            EnemyAudioTelemetryFailureReason lastFailureReason,
            EnemyAudioTelemetryCleanupReason lastCleanupReason,
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
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastOwnerEntityId = Math.Max(0, lastOwnerEntityId);
            LastOriginKind = lastOriginKind;
            LastPhase = lastPhase;
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
            UnsupportedSemanticCount = Math.Max(0, unsupportedSemanticCount);
            UnsupportedLoopSemanticCount = Math.Max(0, unsupportedLoopSemanticCount);
            PortMissingCount = Math.Max(0, portMissingCount);
        }

        public int LastTickIndex { get; }
        public PresentationEnemyAudioCueKey LastCueKey { get; }
        public int LastDedupeKey { get; }
        public int LastOwnerEntityId { get; }
        public PresentationEnemyAudioOriginKind LastOriginKind { get; }
        public PresentationEnemyAudioPhase LastPhase { get; }
        public EnemyAudioTelemetryFailureReason LastFailureReason { get; }
        public EnemyAudioTelemetryCleanupReason LastCleanupReason { get; }
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

        public static EnemyAudioProductionTelemetrySnapshot Build(GameplayPresentationPipeline pipeline)
        {
            var executor = ResolveExecutorDiagnostics(pipeline);
            return new EnemyAudioProductionTelemetrySnapshot(
                executor.LastTickIndex,
                executor.LastCueKey,
                executor.LastDedupeKey,
                executor.LastOwnerEntityId,
                executor.LastOriginKind,
                executor.LastPhase,
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
        IGameplayEnemyAudioPlaybackPort playbackPort);

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
            _controller.ResetSession();
        }

        public void HardCleanup()
        {
            _controller.ResetSession();
        }
    }

    internal sealed class GameplayEnemyAudioPresentationExecutor : IPresentationExecutor
    {
        private readonly IGameplayEnemyAudioPlaybackPort _playbackPort;

        public GameplayEnemyAudioPresentationExecutor(IGameplayEnemyAudioPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
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
                        ref playbackSucceededCount,
                        ref optionalProfileEntryMissingNoOpCount);
                    lastFailureReason = ToTelemetryFailureReason(missingKind);
                    continue;
                }

                requestPlannedCount++;
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
                lastCleanupReason: EnemyAudioTelemetryCleanupReason.ResetSession);
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
                lastCleanupReason: EnemyAudioTelemetryCleanupReason.HardCleanupPresentationExtensions);
            _playbackPort?.HardCleanup();
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
                default:
                    return EnemyAudioTelemetryFailureReason.None;
            }
        }
    }
}
