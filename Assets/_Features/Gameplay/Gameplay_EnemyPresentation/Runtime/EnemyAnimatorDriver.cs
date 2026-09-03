using System;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Flags]
    public enum EnemyPresentationOneShotBlockMask
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

        [Obsolete(
            "Enemy animation no longer owns death presentation duration. This compatibility value is always zero.",
            false)]
        public float DeathPresentationDurationSeconds => 0f;

        public float GetPresentationDurationSeconds(EnemyPresentationPhase phase)
        {
            if (TryResolveAnimationBinding(out _))
            {
                return GetPresentationDurationSeconds(ResolvePhaseCompatibilityCue(phase));
            }

            ResolveLegacyAnimatorSpeed(phase, out var presentationDurationSeconds);
            return presentationDurationSeconds;
        }

        internal float GetPresentationDurationSeconds(EnemyAnimationCue cue)
        {
            ResolveAnimatorSpeedForCue(cue, out var presentationDurationSeconds);
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
        private bool _animationBindingResolved;
        private bool _usesNewAnimationBinding;
        private EnemyAnimationBindingSnapshot _animationBinding;
        private EnemyAnimationPendingStateCommand _pendingStateCommand;

        public bool HasJumpAirborneTopologySuspendSnapshot => _jumpAirborneTopologySuspendSnapshot.HasValue;

        internal bool CanDriveCurrentAnimator => CanDriveAnimator(ResolveAnimator());

        internal Animator ResolveAnimatorForBindingValidation()
        {
            return animator != null
                ? animator
                : GetComponentInChildren<Animator>();
        }

        public int DebugLastJumpAirborneStateShortNameHash
        {
            get
            {
                var stateName = ResolveJumpAirborneRestorableStateName();
                return EnemyAnimatorStateNameResolver.TryResolveLayerZeroStateHash(
                    ResolveAnimator(),
                    stateName,
                    out var stateHash)
                    ? stateHash
                    : Animator.StringToHash(stateName);
            }
        }

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
            Apply(state, EnemyPresentationOneShotBlockMask.None);
        }

        public void Apply(
            in EnemyViewPresentationState state,
            EnemyPresentationOneShotBlockMask oneShotSuppression)
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
            var preserveJumpAirbornePrimaryTrigger = false;

            ApplyAnimatorTimingForCue(targetAnimator, ResolveActiveTimingCue(state));
            if (state.JumpPhase != EnemyJumpPhase.Airborne)
            {
                _jumpAirborneTopologySuspendSnapshot = default;
                _lastJumpAirborneNormalizedTime = 0f;
            }

            if (state.StartedJumpWindupThisTick &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationOneShotBlockMask.JumpWindup))
            {
                JumpWindupSignalCount++;
                DispatchCue(EnemyAnimationCue.JumpWindup, targetAnimator);
            }

            if (state.StartedJumpAirborneThisTick &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationOneShotBlockMask.JumpAirborneStartOrRetry))
            {
                _jumpAirborneTopologySuspendSnapshot = default;
                JumpAirborneSignalCount++;
                DispatchCue(EnemyAnimationCue.JumpAirborne, targetAnimator);
                preserveJumpAirbornePrimaryTrigger = IsPrimaryTriggerDispatch(EnemyAnimationCue.JumpAirborne);
            }

            if (state.LandedFromJumpThisTick &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationOneShotBlockMask.JumpLand))
            {
                DispatchCue(EnemyAnimationCue.JumpLanding, targetAnimator);
            }

            var suppressChargeActiveStart =
                state.StartedChargeActiveThisTick &&
                IsSuppressed(oneShotSuppression, EnemyPresentationOneShotBlockMask.ChargeActiveStart);
            if (!suppressChargeActiveStart &&
                (state.StartedChargeActiveThisTick ||
                 (state.ChargePhase == EnemyChargePhase.Active && previousState.ChargePhase != EnemyChargePhase.Active)))
            {
                ChargeActiveSignalCount++;
                DispatchCue(EnemyAnimationCue.ChargeActive, targetAnimator);
            }

            var handledGlideWindup = false;
            if (state.StartedGlideWindupThisTick)
            {
                GlideWindupSignalCount++;
                var result = DispatchCue(EnemyAnimationCue.GlideWindup, targetAnimator);
                handledGlideWindup = _usesNewAnimationBinding ||
                                     result != EnemyAnimationDispatchResult.Unsupported;
            }

            if (state.StartedGlideActiveThisTick ||
                (state.GlidePhase == EnemyGlidePhase.Active && previousState.GlidePhase != EnemyGlidePhase.Active))
            {
                GlideActiveSignalCount++;
                DispatchCue(EnemyAnimationCue.GlideActive, targetAnimator);
            }

            if (state.StartedWindupThisTick &&
                !(state.StartedChargeWindupThisTick &&
                  IsSuppressed(oneShotSuppression, EnemyPresentationOneShotBlockMask.ChargeWindup)))
            {
                WindupSignalCount++;
                if (!handledGlideWindup)
                {
                    if (state.StartedChargeWindupThisTick)
                    {
                        DispatchCue(EnemyAnimationCue.ChargeWindup, targetAnimator);
                    }
                    else if (!SupportsUtilityWindupCue(state))
                    {
                        DispatchCue(EnemyAnimationCue.ActionWindup, targetAnimator);
                    }
                }
            }

            if (state.StartedUtilityWindupThisTick)
            {
                PlayUtilityWindup(
                    state.UtilityPresentationKind,
                    targetAnimator,
                    dispatchCue: !handledGlideWindup && !state.StartedChargeWindupThisTick);
            }

            if (state.ExecutedThisTick)
            {
                AttackSignalCount++;
                DispatchCue(EnemyAnimationCue.ActionExecute, targetAnimator);
            }

            var handledGlideRecovery = false;
            if (state.StartedGlideRecoverThisTick)
            {
                GlideRecoverySignalCount++;
                var result = DispatchCue(EnemyAnimationCue.GlideRecovery, targetAnimator);
                handledGlideRecovery = _usesNewAnimationBinding ||
                                       result != EnemyAnimationDispatchResult.Unsupported;
            }

            if (state.StartedRecoveryThisTick &&
                !(state.StartedChargeRecoverThisTick &&
                  IsSuppressed(oneShotSuppression, EnemyPresentationOneShotBlockMask.ChargeRecover)))
            {
                RecoverySignalCount++;
                if (!handledGlideRecovery)
                {
                    var recoveryCue = state.StartedChargeRecoverThisTick
                        ? EnemyAnimationCue.ChargeRecovery
                        : state.StartedUtilityRecoverThisTick
                            ? EnemyAnimationCue.UtilityRecovery
                            : state.StartedSummonRecoverThisTick
                                ? EnemyAnimationCue.None
                                : EnemyAnimationCue.ActionRecovery;
                    if (recoveryCue != EnemyAnimationCue.None)
                    {
                        DispatchCue(recoveryCue, targetAnimator);
                    }
                }
            }

            if (state.TookDamage)
            {
                HitSignalCount++;
                DispatchCue(EnemyAnimationCue.Hit, targetAnimator);
            }

            if (state.DidDie &&
                !IsSuppressed(oneShotSuppression, EnemyPresentationOneShotBlockMask.DeathTrigger))
            {
                DeathSignalCount++;
                DispatchCue(EnemyAnimationCue.Death, targetAnimator);
            }

            if (state.JumpPhase == EnemyJumpPhase.Airborne &&
                (!state.StartedJumpAirborneThisTick || !preserveJumpAirbornePrimaryTrigger) &&
                !state.LandedFromJumpThisTick &&
                !state.DidDie)
            {
                EnsureJumpAirborneAnimatorState(targetAnimator);
            }
        }

        private static bool IsSuppressed(
            EnemyPresentationOneShotBlockMask suppression,
            EnemyPresentationOneShotBlockMask value)
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
            ApplyAnimatorTimingForCue(targetAnimator, ResolveActiveTimingCue(settledState));
            DispatchCue(EnemyAnimationCue.JumpLanding, targetAnimator);
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

            ApplyAnimatorTimingForCue(targetAnimator, ResolveActiveTimingCue(LastPresentationState));
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

            ApplyAnimatorTimingForCue(
                targetAnimator,
                ResolveActiveTimingCue(LastPresentationState),
                driveAnimator: false);
        }

        public void ApplyPresentationPhaseTiming(EnemyPresentationPhase phase)
        {
            if (IsPresentationPaused)
            {
                return;
            }

            if (TryResolveAnimationBinding(out _))
            {
                ApplyPresentationCueTiming(ResolvePhaseCompatibilityCue(phase));
                return;
            }

            ApplyLegacyAnimatorTiming(ResolveAnimator(), phase);
        }

        internal void ApplyPresentationCueTiming(EnemyAnimationCue cue)
        {
            if (IsPresentationPaused)
            {
                return;
            }

            ApplyAnimatorTimingForCue(ResolveAnimator(), cue);
        }

        public void RestorePresentationTiming()
        {
            if (IsPresentationPaused)
            {
                return;
            }

            ApplyAnimatorTimingForCue(ResolveAnimator(), ResolveActiveTimingCue(LastPresentationState));
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
            var activeCue = ResolveActiveTimingCue(LastPresentationState);
            ApplyAnimatorTimingForCue(targetAnimator, activeCue);
            if (activeCue == EnemyAnimationCue.JumpAirborne &&
                _jumpAirborneTopologySuspendSnapshot.HasValue)
            {
                return RestoreJumpAirborneAnimatorAfterTopologySuspend(targetAnimator);
            }

            return TryRestoreCueState(activeCue, targetAnimator);
        }

        public void PlayUtilityWindup(EnemyUtilityPresentationKind kind)
        {
            PlayUtilityWindup(kind, ResolveAnimator(), dispatchCue: true);
        }

        private void PlayUtilityWindup(
            EnemyUtilityPresentationKind kind,
            Animator targetAnimator,
            bool dispatchCue)
        {
            if (kind != EnemyUtilityPresentationKind.GravityFieldAura)
            {
                return;
            }

            if (IsPresentationPaused)
            {
                return;
            }

            UtilityWindupSignalCount++;
            if (dispatchCue)
            {
                DispatchCue(EnemyAnimationCue.UtilityWindup, targetAnimator);
            }
        }

        private static bool SupportsUtilityWindupCue(in EnemyViewPresentationState state)
        {
            return state.StartedUtilityWindupThisTick &&
                   state.UtilityPresentationKind == EnemyUtilityPresentationKind.GravityFieldAura;
        }

        internal void PlayDeathCue(int entityId)
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
                return;
            }

            DeathSignalCount++;
            DispatchCue(EnemyAnimationCue.Death, targetAnimator);
            ApplyAnimatorTimingForCue(targetAnimator, EnemyAnimationCue.Death);
        }

        [Obsolete(
            "Enemy animation no longer owns death presentation duration. Use the typed enemy presentation playback path.",
            false)]
        public float PlayDeathPresentation(int entityId)
        {
            PlayDeathCue(entityId);
            return 0f;
        }

        private Animator ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            return animator;
        }

        private bool TryResolveAnimationBinding(out EnemyAnimationBindingSnapshot animationBinding)
        {
            if (_animationBindingResolved)
            {
                animationBinding = _animationBinding;
                return _usesNewAnimationBinding;
            }

            var authoring = EnemyAnimationBindingAuthoring.GetOptionalValidatedRoot(this);
            _animationBindingResolved = true;
            _usesNewAnimationBinding = authoring != null;
            _animationBinding = authoring?.CreateSnapshot();
            animationBinding = _animationBinding;
            return _usesNewAnimationBinding;
        }

        private bool TryResolveAnimationTiming(out EnemyAnimationTimingSnapshot animationTiming)
        {
            if (TryResolveAnimationBinding(out _))
            {
                animationTiming = default;
                return false;
            }

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

        internal void ApplyAnimatorTimingForCue(EnemyAnimationCue cue)
        {
            ApplyAnimatorTimingForCue(ResolveAnimator(), cue);
        }

        private void ApplyAnimatorTimingForCue(
            Animator targetAnimator,
            EnemyAnimationCue cue,
            bool driveAnimator = true)
        {
            var resolvedSpeed = ResolveAnimatorSpeedForCue(cue, out var presentationDurationSeconds);
            ApplyResolvedAnimatorTiming(targetAnimator, resolvedSpeed, presentationDurationSeconds, driveAnimator);
        }

        private void ApplyLegacyAnimatorTiming(
            Animator targetAnimator,
            EnemyPresentationPhase phase,
            bool driveAnimator = true)
        {
            var resolvedSpeed = ResolveLegacyAnimatorSpeed(phase, out var presentationDurationSeconds);
            ApplyResolvedAnimatorTiming(targetAnimator, resolvedSpeed, presentationDurationSeconds, driveAnimator);
        }

        private void ApplyResolvedAnimatorTiming(
            Animator targetAnimator,
            float resolvedSpeed,
            float presentationDurationSeconds,
            bool driveAnimator)
        {
            CurrentAnimatorSpeed = resolvedSpeed;
            CurrentPresentationDurationSeconds = presentationDurationSeconds;
            if (IsPresentationPaused)
            {
                return;
            }

            var targetSpeed = IsPlaybackSuppressed ? 0f : resolvedSpeed;
            if (driveAnimator && CanSetAnimatorSpeed(targetAnimator))
            {
                targetAnimator.speed = targetSpeed;
            }
        }

        private float ResolveAnimatorSpeedForCue(
            EnemyAnimationCue cue,
            out float presentationDurationSeconds)
        {
            if (cue == EnemyAnimationCue.None || cue == EnemyAnimationCue.Death)
            {
                presentationDurationSeconds = 0f;
                return 1f;
            }

            if (!TryResolveAnimationBinding(out var animationBinding))
            {
                return ResolveLegacyAnimatorSpeed(ResolveLegacyPresentationPhase(cue), out presentationDurationSeconds);
            }

            if (!animationBinding.TryGetBinding(cue, out var binding) || !binding.HasReferenceClipLength)
            {
                presentationDurationSeconds = 0f;
                return 1f;
            }

            if (binding.AnimatorDurationSeconds == EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel)
            {
                presentationDurationSeconds = binding.ReferenceClipLengthSeconds;
                return 1f;
            }

            presentationDurationSeconds = binding.AnimatorDurationSeconds;
            return Mathf.Max(0.01f, binding.ReferenceClipLengthSeconds / binding.AnimatorDurationSeconds);
        }

        private float ResolveLegacyAnimatorSpeed(
            EnemyPresentationPhase phase,
            out float presentationDurationSeconds)
        {
            if (phase == EnemyPresentationPhase.None || phase == EnemyPresentationPhase.Death)
            {
                presentationDurationSeconds = 0f;
                return 1f;
            }

            var hasReferenceClipLength = TryResolveLegacyReferenceClipLengthSeconds(
                phase,
                out var referenceClipLengthSeconds);
            if (!TryResolveLegacyAnimatorDurationOverride(phase, out var overrideDurationSeconds))
            {
                presentationDurationSeconds = hasReferenceClipLength ? referenceClipLengthSeconds : 0f;
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

        private bool TryResolveLegacyAnimatorDurationOverride(
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
                case EnemyPresentationPhase.Recovery:
                    return animationTiming.TryGetRecoverAnimatorDurationOverride(out durationSeconds);
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

        private bool TryResolveLegacyReferenceClipLengthSeconds(
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

                default:
                    referenceClipLengthSeconds = 0f;
                    return false;
            }
        }

        internal EnemyAnimationCue ResolveActiveTimingCue(in EnemyViewPresentationState state)
        {
            if (state.DidDie)
            {
                return EnemyAnimationCue.Death;
            }

            switch (state.JumpPhase)
            {
                case EnemyJumpPhase.Windup:
                    return EnemyAnimationCue.JumpWindup;
                case EnemyJumpPhase.Airborne:
                    return EnemyAnimationCue.JumpAirborne;
            }

            if (state.GlidePhase == EnemyGlidePhase.Windup)
            {
                return EnemyAnimationCue.GlideWindup;
            }

            if (state.GlidePhase == EnemyGlidePhase.Recovery)
            {
                return EnemyAnimationCue.GlideRecovery;
            }

            switch (state.ChargePhase)
            {
                case EnemyChargePhase.Windup:
                    return EnemyAnimationCue.ChargeWindup;
                case EnemyChargePhase.Active:
                    return EnemyAnimationCue.ChargeActive;
                case EnemyChargePhase.Recover:
                    return EnemyAnimationCue.ChargeRecovery;
            }

            switch (state.GlidePhase)
            {
                case EnemyGlidePhase.Active:
                    return EnemyAnimationCue.GlideActive;
            }

            if (state.UtilityPresentationKind != EnemyUtilityPresentationKind.None ||
                state.StartedUtilityWindupThisTick ||
                state.StartedUtilityRecoverThisTick ||
                state.StartedSummonWindupThisTick ||
                state.StartedSummonRecoverThisTick)
            {
                return EnemyAnimationCue.None;
            }

            switch (state.AiMode)
            {
                case EnemyAiMode.Attack:
                    return EnemyAnimationCue.ActionWindup;
                case EnemyAiMode.Recover:
                    return EnemyAnimationCue.ActionRecovery;
                default:
                    return EnemyAnimationCue.None;
            }
        }

        private EnemyAnimationCue ResolvePhaseCompatibilityCue(EnemyPresentationPhase phase)
        {
            switch (phase)
            {
                case EnemyPresentationPhase.None:
                    return EnemyAnimationCue.None;
                case EnemyPresentationPhase.JumpWindup:
                    return EnemyAnimationCue.JumpWindup;
                case EnemyPresentationPhase.JumpAirborne:
                    return EnemyAnimationCue.JumpAirborne;
                case EnemyPresentationPhase.ChargeActive:
                    return EnemyAnimationCue.ChargeActive;
                case EnemyPresentationPhase.Death:
                    return EnemyAnimationCue.Death;
                case EnemyPresentationPhase.Windup:
                case EnemyPresentationPhase.Recovery:
                    if (TryResolvePhaseCueFromLastPresentation(phase, out var resolvedCue))
                    {
                        return resolvedCue;
                    }

                    return ResolveSingleConfiguredTimingCue(phase);
                default:
                    return EnemyAnimationCue.None;
            }
        }

        private bool TryResolvePhaseCueFromLastPresentation(
            EnemyPresentationPhase phase,
            out EnemyAnimationCue cue)
        {
            if (phase == EnemyPresentationPhase.Windup)
            {
                if (LastPresentationState.JumpPhase == EnemyJumpPhase.Windup)
                {
                    cue = EnemyAnimationCue.JumpWindup;
                    return true;
                }

                if (LastPresentationState.GlidePhase == EnemyGlidePhase.Windup)
                {
                    cue = EnemyAnimationCue.GlideWindup;
                    return true;
                }

                if (LastPresentationState.ChargePhase == EnemyChargePhase.Windup)
                {
                    cue = EnemyAnimationCue.ChargeWindup;
                    return true;
                }

                if (LastPresentationState.StartedUtilityWindupThisTick ||
                    LastPresentationState.UtilityPhase == EnemyUtilityEffectPhase.Windup)
                {
                    cue = EnemyAnimationCue.UtilityWindup;
                    return true;
                }

                if (LastPresentationState.StartedSummonWindupThisTick)
                {
                    cue = EnemyAnimationCue.None;
                    return true;
                }

                if (LastPresentationState.ActiveActionKind != EnemyActionKind.None ||
                    LastPresentationState.AiMode == EnemyAiMode.Attack)
                {
                    cue = EnemyAnimationCue.ActionWindup;
                    return true;
                }
            }
            else if (phase == EnemyPresentationPhase.Recovery)
            {
                if (LastPresentationState.GlidePhase == EnemyGlidePhase.Recovery)
                {
                    cue = EnemyAnimationCue.GlideRecovery;
                    return true;
                }

                if (LastPresentationState.ChargePhase == EnemyChargePhase.Recover)
                {
                    cue = EnemyAnimationCue.ChargeRecovery;
                    return true;
                }

                if (LastPresentationState.StartedUtilityRecoverThisTick ||
                    LastPresentationState.UtilityPhase == EnemyUtilityEffectPhase.Recover)
                {
                    cue = EnemyAnimationCue.UtilityRecovery;
                    return true;
                }

                if (LastPresentationState.StartedSummonRecoverThisTick)
                {
                    cue = EnemyAnimationCue.None;
                    return true;
                }

                if (LastPresentationState.ActiveActionKind != EnemyActionKind.None ||
                    LastPresentationState.AiMode == EnemyAiMode.Recover)
                {
                    cue = EnemyAnimationCue.ActionRecovery;
                    return true;
                }
            }

            cue = EnemyAnimationCue.None;
            return false;
        }

        private EnemyAnimationCue ResolveSingleConfiguredTimingCue(EnemyPresentationPhase phase)
        {
            if (!TryResolveAnimationBinding(out var animationBinding))
            {
                return EnemyAnimationCue.None;
            }

            var match = EnemyAnimationCue.None;
            var matchCount = 0;
            foreach (var binding in animationBinding.Bindings)
            {
                if (!binding.HasReferenceClipLength || !IsCueInCompatibilityPhase(binding.Cue, phase))
                {
                    continue;
                }

                match = binding.Cue;
                matchCount++;
            }

            if (matchCount > 1)
            {
                throw new InvalidOperationException(
                    $"{phase} timing is ambiguous across {matchCount} configured enemy animation cue bindings.");
            }

            return match;
        }

        private static bool IsCueInCompatibilityPhase(EnemyAnimationCue cue, EnemyPresentationPhase phase)
        {
            return phase switch
            {
                EnemyPresentationPhase.Windup =>
                    cue == EnemyAnimationCue.ActionWindup ||
                    cue == EnemyAnimationCue.ChargeWindup ||
                    cue == EnemyAnimationCue.GlideWindup ||
                    cue == EnemyAnimationCue.UtilityWindup,
                EnemyPresentationPhase.Recovery =>
                    cue == EnemyAnimationCue.ActionRecovery ||
                    cue == EnemyAnimationCue.ChargeRecovery ||
                    cue == EnemyAnimationCue.GlideRecovery ||
                    cue == EnemyAnimationCue.UtilityRecovery,
                _ => false,
            };
        }

        private static EnemyPresentationPhase ResolveLegacyPresentationPhase(EnemyAnimationCue cue)
        {
            return cue switch
            {
                EnemyAnimationCue.JumpWindup => EnemyPresentationPhase.JumpWindup,
                EnemyAnimationCue.JumpAirborne => EnemyPresentationPhase.JumpAirborne,
                EnemyAnimationCue.ActionWindup or
                EnemyAnimationCue.ChargeWindup or
                EnemyAnimationCue.GlideWindup or
                EnemyAnimationCue.UtilityWindup => EnemyPresentationPhase.Windup,
                EnemyAnimationCue.ChargeActive => EnemyPresentationPhase.ChargeActive,
                EnemyAnimationCue.ActionRecovery or
                EnemyAnimationCue.ChargeRecovery or
                EnemyAnimationCue.GlideRecovery or
                EnemyAnimationCue.UtilityRecovery => EnemyPresentationPhase.Recovery,
                EnemyAnimationCue.Death => EnemyPresentationPhase.Death,
                _ => EnemyPresentationPhase.None,
            };
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

        internal EnemyAnimationDispatchResult DispatchCue(EnemyAnimationCue cue)
        {
            return DispatchCue(cue, ResolveAnimator());
        }

        private EnemyAnimationDispatchResult DispatchCue(EnemyAnimationCue cue, Animator targetAnimator)
        {
            EnemyAnimationRuntimeBinding binding;
            float stateCrossFadeSeconds;
            var usesNewBinding = TryResolveAnimationBinding(out var animationBinding);
            if (usesNewBinding)
            {
                if (!animationBinding.TryGetBinding(cue, out binding))
                {
                    return EnemyAnimationDispatchResult.Unsupported;
                }

                stateCrossFadeSeconds = binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.State
                    ? animationBinding.DefaultStateCrossFadeDurationSeconds
                    : 0f;
            }
            else if (!TryResolveLegacyDispatchBinding(cue, out binding, out stateCrossFadeSeconds))
            {
                return EnemyAnimationDispatchResult.Unsupported;
            }

            if (binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.State)
            {
                return DispatchStateCommand(
                    cue,
                    binding.TargetName,
                    stateCrossFadeSeconds,
                    targetAnimator,
                    failOnUnresolvedState: usesNewBinding);
            }

            if (binding.PrimaryDispatchMode != EnemyAnimationDispatchMode.Trigger ||
                string.IsNullOrWhiteSpace(binding.TargetName))
            {
                return EnemyAnimationDispatchResult.Unsupported;
            }

            if (!CanDriveAnimator(targetAnimator))
            {
                return EnemyAnimationDispatchResult.AnimatorUnavailable;
            }

            if (!SetTrigger(targetAnimator, binding.TargetName))
            {
                return EnemyAnimationDispatchResult.AnimatorUnavailable;
            }

            if (cue == EnemyAnimationCue.ActionWindup ||
                cue == EnemyAnimationCue.ChargeWindup ||
                cue == EnemyAnimationCue.UtilityWindup)
            {
                _windupTriggerDispatchCount++;
            }
            else if (cue == EnemyAnimationCue.ActionRecovery ||
                     cue == EnemyAnimationCue.ChargeRecovery ||
                     cue == EnemyAnimationCue.UtilityRecovery)
            {
                _recoveryTriggerDispatchCount++;
            }

            return EnemyAnimationDispatchResult.Applied;
        }

        private bool IsPrimaryTriggerDispatch(EnemyAnimationCue cue)
        {
            if (TryResolveAnimationBinding(out var animationBinding))
            {
                return animationBinding.TryGetBinding(cue, out var binding) &&
                       binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.Trigger;
            }

            return TryResolveLegacyDispatchBinding(cue, out var legacyBinding, out _) &&
                   legacyBinding.PrimaryDispatchMode == EnemyAnimationDispatchMode.Trigger;
        }

        internal bool TryRestoreCueState(EnemyAnimationCue cue)
        {
            return TryRestoreCueState(cue, ResolveAnimator());
        }

        private bool TryRestoreCueState(EnemyAnimationCue cue, Animator targetAnimator)
        {
            if (!TryResolveRestorableState(cue, out var stateName, out var crossFadeSeconds, out var strict))
            {
                return false;
            }

            return DispatchStateCommand(
                       cue,
                       stateName,
                       crossFadeSeconds,
                       targetAnimator,
                       failOnUnresolvedState: strict) == EnemyAnimationDispatchResult.Applied;
        }

        private bool TryResolveRestorableState(
            EnemyAnimationCue cue,
            out string stateName,
            out float crossFadeSeconds,
            out bool failOnUnresolvedState)
        {
            if (TryResolveAnimationBinding(out var animationBinding))
            {
                failOnUnresolvedState = true;
                if (!animationBinding.TryGetBinding(cue, out var binding))
                {
                    stateName = string.Empty;
                    crossFadeSeconds = 0f;
                    return false;
                }

                if (binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.State)
                {
                    stateName = binding.TargetName;
                    crossFadeSeconds = animationBinding.DefaultStateCrossFadeDurationSeconds;
                    return true;
                }

                if (cue == EnemyAnimationCue.JumpAirborne &&
                    binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.Trigger)
                {
                    stateName = binding.SustainedStateName;
                    crossFadeSeconds = 0f;
                    return true;
                }

                stateName = string.Empty;
                crossFadeSeconds = 0f;
                return false;
            }

            failOnUnresolvedState = false;
            if (cue == EnemyAnimationCue.JumpAirborne)
            {
                stateName = jumpAirborneStateName;
                crossFadeSeconds = TryResolveStateTransitionCrossFadeDurationOverride(out var jumpCrossFade)
                    ? Mathf.Max(0f, jumpCrossFade)
                    : 0f;
                return !string.IsNullOrWhiteSpace(stateName);
            }

            if (TryResolveLegacyDispatchBinding(cue, out var legacyBinding, out crossFadeSeconds) &&
                legacyBinding.PrimaryDispatchMode == EnemyAnimationDispatchMode.State)
            {
                stateName = legacyBinding.TargetName;
                return true;
            }

            stateName = string.Empty;
            crossFadeSeconds = 0f;
            return false;
        }

        private bool TryResolveLegacyDispatchBinding(
            EnemyAnimationCue cue,
            out EnemyAnimationRuntimeBinding binding,
            out float stateCrossFadeSeconds)
        {
            var hasCrossFade = TryResolveStateTransitionCrossFadeDurationOverride(out var crossFadeSeconds);
            stateCrossFadeSeconds = hasCrossFade ? Mathf.Max(0f, crossFadeSeconds) : 0f;
            var mode = EnemyAnimationDispatchMode.Trigger;
            var targetName = string.Empty;

            switch (cue)
            {
                case EnemyAnimationCue.ActionWindup:
                case EnemyAnimationCue.ChargeWindup:
                case EnemyAnimationCue.UtilityWindup:
                    mode = hasCrossFade ? EnemyAnimationDispatchMode.State : EnemyAnimationDispatchMode.Trigger;
                    targetName = hasCrossFade ? windupStateName : windupTriggerName;
                    break;
                case EnemyAnimationCue.ActionExecute:
                    targetName = attackTriggerName;
                    break;
                case EnemyAnimationCue.ActionRecovery:
                case EnemyAnimationCue.ChargeRecovery:
                case EnemyAnimationCue.UtilityRecovery:
                    mode = hasCrossFade ? EnemyAnimationDispatchMode.State : EnemyAnimationDispatchMode.Trigger;
                    targetName = hasCrossFade ? recoveryStateName : recoveryTriggerName;
                    break;
                case EnemyAnimationCue.JumpWindup:
                    mode = hasCrossFade ? EnemyAnimationDispatchMode.State : EnemyAnimationDispatchMode.Trigger;
                    targetName = hasCrossFade ? jumpWindupStateName : jumpWindupTriggerName;
                    break;
                case EnemyAnimationCue.JumpAirborne:
                    mode = hasCrossFade ? EnemyAnimationDispatchMode.State : EnemyAnimationDispatchMode.Trigger;
                    targetName = hasCrossFade ? jumpAirborneStateName : jumpAirborneTriggerName;
                    break;
                case EnemyAnimationCue.JumpLanding:
                    mode = EnemyAnimationDispatchMode.State;
                    targetName = DefaultLocomotionStateName;
                    break;
                case EnemyAnimationCue.ChargeActive:
                    if (!hasCrossFade)
                    {
                        binding = default;
                        return false;
                    }

                    mode = EnemyAnimationDispatchMode.State;
                    targetName = chargeActiveStateName;
                    break;
                case EnemyAnimationCue.GlideWindup:
                case EnemyAnimationCue.GlideActive:
                case EnemyAnimationCue.GlideRecovery:
                    if (!hasCrossFade)
                    {
                        binding = default;
                        return false;
                    }

                    mode = EnemyAnimationDispatchMode.State;
                    targetName = cue switch
                    {
                        EnemyAnimationCue.GlideWindup => glideWindupStateName,
                        EnemyAnimationCue.GlideActive => glideActiveStateName,
                        _ => glideRecoveryStateName,
                    };
                    break;
                case EnemyAnimationCue.Hit:
                    targetName = hitTriggerName;
                    break;
                case EnemyAnimationCue.Death:
                    targetName = deathTriggerName;
                    break;
                default:
                    binding = default;
                    return false;
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                binding = default;
                return false;
            }

            binding = new EnemyAnimationRuntimeBinding(
                cue,
                mode,
                targetName,
                cue == EnemyAnimationCue.JumpAirborne ? jumpAirborneStateName : string.Empty,
                EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                null,
                0f);
            return true;
        }

        private static bool SetTrigger(Animator targetAnimator, string parameterName)
        {
            if (!CanDriveAnimator(targetAnimator) ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            var parameterHash = Animator.StringToHash(parameterName);
            var matchingParameterCount = 0;
            var parameters = targetAnimator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.nameHash != parameterHash)
                {
                    continue;
                }

                matchingParameterCount++;
                if (parameter.type != AnimatorControllerParameterType.Trigger)
                {
                    return false;
                }
            }

            if (matchingParameterCount != 1)
            {
                return false;
            }

            targetAnimator.SetTrigger(parameterHash);
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

            if (!TryResolveRestorableState(
                    EnemyAnimationCue.JumpAirborne,
                    out var jumpAirborneState,
                    out _,
                    out var strictStateResolution))
            {
                return false;
            }

            var fallbackStateHash = targetAnimator != null && CanDriveAnimator(targetAnimator)
                ? ResolveStateHashForCommand(targetAnimator, jumpAirborneState, strictStateResolution)
                : Animator.StringToHash(jumpAirborneState);
            if (!CanDriveAnimator(targetAnimator))
            {
                _jumpAirborneTopologySuspendSnapshot = new AnimatorStateSnapshot(
                    fallbackStateHash,
                    Mathf.Max(0f, _lastJumpAirborneNormalizedTime));
                return true;
            }

            var stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
            if (!EnemyAnimatorStateNameResolver.IsLayerZeroState(stateInfo, jumpAirborneState))
            {
                var ensuredStateHash = ResolveStateHashForCommand(
                    targetAnimator,
                    jumpAirborneState,
                    strictStateResolution);
                PlayAnimatorState(
                    targetAnimator,
                    ensuredStateHash,
                    jumpAirborneState,
                    Mathf.Max(0f, _lastJumpAirborneNormalizedTime));
                ApplyAnimatorTimingForCue(targetAnimator, EnemyAnimationCue.JumpAirborne);
                LastCrossFadedStateName = jumpAirborneState;
                stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
            }

            var stateHash = stateInfo.shortNameHash != 0 ? stateInfo.shortNameHash : fallbackStateHash;
            if (stateHash != fallbackStateHash &&
                !EnemyAnimatorStateNameResolver.IsLayerZeroState(stateInfo, jumpAirborneState))
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
            var jumpAirborneState = ResolveJumpAirborneRestorableStateName();
            PlayAnimatorState(targetAnimator, snapshot.StateHash, jumpAirborneState, snapshot.NormalizedTime);
            ApplyAnimatorTimingForCue(targetAnimator, EnemyAnimationCue.JumpAirborne);
            _lastJumpAirborneNormalizedTime = snapshot.NormalizedTime;
            LastCrossFadedStateName = jumpAirborneState;
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

            if (!TryResolveRestorableState(
                    EnemyAnimationCue.JumpAirborne,
                    out var jumpAirborneState,
                    out var crossFadeSeconds,
                    out var strictStateResolution))
            {
                return false;
            }

            var currentState = targetAnimator.GetCurrentAnimatorStateInfo(0);
            if (EnemyAnimatorStateNameResolver.IsLayerZeroState(currentState, jumpAirborneState))
            {
                _lastJumpAirborneNormalizedTime = NormalizeAnimatorTime(currentState.normalizedTime);
                ApplyAnimatorTimingForCue(targetAnimator, EnemyAnimationCue.JumpAirborne);
                return true;
            }

            var result = DispatchStateCommand(
                EnemyAnimationCue.JumpAirborne,
                jumpAirborneState,
                crossFadeSeconds,
                targetAnimator,
                strictStateResolution);
            if (result == EnemyAnimationDispatchResult.Applied)
            {
                targetAnimator.Update(0f);
            }

            ApplyAnimatorTimingForCue(targetAnimator, EnemyAnimationCue.JumpAirborne);
            return result == EnemyAnimationDispatchResult.Applied;
        }

        private void PlayAnimatorState(
            Animator targetAnimator,
            int stateHash,
            string stateName,
            float normalizedTime)
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

            targetAnimator.Play(EnemyAnimatorStateNameResolver.GetRootPath(stateName), 0, normalizedTime);
            targetAnimator.Update(0f);
            if (targetAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != 0)
            {
                return;
            }

            targetAnimator.Play(stateName, 0, normalizedTime);
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

        private EnemyAnimationDispatchResult DispatchStateCommand(
            EnemyAnimationCue cue,
            string stateName,
            float crossFadeSeconds,
            Animator targetAnimator,
            bool failOnUnresolvedState)
        {
            if (IsPresentationPaused)
            {
                return EnemyAnimationDispatchResult.AnimatorUnavailable;
            }

            if (string.IsNullOrWhiteSpace(stateName))
            {
                return EnemyAnimationDispatchResult.Unsupported;
            }

            var resolvedDurationSeconds = Mathf.Max(0f, crossFadeSeconds);
            LastCrossFadeDurationSeconds = resolvedDurationSeconds;
            LastCrossFadedStateName = stateName;

            if (!CanDriveAnimator(targetAnimator))
            {
                _pendingStateCommand = new EnemyAnimationPendingStateCommand(
                    cue,
                    stateName,
                    resolvedDurationSeconds);
                return EnemyAnimationDispatchResult.Queued;
            }

            var stateHash = ResolveStateHashForCommand(
                targetAnimator,
                stateName,
                failOnUnresolvedState);
            targetAnimator.CrossFadeInFixedTime(
                stateHash,
                resolvedDurationSeconds);
            _pendingStateCommand = default;
            return EnemyAnimationDispatchResult.Applied;
        }

        private bool TryConsumePendingNamedStateCrossFade(Animator targetAnimator)
        {
            if (IsPresentationPaused)
            {
                return false;
            }

            if (!_pendingStateCommand.HasValue ||
                !CanDriveAnimator(targetAnimator))
            {
                return false;
            }

            var pending = _pendingStateCommand;
            var stateHash = ResolveStateHashForCommand(
                targetAnimator,
                pending.StateName,
                failOnUnresolvedState: _usesNewAnimationBinding);
            targetAnimator.CrossFadeInFixedTime(stateHash, pending.CrossFadeSeconds);
            LastCrossFadeDurationSeconds = pending.CrossFadeSeconds;
            LastCrossFadedStateName = pending.StateName;
            _pendingStateCommand = default;
            return true;
        }

        private static int ResolveStateHashForCommand(
            Animator targetAnimator,
            string stateName,
            bool failOnUnresolvedState)
        {
            if (EnemyAnimatorStateNameResolver.TryResolveLayerZeroStateHash(
                    targetAnimator,
                    stateName,
                    out var stateHash))
            {
                return stateHash;
            }

            if (failOnUnresolvedState)
            {
                throw new InvalidOperationException(
                    $"Animation state '{stateName}' cannot be resolved on layer 0.");
            }

            return EnemyAnimatorStateNameResolver.ResolveLayerZeroStateHashOrFallback(
                targetAnimator,
                stateName);
        }

        private string ResolveJumpAirborneRestorableStateName()
        {
            return TryResolveRestorableState(
                       EnemyAnimationCue.JumpAirborne,
                       out var stateName,
                       out _,
                       out _)
                ? stateName
                : jumpAirborneStateName;
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
