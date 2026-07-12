using System;

namespace Game.Feature.Stages
{
    public interface IPendingLaunchSlotProvider
    {
        bool TryGetPendingLaunchSlot(out int slotNumber);

        void SetPendingLaunchSlot(int slotNumber);

        void ClearPendingLaunchSlot();

        bool IsPendingLaunchSlot(int slotNumber);
    }

    public sealed class ActiveSlotProviderPendingLaunchAdapter : IPendingLaunchSlotProvider
    {
        private readonly ActiveSlotProvider _activeSlotProvider;

        public ActiveSlotProviderPendingLaunchAdapter(ActiveSlotProvider activeSlotProvider)
        {
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
        }

        public string DiagnosticsKey => _activeSlotProvider.DiagnosticsKey;

        public bool TryGetPendingLaunchSlot(out int slotNumber)
        {
            return _activeSlotProvider.TryGetActiveSlotNumber(out slotNumber);
        }

        public void SetPendingLaunchSlot(int slotNumber)
        {
            _activeSlotProvider.SetActiveSlot(slotNumber);
        }

        public void ClearPendingLaunchSlot()
        {
            _activeSlotProvider.ClearActiveSlot();
        }

        public bool IsPendingLaunchSlot(int slotNumber)
        {
            return SaveSlotStore.IsValidSlotNumber(slotNumber) &&
                _activeSlotProvider.TryGetActiveSlotNumber(out var pendingSlotNumber) &&
                pendingSlotNumber == slotNumber;
        }
    }

    public sealed class CampaignRunningSlotContext : IEquatable<CampaignRunningSlotContext>
    {
        public CampaignRunningSlotContext(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            SlotNumber = slotNumber;
        }

        public int SlotNumber { get; }

        public bool Equals(CampaignRunningSlotContext other)
        {
            return other != null && SlotNumber == other.SlotNumber;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CampaignRunningSlotContext);
        }

        public override int GetHashCode()
        {
            return SlotNumber;
        }

        public override string ToString()
        {
            return $"CampaignRunningSlotContext({SlotNumber})";
        }
    }
}
