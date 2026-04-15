using System;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class PlayerStatusPresenter
    {
        public PlayerStatusViewModel ViewModel { get; } = new();

        public void Apply(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            ViewModel.SetState(
                player.CurrentHp,
                player.Facing.ToString(),
                FormatAction(player),
                tick.FinalTopology.BottomFace.ToString(),
                FormatStatus(tick, interaction, player),
                FormatDamage(tick, player));
        }

        private static string FormatAction(UIPlayerActionSlice player)
        {
            if (player.ActiveActionKind == GameplayUiActionKind.None)
            {
                return "Idle";
            }

            return player.IsRecoveryPhase
                ? $"{player.ActiveActionKind} (Recovery)"
                : player.ActiveActionKind.ToString();
        }

        private static string FormatStatus(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            if (tick.IsStageCleared)
            {
                return "Stage Cleared";
            }

            if (interaction.IsPaused)
            {
                return "Paused";
            }

            if (interaction.IsUiGameplayInputBlocked)
            {
                return "Read Only";
            }

            if (interaction.HasBlockingGameplayPresentation)
            {
                return "Busy";
            }

            if (!interaction.CanAcceptGameplayCommands)
            {
                return "Blocked";
            }

            if (player.IsRecoveryPhase)
            {
                return "Recovery";
            }

            return "Ready";
        }

        private static string FormatDamage(
            UITickSlice tick,
            UIPlayerActionSlice player)
        {
            if (player.TookDamageThisTick &&
                player.LastDamageAmount > 0 &&
                player.LastDamageTickIndex == tick.LastReducedTickIndex)
            {
                return $"-{player.LastDamageAmount} this tick";
            }

            return "Stable";
        }
    }
}
