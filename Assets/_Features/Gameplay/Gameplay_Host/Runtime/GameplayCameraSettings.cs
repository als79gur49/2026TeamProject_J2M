using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class GameplayCameraSettings
    {
        public float PitchDegrees = 35f;
        public float YawDegrees = 0f;
        public GameplayCameraRig.DistanceMode DistanceMode = GameplayCameraRig.DistanceMode.AutoFit;
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
                PitchDegrees = 18f,
                YawDegrees = 0f,
                DistanceMode = GameplayCameraRig.DistanceMode.AutoFit,
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
                PitchDegrees = 35f,
                YawDegrees = 0f,
                DistanceMode = GameplayCameraRig.DistanceMode.Manual,
                ManualDistance = 16f,
                FramingPadding = 1.2f,
                PerspectiveFieldOfView = 50f,
                NearClipPlane = 0.03f,
                FarClipPlane = 100f,
                ClearFlags = CameraClearFlags.SolidColor,
                BackgroundColor = new Color(0.93f, 0.95f, 0.98f),
            };
        }

        public static GameplayCameraSettings CreateSampleDefault()
        {
            var settings = CreateShowcaseDefault();
            settings.BackgroundColor = new Color(0.92f, 0.94f, 0.98f);
            return settings;
        }
    }
}
