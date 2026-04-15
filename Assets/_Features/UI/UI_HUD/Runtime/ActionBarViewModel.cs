using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Feature.UI.HUD
{
    public enum HudActionSlotId
    {
        Primary = 0,
        Secondary = 1,
    }

    public enum ActionBarCommandFailureKind
    {
        None = 0,
        Paused = 1,
        Busy = 2,
        Unavailable = 3,
    }

    public readonly struct ActionBarCommandResult
    {
        public ActionBarCommandResult(bool accepted, ActionBarCommandFailureKind failureKind)
        {
            Accepted = accepted;
            FailureKind = failureKind;
        }

        public bool Accepted { get; }

        public ActionBarCommandFailureKind FailureKind { get; }

        public static ActionBarCommandResult Accept()
        {
            return new ActionBarCommandResult(true, ActionBarCommandFailureKind.None);
        }

        public static ActionBarCommandResult Reject(ActionBarCommandFailureKind failureKind)
        {
            return new ActionBarCommandResult(false, failureKind);
        }
    }

    public readonly struct ActionSlotViewModel
    {
        public ActionSlotViewModel(
            HudActionSlotId slotId,
            string labelText,
            string stateText,
            bool isInteractive,
            bool isHighlighted)
        {
            SlotId = slotId;
            LabelText = labelText ?? string.Empty;
            StateText = stateText ?? string.Empty;
            IsInteractive = isInteractive;
            IsHighlighted = isHighlighted;
        }

        public HudActionSlotId SlotId { get; }

        public string LabelText { get; }

        public string StateText { get; }

        public bool IsInteractive { get; }

        public bool IsHighlighted { get; }
    }

    public sealed class ActionBarViewModel
    {
        public event Action Changed;

        private readonly ReadOnlyCollection<ActionSlotViewModel> _emptySlots =
            new(new List<ActionSlotViewModel>());
        private ReadOnlyCollection<ActionSlotViewModel> _slots;

        public IReadOnlyList<ActionSlotViewModel> Slots => _slots ?? _emptySlots;

        public string FeedbackText { get; private set; } = string.Empty;

        public string OutcomeText { get; private set; } = string.Empty;

        public bool IsInteractive { get; private set; }

        public ActionBarCommandResult? LastCommandResult { get; private set; }

        public void SetState(
            IEnumerable<ActionSlotViewModel> slots,
            string feedbackText,
            string outcomeText,
            bool isInteractive,
            ActionBarCommandResult? lastCommandResult)
        {
            _slots = new ReadOnlyCollection<ActionSlotViewModel>(new List<ActionSlotViewModel>(slots ?? Array.Empty<ActionSlotViewModel>()));
            FeedbackText = feedbackText ?? string.Empty;
            OutcomeText = outcomeText ?? string.Empty;
            IsInteractive = isInteractive;
            LastCommandResult = lastCommandResult;
            Changed?.Invoke();
        }
    }
}
