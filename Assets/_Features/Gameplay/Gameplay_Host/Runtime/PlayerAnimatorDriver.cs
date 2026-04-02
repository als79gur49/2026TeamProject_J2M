using Game.Feature.Gameplay.PlayerControl;
using System;
using System.Collections.Generic;
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
        [SerializeField] private string pushExecuteTriggerName = "PushExecute";
        [SerializeField] private string flipExecuteTriggerName = "FlipExecute";
        [SerializeField] private float crossFadeDurationSeconds = 0.08f;
        [SerializeField] private PlayerActionTimingAuthoring actionTimingAuthoring;

        private bool _pendingRestart;
        private PlayerActionKind _pendingExecuteActionKind;
        private readonly Dictionary<string, float> _clipLengthCache = new();
        private bool _presentationTimingResolved;
        private PlayerActionTimingPresentationSnapshot _presentationTiming;

        public PlayerViewPresentationState LastPresentationState { get; private set; }

        public PlayerViewAnimationState CurrentState { get; private set; }

        public bool IsVisible { get; private set; }

        public int ActionStartSignalCount { get; private set; }

        public int ActionExecuteSignalCount { get; private set; }

        public float PushPresentationDurationSeconds => ResolvePresentationTiming().PushPresentationDurationSeconds;

        public float FlipPresentationDurationSeconds => ResolvePresentationTiming().FlipPresentationDurationSeconds;

        public float CurrentAnimatorSpeed { get; private set; } = 1f;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            actionTimingAuthoring = GetComponent<PlayerActionTimingAuthoring>();
        }

        public void Apply(in PlayerViewPresentationState state)
        {
            LastPresentationState = state;
            if (state.StartedThisTick)
            {
                ActionStartSignalCount++;
                _pendingRestart = true;
            }

            if (state.ExecutedThisTick)
            {
                ActionExecuteSignalCount++;
                _pendingExecuteActionKind = state.ActiveActionKind;
            }
        }

        public void SyncRuntimeState(bool isVisible, PlayerViewAnimationState resolvedState)
        {
            IsVisible = isVisible;

            var restart = _pendingRestart;
            _pendingRestart = false;
            var executeActionKind = _pendingExecuteActionKind;
            _pendingExecuteActionKind = PlayerActionKind.None;
            ApplyResolvedState(resolvedState, restart, executeActionKind);
        }

        private void ApplyResolvedState(
            PlayerViewAnimationState resolvedState,
            bool restart,
            PlayerActionKind executeActionKind)
        {
            var targetAnimator = ResolveAnimator();
            ApplyAnimatorSpeed(targetAnimator, resolvedState);
            if (targetAnimator != null)
            {
                SetIntegerParameter(targetAnimator, stateParameterName, (int)resolvedState);
            }

            if (resolvedState == CurrentState &&
                !restart)
            {
                ApplyExecuteSignal(targetAnimator, executeActionKind, resolvedState);
                return;
            }

            CurrentState = resolvedState;
            CrossFadeState(targetAnimator, ResolveStateName(resolvedState), crossFadeDurationSeconds);
            ApplyExecuteSignal(targetAnimator, executeActionKind, resolvedState);
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

        private PlayerActionTimingPresentationSnapshot ResolvePresentationTiming()
        {
            if (_presentationTimingResolved)
            {
                return _presentationTiming;
            }

            if (actionTimingAuthoring == null)
            {
                actionTimingAuthoring = GetComponent<PlayerActionTimingAuthoring>();
            }

            if (actionTimingAuthoring == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(PlayerAnimatorDriver)} requires {nameof(PlayerActionTimingAuthoring)} on the same root.");
            }

            _presentationTiming = actionTimingAuthoring.CreatePresentationSnapshot();
            _presentationTimingResolved = true;
            return _presentationTiming;
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

        private void ApplyAnimatorSpeed(Animator targetAnimator, PlayerViewAnimationState resolvedState)
        {
            var targetSpeed = ResolveAnimatorSpeed(targetAnimator, resolvedState);
            CurrentAnimatorSpeed = targetSpeed;

            if (targetAnimator != null)
            {
                targetAnimator.speed = targetSpeed;
            }
        }

        private float ResolveAnimatorSpeed(Animator targetAnimator, PlayerViewAnimationState resolvedState)
        {
            if (resolvedState != PlayerViewAnimationState.Push &&
                resolvedState != PlayerViewAnimationState.Flip)
            {
                return 1f;
            }

            var presentationDurationSeconds = resolvedState == PlayerViewAnimationState.Push
                ? ResolvePresentationTiming().PushPresentationDurationSeconds
                : ResolvePresentationTiming().FlipPresentationDurationSeconds;
            var referenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(targetAnimator, ResolveStateName(resolvedState));
            return Mathf.Max(0.01f, referenceClipLengthSeconds / presentationDurationSeconds);
        }

        private float ResolveReferenceClipLengthSeconds(Animator targetAnimator, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return 1f;
            }

            if (_clipLengthCache.TryGetValue(stateName, out var cachedLength))
            {
                return cachedLength;
            }

            var clips = targetAnimator?.runtimeAnimatorController?.animationClips;
            if (clips != null)
            {
                for (var i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i];
                    if (clip != null &&
                        string.Equals(clip.name, stateName, StringComparison.Ordinal))
                    {
                        var resolvedLength = Mathf.Max(clip.length, 0.01f);
                        _clipLengthCache[stateName] = resolvedLength;
                        return resolvedLength;
                    }
                }
            }

            _clipLengthCache[stateName] = 1f;
            return 1f;
        }

        private void ApplyExecuteSignal(
            Animator targetAnimator,
            PlayerActionKind executeActionKind,
            PlayerViewAnimationState resolvedState)
        {
            if (executeActionKind == PlayerActionKind.None)
            {
                return;
            }

            var executeTriggerName = ResolveExecuteTriggerName(executeActionKind);
            if (!string.IsNullOrWhiteSpace(executeTriggerName))
            {
                SetTriggerParameter(targetAnimator, executeTriggerName);
                return;
            }

            if (resolvedState == PlayerViewAnimationState.Push ||
                resolvedState == PlayerViewAnimationState.Flip)
            {
                CrossFadeState(targetAnimator, ResolveStateName(resolvedState), 0f);
            }
        }

        private string ResolveExecuteTriggerName(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => pushExecuteTriggerName,
                PlayerActionKind.Flip => flipExecuteTriggerName,
                _ => null,
            };
        }

        private static void SetTriggerParameter(Animator targetAnimator, string parameterName)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetTrigger(Animator.StringToHash(parameterName));
        }
    }
}
