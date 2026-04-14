namespace Game.Feature.UI.Flow
{
    public readonly struct UIBlockSnapshot
    {
        public UIBlockSnapshot(
            bool blocksHudInteraction,
            bool blocksScreenInteraction,
            bool popupConsumesBack)
        {
            BlocksHudInteraction = blocksHudInteraction;
            BlocksScreenInteraction = blocksScreenInteraction;
            PopupConsumesBack = popupConsumesBack;
        }

        public bool BlocksHudInteraction { get; }

        public bool BlocksScreenInteraction { get; }

        public bool PopupConsumesBack { get; }
    }
}
