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
            DamageDeathVfxOwnershipDiagnostics ownership,
            DamageDeathVfxExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot)
        {
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
        }

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
        private GameplayTimingProfile _timingProfile;
        private bool _usesDefaultPipelineFactory;

        public DamageDeathVfxPresentationLaneRuntime(
            DamageDeathVfxExecutionPipelineFactory pipelineFactory = null,
            IDamageDeathVfxPlaybackPort playbackPort = null,
            DamageDeathVfxExecutionGuard executionGuard = null)
        {
            _usesDefaultPipelineFactory = pipelineFactory == null;
            _pipelineFactory = pipelineFactory ?? CreateDefaultExecutionPipeline;
            _playbackPort = playbackPort;
            _executionGuard = executionGuard ?? new DamageDeathVfxExecutionGuard();
        }

        public DamageDeathVfxOwnershipDiagnostics OwnershipDiagnostics =>
            _executionGuard.Diagnostics;

        public DamageDeathVfxExecutorDiagnostics ExecutorDiagnostics =>
            DamageDeathVfxProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public DamageDeathVfxPresentationLaneDiagnostics Diagnostics =>
            new(
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot);

        public void ConfigurePlaybackPort(IDamageDeathVfxPlaybackPort playbackPort)
        {
            _playbackPort = playbackPort;
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public void ConfigureTiming(GameplayTimingProfile timingProfile)
        {
            _timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            if (!_usesDefaultPipelineFactory)
            {
                return;
            }

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

            _executionPipeline ??= CreateExecutionPipeline();
            _executionPipeline?.Present(result);
            ApplyPlannerOmissionDiagnostics();
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
                _playbackPort,
                _executionGuard);
        }

        private GameplayPresentationPipeline CreateDefaultExecutionPipeline(
            IDamageDeathVfxPlaybackPort playbackPort,
            DamageDeathVfxExecutionGuard executionGuard)
        {
            return GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline(
                playbackPort,
                executionGuard,
                _timingProfile);
        }

        private void ResetExecutionSession()
        {
            _executionGuard.ResetSession();
        }

        private void ApplyPlannerOmissionDiagnostics()
        {
            if (_executionPipeline?.LastCueFrame == null)
            {
                return;
            }

            var omittedByDeath = _executionPipeline.LastCueFrame
                .Diagnostics
                .DamageHitOmittedByEnemyDeathCount;
            if (omittedByDeath <= 0)
            {
                return;
            }

            DamageDeathVfxProductionTelemetryBuilder.RecordSameTickDamageHitOmittedByDeath(
                _executionPipeline,
                omittedByDeath);
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
