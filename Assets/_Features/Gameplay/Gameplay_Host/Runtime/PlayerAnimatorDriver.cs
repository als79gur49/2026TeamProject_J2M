using Game.Feature.Gameplay.PlayerControl;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Feature.Gameplay.Host
{
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        private const string OptionalStateParameterName = "PlayerPresentationState";

        [SerializeField] private Animator animator;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private string pushStateName = "Push";
        [SerializeField] private string flipStateName = "Flip";
        [FormerlySerializedAs("crossFadeDurationSeconds")]
        [SerializeField] private float stateTransitionCrossFadeDurationSeconds = 0.08f;
        [FormerlySerializedAs("actionTimingAuthoring")]
        [SerializeField] private PlayerAnimationTimingAuthoring animationTimingAuthoring;

        private bool _pendingRestart;
        private PlayerActionKind _pendingExecuteActionKind;
        private readonly Dictionary<string, float> _clipLengthCache = new();
        private RuntimeAnimatorController _cachedClipLengthController;
        private Animator _validatedOptionalStateParameterAnimator;
        private RuntimeAnimatorController _validatedOptionalStateParameterController;
        private bool _optionalStateParameterSupported;
        private bool _animationTimingResolved;
        private PlayerAnimationTimingSnapshot _animationTiming;

        public PlayerViewPresentationState LastPresentationState { get; private set; }

        public PlayerViewAnimationState CurrentState { get; private set; }

        public bool IsVisible { get; private set; }

        public int ActionStartSignalCount { get; private set; }

        public int ActionExecuteSignalCount { get; private set; }

        public float PushPresentationDurationSeconds => GetPresentationDurationSeconds(PlayerActionKind.Push);

        public float FlipPresentationDurationSeconds => GetPresentationDurationSeconds(PlayerActionKind.Flip);

        public float CurrentAnimatorSpeed { get; private set; } = 1f;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            animationTimingAuthoring = GetComponent<PlayerAnimationTimingAuthoring>();
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

        public void SyncRuntimeState(
            bool isVisible,
            PlayerViewAnimationState resolvedState,
            float resolvedMotionDurationSeconds = 0f)
        {
            IsVisible = isVisible;

            var restart = _pendingRestart;
            _pendingRestart = false;
            var executeActionKind = _pendingExecuteActionKind;
            _pendingExecuteActionKind = PlayerActionKind.None;
            ApplyResolvedState(resolvedState, restart, executeActionKind, resolvedMotionDurationSeconds);
        }

        public float GetPresentationDurationSeconds(
            PlayerActionKind actionKind,
            float resolvedMotionDurationSeconds = 0f)
        {
            var stateName = actionKind switch
            {
                PlayerActionKind.Push => pushStateName,
                PlayerActionKind.Flip => flipStateName,
                _ => string.Empty,
            };

            return ResolvePresentationDurationSeconds(
                actionKind,
                resolvedMotionDurationSeconds,
                ResolveAnimator(),
                stateName);
        }

        private void ApplyResolvedState(
            PlayerViewAnimationState resolvedState,
            bool restart,
            PlayerActionKind executeActionKind,
            float resolvedMotionDurationSeconds)
        {
            var targetAnimator = ResolveAnimator();
            ApplyAnimatorSpeed(targetAnimator, resolvedState, resolvedMotionDurationSeconds);
            SyncOptionalStateParameter(targetAnimator, resolvedState);

            if (resolvedState == CurrentState &&
                !restart)
            {
                ApplyExecuteSignal(targetAnimator, executeActionKind, resolvedState);
                return;
            }

            CurrentState = resolvedState;
            TransitionToResolvedState(targetAnimator, resolvedState);
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

        private PlayerAnimationTimingSnapshot ResolveAnimationTiming()
        {
            if (_animationTimingResolved)
            {
                return _animationTiming;
            }

            if (animationTimingAuthoring == null)
            {
                animationTimingAuthoring = GetComponent<PlayerAnimationTimingAuthoring>();
            }

            if (animationTimingAuthoring == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(PlayerAnimatorDriver)} requires {nameof(PlayerAnimationTimingAuthoring)} on the same root.");
            }

            _animationTiming = animationTimingAuthoring.CreateSnapshot();
            _animationTimingResolved = true;
            return _animationTiming;
        }

        private void TransitionToResolvedState(Animator targetAnimator, PlayerViewAnimationState resolvedState)
        {
            CrossFadeState(
                targetAnimator,
                ResolveStateName(resolvedState),
                stateTransitionCrossFadeDurationSeconds);
        }

        private static void CrossFadeState(Animator targetAnimator, string stateName, float durationSeconds)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            targetAnimator.CrossFadeInFixedTime(Animator.StringToHash(stateName), Mathf.Max(0f, durationSeconds));
        }

        private void SyncOptionalStateParameter(Animator targetAnimator, PlayerViewAnimationState resolvedState)
        {
            if (!SupportsOptionalStateParameter(targetAnimator))
            {
                return;
            }

            targetAnimator.SetInteger(Animator.StringToHash(OptionalStateParameterName), (int)resolvedState);
        }

        private bool SupportsOptionalStateParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            var controller = targetAnimator.runtimeAnimatorController;

            if (_validatedOptionalStateParameterAnimator == targetAnimator)
            {
                if (_validatedOptionalStateParameterController == controller)
                {
                    return _optionalStateParameterSupported;
                }
            }

            _validatedOptionalStateParameterAnimator = targetAnimator;
            _validatedOptionalStateParameterController = controller;
            _optionalStateParameterSupported = false;

            var parameterHash = Animator.StringToHash(OptionalStateParameterName);
            var parameters = targetAnimator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.nameHash == parameterHash &&
                    parameter.type == AnimatorControllerParameterType.Int)
                {
                    _optionalStateParameterSupported = true;
                    break;
                }
            }

            return _optionalStateParameterSupported;
        }

        private void ApplyAnimatorSpeed(
            Animator targetAnimator,
            PlayerViewAnimationState resolvedState,
            float resolvedMotionDurationSeconds)
        {
            var targetSpeed = ResolveAnimatorSpeed(targetAnimator, resolvedState, resolvedMotionDurationSeconds);
            CurrentAnimatorSpeed = targetSpeed;

            if (targetAnimator != null)
            {
                targetAnimator.speed = targetSpeed;
            }
        }

        private float ResolveAnimatorSpeed(
            Animator targetAnimator,
            PlayerViewAnimationState resolvedState,
            float resolvedMotionDurationSeconds)
        {
            if (resolvedState != PlayerViewAnimationState.Push &&
                resolvedState != PlayerViewAnimationState.Flip)
            {
                return 1f;
            }

            var actionKind = resolvedState == PlayerViewAnimationState.Push
                ? PlayerActionKind.Push
                : PlayerActionKind.Flip;
            var stateName = ResolveStateName(resolvedState);
            var presentationDurationSeconds = ResolvePresentationDurationSeconds(
                actionKind,
                resolvedMotionDurationSeconds,
                targetAnimator,
                stateName);
            var referenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(targetAnimator, stateName);
            return Mathf.Max(0.01f, referenceClipLengthSeconds / presentationDurationSeconds);
        }

        private float ResolvePresentationDurationSeconds(
            PlayerActionKind actionKind,
            float resolvedMotionDurationSeconds,
            Animator targetAnimator,
            string stateName)
        {
            var animationTiming = ResolveAnimationTiming();
            if (animationTiming.TryGetAnimatorDurationOverride(actionKind, out var animatorDurationSeconds))
            {
                return animatorDurationSeconds;
            }

            if (resolvedMotionDurationSeconds > 0f)
            {
                return resolvedMotionDurationSeconds;
            }

            return ResolveReferenceClipLengthSeconds(targetAnimator, stateName);
        }

        private float ResolveReferenceClipLengthSeconds(Animator targetAnimator, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return 1f;
            }

            var controller = targetAnimator?.runtimeAnimatorController;
            if (_cachedClipLengthController != controller)
            {
                _clipLengthCache.Clear();
                _cachedClipLengthController = controller;
            }

            if (_clipLengthCache.TryGetValue(stateName, out var cachedLength))
            {
                return cachedLength;
            }

            var clips = controller?.animationClips;
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

            if (executeActionKind != PlayerActionKind.Push &&
                executeActionKind != PlayerActionKind.Flip)
            {
                return;
            }

            if (resolvedState != PlayerViewAnimationState.Push &&
                resolvedState != PlayerViewAnimationState.Flip)
            {
                return;
            }

            CrossFadeState(targetAnimator, ResolveStateName(resolvedState), 0f);
        }
    }
}
