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
        Recovery = 4,
        Cooldown = 5,
    }

    public struct EnemyGlideRuntimeState
    {
        private const int LegacyLandingPendingPhaseValue = 3;

        [SerializeField] private EnemyGlidePhase phase;
        [SerializeField] private bool isActive;
        // Legacy serialized field only. Current runtime represents expired solid overlap as Active + WantsRecover.
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
        // Legacy serialized field only. Current runtime no longer stores a pending landing cell.
        [SerializeField] private SurfaceCell landingPendingCell;
        [SerializeField] private bool hasLockedStep;
        [SerializeField] private int lockedStepX;
        [SerializeField] private int lockedStepY;
        [SerializeField] private int lockedTargetEntityId;

        public EnemyGlidePhase Phase => ResolvePhase();

        public bool IsActive => Phase == EnemyGlidePhase.Active;

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

        public bool WantsRecover => wantsRecover || HasLegacyLandingPendingRecord;

        public bool InitialDelayInitialized => initialDelayInitialized;

        public int InitialDelayTicksRemaining => initialDelayTicksRemaining;

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
                hasLockedStep = hasLockedStep,
                lockedStepX = lockedStepX,
                lockedStepY = lockedStepY,
                lockedTargetEntityId = lockedTargetEntityId,
            };
        }

        private bool HasLegacyLandingPendingRecord =>
            (int)phase == LegacyLandingPendingPhaseValue ||
            isLandingPending;

        private EnemyGlidePhase ResolvePhase()
        {
            if (HasLegacyLandingPendingRecord)
            {
                return EnemyGlidePhase.Active;
            }

            if (phase != EnemyGlidePhase.Ready)
            {
                return phase;
            }

            if (isActive)
            {
                return EnemyGlidePhase.Active;
            }

            return EnemyGlidePhase.Ready;
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

    internal static class EnemyGlideSemanticQueries
    {
        public static bool IsActive(WorldSnapshot snapshot, int entityId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return snapshot.TryGetActiveEnemyGlideState(entityId, out _);
        }
    }
}
