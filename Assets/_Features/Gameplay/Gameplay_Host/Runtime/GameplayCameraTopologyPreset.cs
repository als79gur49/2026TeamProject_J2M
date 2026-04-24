using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayCameraTopologyPresetSnapshot
    {
        public GameplayCameraTopologyPresetSnapshot(
            TopologyRotationVisualMapping topologyRotationVisualMapping,
            TopologyRotationTweenSettings topologyRotationTweenSettings,
            GameplayCameraSettings cameraSettings,
            TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile,
            TopologyTransitionPostFxProfile topologyTransitionPostFxProfile)
        {
            TopologyRotationVisualMapping = topologyRotationVisualMapping;
            TopologyRotationTweenSettings = topologyRotationTweenSettings;
            CameraSettings = cameraSettings?.Clone() ?? GameplayCameraSettings.CreateShowcaseDefault();
            TopologyTransitionCameraShakeProfile =
                topologyTransitionCameraShakeProfile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            TopologyTransitionPostFxProfile =
                topologyTransitionPostFxProfile?.Clone() ?? TopologyTransitionPostFxProfile.CreateDefault();
        }

        public TopologyRotationVisualMapping TopologyRotationVisualMapping { get; }

        public TopologyRotationTweenSettings TopologyRotationTweenSettings { get; }

        public GameplayCameraSettings CameraSettings { get; }

        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile { get; }

        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile { get; }
    }

    [CreateAssetMenu(menuName = "Gameplay/Camera/Topology Preset", fileName = "GameplayCameraTopologyPreset")]
    public sealed class GameplayCameraTopologyPreset : ScriptableObject
    {
        [Tooltip("Stage-scoped shared topology rotation mapping.")]
        [SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping =
            TopologyRotationVisualMapping.ForwardUsesPositiveX;
        [Tooltip("Stage-scoped shared topology rotation tween settings.")]
        [SerializeField] private TopologyRotationTweenSettings topologyRotationTweenSettings =
            TopologyRotationTweenSettings.CreateDefault();
        [Tooltip("Stage-scoped shared camera tuning payload. In Preset mode, authored-baseline usage flags remain scene-local policy.")]
        [SerializeField] private GameplayCameraSettings cameraSettings = GameplayCameraSettings.CreateShowcaseDefault();
        [Tooltip("Stage-scoped shared topology-transition camera shake tuning.")]
        [SerializeField] private TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile =
            TopologyTransitionCameraShakeProfile.CreateDefault();
        [Tooltip("Stage-scoped shared topology-transition post-fx tuning.")]
        [SerializeField] private TopologyTransitionPostFxProfile topologyTransitionPostFxProfile =
            TopologyTransitionPostFxProfile.CreateDefault();

        public GameplayCameraTopologyPresetSnapshot CreateSnapshot()
        {
            Validate();

            return new GameplayCameraTopologyPresetSnapshot(
                topologyRotationVisualMapping,
                topologyRotationTweenSettings,
                cameraSettings,
                topologyTransitionCameraShakeProfile,
                topologyTransitionPostFxProfile);
        }

        public void Validate()
        {
            cameraSettings ??= GameplayCameraSettings.CreateShowcaseDefault();
            topologyTransitionCameraShakeProfile ??= TopologyTransitionCameraShakeProfile.CreateDefault();
            topologyTransitionPostFxProfile ??= TopologyTransitionPostFxProfile.CreateDefault();
        }
    }
}
