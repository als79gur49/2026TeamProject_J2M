using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public struct EnemyGlideRuntimeState
    {
        [SerializeField] private bool isActive;
        [SerializeField] private bool isLandingPending;
        [SerializeField] private int sequence;
        [SerializeField] private int activeUntilTickExclusive;
        [SerializeField] private int cooldownUntilTickExclusive;
        [SerializeField] private int durationTicks;
        [SerializeField] private int cooldownTicks;
        [SerializeField] private int lastExitedTick;
        [SerializeField] private SurfaceCell landingPendingCell;

        public bool IsActive => isActive;

        public bool IsLandingPending => isLandingPending;

        public int Sequence => sequence;

        public int ActiveUntilTickExclusive => activeUntilTickExclusive;

        public int CooldownUntilTickExclusive => cooldownUntilTickExclusive;

        public int DurationTicks => durationTicks;

        public int CooldownTicks => cooldownTicks;

        public int LastExitedTick => lastExitedTick;

        public SurfaceCell LandingPendingCell => landingPendingCell;

        public bool HasAuthoritativeRecord =>
            isActive ||
            isLandingPending ||
            sequence != 0 ||
            activeUntilTickExclusive != 0 ||
            cooldownUntilTickExclusive != 0 ||
            durationTicks != 0 ||
            cooldownTicks != 0 ||
            lastExitedTick != 0;

        internal static EnemyGlideRuntimeState Create(
            bool isActive,
            bool isLandingPending,
            int sequence,
            int activeUntilTickExclusive,
            int cooldownUntilTickExclusive,
            int durationTicks,
            int cooldownTicks,
            int lastExitedTick,
            SurfaceCell landingPendingCell)
        {
            return new EnemyGlideRuntimeState
            {
                isActive = isActive,
                isLandingPending = isLandingPending,
                sequence = sequence,
                activeUntilTickExclusive = activeUntilTickExclusive,
                cooldownUntilTickExclusive = cooldownUntilTickExclusive,
                durationTicks = durationTicks,
                cooldownTicks = cooldownTicks,
                lastExitedTick = lastExitedTick,
                landingPendingCell = landingPendingCell,
            };
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
            in EnemyGlideTimingSettings timingSettings)
        {
            timingSettings.Validate(nameof(timingSettings));

            return EnemyGlideRuntimeState.Create(
                isActive: true,
                isLandingPending: false,
                sequence: Math.Max(1, previousState.Sequence + 1),
                activeUntilTickExclusive: tickIndex + timingSettings.DurationTicks,
                cooldownUntilTickExclusive: 0,
                durationTicks: timingSettings.DurationTicks,
                cooldownTicks: timingSettings.CooldownTicks,
                lastExitedTick: previousState.LastExitedTick,
                landingPendingCell: default);
        }

        public static EnemyGlideRuntimeState EndActiveToCooldown(
            in EnemyGlideRuntimeState state,
            int tickIndex)
        {
            return EnemyGlideRuntimeState.Create(
                isActive: false,
                isLandingPending: false,
                sequence: state.Sequence,
                activeUntilTickExclusive: state.ActiveUntilTickExclusive,
                cooldownUntilTickExclusive: tickIndex + Mathf.Max(0, state.CooldownTicks),
                durationTicks: state.DurationTicks,
                cooldownTicks: state.CooldownTicks,
                lastExitedTick: tickIndex,
                landingPendingCell: default);
        }

        public static EnemyGlideRuntimeState EndActiveToLandingPending(
            in EnemyGlideRuntimeState state,
            int tickIndex,
            SurfaceCell pendingCell)
        {
            return EnemyGlideRuntimeState.Create(
                isActive: false,
                isLandingPending: true,
                sequence: state.Sequence,
                activeUntilTickExclusive: state.ActiveUntilTickExclusive,
                cooldownUntilTickExclusive: 0,
                durationTicks: state.DurationTicks,
                cooldownTicks: state.CooldownTicks,
                lastExitedTick: tickIndex,
                landingPendingCell: pendingCell);
        }

        public static EnemyGlideRuntimeState ClearLandingPendingToCooldown(
            in EnemyGlideRuntimeState state,
            int tickIndex)
        {
            return EnemyGlideRuntimeState.Create(
                isActive: false,
                isLandingPending: false,
                sequence: state.Sequence,
                activeUntilTickExclusive: state.ActiveUntilTickExclusive,
                cooldownUntilTickExclusive: tickIndex + Mathf.Max(0, state.CooldownTicks),
                durationTicks: state.DurationTicks,
                cooldownTicks: state.CooldownTicks,
                lastExitedTick: tickIndex,
                landingPendingCell: default);
        }

        public static EnemyGlideRuntimeState Clear()
        {
            return default;
        }

        public static bool CanStart(bool hasPreviousState, in EnemyGlideRuntimeState state, int tickIndex)
        {
            if (!hasPreviousState)
            {
                return true;
            }

            return !state.IsActive &&
                   !state.IsLandingPending &&
                   tickIndex >= state.CooldownUntilTickExclusive &&
                   tickIndex > state.LastExitedTick;
        }
    }
}
