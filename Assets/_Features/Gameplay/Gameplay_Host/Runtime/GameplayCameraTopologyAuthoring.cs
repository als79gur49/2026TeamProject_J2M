using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayCameraTopologyAuthoringSnapshot
    {
        public GameplayCameraTopologyAuthoringSnapshot(
            bool configureMainCamera,
            GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy,
            GameplayCameraTopologySharedTuning sharedTuning)
        {
            ConfigureMainCamera = configureMainCamera;
            BaselineAuthoringPolicy = baselineAuthoringPolicy;
            SharedTuning = sharedTuning?.Clone() ?? GameplayCameraTopologySharedTuning.CreateShowcaseDefault();
        }

        public bool ConfigureMainCamera { get; }

        public GameplayCameraBaselineAuthoringPolicy BaselineAuthoringPolicy { get; }

        public GameplayCameraTopologySharedTuning SharedTuning { get; }
    }

    /// <summary>
    /// Scene-local camera topology authority entrypoint.
    /// Preset mode resolves stage-shared tuning from a referenced preset, while authored-baseline
    /// policy remains local on this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayCameraTopologyAuthoring : MonoBehaviour
    {
        [Tooltip("Inline uses the local inline shared-tuning block. Preset uses only the referenced preset for shared tuning, while configureMainCamera and authored-baseline policy remain local scene policy.")]
        [SerializeField] private GameplayCameraTopologySourceMode sourceMode = GameplayCameraTopologySourceMode.Inline;
        [Tooltip("Stage-scoped shared tuning asset. In Preset mode, shared tuning resolves only from this asset.")]
        [SerializeField] private GameplayCameraTopologyPreset preset;
        [Tooltip("Scene-local camera bootstrap policy. This remains local even when Preset mode is active.")]
        [SerializeField] private bool configureMainCamera = true;
        [Tooltip("Scene-local authored-baseline policy. This remains local even when Preset mode is active.")]
        [SerializeField] private GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy =
            GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault();
        [Tooltip("Inline-only shared tuning. This block is authoritative only when Source Mode is Inline.")]
        [SerializeField] private GameplayCameraTopologySharedTuning inlineSharedTuning =
            GameplayCameraTopologySharedTuning.CreateShowcaseDefault();

        public GameplayCameraTopologyAuthoringSnapshot CreateSnapshot()
        {
            Validate();

            return new GameplayCameraTopologyAuthoringSnapshot(
                configureMainCamera,
                baselineAuthoringPolicy,
                ResolveSharedTuning());
        }

        public void Validate()
        {
            inlineSharedTuning ??= GameplayCameraTopologySharedTuning.CreateShowcaseDefault();
            inlineSharedTuning.Validate();
            preset?.Validate();
        }

        public static GameplayCameraTopologyAuthoring GetRequiredValidated(Component owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (!owner.TryGetComponent<GameplayCameraTopologyAuthoring>(out var authoring) ||
                authoring == null)
            {
                throw new InvalidOperationException(
                    $"{owner.GetType().Name} on '{owner.gameObject.name}' requires a co-located {nameof(GameplayCameraTopologyAuthoring)} component.");
            }

            authoring.Validate();
            return authoring;
        }

        public GameplayCameraSettings GetCameraSettings()
        {
            Validate();
            return ResolveSharedTuning().CameraSettings?.Clone() ??
                   GameplayCameraSettings.CreateShowcaseDefault();
        }

        public GameplayCameraBaselineAuthoringPolicy GetBaselineAuthoringPolicy()
        {
            Validate();
            return baselineAuthoringPolicy;
        }

        public TopologyTransitionCameraShakeProfile GetTopologyTransitionCameraShakeProfile()
        {
            Validate();
            return ResolveSharedTuning().TopologyTransitionCameraShakeProfile?.Clone() ??
                   TopologyTransitionCameraShakeProfile.CreateDefault();
        }

        public GameplayCameraShakeProfile GetGameplayCameraShakeProfile()
        {
            Validate();
            return ResolveSharedTuning().GameplayCameraShakeProfile;
        }

        public TopologyTransitionPostFxProfile GetTopologyTransitionPostFxProfile()
        {
            Validate();
            return ResolveSharedTuning().TopologyTransitionPostFxProfile?.Clone() ??
                   TopologyTransitionPostFxProfile.CreateDefault();
        }

        // Preset mode never reads inline shared tuning. The nested inline lane exists only as the
        // explicit Inline-mode authority surface and is not the canonical runtime source while
        // Preset mode is active.
        private GameplayCameraTopologySharedTuning ResolveSharedTuning()
        {
            return sourceMode switch
            {
                GameplayCameraTopologySourceMode.Inline => inlineSharedTuning.Clone(),
                GameplayCameraTopologySourceMode.Preset => ResolveRequiredPreset().CreateSnapshot(),
                _ => throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, "Unknown camera topology source mode."),
            };
        }

        private GameplayCameraTopologyPreset ResolveRequiredPreset()
        {
            if (preset != null)
            {
                return preset;
            }

            throw new InvalidOperationException(
                $"{nameof(GameplayCameraTopologyAuthoring)} on '{gameObject.name}' is set to {nameof(GameplayCameraTopologySourceMode.Preset)} mode but has no {nameof(GameplayCameraTopologyPreset)} assigned. Inline shared-tuning fallback is not authoritative in Preset mode; assign a preset or switch Source Mode to Inline.");
        }
    }
}
