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

        [SerializeField] private GameplayCameraShakeProfile gameplayCameraShakeProfile;

        [SerializeField] private TopologyTransitionPostFxProfile topologyTransitionPostFxProfile =
            TopologyTransitionPostFxProfile.CreateDefault();

        public TopologyRotationVisualMapping TopologyRotationVisualMapping => topologyRotationVisualMapping;

        public TopologyRotationTweenSettings TopologyRotationTweenSettings => topologyRotationTweenSettings;

        public GameplayCameraSettings CameraSettings => cameraSettings;

        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile =>
            topologyTransitionCameraShakeProfile;

        public GameplayCameraShakeProfile GameplayCameraShakeProfile => gameplayCameraShakeProfile;

        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile => topologyTransitionPostFxProfile;

        public void Validate()
        {
            cameraSettings ??= GameplayCameraSettings.CreateShowcaseDefault();
            topologyTransitionCameraShakeProfile ??= TopologyTransitionCameraShakeProfile.CreateDefault();
            topologyTransitionPostFxProfile ??= TopologyTransitionPostFxProfile.CreateDefault();
            gameplayCameraShakeProfile?.ValidateOrThrow();
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
                gameplayCameraShakeProfile = gameplayCameraShakeProfile,
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
                gameplayCameraShakeProfile = null,
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
                gameplayCameraShakeProfile = null,
                topologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
            };
        }
    }
}
