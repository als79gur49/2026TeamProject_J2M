using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayCameraTopologyConfigurationComposer
    {
        internal static void ApplyTo(
            GameplaySceneHostConfiguration configuration,
            in GameplayCameraTopologyAuthoringSnapshot snapshot,
            GameplayCameraSettings resolvedCameraSettings,
            Camera viewCamera)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var sharedTuning = snapshot.SharedTuning ?? GameplayCameraTopologySharedTuning.CreateShowcaseDefault();
            configuration.SnapViewCameraToTarget = snapshot.ConfigureMainCamera;
            configuration.CameraBaselineAuthoringPolicy = snapshot.BaselineAuthoringPolicy;
            configuration.TopologyRotationVisualMapping = sharedTuning.TopologyRotationVisualMapping;
            configuration.TopologyRotationTween = sharedTuning.TopologyRotationTweenSettings;
            configuration.CameraSettings =
                resolvedCameraSettings?.Clone() ??
                sharedTuning.CameraSettings?.Clone() ??
                GameplayCameraSettings.CreateShowcaseDefault();
            configuration.TopologyTransitionCameraShakeProfile =
                sharedTuning.TopologyTransitionCameraShakeProfile?.Clone() ??
                TopologyTransitionCameraShakeProfile.CreateDefault();
            configuration.TopologyTransitionPostFxProfile =
                sharedTuning.TopologyTransitionPostFxProfile?.Clone() ??
                TopologyTransitionPostFxProfile.CreateDefault();
            configuration.ViewCamera = viewCamera;
        }
    }
}
