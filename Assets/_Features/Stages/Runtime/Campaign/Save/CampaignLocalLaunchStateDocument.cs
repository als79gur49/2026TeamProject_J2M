using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignLocalLaunchStateDocument
    {
        public int schemaVersion = CampaignLocalLaunchStateRepository.SchemaVersion;
        public CampaignLocalLaunchStateCampaignDocument campaign = new();
    }

    [Serializable]
    public sealed class CampaignLocalLaunchStateCampaignDocument
    {
        public int activeSlotNumber;
        public string lastUpdatedUtc = string.Empty;
    }

    public enum CampaignLocalLaunchStateLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        CorruptNoFallback = 2,
        SchemaInvalid = 3,
        Unauthorized = 4,
        IoFailed = 5,
    }

    public readonly struct CampaignLocalLaunchStateLoadResult
    {
        public CampaignLocalLaunchStateLoadResult(
            CampaignLocalLaunchStateLoadStatus status,
            CampaignLocalLaunchStateDocument document,
            string message)
        {
            Status = status;
            Document = document;
            Message = message ?? string.Empty;
        }

        public CampaignLocalLaunchStateLoadStatus Status { get; }

        public CampaignLocalLaunchStateDocument Document { get; }

        public string Message { get; }

        public bool HasDocument => Document != null;
    }

    public interface ICampaignLocalLaunchStateRepository
    {
        CampaignLocalLaunchStateLoadResult Load();

        void SaveActiveSlot(int slotNumber);

        void ClearActiveSlot();
    }
}
