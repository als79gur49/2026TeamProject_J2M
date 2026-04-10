using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class TopologyTransitionPostFxProfile
    {
        private const float DefaultAngularVelocityResponseExponent = 0.65f;

        [SerializeField] private VolumeProfile authoritativeVolumeProfile;
        [SerializeField] private MotionBlurMode motionBlurMode = MotionBlurMode.CameraOnly;
        [SerializeField] private MotionBlurQuality motionBlurQuality = MotionBlurQuality.Low;
        [SerializeField, Range(0f, 1f)] private float maxBlurIntensity = 0.3f;
        [SerializeField, Range(0f, 0.2f)] private float cameraClamp = 0.05f;
        [SerializeField, Range(0.5f, 1f)] private float angularVelocityResponseExponent =
            DefaultAngularVelocityResponseExponent;
        [SerializeField, Range(0.8f, 0.85f)] private float landingFadeStart01 = 0.82f;
        [SerializeField, Min(1f)] private float landingFadeExponent = 3f;

        public VolumeProfile AuthoritativeVolumeProfile => authoritativeVolumeProfile;

        public MotionBlurMode MotionBlurMode => motionBlurMode;

        public MotionBlurQuality MotionBlurQuality => motionBlurQuality;

        public float MaxBlurIntensity => maxBlurIntensity;

        public float CameraClamp => cameraClamp;

        public float AngularVelocityResponseExponent =>
            angularVelocityResponseExponent > 0.001f
                ? angularVelocityResponseExponent
                : DefaultAngularVelocityResponseExponent;

        public float LandingFadeStart01 => landingFadeStart01;

        public float LandingFadeExponent => landingFadeExponent;

        public static TopologyTransitionPostFxProfile CreateDefault()
        {
            return new TopologyTransitionPostFxProfile();
        }

        public static TopologyTransitionPostFxProfile Create(
            VolumeProfile authoritativeVolumeProfile,
            MotionBlurMode motionBlurMode = MotionBlurMode.CameraOnly,
            MotionBlurQuality motionBlurQuality = MotionBlurQuality.Low,
            float maxBlurIntensity = 0.3f,
            float cameraClamp = 0.05f,
            float angularVelocityResponseExponent = DefaultAngularVelocityResponseExponent,
            float landingFadeStart01 = 0.82f,
            float landingFadeExponent = 3f)
        {
            return new TopologyTransitionPostFxProfile
            {
                authoritativeVolumeProfile = authoritativeVolumeProfile,
                motionBlurMode = motionBlurMode,
                motionBlurQuality = motionBlurQuality,
                maxBlurIntensity = maxBlurIntensity,
                cameraClamp = cameraClamp,
                angularVelocityResponseExponent = angularVelocityResponseExponent,
                landingFadeStart01 = landingFadeStart01,
                landingFadeExponent = landingFadeExponent,
            };
        }

        public TopologyTransitionPostFxProfile Clone()
        {
            return new TopologyTransitionPostFxProfile
            {
                authoritativeVolumeProfile = authoritativeVolumeProfile,
                motionBlurMode = motionBlurMode,
                motionBlurQuality = motionBlurQuality,
                maxBlurIntensity = maxBlurIntensity,
                cameraClamp = cameraClamp,
                angularVelocityResponseExponent = angularVelocityResponseExponent,
                landingFadeStart01 = landingFadeStart01,
                landingFadeExponent = landingFadeExponent,
            };
        }

        public void ApplyDefaults(MotionBlur motionBlur)
        {
            if (motionBlur == null)
            {
                throw new ArgumentNullException(nameof(motionBlur));
            }

            motionBlur.active = true;
            motionBlur.mode.overrideState = true;
            motionBlur.mode.value = motionBlurMode;
            motionBlur.quality.overrideState = true;
            motionBlur.quality.value = motionBlurQuality;
            motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value = 0f;
            motionBlur.clamp.overrideState = true;
            motionBlur.clamp.value = Mathf.Clamp(cameraClamp, 0f, 0.2f);
        }

        public float EvaluateIntensity(in TopologyTransitionVisualState visualState)
        {
            if (!visualState.IsActive)
            {
                return 0f;
            }

            var progress01 = Mathf.Clamp01(visualState.Progress01);
            if (progress01 >= 1f)
            {
                return 0f;
            }

            var angularVelocityEnvelope = Mathf.Pow(
                Mathf.Clamp01(visualState.AngularVelocityNormalized),
                AngularVelocityResponseExponent);
            if (angularVelocityEnvelope <= 0f)
            {
                return 0f;
            }

            var landingFadeStart = Mathf.Clamp(landingFadeStart01, 0f, 0.99f);
            var landingEnvelope = 1f;
            if (progress01 > landingFadeStart)
            {
                var landingT = Mathf.Clamp01((progress01 - landingFadeStart) / (1f - landingFadeStart));
                landingEnvelope = Mathf.Pow(1f - landingT, Mathf.Max(1f, landingFadeExponent));
            }

            var intensity = Mathf.Clamp01(maxBlurIntensity) * angularVelocityEnvelope * landingEnvelope;
            return intensity > 0.0001f ? intensity : 0f;
        }
    }
}
