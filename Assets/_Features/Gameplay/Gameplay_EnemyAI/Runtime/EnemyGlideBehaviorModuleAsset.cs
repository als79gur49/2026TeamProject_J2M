using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(
        menuName = "Gameplay/AI/Behaviors/Glide Module",
        fileName = "EnemyGlideBehaviorModule")]
    public sealed class EnemyGlideBehaviorModuleAsset : EnemyBehaviorModuleAsset
    {
        [SerializeField] private EnemyGlideTimingAuthoringSettings timing =
            EnemyGlideTimingAuthoringSettings.CreateDefault();
        [SerializeField] private EnemyGlidePresentationAuthoringSettings presentationSettings =
            EnemyGlidePresentationAuthoringSettings.CreateDefault();

        public override EnemyBehaviorModuleKey Key => EnemyBehaviorModuleKey.Glide;

        public EnemyGlideTimingAuthoringSettings Timing => timing;

        public EnemyGlidePresentationAuthoringSettings PresentationSettings => presentationSettings;

        internal override EnemyBehaviorModuleRuntime Compile(in EnemyBehaviorModuleCompileContext context)
        {
            return new EnemyGlideBehaviorRuntime(
                timing.ToRuntimeSettings(context.SimulationTicksPerSecond),
                presentationSettings.ToRuntimeSettings());
        }
    }
}
