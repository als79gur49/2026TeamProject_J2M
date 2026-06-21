using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct DamageDeathVfxPresentationLaneDiagnostics
    {
        public DamageDeathVfxPresentationLaneDiagnostics(
            DamageDeathVfxExecutionMode executionMode,
            DamageDeathVfxOwnershipDiagnostics ownership,
            DamageDeathVfxExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot)
        {
            ExecutionMode = executionMode;
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
        }

        public DamageDeathVfxExecutionMode ExecutionMode { get; }

        public DamageDeathVfxOwnershipDiagnostics Ownership { get; }

        public DamageDeathVfxExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }
    }

    internal sealed class DamageDeathVfxPresentationLaneRuntime
    {
        private static readonly IReadOnlyList<DamageDeathVfxPlaybackKey> EmptyPlaybackKeys =
            Array.Empty<DamageDeathVfxPlaybackKey>();

        private readonly DamageDeathVfxExecutionGuard _executionGuard;
        private readonly DamageDeathVfxExecutionPipelineFactory _pipelineFactory;

        private GameplayPresentationPipeline _executionPipeline;
        private IDamageDeathVfxPlaybackPort _playbackPort;

        public DamageDeathVfxPresentationLaneRuntime(
            DamageDeathVfxExecutionPipelineFactory pipelineFactory = null,
            DamageDeathVfxExecutionGuard executionGuard = null)
        {
            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline;
            _executionGuard = executionGuard ?? new DamageDeathVfxExecutionGuard();
        }

        public DamageDeathVfxExecutionMode ExecutionMode =>
            _executionGuard.Diagnostics.Mode;

        public DamageDeathVfxOwnershipDiagnostics OwnershipDiagnostics =>
            _executionGuard.Diagnostics;

        public DamageDeathVfxExecutorDiagnostics ExecutorDiagnostics =>
            DamageDeathVfxProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public DamageDeathVfxPresentationLaneDiagnostics Diagnostics =>
            new(
                ExecutionMode,
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot);

        public void ConfigureExecution(
            DamageDeathVfxExecutionMode mode,
            IDamageDeathVfxPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
            _executionGuard.Configure(DamageDeathVfxExecutionPolicy.Normalize(mode));
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            var keys = BuildDamageDeathVfxPlaybackKeys(result);
            if (UseProductionExecutor())
            {
                RecordLegacySkippedByPolicy(keys);
                _executionPipeline ??= CreateExecutionPipeline();
                _executionPipeline?.Present(result);
                ApplyPlannerSuppressionDiagnostics();
                return;
            }

            RecordLegacyOwnership(keys);
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

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _pipelineFactory(
                ExecutionMode,
                _playbackPort,
                _executionGuard);
        }

        private void ResetExecutionSession()
        {
            _executionGuard.ResetSession();
        }

        private bool UseProductionExecutor()
        {
            return ExecutionMode == DamageDeathVfxExecutionPolicy.ProductionDefault;
        }

        private void RecordLegacyOwnership(IReadOnlyList<DamageDeathVfxPlaybackKey> playbackKeys)
        {
            for (var i = 0; i < playbackKeys.Count; i++)
            {
                _executionGuard.TryBeginExecution(
                    DamageDeathVfxExecutionOwner.LegacyExtension,
                    playbackKeys[i]);
            }
        }

        private void RecordLegacySkippedByPolicy(IReadOnlyList<DamageDeathVfxPlaybackKey> playbackKeys)
        {
            for (var i = 0; i < playbackKeys.Count; i++)
            {
                _executionGuard.RecordSkippedByPolicy(
                    DamageDeathVfxExecutionOwner.LegacyExtension);
            }
        }

        private void ApplyPlannerSuppressionDiagnostics()
        {
            if (_executionPipeline?.LastCueFrame == null)
            {
                return;
            }

            var suppressedByDeath = _executionPipeline.LastCueFrame
                .Diagnostics
                .DamageHitSuppressedByEnemyDeathCount;
            if (suppressedByDeath <= 0)
            {
                return;
            }

            DamageDeathVfxProductionTelemetryBuilder.RecordSameTickDamageHitSuppressedByDeath(
                _executionPipeline,
                suppressedByDeath);
        }

        private static IReadOnlyList<DamageDeathVfxPlaybackKey> BuildDamageDeathVfxPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new VfxCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return EmptyPlaybackKeys;
            }

            var keys = new List<DamageDeathVfxPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Vfx ||
                    !cue.Key.TryGetVfxCueKey(out var cueKey) ||
                    (cueKey != PresentationVfxCueKey.DamageHit &&
                     cueKey != PresentationVfxCueKey.EnemyDeath) ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                keys.Add(new DamageDeathVfxPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Source.SourceEntityId,
                    cue.Target.EntityId,
                    cueKey));
            }

            return keys.Count == 0
                ? EmptyPlaybackKeys
                : keys;
        }
    }
}
