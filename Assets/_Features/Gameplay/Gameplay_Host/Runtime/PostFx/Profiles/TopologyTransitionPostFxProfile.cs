using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class TopologyTransitionDistortionProfile
    {
        [Header("Impact Pulse")]
        [Min(0f)] public float ImpactStart01 = 0.04f;
        [Min(0.0001f)] public float ImpactDuration01 = 0.16f;
        [Range(-1f, 1f)] public float ImpactIntensity = -0.2f;

        [Header("Landing Pulse")]
        [Min(0f)] public float LandingStart01 = 0.72f;
        [Min(0.0001f)] public float LandingDuration01 = 0.18f;
        [Range(-1f, 1f)] public float LandingIntensity = 0.12f;

        [Header("Lens Shape")]
        [Range(0f, 1f)] public float XMultiplier = 1f;
        [Range(0f, 1f)] public float YMultiplier = 1f;
        public Vector2 Center = new(0.5f, 0.5f);
        [Range(0.01f, 5f)] public float Scale = 1.05f;

        public static TopologyTransitionDistortionProfile CreateDefault()
        {
            return new TopologyTransitionDistortionProfile();
        }

        public static TopologyTransitionDistortionProfile Create(
            float impactStart01 = 0.04f,
            float impactDuration01 = 0.16f,
            float impactIntensity = -0.2f,
            float landingStart01 = 0.72f,
            float landingDuration01 = 0.18f,
            float landingIntensity = 0.12f,
            float xMultiplier = 1f,
            float yMultiplier = 1f,
            Vector2? center = null,
            float scale = 1.05f)
        {
            return new TopologyTransitionDistortionProfile
            {
                ImpactStart01 = impactStart01,
                ImpactDuration01 = impactDuration01,
                ImpactIntensity = impactIntensity,
                LandingStart01 = landingStart01,
                LandingDuration01 = landingDuration01,
                LandingIntensity = landingIntensity,
                XMultiplier = xMultiplier,
                YMultiplier = yMultiplier,
                Center = center ?? new Vector2(0.5f, 0.5f),
                Scale = scale,
            };
        }

        public TopologyTransitionDistortionProfile Clone()
        {
            return new TopologyTransitionDistortionProfile
            {
                ImpactStart01 = ImpactStart01,
                ImpactDuration01 = ImpactDuration01,
                ImpactIntensity = ImpactIntensity,
                LandingStart01 = LandingStart01,
                LandingDuration01 = LandingDuration01,
                LandingIntensity = LandingIntensity,
                XMultiplier = XMultiplier,
                YMultiplier = YMultiplier,
                Center = Center,
                Scale = Scale,
            };
        }

        public void ApplyDefaults(LensDistortion lensDistortion)
        {
            if (lensDistortion == null)
            {
                throw new ArgumentNullException(nameof(lensDistortion));
            }

            lensDistortion.active = true;
            lensDistortion.intensity.overrideState = true;
            lensDistortion.intensity.value = 0f;
            lensDistortion.xMultiplier.overrideState = true;
            lensDistortion.xMultiplier.value = Mathf.Clamp01(XMultiplier);
            lensDistortion.yMultiplier.overrideState = true;
            lensDistortion.yMultiplier.value = Mathf.Clamp01(YMultiplier);
            lensDistortion.center.overrideState = true;
            lensDistortion.center.value = new Vector2(
                Mathf.Clamp01(Center.x),
                Mathf.Clamp01(Center.y));
            lensDistortion.scale.overrideState = true;
            lensDistortion.scale.value = Mathf.Clamp(Scale, 0.01f, 5f);
        }

        public float EvaluateIntensity(in TopologyTransitionVisualState visualState)
        {
            if (!visualState.IsActive ||
                (Mathf.Clamp01(XMultiplier) <= 0f && Mathf.Clamp01(YMultiplier) <= 0f))
            {
                return 0f;
            }

            var progress01 = Mathf.Clamp01(visualState.Progress01);
            var intensity =
                EvaluatePulse(progress01, ImpactStart01, ImpactDuration01, Mathf.Clamp(ImpactIntensity, -1f, 1f)) +
                EvaluatePulse(progress01, LandingStart01, LandingDuration01, Mathf.Clamp(LandingIntensity, -1f, 1f));
            return Mathf.Abs(intensity) > 0.0001f ? intensity : 0f;
        }

        private static float EvaluatePulse(
            float progress01,
            float pulseStart01,
            float pulseDuration01,
            float intensity)
        {
            var clampedDuration01 = Mathf.Max(0.0001f, pulseDuration01);
            if (progress01 < pulseStart01 ||
                progress01 > pulseStart01 + clampedDuration01)
            {
                return 0f;
            }

            var pulseProgress01 = Mathf.InverseLerp(pulseStart01, pulseStart01 + clampedDuration01, progress01);
            var window = Mathf.Sin(Mathf.PI * pulseProgress01);
            return intensity * window * window;
        }
    }

    [Serializable]
    public sealed class TopologyTransitionPostFxProfile
    {
        private const float DefaultAngularVelocityResponseExponent = 0.65f;

        [SerializeField] private VolumeProfile authoritativeVolumeProfile;
        [Header("Motion Blur")]
        [SerializeField] private MotionBlurMode motionBlurMode = MotionBlurMode.CameraOnly;
        [SerializeField] private MotionBlurQuality motionBlurQuality = MotionBlurQuality.Low;
        [SerializeField, Range(0f, 1f)] private float maxBlurIntensity = 0.3f;
        [SerializeField, Range(0f, 0.2f)] private float cameraClamp = 0.05f;
        [SerializeField, Range(0.5f, 1f)] private float angularVelocityResponseExponent =
            DefaultAngularVelocityResponseExponent;
        [SerializeField, Range(0.8f, 0.85f)] private float landingFadeStart01 = 0.82f;
        [SerializeField, Min(1f)] private float landingFadeExponent = 3f;
        [Header("Distortion")]
        [SerializeField] private TopologyTransitionDistortionProfile distortionProfile =
            TopologyTransitionDistortionProfile.CreateDefault();

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

        public TopologyTransitionDistortionProfile DistortionProfile =>
            distortionProfile?.Clone() ?? TopologyTransitionDistortionProfile.CreateDefault();

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
            float landingFadeExponent = 3f,
            TopologyTransitionDistortionProfile distortionProfile = null)
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
                distortionProfile =
                    distortionProfile?.Clone() ?? TopologyTransitionDistortionProfile.CreateDefault(),
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
                distortionProfile =
                    distortionProfile?.Clone() ?? TopologyTransitionDistortionProfile.CreateDefault(),
            };
        }

        public void ApplyMotionBlurDefaults(MotionBlur motionBlur)
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

        public void ApplyDistortionDefaults(LensDistortion lensDistortion)
        {
            ResolveDistortionProfile().ApplyDefaults(lensDistortion);
        }

        public float EvaluateMotionBlurIntensity(in TopologyTransitionVisualState visualState)
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

        public float EvaluateDistortionIntensity(in TopologyTransitionVisualState visualState)
        {
            return ResolveDistortionProfile().EvaluateIntensity(visualState);
        }

        private TopologyTransitionDistortionProfile ResolveDistortionProfile()
        {
            distortionProfile ??= TopologyTransitionDistortionProfile.CreateDefault();
            return distortionProfile;
        }
    }
}
