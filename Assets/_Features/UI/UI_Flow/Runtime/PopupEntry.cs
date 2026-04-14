namespace Game.Feature.UI.Flow
{
    public readonly struct PopupEntry
    {
        public PopupEntry(PopupId popupId, bool isModal)
        {
            PopupId = popupId;
            IsModal = isModal;
        }

        public PopupId PopupId { get; }

        public bool IsModal { get; }
    }
}
