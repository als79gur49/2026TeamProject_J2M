using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Capabilities/Movement/Jump To Locked Target", fileName = "JumpToLockedTargetCapability")]
    public sealed class JumpToLockedTargetCapabilityAsset : EnemyMovementSkillCapabilityAsset
    {
        [SerializeField] private EnemyJumpTimingAuthoringSettings jumpTimingSettings = new(0f, 0f, 0f);

        public override MovementSkillStrategyKind Kind => MovementSkillStrategyKind.JumpToLockedTarget;

        public override EnemyJumpTimingAuthoringSettings JumpTimingSettings => jumpTimingSettings;
    }
}
