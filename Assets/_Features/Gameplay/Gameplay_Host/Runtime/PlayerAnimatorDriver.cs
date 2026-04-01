using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string stateParameterName = "PlayerPresentationState";
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private string pushStateName = "Push";
        [SerializeField] private string flipStateName = "Flip";
        [SerializeField] private float crossFadeDurationSeconds = 0.08f;

        private bool _pendingRestart;

        public PlayerViewPresentationState LastPresentationState { get; private set; }

        public PlayerViewAnimationState CurrentState { get; private set; }

        public bool IsVisible { get; private set; }

        public int ActionStartSignalCount { get; private set; }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        public void Apply(in PlayerViewPresentationState state)
        {
            LastPresentationState = state;
            if (state.StartedThisTick)
            {
                ActionStartSignalCount++;
                _pendingRestart = true;
            }
        }

        public void SyncRuntimeState(bool isVisible, PlayerViewAnimationState resolvedState)
        {
            IsVisible = isVisible;

            var restart = _pendingRestart;
            _pendingRestart = false;
            ApplyResolvedState(resolvedState, restart);
        }

        private void ApplyResolvedState(PlayerViewAnimationState resolvedState, bool restart)
        {
            var targetAnimator = ResolveAnimator();
            if (targetAnimator != null)
            {
                SetIntegerParameter(targetAnimator, stateParameterName, (int)resolvedState);
            }

            if (resolvedState == CurrentState &&
                !restart)
            {
                return;
            }

            CurrentState = resolvedState;
            CrossFadeState(targetAnimator, ResolveStateName(resolvedState), crossFadeDurationSeconds);
        }

        private string ResolveStateName(PlayerViewAnimationState state)
        {
            return state switch
            {
                PlayerViewAnimationState.Walk => walkStateName,
                PlayerViewAnimationState.Push => pushStateName,
                PlayerViewAnimationState.Flip => flipStateName,
                _ => idleStateName,
            };
        }

        private Animator ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            return animator;
        }

        private static void CrossFadeState(Animator targetAnimator, string stateName, float durationSeconds)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            targetAnimator.CrossFadeInFixedTime(Animator.StringToHash(stateName), Mathf.Max(0f, durationSeconds));
        }

        private static void SetIntegerParameter(Animator targetAnimator, string parameterName, int value)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetInteger(Animator.StringToHash(parameterName), value);
        }
    }
}
