using System;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host
{
    public interface ICampaignChancesReadSource
    {
        bool TryReadRemainingChances(out int remainingChances);
    }

    internal sealed class SaveSlotCampaignChancesReadSource : ICampaignChancesReadSource
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly SaveSlotStore _saveSlotStore;

        public SaveSlotCampaignChancesReadSource(
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
        }

        public bool TryReadRemainingChances(out int remainingChances)
        {
            remainingChances = 0;
            if (!_activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber))
            {
                return false;
            }

            var slot = _saveSlotStore.LoadSlot(activeSlotNumber);
            remainingChances = slot.RemainingChances <= 0
                ? SaveSlotStore.DefaultRemainingChances
                : slot.RemainingChances;
            return true;
        }
    }
}
