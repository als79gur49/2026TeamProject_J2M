using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum CoreGameplaySfxExecutionMode
    {
        LegacyGameplayAudioController = 0,
        OrchestrationSfxBridgeExecutor = 1,
    }

    internal enum CoreGameplaySfxExecutionOwner
    {
        None = 0,
        LegacyGameplayAudioController = 1,
        OrchestrationSfxBridgeExecutor = 2,
    }

    internal enum GameplaySfxPlaybackResultKind
    {
        None = 0,
        SemanticUnsupported = 1,
        MapMissing = 2,
        BindingMissing = 3,
        TargetMissing = 4,
        OwnerViewMissing = 5,
        PortMissing = 6,
        Requested = 7,
        Succeeded = 8,
        NoOpFallback = 9,
        LegacyOwnerActive = 10,
    }

    internal readonly struct CoreGameplaySfxPlaybackKey : IEquatable<CoreGameplaySfxPlaybackKey>
    {
        public CoreGameplaySfxPlaybackKey(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int sourceEntityId,
            int targetEntityId,
            PresentationSfxCueKey cueKey)
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

        public PresentationSfxCueKey CueKey { get; }

        public bool Equals(CoreGameplaySfxPlaybackKey other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   SourceEntityId == other.SourceEntityId &&
                   TargetEntityId == other.TargetEntityId &&
                   CueKey == other.CueKey;
        }

        public override bool Equals(object obj)
        {
            return obj is CoreGameplaySfxPlaybackKey other && Equals(other);
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

    internal readonly struct CoreGameplaySfxOwnershipDiagnostics
    {
        public CoreGameplaySfxOwnershipDiagnostics(
            CoreGameplaySfxExecutionMode mode,
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            CoreGameplaySfxExecutionOwner lastExecutionOwner)
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

        public CoreGameplaySfxExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public CoreGameplaySfxExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class CoreGameplaySfxExecutionGuard
    {
        private readonly HashSet<CoreGameplaySfxPlaybackKey> _claimedKeys = new();
        private CoreGameplaySfxExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private CoreGameplaySfxExecutionOwner _lastExecutionOwner;

        public CoreGameplaySfxExecutionGuard(
            CoreGameplaySfxExecutionMode mode = CoreGameplaySfxExecutionMode.LegacyGameplayAudioController)
        {
            _mode = NormalizeMode(mode);
        }

        public CoreGameplaySfxOwnershipDiagnostics Diagnostics =>
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

        public void Configure(CoreGameplaySfxExecutionMode mode)
        {
            _mode = NormalizeMode(mode);
        }

        public void RecordSkippedByPolicy(CoreGameplaySfxExecutionOwner skippedOwner)
        {
            if (skippedOwner == CoreGameplaySfxExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOwner), "Core SFX owner must be explicit.");
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
            _lastExecutionOwner = CoreGameplaySfxExecutionOwner.None;
        }

        public bool TryBeginExecution(
            CoreGameplaySfxExecutionOwner owner,
            in CoreGameplaySfxPlaybackKey key)
        {
            if (owner == CoreGameplaySfxExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "Core SFX owner must be explicit.");
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
            if (owner == CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static CoreGameplaySfxExecutionMode NormalizeMode(CoreGameplaySfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(CoreGameplaySfxExecutionMode), mode)
                ? mode
                : CoreGameplaySfxExecutionMode.LegacyGameplayAudioController;
        }

        private bool IsOwnerAllowed(CoreGameplaySfxExecutionOwner owner)
        {
            return (_mode == CoreGameplaySfxExecutionMode.LegacyGameplayAudioController &&
                    owner == CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController) ||
                   (_mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor &&
                    owner == CoreGameplaySfxExecutionOwner.OrchestrationSfxBridgeExecutor);
        }

        private void RecordAttempt(CoreGameplaySfxExecutionOwner owner)
        {
            if (owner == CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController)
            {
                _legacyAttemptCount++;
            }
            else if (owner == CoreGameplaySfxExecutionOwner.OrchestrationSfxBridgeExecutor)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(CoreGameplaySfxExecutionOwner owner)
        {
            if (owner == CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (owner == CoreGameplaySfxExecutionOwner.OrchestrationSfxBridgeExecutor)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
            }
        }
    }

    internal readonly struct GameplaySfxPlaybackRequest
    {
        public GameplaySfxPlaybackRequest(
            CoreGameplaySfxPlaybackKey ownershipKey,
            PresentationSfxCueKey cueKey,
            PresentationSource source,
            PresentationTarget target,
            PresentationAnchor anchor,
            PresentationSfxPayload sfxPayload = default)
        {
            OwnershipKey = ownershipKey;
            CueKey = cueKey;
            Source = source;
            Target = target;
            Anchor = anchor;
            SfxPayload = sfxPayload;
        }

        public CoreGameplaySfxPlaybackKey OwnershipKey { get; }

        public PresentationSfxCueKey CueKey { get; }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public PresentationSfxPayload SfxPayload { get; }

        public int TickIndex => OwnershipKey.TickIndex;

        public int OwnerEntityId => Target.Kind == PresentationTargetKind.Entity ? Target.EntityId : 0;
    }

    internal readonly struct GameplaySfxPlaybackResult
    {
        public GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind kind)
        {
            Kind = kind;
        }

        public GameplaySfxPlaybackResultKind Kind { get; }
    }

    internal readonly struct GameplaySfxExecutorDiagnostics
    {
        public GameplaySfxExecutorDiagnostics(
            int observedCueCount,
            int semanticUnsupportedCount,
            int mapMissingCount,
            int bindingMissingCount,
            int targetMissingCount,
            int ownerViewMissingCount,
            int portMissingCount,
            int duplicateSuppressedCount,
            int legacyOwnerNoOpCount,
            int requestPlannedCount,
            int playbackRequestedCount,
            int playbackSucceededCount,
            int playbackNoOpFallbackCount)
        {
            ObservedCueCount = Math.Max(0, observedCueCount);
            SemanticUnsupportedCount = Math.Max(0, semanticUnsupportedCount);
            MapMissingCount = Math.Max(0, mapMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            TargetMissingCount = Math.Max(0, targetMissingCount);
            OwnerViewMissingCount = Math.Max(0, ownerViewMissingCount);
            PortMissingCount = Math.Max(0, portMissingCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            LegacyOwnerNoOpCount = Math.Max(0, legacyOwnerNoOpCount);
            RequestPlannedCount = Math.Max(0, requestPlannedCount);
            PlaybackRequestedCount = Math.Max(0, playbackRequestedCount);
            PlaybackSucceededCount = Math.Max(0, playbackSucceededCount);
            PlaybackNoOpFallbackCount = Math.Max(0, playbackNoOpFallbackCount);
        }

        public int ObservedCueCount { get; }

        public int SemanticUnsupportedCount { get; }

        public int MapMissingCount { get; }

        public int BindingMissingCount { get; }

        public int TargetMissingCount { get; }

        public int OwnerViewMissingCount { get; }

        public int PortMissingCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int LegacyOwnerNoOpCount { get; }

        public int RequestPlannedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int PlaybackNoOpFallbackCount { get; }
    }

    internal interface IGameplaySfxPlaybackPort
    {
        bool TryPlayCoreGameplaySfx(
            in GameplaySfxPlaybackRequest request,
            out GameplaySfxPlaybackResult result);

        void ResetSession();

        void HardCleanup();
    }

    internal delegate GameplayPresentationPipeline CoreGameplaySfxExecutionPipelineFactory(
        CoreGameplaySfxExecutionMode mode,
        IGameplaySfxPlaybackPort playbackPort,
        CoreGameplaySfxExecutionGuard executionGuard);

    internal sealed class GameplaySfxPlaybackPortAdapter : IGameplaySfxPlaybackPort
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private GameplayAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;

        public GameplaySfxPlaybackPortAdapter(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort, GameplayAudioMap audioMap)
        {
            _playbackPort = playbackPort;
            _audioMap = audioMap;
        }

        public void DetachRuntime()
        {
            _playbackPort = null;
            _audioMap = null;
        }

        public bool TryPlayCoreGameplaySfx(
            in GameplaySfxPlaybackRequest request,
            out GameplaySfxPlaybackResult result)
        {
            if (_playbackPort == null)
            {
                result = new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.PortMissing);
                return false;
            }

            if (_audioMap == null)
            {
                result = new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.MapMissing);
                return false;
            }

            if (!TryMapSemantic(request.CueKey, out var semanticId))
            {
                result = new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.SemanticUnsupported);
                return false;
            }

            if (!_audioMap.TryResolve(semanticId, out var binding, out var failureKind))
            {
                result = new GameplaySfxPlaybackResult(MapFailure(failureKind));
                return false;
            }

            if (request.Target.Kind == PresentationTargetKind.None ||
                (request.Target.Kind == PresentationTargetKind.Entity && request.Target.EntityId <= 0))
            {
                result = new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.TargetMissing);
                return false;
            }

            var context = new AudioPlaybackContext(
                ownerEntityId: request.OwnerEntityId > 0 ? request.OwnerEntityId : (int?)null,
                debugTag: GameplayAudioSemanticCatalog.Format(semanticId));
            if (binding.HasAttachmentSlot &&
                TryResolveOwner(request.OwnerEntityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, context);
                result = new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.Succeeded);
                return true;
            }

            _playbackPort.Play2D(binding.Definition, context);
            result = binding.HasAttachmentSlot
                ? new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.OwnerViewMissing)
                : new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.Succeeded);
            return true;
        }

        public void ResetSession()
        {
        }

        public void HardCleanup()
        {
            DetachRuntime();
        }

        private bool TryResolveOwner(int ownerEntityId, out GameplayEntityView owner)
        {
            owner = null;
            if (ownerEntityId <= 0 ||
                !_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId, out owner) ||
                owner == null ||
                !owner.gameObject.activeInHierarchy)
            {
                owner = null;
                return false;
            }

            return true;
        }

        private static bool TryMapSemantic(PresentationSfxCueKey cueKey, out GameplayAudioSemanticId semanticId)
        {
            switch (cueKey)
            {
                case PresentationSfxCueKey.PlayerDamage:
                    semanticId = GameplayAudioSemanticId.PlayerDamage;
                    return true;
                case PresentationSfxCueKey.EnemyDamage:
                    semanticId = GameplayAudioSemanticId.EnemyDamage;
                    return true;
                case PresentationSfxCueKey.EntityExitItemConsume:
                    semanticId = GameplayAudioSemanticId.EntityExitItemConsume;
                    return true;
                case PresentationSfxCueKey.EntityExitBoxDestroy:
                    semanticId = GameplayAudioSemanticId.EntityExitBoxDestroy;
                    return true;
                case PresentationSfxCueKey.EntityExitEnemyDeath:
                    semanticId = GameplayAudioSemanticId.EntityExitEnemyDeath;
                    return true;
                case PresentationSfxCueKey.EntityExitOutOfBounds:
                    semanticId = GameplayAudioSemanticId.EntityExitOutOfBounds;
                    return true;
                default:
                    semanticId = GameplayAudioSemanticId.None;
                    return false;
            }
        }

        private static GameplaySfxPlaybackResultKind MapFailure(GameplayAudioMapResolveFailureKind failureKind)
        {
            switch (failureKind)
            {
                case GameplayAudioMapResolveFailureKind.UnsupportedSemantic:
                    return GameplaySfxPlaybackResultKind.SemanticUnsupported;
                case GameplayAudioMapResolveFailureKind.MapMissing:
                    return GameplaySfxPlaybackResultKind.MapMissing;
                case GameplayAudioMapResolveFailureKind.BindingMissing:
                case GameplayAudioMapResolveFailureKind.None:
                default:
                    return GameplaySfxPlaybackResultKind.BindingMissing;
            }
        }
    }

    internal sealed class GameplaySfxPresentationExecutor : IPresentationSfxBridgeExecutor
    {
        private readonly IGameplaySfxPlaybackPort _playbackPort;
        private readonly CoreGameplaySfxExecutionMode _mode;
        private readonly CoreGameplaySfxExecutionGuard _executionGuard;

        public GameplaySfxPresentationExecutor(
            IGameplaySfxPlaybackPort playbackPort = null,
            CoreGameplaySfxExecutionMode mode = CoreGameplaySfxExecutionMode.LegacyGameplayAudioController,
            CoreGameplaySfxExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = NormalizeMode(mode);
            _executionGuard = executionGuard;
        }

        public GameplaySfxExecutorDiagnostics Diagnostics { get; private set; }

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
            var semanticUnsupportedCount = 0;
            var mapMissingCount = 0;
            var bindingMissingCount = 0;
            var targetMissingCount = 0;
            var ownerViewMissingCount = 0;
            var portMissingCount = 0;
            var duplicateSuppressedCount = 0;
            var legacyOwnerNoOpCount = 0;
            var requestPlannedCount = 0;
            var playbackRequestedCount = 0;
            var playbackSucceededCount = 0;
            var playbackNoOpFallbackCount = 0;

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var cue = plan.Cues[i].Cue;
                if (!IsCoreGameplaySfxCue(cue))
                {
                    continue;
                }

                observedCueCount++;
                if (_mode != CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor)
                {
                    legacyOwnerNoOpCount++;
                    continue;
                }

                if (!TryCreateRequest(cue, out var request, out var missingKind))
                {
                    RecordResult(
                        missingKind,
                        ref semanticUnsupportedCount,
                        ref mapMissingCount,
                        ref bindingMissingCount,
                        ref targetMissingCount,
                        ref ownerViewMissingCount,
                        ref portMissingCount,
                        ref legacyOwnerNoOpCount,
                        ref playbackSucceededCount,
                        ref playbackNoOpFallbackCount);
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
                _playbackPort.TryPlayCoreGameplaySfx(request, out var result);
                RecordResult(
                    result.Kind,
                    ref semanticUnsupportedCount,
                    ref mapMissingCount,
                    ref bindingMissingCount,
                    ref targetMissingCount,
                    ref ownerViewMissingCount,
                    ref portMissingCount,
                    ref legacyOwnerNoOpCount,
                    ref playbackSucceededCount,
                    ref playbackNoOpFallbackCount);
            }

            Diagnostics = new GameplaySfxExecutorDiagnostics(
                observedCueCount,
                semanticUnsupportedCount,
                mapMissingCount,
                bindingMissingCount,
                targetMissingCount,
                ownerViewMissingCount,
                portMissingCount,
                duplicateSuppressedCount,
                legacyOwnerNoOpCount,
                requestPlannedCount,
                playbackRequestedCount,
                playbackSucceededCount,
                playbackNoOpFallbackCount);
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

        private bool TryClaimExecution(in CoreGameplaySfxPlaybackKey key)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    CoreGameplaySfxExecutionOwner.OrchestrationSfxBridgeExecutor,
                    key);
            }

            return _mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor;
        }

        private static bool IsCoreGameplaySfxCue(PresentationCue cue)
        {
            return cue.Domain == PresentationDomain.Sfx &&
                   cue.Key.Domain == PresentationDomain.Sfx &&
                   cue.Key.LocalKey > 0;
        }

        private static bool TryCreateRequest(
            PresentationCue cue,
            out GameplaySfxPlaybackRequest request,
            out GameplaySfxPlaybackResultKind missingKind)
        {
            request = default;
            missingKind = GameplaySfxPlaybackResultKind.None;
            if (!cue.Key.TryGetSfxCueKey(out var cueKey))
            {
                missingKind = GameplaySfxPlaybackResultKind.SemanticUnsupported;
                return false;
            }

            if (cue.Target.Kind == PresentationTargetKind.None ||
                (cue.Target.Kind == PresentationTargetKind.Entity && cue.Target.EntityId <= 0))
            {
                missingKind = GameplaySfxPlaybackResultKind.TargetMissing;
                return false;
            }

            var targetEntityId = cue.Target.Kind == PresentationTargetKind.Entity ? cue.Target.EntityId : 0;
            var key = new CoreGameplaySfxPlaybackKey(
                cue.Source.TickIndex,
                cue.Source.SemanticSource,
                cue.Source.SourceEntityId,
                targetEntityId,
                cueKey);
            request = new GameplaySfxPlaybackRequest(
                key,
                cueKey,
                cue.Source,
                cue.Target,
                cue.Anchor,
                cue.SfxPayload);
            return true;
        }

        private static void RecordResult(
            GameplaySfxPlaybackResultKind resultKind,
            ref int semanticUnsupportedCount,
            ref int mapMissingCount,
            ref int bindingMissingCount,
            ref int targetMissingCount,
            ref int ownerViewMissingCount,
            ref int portMissingCount,
            ref int legacyOwnerNoOpCount,
            ref int playbackSucceededCount,
            ref int playbackNoOpFallbackCount)
        {
            switch (resultKind)
            {
                case GameplaySfxPlaybackResultKind.SemanticUnsupported:
                    semanticUnsupportedCount++;
                    break;
                case GameplaySfxPlaybackResultKind.MapMissing:
                    mapMissingCount++;
                    break;
                case GameplaySfxPlaybackResultKind.BindingMissing:
                    bindingMissingCount++;
                    break;
                case GameplaySfxPlaybackResultKind.TargetMissing:
                    targetMissingCount++;
                    break;
                case GameplaySfxPlaybackResultKind.OwnerViewMissing:
                    ownerViewMissingCount++;
                    playbackSucceededCount++;
                    break;
                case GameplaySfxPlaybackResultKind.PortMissing:
                    portMissingCount++;
                    break;
                case GameplaySfxPlaybackResultKind.Succeeded:
                case GameplaySfxPlaybackResultKind.Requested:
                    playbackSucceededCount++;
                    break;
                case GameplaySfxPlaybackResultKind.LegacyOwnerActive:
                    legacyOwnerNoOpCount++;
                    break;
                case GameplaySfxPlaybackResultKind.NoOpFallback:
                    playbackNoOpFallbackCount++;
                    break;
            }
        }

        private static CoreGameplaySfxExecutionMode NormalizeMode(CoreGameplaySfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(CoreGameplaySfxExecutionMode), mode)
                ? mode
                : CoreGameplaySfxExecutionMode.LegacyGameplayAudioController;
        }
    }
}
