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

            configuration.SnapViewCameraToTarget = snapshot.ConfigureMainCamera;
            configuration.CameraBaselineAuthoringPolicy = snapshot.BaselineAuthoringPolicy;
            configuration.TopologyRotationVisualMapping = snapshot.TopologyRotationVisualMapping;
            configuration.TopologyRotationTween = snapshot.TopologyRotationTweenSettings;
            configuration.CameraSettings = resolvedCameraSettings?.Clone() ?? GameplayCameraSettings.CreateShowcaseDefault();
            configuration.TopologyTransitionCameraShakeProfile =
                snapshot.TopologyTransitionCameraShakeProfile?.Clone() ??
                TopologyTransitionCameraShakeProfile.CreateDefault();
            configuration.TopologyTransitionPostFxProfile =
                snapshot.TopologyTransitionPostFxProfile?.Clone() ??
                TopologyTransitionPostFxProfile.CreateDefault();
            configuration.ViewCamera = viewCamera;
        }
    }
}
