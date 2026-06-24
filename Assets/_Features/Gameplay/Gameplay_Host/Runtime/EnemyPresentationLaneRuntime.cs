using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct EnemyPresentationPreparation
    {
        public EnemyPresentationPreparation(
            EnemyPresentationLegacyOneShotSuppression legacyOneShotSuppression,
            int tickIndex,
            int token)
        {
            LegacyOneShotSuppression = legacyOneShotSuppression;
            TickIndex = Math.Max(0, tickIndex);
            Token = Math.Max(0, token);
        }

        public EnemyPresentationLegacyOneShotSuppression LegacyOneShotSuppression { get; }

        internal int TickIndex { get; }

        internal int Token { get; }
    }

    internal sealed class EnemyPresentationLaneRuntime
    {
        private readonly EnemyPresentationExecutionGuard _executionGuard;
        private readonly EnemyPresentationExecutionPipelineFactory _pipelineFactory;
        private readonly IGameplayEnemyPresentationPlaybackPort _defaultPlaybackPort;

        private GameplayPresentationPipeline _executionPipeline;
        private IGameplayEnemyPresentationPlaybackPort _playbackPort;
        private int _nextPreparationToken;
        private int _lastPreparationToken;
        private int _lastConsumedPreparationToken;

        public EnemyPresentationLaneRuntime(
            GameplayPresentationStateStore stateStore,
            GameplayAnimationSyncCoordinator animationSync,
            EnemyPresentationExecutionPipelineFactory pipelineFactory = null,
            EnemyPresentationExecutionGuard executionGuard = null,
            IGameplayEnemyPresentationPlaybackPort defaultPlaybackPort = null)
        {
            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            if (animationSync == null)
            {
                throw new ArgumentNullException(nameof(animationSync));
            }

            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateEnemyPresentationExecutionPipeline;
            _executionGuard = executionGuard ?? new EnemyPresentationExecutionGuard();
            _defaultPlaybackPort = defaultPlaybackPort ??
                                   new GameplayEnemyPresentationSyncPlaybackPort(animationSync, stateStore);
        }

        public EnemyPresentationExecutionMode ExecutionMode => _executionGuard.Diagnostics.Mode;

        public EnemyPresentationOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public GameplayEnemyPresentationExecutorDiagnostics ExecutorDiagnostics =>
            EnemyPresentationProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public EnemyPresentationProductionTelemetrySnapshot ProductionTelemetrySnapshot =>
            EnemyPresentationProductionTelemetryBuilder.Build(
                ExecutionMode,
                _executionGuard.Diagnostics,
                _executionPipeline);

        public void ConfigureExecution(
            EnemyPresentationExecutionMode mode,
            IGameplayEnemyPresentationPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
            _executionGuard.Configure(EnemyPresentationExecutionPolicy.Normalize(mode));
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public EnemyPresentationPreparation Prepare(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var keys = BuildEnemyPresentationPlaybackKeys(result);
            var useProductionExecutor = UseProductionExecutor();
            if (useProductionExecutor)
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    _executionGuard.RecordSkippedByPolicy(
                        EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper);
                }
            }
            else
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    _executionGuard.TryBeginExecution(
                        EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper,
                        keys[i]);
                }
            }

            _lastPreparationToken = ++_nextPreparationToken;
            return new EnemyPresentationPreparation(
                useProductionExecutor
                    ? BuildLegacyOneShotSuppression(result)
                    : EnemyPresentationLegacyOneShotSuppression.None,
                result.TickIndex,
                _lastPreparationToken);
        }

        public void PresentPrepared(
            TickResult result,
            EnemyPresentationPreparation preparation)
        {
            if (result == null ||
                !UseProductionExecutor() ||
                preparation.Token == 0 ||
                preparation.Token != _lastPreparationToken ||
                preparation.Token == _lastConsumedPreparationToken ||
                preparation.TickIndex != result.TickIndex)
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

        private IGameplayEnemyPresentationPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPort ?? _defaultPlaybackPort;
        }

        private bool UseProductionExecutor()
        {
            return ExecutionMode == EnemyPresentationExecutionDefaults.ProductionDefault;
        }

        private static IReadOnlyList<EnemyPresentationPlaybackKey> BuildEnemyPresentationPlaybackKeys(
            TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyPresentationCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<EnemyPresentationPlaybackKey>();
            }

            var keys = new List<EnemyPresentationPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Animation ||
                    !cue.Key.TryGetAnimationCueKey(out var cueKey) ||
                    !cue.EnemyPayload.IsValid ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                keys.Add(new EnemyPresentationPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Target.EntityId,
                    cueKey,
                    cue.EnemyPayload.Kind,
                    cue.EnemyPayload.Phase,
                    cue.EnemyPayload.SourceSequenceId));
            }

            return keys.Count == 0
                ? Array.Empty<EnemyPresentationPlaybackKey>()
                : keys;
        }

        private static EnemyPresentationLegacyOneShotSuppression BuildLegacyOneShotSuppression(TickResult result)
        {
            var suppression = EnemyPresentationLegacyOneShotSuppression.None;
            var presentationData = result.PresentationData;

            var jumpSignals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < jumpSignals.Count; i++)
            {
                var signal = jumpSignals[i];
                if (signal.StartedWindupThisTick)
                {
                    suppression |= EnemyPresentationLegacyOneShotSuppression.JumpWindup;
                }

                if (signal.StartedAirborneThisTick || signal.RetryThisTick)
                {
                    suppression |= EnemyPresentationLegacyOneShotSuppression.JumpAirborneStartOrRetry;
                }

                if (signal.LandedThisTick)
                {
                    suppression |= EnemyPresentationLegacyOneShotSuppression.JumpLand;
                }
            }

            var chargeSignals = presentationData.EnemyChargeSignals;
            for (var i = 0; i < chargeSignals.Count; i++)
            {
                var signal = chargeSignals[i];
                if (signal.StartedWindupThisTick)
                {
                    suppression |= EnemyPresentationLegacyOneShotSuppression.ChargeWindup;
                }

                if (signal.StartedActiveThisTick)
                {
                    suppression |= EnemyPresentationLegacyOneShotSuppression.ChargeActiveStart;
                }

                if (signal.StartedRecoverThisTick)
                {
                    suppression |= EnemyPresentationLegacyOneShotSuppression.ChargeRecover;
                }
            }

            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                if (exitSignals[i].ExitCause == TickEntityExitCause.EnemyDeath)
                {
                    suppression |= EnemyPresentationLegacyOneShotSuppression.DeathTrigger;
                }
            }

            return suppression;
        }
    }
}
