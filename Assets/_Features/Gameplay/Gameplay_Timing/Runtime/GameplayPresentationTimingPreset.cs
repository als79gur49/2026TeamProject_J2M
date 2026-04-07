using System;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.Timing
{
    [CreateAssetMenu(
        menuName = "Gameplay/Timing/Presentation Timing Preset",
        fileName = "GameplayPresentationTimingPreset")]
    public sealed class GameplayPresentationTimingPreset : ScriptableObject
    {
        private const float UseConfigurationFallbackSentinel = -1f;

        [SerializeField] private float moveMotionDurationSeconds = UseConfigurationFallbackSentinel;
        [SerializeField] private float pushMotionDurationSeconds = UseConfigurationFallbackSentinel;
        [SerializeField] private float flipMotionDurationSeconds = UseConfigurationFallbackSentinel;
        [SerializeField] private float topologyMotionDurationSeconds = UseConfigurationFallbackSentinel;
        [SerializeField] private float itemConsumeEffectDurationSeconds = UseConfigurationFallbackSentinel;
        [SerializeField] private float boxDestroyEffectDurationSeconds = UseConfigurationFallbackSentinel;

        public void ApplyTo(GameplaySceneHostConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Validate();

            configuration.MoveMotionDurationSeconds = moveMotionDurationSeconds;
            configuration.PushMotionDurationSeconds = pushMotionDurationSeconds;
            configuration.FlipMotionDurationSeconds = flipMotionDurationSeconds;
            configuration.TopologyMotionDurationSeconds = topologyMotionDurationSeconds;
            configuration.ItemConsumeEffectDurationSeconds = itemConsumeEffectDurationSeconds;
            configuration.BoxDestroyEffectDurationSeconds = boxDestroyEffectDurationSeconds;
        }

        public void Validate()
        {
            ValidateDuration(moveMotionDurationSeconds, nameof(moveMotionDurationSeconds));
            ValidateDuration(pushMotionDurationSeconds, nameof(pushMotionDurationSeconds));
            ValidateDuration(flipMotionDurationSeconds, nameof(flipMotionDurationSeconds));
            ValidateDuration(topologyMotionDurationSeconds, nameof(topologyMotionDurationSeconds));
            ValidateDuration(itemConsumeEffectDurationSeconds, nameof(itemConsumeEffectDurationSeconds));
            ValidateDuration(boxDestroyEffectDurationSeconds, nameof(boxDestroyEffectDurationSeconds));
        }

        private static void ValidateDuration(float value, string paramName)
        {
            if (value == UseConfigurationFallbackSentinel)
            {
                return;
            }

            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    "Presentation timing values must be greater than zero, or -1 to use the existing configuration fallback.");
            }
        }
    }
}
