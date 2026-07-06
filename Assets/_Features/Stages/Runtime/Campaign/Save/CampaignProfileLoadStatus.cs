namespace Game.Feature.Stages
{
    public enum CampaignProfileLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        BackupRecovered = 2,
        CorruptQuarantined = 3,
        CorruptNoFallback = 4,
        Unauthorized = 5,
        IoFailed = 6,
        SchemaInvalid = 7,
    }
}
