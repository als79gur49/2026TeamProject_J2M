using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignLegacyImportDocument
    {
        public string ImportedSourceHash;
        public bool ImportDisabled;
        public string ResetTombstoneUtc;
    }
}
