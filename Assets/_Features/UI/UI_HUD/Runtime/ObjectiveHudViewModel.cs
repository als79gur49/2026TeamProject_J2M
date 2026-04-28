using System;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveHudViewModel
    {
        public event Action Changed;

        public bool IsVisible { get; private set; }

        public bool IsComplete { get; private set; }

        public string ObjectiveText { get; private set; } = string.Empty;

        public void SetState(
            bool isVisible,
            string objectiveText,
            bool isComplete)
        {
            var nextText = objectiveText ?? string.Empty;
            if (IsVisible == isVisible &&
                IsComplete == isComplete &&
                string.Equals(ObjectiveText, nextText, StringComparison.Ordinal))
            {
                return;
            }

            IsVisible = isVisible;
            ObjectiveText = nextText;
            IsComplete = isComplete;
            Changed?.Invoke();
        }
    }
}
