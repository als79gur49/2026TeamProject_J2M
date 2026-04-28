using System;

namespace Game.Feature.UI.HUD
{
    public sealed class PlayerStatusViewModel
    {
        public event Action Changed;

        public string FacingText { get; private set; } = string.Empty;

        public string TopologyText { get; private set; } = string.Empty;

        public bool HasRemainingChances { get; private set; }

        public int RemainingChances { get; private set; }

        public int MaxChances { get; private set; }

        public void SetState(
            string facingText,
            string topologyText,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0)
        {
            FacingText = facingText ?? string.Empty;
            TopologyText = topologyText ?? string.Empty;
            MaxChances = ResolveMaxChances(hasRemainingChances, remainingChances, maxChances);
            HasRemainingChances = hasRemainingChances && MaxChances > 0;
            RemainingChances = HasRemainingChances
                ? Clamp(remainingChances, 0, MaxChances)
                : 0;
            Changed?.Invoke();
        }

        private static int ResolveMaxChances(bool hasRemainingChances, int remainingChances, int maxChances)
        {
            if (!hasRemainingChances)
            {
                return 0;
            }

            if (maxChances > 0)
            {
                return maxChances;
            }

            return remainingChances > 0 ? remainingChances : 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
