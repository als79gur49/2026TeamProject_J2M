using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(
        menuName = "Gameplay/AI/Behaviors/Summon Module",
        fileName = "EnemySummonBehaviorModule")]
    public sealed class EnemySummonBehaviorModuleAsset : EnemyBehaviorModuleAsset
    {
        [SerializeField] private float initialDelaySeconds = 0f;
        [SerializeField] private float cooldownSeconds = 1f;
        [SerializeField] private SummonMinionAuthoring summon = new();

        public override EnemyBehaviorModuleKey Key => EnemyBehaviorModuleKey.Summon;

        public float InitialDelaySeconds => initialDelaySeconds;

        public float CooldownSeconds => cooldownSeconds;

        public SummonMinionAuthoring Summon => summon;

        internal override EnemyBehaviorModuleRuntime Compile(in EnemyBehaviorModuleCompileContext context)
        {
            if (initialDelaySeconds < 0f)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{context.ProfileName}' summon behavior module '{name}' requires a non-negative initial delay.",
                    nameof(initialDelaySeconds));
            }

            if (cooldownSeconds <= 0f)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{context.ProfileName}' summon behavior module '{name}' requires a positive cooldown.",
                    nameof(cooldownSeconds));
            }

            if (summon == null)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{context.ProfileName}' summon behavior module '{name}' requires summon authoring data.",
                    nameof(summon));
            }

            return new EnemySummonBehaviorRuntime(
                GameplayTimingProfile.SecondsToTicks(
                    initialDelaySeconds,
                    context.SimulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(cooldownSeconds, context.SimulationTicksPerSecond),
                summon.Compile(context.SimulationTicksPerSecond));
        }
    }
}
