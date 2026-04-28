using System;

namespace Game.Feature.UI.HUD
{
    public sealed class HUDRootViewModel
    {
        public event Action Changed;

        public bool IsVisible { get; private set; } = true;

        public bool IsDimmed { get; private set; }

        public bool IsGameplayReadOnly { get; private set; } = true;

        public bool IsPauseButtonEnabled { get; private set; } = true;

        public void SetShellState(
            bool isVisible,
            bool isDimmed,
            bool isGameplayReadOnly,
            bool isPauseButtonEnabled)
        {
            IsVisible = isVisible;
            IsDimmed = isDimmed;
            IsGameplayReadOnly = isGameplayReadOnly;
            IsPauseButtonEnabled = isPauseButtonEnabled;
            Changed?.Invoke();
        }
    }

    public sealed class StageInfoViewModel
    {
        public event Action Changed;

        public string StageName { get; private set; } = string.Empty;

        public bool HasStageName => !string.IsNullOrWhiteSpace(StageName);

        public void SetStageName(string stageName)
        {
            var nextStageName = stageName ?? string.Empty;
            if (string.Equals(StageName, nextStageName, StringComparison.Ordinal))
            {
                return;
            }

            StageName = nextStageName;
            Changed?.Invoke();
        }
    }
}
