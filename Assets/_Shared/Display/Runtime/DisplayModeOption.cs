using System;

namespace Game.Shared.Display
{
    public readonly struct DisplayModeOption : IEquatable<DisplayModeOption>
    {
        public DisplayModeOption(
            int width,
            int height,
            int refreshRateNumerator,
            int refreshRateDenominator)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            RefreshRateNumerator = Math.Max(0, refreshRateNumerator);
            RefreshRateDenominator = Math.Max(1, refreshRateDenominator);
        }

        public int Width { get; }

        public int Height { get; }

        public int RefreshRateNumerator { get; }

        public int RefreshRateDenominator { get; }

        public string VisibleLabel => $"{Width} x {Height}";

        public int PreferredRefreshRate =>
            RefreshRateNumerator <= 0
                ? 0
                : Math.Max(0, (int)Math.Round((double)RefreshRateNumerator / RefreshRateDenominator, MidpointRounding.AwayFromZero));

        public DisplaySettingsSnapshot ToSnapshot(DisplayWindowMode windowMode)
        {
            return new DisplaySettingsSnapshot(
                Width,
                Height,
                windowMode,
                RefreshRateNumerator,
                RefreshRateDenominator);
        }

        public bool Equals(DisplayModeOption other)
        {
            return Width == other.Width &&
                   Height == other.Height &&
                   RefreshRateNumerator == other.RefreshRateNumerator &&
                   RefreshRateDenominator == other.RefreshRateDenominator;
        }

        public override bool Equals(object obj)
        {
            return obj is DisplayModeOption other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height, RefreshRateNumerator, RefreshRateDenominator);
        }
    }
}
