using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignLegacyDeletedSlotGuardDocument
    {
        public int SlotNumber;
        public string ImportedSourceHash;
        public string DeletedAtUtc;
        public string Reason;
    }

    [Serializable]
    public sealed class CampaignLegacyImportDocument
    {
        public string ImportedSourceHash;
        public bool ImportDisabled;
        public string ResetTombstoneUtc;
        public CampaignLegacyDeletedSlotGuardDocument[] DeletedSlotGuards =
            Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
    }
}
