using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [CreateAssetMenu(
        menuName = "Gameplay/Enemy Presentation/Enemy Inactive Visual Settings",
        fileName = "EnemyInactiveVisualSettings")]
    public sealed class EnemyInactiveVisualSettings : ScriptableObject
    {
        [SerializeField] private Color inactiveTint = new(0.62f, 0.64f, 0.68f, 1f);
        [SerializeField] [Range(0f, 1f)] private float desaturateStrength = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float emissionSuppression = 0.85f;
        [SerializeField] [Min(0.0001f)] private float inactiveRevealInSeconds = 0.25f;
        [SerializeField] [Min(0.0001f)] private float inactiveRevealOutSeconds = 0.18f;
        [SerializeField] private AnimationCurve inactiveRevealCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public Color InactiveTint => inactiveTint;

        public float DesaturateStrength => Mathf.Clamp01(desaturateStrength);

        public float EmissionSuppression => Mathf.Clamp01(emissionSuppression);

        public float InactiveRevealInSeconds => Mathf.Max(0.0001f, inactiveRevealInSeconds);

        public float InactiveRevealOutSeconds => Mathf.Max(0.0001f, inactiveRevealOutSeconds);

        public AnimationCurve InactiveRevealCurve => inactiveRevealCurve;
    }
}
