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
    internal readonly struct PlayerActionAnimationPreparation
    {
        public PlayerActionAnimationPreparation(
            bool suppressPlayerActionFieldsInSharedSync,
            int token)
        {
            SuppressPlayerActionFieldsInSharedSync = suppressPlayerActionFieldsInSharedSync;
            Token = Math.Max(0, token);
        }

        public bool SuppressPlayerActionFieldsInSharedSync { get; }

        internal int Token { get; }
    }

    internal sealed class PlayerActionAnimationLaneRuntime
    {
        private readonly PlayerActionAnimationExecutionGuard _executionGuard;
        private readonly PlayerActionAnimationExecutionPipelineFactory _pipelineFactory;
        private readonly IGameplayAnimationPlaybackPort _defaultPlaybackPort;

        private GameplayPresentationPipeline _executionPipeline;
        private IGameplayAnimationPlaybackPort _playbackPort;
        private int _nextPreparationToken;
        private int _lastPreparationToken;
        private int _lastConsumedPreparationToken;

        public PlayerActionAnimationLaneRuntime(
            GameplayPresentationStateStore stateStore,
            GameplayAnimationSyncCoordinator animationSync,
            Func<int, PlayerActionKind, float> resolveActionDurationSeconds,
            PlayerActionAnimationExecutionPipelineFactory pipelineFactory = null,
            PlayerActionAnimationExecutionGuard executionGuard = null,
            IGameplayAnimationPlaybackPort defaultPlaybackPort = null)
        {
            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            if (animationSync == null)
            {
                throw new ArgumentNullException(nameof(animationSync));
            }

            if (resolveActionDurationSeconds == null)
            {
                throw new ArgumentNullException(nameof(resolveActionDurationSeconds));
            }

            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreatePlayerActionAnimationExecutionPipeline;
            _executionGuard = executionGuard ?? new PlayerActionAnimationExecutionGuard();
            _defaultPlaybackPort = defaultPlaybackPort ??
                                   new GameplayAnimationSyncPlaybackPort(
                                       animationSync,
                                       stateStore,
                                       resolveActionDurationSeconds);
        }

        public PlayerActionAnimationExecutionMode ExecutionMode => _executionGuard.Diagnostics.Mode;

        public PlayerActionAnimationOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public GameplayAnimationExecutorDiagnostics ExecutorDiagnostics =>
            PlayerActionAnimationProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PlayerActionAnimationProductionTelemetrySnapshot ProductionTelemetrySnapshot =>
            PlayerActionAnimationProductionTelemetryBuilder.Build(
                ExecutionMode,
                _executionGuard.Diagnostics,
                _executionPipeline);

        public void ConfigureExecution(
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
            _executionGuard.Configure(PlayerActionAnimationExecutionPolicy.Normalize(mode));
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public PlayerActionAnimationPreparation Prepare(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var keys = BuildPlayerActionAnimationPlaybackKeys(result);
            _executionGuard.RecordPlanned(keys.Count);

            _lastPreparationToken = ++_nextPreparationToken;
            return new PlayerActionAnimationPreparation(true, _lastPreparationToken);
        }

        public void PresentPrepared(
            TickResult result,
            PlayerActionAnimationPreparation preparation)
        {
            if (result == null ||
                !preparation.SuppressPlayerActionFieldsInSharedSync ||
                preparation.Token == 0 ||
                preparation.Token != _lastPreparationToken ||
                preparation.Token == _lastConsumedPreparationToken)
            {
                return;
            }

            _lastConsumedPreparationToken = preparation.Token;
            _executionPipeline ??= CreateExecutionPipeline();
            _executionPipeline?.Present(result);
        }

        public void Update(float deltaTime)
        {
            _executionPipeline?.Update(deltaTime);
        }

        public void ResetSession()
        {
            ResetExecutionSession();
            _executionPipeline?.ResetSession();
        }

        public void HardCleanup()
        {
            _executionPipeline?.HardCleanup();
            ResetExecutionSession();
        }

        private void ResetExecutionSession()
        {
            _executionGuard.ResetSession();
            _lastPreparationToken = 0;
            _lastConsumedPreparationToken = 0;
        }

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _pipelineFactory(
                ExecutionMode,
                ResolvePlaybackPort(),
                _executionGuard);
        }

        private IGameplayAnimationPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPort ?? _defaultPlaybackPort;
        }

        private static IReadOnlyList<PlayerActionAnimationPlaybackKey> BuildPlayerActionAnimationPlaybackKeys(
            TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new AnimationCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<PlayerActionAnimationPlaybackKey>();
            }

            var keys = new List<PlayerActionAnimationPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Animation ||
                    !cue.Key.TryGetAnimationCueKey(out var cueKey) ||
                    !cue.AnimationPayload.IsValid ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                keys.Add(new PlayerActionAnimationPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Target.EntityId,
                    cueKey,
                    cue.AnimationPayload.ActionKind,
                    cue.AnimationPayload.PhaseKind,
                    cue.AnimationPayload.SourceSequenceId,
                    cue.AnimationPayload.SourceActionPlanId));
            }

            return keys.Count == 0
                ? Array.Empty<PlayerActionAnimationPlaybackKey>()
                : keys;
        }
    }
}
