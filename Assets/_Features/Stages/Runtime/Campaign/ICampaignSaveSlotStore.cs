using System;

namespace Game.Feature.Stages
{
    public enum CampaignSaveLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        ImportedLegacy = 2,
        BackupRecovered = 3,
        CorruptRepairRequired = 4,
        SchemaInvalidRepairRequired = 5,
        IoFailed = 6,
        Unauthorized = 7,
    }

    public readonly struct CampaignSaveLoadReport
    {
        public CampaignSaveLoadReport(
            CampaignSaveLoadStatus status,
            string reason,
            string matchedToken)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            MatchedToken = matchedToken ?? string.Empty;
        }

        public CampaignSaveLoadStatus Status { get; }

        public string Reason { get; }

        public string MatchedToken { get; }

        public bool RequiresRepair =>
            Status == CampaignSaveLoadStatus.CorruptRepairRequired ||
            Status == CampaignSaveLoadStatus.SchemaInvalidRepairRequired;

        public static CampaignSaveLoadReport Missing(string reason)
        {
            return new CampaignSaveLoadReport(CampaignSaveLoadStatus.Missing, reason, string.Empty);
        }

        public static CampaignSaveLoadReport Loaded(string reason, string matchedToken)
        {
            return new CampaignSaveLoadReport(CampaignSaveLoadStatus.Loaded, reason, matchedToken);
        }
    }

    public readonly struct CampaignSaveLoadResult
    {
        public CampaignSaveLoadResult(
            SaveSlotData[] slots,
            CampaignSaveLoadReport report)
        {
            Slots = slots ?? Array.Empty<SaveSlotData>();
            Report = report;
        }

        public SaveSlotData[] Slots { get; }

        public CampaignSaveLoadReport Report { get; }
    }

    public interface ICampaignSaveSlotStore
    {
        string DiagnosticsKey { get; }

        CampaignSaveLoadReport LastCampaignLoadReport { get; }

        SaveSlotData[] LoadAll();

        CampaignSaveLoadResult LoadAllWithReport();

        SaveSlotData LoadSlot(int slotNumber);

        void SaveSlot(SaveSlotData slot);

        SaveSlotData InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt);

        void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation);

        void DeleteSlot(int slotNumber);

        void ClearAll();
    }
}
