using System;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host
{
    public interface ICampaignChancesReadSource
    {
        bool TryReadChances(out int remainingChances, out int maxChances);
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

        public bool TryReadChances(out int remainingChances, out int maxChances)
        {
            remainingChances = 0;
            maxChances = SaveSlotStore.DefaultRemainingChances;
            if (!_activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber))
            {
                return false;
            }

            var slot = _saveSlotStore.LoadSlot(activeSlotNumber);
            var normalizedRemainingChances = slot.RemainingChances <= 0
                ? SaveSlotStore.DefaultRemainingChances
                : slot.RemainingChances;
            remainingChances = Clamp(normalizedRemainingChances, 0, maxChances);
            return true;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
