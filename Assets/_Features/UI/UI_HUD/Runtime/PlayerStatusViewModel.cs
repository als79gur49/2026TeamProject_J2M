using System;

namespace Game.Feature.UI.HUD
{
    public sealed class PlayerStatusViewModel
    {
        public event Action Changed;

        public int CurrentHp { get; private set; }

        public string FacingText { get; private set; } = string.Empty;

        public string ActionText { get; private set; } = string.Empty;

        public string TopologyText { get; private set; } = string.Empty;

        public string StatusText { get; private set; } = string.Empty;

        public string DamageText { get; private set; } = string.Empty;

        public void SetState(
            int currentHp,
            string facingText,
            string actionText,
            string topologyText,
            string statusText,
            string damageText)
        {
            CurrentHp = currentHp;
            FacingText = facingText ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            TopologyText = topologyText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            DamageText = damageText ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
