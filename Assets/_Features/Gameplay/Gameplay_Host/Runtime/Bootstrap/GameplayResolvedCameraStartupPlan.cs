using Unity.Cinemachine;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct GameplayResolvedCameraStartupPlan
    {
        internal GameplayResolvedCameraStartupPlan(
            GameplayCameraSettings resolvedCameraSettings,
            GameplayCameraBaselineAuthoringPolicy cameraBaselineAuthoringPolicy,
            bool configureMainCamera,
            Camera viewCamera,
            Camera outputCamera,
            CinemachineBrain outputCameraBrain,
            TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile,
            GameplayCameraShakeProfile gameplayCameraShakeProfile,
            TopologyTransitionPostFxProfile topologyTransitionPostFxProfile,
            bool usesDirectCameraPath,
            bool usesHierarchyCinemachinePath)
        {
            ResolvedCameraSettings = resolvedCameraSettings?.Clone() ?? GameplayCameraSettings.CreateRuntimeDefault();
            CameraBaselineAuthoringPolicy = cameraBaselineAuthoringPolicy;
            ConfigureMainCamera = configureMainCamera;
            ViewCamera = viewCamera;
            OutputCamera = outputCamera;
            OutputCameraBrain = outputCameraBrain;
            TopologyTransitionCameraShakeProfile =
                topologyTransitionCameraShakeProfile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            GameplayCameraShakeProfile = gameplayCameraShakeProfile;
            TopologyTransitionPostFxProfile =
                topologyTransitionPostFxProfile?.Clone() ?? TopologyTransitionPostFxProfile.CreateDefault();
            UsesDirectCameraPath = usesDirectCameraPath;
            UsesHierarchyCinemachinePath = usesHierarchyCinemachinePath;
        }

        internal GameplayCameraSettings ResolvedCameraSettings { get; }

        internal GameplayCameraBaselineAuthoringPolicy CameraBaselineAuthoringPolicy { get; }

        internal bool ConfigureMainCamera { get; }

        internal Camera ViewCamera { get; }

        internal Camera OutputCamera { get; }

        internal CinemachineBrain OutputCameraBrain { get; }

        internal TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile { get; }

        internal GameplayCameraShakeProfile GameplayCameraShakeProfile { get; }

        internal TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile { get; }

        internal bool UsesDirectCameraPath { get; }

        internal bool UsesHierarchyCinemachinePath { get; }
    }
}
