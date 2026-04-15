using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public enum ActionBarSlotCommandKind
    {
        HoldMove = 0,
        Flip = 1,
    }

    public readonly struct ActionBarSlotDefinition
    {
        public ActionBarSlotDefinition(
            HudActionSlotId slotId,
            string labelText,
            GameplayUiActionKind actionKind,
            ActionBarSlotCommandKind commandKind,
            GameplayUiDirection direction)
        {
            SlotId = slotId;
            LabelText = labelText ?? string.Empty;
            ActionKind = actionKind;
            CommandKind = commandKind;
            Direction = direction;
        }

        public HudActionSlotId SlotId { get; }

        public string LabelText { get; }

        public GameplayUiActionKind ActionKind { get; }

        public ActionBarSlotCommandKind CommandKind { get; }

        public GameplayUiDirection Direction { get; }
    }

    public sealed class ActionBarPresenter
    {
        private static readonly ActionBarSlotDefinition[] DefaultSlots =
        {
            new(HudActionSlotId.Primary, "Move Up", GameplayUiActionKind.Push, ActionBarSlotCommandKind.HoldMove, GameplayUiDirection.Up),
            new(HudActionSlotId.Secondary, "Flip Right", GameplayUiActionKind.Flip, ActionBarSlotCommandKind.Flip, GameplayUiDirection.Right),
        };

        private readonly IGameplayCommandGateway _commandGateway;
        private readonly ActionBarSlotDefinition[] _slotDefinitions;

        private UITickSlice _tick;
        private UIInteractionSlice _interaction;
        private UIPlayerActionSlice _player;
        private string _feedbackText = string.Empty;
        private ActionBarCommandResult? _lastCommandResult;

        public ActionBarPresenter(
            IGameplayCommandGateway commandGateway,
            IEnumerable<ActionBarSlotDefinition> slotDefinitions = null)
        {
            _commandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
            _slotDefinitions = slotDefinitions != null
                ? CreateDefinitions(slotDefinitions)
                : DefaultSlots;
            ViewModel = new ActionBarViewModel();
            Apply(UIPresentationSnapshot.Empty.Tick, UIPresentationSnapshot.Empty.Interaction, UIPresentationSnapshot.Empty.Player);
        }

        public ActionBarViewModel ViewModel { get; }

        public ActionBarCommandResult RequestSlot(HudActionSlotId slotId)
        {
            if (!TryGetDefinition(slotId, out var definition))
            {
                return Reject(ActionBarCommandFailureKind.Unavailable);
            }

            if (!IsSlotInteractive(definition, _tick, _interaction, _player))
            {
                return Reject(ResolveLocalFailureKind(_tick, _interaction));
            }

            var acceptance = definition.CommandKind == ActionBarSlotCommandKind.HoldMove
                ? _commandGateway.SetHeldMoveDirection(definition.Direction)
                : _commandGateway.RequestFlip(definition.Direction);

            var result = MapAcceptance(acceptance);
            _lastCommandResult = result;
            _feedbackText = result.Accepted ? string.Empty : MapFeedback(result.FailureKind);
            Publish();
            return result;
        }

        public void Apply(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            _tick = tick;
            _interaction = interaction;
            _player = player;

            if (ShouldClearFeedback())
            {
                _feedbackText = string.Empty;
                _lastCommandResult = null;
            }

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
                var isHighlighted = _player.ActiveActionKind != GameplayUiActionKind.None &&
                                    _player.ActiveActionKind == definition.ActionKind;
                slots.Add(new ActionSlotViewModel(
                    definition.SlotId,
                    definition.LabelText,
                    BuildStateText(definition, _tick, _interaction, _player),
                    isInteractive,
                    isHighlighted));
                hasInteractiveSlot |= isInteractive;
            }

            ViewModel.SetState(
                slots,
                _feedbackText,
                BuildOutcomeText(_player),
                hasInteractiveSlot,
                _lastCommandResult);
        }

        private bool ShouldClearFeedback()
        {
            if (!_lastCommandResult.HasValue || _lastCommandResult.Value.Accepted)
            {
                return false;
            }

            switch (_lastCommandResult.Value.FailureKind)
            {
                case ActionBarCommandFailureKind.Paused:
                    return !_interaction.IsPaused && HasAnyInteractiveSlot(_tick, _interaction, _player);
                case ActionBarCommandFailureKind.Busy:
                    return !_interaction.HasBlockingGameplayPresentation &&
                           !_interaction.IsUiGameplayInputBlocked &&
                           HasAnyInteractiveSlot(_tick, _interaction, _player);
                default:
                    return HasAnyInteractiveSlot(_tick, _interaction, _player);
            }
        }

        private bool HasAnyInteractiveSlot(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            for (var i = 0; i < _slotDefinitions.Length; i++)
            {
                if (IsSlotInteractive(_slotDefinitions[i], tick, interaction, player))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildStateText(
            ActionBarSlotDefinition definition,
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player)
        {
            if (player.ActiveActionKind == definition.ActionKind)
            {
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
            return definition.CommandKind == ActionBarSlotCommandKind.HoldMove
                ? player.CanMoveThisTick
                : player.CanStartActionThisTick;
        }

        private static ActionBarCommandFailureKind ResolveLocalFailureKind(
            UITickSlice tick,
            UIInteractionSlice interaction)
        {
            if (interaction.IsPaused)
            {
                return ActionBarCommandFailureKind.Paused;
            }

            if (interaction.HasBlockingGameplayPresentation)
            {
                return ActionBarCommandFailureKind.Busy;
            }

            return tick.IsStageCleared || interaction.IsUiGameplayInputBlocked || !interaction.CanAcceptGameplayCommands
                ? ActionBarCommandFailureKind.Unavailable
                : ActionBarCommandFailureKind.Unavailable;
        }

        private ActionBarCommandResult Reject(ActionBarCommandFailureKind failureKind)
        {
            var result = ActionBarCommandResult.Reject(failureKind);
            _lastCommandResult = result;
            _feedbackText = MapFeedback(failureKind);
            Publish();
            return result;
        }

        private static ActionBarCommandResult MapAcceptance(GameplayCommandAcceptance acceptance)
        {
            if (acceptance.Accepted)
            {
                return ActionBarCommandResult.Accept();
            }

            switch (acceptance.RejectionReason)
            {
                case GameplayCommandRejectionReason.Paused:
                    return ActionBarCommandResult.Reject(ActionBarCommandFailureKind.Paused);
                case GameplayCommandRejectionReason.BlockingPresentation:
                    return ActionBarCommandResult.Reject(ActionBarCommandFailureKind.Busy);
                default:
                    return ActionBarCommandResult.Reject(ActionBarCommandFailureKind.Unavailable);
            }
        }

        private static string MapFeedback(ActionBarCommandFailureKind failureKind)
        {
            switch (failureKind)
            {
                case ActionBarCommandFailureKind.Paused:
                    return "Paused";
                case ActionBarCommandFailureKind.Busy:
                    return "Busy";
                default:
                    return "Unavailable";
            }
        }

        private bool TryGetDefinition(HudActionSlotId slotId, out ActionBarSlotDefinition definition)
        {
            for (var i = 0; i < _slotDefinitions.Length; i++)
            {
                if (_slotDefinitions[i].SlotId == slotId)
                {
                    definition = _slotDefinitions[i];
                    return true;
                }
            }

            definition = default;
            return false;
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
