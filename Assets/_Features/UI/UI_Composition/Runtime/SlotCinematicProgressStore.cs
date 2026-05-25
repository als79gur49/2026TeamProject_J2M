using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    public sealed class SlotCinematicProgressStore
    {
        private readonly SaveSlotStore _saveSlotStore;

        public SlotCinematicProgressStore(SaveSlotStore saveSlotStore)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
        }

        public bool IsIntroPlayed(int slotNumber)
        {
            return LoadSlot(slotNumber).IntroPlayed;
        }

        public bool IsOutroPlayed(int slotNumber)
        {
            return LoadSlot(slotNumber).OutroPlayed;
        }

        public void MarkIntroPlayed(int slotNumber)
        {
            _saveSlotStore.UpdateSlot(slotNumber, slot => slot.IntroPlayed = true);
        }

        public void MarkOutroPlayed(int slotNumber)
        {
            _saveSlotStore.UpdateSlot(slotNumber, slot => slot.OutroPlayed = true);
        }

        private SaveSlotData LoadSlot(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            return _saveSlotStore.LoadSlot(slotNumber);
        }
    }
}
