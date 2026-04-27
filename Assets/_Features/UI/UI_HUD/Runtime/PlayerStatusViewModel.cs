using System;

namespace Game.Feature.UI.HUD
{
    public sealed class PlayerStatusViewModel
    {
        public event Action Changed;

        public int CurrentHp { get; private set; }

        public int MaxHp { get; private set; }

        public float HpNormalized { get; private set; } = 1f;

        public string FacingText { get; private set; } = string.Empty;

        public string ActionText { get; private set; } = string.Empty;

        public string TopologyText { get; private set; } = string.Empty;

        public string StatusText { get; private set; } = string.Empty;

        public string DamageText { get; private set; } = string.Empty;

        public bool HasRemainingChances { get; private set; }

        public int RemainingChances { get; private set; }

        public string ChancesText { get; private set; } = string.Empty;

        public void SetState(
            int currentHp,
            int maxHp,
            string facingText,
            string actionText,
            string topologyText,
            string statusText,
            string damageText,
            bool hasRemainingChances = false,
            int remainingChances = 0)
        {
            CurrentHp = currentHp;
            MaxHp = maxHp > 0 ? maxHp : currentHp;
            HpNormalized = MaxHp > 0
                ? Clamp01((float)CurrentHp / MaxHp)
                : 0f;
            FacingText = facingText ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            TopologyText = topologyText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            DamageText = damageText ?? string.Empty;
            HasRemainingChances = hasRemainingChances;
            RemainingChances = remainingChances;
            ChancesText = hasRemainingChances ? $"Chances: {remainingChances}" : string.Empty;
            Changed?.Invoke();
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
