using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignProfileDocument
    {
        public int SchemaVersion;
        public string ProductVersion;
        public string SavedAtUtc;
        public string ProfileId;
        public int LastPlayedSlotNumber;
        public CampaignLegacyImportDocument LegacyImport = new();
        public CampaignSlotDocument[] Slots = Array.Empty<CampaignSlotDocument>();
    }
}
