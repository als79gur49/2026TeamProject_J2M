using System;

namespace Game.Shared.Display
{
    public readonly struct DisplaySettingsSnapshot : IEquatable<DisplaySettingsSnapshot>
    {
        public DisplaySettingsSnapshot(
            int width,
            int height,
            DisplayWindowMode windowMode,
            int refreshRateNumerator,
            int refreshRateDenominator)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            WindowMode = windowMode;
            RefreshRateNumerator = Math.Max(0, refreshRateNumerator);
            RefreshRateDenominator = Math.Max(1, refreshRateDenominator);
        }

        public int Width { get; }

        public int Height { get; }

        public DisplayWindowMode WindowMode { get; }

        public int RefreshRateNumerator { get; }

        public int RefreshRateDenominator { get; }

        public string VisibleResolutionLabel => $"{Width} x {Height}";

        public int PreferredRefreshRate =>
            RefreshRateNumerator <= 0
                ? 0
                : Math.Max(0, (int)Math.Round((double)RefreshRateNumerator / RefreshRateDenominator, MidpointRounding.AwayFromZero));

        public bool Equals(DisplaySettingsSnapshot other)
        {
            return Width == other.Width &&
                   Height == other.Height &&
                   WindowMode == other.WindowMode &&
                   RefreshRateNumerator == other.RefreshRateNumerator &&
                   RefreshRateDenominator == other.RefreshRateDenominator;
        }

        public override bool Equals(object obj)
        {
            return obj is DisplaySettingsSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height, (int)WindowMode, RefreshRateNumerator, RefreshRateDenominator);
        }
    }
}
