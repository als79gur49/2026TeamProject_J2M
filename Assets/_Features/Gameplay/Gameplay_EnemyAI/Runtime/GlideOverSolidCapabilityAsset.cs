using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Capabilities/Movement/Glide Over Solid", fileName = "GlideOverSolidCapability")]
    public sealed class GlideOverSolidCapabilityAsset : EnemyMovementSkillCapabilityAsset
    {
        [SerializeField] private EnemyGlideTimingAuthoringSettings glideTimingSettings = new(3f, 2f);

        public override MovementSkillStrategyKind Kind => MovementSkillStrategyKind.GlideOverSolid;

        public override EnemyGlideTimingAuthoringSettings GlideTimingSettings => glideTimingSettings;
    }
}
