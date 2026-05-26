using UnityEngine;

namespace Game.Feature.Stages
{
    public enum BackgroundWallSurfaceNoiseSeedMode
    {
        ProfileConstant = 0,
        FaceOnly = 1,
        FaceAndTarget = 2,
    }

    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Background Wall Surface Noise Profile",
        fileName = "BackgroundWallSurfaceNoiseProfile")]
    public sealed class BackgroundWallSurfaceNoiseProfile : ScriptableObject
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private float durationSeconds = 0.45f;
        [SerializeField] private bool useTopologyTransitionDuration = true;
        [SerializeField] private float noiseFrequency = 8f;
        [SerializeField] private float noiseStrength = 1f;
        [SerializeField] private float baseColorNoiseAmount = 0.12f;
        [SerializeField] private float emissionNoiseAmount = 0.16f;
        [SerializeField] private AnimationCurve progressCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));
        [SerializeField] private BackgroundWallSurfaceNoiseSeedMode seedMode =
            BackgroundWallSurfaceNoiseSeedMode.FaceAndTarget;
        [SerializeField] private int seed = 137;

        public bool Enabled => enabled;

        public float DurationSeconds => Mathf.Max(0f, durationSeconds);

        public bool UseTopologyTransitionDuration => useTopologyTransitionDuration;

        public float NoiseFrequency => Mathf.Max(0f, noiseFrequency);

        public float NoiseStrength => Mathf.Max(0f, noiseStrength);

        public float BaseColorNoiseAmount => Mathf.Max(0f, baseColorNoiseAmount);

        public float EmissionNoiseAmount => Mathf.Max(0f, emissionNoiseAmount);

        public AnimationCurve ProgressCurve => progressCurve;

        public BackgroundWallSurfaceNoiseSeedMode SeedMode => seedMode;

        public int Seed => seed;

        public float EvaluateProgress(float normalizedTime)
        {
            if (progressCurve == null)
            {
                return Mathf.Clamp01(normalizedTime);
            }

            return Mathf.Clamp01(progressCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
        }
    }
}
