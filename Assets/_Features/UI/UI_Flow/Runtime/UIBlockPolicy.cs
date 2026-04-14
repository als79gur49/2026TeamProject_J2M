namespace Game.Feature.UI.Flow
{
    public readonly struct UIFlowStateSnapshot
    {
        public UIFlowStateSnapshot(ScreenId currentScreenId, PopupEntry? topPopup, int popupCount)
        {
            CurrentScreenId = currentScreenId;
            TopPopup = topPopup;
            PopupCount = popupCount;
        }

        public ScreenId CurrentScreenId { get; }

        public PopupEntry? TopPopup { get; }

        public int PopupCount { get; }
    }

    public sealed class UIBlockPolicy
    {
        public UIBlockSnapshot Evaluate(UIFlowStateSnapshot flowState)
        {
            var popupConsumesBack = flowState.PopupCount > 0;
            var blocksScreenInteraction = BlocksScreenInteraction(flowState.TopPopup);
            var blocksHudInteraction = BlocksHudInteraction(flowState.CurrentScreenId, blocksScreenInteraction);

            return new UIBlockSnapshot(
                blocksHudInteraction,
                blocksScreenInteraction,
                popupConsumesBack);
        }

        private static bool BlocksHudInteraction(ScreenId currentScreenId, bool blocksScreenInteraction)
        {
            return currentScreenId != ScreenId.Gameplay || blocksScreenInteraction;
        }

        private static bool BlocksScreenInteraction(PopupEntry? topPopup)
        {
            return topPopup.HasValue && topPopup.Value.IsModal;
        }
    }
}
