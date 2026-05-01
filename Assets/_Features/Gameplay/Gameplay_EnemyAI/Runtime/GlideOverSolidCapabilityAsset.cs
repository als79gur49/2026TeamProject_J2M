using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Capabilities/Movement/Glide Over Solid", fileName = "GlideOverSolidCapability")]
    public sealed class GlideOverSolidCapabilityAsset : EnemyMovementSkillCapabilityAsset
    {
        [SerializeField] private EnemyGlideTimingAuthoringSettings glideTimingSettings = new(3f, 2f);
        [SerializeField] private EnemyGlidePresentationAuthoringSettings glidePresentationSettings =
            EnemyGlidePresentationAuthoringSettings.CreateDefault();

        public override MovementSkillStrategyKind Kind => MovementSkillStrategyKind.GlideOverSolid;

        public override EnemyGlideTimingAuthoringSettings GlideTimingSettings => glideTimingSettings;

        public override EnemyGlidePresentationAuthoringSettings GlidePresentationSettings => glidePresentationSettings;
    }
}
