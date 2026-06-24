using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayPresentationRuntimeCompositionFactoryOptions
    {
        public TopologyExecutionPipelineFactory TopologyExecutionPipelineFactory { get; set; }
        public DamageDeathVfxExecutionPipelineFactory DamageDeathVfxExecutionPipelineFactory { get; set; }
        public IDamageDeathVfxPlaybackPort DamageDeathVfxPlaybackPort { get; set; }
        public BoxMotionExecutionPipelineFactory BoxMotionExecutionPipelineFactory { get; set; }
        public PlayerActionAnimationExecutionPipelineFactory PlayerActionAnimationExecutionPipelineFactory { get; set; }
        public EnemyPresentationExecutionPipelineFactory EnemyPresentationExecutionPipelineFactory { get; set; }
        public CoreGameplaySfxExecutionPipelineFactory CoreGameplaySfxExecutionPipelineFactory { get; set; }
        public ActionAudioExecutionPipelineFactory ActionAudioExecutionPipelineFactory { get; set; }
        public EnemyAudioExecutionPipelineFactory EnemyAudioExecutionPipelineFactory { get; set; }
    }

    internal static class GameplayPresentationRuntimeCompositionFactory
    {
        public static GameplayPresentationRuntimeComposition Create(
            GameplayPresentationRuntimeCompositionFactoryOptions options = null)
        {
            options ??= new GameplayPresentationRuntimeCompositionFactoryOptions();

            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            var animationSync = new GameplayAnimationSyncCoordinator();
            var enemyVisualSemanticResolver = new DefaultEnemyVisualSemanticResolver();
            var blockAudioRequestPlanner = new BlockAudioRequestPlanner();
            var gravityFieldAudioRequestPlanner = new GravityFieldAudioRequestPlanner();
            var gravityFieldPresentationRequestPlanner = new GravityFieldPresentationRequestPlanner();
            var summonedEnemyPresentationResolver = new SummonedEnemyPresentationResolver();
            var tileFeatureAudioRequestPlanner = new TileFeatureAudioRequestPlanner();
            var topologyAudioRequestPlanner = new TopologyAudioRequestPlanner();
            var topologyAudioPresentationController = new TopologyAudioPresentationController();
            var tilePresentationRequestPlanner = new TilePresentationRequestPlanner();
            var presentationPauseRegistry = new GameplayPresentationPauseRegistry();
            var utilityWindupVfxPresenter = new GameplayUtilityWindupVfxPresenter();
            var tileFeatureVisualPresentationController = new TileFeatureVisualPresentationController();
            var moonBlockEmergencePresentationController = new MoonBlockEmergencePresentationController();
            var gameplaySfxArbiter = new GameplaySfxArbiter();

            var coreGameplaySfxLane = new CoreGameplaySfxLaneRuntime(
                stateStore,
                options.CoreGameplaySfxExecutionPipelineFactory);
            var gameplayActionAudioLane = new GameplayActionAudioLaneRuntime(
                stateStore,
                options.ActionAudioExecutionPipelineFactory);
            var enemyOneShotAudioLane = new EnemyOneShotAudioLaneRuntime(
                stateStore,
                options.EnemyAudioExecutionPipelineFactory);
            var enemyChargeLoopAudioPresentationController = new EnemyChargeLoopAudioPresentationController(stateStore);
            var blockAudioPresentationController = new BlockAudioPresentationController(stateStore);
            var playerLocomotionAudioPresentationController =
                new PlayerLocomotionAudioPresentationController(stateStore);
            var tileFeatureAudioPresentationController = new TileFeatureAudioPresentationController(stateStore);
            var gravityFieldAudioPresentationController = new GravityFieldAudioPresentationController(stateStore);
            var gravityFieldVisualPresentationController = new GravityFieldVisualPresentationController(stateStore);
            var presentationActivityInspector = new GameplayPresentationActivityInspector(trackState);
            var motionTimingResolver = new GameplayMotionTimingResolver(stateStore, trackState);
            var topologyTransitionController = new GameplayTopologyTransitionController(motionTimingResolver);
            var topologyLane = new TopologyPresentationLaneRuntime(
                options.TopologyExecutionPipelineFactory ??
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                new GameplayTopologyTransitionPlaybackPort(topologyTransitionController),
                new GameplayTopologyLegacyTransitionPort(topologyTransitionController),
                new GameplayTopologyTransitionCleanupPort(topologyTransitionController));
            var damageDeathVfxLane = new DamageDeathVfxPresentationLaneRuntime(
                options.DamageDeathVfxExecutionPipelineFactory);
            var poseResolver = new GameplayPoseResolver(
                stateStore,
                trackState);
            var committedFrameBuilder = new GameplayCommittedFrameBuilder(
                stateStore,
                poseResolver,
                animationSync);
            var entityPresentationApplier = new GameplayEntityPresentationApplier(
                stateStore,
                trackState,
                poseResolver,
                animationSync,
                motionTimingResolver,
                enemyVisualSemanticResolver,
                committedFrameBuilder);
            var exitPresentationController = new GameplayExitPresentationController(
                animationSync,
                stateStore,
                trackState);
            var destroyShrinkVfxSequenceStateResolver = new GameplayDestroyShrinkVfxSequenceStateResolver();
            var playerActionAnimationTimingProfileSource = new GameplayPlayerActionAnimationTimingProfileSource();
            var moonBlockDestructionPresentationController = new MoonBlockDestructionPresentationController(
                stateStore,
                trackState,
                exitPresentationController,
                destroyShrinkVfxSequenceStateResolver.Resolve);
            exitPresentationController.SetDeferredExitCleanupHoldPredicate(
                moonBlockDestructionPresentationController.ShouldHoldDeferredExitCleanup);
            exitPresentationController.SetLiveExitOwnershipBypassPredicate(
                moonBlockDestructionPresentationController.ShouldBypassLiveExitOwnership);
            var trackPlanner = new GameplayTrackPlanner(
                stateStore,
                trackState,
                motionTimingResolver,
                poseResolver,
                exitPresentationController,
                moonBlockDestructionPresentationController,
                entityPresentationApplier);
            var boxMotionTrackPlannerPlaybackPort = new GameplayMotionTrackPlannerPlaybackPort(
                trackPlanner,
                stateStore,
                trackState);
            var boxMotionLane = new BoxMotionPresentationLaneRuntime(
                trackState,
                options.BoxMotionExecutionPipelineFactory ??
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                boxMotionTrackPlannerPlaybackPort,
                new GameplayBoxMotionRuntimeCleanupAdapter(
                    stateStore,
                    trackState,
                    entityPresentationApplier));
            var playerActionAnimationLane = new PlayerActionAnimationLaneRuntime(
                stateStore,
                animationSync,
                (entityId, actionKind) => motionTimingResolver.ResolvePlayerMotionDurationSeconds(
                    entityId,
                    actionKind,
                    playerActionAnimationTimingProfileSource.TimingProfile),
                options.PlayerActionAnimationExecutionPipelineFactory);
            var enemyPresentationLane = new EnemyPresentationLaneRuntime(
                stateStore,
                animationSync,
                options.EnemyPresentationExecutionPipelineFactory);

            ConfigureProductionDefaultExecutionGuards(
                topologyLane,
                damageDeathVfxLane,
                options.DamageDeathVfxPlaybackPort,
                boxMotionLane,
                playerActionAnimationLane,
                enemyPresentationLane,
                coreGameplaySfxLane,
                gameplayActionAudioLane,
                enemyOneShotAudioLane);

            return new GameplayPresentationRuntimeComposition(
                stateStore,
                trackState,
                animationSync,
                motionTimingResolver,
                poseResolver,
                committedFrameBuilder,
                entityPresentationApplier,
                enemyVisualSemanticResolver,
                exitPresentationController,
                moonBlockDestructionPresentationController,
                moonBlockEmergencePresentationController,
                trackPlanner,
                topologyTransitionController,
                presentationActivityInspector,
                presentationPauseRegistry,
                destroyShrinkVfxSequenceStateResolver,
                playerActionAnimationTimingProfileSource,
                blockAudioRequestPlanner,
                blockAudioPresentationController,
                playerLocomotionAudioPresentationController,
                gravityFieldAudioRequestPlanner,
                gravityFieldAudioPresentationController,
                gravityFieldPresentationRequestPlanner,
                gravityFieldVisualPresentationController,
                summonedEnemyPresentationResolver,
                tileFeatureAudioRequestPlanner,
                tileFeatureAudioPresentationController,
                topologyAudioRequestPlanner,
                topologyAudioPresentationController,
                tilePresentationRequestPlanner,
                utilityWindupVfxPresenter,
                tileFeatureVisualPresentationController,
                gameplaySfxArbiter,
                enemyChargeLoopAudioPresentationController,
                enemyOneShotAudioLane,
                gameplayActionAudioLane,
                coreGameplaySfxLane,
                playerActionAnimationLane,
                enemyPresentationLane,
                boxMotionLane,
                topologyLane,
                damageDeathVfxLane);
        }

        private static void ConfigureProductionDefaultExecutionGuards(
            TopologyPresentationLaneRuntime topologyLane,
            DamageDeathVfxPresentationLaneRuntime damageDeathVfxLane,
            IDamageDeathVfxPlaybackPort damageDeathVfxPlaybackPort,
            BoxMotionPresentationLaneRuntime boxMotionLane,
            PlayerActionAnimationLaneRuntime playerActionAnimationLane,
            EnemyPresentationLaneRuntime enemyPresentationLane,
            CoreGameplaySfxLaneRuntime coreGameplaySfxLane,
            GameplayActionAudioLaneRuntime gameplayActionAudioLane,
            EnemyOneShotAudioLaneRuntime enemyOneShotAudioLane)
        {
            topologyLane.ConfigureExecution(TopologyPresentationExecutionPolicy.ProductionDefault);
            damageDeathVfxLane.ConfigureExecution(
                DamageDeathVfxExecutionPolicy.ProductionDefault,
                damageDeathVfxPlaybackPort);
            boxMotionLane.ConfigureExecution(BoxMotionExecutionPolicy.ProductionDefault);
            playerActionAnimationLane.ConfigureExecution(PlayerActionAnimationExecutionPolicy.ProductionDefault);
            enemyPresentationLane.ConfigureExecution(EnemyPresentationExecutionPolicy.ProductionDefault);
            coreGameplaySfxLane.ConfigureExecution(CoreGameplaySfxExecutionPolicy.ProductionDefault);
            gameplayActionAudioLane.ConfigureExecution(ActionAudioExecutionPolicy.ProductionDefault);
            enemyOneShotAudioLane.ConfigureExecution(EnemyAudioExecutionPolicy.ProductionDefault);
        }
    }

    internal sealed class GameplayDestroyShrinkVfxSequenceStateResolver
    {
        private IReadOnlyList<IGameplayTickPresentationExtension> _extensions =
            Array.Empty<IGameplayTickPresentationExtension>();

        public void BindExtensions(IReadOnlyList<IGameplayTickPresentationExtension> extensions)
        {
            _extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
        }

        public DestroyShrinkVfxSequenceState Resolve(int sourceEntityId, int sequenceId)
        {
            for (var i = 0; i < _extensions.Count; i++)
            {
                if (_extensions[i] is IGameplayDestroyShrinkVfxSequenceStateProvider provider)
                {
                    var state = provider.GetDestroyShrinkState(sourceEntityId, sequenceId);
                    if (state != DestroyShrinkVfxSequenceState.None)
                    {
                        return state;
                    }
                }
            }

            return DestroyShrinkVfxSequenceState.None;
        }
    }

    internal sealed class GameplayPlayerActionAnimationTimingProfileSource
    {
        public GameplayTimingProfile TimingProfile { get; set; }
    }
}
