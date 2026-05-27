using System;

namespace Game.Feature.Gameplay.PlayerControl
{
    public enum DamageRejectReason
    {
        None = 0,
        ReceiverCooldown = 1,
        PlayerInvincible = 2,
    }

    public struct PlayerDamageState
    {
        public int nextDamageAllowedTick;
    }

    internal readonly struct PlayerDamageSnapshotEntry
    {
        public PlayerDamageSnapshotEntry(int entityId, PlayerDamageState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public PlayerDamageState State { get; }
    }

    internal static class PlayerDamageQueries
    {
        public static bool CanAcceptDamage(in PlayerDamageState state, int tickIndex)
        {
            return tickIndex >= state.nextDamageAllowedTick;
        }

        public static PlayerDamageState AcceptDamage(
            in PlayerDamageState state,
            int tickIndex,
            int damageCooldownTicks)
        {
            if (damageCooldownTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damageCooldownTicks));
            }

            var updatedState = state;
            updatedState.nextDamageAllowedTick = tickIndex + damageCooldownTicks + 1;
            return updatedState;
        }
    }
}
