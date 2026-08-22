namespace Game.Feature.Stages
{
    public enum CampaignProfileLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        BackupRecovered = 2,
        CorruptNoFallback = 3,
        Unauthorized = 4,
        IoFailed = 5,
        UnsupportedVersion = 6,
        InvalidDocument = 7,
    }
}
