using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Loop;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct PlayerAnimationPlaybackRequest
    {
        public PlayerAnimationPlaybackRequest(
            bool isVisible,
            PlayerViewAnimationState state,
            PlayerPresentationPhase phaseOverride = PlayerPresentationPhase.None,
            bool restart = false,
            float resolvedMotionDurationSeconds = 0f)
        {
            IsVisible = isVisible;
            State = state;
            PhaseOverride = phaseOverride;
            Restart = restart;
            ResolvedMotionDurationSeconds = resolvedMotionDurationSeconds;
        }

        public bool IsVisible { get; }

        public PlayerViewAnimationState State { get; }

        public PlayerPresentationPhase PhaseOverride { get; }

        public bool Restart { get; }

        public float ResolvedMotionDurationSeconds { get; }
    }

    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        private const string OptionalStateParameterName = "PlayerPresentationState";
        private const string OptionalFlipOutcomeParameterName = "FlipOutcome";

        [SerializeField] private Animator animator;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk_Loop";
        [FormerlySerializedAs("pushStateName")]
        [SerializeField] private string pushWindupStateName = "Push_Windup";
        [FormerlySerializedAs("pushExitStateName")]
        [SerializeField] private string pushRecoveryStateName = "Push_Recovery";
        [FormerlySerializedAs("flipStateName")]
        [SerializeField] private string flipWindupStateName = "Flip_Windup";
        [FormerlySerializedAs("flipExitStateName")]
        [SerializeField] private string flipRecoveryStateName = "Flip_Recovery";
        [SerializeField] private string deathStateName = "Death";
        [SerializeField] private string stageClearVictoryStateName = "Item";
        [SerializeField] private string hitTriggerName = "Hit";
        [SerializeField] private string walkExitStateName;
        [FormerlySerializedAs("crossFadeDurationSeconds")]
        [SerializeField] private float stateTransitionCrossFadeDurationSeconds = 0.08f;
        [FormerlySerializedAs("actionTimingAuthoring")]
        [SerializeField] private PlayerAnimationTimingAuthoring animationTimingAuthoring;

        private bool _pendingRestart;
        private bool _pendingHitTrigger;
        private bool _hasDrivenResolvedState;
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

        public PlayerPresentationPhase CurrentPresentationPhase { get; private set; }

        public bool IsVisible { get; private set; }

        public int ActionStartSignalCount { get; private set; }

        public int ActionExecuteSignalCount { get; private set; }

        public int HitSignalCount { get; private set; }

        public float PushPresentationDurationSeconds => GetPresentationDurationSeconds(PlayerActionKind.Push);

        public float FlipPresentationDurationSeconds => GetPresentationDurationSeconds(PlayerActionKind.Flip);

        public float DeathPresentationDurationSeconds => GetDeathPresentationDurationSeconds();

        public float StageClearVictoryPresentationDurationSeconds => GetStageClearVictoryPresentationDurationSeconds();

        public float CurrentAnimatorSpeed { get; private set; } = 1f;

        public float CurrentPresentationDurationSeconds { get; private set; }

        public float LastCrossFadeDurationSeconds { get; private set; }

        public string LastCrossFadedStateName { get; private set; } = string.Empty;

        internal int CrossFadeCommandCount { get; private set; }

        internal int TriggerWriteCount { get; private set; }

        internal bool CanDriveCurrentAnimator => CanDriveAnimator(ResolveAnimator());

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

            if (state.HasActionAttempt)
            {
                ActionStartSignalCount++;
                _pendingRestart = true;
            }

            if (state.ExecutedThisTick)
            {
                ActionExecuteSignalCount++;
                _pendingExecuteActionKind = state.ActiveActionKind;
            }

            if (state.TookDamageThisTick && !state.DidDie)
            {
                HitSignalCount++;
                _pendingHitTrigger = true;
            }
        }

        public void SyncRuntimeState(
            bool isVisible,
            PlayerViewAnimationState resolvedState,
            float resolvedMotionDurationSeconds = 0f)
        {
            SyncRuntimeState(new PlayerAnimationPlaybackRequest(
                isVisible,
                resolvedState,
                PlayerPresentationPhase.None,
                restart: false,
                resolvedMotionDurationSeconds));
        }

        public void SyncRuntimeState(in PlayerAnimationPlaybackRequest request)
        {
            IsVisible = request.IsVisible;

            var targetAnimator = ResolveAnimator();
            if (!CanDriveAnimator(targetAnimator))
            {
                return;
            }

            var restart = _pendingRestart || request.Restart;
            var shouldTriggerHit = _pendingHitTrigger;
            var executeActionKind = _pendingExecuteActionKind;
            if (!ApplyResolvedState(
                targetAnimator,
                request.State,
                restart,
                executeActionKind,
                request.ResolvedMotionDurationSeconds,
                request.PhaseOverride))
            {
                return;
            }

            _pendingRestart = false;
            _pendingHitTrigger = false;
            _pendingExecuteActionKind = PlayerActionKind.None;
            if (shouldTriggerHit)
            {
                FireHitTrigger(targetAnimator);
            }
        }

        public void SyncHiddenRuntimeState()
        {
            IsVisible = false;
        }

        public float GetPresentationDurationSeconds(
            PlayerActionKind actionKind,
            float resolvedMotionDurationSeconds = 0f)
        {
            return ResolveActionPresentationDurationSeconds(
                ResolveAnimator(),
                actionKind,
                resolvedMotionDurationSeconds);
        }

        public float GetPresentationDurationSeconds(
            PlayerPresentationPhase phase,
            float resolvedMotionDurationSeconds = 0f)
        {
            return ResolvePhasePresentationDurationSeconds(
                ResolveAnimator(),
                phase,
                resolvedMotionDurationSeconds);
        }

        public float GetActionHoldPresentationDurationSeconds(
            PlayerActionKind actionKind,
            float resolvedMotionDurationSeconds = 0f)
        {
            return ResolveActionHoldPresentationDurationSeconds(
                ResolveAnimator(),
                actionKind,
                resolvedMotionDurationSeconds);
        }

        public float GetDeathClipLengthSeconds()
        {
            return ResolveStateReferenceClipLengthSeconds(ResolveAnimator(), deathStateName);
        }

        public float GetDeathPresentationDurationSeconds()
        {
            return ResolveDeathPresentationDurationSeconds(ResolveAnimator());
        }

        public float GetStageClearVictoryPresentationDurationSeconds()
        {
            return ResolveStageClearVictoryPresentationDurationSeconds(ResolveAnimator());
        }

        private bool ApplyResolvedState(
            Animator targetAnimator,
            PlayerViewAnimationState resolvedState,
            bool restart,
            PlayerActionKind executeActionKind,
            float resolvedMotionDurationSeconds,
            PlayerPresentationPhase phaseOverride = PlayerPresentationPhase.None)
        {
            if (!CanDriveAnimator(targetAnimator))
            {
                return false;
            }

            var previousState = CurrentState;
            var previousPhase = CurrentPresentationPhase;
            var targetPhase = phaseOverride != PlayerPresentationPhase.None
                ? phaseOverride
                : ResolveTargetPresentationPhase(
                    resolvedState,
                    restart,
                    executeActionKind,
                    previousState,
                    previousPhase);

            ApplyAnimatorSpeed(targetAnimator, resolvedState, targetPhase, resolvedMotionDurationSeconds);
            SyncOptionalStateParameter(targetAnimator, resolvedState);
            SyncOptionalFlipOutcomeParameter(targetAnimator, LastPresentationState.FlipOutcome);

            var stateChanged = resolvedState != previousState;
            var phaseChanged = targetPhase != previousPhase;

            if (!stateChanged &&
                !phaseChanged &&
                !restart &&
                _hasDrivenResolvedState)
            {
                CurrentState = resolvedState;
                CurrentPresentationPhase = targetPhase;
                return true;
            }

            if (!TransitionToResolvedState(targetAnimator, resolvedState, targetPhase))
            {
                return false;
            }

            CurrentState = resolvedState;
            CurrentPresentationPhase = targetPhase;
            _hasDrivenResolvedState = true;
            return true;
        }

        private string ResolveLocomotionStateName(PlayerViewAnimationState state)
        {
            return state switch
            {
                PlayerViewAnimationState.WalkLoop => walkStateName,
                PlayerViewAnimationState.Death => deathStateName,
                PlayerViewAnimationState.StageClearVictory => stageClearVictoryStateName,
                _ => idleStateName,
            };
        }

        private string ResolvePhaseStateName(PlayerPresentationPhase phase)
        {
            return phase switch
            {
                PlayerPresentationPhase.PushWindup => pushWindupStateName,
                PlayerPresentationPhase.PushRecovery => pushRecoveryStateName,
                PlayerPresentationPhase.FlipWindup => flipWindupStateName,
                PlayerPresentationPhase.FlipRecovery => flipRecoveryStateName,
                _ => string.Empty,
            };
        }

        private PlayerActionKind ResolveActionKind(PlayerPresentationPhase phase)
        {
            return phase switch
            {
                PlayerPresentationPhase.PushWindup => PlayerActionKind.Push,
                PlayerPresentationPhase.PushRecovery => PlayerActionKind.Push,
                PlayerPresentationPhase.FlipWindup => PlayerActionKind.Flip,
                PlayerPresentationPhase.FlipRecovery => PlayerActionKind.Flip,
                _ => PlayerActionKind.None,
            };
        }

        private PlayerPresentationPhase ResolveWindupPhase(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => PlayerPresentationPhase.PushWindup,
                PlayerActionKind.Flip => PlayerPresentationPhase.FlipWindup,
                _ => PlayerPresentationPhase.None,
            };
        }

        private PlayerPresentationPhase ResolveRecoveryPhase(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => PlayerPresentationPhase.PushRecovery,
                PlayerActionKind.Flip => PlayerPresentationPhase.FlipRecovery,
                _ => PlayerPresentationPhase.None,
            };
        }

        private bool TryResolveActionKind(PlayerViewAnimationState state, out PlayerActionKind actionKind)
        {
            switch (state)
            {
                case PlayerViewAnimationState.Push:
                    actionKind = PlayerActionKind.Push;
                    return true;

                case PlayerViewAnimationState.Flip:
                    actionKind = PlayerActionKind.Flip;
                    return true;

                default:
                    actionKind = PlayerActionKind.None;
                    return false;
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

        private bool TransitionToResolvedState(
            Animator targetAnimator,
            PlayerViewAnimationState resolvedState,
            PlayerPresentationPhase targetPhase)
        {
            var stateName = targetPhase != PlayerPresentationPhase.None
                ? ResolvePhaseStateName(targetPhase)
                : ResolveLocomotionStateName(resolvedState);
            return CrossFadeState(
                targetAnimator,
                stateName,
                stateTransitionCrossFadeDurationSeconds);
        }

        private bool CrossFadeState(Animator targetAnimator, string stateName, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            if (!CanDriveAnimator(targetAnimator))
            {
                return false;
            }

            var resolvedDurationSeconds = Mathf.Max(0f, durationSeconds);
            targetAnimator.CrossFadeInFixedTime(Animator.StringToHash(stateName), resolvedDurationSeconds);
            LastCrossFadedStateName = stateName;
            LastCrossFadeDurationSeconds = resolvedDurationSeconds;
            CrossFadeCommandCount++;
            return true;
        }

        private void SyncOptionalStateParameter(Animator targetAnimator, PlayerViewAnimationState resolvedState)
        {
            if (!CanDriveAnimator(targetAnimator))
            {
                return;
            }

            if (!SupportsOptionalStateParameter(targetAnimator))
            {
                return;
            }

            targetAnimator.SetInteger(Animator.StringToHash(OptionalStateParameterName), (int)resolvedState);
        }

        private void SyncOptionalFlipOutcomeParameter(
            Animator targetAnimator,
            TickPlayerFlipOutcomeKind flipOutcome)
        {
            if (!CanDriveAnimator(targetAnimator) ||
                !HasAnimatorParameter(targetAnimator, OptionalFlipOutcomeParameterName, AnimatorControllerParameterType.Int))
            {
                return;
            }

            targetAnimator.SetInteger(Animator.StringToHash(OptionalFlipOutcomeParameterName), (int)flipOutcome);
        }

        private void FireHitTrigger(Animator targetAnimator)
        {
            if (!CanDriveAnimator(targetAnimator) ||
                string.IsNullOrWhiteSpace(hitTriggerName) ||
                !HasAnimatorParameter(targetAnimator, hitTriggerName, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            targetAnimator.SetTrigger(hitTriggerName);
            TriggerWriteCount++;
        }

        private static bool HasAnimatorParameter(
            Animator targetAnimator,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (targetAnimator == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            var parameters = targetAnimator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == parameterType &&
                    string.Equals(parameters[i].name, parameterName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool SupportsOptionalStateParameter(Animator targetAnimator)
        {
            if (!CanDriveAnimator(targetAnimator))
            {
                return false;
            }

            var controller = targetAnimator.runtimeAnimatorController;

            if (_validatedOptionalStateParameterAnimator == targetAnimator &&
                _validatedOptionalStateParameterController == controller)
            {
                return _optionalStateParameterSupported;
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
            PlayerPresentationPhase phase,
            float resolvedMotionDurationSeconds)
        {
            var targetSpeed = ResolveAnimatorSpeed(
                targetAnimator,
                resolvedState,
                phase,
                resolvedMotionDurationSeconds,
                out var presentationDurationSeconds);
            CurrentAnimatorSpeed = targetSpeed;
            CurrentPresentationDurationSeconds = presentationDurationSeconds;

            if (CanDriveAnimator(targetAnimator))
            {
                targetAnimator.speed = targetSpeed;
            }
        }

        private static bool CanDriveAnimator(Animator targetAnimator)
        {
            return targetAnimator != null &&
                   targetAnimator.runtimeAnimatorController != null &&
                   targetAnimator.enabled &&
                   targetAnimator.isActiveAndEnabled &&
                   targetAnimator.gameObject.activeInHierarchy;
        }

        private float ResolveAnimatorSpeed(
            Animator targetAnimator,
            PlayerViewAnimationState resolvedState,
            PlayerPresentationPhase phase,
            float resolvedMotionDurationSeconds,
            out float presentationDurationSeconds)
        {
            float referenceClipLengthSeconds;

            if (resolvedState == PlayerViewAnimationState.Death)
            {
                presentationDurationSeconds = ResolveDeathPresentationDurationSeconds(targetAnimator);
                referenceClipLengthSeconds = ResolveStateReferenceClipLengthSeconds(targetAnimator, deathStateName);
            }
            else if (resolvedState == PlayerViewAnimationState.StageClearVictory)
            {
                presentationDurationSeconds = ResolveStageClearVictoryPresentationDurationSeconds(targetAnimator);
                referenceClipLengthSeconds = ResolveStateReferenceClipLengthSeconds(targetAnimator, stageClearVictoryStateName);
            }
            else
            {
                if (phase == PlayerPresentationPhase.None)
                {
                    presentationDurationSeconds = 0f;
                    return 1f;
                }

                presentationDurationSeconds = ResolvePhasePresentationDurationSeconds(
                    targetAnimator,
                    phase,
                    resolvedMotionDurationSeconds);
                referenceClipLengthSeconds = ResolvePhaseReferenceClipLengthSeconds(targetAnimator, phase);
            }

            if (presentationDurationSeconds <= 0f)
            {
                presentationDurationSeconds = 0.01f;
            }

            if (referenceClipLengthSeconds <= 0f)
            {
                referenceClipLengthSeconds = 1f;
            }

            return Mathf.Max(0.01f, referenceClipLengthSeconds / presentationDurationSeconds);
        }

        private float ResolveDeathPresentationDurationSeconds(Animator targetAnimator)
        {
            var animationTiming = ResolveAnimationTiming();
            if (animationTiming.TryGetDeathAnimatorDurationOverride(out var durationSeconds))
            {
                return durationSeconds;
            }

            return ResolveStateReferenceClipLengthSeconds(targetAnimator, deathStateName);
        }

        private float ResolveStageClearVictoryPresentationDurationSeconds(Animator targetAnimator)
        {
            var animationTiming = ResolveAnimationTiming();
            if (animationTiming.TryGetStageClearVictoryAnimatorDurationOverride(out var durationSeconds))
            {
                return durationSeconds;
            }

            return ResolveStateReferenceClipLengthSeconds(targetAnimator, stageClearVictoryStateName);
        }

        private float ResolveActionPresentationDurationSeconds(
            Animator targetAnimator,
            PlayerActionKind actionKind,
            float resolvedMotionDurationSeconds)
        {
            var animationTiming = ResolveAnimationTiming();
            if (animationTiming.TryGetLegacyAnimatorDurationOverride(actionKind, out var legacyDurationSeconds))
            {
                return legacyDurationSeconds;
            }

            var windupPhase = ResolveWindupPhase(actionKind);
            var recoveryPhase = ResolveRecoveryPhase(actionKind);
            var hasWindupOverride = animationTiming.TryGetAnimatorDurationOverride(windupPhase, out var windupDurationSeconds);
            var hasRecoveryOverride = animationTiming.TryGetAnimatorDurationOverride(recoveryPhase, out var recoveryDurationSeconds);
            if (resolvedMotionDurationSeconds > 0f)
            {
                if (hasWindupOverride)
                {
                    return windupDurationSeconds;
                }

                if (hasRecoveryOverride)
                {
                    return recoveryDurationSeconds;
                }

                return resolvedMotionDurationSeconds;
            }

            if (hasWindupOverride || hasRecoveryOverride)
            {
                return (hasWindupOverride ? windupDurationSeconds : ResolvePhaseReferenceClipLengthSeconds(targetAnimator, windupPhase)) +
                       (hasRecoveryOverride ? recoveryDurationSeconds : ResolvePhaseReferenceClipLengthSeconds(targetAnimator, recoveryPhase));
            }

            return ResolveActionReferenceClipLengthSeconds(targetAnimator, actionKind);
        }

        private float ResolveActionHoldPresentationDurationSeconds(
            Animator targetAnimator,
            PlayerActionKind actionKind,
            float resolvedMotionDurationSeconds)
        {
            if (actionKind != PlayerActionKind.Push &&
                actionKind != PlayerActionKind.Flip)
            {
                return 0f;
            }

            var animationTiming = ResolveAnimationTiming();
            if (animationTiming.TryGetLegacyAnimatorDurationOverride(actionKind, out var legacyDurationSeconds))
            {
                return legacyDurationSeconds;
            }

            var windupPhase = ResolveWindupPhase(actionKind);
            var recoveryPhase = ResolveRecoveryPhase(actionKind);
            var hasWindupOverride = animationTiming.TryGetAnimatorDurationOverride(windupPhase, out _);
            var hasRecoveryOverride = animationTiming.TryGetAnimatorDurationOverride(recoveryPhase, out _);
            if (hasWindupOverride || hasRecoveryOverride)
            {
                return ResolvePhasePresentationDurationSeconds(targetAnimator, windupPhase, resolvedMotionDurationSeconds) +
                       ResolvePhasePresentationDurationSeconds(targetAnimator, recoveryPhase, resolvedMotionDurationSeconds);
            }

            return ResolveActionPresentationDurationSeconds(
                targetAnimator,
                actionKind,
                resolvedMotionDurationSeconds);
        }

        private float ResolvePhasePresentationDurationSeconds(
            Animator targetAnimator,
            PlayerPresentationPhase phase,
            float resolvedMotionDurationSeconds)
        {
            var animationTiming = ResolveAnimationTiming();
            if (animationTiming.TryGetAnimatorDurationOverride(phase, out var phaseDurationSeconds))
            {
                return phaseDurationSeconds;
            }

            var actionKind = ResolveActionKind(phase);
            if (actionKind == PlayerActionKind.None)
            {
                return 0f;
            }

            if (animationTiming.TryGetLegacyAnimatorDurationOverride(actionKind, out var legacyDurationSeconds))
            {
                return ResolveProportionalPhaseDurationSeconds(targetAnimator, phase, legacyDurationSeconds);
            }

            if (resolvedMotionDurationSeconds > 0f)
            {
                return ResolveProportionalPhaseDurationSeconds(targetAnimator, phase, resolvedMotionDurationSeconds);
            }

            return ResolvePhaseReferenceClipLengthSeconds(targetAnimator, phase);
        }

        private float ResolveProportionalPhaseDurationSeconds(
            Animator targetAnimator,
            PlayerPresentationPhase phase,
            float totalDurationSeconds)
        {
            var actionKind = ResolveActionKind(phase);
            var phaseReferenceLengthSeconds = ResolvePhaseReferenceClipLengthSeconds(targetAnimator, phase);
            var totalReferenceLengthSeconds = ResolveActionReferenceClipLengthSeconds(targetAnimator, actionKind);
            if (phaseReferenceLengthSeconds > 0f &&
                totalReferenceLengthSeconds > 0f &&
                targetAnimator?.runtimeAnimatorController != null)
            {
                return totalDurationSeconds * (phaseReferenceLengthSeconds / totalReferenceLengthSeconds);
            }

            return totalDurationSeconds;
        }

        private float ResolveActionReferenceClipLengthSeconds(
            Animator targetAnimator,
            PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => ResolvePhaseReferenceClipLengthSeconds(targetAnimator, PlayerPresentationPhase.PushWindup) +
                                         ResolvePhaseReferenceClipLengthSeconds(targetAnimator, PlayerPresentationPhase.PushRecovery),
                PlayerActionKind.Flip => ResolvePhaseReferenceClipLengthSeconds(targetAnimator, PlayerPresentationPhase.FlipWindup) +
                                         ResolvePhaseReferenceClipLengthSeconds(targetAnimator, PlayerPresentationPhase.FlipRecovery),
                _ => 0f,
            };
        }

        private float ResolvePhaseReferenceClipLengthSeconds(
            Animator targetAnimator,
            PlayerPresentationPhase phase)
        {
            var stateName = ResolvePhaseStateName(phase);
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return 1f;
            }

            var resolvedLength = ResolveStateReferenceClipLengthSeconds(targetAnimator, stateName);
            return resolvedLength > 0f ? resolvedLength : 1f;
        }

        private float ResolveStateReferenceClipLengthSeconds(
            Animator targetAnimator,
            string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return 0f;
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

            var resolvedLength = ResolveClipLengthSeconds(controller, stateName);
            _clipLengthCache[stateName] = resolvedLength;
            return resolvedLength;
        }

        private static float ResolveClipLengthSeconds(RuntimeAnimatorController controller, string clipName)
        {
            if (controller == null ||
                string.IsNullOrWhiteSpace(clipName))
            {
                return 0f;
            }

            var clips = controller.animationClips;
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip != null &&
                    string.Equals(clip.name, clipName, StringComparison.Ordinal))
                {
                    return Mathf.Max(clip.length, 0.01f);
                }
            }

            return 0f;
        }

        private PlayerPresentationPhase ResolveTargetPresentationPhase(
            PlayerViewAnimationState resolvedState,
            bool restart,
            PlayerActionKind executeActionKind,
            PlayerViewAnimationState previousState,
            PlayerPresentationPhase previousPhase)
        {
            if (resolvedState == PlayerViewAnimationState.Death)
            {
                return PlayerPresentationPhase.None;
            }

            if (resolvedState == PlayerViewAnimationState.StageClearVictory)
            {
                return PlayerPresentationPhase.None;
            }

            if (executeActionKind == PlayerActionKind.Push ||
                executeActionKind == PlayerActionKind.Flip)
            {
                return ResolveRecoveryPhase(executeActionKind);
            }

            if (TryResolveActionKind(resolvedState, out var activeActionKind))
            {
                if (restart)
                {
                    return ResolveWindupPhase(activeActionKind);
                }

                if (LastPresentationState.ActiveActionKind == activeActionKind &&
                    LastPresentationState.IsRecoveryPhase)
                {
                    return ResolveRecoveryPhase(activeActionKind);
                }

                if (previousState == resolvedState &&
                    ResolveActionKind(previousPhase) == activeActionKind)
                {
                    return previousPhase;
                }

                return ResolveWindupPhase(activeActionKind);
            }

            return PlayerPresentationPhase.None;
        }
    }
}
