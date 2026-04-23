using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class GameplayCameraSettings
    {
        [Tooltip("Use the initially authored scene or Cinemachine camera pose as the runtime camera baseline.")]
        public bool UseAuthoredSceneCameraPose;
        [Tooltip("When using the authored camera baseline, also copy its lens settings.")]
        public bool UseAuthoredSceneCameraLens = true;
        public float PitchDegrees = 35f;
        public float YawDegrees = 0f;
        public CameraDistanceMode DistanceMode = CameraDistanceMode.AutoFit;
        public float ManualDistance = 8f;
        public float FramingPadding = 1.2f;
        public float PerspectiveFieldOfView = 60f;
        public float NearClipPlane = 0.03f;
        public float FarClipPlane = 100f;
        public CameraClearFlags ClearFlags = CameraClearFlags.Skybox;
        public Color BackgroundColor = Color.black;

        public GameplayCameraSettings Clone()
        {
            return new GameplayCameraSettings
            {
                UseAuthoredSceneCameraPose = UseAuthoredSceneCameraPose,
                UseAuthoredSceneCameraLens = UseAuthoredSceneCameraLens,
                PitchDegrees = PitchDegrees,
                YawDegrees = YawDegrees,
                DistanceMode = DistanceMode,
                ManualDistance = ManualDistance,
                FramingPadding = FramingPadding,
                PerspectiveFieldOfView = PerspectiveFieldOfView,
                NearClipPlane = NearClipPlane,
                FarClipPlane = FarClipPlane,
                ClearFlags = ClearFlags,
                BackgroundColor = BackgroundColor,
            };
        }

        public static GameplayCameraSettings CreateRuntimeDefault()
        {
            return new GameplayCameraSettings
            {
                UseAuthoredSceneCameraPose = false,
                UseAuthoredSceneCameraLens = true,
                PitchDegrees = 18f,
                YawDegrees = 0f,
                DistanceMode = CameraDistanceMode.AutoFit,
                ManualDistance = 8f,
                FramingPadding = 1.2f,
                PerspectiveFieldOfView = 60f,
                NearClipPlane = 0.03f,
                FarClipPlane = 100f,
                ClearFlags = CameraClearFlags.Skybox,
                BackgroundColor = Color.black,
            };
        }

        public static GameplayCameraSettings CreateShowcaseDefault()
        {
            return new GameplayCameraSettings
            {
                UseAuthoredSceneCameraPose = true,
                UseAuthoredSceneCameraLens = true,
                PitchDegrees = 35f,
                YawDegrees = 0f,
                DistanceMode = CameraDistanceMode.Manual,
                ManualDistance = 16f,
                FramingPadding = 1.2f,
                PerspectiveFieldOfView = 50f,
                NearClipPlane = 0.03f,
                FarClipPlane = 100f,
                ClearFlags = CameraClearFlags.SolidColor,
                BackgroundColor = new Color(0.93f, 0.95f, 0.98f),
            };
        }

    }
}
