using System;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayPresentationRuntimeComposition
    {
        public GameplayPresentationRuntimeComposition(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayAnimationSyncCoordinator animationSync,
            GameplayMotionTimingResolver motionTimingResolver,
            GameplayPoseResolver poseResolver,
            GameplayCommittedFrameBuilder committedFrameBuilder,
            GameplayEntityPresentationApplier entityPresentationApplier,
            IEnemyVisualSemanticResolver enemyVisualSemanticResolver,
            GameplayExitPresentationController exitPresentationController,
            MoonBlockDestructionPresentationController moonBlockDestructionPresentationController,
            MoonBlockEmergencePresentationController moonBlockEmergencePresentationController,
            GameplayTrackPlanner trackPlanner,
            GameplayTopologyTransitionController topologyTransitionController,
            GameplayPresentationActivityInspector presentationActivityInspector,
            GameplayPresentationPauseRegistry presentationPauseRegistry,
            GameplayDestroyShrinkVfxSequenceStateResolver destroyShrinkVfxSequenceStateResolver,
            GameplayPlayerActionAnimationTimingProfileSource playerActionAnimationTimingProfileSource,
            BlockAudioRequestPlanner blockAudioRequestPlanner,
            BlockAudioPresentationController blockAudioPresentationController,
            PlayerLocomotionAudioPresentationController playerLocomotionAudioPresentationController,
            GravityFieldAudioRequestPlanner gravityFieldAudioRequestPlanner,
            GravityFieldAudioPresentationController gravityFieldAudioPresentationController,
            GravityFieldPresentationRequestPlanner gravityFieldPresentationRequestPlanner,
            GravityFieldVisualPresentationController gravityFieldVisualPresentationController,
            SummonedEnemyPresentationResolver summonedEnemyPresentationResolver,
            TileFeatureAudioRequestPlanner tileFeatureAudioRequestPlanner,
            TileFeatureAudioPresentationController tileFeatureAudioPresentationController,
            TopologyAudioRequestPlanner topologyAudioRequestPlanner,
            TopologyAudioPresentationController topologyAudioPresentationController,
            TilePresentationRequestPlanner tilePresentationRequestPlanner,
            GameplayUtilityWindupVfxPresenter utilityWindupVfxPresenter,
            TileFeatureVisualPresentationController tileFeatureVisualPresentationController,
            GameplaySfxArbiter gameplaySfxArbiter,
            EnemyChargeLoopAudioPresentationController enemyChargeLoopAudioPresentationController,
            EnemyOneShotAudioLaneRuntime enemyOneShotAudioLane,
            GameplayActionAudioLaneRuntime gameplayActionAudioLane,
            CoreGameplaySfxLaneRuntime coreGameplaySfxLane,
            PlayerActionAnimationLaneRuntime playerActionAnimationLane,
            EnemyPresentationLaneRuntime enemyPresentationLane,
            BoxMotionPresentationLaneRuntime boxMotionLane,
            TopologyPresentationLaneRuntime topologyLane,
            DamageDeathVfxPresentationLaneRuntime damageDeathVfxLane)
        {
            StateStore = Require(stateStore, nameof(stateStore));
            TrackState = Require(trackState, nameof(trackState));
            AnimationSync = Require(animationSync, nameof(animationSync));
            MotionTimingResolver = Require(motionTimingResolver, nameof(motionTimingResolver));
            PoseResolver = Require(poseResolver, nameof(poseResolver));
            CommittedFrameBuilder = Require(committedFrameBuilder, nameof(committedFrameBuilder));
            EntityPresentationApplier = Require(entityPresentationApplier, nameof(entityPresentationApplier));
            EnemyVisualSemanticResolver = Require(enemyVisualSemanticResolver, nameof(enemyVisualSemanticResolver));
            ExitPresentationController = Require(exitPresentationController, nameof(exitPresentationController));
            MoonBlockDestructionPresentationController = Require(
                moonBlockDestructionPresentationController,
                nameof(moonBlockDestructionPresentationController));
            MoonBlockEmergencePresentationController = Require(
                moonBlockEmergencePresentationController,
                nameof(moonBlockEmergencePresentationController));
            TrackPlanner = Require(trackPlanner, nameof(trackPlanner));
            TopologyTransitionController = Require(topologyTransitionController, nameof(topologyTransitionController));
            PresentationActivityInspector = Require(
                presentationActivityInspector,
                nameof(presentationActivityInspector));
            PresentationPauseRegistry = Require(presentationPauseRegistry, nameof(presentationPauseRegistry));
            DestroyShrinkVfxSequenceStateResolver = Require(
                destroyShrinkVfxSequenceStateResolver,
                nameof(destroyShrinkVfxSequenceStateResolver));
            PlayerActionAnimationTimingProfileSource = Require(
                playerActionAnimationTimingProfileSource,
                nameof(playerActionAnimationTimingProfileSource));
            BlockAudioRequestPlanner = Require(blockAudioRequestPlanner, nameof(blockAudioRequestPlanner));
            BlockAudioPresentationController = Require(
                blockAudioPresentationController,
                nameof(blockAudioPresentationController));
            PlayerLocomotionAudioPresentationController = Require(
                playerLocomotionAudioPresentationController,
                nameof(playerLocomotionAudioPresentationController));
            GravityFieldAudioRequestPlanner = Require(
                gravityFieldAudioRequestPlanner,
                nameof(gravityFieldAudioRequestPlanner));
            GravityFieldAudioPresentationController = Require(
                gravityFieldAudioPresentationController,
                nameof(gravityFieldAudioPresentationController));
            GravityFieldPresentationRequestPlanner = Require(
                gravityFieldPresentationRequestPlanner,
                nameof(gravityFieldPresentationRequestPlanner));
            GravityFieldVisualPresentationController = Require(
                gravityFieldVisualPresentationController,
                nameof(gravityFieldVisualPresentationController));
            SummonedEnemyPresentationResolver = Require(
                summonedEnemyPresentationResolver,
                nameof(summonedEnemyPresentationResolver));
            TileFeatureAudioRequestPlanner = Require(
                tileFeatureAudioRequestPlanner,
                nameof(tileFeatureAudioRequestPlanner));
            TileFeatureAudioPresentationController = Require(
                tileFeatureAudioPresentationController,
                nameof(tileFeatureAudioPresentationController));
            TopologyAudioRequestPlanner = Require(topologyAudioRequestPlanner, nameof(topologyAudioRequestPlanner));
            TopologyAudioPresentationController = Require(
                topologyAudioPresentationController,
                nameof(topologyAudioPresentationController));
            TilePresentationRequestPlanner = Require(
                tilePresentationRequestPlanner,
                nameof(tilePresentationRequestPlanner));
            UtilityWindupVfxPresenter = Require(utilityWindupVfxPresenter, nameof(utilityWindupVfxPresenter));
            TileFeatureVisualPresentationController = Require(
                tileFeatureVisualPresentationController,
                nameof(tileFeatureVisualPresentationController));
            GameplaySfxArbiter = Require(gameplaySfxArbiter, nameof(gameplaySfxArbiter));
            EnemyChargeLoopAudioPresentationController = Require(
                enemyChargeLoopAudioPresentationController,
                nameof(enemyChargeLoopAudioPresentationController));
            EnemyOneShotAudioLane = Require(enemyOneShotAudioLane, nameof(enemyOneShotAudioLane));
            GameplayActionAudioLane = Require(gameplayActionAudioLane, nameof(gameplayActionAudioLane));
            CoreGameplaySfxLane = Require(coreGameplaySfxLane, nameof(coreGameplaySfxLane));
            PlayerActionAnimationLane = Require(playerActionAnimationLane, nameof(playerActionAnimationLane));
            EnemyPresentationLane = Require(enemyPresentationLane, nameof(enemyPresentationLane));
            BoxMotionLane = Require(boxMotionLane, nameof(boxMotionLane));
            TopologyLane = Require(topologyLane, nameof(topologyLane));
            DamageDeathVfxLane = Require(damageDeathVfxLane, nameof(damageDeathVfxLane));
        }

        public GameplayPresentationStateStore StateStore { get; }
        public GameplayPresentationTrackState TrackState { get; }
        public GameplayAnimationSyncCoordinator AnimationSync { get; }
        public GameplayMotionTimingResolver MotionTimingResolver { get; }
        public GameplayPoseResolver PoseResolver { get; }
        public GameplayCommittedFrameBuilder CommittedFrameBuilder { get; }
        public GameplayEntityPresentationApplier EntityPresentationApplier { get; }
        public IEnemyVisualSemanticResolver EnemyVisualSemanticResolver { get; }
        public GameplayExitPresentationController ExitPresentationController { get; }
        public MoonBlockDestructionPresentationController MoonBlockDestructionPresentationController { get; }
        public MoonBlockEmergencePresentationController MoonBlockEmergencePresentationController { get; }
        public GameplayTrackPlanner TrackPlanner { get; }
        public GameplayTopologyTransitionController TopologyTransitionController { get; }
        public GameplayPresentationActivityInspector PresentationActivityInspector { get; }
        public GameplayPresentationPauseRegistry PresentationPauseRegistry { get; }
        public GameplayDestroyShrinkVfxSequenceStateResolver DestroyShrinkVfxSequenceStateResolver { get; }
        public GameplayPlayerActionAnimationTimingProfileSource PlayerActionAnimationTimingProfileSource { get; }
        public BlockAudioRequestPlanner BlockAudioRequestPlanner { get; }
        public BlockAudioPresentationController BlockAudioPresentationController { get; }
        public PlayerLocomotionAudioPresentationController PlayerLocomotionAudioPresentationController { get; }
        public GravityFieldAudioRequestPlanner GravityFieldAudioRequestPlanner { get; }
        public GravityFieldAudioPresentationController GravityFieldAudioPresentationController { get; }
        public GravityFieldPresentationRequestPlanner GravityFieldPresentationRequestPlanner { get; }
        public GravityFieldVisualPresentationController GravityFieldVisualPresentationController { get; }
        public SummonedEnemyPresentationResolver SummonedEnemyPresentationResolver { get; }
        public TileFeatureAudioRequestPlanner TileFeatureAudioRequestPlanner { get; }
        public TileFeatureAudioPresentationController TileFeatureAudioPresentationController { get; }
        public TopologyAudioRequestPlanner TopologyAudioRequestPlanner { get; }
        public TopologyAudioPresentationController TopologyAudioPresentationController { get; }
        public TilePresentationRequestPlanner TilePresentationRequestPlanner { get; }
        public GameplayUtilityWindupVfxPresenter UtilityWindupVfxPresenter { get; }
        public TileFeatureVisualPresentationController TileFeatureVisualPresentationController { get; }
        public GameplaySfxArbiter GameplaySfxArbiter { get; }
        public EnemyChargeLoopAudioPresentationController EnemyChargeLoopAudioPresentationController { get; }
        public EnemyOneShotAudioLaneRuntime EnemyOneShotAudioLane { get; }
        public GameplayActionAudioLaneRuntime GameplayActionAudioLane { get; }
        public CoreGameplaySfxLaneRuntime CoreGameplaySfxLane { get; }
        public PlayerActionAnimationLaneRuntime PlayerActionAnimationLane { get; }
        public EnemyPresentationLaneRuntime EnemyPresentationLane { get; }
        public BoxMotionPresentationLaneRuntime BoxMotionLane { get; }
        public TopologyPresentationLaneRuntime TopologyLane { get; }
        public DamageDeathVfxPresentationLaneRuntime DamageDeathVfxLane { get; }

        private static T Require<T>(T value, string name)
            where T : class
        {
            return value ?? throw new ArgumentNullException(name);
        }
    }
}
