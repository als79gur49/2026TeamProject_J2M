using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayCameraTopologyAuthoringSnapshot
    {
        public GameplayCameraTopologyAuthoringSnapshot(
            bool configureMainCamera,
            TopologyRotationVisualMapping topologyRotationVisualMapping,
            TopologyRotationTweenSettings topologyRotationTweenSettings,
            GameplayCameraSettings cameraSettings,
            TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile,
            TopologyTransitionPostFxProfile topologyTransitionPostFxProfile)
        {
            ConfigureMainCamera = configureMainCamera;
            TopologyRotationVisualMapping = topologyRotationVisualMapping;
            TopologyRotationTweenSettings = topologyRotationTweenSettings;
            CameraSettings = cameraSettings?.Clone() ?? GameplayCameraSettings.CreateShowcaseDefault();
            TopologyTransitionCameraShakeProfile =
                topologyTransitionCameraShakeProfile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            TopologyTransitionPostFxProfile =
                topologyTransitionPostFxProfile?.Clone() ?? TopologyTransitionPostFxProfile.CreateDefault();
        }

        public bool ConfigureMainCamera { get; }

        public TopologyRotationVisualMapping TopologyRotationVisualMapping { get; }

        public TopologyRotationTweenSettings TopologyRotationTweenSettings { get; }

        public GameplayCameraSettings CameraSettings { get; }

        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile { get; }

        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile { get; }
    }

    [DisallowMultipleComponent]
    public sealed class GameplayCameraTopologyAuthoring : MonoBehaviour
    {
        [SerializeField] private bool configureMainCamera = true;
        [SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping =
            TopologyRotationVisualMapping.ForwardUsesPositiveX;
        [SerializeField] private TopologyRotationTweenSettings topologyRotationTweenSettings =
            TopologyRotationTweenSettings.CreateDefault();
        [SerializeField] private GameplayCameraSettings cameraSettings = GameplayCameraSettings.CreateShowcaseDefault();
        [SerializeField] private TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile =
            TopologyTransitionCameraShakeProfile.CreateDefault();
        [SerializeField] private TopologyTransitionPostFxProfile topologyTransitionPostFxProfile =
            TopologyTransitionPostFxProfile.CreateDefault();

        public GameplayCameraTopologyAuthoringSnapshot CreateSnapshot()
        {
            Validate();

            return new GameplayCameraTopologyAuthoringSnapshot(
                configureMainCamera,
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
            return cameraSettings?.Clone() ?? GameplayCameraSettings.CreateShowcaseDefault();
        }

        public TopologyTransitionCameraShakeProfile GetTopologyTransitionCameraShakeProfile()
        {
            Validate();
            return topologyTransitionCameraShakeProfile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
        }

        public TopologyTransitionPostFxProfile GetTopologyTransitionPostFxProfile()
        {
            Validate();
            return topologyTransitionPostFxProfile?.Clone() ?? TopologyTransitionPostFxProfile.CreateDefault();
        }
    }
}
