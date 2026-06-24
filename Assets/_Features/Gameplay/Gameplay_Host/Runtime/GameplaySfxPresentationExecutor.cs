using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.EnemyAudio;
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

    internal enum GameplaySfxFallbackReason
    {
        None = 0,
        OwnerViewMissingTwoDFallback = 1,
        EnemyDeathProfileSuppression = 2,
        LethalEnemyDamageSuppression = 3,
        SemanticUnsupported = 4,
        MapMissing = 5,
        BindingMissing = 6,
        TargetMissing = 7,
        PortMissing = 8,
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
            CoreGameplaySfxExecutionMode mode = CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor)
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
        private static readonly IReadOnlyList<GameplaySfxSemanticDiagnostics> EmptySemanticDiagnostics =
            Array.Empty<GameplaySfxSemanticDiagnostics>();

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
            : this(
                CoreGameplaySfxExecutionMode.LegacyGameplayAudioController,
                false,
                legacyOwnerNoOpCount,
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
                playbackNoOpFallbackCount,
                fallbackCount: playbackNoOpFallbackCount,
                attachedLikePlaybackCount: 0,
                twoDFallbackPlaybackCount: ownerViewMissingCount,
                deferredDuringTopologyLockCount: 0,
                deferredDrainCount: 0,
                enemyDeathGenericCoreSfxSuppressedCount: 0,
                lethalEnemyDamageSuppressedByDeathCount: 0,
                lastTickIndex: 0,
                lastSemanticKey: PresentationSfxCueKey.None,
                lastFallbackReason: ownerViewMissingCount > 0
                    ? GameplaySfxFallbackReason.OwnerViewMissingTwoDFallback
                    : GameplaySfxFallbackReason.None,
                semanticDiagnostics: EmptySemanticDiagnostics)
        {
        }

        public GameplaySfxExecutorDiagnostics(
            CoreGameplaySfxExecutionMode currentMode,
            bool isProductionDefaultOwner,
            int legacyOwnerSkippedByPolicyCount,
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
            int playbackNoOpFallbackCount,
            int fallbackCount,
            int attachedLikePlaybackCount,
            int twoDFallbackPlaybackCount,
            int deferredDuringTopologyLockCount,
            int deferredDrainCount,
            int enemyDeathGenericCoreSfxSuppressedCount,
            int lethalEnemyDamageSuppressedByDeathCount,
            int lastTickIndex,
            PresentationSfxCueKey lastSemanticKey,
            GameplaySfxFallbackReason lastFallbackReason,
            IReadOnlyList<GameplaySfxSemanticDiagnostics> semanticDiagnostics)
        {
            CurrentMode = Enum.IsDefined(typeof(CoreGameplaySfxExecutionMode), currentMode)
                ? currentMode
                : CoreGameplaySfxExecutionMode.LegacyGameplayAudioController;
            IsProductionDefaultOwner = isProductionDefaultOwner;
            LegacyOwnerSkippedByPolicyCount = Math.Max(0, legacyOwnerSkippedByPolicyCount);
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
            PlaybackRequestPlannedCount = RequestPlannedCount;
            FallbackCount = Math.Max(0, fallbackCount);
            AttachedLikePlaybackCount = Math.Max(0, attachedLikePlaybackCount);
            TwoDFallbackPlaybackCount = Math.Max(0, twoDFallbackPlaybackCount);
            DeferredDuringTopologyLockCount = Math.Max(0, deferredDuringTopologyLockCount);
            DeferredDrainCount = Math.Max(0, deferredDrainCount);
            EnemyDeathGenericCoreSfxSuppressedCount = Math.Max(0, enemyDeathGenericCoreSfxSuppressedCount);
            LethalEnemyDamageSuppressedByDeathCount = Math.Max(0, lethalEnemyDamageSuppressedByDeathCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastSemanticKey = lastSemanticKey;
            LastFallbackReason = lastFallbackReason;
            SemanticDiagnostics = semanticDiagnostics ?? EmptySemanticDiagnostics;
        }

        public CoreGameplaySfxExecutionMode CurrentMode { get; }

        public bool IsProductionDefaultOwner { get; }

        public int LegacyOwnerSkippedByPolicyCount { get; }

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

        public int PlaybackRequestPlannedCount { get; }

        public int PlaybackRequestedCount { get; }

        public int PlaybackSucceededCount { get; }

        public int PlaybackNoOpFallbackCount { get; }

        public int FallbackCount { get; }

        public int AttachedLikePlaybackCount { get; }

        public int TwoDFallbackPlaybackCount { get; }

        public int DeferredDuringTopologyLockCount { get; }

        public int DeferredDrainCount { get; }

        public int EnemyDeathGenericCoreSfxSuppressedCount { get; }

        public int LethalEnemyDamageSuppressedByDeathCount { get; }

        public int LastTickIndex { get; }

        public PresentationSfxCueKey LastSemanticKey { get; }

        public GameplaySfxFallbackReason LastFallbackReason { get; }

        public IReadOnlyList<GameplaySfxSemanticDiagnostics> SemanticDiagnostics { get; }
    }

    internal readonly struct GameplaySfxSemanticDiagnostics
    {
        public GameplaySfxSemanticDiagnostics(
            PresentationSfxCueKey cueKey,
            int plannedCount,
            int requestedCount,
            int succeededCount,
            int fallbackCount,
            int duplicateSuppressedCount,
            int lastDedupeKey)
        {
            CueKey = cueKey;
            PlannedCount = Math.Max(0, plannedCount);
            RequestedCount = Math.Max(0, requestedCount);
            SucceededCount = Math.Max(0, succeededCount);
            FallbackCount = Math.Max(0, fallbackCount);
            DuplicateSuppressedCount = Math.Max(0, duplicateSuppressedCount);
            LastDedupeKey = Math.Max(0, lastDedupeKey);
        }

        public PresentationSfxCueKey CueKey { get; }

        public int PlannedCount { get; }

        public int RequestedCount { get; }

        public int SucceededCount { get; }

        public int FallbackCount { get; }

        public int DuplicateSuppressedCount { get; }

        public int LastDedupeKey { get; }
    }

    internal readonly struct GameplaySfxPlaybackAdapterDiagnostics
    {
        public GameplaySfxPlaybackAdapterDiagnostics(
            int attachedLikePlaybackCount,
            int twoDFallbackPlaybackCount,
            int deferredDuringTopologyLockCount,
            int deferredDrainCount,
            int enemyDeathGenericCoreSfxSuppressedCount,
            int lethalEnemyDamageSuppressedByDeathCount,
            int lastTickIndex,
            PresentationSfxCueKey lastSemanticKey,
            GameplaySfxFallbackReason lastFallbackReason)
        {
            AttachedLikePlaybackCount = Math.Max(0, attachedLikePlaybackCount);
            TwoDFallbackPlaybackCount = Math.Max(0, twoDFallbackPlaybackCount);
            DeferredDuringTopologyLockCount = Math.Max(0, deferredDuringTopologyLockCount);
            DeferredDrainCount = Math.Max(0, deferredDrainCount);
            EnemyDeathGenericCoreSfxSuppressedCount = Math.Max(0, enemyDeathGenericCoreSfxSuppressedCount);
            LethalEnemyDamageSuppressedByDeathCount = Math.Max(0, lethalEnemyDamageSuppressedByDeathCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastSemanticKey = lastSemanticKey;
            LastFallbackReason = lastFallbackReason;
        }

        public int AttachedLikePlaybackCount { get; }

        public int TwoDFallbackPlaybackCount { get; }

        public int DeferredDuringTopologyLockCount { get; }

        public int DeferredDrainCount { get; }

        public int EnemyDeathGenericCoreSfxSuppressedCount { get; }

        public int LethalEnemyDamageSuppressedByDeathCount { get; }

        public int LastTickIndex { get; }

        public PresentationSfxCueKey LastSemanticKey { get; }

        public GameplaySfxFallbackReason LastFallbackReason { get; }
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
        private readonly List<GameplaySfxPlaybackRequest> _deferredRequests = new();
        private readonly HashSet<CoreGameplaySfxPlaybackKey> _deferredKeys = new();
        private readonly HashSet<CoreGameplaySfxPlaybackKey> _playedDeferredKeys = new();
        private readonly HashSet<int> _enemyDeathCueSuppressedEntityIds = new();
        private GameplayAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;
        private GameplayAudioPlaybackGateState _gateState = GameplayAudioPlaybackGateState.Open;
        private int _attachedLikePlaybackCount;
        private int _twoDFallbackPlaybackCount;
        private int _deferredDuringTopologyLockCount;
        private int _deferredDrainCount;
        private int _enemyDeathGenericCoreSfxSuppressedCount;
        private int _lethalEnemyDamageSuppressedByDeathCount;
        private int _lastTickIndex;
        private PresentationSfxCueKey _lastSemanticKey;
        private GameplaySfxFallbackReason _lastFallbackReason;

        public GameplaySfxPlaybackPortAdapter(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public int DeferredRequestCount => _deferredRequests.Count;

        public GameplaySfxPlaybackAdapterDiagnostics Diagnostics =>
            new(
                _attachedLikePlaybackCount,
                _twoDFallbackPlaybackCount,
                _deferredDuringTopologyLockCount,
                _deferredDrainCount,
                _enemyDeathGenericCoreSfxSuppressedCount,
                _lethalEnemyDamageSuppressedByDeathCount,
                _lastTickIndex,
                _lastSemanticKey,
                _lastFallbackReason);

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort, GameplayAudioMap audioMap)
        {
            _playbackPort = playbackPort;
            _audioMap = audioMap;
        }

        public void DetachRuntime()
        {
            _playbackPort = null;
            _audioMap = null;
            ClearDeferredRequests();
        }

        public void SetPlaybackGateState(GameplayAudioPlaybackGateState gateState)
        {
            _gateState = gateState;
        }

        public void ConfigureEnemyDeathCueSuppression(IReadOnlyCollection<int> entityIds)
        {
            _enemyDeathCueSuppressedEntityIds.Clear();
            if (entityIds == null)
            {
                return;
            }

            foreach (var entityId in entityIds)
            {
                if (entityId > 0)
                {
                    _enemyDeathCueSuppressedEntityIds.Add(entityId);
                }
            }
        }

        public void Update()
        {
            if (_gateState.IsBlocked || _deferredRequests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _deferredRequests.Count; i++)
            {
                var request = _deferredRequests[i];
                if (!ShouldSuppress(request, out var semanticId))
                {
                    PlayMappedRequest(request, semanticId);
                }

                _playedDeferredKeys.Add(request.OwnershipKey);
                _deferredDrainCount++;
            }

            _deferredRequests.Clear();
            _deferredKeys.Clear();
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

            if (ShouldSuppress(request, semanticId))
            {
                result = new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.NoOpFallback);
                return true;
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

            if (ShouldDefer(semanticId))
            {
                DeferRequest(request);
                result = new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.Requested);
                return true;
            }

            PlayMappedRequest(request, semanticId, binding);
            result = binding.HasAttachmentSlot &&
                     !TryResolveOwner(request.OwnerEntityId, out _)
                ? new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.OwnerViewMissing)
                : new GameplaySfxPlaybackResult(GameplaySfxPlaybackResultKind.Succeeded);
            return true;
        }

        public void ResetSession()
        {
            ClearDeferredRequests();
            _enemyDeathCueSuppressedEntityIds.Clear();
            _gateState = GameplayAudioPlaybackGateState.Open;
            _attachedLikePlaybackCount = 0;
            _twoDFallbackPlaybackCount = 0;
            _deferredDuringTopologyLockCount = 0;
            _deferredDrainCount = 0;
            _enemyDeathGenericCoreSfxSuppressedCount = 0;
            _lethalEnemyDamageSuppressedByDeathCount = 0;
            _lastTickIndex = 0;
            _lastSemanticKey = PresentationSfxCueKey.None;
            _lastFallbackReason = GameplaySfxFallbackReason.None;
        }

        public void HardCleanup()
        {
            ResetSession();
            DetachRuntime();
        }

        private void PlayMappedRequest(
            in GameplaySfxPlaybackRequest request,
            GameplayAudioSemanticId semanticId)
        {
            var binding = _audioMap.ResolveOrThrow(semanticId);
            PlayMappedRequest(request, semanticId, binding);
        }

        private void PlayMappedRequest(
            in GameplaySfxPlaybackRequest request,
            GameplayAudioSemanticId semanticId,
            AudioBinding binding)
        {
            var context = new AudioPlaybackContext(
                ownerEntityId: request.OwnerEntityId > 0 ? request.OwnerEntityId : (int?)null,
                debugTag: GameplayAudioSemanticCatalog.Format(semanticId));
            if (binding.HasAttachmentSlot &&
                TryResolveOwner(request.OwnerEntityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, context);
                _attachedLikePlaybackCount++;
                RecordLast(request, semanticId, GameplaySfxFallbackReason.None);
                return;
            }

            _playbackPort.Play2D(binding.Definition, context);
            if (binding.HasAttachmentSlot)
            {
                _twoDFallbackPlaybackCount++;
                RecordLast(request, semanticId, GameplaySfxFallbackReason.OwnerViewMissingTwoDFallback);
            }
            else
            {
                RecordLast(request, semanticId, GameplaySfxFallbackReason.None);
            }
        }

        private void DeferRequest(in GameplaySfxPlaybackRequest request)
        {
            if (_deferredKeys.Contains(request.OwnershipKey) ||
                _playedDeferredKeys.Contains(request.OwnershipKey))
            {
                return;
            }

            _deferredRequests.Add(request);
            _deferredKeys.Add(request.OwnershipKey);
            _deferredDuringTopologyLockCount++;
            RecordLast(request, GameplayAudioSemanticId.None, GameplaySfxFallbackReason.None);
        }

        private void ClearDeferredRequests()
        {
            _deferredRequests.Clear();
            _deferredKeys.Clear();
            _playedDeferredKeys.Clear();
        }

        private bool ShouldDefer(GameplayAudioSemanticId semanticId)
        {
            return _gateState.IsBlocked &&
                   _gateState.Reason == GameplayAudioPlaybackBlockReason.TopologyPresentationLock &&
                   IsTopologyLockSensitive(semanticId);
        }

        private bool ShouldSuppress(
            in GameplaySfxPlaybackRequest request,
            out GameplayAudioSemanticId semanticId)
        {
            if (!TryMapSemantic(request.CueKey, out semanticId))
            {
                return false;
            }

            return ShouldSuppress(request, semanticId);
        }

        private bool ShouldSuppress(
            in GameplaySfxPlaybackRequest request,
            GameplayAudioSemanticId semanticId)
        {
            if (semanticId == GameplayAudioSemanticId.EnemyDamage &&
                request.OwnerEntityId > 0 &&
                _enemyDeathCueSuppressedEntityIds.Contains(request.OwnerEntityId))
            {
                _lethalEnemyDamageSuppressedByDeathCount++;
                RecordLast(
                    request,
                    semanticId,
                    GameplaySfxFallbackReason.LethalEnemyDamageSuppression);
                return true;
            }

            if (semanticId == GameplayAudioSemanticId.EntityExitEnemyDeath &&
                ShouldSuppressGenericEnemyDeath(request.OwnerEntityId))
            {
                _enemyDeathGenericCoreSfxSuppressedCount++;
                RecordLast(
                    request,
                    semanticId,
                    GameplaySfxFallbackReason.EnemyDeathProfileSuppression);
                return true;
            }

            return false;
        }

        private bool ShouldSuppressGenericEnemyDeath(int ownerEntityId)
        {
            if (!TryResolveOwner(ownerEntityId, out var owner))
            {
                return false;
            }

            var authoring = EnemyAudioAuthoring.GetOptionalValidatedAuthoring(owner);
            return authoring != null &&
                   authoring.Profile.HasCue(EnemyAudioCue.Death);
        }

        private static bool IsTopologyLockSensitive(GameplayAudioSemanticId semanticId)
        {
            return semanticId == GameplayAudioSemanticId.PlayerDamage ||
                   semanticId == GameplayAudioSemanticId.EnemyDamage;
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

        private void RecordLast(
            in GameplaySfxPlaybackRequest request,
            GameplayAudioSemanticId semanticId,
            GameplaySfxFallbackReason fallbackReason)
        {
            _lastTickIndex = request.TickIndex;
            _lastSemanticKey = request.CueKey;
            _lastFallbackReason = fallbackReason;
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
            CoreGameplaySfxExecutionMode mode = CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor,
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
            var fallbackCount = 0;
            var lastTickIndex = 0;
            var lastSemanticKey = PresentationSfxCueKey.None;
            var lastFallbackReason = GameplaySfxFallbackReason.None;
            var semanticCounters = new Dictionary<PresentationSfxCueKey, SemanticTelemetryCounter>();

            for (var i = 0; i < plan.Cues.Count; i++)
            {
                var playbackCue = plan.Cues[i];
                var cue = playbackCue.Cue;
                if (!IsCoreGameplaySfxCue(cue))
                {
                    continue;
                }

                observedCueCount++;
                if (cue.Key.TryGetSfxCueKey(out var observedCueKey))
                {
                    lastSemanticKey = observedCueKey;
                }

                if (_mode != CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor)
                {
                    legacyOwnerNoOpCount++;
                    continue;
                }

                if (!TryCreateRequest(cue, out var request, out var missingKind))
                {
                    lastTickIndex = Math.Max(0, cue.Source.TickIndex);
                    lastFallbackReason = ResolveFallbackReason(missingKind);
                    if (lastFallbackReason != GameplaySfxFallbackReason.None)
                    {
                        fallbackCount++;
                    }

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
                lastTickIndex = request.TickIndex;
                lastSemanticKey = request.CueKey;
                GetOrCreateSemanticCounter(semanticCounters, request.CueKey)
                    .RecordPlanned(playbackCue.Policy.DedupeKey);
                var duplicateBefore = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? 0;
                if (!TryClaimExecution(request.OwnershipKey))
                {
                    var duplicateAfter = _executionGuard?.Diagnostics.DuplicateAttemptCount ?? duplicateBefore;
                    if (duplicateAfter > duplicateBefore)
                    {
                        duplicateSuppressedCount++;
                        GetOrCreateSemanticCounter(semanticCounters, request.CueKey).DuplicateSuppressedCount++;
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
                GetOrCreateSemanticCounter(semanticCounters, request.CueKey).RequestedCount++;
                _playbackPort.TryPlayCoreGameplaySfx(request, out var result);
                var fallbackReason = ResolveFallbackReason(result.Kind);
                if (fallbackReason != GameplaySfxFallbackReason.None)
                {
                    fallbackCount++;
                    lastFallbackReason = fallbackReason;
                    GetOrCreateSemanticCounter(semanticCounters, request.CueKey).FallbackCount++;
                }

                if (IsSuccessResult(result.Kind))
                {
                    GetOrCreateSemanticCounter(semanticCounters, request.CueKey).SucceededCount++;
                }

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
                currentMode: _mode,
                isProductionDefaultOwner: _mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor,
                legacyOwnerSkippedByPolicyCount: 0,
                observedCueCount: observedCueCount,
                semanticUnsupportedCount: semanticUnsupportedCount,
                mapMissingCount: mapMissingCount,
                bindingMissingCount: bindingMissingCount,
                targetMissingCount: targetMissingCount,
                ownerViewMissingCount: ownerViewMissingCount,
                portMissingCount: portMissingCount,
                duplicateSuppressedCount: duplicateSuppressedCount,
                legacyOwnerNoOpCount: legacyOwnerNoOpCount,
                requestPlannedCount: requestPlannedCount,
                playbackRequestedCount: playbackRequestedCount,
                playbackSucceededCount: playbackSucceededCount,
                playbackNoOpFallbackCount: playbackNoOpFallbackCount,
                fallbackCount: fallbackCount,
                attachedLikePlaybackCount: 0,
                twoDFallbackPlaybackCount: ownerViewMissingCount,
                deferredDuringTopologyLockCount: 0,
                deferredDrainCount: 0,
                enemyDeathGenericCoreSfxSuppressedCount: 0,
                lethalEnemyDamageSuppressedByDeathCount: 0,
                lastTickIndex: lastTickIndex,
                lastSemanticKey: lastSemanticKey,
                lastFallbackReason: lastFallbackReason,
                semanticDiagnostics: BuildSemanticDiagnostics(semanticCounters));
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

        private static bool IsSuccessResult(GameplaySfxPlaybackResultKind resultKind)
        {
            return resultKind == GameplaySfxPlaybackResultKind.Succeeded ||
                   resultKind == GameplaySfxPlaybackResultKind.Requested ||
                   resultKind == GameplaySfxPlaybackResultKind.OwnerViewMissing;
        }

        private static GameplaySfxFallbackReason ResolveFallbackReason(GameplaySfxPlaybackResultKind resultKind)
        {
            switch (resultKind)
            {
                case GameplaySfxPlaybackResultKind.SemanticUnsupported:
                    return GameplaySfxFallbackReason.SemanticUnsupported;
                case GameplaySfxPlaybackResultKind.MapMissing:
                    return GameplaySfxFallbackReason.MapMissing;
                case GameplaySfxPlaybackResultKind.BindingMissing:
                    return GameplaySfxFallbackReason.BindingMissing;
                case GameplaySfxPlaybackResultKind.TargetMissing:
                    return GameplaySfxFallbackReason.TargetMissing;
                case GameplaySfxPlaybackResultKind.OwnerViewMissing:
                    return GameplaySfxFallbackReason.OwnerViewMissingTwoDFallback;
                case GameplaySfxPlaybackResultKind.PortMissing:
                    return GameplaySfxFallbackReason.PortMissing;
                case GameplaySfxPlaybackResultKind.NoOpFallback:
                    return GameplaySfxFallbackReason.EnemyDeathProfileSuppression;
                default:
                    return GameplaySfxFallbackReason.None;
            }
        }

        private static SemanticTelemetryCounter GetOrCreateSemanticCounter(
            IDictionary<PresentationSfxCueKey, SemanticTelemetryCounter> counters,
            PresentationSfxCueKey cueKey)
        {
            if (!counters.TryGetValue(cueKey, out var counter))
            {
                counter = new SemanticTelemetryCounter(cueKey);
                counters.Add(cueKey, counter);
            }

            return counter;
        }

        private static IReadOnlyList<GameplaySfxSemanticDiagnostics> BuildSemanticDiagnostics(
            Dictionary<PresentationSfxCueKey, SemanticTelemetryCounter> counters)
        {
            if (counters.Count == 0)
            {
                return Array.Empty<GameplaySfxSemanticDiagnostics>();
            }

            var diagnostics = new GameplaySfxSemanticDiagnostics[counters.Count];
            var index = 0;
            foreach (var counter in counters.Values)
            {
                diagnostics[index++] = counter.ToDiagnostics();
            }

            Array.Sort(
                diagnostics,
                (left, right) => ((int)left.CueKey).CompareTo((int)right.CueKey));
            return diagnostics;
        }

        private static CoreGameplaySfxExecutionMode NormalizeMode(CoreGameplaySfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(CoreGameplaySfxExecutionMode), mode)
                ? mode
                : CoreGameplaySfxExecutionMode.LegacyGameplayAudioController;
        }

        private sealed class SemanticTelemetryCounter
        {
            public SemanticTelemetryCounter(PresentationSfxCueKey cueKey)
            {
                CueKey = cueKey;
            }

            private PresentationSfxCueKey CueKey { get; }

            private int PlannedCount { get; set; }

            public int RequestedCount { get; set; }

            public int SucceededCount { get; set; }

            public int FallbackCount { get; set; }

            public int DuplicateSuppressedCount { get; set; }

            private int LastDedupeKey { get; set; }

            public void RecordPlanned(int dedupeKey)
            {
                PlannedCount++;
                LastDedupeKey = Math.Max(0, dedupeKey);
            }

            public GameplaySfxSemanticDiagnostics ToDiagnostics()
            {
                return new GameplaySfxSemanticDiagnostics(
                    CueKey,
                    PlannedCount,
                    RequestedCount,
                    SucceededCount,
                    FallbackCount,
                    DuplicateSuppressedCount,
                    LastDedupeKey);
            }
        }
    }
}
