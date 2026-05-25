using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyGlidePhase
    {
        Ready = 0,
        Windup = 1,
        Active = 2,
        // Compatibility-only residue for serialized/trace/presentation readers.
        // Current runtime transitions keep expired solid overlap as Active + WantsRecover.
        LandingPending = 3,
        Recovery = 4,
        Cooldown = 5,
    }

    public struct EnemyGlideRuntimeState
    {
        [SerializeField] private EnemyGlidePhase phase;
        [SerializeField] private bool isActive;
        [SerializeField] private bool isLandingPending;
        [SerializeField] private int sequence;
        [SerializeField] private int windupUntilTickExclusive;
        [SerializeField] private int activeUntilTickExclusive;
        [SerializeField] private int recoveryUntilTickExclusive;
        [SerializeField] private int cooldownUntilTickExclusive;
        [SerializeField] private int windupTicks;
        [SerializeField] private int durationTicks;
        [SerializeField] private int recoveryTicks;
        [SerializeField] private int cooldownTicks;
        [SerializeField] private int glideMoveTicks;
        [SerializeField] private int lastExitedTick;
        [SerializeField] private bool wantsRecover;
        [SerializeField] private bool initialDelayInitialized;
        [SerializeField] private int initialDelayTicksRemaining;
        [SerializeField] private SurfaceCell landingPendingCell;
        [SerializeField] private bool hasLockedStep;
        [SerializeField] private int lockedStepX;
        [SerializeField] private int lockedStepY;
        [SerializeField] private int lockedTargetEntityId;

        public EnemyGlidePhase Phase => ResolvePhase();

        public bool IsActive => Phase == EnemyGlidePhase.Active;

        public bool IsLandingPending => Phase == EnemyGlidePhase.LandingPending;

        public int Sequence => sequence;

        public int WindupUntilTickExclusive => windupUntilTickExclusive;

        public int ActiveUntilTickExclusive => activeUntilTickExclusive;

        public int RecoveryUntilTickExclusive => recoveryUntilTickExclusive;

        public int CooldownUntilTickExclusive => cooldownUntilTickExclusive;

        public int WindupTicks => windupTicks;

        public int DurationTicks => durationTicks;

        public int RecoveryTicks => recoveryTicks;

        public int CooldownTicks => cooldownTicks;

        public int GlideMoveTicks => glideMoveTicks;

        public int LastExitedTick => lastExitedTick;

        public bool WantsRecover => wantsRecover;

        public bool InitialDelayInitialized => initialDelayInitialized;

        public int InitialDelayTicksRemaining => initialDelayTicksRemaining;

        public SurfaceCell LandingPendingCell => landingPendingCell;

        public bool HasLockedStep => hasLockedStep;

        public int LockedStepX => lockedStepX;

        public int LockedStepY => lockedStepY;

        public int LockedTargetEntityId => lockedTargetEntityId;

        public bool HasAuthoritativeRecord =>
            Phase != EnemyGlidePhase.Ready ||
            isActive ||
            isLandingPending ||
            sequence != 0 ||
            windupUntilTickExclusive != 0 ||
            activeUntilTickExclusive != 0 ||
            recoveryUntilTickExclusive != 0 ||
            cooldownUntilTickExclusive != 0 ||
            windupTicks != 0 ||
            durationTicks != 0 ||
            recoveryTicks != 0 ||
            cooldownTicks != 0 ||
            glideMoveTicks != 0 ||
            lastExitedTick != 0 ||
            wantsRecover ||
            initialDelayInitialized ||
            initialDelayTicksRemaining != 0 ||
            hasLockedStep ||
            lockedStepX != 0 ||
            lockedStepY != 0 ||
            lockedTargetEntityId != 0;

        internal static EnemyGlideRuntimeState Create(
            bool isActive,
            bool isLandingPending,
            int sequence,
            int activeUntilTickExclusive,
            int cooldownUntilTickExclusive,
            int durationTicks,
            int cooldownTicks,
            int lastExitedTick,
            SurfaceCell landingPendingCell,
            bool hasLockedStep = false,
            int lockedStepX = 0,
            int lockedStepY = 0,
            bool initialDelayInitialized = false,
            int initialDelayTicksRemaining = 0,
            int lockedTargetEntityId = 0,
            bool wantsRecover = false,
            int glideMoveTicks = 0)
        {
            var phase = isActive
                ? EnemyGlidePhase.Active
                : isLandingPending
                    ? EnemyGlidePhase.LandingPending
                    : EnemyGlidePhase.Ready;
            return Create(
                phase,
                sequence,
                windupUntilTickExclusive: 0,
                activeUntilTickExclusive,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive,
                windupTicks: 0,
                durationTicks,
                recoveryTicks: 0,
                cooldownTicks,
                glideMoveTicks,
                lastExitedTick,
                wantsRecover,
                landingPendingCell,
                hasLockedStep,
                lockedStepX,
                lockedStepY,
                initialDelayInitialized,
                initialDelayTicksRemaining,
                lockedTargetEntityId);
        }

        internal static EnemyGlideRuntimeState Create(
            EnemyGlidePhase phase,
            int sequence,
            int windupUntilTickExclusive,
            int activeUntilTickExclusive,
            int recoveryUntilTickExclusive,
            int cooldownUntilTickExclusive,
            int windupTicks,
            int durationTicks,
            int recoveryTicks,
            int cooldownTicks,
            int lastExitedTick,
            SurfaceCell landingPendingCell,
            bool hasLockedStep = false,
            int lockedStepX = 0,
            int lockedStepY = 0,
            bool initialDelayInitialized = false,
            int initialDelayTicksRemaining = 0,
            int lockedTargetEntityId = 0)
        {
            return Create(
                phase,
                sequence,
                windupUntilTickExclusive,
                activeUntilTickExclusive,
                recoveryUntilTickExclusive,
                cooldownUntilTickExclusive,
                windupTicks,
                durationTicks,
                recoveryTicks,
                cooldownTicks,
                glideMoveTicks: 2,
                lastExitedTick,
                wantsRecover: false,
                landingPendingCell,
                hasLockedStep,
                lockedStepX,
                lockedStepY,
                initialDelayInitialized,
                initialDelayTicksRemaining,
                lockedTargetEntityId);
        }

        internal static EnemyGlideRuntimeState Create(
            EnemyGlidePhase phase,
            int sequence,
            int windupUntilTickExclusive,
            int activeUntilTickExclusive,
            int recoveryUntilTickExclusive,
            int cooldownUntilTickExclusive,
            int windupTicks,
            int durationTicks,
            int recoveryTicks,
            int cooldownTicks,
            int glideMoveTicks,
            int lastExitedTick,
            bool wantsRecover,
            SurfaceCell landingPendingCell,
            bool hasLockedStep = false,
            int lockedStepX = 0,
            int lockedStepY = 0,
            bool initialDelayInitialized = false,
            int initialDelayTicksRemaining = 0,
            int lockedTargetEntityId = 0)
        {
            return new EnemyGlideRuntimeState
            {
                phase = phase,
                isActive = phase == EnemyGlidePhase.Active,
                isLandingPending = phase == EnemyGlidePhase.LandingPending,
                sequence = sequence,
                windupUntilTickExclusive = windupUntilTickExclusive,
                activeUntilTickExclusive = activeUntilTickExclusive,
                recoveryUntilTickExclusive = recoveryUntilTickExclusive,
                cooldownUntilTickExclusive = cooldownUntilTickExclusive,
                windupTicks = windupTicks,
                durationTicks = durationTicks,
                recoveryTicks = recoveryTicks,
                cooldownTicks = cooldownTicks,
                glideMoveTicks = glideMoveTicks,
                lastExitedTick = lastExitedTick,
                wantsRecover = wantsRecover,
                initialDelayInitialized = initialDelayInitialized,
                initialDelayTicksRemaining = initialDelayTicksRemaining,
                landingPendingCell = landingPendingCell,
                hasLockedStep = hasLockedStep,
                lockedStepX = lockedStepX,
                lockedStepY = lockedStepY,
                lockedTargetEntityId = lockedTargetEntityId,
            };
        }

        private EnemyGlidePhase ResolvePhase()
        {
            if (phase != EnemyGlidePhase.Ready)
            {
                return phase;
            }

            if (isActive)
            {
                return EnemyGlidePhase.Active;
            }

            return isLandingPending ? EnemyGlidePhase.LandingPending : EnemyGlidePhase.Ready;
        }
    }

    internal readonly struct EnemyGlideSnapshotEntry
    {
        public EnemyGlideSnapshotEntry(int entityId, EnemyGlideRuntimeState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EnemyGlideRuntimeState State { get; }
    }

    internal static class EnemyGlideQueries
    {
        public static EnemyGlideRuntimeState Start(
            in EnemyGlideRuntimeState previousState,
            int tickIndex,
            in EnemyGlideTimingSettings timingSettings,
            Vector2Int lockedStep,
            int lockedTargetEntityId)
        {
            timingSettings.Validate(nameof(timingSettings));

            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Windup,
                sequence: Math.Max(1, previousState.Sequence + 1),
                windupUntilTickExclusive: tickIndex + timingSettings.WindupTicks,
                activeUntilTickExclusive: 0,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: timingSettings.WindupTicks,
                durationTicks: timingSettings.DurationTicks,
                recoveryTicks: timingSettings.RecoveryTicks,
                cooldownTicks: timingSettings.CooldownTicks,
                glideMoveTicks: timingSettings.GlideMoveTicks,
                lastExitedTick: previousState.LastExitedTick,
                wantsRecover: false,
                landingPendingCell: default,
                hasLockedStep: true,
                lockedStepX: lockedStep.x,
                lockedStepY: lockedStep.y,
                initialDelayInitialized: previousState.InitialDelayInitialized,
                initialDelayTicksRemaining: 0,
                lockedTargetEntityId: lockedTargetEntityId);
        }

        public static EnemyGlideRuntimeState TickInitialDelay(
            in EnemyGlideRuntimeState state,
            int initialDelayTicks)
        {
            var remainingTicks = state.InitialDelayTicksRemaining;
            if (!state.InitialDelayInitialized)
            {
                remainingTicks = Mathf.Max(0, initialDelayTicks);
            }

            if (remainingTicks > 0)
            {
                remainingTicks = Mathf.Max(0, remainingTicks - 1);
            }

            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Ready,
                sequence: state.Sequence,
                windupUntilTickExclusive: 0,
                activeUntilTickExclusive: 0,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: 0,
                durationTicks: 0,
                recoveryTicks: 0,
                cooldownTicks: 0,
                glideMoveTicks: 0,
                lastExitedTick: state.LastExitedTick,
                wantsRecover: false,
                landingPendingCell: default,
                initialDelayInitialized: true,
                initialDelayTicksRemaining: remainingTicks);
        }

        public static EnemyGlideRuntimeState BeginActive(
            in EnemyGlideRuntimeState state,
            int tickIndex,
            Vector2Int? lockedStep = null,
            int lockedTargetEntityId = 0)
        {
            var hasLockedStep = lockedStep.HasValue &&
                                Math.Abs(lockedStep.Value.x) + Math.Abs(lockedStep.Value.y) == 1;
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Active,
                sequence: state.Sequence,
                windupUntilTickExclusive: state.WindupUntilTickExclusive,
                activeUntilTickExclusive: tickIndex + state.DurationTicks,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: state.WindupTicks,
                durationTicks: state.DurationTicks,
                recoveryTicks: state.RecoveryTicks,
                cooldownTicks: state.CooldownTicks,
                glideMoveTicks: state.GlideMoveTicks,
                lastExitedTick: state.LastExitedTick,
                wantsRecover: false,
                landingPendingCell: default,
                hasLockedStep: hasLockedStep || state.HasLockedStep,
                lockedStepX: hasLockedStep ? lockedStep.Value.x : state.LockedStepX,
                lockedStepY: hasLockedStep ? lockedStep.Value.y : state.LockedStepY,
                initialDelayInitialized: state.InitialDelayInitialized,
                initialDelayTicksRemaining: state.InitialDelayTicksRemaining,
                lockedTargetEntityId: lockedTargetEntityId > 0 ? lockedTargetEntityId : state.LockedTargetEntityId);
        }

        public static EnemyGlideRuntimeState MarkActiveWantsRecover(in EnemyGlideRuntimeState state)
        {
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Active,
                sequence: state.Sequence,
                windupUntilTickExclusive: state.WindupUntilTickExclusive,
                activeUntilTickExclusive: state.ActiveUntilTickExclusive,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: state.WindupTicks,
                durationTicks: state.DurationTicks,
                recoveryTicks: state.RecoveryTicks,
                cooldownTicks: state.CooldownTicks,
                glideMoveTicks: state.GlideMoveTicks,
                lastExitedTick: state.LastExitedTick,
                wantsRecover: true,
                landingPendingCell: default,
                hasLockedStep: state.HasLockedStep,
                lockedStepX: state.LockedStepX,
                lockedStepY: state.LockedStepY,
                initialDelayInitialized: state.InitialDelayInitialized,
                initialDelayTicksRemaining: state.InitialDelayTicksRemaining,
                lockedTargetEntityId: state.LockedTargetEntityId);
        }

        public static EnemyGlideRuntimeState BeginRecovery(
            in EnemyGlideRuntimeState state,
            int tickIndex)
        {
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Recovery,
                sequence: state.Sequence,
                windupUntilTickExclusive: state.WindupUntilTickExclusive,
                activeUntilTickExclusive: state.ActiveUntilTickExclusive,
                recoveryUntilTickExclusive: tickIndex + state.RecoveryTicks,
                cooldownUntilTickExclusive: 0,
                windupTicks: state.WindupTicks,
                durationTicks: state.DurationTicks,
                recoveryTicks: state.RecoveryTicks,
                cooldownTicks: state.CooldownTicks,
                glideMoveTicks: state.GlideMoveTicks,
                lastExitedTick: state.LastExitedTick,
                wantsRecover: false,
                landingPendingCell: default,
                hasLockedStep: state.HasLockedStep,
                lockedStepX: state.LockedStepX,
                lockedStepY: state.LockedStepY,
                initialDelayInitialized: state.InitialDelayInitialized,
                initialDelayTicksRemaining: state.InitialDelayTicksRemaining,
                lockedTargetEntityId: state.LockedTargetEntityId);
        }

        public static EnemyGlideRuntimeState EndRecoveryToCooldown(
            in EnemyGlideRuntimeState state,
            int tickIndex)
        {
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Cooldown,
                sequence: state.Sequence,
                windupUntilTickExclusive: state.WindupUntilTickExclusive,
                activeUntilTickExclusive: state.ActiveUntilTickExclusive,
                recoveryUntilTickExclusive: state.RecoveryUntilTickExclusive,
                cooldownUntilTickExclusive: tickIndex + Mathf.Max(0, state.CooldownTicks),
                windupTicks: state.WindupTicks,
                durationTicks: state.DurationTicks,
                recoveryTicks: state.RecoveryTicks,
                cooldownTicks: state.CooldownTicks,
                glideMoveTicks: state.GlideMoveTicks,
                lastExitedTick: tickIndex,
                wantsRecover: false,
                landingPendingCell: default,
                hasLockedStep: state.HasLockedStep,
                lockedStepX: state.LockedStepX,
                lockedStepY: state.LockedStepY,
                initialDelayInitialized: state.InitialDelayInitialized,
                initialDelayTicksRemaining: state.InitialDelayTicksRemaining,
                lockedTargetEntityId: state.LockedTargetEntityId);
        }

        public static EnemyGlideRuntimeState Clear()
        {
            return default;
        }

        public static EnemyGlideRuntimeState ClearRuntimeActivityPreservingInitialDelay(in EnemyGlideRuntimeState state)
        {
            if (!state.InitialDelayInitialized &&
                state.InitialDelayTicksRemaining <= 0)
            {
                return default;
            }

            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Ready,
                sequence: 0,
                windupUntilTickExclusive: 0,
                activeUntilTickExclusive: 0,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: 0,
                durationTicks: 0,
                recoveryTicks: 0,
                cooldownTicks: 0,
                glideMoveTicks: 0,
                lastExitedTick: state.LastExitedTick,
                wantsRecover: false,
                landingPendingCell: default,
                initialDelayInitialized: state.InitialDelayInitialized,
                initialDelayTicksRemaining: state.InitialDelayTicksRemaining);
        }

        public static bool CanStart(bool hasPreviousState, in EnemyGlideRuntimeState state, int tickIndex)
        {
            if (!hasPreviousState)
            {
                return true;
            }

            return (state.Phase == EnemyGlidePhase.Ready ||
                    (state.Phase == EnemyGlidePhase.Cooldown &&
                     tickIndex >= state.CooldownUntilTickExclusive)) &&
                   tickIndex > state.LastExitedTick &&
                   state.InitialDelayTicksRemaining <= 0;
        }
    }
}
