using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class EnemyAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string aiModeParameterName = "EnemyAiMode";
        [SerializeField] private string movingParameterName = "IsMoving";
        [SerializeField] private string attackTriggerName = "Attack";
        [SerializeField] private string hitTriggerName = "Hit";
        [SerializeField] private string deathTriggerName = "Death";

        public EnemyViewPresentationState LastPresentationState { get; private set; }

        public EnemyAiMode CurrentAiMode { get; private set; }

        public bool IsMoving { get; private set; }

        public bool IsVisible { get; private set; }

        public int AttackSignalCount { get; private set; }

        public int HitSignalCount { get; private set; }

        public int DeathSignalCount { get; private set; }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        public void Apply(in EnemyViewPresentationState state)
        {
            LastPresentationState = state;
            CurrentAiMode = state.AiMode;
            IsMoving = state.IsMoving;

            var targetAnimator = ResolveAnimator();
            if (targetAnimator != null)
            {
                SetIntegerParameter(targetAnimator, aiModeParameterName, (int)state.AiMode);
                SetBoolParameter(targetAnimator, movingParameterName, state.IsMoving);
            }

            if (state.DidAttack)
            {
                AttackSignalCount++;
                SetTrigger(targetAnimator, attackTriggerName);
            }

            if (state.TookDamage)
            {
                HitSignalCount++;
                SetTrigger(targetAnimator, hitTriggerName);
            }

            if (state.DidDie)
            {
                DeathSignalCount++;
                SetTrigger(targetAnimator, deathTriggerName);
            }
        }

        public void SyncRuntimeState(bool isVisible, bool isMoving)
        {
            IsVisible = isVisible;
            IsMoving = isMoving;

            var targetAnimator = ResolveAnimator();
            if (targetAnimator != null)
            {
                SetBoolParameter(targetAnimator, movingParameterName, isMoving);
            }
        }

        private Animator ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            return animator;
        }

        private static void SetBoolParameter(Animator targetAnimator, string parameterName, bool value)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetBool(Animator.StringToHash(parameterName), value);
        }

        private static void SetIntegerParameter(Animator targetAnimator, string parameterName, int value)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetInteger(Animator.StringToHash(parameterName), value);
        }

        private static void SetTrigger(Animator targetAnimator, string parameterName)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetTrigger(Animator.StringToHash(parameterName));
        }
    }
}
