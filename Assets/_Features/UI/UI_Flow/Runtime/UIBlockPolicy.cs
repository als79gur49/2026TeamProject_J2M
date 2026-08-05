namespace Game.Feature.UI.Flow
{
    public readonly struct UIFlowStateSnapshot
    {
        public UIFlowStateSnapshot(
            ScreenEntry? currentScreen,
            PopupEntry? topPopup,
            int popupCount,
            bool terminalSessionActive = false)
        {
            CurrentScreen = currentScreen;
            TopPopup = topPopup;
            PopupCount = popupCount;
            TerminalSessionActive = terminalSessionActive;
        }

        public ScreenEntry? CurrentScreen { get; }

        public PopupEntry? TopPopup { get; }

        public int PopupCount { get; }

        public bool TerminalSessionActive { get; }
    }

    public sealed class UIBlockPolicy
    {
        public UIBlockSnapshot Evaluate(UIFlowStateSnapshot flowState)
        {
            var popupConsumesBack = flowState.PopupCount > 0;
            var blocksScreenInteraction =
                flowState.TerminalSessionActive ||
                BlocksScreenInteraction(flowState.TopPopup);
            var blocksUiGameplayInput =
                flowState.TerminalSessionActive ||
                BlocksUiGameplayInput(flowState.CurrentScreen, blocksScreenInteraction);
            var blocksHudInteraction = blocksUiGameplayInput;
            var showsPopupDim = flowState.TopPopup.HasValue && flowState.TopPopup.Value.Policy.ShowsDim;
            var blocksLowerLayerPointer = flowState.TerminalSessionActive ||
                                          (flowState.TopPopup.HasValue &&
                                          (flowState.TopPopup.Value.Policy.BlocksLowerLayers ||
                                           flowState.TopPopup.Value.Policy.BackdropMode != Game.Feature.UI.Popups.PopupBackdropMode.None));
            var backdropMode = flowState.TopPopup.HasValue
                ? flowState.TopPopup.Value.Policy.BackdropMode
                : Game.Feature.UI.Popups.PopupBackdropMode.None;

            return new UIBlockSnapshot(
                blocksHudInteraction,
                blocksScreenInteraction,
                blocksUiGameplayInput,
                popupConsumesBack,
                showsPopupDim,
                blocksLowerLayerPointer,
                backdropMode);
        }

        private static bool BlocksUiGameplayInput(ScreenEntry? currentScreen, bool blocksScreenInteraction)
        {
            return blocksScreenInteraction ||
                   (currentScreen.HasValue && currentScreen.Value.Policy.BlocksUiGameplayInput);
        }

        private static bool BlocksScreenInteraction(PopupEntry? topPopup)
        {
            return topPopup.HasValue && topPopup.Value.Policy.BlocksLowerLayers;
        }
    }
}
