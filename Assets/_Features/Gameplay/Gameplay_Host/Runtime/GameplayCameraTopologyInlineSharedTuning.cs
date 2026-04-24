using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class GameplayCameraTopologyInlineSharedTuning
    {
        [SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping =
            TopologyRotationVisualMapping.ForwardUsesPositiveX;

        [SerializeField] private TopologyRotationTweenSettings topologyRotationTweenSettings =
            TopologyRotationTweenSettings.CreateDefault();

        [SerializeField] private GameplayCameraSettings cameraSettings = GameplayCameraSettings.CreateShowcaseDefault();

        [SerializeField] private TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile =
            TopologyTransitionCameraShakeProfile.CreateDefault();

        [SerializeField] private TopologyTransitionPostFxProfile topologyTransitionPostFxProfile =
            TopologyTransitionPostFxProfile.CreateDefault();

        public TopologyRotationVisualMapping TopologyRotationVisualMapping
        {
            get => topologyRotationVisualMapping;
            set => topologyRotationVisualMapping = value;
        }

        public TopologyRotationTweenSettings TopologyRotationTweenSettings
        {
            get => topologyRotationTweenSettings;
            set => topologyRotationTweenSettings = value;
        }

        public GameplayCameraSettings CameraSettings
        {
            get => cameraSettings;
            set => cameraSettings = value;
        }

        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile
        {
            get => topologyTransitionCameraShakeProfile;
            set => topologyTransitionCameraShakeProfile = value;
        }

        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile
        {
            get => topologyTransitionPostFxProfile;
            set => topologyTransitionPostFxProfile = value;
        }

        public void Validate()
        {
            cameraSettings ??= GameplayCameraSettings.CreateShowcaseDefault();
            topologyTransitionCameraShakeProfile ??= TopologyTransitionCameraShakeProfile.CreateDefault();
            topologyTransitionPostFxProfile ??= TopologyTransitionPostFxProfile.CreateDefault();
        }

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

        public static GameplayCameraTopologyInlineSharedTuning CreateShowcaseDefault()
        {
            return new GameplayCameraTopologyInlineSharedTuning
            {
                TopologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX,
                TopologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault(),
                CameraSettings = GameplayCameraSettings.CreateShowcaseDefault(),
                TopologyTransitionCameraShakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault(),
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
            };
        }
    }
}
