using System;

namespace Game.Feature.UI.HUD
{
    public sealed class HUDRootViewModel
    {
        public event Action Changed;

        public bool IsVisible { get; private set; } = true;

        public bool IsDimmed { get; private set; }

        public bool IsPauseButtonEnabled { get; private set; } = true;

        public void SetShellState(
            bool isVisible,
            bool isDimmed,
            bool isPauseButtonEnabled)
        {
            IsVisible = isVisible;
            IsDimmed = isDimmed;
            IsPauseButtonEnabled = isPauseButtonEnabled;
            Changed?.Invoke();
        }
    }
}
