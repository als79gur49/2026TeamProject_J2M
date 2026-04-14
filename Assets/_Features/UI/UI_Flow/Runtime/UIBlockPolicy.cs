namespace Game.Feature.UI.Flow
{
    public sealed class UIBlockPolicy
    {
        public UIBlockSnapshot Evaluate(ScreenId currentScreenId, PopupEntry? topPopup, int popupCount)
        {
            var popupConsumesBack = popupCount > 0;
            var blocksScreenInteraction = topPopup.HasValue && topPopup.Value.IsModal;
            var blocksHudInteraction = currentScreenId != ScreenId.Gameplay || blocksScreenInteraction;

            return new UIBlockSnapshot(
                blocksHudInteraction,
                blocksScreenInteraction,
                popupConsumesBack);
        }
    }
}
