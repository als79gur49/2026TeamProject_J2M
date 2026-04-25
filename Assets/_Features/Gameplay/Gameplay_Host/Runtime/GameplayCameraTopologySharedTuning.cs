using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class GameplayCameraTopologySharedTuning
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

        public TopologyRotationVisualMapping TopologyRotationVisualMapping => topologyRotationVisualMapping;

        public TopologyRotationTweenSettings TopologyRotationTweenSettings => topologyRotationTweenSettings;

        public GameplayCameraSettings CameraSettings => cameraSettings;

        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile =>
            topologyTransitionCameraShakeProfile;

        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile => topologyTransitionPostFxProfile;

        public void Validate()
        {
            cameraSettings ??= GameplayCameraSettings.CreateShowcaseDefault();
            topologyTransitionCameraShakeProfile ??= TopologyTransitionCameraShakeProfile.CreateDefault();
            topologyTransitionPostFxProfile ??= TopologyTransitionPostFxProfile.CreateDefault();
        }

        public GameplayCameraTopologySharedTuning Clone()
        {
            Validate();

            return new GameplayCameraTopologySharedTuning
            {
                topologyRotationVisualMapping = topologyRotationVisualMapping,
                topologyRotationTweenSettings = topologyRotationTweenSettings,
                cameraSettings = cameraSettings?.Clone() ?? GameplayCameraSettings.CreateShowcaseDefault(),
                topologyTransitionCameraShakeProfile =
                    topologyTransitionCameraShakeProfile?.Clone() ??
                    TopologyTransitionCameraShakeProfile.CreateDefault(),
                topologyTransitionPostFxProfile =
                    topologyTransitionPostFxProfile?.Clone() ??
                    TopologyTransitionPostFxProfile.CreateDefault(),
            };
        }

        public static GameplayCameraTopologySharedTuning CreateRuntimeDefault()
        {
            return new GameplayCameraTopologySharedTuning
            {
                topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX,
                topologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault(),
                cameraSettings = GameplayCameraSettings.CreateRuntimeDefault(),
                topologyTransitionCameraShakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault(),
                topologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
            };
        }

        public static GameplayCameraTopologySharedTuning CreateShowcaseDefault()
        {
            return new GameplayCameraTopologySharedTuning
            {
                topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX,
                topologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault(),
                cameraSettings = GameplayCameraSettings.CreateShowcaseDefault(),
                topologyTransitionCameraShakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault(),
                topologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
            };
        }
    }
}
