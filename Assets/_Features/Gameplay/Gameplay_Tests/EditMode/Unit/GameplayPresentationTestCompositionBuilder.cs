using System;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayPresentationTestCompositionBuilder
    {
        public static GameplayPresentationRuntimeComposition CreateComposition(
            TopologyExecutionPipelineFactory topologyExecutionPipelineFactory = null,
            DamageDeathVfxExecutionPipelineFactory damageDeathVfxExecutionPipelineFactory = null,
            BoxMotionExecutionPipelineFactory boxMotionExecutionPipelineFactory = null,
            PlayerActionAnimationExecutionPipelineFactory playerActionAnimationExecutionPipelineFactory = null,
            EnemyPresentationExecutionPipelineFactory enemyPresentationExecutionPipelineFactory = null,
            CoreGameplaySfxExecutionPipelineFactory coreGameplaySfxExecutionPipelineFactory = null,
            ActionAudioExecutionPipelineFactory actionAudioExecutionPipelineFactory = null,
            EnemyAudioExecutionPipelineFactory enemyAudioExecutionPipelineFactory = null,
            IDamageDeathVfxPlaybackPort damageDeathVfxPlaybackPort = null,
            Action<GameplayPresentationRuntimeComposition> configure = null)
        {
            var composition = GameplayPresentationRuntimeCompositionFactory.Create(
                new GameplayPresentationRuntimeCompositionFactoryOptions
                {
                    TopologyExecutionPipelineFactory = topologyExecutionPipelineFactory,
                    DamageDeathVfxExecutionPipelineFactory = damageDeathVfxExecutionPipelineFactory,
                    DamageDeathVfxPlaybackPort = damageDeathVfxPlaybackPort,
                    BoxMotionExecutionPipelineFactory = boxMotionExecutionPipelineFactory,
                    PlayerActionAnimationExecutionPipelineFactory = playerActionAnimationExecutionPipelineFactory,
                    EnemyPresentationExecutionPipelineFactory = enemyPresentationExecutionPipelineFactory,
                    CoreGameplaySfxExecutionPipelineFactory = coreGameplaySfxExecutionPipelineFactory,
                    ActionAudioExecutionPipelineFactory = actionAudioExecutionPipelineFactory,
                    EnemyAudioExecutionPipelineFactory = enemyAudioExecutionPipelineFactory,
                });
            configure?.Invoke(composition);
            return composition;
        }

        public static GameplayTickPresentationCoordinator CreateCoordinator(
            TopologyExecutionPipelineFactory topologyExecutionPipelineFactory = null,
            DamageDeathVfxExecutionPipelineFactory damageDeathVfxExecutionPipelineFactory = null,
            BoxMotionExecutionPipelineFactory boxMotionExecutionPipelineFactory = null,
            PlayerActionAnimationExecutionPipelineFactory playerActionAnimationExecutionPipelineFactory = null,
            EnemyPresentationExecutionPipelineFactory enemyPresentationExecutionPipelineFactory = null,
            CoreGameplaySfxExecutionPipelineFactory coreGameplaySfxExecutionPipelineFactory = null,
            ActionAudioExecutionPipelineFactory actionAudioExecutionPipelineFactory = null,
            EnemyAudioExecutionPipelineFactory enemyAudioExecutionPipelineFactory = null,
            IDamageDeathVfxPlaybackPort damageDeathVfxPlaybackPort = null,
            Action<GameplayPresentationRuntimeComposition> configure = null)
        {
            return new GameplayTickPresentationCoordinator(
                CreateComposition(
                    topologyExecutionPipelineFactory,
                    damageDeathVfxExecutionPipelineFactory,
                    boxMotionExecutionPipelineFactory,
                    playerActionAnimationExecutionPipelineFactory,
                    enemyPresentationExecutionPipelineFactory,
                    coreGameplaySfxExecutionPipelineFactory,
                    actionAudioExecutionPipelineFactory,
                    enemyAudioExecutionPipelineFactory,
                    damageDeathVfxPlaybackPort,
                    configure));
        }

        public static void BindPresenter(GameplayTickViewPresenter presenter)
        {
            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            presenter.BindCoordinator(CreateCoordinator());
        }
    }
}
