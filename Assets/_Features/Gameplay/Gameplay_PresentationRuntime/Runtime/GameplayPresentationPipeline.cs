using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;

namespace Game.Feature.Gameplay.PresentationRuntime
{
    public sealed class TickPresentationFactExtractor
    {
        public PresentationFactFrame Extract(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var presentationData = result.PresentationData ?? TickPresentationData.Empty;
            var facts = new List<PresentationFact>();
            var tickIndex = result.TickIndex;
            var topologyCount = 0;
            var combatCount = 0;
            var lifecycleCount = 0;
            var movementCount = 0;
            var tileCount = 0;
            var gravityCount = 0;
            var objectiveCount = 0;
            var stageCount = 0;

            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var motion = presentationData.EntityMotions[i];
                facts.Add(new PresentationFact(
                    PresentationFactKind.Movement,
                    new PresentationSource(tickIndex, PresentationSemanticSource.EntityMotion, motion.EntityId),
                    PresentationTarget.Entity(motion.EntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)motion.MotionKind,
                        primaryCell: motion.SourceCell,
                        secondaryCell: motion.DestinationCell,
                        hasPrimaryCell: true,
                        hasSecondaryCell: true)));
                movementCount++;
            }

            if (presentationData.TopologyMotion.HasValue)
            {
                var topologyMotion = presentationData.TopologyMotion.Value;
                facts.Add(new PresentationFact(
                    PresentationFactKind.Topology,
                    new PresentationSource(tickIndex, PresentationSemanticSource.TopologyMotion),
                    PresentationTarget.Topology(),
                    topologyPayload: new PresentationTopologyTransitionPayload(
                        topologyMotion.SourceTopology,
                        topologyMotion.DestinationTopology,
                        topologyMotion.RotationKind,
                        tickIndex)));
                topologyCount++;
            }

            for (var i = 0; i < presentationData.PlayerActionSignals.Count; i++)
            {
                var signal = presentationData.PlayerActionSignals[i];
                if (!signal.StartedThisTick &&
                    !signal.ExecutedThisTick &&
                    !signal.CompletedThisTick &&
                    !signal.CanceledThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.Action,
                    new PresentationSource(
                        tickIndex,
                        PresentationSemanticSource.PlayerAction,
                        signal.EntityId,
                        (int)signal.ActiveActionKind,
                        signal.ActiveActionSequence),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)signal.ResolutionKind,
                        secondaryValue: signal.TargetEntityId)));
            }

            for (var i = 0; i < presentationData.PlayerActionAttemptSignals.Count; i++)
            {
                var signal = presentationData.PlayerActionAttemptSignals[i];
                facts.Add(new PresentationFact(
                    PresentationFactKind.Action,
                    new PresentationSource(
                        tickIndex,
                        PresentationSemanticSource.PlayerActionAttempt,
                        signal.EntityId,
                        (int)signal.ActionKind),
                    signal.HasTarget ? PresentationTarget.Entity(signal.TargetEntityId) : PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)signal.FeedbackKind,
                        secondaryValue: signal.EmitsVisualFeedback ? 1 : 0)));
            }

            for (var i = 0; i < presentationData.PlayerDamageSignals.Count; i++)
            {
                var signal = presentationData.PlayerDamageSignals[i];
                if (!signal.TookDamageThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.Combat,
                    new PresentationSource(tickIndex, PresentationSemanticSource.PlayerDamage, signal.EntityId),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(primaryValue: signal.DamageAmount)));
                combatCount++;
            }

            for (var i = 0; i < presentationData.EnemyDamageSignals.Count; i++)
            {
                var signal = presentationData.EnemyDamageSignals[i];
                if (!signal.TookDamageThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.Combat,
                    new PresentationSource(tickIndex, PresentationSemanticSource.EnemyDamage, signal.EntityId),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(primaryValue: signal.DamageAmount)));
                combatCount++;
            }

            for (var i = 0; i < presentationData.PlayerDeathSignals.Count; i++)
            {
                var signal = presentationData.PlayerDeathSignals[i];
                if (!signal.DidDieThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.EntityLifecycle,
                    new PresentationSource(tickIndex, PresentationSemanticSource.PlayerDeath, signal.SourceEntityId),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(primaryValue: signal.DamageAmountAtFatalHit)));
                lifecycleCount++;
            }

            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.VisibilityChanges.Count,
                PresentationSemanticSource.EntityExit);
            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.TransitionVisibilityChanges.Count,
                PresentationSemanticSource.EntityExit);
            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.EntitySpawnSignals.Count,
                PresentationSemanticSource.EntitySpawn);
            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.EntityExitSignals.Count,
                PresentationSemanticSource.EntityExit);

            for (var i = 0; i < presentationData.TileEvents.Count; i++)
            {
                var tileEvent = presentationData.TileEvents[i];
                facts.Add(new PresentationFact(
                    PresentationFactKind.Tile,
                    new PresentationSource(tickIndex, PresentationSemanticSource.TileEvent, tileEvent.SourceEntityId),
                    PresentationTarget.SurfaceCell(tileEvent.Cell),
                    new PresentationFactPayload(
                        primaryValue: (int)tileEvent.EventKind,
                        secondaryValue: tileEvent.TileId,
                        tertiaryValue: (int)tileEvent.TileFeatureKind,
                        primaryCell: tileEvent.Cell,
                        hasPrimaryCell: true)));
                tileCount++;
            }

            for (var i = 0; i < presentationData.GravityFieldEvents.Count; i++)
            {
                var gravityEvent = presentationData.GravityFieldEvents[i];
                facts.Add(new PresentationFact(
                    PresentationFactKind.Gravity,
                    new PresentationSource(tickIndex, PresentationSemanticSource.GravityField, gravityEvent.EmitterEntityId),
                    PresentationTarget.SurfaceCell(gravityEvent.Cell),
                    new PresentationFactPayload(
                        primaryValue: (int)gravityEvent.EventKind,
                        secondaryValue: gravityEvent.TargetEntityId,
                        primaryCell: gravityEvent.Cell,
                        hasPrimaryCell: true)));
                gravityCount++;
            }

            if (result.ObjectiveResult.HasObjective)
            {
                if (result.ObjectiveResult.ClearedThisTick ||
                    result.ObjectiveResult.GoalReached ||
                    result.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick)
                {
                    facts.Add(new PresentationFact(
                        PresentationFactKind.Objective,
                        new PresentationSource(tickIndex, PresentationSemanticSource.ObjectiveResult),
                        PresentationTarget.Global(),
                        new PresentationFactPayload(
                            primaryValue: result.ObjectiveResult.ClearedThisTick ? 1 : 0,
                            secondaryValue: result.ObjectiveResult.GoalReached ? 1 : 0,
                            tertiaryValue: result.ObjectiveResult.ConditionStatuses.Count)));
                    objectiveCount++;
                }

                if (result.ObjectiveResult.ClearedThisTick)
                {
                    facts.Add(new PresentationFact(
                        PresentationFactKind.Stage,
                        new PresentationSource(tickIndex, PresentationSemanticSource.StageOutcome),
                        PresentationTarget.Global(),
                        new PresentationFactPayload(primaryValue: 1)));
                    stageCount++;
                }
            }

            return new PresentationFactFrame(
                tickIndex,
                facts,
                new PresentationFactFrameDiagnostics(
                    facts.Count,
                    topologyCount,
                    combatCount,
                    lifecycleCount,
                    movementCount,
                    tileCount,
                    gravityCount,
                    objectiveCount,
                    stageCount));
        }

        private static int AddEntityLifecycleFacts(
            List<PresentationFact> facts,
            int tickIndex,
            int count,
            PresentationSemanticSource semanticSource)
        {
            for (var i = 0; i < count; i++)
            {
                facts.Add(new PresentationFact(
                    PresentationFactKind.EntityLifecycle,
                    new PresentationSource(tickIndex, semanticSource, sourceSequence: i + 1),
                    PresentationTarget.Global(),
                    new PresentationFactPayload(primaryValue: count, secondaryValue: i + 1)));
            }

            return count;
        }
    }

    public sealed class GameplayPresentationPipeline
    {
        private static readonly IReadOnlyList<IPresentationExecutor> EmptyExecutors =
            new ReadOnlyCollection<IPresentationExecutor>(new List<IPresentationExecutor>());

        private readonly TickPresentationFactExtractor _factExtractor;
        private readonly PresentationCuePlannerSet _cuePlannerSet;
        private readonly PresentationPlaybackPlanner _playbackPlanner;
        private readonly PresentationPlaybackScheduler _scheduler;
        private readonly IReadOnlyList<IPresentationExecutor> _executors;

        public GameplayPresentationPipeline(
            TickPresentationFactExtractor factExtractor,
            PresentationCuePlannerSet cuePlannerSet,
            PresentationPlaybackPlanner playbackPlanner,
            PresentationPlaybackScheduler scheduler,
            IReadOnlyList<IPresentationExecutor> executors = null)
        {
            _factExtractor = factExtractor ?? throw new ArgumentNullException(nameof(factExtractor));
            _cuePlannerSet = cuePlannerSet ?? throw new ArgumentNullException(nameof(cuePlannerSet));
            _playbackPlanner = playbackPlanner ?? throw new ArgumentNullException(nameof(playbackPlanner));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _executors = executors == null || executors.Count == 0
                ? EmptyExecutors
                : new ReadOnlyCollection<IPresentationExecutor>(new List<IPresentationExecutor>(executors));
        }

        public PresentationFactFrame LastFactFrame { get; private set; }

        public PresentationCueFrame LastCueFrame { get; private set; }

        public PresentationPlaybackPlan LastPlaybackPlan { get; private set; }

        public IReadOnlyList<IPresentationExecutor> Executors => _executors;

        public PresentationPlaybackDiagnostics CurrentDiagnostics => _scheduler.CurrentDiagnostics;

        public int NoOpSchedulerAcceptCount => _scheduler.CurrentDiagnostics.NoOpSchedulerAcceptCount;

        public bool HasBlockingPresentation => _scheduler.HasBlockingPresentation;

        public void Present(TickResult result)
        {
            var factFrame = _factExtractor.Extract(result);
            var cueFrame = _cuePlannerSet.Plan(factFrame);
            var playbackPlan = _playbackPlanner.Plan(cueFrame);

            LastFactFrame = factFrame;
            LastCueFrame = cueFrame;
            LastPlaybackPlan = playbackPlan;
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.Prepare(playbackPlan);
            }

            _scheduler.Accept(playbackPlan);
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.Play(playbackPlan);
            }
        }

        public void Update(float deltaTime)
        {
            _scheduler.Update(deltaTime);
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.Update(deltaTime);
            }
        }

        public void ResetSession()
        {
            LastFactFrame = null;
            LastCueFrame = null;
            LastPlaybackPlan = null;
            _scheduler.ResetSession();
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.ResetSession();
            }
        }

        public void HardCleanup()
        {
            LastFactFrame = null;
            LastCueFrame = null;
            LastPlaybackPlan = null;
            _scheduler.HardCleanup();
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.HardCleanup();
            }
        }
    }

    public static class GameplayPresentationPipelineInstaller
    {
        public static GameplayPresentationPipeline CreateDiagnosticsOnly()
        {
            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new TopologyCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler());
        }
    }
}
