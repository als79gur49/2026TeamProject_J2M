using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Flow
{
    public readonly struct UIBlockSnapshot
    {
        public UIBlockSnapshot(
            bool blocksHudInteraction,
            bool blocksScreenInteraction,
            bool blocksUiGameplayInput,
            bool popupConsumesBack,
            bool showsPopupDim,
            bool blocksLowerLayerPointer,
            PopupBackdropMode popupBackdropMode)
        {
            BlocksHudInteraction = blocksHudInteraction;
            BlocksScreenInteraction = blocksScreenInteraction;
            BlocksUiGameplayInput = blocksUiGameplayInput;
            PopupConsumesBack = popupConsumesBack;
            ShowsPopupDim = showsPopupDim;
            BlocksLowerLayerPointer = blocksLowerLayerPointer;
            PopupBackdropMode = popupBackdropMode;
        }

        public bool BlocksHudInteraction { get; }

        public bool BlocksScreenInteraction { get; }

        public bool BlocksUiGameplayInput { get; }

        public bool PopupConsumesBack { get; }

        public bool ShowsPopupDim { get; }

        public bool BlocksLowerLayerPointer { get; }

        public PopupBackdropMode PopupBackdropMode { get; }
    }
}
