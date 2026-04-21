using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public readonly struct ActionBarSlotDefinition
    {
        public ActionBarSlotDefinition(
            HudActionSlotId slotId,
            string labelText,
            GameplayUiActionKind actionKind)
        {
            SlotId = slotId;
            LabelText = labelText ?? string.Empty;
            ActionKind = actionKind;
        }

        public HudActionSlotId SlotId { get; }

        public string LabelText { get; }

        public GameplayUiActionKind ActionKind { get; }
    }

    public sealed class ActionBarPresenter
    {
        private static readonly ActionBarSlotDefinition[] DefaultSlots =
        {
            new(HudActionSlotId.Primary, "Push", GameplayUiActionKind.Push),
            new(HudActionSlotId.Secondary, "Flip", GameplayUiActionKind.Flip),
        };

        private readonly ActionBarSlotDefinition[] _slotDefinitions;

        private UITickSlice _tick;
        private UIInteractionSlice _interaction;
        private UIPlayerActionSlice _player;

        public ActionBarPresenter(IEnumerable<ActionBarSlotDefinition> slotDefinitions = null)
        {
            _slotDefinitions = slotDefinitions != null
                ? CreateDefinitions(slotDefinitions)
                : DefaultSlots;
            ViewModel = new ActionBarViewModel();
            Apply(UIPresentationSnapshot.Empty.Tick, UIPresentationSnapshot.Empty.Interaction, UIPresentationSnapshot.Empty.Player);
        }

        public ActionBarViewModel ViewModel { get; }

        public void Apply(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            _tick = tick;
            _interaction = interaction;
            _player = player;

            Publish();
        }

        private void Publish()
        {
            var slots = new List<ActionSlotViewModel>(_slotDefinitions.Length);
            var hasInteractiveSlot = false;

            for (var i = 0; i < _slotDefinitions.Length; i++)
            {
                var definition = _slotDefinitions[i];
                var isInteractive = IsSlotInteractive(definition, _tick, _interaction, _player);
                slots.Add(new ActionSlotViewModel(
                    definition.SlotId,
                    definition.LabelText,
                    BuildStateText(definition, _tick, _interaction, _player),
                    IsSlotArmed(definition, _interaction, _player)));
                hasInteractiveSlot |= isInteractive;
            }

            ViewModel.SetState(
                slots,
                BuildOutcomeText(_player),
                hasInteractiveSlot);
        }

        private static string BuildStateText(
            ActionBarSlotDefinition definition,
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            if (player.ActiveActionKind == definition.ActionKind)
            {
                if (TryGetRecoveryCooldown(definition, player, out var recoveryCooldown))
                {
                    return $"Recovering: {recoveryCooldown.RemainingRecoveryTicks}";
                }

                return player.IsRecoveryPhase ? "Recovering" : "Active";
            }

            if (tick.IsStageCleared)
            {
                return "Cleared";
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

            if (definition.ActionKind == GameplayUiActionKind.Push &&
                player.CanStartAnyActionThisTick &&
                player.HasExplicitPushCandidateInCurrentDirection)
            {
                return "Armed";
            }

            return IsSlotReady(definition, player)
                ? "Ready"
                : "Unavailable";
        }

        private static string BuildOutcomeText(UIPlayerActionSlice player)
        {
            return player.LastResolvedOutcome == GameplayUiActionResolutionKind.None
                ? string.Empty
                : player.LastResolvedOutcome.ToString();
        }

        private static bool TryGetRecoveryCooldown(
            ActionBarSlotDefinition definition,
            UIPlayerActionSlice player,
            out UIRecoveryCooldownSlice recoveryCooldown)
        {
            if (player.RecoveryCooldown.HasValue &&
                player.RecoveryCooldown.Value.ActionKind == definition.ActionKind)
            {
                recoveryCooldown = player.RecoveryCooldown.Value;
                return true;
            }

            recoveryCooldown = default;
            return false;
        }

        private static bool IsSlotInteractive(
            ActionBarSlotDefinition definition,
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            if (tick.IsStageCleared ||
                interaction.IsPaused ||
                interaction.IsUiGameplayInputBlocked ||
                interaction.HasBlockingGameplayPresentation ||
                !interaction.CanAcceptGameplayCommands)
            {
                return false;
            }

            return IsSlotReady(definition, player);
        }

        private static bool IsSlotReady(ActionBarSlotDefinition definition, UIPlayerActionSlice player)
        {
            return definition.ActionKind == GameplayUiActionKind.Push
                ? player.CanStartAnyActionThisTick
                : player.CanStartActionThisTick;
        }

        private static bool IsSlotArmed(
            ActionBarSlotDefinition definition,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            if (definition.ActionKind != GameplayUiActionKind.Push ||
                interaction.IsPaused ||
                interaction.IsUiGameplayInputBlocked ||
                interaction.HasBlockingGameplayPresentation ||
                !interaction.CanAcceptGameplayCommands)
            {
                return false;
            }

            return player.CanStartAnyActionThisTick &&
                   player.HasExplicitPushCandidateInCurrentDirection;
        }

        private static ActionBarSlotDefinition[] CreateDefinitions(IEnumerable<ActionBarSlotDefinition> slotDefinitions)
        {
            var list = new List<ActionBarSlotDefinition>();
            foreach (var definition in slotDefinitions)
            {
                list.Add(definition);
            }

            return list.Count > 0 ? list.ToArray() : DefaultSlots;
        }
    }
}
