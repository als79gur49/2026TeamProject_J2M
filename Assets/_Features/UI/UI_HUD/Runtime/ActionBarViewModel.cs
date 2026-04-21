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

    public readonly struct ActionSlotViewModel
    {
        public ActionSlotViewModel(
            HudActionSlotId slotId,
            string labelText,
            string stateText,
            bool isArmed = false)
        {
            SlotId = slotId;
            LabelText = labelText ?? string.Empty;
            StateText = stateText ?? string.Empty;
            IsArmed = isArmed;
        }

        public HudActionSlotId SlotId { get; }

        public string LabelText { get; }

        public string StateText { get; }

        public bool IsArmed { get; }
    }

    public sealed class ActionBarViewModel
    {
        public event Action Changed;

        private readonly ReadOnlyCollection<ActionSlotViewModel> _emptySlots =
            new(new List<ActionSlotViewModel>());
        private ReadOnlyCollection<ActionSlotViewModel> _slots;

        public IReadOnlyList<ActionSlotViewModel> Slots => _slots ?? _emptySlots;

        public string OutcomeText { get; private set; } = string.Empty;

        public bool IsInteractive { get; private set; }

        public void SetState(
            IEnumerable<ActionSlotViewModel> slots,
            string outcomeText,
            bool isInteractive)
        {
            _slots = new ReadOnlyCollection<ActionSlotViewModel>(new List<ActionSlotViewModel>(slots ?? Array.Empty<ActionSlotViewModel>()));
            OutcomeText = outcomeText ?? string.Empty;
            IsInteractive = isInteractive;
            Changed?.Invoke();
        }
    }
}
