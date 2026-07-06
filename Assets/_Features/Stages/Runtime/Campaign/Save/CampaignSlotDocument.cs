using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignSlotDocument
    {
        public int SlotNumber;
        public string StageId;
        public string LevelGroupId;
        public int RemainingChances;
        public bool CampaignCompleted;
        public string LastPlayedAtUtc;
    }
}
