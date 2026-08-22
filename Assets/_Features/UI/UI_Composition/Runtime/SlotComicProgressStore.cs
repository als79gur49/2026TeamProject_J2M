using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    public sealed class SlotComicProgressStore
    {
        private readonly ICampaignSaveSlotStore _saveSlotStore;

        public SlotComicProgressStore(ICampaignSaveSlotStore saveSlotStore)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
        }

        public bool IsIntroComicCompleted(int slotNumber)
        {
            return LoadSlot(slotNumber).IntroComicCompleted;
        }

        public bool IsOutroComicCompleted(int slotNumber)
        {
            return LoadSlot(slotNumber).OutroComicCompleted;
        }

        public void MarkIntroComicCompleted(int slotNumber)
        {
            _saveSlotStore.UpdateSlot(slotNumber, slot => slot.IntroComicCompleted = true);
        }

        public void MarkOutroComicCompleted(int slotNumber)
        {
            _saveSlotStore.UpdateSlot(slotNumber, slot => slot.OutroComicCompleted = true);
        }

        private SaveSlotData LoadSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            return _saveSlotStore.LoadSlot(slotNumber);
        }
    }
}
