using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayCameraTopologyAuthoringSnapshot
    {
        public GameplayCameraTopologyAuthoringSnapshot(
            bool configureMainCamera,
            GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy,
            TopologyRotationVisualMapping topologyRotationVisualMapping,
            TopologyRotationTweenSettings topologyRotationTweenSettings,
            GameplayCameraSettings cameraSettings,
            TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile,
            TopologyTransitionPostFxProfile topologyTransitionPostFxProfile)
        {
            ConfigureMainCamera = configureMainCamera;
            BaselineAuthoringPolicy = baselineAuthoringPolicy;
            TopologyRotationVisualMapping = topologyRotationVisualMapping;
            TopologyRotationTweenSettings = topologyRotationTweenSettings;
            CameraSettings = cameraSettings?.Clone() ?? GameplayCameraSettings.CreateShowcaseDefault();
            TopologyTransitionCameraShakeProfile =
                topologyTransitionCameraShakeProfile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            TopologyTransitionPostFxProfile =
                topologyTransitionPostFxProfile?.Clone() ?? TopologyTransitionPostFxProfile.CreateDefault();
        }

        public bool ConfigureMainCamera { get; }

        public GameplayCameraBaselineAuthoringPolicy BaselineAuthoringPolicy { get; }

        public TopologyRotationVisualMapping TopologyRotationVisualMapping { get; }

        public TopologyRotationTweenSettings TopologyRotationTweenSettings { get; }

        public GameplayCameraSettings CameraSettings { get; }

        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile { get; }

        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile { get; }
    }

    /// <summary>
    /// Scene-local camera topology authority entrypoint.
    /// Preset mode resolves stage-shared tuning from a referenced preset, while authored-baseline
    /// policy remains local on this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayCameraTopologyAuthoring : MonoBehaviour
    {
        [Tooltip("Inline uses the local shared-tuning fields below. Preset uses only the referenced preset for shared tuning, while configureMainCamera and authored-baseline policy remain local scene policy.")]
        [SerializeField] private GameplayCameraTopologySourceMode sourceMode = GameplayCameraTopologySourceMode.Inline;
        [Tooltip("Stage-scoped shared tuning asset. In Preset mode, shared tuning resolves only from this asset. Inline shared-tuning fields below remain serialized fallback and are not authoritative.")]
        [SerializeField] private GameplayCameraTopologyPreset preset;
        [Tooltip("Scene-local camera bootstrap policy. This remains local even when Preset mode is active.")]
        [SerializeField] private bool configureMainCamera = true;
        [Tooltip("Scene-local authored-baseline policy. This remains local even when Preset mode is active.")]
        [SerializeField] private GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy =
            GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault();
        [Tooltip("Shared topology rotation mapping for Inline mode. In Preset mode this serialized fallback is retained for migration safety only and is not authoritative.")]
        [SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping =
            TopologyRotationVisualMapping.ForwardUsesPositiveX;
        [Tooltip("Shared topology rotation tween settings for Inline mode. In Preset mode this serialized fallback is retained for migration safety only and is not authoritative.")]
        [SerializeField] private TopologyRotationTweenSettings topologyRotationTweenSettings =
            TopologyRotationTweenSettings.CreateDefault();
        [Tooltip("Shared camera tuning payload for Inline mode. In Preset mode this serialized fallback is retained for migration safety only and is not authoritative.")]
        [SerializeField] private GameplayCameraSettings cameraSettings = GameplayCameraSettings.CreateShowcaseDefault();
        [Tooltip("Shared topology-transition camera shake tuning for Inline mode. In Preset mode this serialized fallback is retained for migration safety only and is not authoritative.")]
        [SerializeField] private TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile =
            TopologyTransitionCameraShakeProfile.CreateDefault();
        [Tooltip("Shared topology-transition post-fx tuning for Inline mode. In Preset mode this serialized fallback is retained for migration safety only and is not authoritative.")]
        [SerializeField] private TopologyTransitionPostFxProfile topologyTransitionPostFxProfile =
            TopologyTransitionPostFxProfile.CreateDefault();

        public GameplayCameraTopologyAuthoringSnapshot CreateSnapshot()
        {
            Validate();
            var sharedSnapshot = ResolveSharedSnapshot();

            return new GameplayCameraTopologyAuthoringSnapshot(
                configureMainCamera,
                baselineAuthoringPolicy,
                sharedSnapshot.TopologyRotationVisualMapping,
                sharedSnapshot.TopologyRotationTweenSettings,
                sharedSnapshot.CameraSettings,
                sharedSnapshot.TopologyTransitionCameraShakeProfile,
                sharedSnapshot.TopologyTransitionPostFxProfile);
        }

        public void Validate()
        {
            cameraSettings ??= GameplayCameraSettings.CreateShowcaseDefault();
            topologyTransitionCameraShakeProfile ??= TopologyTransitionCameraShakeProfile.CreateDefault();
            topologyTransitionPostFxProfile ??= TopologyTransitionPostFxProfile.CreateDefault();
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
            return ResolveSharedSnapshot().CameraSettings?.Clone() ??
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
            return ResolveSharedSnapshot().TopologyTransitionCameraShakeProfile?.Clone() ??
                   TopologyTransitionCameraShakeProfile.CreateDefault();
        }

        public TopologyTransitionPostFxProfile GetTopologyTransitionPostFxProfile()
        {
            Validate();
            return ResolveSharedSnapshot().TopologyTransitionPostFxProfile?.Clone() ??
                   TopologyTransitionPostFxProfile.CreateDefault();
        }

        // Preset mode never reads inline shared tuning. Those serialized fields remain transitional
        // fallback only and are not the canonical runtime source while Preset mode is active.
        private GameplayCameraTopologyPresetSnapshot ResolveSharedSnapshot()
        {
            return sourceMode switch
            {
                GameplayCameraTopologySourceMode.Inline => CreateInlineSharedSnapshot(),
                GameplayCameraTopologySourceMode.Preset => ResolveRequiredPreset().CreateSnapshot(),
                _ => throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, "Unknown camera topology source mode."),
            };
        }

        private GameplayCameraTopologyPresetSnapshot CreateInlineSharedSnapshot()
        {
            return new GameplayCameraTopologyPresetSnapshot(
                topologyRotationVisualMapping,
                topologyRotationTweenSettings,
                cameraSettings,
                topologyTransitionCameraShakeProfile,
                topologyTransitionPostFxProfile);
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
