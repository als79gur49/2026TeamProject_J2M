using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    public sealed class SlotComicProgressStore
    {
        private readonly ICampaignSaveQuery _saveSlotStore;
        private readonly ICampaignComicProgressPort _comicProgressPort;

        public SlotComicProgressStore(
            ICampaignSaveQuery saveSlotStore,
            ICampaignComicProgressPort comicProgressPort)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _comicProgressPort = comicProgressPort ??
                throw new ArgumentNullException(nameof(comicProgressPort));
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
            LoadSlot(slotNumber);
            _comicProgressPort.MarkIntroComicCompleted(slotNumber);
        }

        public void MarkOutroComicCompleted(int slotNumber)
        {
            LoadSlot(slotNumber);
            _comicProgressPort.MarkOutroComicCompleted(slotNumber);
        }

        private CampaignSlotState LoadSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var entry = _saveSlotStore.LoadSlot(slotNumber);
            if (entry == null || entry.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Campaign slot '{slotNumber}' is empty or missing.");
            }

            return entry.State;
        }
    }
}
