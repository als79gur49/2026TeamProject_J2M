using System;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Flags]
    public enum EnemyPresentationLegacyOneShotSuppression
    {
        None = 0,
        JumpWindup = 1 << 0,
        JumpAirborneStartOrRetry = 1 << 1,
        JumpLand = 1 << 2,
        ChargeWindup = 1 << 3,
        ChargeActiveStart = 1 << 4,
        ChargeRecover = 1 << 5,
        DeathTrigger = 1 << 6,
    }

    public sealed class EnemyAnimatorDriver : MonoBehaviour
    {
        private const string DefaultLocomotionStateName = "Move";
        private const string AiModeParameterName = "EnemyAiMode";
        private const string ActiveActionKindParameterName = "EnemyActionKind";
        private const string JumpPhaseParameterName = "EnemyJumpPhase";
        private const string ChargePhaseParameterName = "EnemyChargePhase";
        private const string MovingParameterName = "IsMoving";
        private static readonly int AiModeParameterHash = Animator.StringToHash(AiModeParameterName);
        private static readonly int ActiveActionKindParameterHash = Animator.StringToHash(ActiveActionKindParameterName);
        private static readonly int JumpPhaseParameterHash = Animator.StringToHash(JumpPhaseParameterName);
        private static readonly int ChargePhaseParameterHash = Animator.StringToHash(ChargePhaseParameterName);
        private static readonly int MovingParameterHash = Animator.StringToHash(MovingParameterName);

        [SerializeField] private Animator animator;
        [SerializeField] private EnemyAnimationTimingAuthoring animationTimingAuthoring;
        [SerializeField] private string windupStateName = "Windup";
        [SerializeField] private string jumpWindupStateName = "JumpWindup";
        [SerializeField] private string jumpAirborneStateName = "JumpAirborne";
        [SerializeField] private string chargeActiveStateName = "Charge";
        [SerializeField] private string recoveryStateName = "Recover";
        [SerializeField] private string glideWindupStateName = "Fly_Start";
        [SerializeField] private string glideActiveStateName = "Fly_Loop";
        [SerializeField] private string glideRecoveryStateName = "Fly_Done";
        [SerializeField] private string windupTriggerName = "Windup";
        [SerializeField] private string jumpWindupTriggerName = "JumpWindup";
        [SerializeField] private string jumpAirborneTriggerName = "JumpAirborne";
        [SerializeField] private string attackTriggerName = "Attack";
        [SerializeField] private string recoveryTriggerName = "Recover";
        [SerializeField] private string hitTriggerName = "Hit";
        [SerializeField] private string deathTriggerName = "Death";

        public EnemyViewPresentationState LastPresentationState { get; private set; }

        public EnemyAiMode CurrentAiMode { get; private set; }

        public EnemyActionKind CurrentActiveActionKind { get; private set; }

        public bool IsMoving { get; private set; }

        public bool IsVisible { get; private set; }

        public bool IsPlaybackSuppressed { get; private set; }

        public int WindupSignalCount { get; private set; }

        public int AttackSignalCount { get; private set; }

        public int RecoverySignalCount { get; private set; }

        public int JumpWindupSignalCount { get; private set; }

        public int JumpAirborneSignalCount { get; private set; }

        public int JumpAirborneRestoreCount { get; private set; }

        public int ChargeActiveSignalCount { get; private set; }

        public int GlideWindupSignalCount { get; private set; }

        public int GlideActiveSignalCount { get; private set; }

        public int GlideRecoverySignalCount { get; private set; }

        public int UtilityWindupSignalCount { get; private set; }

        public int HitSignalCount { get; private set; }

        public int DeathSignalCount { get; private set; }

        public float DeathPresentationDurationSeconds => ResolveDeathPresentationDurationSeconds();

        public float GetPresentationDurationSeconds(EnemyPresentationPhase phase)
        {
            ResolveAnimatorSpeed(phase, out var presentationDurationSeconds);
            return presentationDurationSeconds;
        }

        public float CurrentAnimatorSpeed { get; private set; } = 1f;

        public float CurrentPresentationDurationSeconds { get; private set; }

        public float LastCrossFadeDurationSeconds { get; private set; }

        public string LastCrossFadedStateName { get; private set; } = string.Empty;

        public bool IsPresentationPaused { get; private set; }

        private bool _animationTimingResolved;
        private bool _hasAnimationTimingAuthoring;
        private EnemyAnimationTimingSnapshot _animationTiming;
        private Animator _validatedOptionalParameterAnimator;
        private RuntimeAnimatorController _validatedOptionalParameterController;
        private bool _supportsAiModeParameter;
        private bool _supportsActiveActionKindParameter;
        private bool _supportsJumpPhaseParameter;
        private bool _supportsChargePhaseParameter;
        private bool _supportsMovingParameter;
        private int _windupTriggerDispatchCount;
        private int _recoveryTriggerDispatchCount;
        private AnimatorStateSnapshot _jumpAirborneTopologySuspendSnapshot;
        private float _lastJumpAirborneNormalizedTime;
        private string _pendingCrossFadeStateName = string.Empty;
        private bool _pendingCrossFadeRequiresOverride;

        public bool HasJumpAirborneTopologySuspendSnapshot => _jumpAirborneTopologySuspendSnapshot.HasValue;

        internal bool CanDriveCurrentAnimator => CanDriveAnimator(ResolveAnimator());

        public int DebugLastJumpAirborneStateShortNameHash => Animator.StringToHash(jumpAirborneStateName);

        public float DebugLastJumpAirborneNormalizedTime =>
            HasJumpAirborneTopologySuspendSnapshot
                ? _jumpAirborneTopologySuspendSnapshot.NormalizedTime
                : _lastJumpAirborneNormalizedTime;

        public void SetPresentationPaused(bool paused)
        {
            IsPresentationPaused = paused;
        }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            animationTimingAuthoring = GetComponent<EnemyAnimationTimingAuthoring>();
        }

        public void Apply(in EnemyViewPresentationState state)
        {
            Apply(state, EnemyPresentationLegacyOneShotSuppression.None);
        }

        public void Apply(
            in EnemyViewPresentationState state,
            EnemyPresentationLegacyOneShotSuppression oneShotSuppression)
        {
            var previousState = LastPresentationState;
            LastPresentationState = state;
            CurrentAiMode = state.AiMode;
            CurrentActiveActionKind = state.ActiveActionKind;
            IsMoving = state.IsMoving;

            if (IsPresentationPaused)
            {
                return;
            }

            var targetAnimator = ResolveAnimator();
            TryConsumePendingNamedStateCrossFade(targetAnimator);
            SyncOptionalParameters(targetAnimator, state);

            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(state));
            if (state.JumpPhase != EnemyJumpPhase.Airborne)
            {
                _jumpAirborneTopologySuspendSnapshot = default;
                _lastJumpAirborneNormalizedTime = 0f;
            }

            if (state.StartedJumpWindupThisTick &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationLegacyOneShotSuppression.JumpWindup))
            {
                JumpWindupSignalCount++;
                if (!TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.JumpWindup))
                {
                    SetTrigger(targetAnimator, jumpWindupTriggerName);
                }
            }

            if (state.StartedJumpAirborneThisTick &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationLegacyOneShotSuppression.JumpAirborneStartOrRetry))
            {
                _jumpAirborneTopologySuspendSnapshot = default;
                JumpAirborneSignalCount++;
                if (!TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.JumpAirborne))
                {
                    SetTrigger(targetAnimator, jumpAirborneTriggerName);
                }
            }

            if (state.LandedFromJumpThisTick &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationLegacyOneShotSuppression.JumpLand))
            {
                TryApplyNamedStateCrossFade(targetAnimator, DefaultLocomotionStateName);
            }

            var suppressChargeActiveStart =
                state.StartedChargeActiveThisTick &&
                IsSuppressed(oneShotSuppression, EnemyPresentationLegacyOneShotSuppression.ChargeActiveStart);
            if (!suppressChargeActiveStart &&
                (state.StartedChargeActiveThisTick ||
                 (state.ChargePhase == EnemyChargePhase.Active && previousState.ChargePhase != EnemyChargePhase.Active)))
            {
                ChargeActiveSignalCount++;
                TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.ChargeActive);
            }

            var handledGlideWindup = false;
            if (state.StartedGlideWindupThisTick)
            {
                GlideWindupSignalCount++;
                handledGlideWindup = TryApplyNamedStateCrossFade(
                    targetAnimator,
                    glideWindupStateName,
                    requireOverride: true);
            }

            if (state.StartedGlideActiveThisTick ||
                (state.GlidePhase == EnemyGlidePhase.Active && previousState.GlidePhase != EnemyGlidePhase.Active))
            {
                GlideActiveSignalCount++;
                TryApplyNamedStateCrossFade(
                    targetAnimator,
                    glideActiveStateName,
                    requireOverride: true);
            }

            if (state.StartedWindupThisTick &&
                !(state.StartedChargeWindupThisTick &&
                  IsSuppressed(oneShotSuppression, EnemyPresentationLegacyOneShotSuppression.ChargeWindup)))
            {
                WindupSignalCount++;
                if (!handledGlideWindup &&
                    !TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.Windup))
                {
                    DispatchWindupTrigger(targetAnimator);
                }
            }

            if (state.StartedUtilityWindupThisTick)
            {
                PlayUtilityWindup(state.UtilityPresentationKind);
            }

            if (state.ExecutedThisTick)
            {
                AttackSignalCount++;
                SetTrigger(targetAnimator, attackTriggerName);
            }

            var handledGlideRecovery = false;
            if (state.StartedGlideRecoverThisTick)
            {
                GlideRecoverySignalCount++;
                handledGlideRecovery = TryApplyNamedStateCrossFade(
                    targetAnimator,
                    glideRecoveryStateName,
                    requireOverride: true);
            }

            if (state.StartedRecoveryThisTick &&
                !(state.StartedChargeRecoverThisTick &&
                  IsSuppressed(oneShotSuppression, EnemyPresentationLegacyOneShotSuppression.ChargeRecover)))
            {
                RecoverySignalCount++;
                if (!handledGlideRecovery &&
                    !TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.Recovery))
                {
                    DispatchRecoveryTrigger(targetAnimator);
                }
            }

            if (state.TookDamage)
            {
                HitSignalCount++;
                SetTrigger(targetAnimator, hitTriggerName);
            }

            if (state.DidDie &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationLegacyOneShotSuppression.DeathTrigger))
            {
                DeathSignalCount++;
                SetTrigger(targetAnimator, deathTriggerName);
            }

            if (state.JumpPhase == EnemyJumpPhase.Airborne &&
                !state.LandedFromJumpThisTick &&
                !state.DidDie)
            {
                EnsureJumpAirborneAnimatorState(targetAnimator);
            }
        }

        private static bool IsSuppressed(
            EnemyPresentationLegacyOneShotSuppression suppression,
            EnemyPresentationLegacyOneShotSuppression value)
        {
            return (suppression & value) == value;
        }

        public void CompleteJumpLandingPresentation()
        {
            var settledState = LastPresentationState.WithJumpLandingCompletionSettled();
            LastPresentationState = settledState;
            CurrentAiMode = settledState.AiMode;
            CurrentActiveActionKind = settledState.ActiveActionKind;
            IsMoving = settledState.IsMoving;

            if (IsPresentationPaused)
            {
                return;
            }

            var targetAnimator = ResolveAnimator();
            TryConsumePendingNamedStateCrossFade(targetAnimator);
            SyncOptionalParameters(targetAnimator, settledState);
            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(settledState));
            TryApplyNamedStateCrossFade(targetAnimator, DefaultLocomotionStateName);
            SyncRuntimeState(IsVisible, IsMoving, playbackSuppressed: false);
        }

        public void SyncRuntimeState(bool isVisible, bool isMoving, bool playbackSuppressed = false)
        {
            var isJumpAirborne = LastPresentationState.JumpPhase == EnemyJumpPhase.Airborne;
            var effectivePlaybackSuppressed = playbackSuppressed || (isJumpAirborne && !isVisible);
            IsVisible = isVisible;
            IsMoving = isMoving;
            IsPlaybackSuppressed = effectivePlaybackSuppressed;

            if (IsPresentationPaused)
            {
                return;
            }

            var targetAnimator = ResolveAnimator();
            TryConsumePendingNamedStateCrossFade(targetAnimator);
            if (isJumpAirborne)
            {
                if (effectivePlaybackSuppressed)
                {
                    PreserveJumpAirborneAnimatorForTopologySuspend(targetAnimator);
                }
                else
                {
                    EnsureJumpAirborneAnimatorState(targetAnimator);
                }
            }

            SyncOptionalMovingParameter(targetAnimator, isMoving);

            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(LastPresentationState));
            if (isJumpAirborne &&
                isVisible &&
                !effectivePlaybackSuppressed)
            {
                EnsureJumpAirborneAnimatorState(targetAnimator);
            }
        }

        public void SyncHiddenRuntimeState(bool isMoving, bool playbackSuppressed = false)
        {
            var isJumpAirborne = LastPresentationState.JumpPhase == EnemyJumpPhase.Airborne;
            IsVisible = false;
            IsMoving = isMoving;
            IsPlaybackSuppressed = playbackSuppressed || isJumpAirborne;

            if (IsPresentationPaused)
            {
                return;
            }

            var targetAnimator = ResolveAnimator();
            if (isJumpAirborne)
            {
                PreserveJumpAirborneAnimatorForTopologySuspend(targetAnimator);
            }

            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(LastPresentationState), driveAnimator: false);
        }

        public void ApplyPresentationPhaseTiming(EnemyPresentationPhase phase)
        {
            if (IsPresentationPaused)
            {
                return;
            }

            ApplyAnimatorTiming(ResolveAnimator(), phase);
        }

        public void RestorePresentationTiming()
        {
            if (IsPresentationPaused)
            {
                return;
            }

            ApplyAnimatorTiming(ResolveAnimator(), ResolvePresentationPhase(LastPresentationState));
        }

        public bool ResyncAnimatorStateFromLastPresentation()
        {
            if (LastPresentationState.EntityId == 0)
            {
                return false;
            }

            if (IsPresentationPaused)
            {
                return false;
            }

            var targetAnimator = ResolveAnimator();
            TryConsumePendingNamedStateCrossFade(targetAnimator);
            SyncOptionalParameters(targetAnimator, LastPresentationState);
            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(LastPresentationState));

            if (LastPresentationState.JumpPhase == EnemyJumpPhase.Airborne &&
                EnsureJumpAirborneAnimatorState(targetAnimator))
            {
                return true;
            }

            switch (LastPresentationState.GlidePhase)
            {
                case EnemyGlidePhase.Windup:
                    return TryApplyNamedStateCrossFade(targetAnimator, glideWindupStateName, requireOverride: true);

                case EnemyGlidePhase.Active:
                    return TryApplyNamedStateCrossFade(targetAnimator, glideActiveStateName, requireOverride: true);

                case EnemyGlidePhase.Recovery:
                    return TryApplyNamedStateCrossFade(targetAnimator, glideRecoveryStateName, requireOverride: true);
            }

            var phase = ResolvePresentationPhase(LastPresentationState);
            switch (phase)
            {
                case EnemyPresentationPhase.Windup:
                case EnemyPresentationPhase.Recovery:
                case EnemyPresentationPhase.JumpWindup:
                case EnemyPresentationPhase.JumpAirborne:
                case EnemyPresentationPhase.ChargeActive:
                    return TryApplyPresentationCrossFade(targetAnimator, phase);

                default:
                    return false;
            }
        }

        public void PlayUtilityWindup(EnemyUtilityPresentationKind kind)
        {
            if (kind != EnemyUtilityPresentationKind.GravityFieldAura)
            {
                return;
            }

            if (IsPresentationPaused)
            {
                return;
            }

            var targetAnimator = ResolveAnimator();
            UtilityWindupSignalCount++;
            if (!TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.Windup))
            {
                DispatchWindupTrigger(targetAnimator);
            }
        }

        public float PlayDeathPresentation(int entityId)
        {
            var targetAnimator = ResolveAnimator();
            var currentState = LastPresentationState.EntityId == 0
                ? new EnemyViewPresentationState(
                    entityId,
                    tickIndex: -1,
                    EnemyAiMode.Dead,
                    EnemyActionKind.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: true)
                : LastPresentationState.WithDidDie(true);
            LastPresentationState = currentState;
            CurrentAiMode = currentState.AiMode;
            CurrentActiveActionKind = currentState.ActiveActionKind;
            IsMoving = false;
            if (IsPresentationPaused)
            {
                return DeathPresentationDurationSeconds;
            }

            DeathSignalCount++;
            SetTrigger(targetAnimator, deathTriggerName);
            ApplyAnimatorTiming(targetAnimator, EnemyPresentationPhase.Death);
            return DeathPresentationDurationSeconds;
        }

        private Animator ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            return animator;
        }

        private bool TryResolveAnimationTiming(out EnemyAnimationTimingSnapshot animationTiming)
        {
            if (_animationTimingResolved)
            {
                animationTiming = _animationTiming;
                return _hasAnimationTimingAuthoring;
            }

            if (animationTimingAuthoring == null)
            {
                animationTimingAuthoring = GetComponent<EnemyAnimationTimingAuthoring>();
            }

            if (animationTimingAuthoring == null)
            {
                _animationTimingResolved = true;
                _hasAnimationTimingAuthoring = false;
                _animationTiming = default;
                animationTiming = default;
                return false;
            }

            _animationTiming = animationTimingAuthoring.CreateSnapshot();
            _animationTimingResolved = true;
            _hasAnimationTimingAuthoring = true;
            animationTiming = _animationTiming;
            return true;
        }

        private void ApplyAnimatorTiming(
            Animator targetAnimator,
            EnemyPresentationPhase phase,
            bool driveAnimator = true)
        {
            var resolvedSpeed = ResolveAnimatorSpeed(phase, out var presentationDurationSeconds);
            CurrentAnimatorSpeed = resolvedSpeed;
            CurrentPresentationDurationSeconds = presentationDurationSeconds;
            if (IsPresentationPaused)
            {
                return;
            }

            var targetSpeed = IsPlaybackSuppressed
                ? 0f
                : resolvedSpeed;

            if (driveAnimator && CanSetAnimatorSpeed(targetAnimator))
            {
                targetAnimator.speed = targetSpeed;
            }
        }

        private bool TryApplyPresentationCrossFade(Animator targetAnimator, EnemyPresentationPhase phase)
        {
            var stateName = ResolveStateName(phase);
            return TryApplyNamedStateCrossFade(targetAnimator, stateName, requireOverride: true);
        }

        private float ResolveAnimatorSpeed(
            EnemyPresentationPhase phase,
            out float presentationDurationSeconds)
        {
            if (phase == EnemyPresentationPhase.None)
            {
                presentationDurationSeconds = 0f;
                return 1f;
            }

            var hasReferenceClipLength = TryResolveReferenceClipLengthSeconds(
                phase,
                out var referenceClipLengthSeconds);
            if (!TryResolveAnimatorDurationOverride(phase, out var overrideDurationSeconds))
            {
                presentationDurationSeconds = hasReferenceClipLength
                    ? referenceClipLengthSeconds
                    : 0f;
                return 1f;
            }

            if (!hasReferenceClipLength)
            {
                throw new InvalidOperationException(
                    $"Missing reference clip length for {nameof(EnemyPresentationPhase)}.{phase}.");
            }

            presentationDurationSeconds = overrideDurationSeconds;
            return Mathf.Max(0.01f, referenceClipLengthSeconds / overrideDurationSeconds);
        }

        private bool TryResolveAnimatorDurationOverride(
            EnemyPresentationPhase phase,
            out float durationSeconds)
        {
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                return false;
            }

            switch (phase)
            {
                case EnemyPresentationPhase.JumpWindup:
                    return animationTiming.TryGetJumpWindupAnimatorDurationOverride(out durationSeconds);

                case EnemyPresentationPhase.JumpAirborne:
                    return animationTiming.TryGetJumpAirborneAnimatorDurationOverride(out durationSeconds);

                case EnemyPresentationPhase.Windup:
                    return animationTiming.TryGetAttackWindupAnimatorDurationOverride(out durationSeconds);

                case EnemyPresentationPhase.ChargeActive:
                    durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                    return false;

                case EnemyPresentationPhase.Recovery:
                    return animationTiming.TryGetRecoverAnimatorDurationOverride(out durationSeconds);

                case EnemyPresentationPhase.Death:
                    return animationTiming.TryGetDeathAnimatorDurationOverride(out durationSeconds);

                default:
                    durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                    return false;
            }
        }

        private bool TryResolveStateTransitionCrossFadeDurationOverride(out float durationSeconds)
        {
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                return false;
            }

            return animationTiming.TryGetStateTransitionCrossFadeDurationOverride(out durationSeconds);
        }

        private bool TryResolveReferenceClipLengthSeconds(
            EnemyPresentationPhase phase,
            out float referenceClipLengthSeconds)
        {
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                referenceClipLengthSeconds = 0f;
                return false;
            }

            switch (phase)
            {
                case EnemyPresentationPhase.JumpWindup:
                    return animationTiming.TryGetJumpWindupReferenceClipLengthSeconds(
                        out referenceClipLengthSeconds);

                case EnemyPresentationPhase.JumpAirborne:
                    return animationTiming.TryGetJumpAirborneReferenceClipLengthSeconds(
                        out referenceClipLengthSeconds);

                case EnemyPresentationPhase.Windup:
                    return animationTiming.TryGetAttackWindupReferenceClipLengthSeconds(
                        out referenceClipLengthSeconds);

                case EnemyPresentationPhase.ChargeActive:
                    referenceClipLengthSeconds = 0f;
                    return false;

                case EnemyPresentationPhase.Recovery:
                    return animationTiming.TryGetRecoverReferenceClipLengthSeconds(
                        out referenceClipLengthSeconds);

                case EnemyPresentationPhase.Death:
                    return animationTiming.TryGetDeathReferenceClipLengthSeconds(
                        out referenceClipLengthSeconds);

                default:
                    referenceClipLengthSeconds = 0f;
                    return false;
            }
        }

        private string ResolveStateName(EnemyPresentationPhase phase)
        {
            switch (phase)
            {
                case EnemyPresentationPhase.JumpWindup:
                    return jumpWindupStateName;

                case EnemyPresentationPhase.JumpAirborne:
                    return jumpAirborneStateName;

                case EnemyPresentationPhase.Windup:
                    return windupStateName;

                case EnemyPresentationPhase.ChargeActive:
                    return chargeActiveStateName;

                case EnemyPresentationPhase.Recovery:
                    return recoveryStateName;

                default:
                    return string.Empty;
            }
        }

        private float ResolveDeathPresentationDurationSeconds()
        {
            ResolveAnimatorSpeed(EnemyPresentationPhase.Death, out var presentationDurationSeconds);
            return presentationDurationSeconds;
        }

        private static EnemyPresentationPhase ResolvePresentationPhase(in EnemyViewPresentationState state)
        {
            if (state.DidDie)
            {
                return EnemyPresentationPhase.Death;
            }

            switch (state.JumpPhase)
            {
                case EnemyJumpPhase.Windup:
                    return EnemyPresentationPhase.JumpWindup;

                case EnemyJumpPhase.Airborne:
                    return EnemyPresentationPhase.JumpAirborne;
            }

            switch (state.ChargePhase)
            {
                case EnemyChargePhase.Windup:
                    return EnemyPresentationPhase.Windup;

                case EnemyChargePhase.Active:
                    return EnemyPresentationPhase.ChargeActive;

                case EnemyChargePhase.Recover:
                    return EnemyPresentationPhase.Recovery;
            }

            switch (state.GlidePhase)
            {
                case EnemyGlidePhase.Windup:
                    return EnemyPresentationPhase.Windup;

                case EnemyGlidePhase.Recovery:
                    return EnemyPresentationPhase.Recovery;
            }

            switch (state.AiMode)
            {
                case EnemyAiMode.Attack:
                    return EnemyPresentationPhase.Windup;

                case EnemyAiMode.Recover:
                    return EnemyPresentationPhase.Recovery;

                default:
                    return EnemyPresentationPhase.None;
            }
        }

        private void SyncOptionalParameters(Animator targetAnimator, in EnemyViewPresentationState state)
        {
            if (!CanDriveAnimator(targetAnimator))
            {
                return;
            }

            if (SupportsAiModeParameter(targetAnimator))
            {
                targetAnimator.SetInteger(AiModeParameterHash, (int)state.AiMode);
            }

            if (SupportsActiveActionKindParameter(targetAnimator))
            {
                targetAnimator.SetInteger(ActiveActionKindParameterHash, (int)state.ActiveActionKind);
            }

            if (SupportsJumpPhaseParameter(targetAnimator))
            {
                targetAnimator.SetInteger(JumpPhaseParameterHash, ResolveAnimatorJumpPhase(state.JumpPhase));
            }

            if (SupportsChargePhaseParameter(targetAnimator))
            {
                targetAnimator.SetInteger(ChargePhaseParameterHash, (int)state.ChargePhase);
            }

            SyncOptionalMovingParameter(targetAnimator, state.IsMoving);
        }

        private void SyncOptionalMovingParameter(Animator targetAnimator, bool isMoving)
        {
            if (!CanDriveAnimator(targetAnimator))
            {
                return;
            }

            if (!SupportsMovingParameter(targetAnimator))
            {
                return;
            }

            targetAnimator.SetBool(MovingParameterHash, isMoving);
        }

        private bool SupportsAiModeParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsAiModeParameter;
        }

        private bool SupportsActiveActionKindParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsActiveActionKindParameter;
        }

        private bool SupportsMovingParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsMovingParameter;
        }

        private bool SupportsJumpPhaseParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsJumpPhaseParameter;
        }

        private bool SupportsChargePhaseParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsChargePhaseParameter;
        }

        private void RefreshOptionalParameterSupport(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return;
            }

            var controller = targetAnimator.runtimeAnimatorController;
            if (_validatedOptionalParameterAnimator == targetAnimator &&
                _validatedOptionalParameterController == controller)
            {
                return;
            }

            _validatedOptionalParameterAnimator = targetAnimator;
            _validatedOptionalParameterController = controller;
            _supportsAiModeParameter = false;
            _supportsActiveActionKindParameter = false;
            _supportsJumpPhaseParameter = false;
            _supportsChargePhaseParameter = false;
            _supportsMovingParameter = false;

             if (controller == null)
             {
                 return;
             }

            var parameters = targetAnimator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Int)
                {
                    if (parameter.nameHash == AiModeParameterHash)
                    {
                        _supportsAiModeParameter = true;
                    }
                    else if (parameter.nameHash == ActiveActionKindParameterHash)
                    {
                        _supportsActiveActionKindParameter = true;
                    }
                    else if (parameter.nameHash == JumpPhaseParameterHash)
                    {
                        _supportsJumpPhaseParameter = true;
                    }
                    else if (parameter.nameHash == ChargePhaseParameterHash)
                    {
                        _supportsChargePhaseParameter = true;
                    }
                }
                else if (parameter.type == AnimatorControllerParameterType.Bool &&
                         parameter.nameHash == MovingParameterHash)
                {
                    _supportsMovingParameter = true;
                }
            }
        }

        private static int ResolveAnimatorJumpPhase(EnemyJumpPhase jumpPhase)
        {
            return jumpPhase switch
            {
                EnemyJumpPhase.Windup => 1,
                EnemyJumpPhase.Airborne => 2,
                _ => 0,
            };
        }

        private void DispatchWindupTrigger(Animator targetAnimator)
        {
            if (SetTrigger(targetAnimator, windupTriggerName))
            {
                _windupTriggerDispatchCount++;
            }
        }

        private void DispatchRecoveryTrigger(Animator targetAnimator)
        {
            if (SetTrigger(targetAnimator, recoveryTriggerName))
            {
                _recoveryTriggerDispatchCount++;
            }
        }

        private static bool SetTrigger(Animator targetAnimator, string parameterName)
        {
            if (!CanDriveAnimator(targetAnimator) ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            targetAnimator.SetTrigger(Animator.StringToHash(parameterName));
            return true;
        }

        public bool PreserveJumpAirborneAnimatorForTopologySuspend()
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            return PreserveJumpAirborneAnimatorForTopologySuspend(ResolveAnimator());
        }

        public bool EnsureJumpAirborneBaseAnimation()
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            return EnsureJumpAirborneAnimatorState(ResolveAnimator());
        }

        private bool PreserveJumpAirborneAnimatorForTopologySuspend(Animator targetAnimator)
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            if (LastPresentationState.JumpPhase != EnemyJumpPhase.Airborne)
            {
                return false;
            }

            var fallbackStateHash = targetAnimator != null
                ? ResolveAnimatorStateHash(targetAnimator, jumpAirborneStateName)
                : Animator.StringToHash(jumpAirborneStateName);
            if (!CanDriveAnimator(targetAnimator))
            {
                _jumpAirborneTopologySuspendSnapshot = new AnimatorStateSnapshot(
                    fallbackStateHash,
                    Mathf.Max(0f, _lastJumpAirborneNormalizedTime));
                return true;
            }

            var stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
            if (!IsJumpAirborneAnimatorState(stateInfo))
            {
                var ensuredStateHash = ResolveAnimatorStateHash(targetAnimator, jumpAirborneStateName);
                PlayAnimatorState(targetAnimator, ensuredStateHash, Mathf.Max(0f, _lastJumpAirborneNormalizedTime));
                ApplyAnimatorTiming(targetAnimator, EnemyPresentationPhase.JumpAirborne);
                LastCrossFadedStateName = jumpAirborneStateName;
                stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
            }

            var stateHash = stateInfo.shortNameHash != 0 ? stateInfo.shortNameHash : fallbackStateHash;
            if (stateHash != fallbackStateHash &&
                !stateInfo.IsName(jumpAirborneStateName) &&
                !stateInfo.IsName($"Base Layer.{jumpAirborneStateName}") &&
                !stateInfo.IsName($"Base Layer.Locomotion.{jumpAirborneStateName}"))
            {
                stateHash = fallbackStateHash;
            }

            _jumpAirborneTopologySuspendSnapshot = new AnimatorStateSnapshot(
                stateHash,
                NormalizeAnimatorTime(stateInfo.normalizedTime));
            _lastJumpAirborneNormalizedTime = _jumpAirborneTopologySuspendSnapshot.NormalizedTime;
            return true;
        }

        public bool RestoreJumpAirborneAnimatorAfterTopologySuspend()
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            return RestoreJumpAirborneAnimatorAfterTopologySuspend(ResolveAnimator());
        }

        private bool RestoreJumpAirborneAnimatorAfterTopologySuspend(Animator targetAnimator)
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            if (LastPresentationState.JumpPhase != EnemyJumpPhase.Airborne ||
                !_jumpAirborneTopologySuspendSnapshot.HasValue)
            {
                return false;
            }

            if (!CanDriveAnimator(targetAnimator))
            {
                return false;
            }

            var snapshot = _jumpAirborneTopologySuspendSnapshot;
            _jumpAirborneTopologySuspendSnapshot = default;
            PlayAnimatorState(targetAnimator, snapshot.StateHash, snapshot.NormalizedTime);
            ApplyAnimatorTiming(targetAnimator, EnemyPresentationPhase.JumpAirborne);
            _lastJumpAirborneNormalizedTime = snapshot.NormalizedTime;
            LastCrossFadedStateName = jumpAirborneStateName;
            JumpAirborneRestoreCount++;
            return true;
        }

        private bool EnsureJumpAirborneAnimatorState(Animator targetAnimator)
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            if (LastPresentationState.JumpPhase != EnemyJumpPhase.Airborne)
            {
                return false;
            }

            if (RestoreJumpAirborneAnimatorAfterTopologySuspend(targetAnimator))
            {
                return true;
            }

            if (!CanDriveAnimator(targetAnimator))
            {
                return false;
            }

            var currentState = targetAnimator.GetCurrentAnimatorStateInfo(0);
            if (IsJumpAirborneAnimatorState(currentState))
            {
                _lastJumpAirborneNormalizedTime = NormalizeAnimatorTime(currentState.normalizedTime);
                ApplyAnimatorTiming(targetAnimator, EnemyPresentationPhase.JumpAirborne);
                return true;
            }

            var fallbackNormalizedTime = Mathf.Max(0f, _lastJumpAirborneNormalizedTime);
            var stateHash = ResolveAnimatorStateHash(targetAnimator, jumpAirborneStateName);
            PlayAnimatorState(targetAnimator, stateHash, fallbackNormalizedTime);
            ApplyAnimatorTiming(targetAnimator, EnemyPresentationPhase.JumpAirborne);
            LastCrossFadedStateName = jumpAirborneStateName;
            return true;
        }

        private bool IsJumpAirborneAnimatorState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.IsName(jumpAirborneStateName) ||
                   stateInfo.IsName($"Base Layer.{jumpAirborneStateName}") ||
                   stateInfo.IsName($"Base Layer.Locomotion.{jumpAirborneStateName}") ||
                   stateInfo.shortNameHash == Animator.StringToHash(jumpAirborneStateName);
        }

        private void PlayAnimatorState(Animator targetAnimator, int stateHash, float normalizedTime)
        {
            targetAnimator.Play(stateHash, 0, normalizedTime);
            targetAnimator.Update(0f);
            if (targetAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != 0)
            {
                return;
            }

            targetAnimator.Rebind();
            targetAnimator.Play(stateHash, 0, normalizedTime);
            targetAnimator.Update(0f);
            if (targetAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != 0)
            {
                return;
            }

            targetAnimator.Play($"Base Layer.{jumpAirborneStateName}", 0, normalizedTime);
            targetAnimator.Update(0f);
            if (targetAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != 0)
            {
                return;
            }

            targetAnimator.Play(jumpAirborneStateName, 0, normalizedTime);
            targetAnimator.Update(0f);
        }

        private static float NormalizeAnimatorTime(float normalizedTime)
        {
            if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
            {
                return 0f;
            }

            return Mathf.Max(0f, normalizedTime);
        }

        private bool TryApplyNamedStateCrossFade(
            Animator targetAnimator,
            string stateName,
            bool requireOverride = false)
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            var hasOverride = TryResolveStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds);
            if (requireOverride && !hasOverride)
            {
                return false;
            }

            var resolvedDurationSeconds = hasOverride
                ? Mathf.Max(0f, crossFadeDurationSeconds)
                : 0f;
            LastCrossFadeDurationSeconds = resolvedDurationSeconds;
            LastCrossFadedStateName = stateName;

            if (!CanDriveAnimator(targetAnimator))
            {
                _pendingCrossFadeStateName = stateName;
                _pendingCrossFadeRequiresOverride = requireOverride;
                return false;
            }

            var stateHash = ResolveAnimatorStateHash(targetAnimator, stateName);
            targetAnimator.CrossFadeInFixedTime(
                stateHash,
                resolvedDurationSeconds);
            ClearPendingNamedStateCrossFade(stateName);
            return true;
        }

        private bool TryConsumePendingNamedStateCrossFade(Animator targetAnimator)
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_pendingCrossFadeStateName) ||
                !CanDriveAnimator(targetAnimator))
            {
                return false;
            }

            var stateName = _pendingCrossFadeStateName;
            var requireOverride = _pendingCrossFadeRequiresOverride;
            var hasOverride = TryResolveStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds);
            if (requireOverride && !hasOverride)
            {
                return false;
            }

            var resolvedDurationSeconds = hasOverride
                ? Mathf.Max(0f, crossFadeDurationSeconds)
                : 0f;
            var stateHash = ResolveAnimatorStateHash(targetAnimator, stateName);
            targetAnimator.CrossFadeInFixedTime(stateHash, resolvedDurationSeconds);
            LastCrossFadeDurationSeconds = resolvedDurationSeconds;
            LastCrossFadedStateName = stateName;
            _pendingCrossFadeStateName = string.Empty;
            _pendingCrossFadeRequiresOverride = false;
            return true;
        }

        private void ClearPendingNamedStateCrossFade(string appliedStateName)
        {
            if (!string.Equals(_pendingCrossFadeStateName, appliedStateName, StringComparison.Ordinal))
            {
                return;
            }

            _pendingCrossFadeStateName = string.Empty;
            _pendingCrossFadeRequiresOverride = false;
        }

        private static bool CanDriveAnimator(Animator targetAnimator)
        {
            return CanSetAnimatorSpeed(targetAnimator) &&
                   targetAnimator.runtimeAnimatorController != null;
        }

        private static bool CanSetAnimatorSpeed(Animator targetAnimator)
        {
            return targetAnimator != null &&
                   targetAnimator.enabled &&
                   targetAnimator.isActiveAndEnabled &&
                   targetAnimator.gameObject.activeInHierarchy;
        }

        private static int ResolveAnimatorStateHash(Animator targetAnimator, string stateName)
        {
            var shortNameHash = Animator.StringToHash(stateName);
            if (targetAnimator.HasState(0, shortNameHash))
            {
                return shortNameHash;
            }

            var rootStateHash = Animator.StringToHash($"Base Layer.{stateName}");
            if (targetAnimator.HasState(0, rootStateHash))
            {
                return rootStateHash;
            }

            var locomotionStateHash = Animator.StringToHash($"Base Layer.Locomotion.{stateName}");
            return targetAnimator.HasState(0, locomotionStateHash)
                ? locomotionStateHash
                : shortNameHash;
        }

        public enum EnemyPresentationPhase
        {
            None = 0,
            Windup = 1,
            Recovery = 2,
            JumpWindup = 3,
            JumpAirborne = 4,
            ChargeActive = 5,
            Death = 6,
        }

        private readonly struct AnimatorStateSnapshot
        {
            public AnimatorStateSnapshot(int stateHash, float normalizedTime)
            {
                StateHash = stateHash;
                NormalizedTime = normalizedTime;
                HasValue = true;
            }

            public int StateHash { get; }

            public float NormalizedTime { get; }

            public bool HasValue { get; }
        }
    }
}
