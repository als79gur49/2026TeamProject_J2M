using System;

namespace Game.Feature.Stages
{
    public sealed class CampaignSaveSlotStoreAdapter : ICampaignSaveSlotStore
    {
        private readonly CampaignSaveService _campaignSaveService;
        private readonly ICampaignSaveRecoveryPort _recoveryPort;

        public CampaignSaveSlotStoreAdapter(
            CampaignSaveService campaignSaveService,
            ICampaignSaveRecoveryPort recoveryPort = null)
        {
            _campaignSaveService = campaignSaveService ?? throw new ArgumentNullException(nameof(campaignSaveService));
            _recoveryPort = recoveryPort;
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Load has not run.");
        }

        public string DiagnosticsKey => CampaignSaveServiceResultStatusToken;

        public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; }

        public SaveSlotData[] LoadAll()
        {
            var result = LoadAllWithReport();
            ThrowIfCampaignAccessBlocked(result.Report);
            return result.Slots;
        }

        public CampaignSaveLoadResult LoadAllWithReport()
        {
            if (IsRecoveryPending())
            {
                LastCampaignLoadReport = CreateRecoveryPendingReport();
                return new CampaignSaveLoadResult(CreateEmptySlots(), LastCampaignLoadReport);
            }

            var result = _campaignSaveService.GetSlots();
            if (!result.Succeeded)
            {
                LastCampaignLoadReport = ToCampaignLoadReport(result);
                return new CampaignSaveLoadResult(CreateEmptySlots(), LastCampaignLoadReport);
            }

            LastCampaignLoadReport = result.HasProfileLoadStatus
                ? ToCampaignLoadReport(result)
                : CampaignSaveLoadReport.Loaded(
                    "Campaign profile loaded successfully.",
                    CampaignSaveServiceResultStatusToken);
            return new CampaignSaveLoadResult(
                CampaignProfileDocumentMapper.ToDomainSlots(result.Document),
                LastCampaignLoadReport);
        }

        public SaveSlotData LoadSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            return LoadAll()[slotNumber - 1].Clone();
        }

        public void SaveSlot(SaveSlotData slot)
        {
            ThrowIfRecoveryPending();
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slot.SlotNumber);
            if (slot.IsEmpty)
            {
                DeleteSlot(slot.SlotNumber);
                return;
            }

            ThrowIfFailed(_campaignSaveService.UpdateSlot(
                slot.SlotNumber,
                CampaignProfileDocumentMapper.ToFullReplacementUpdate(slot)));
        }

        public SaveSlotData InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (sequenceResolver == null)
            {
                throw new ArgumentNullException(nameof(sequenceResolver));
            }

            var firstStageId = sequenceResolver.FirstStageId;
            ThrowIfFailed(_campaignSaveService.InitializeNewGame(new CampaignNewGameRequest
            {
                SlotNumber = slotNumber,
                InitialStageId = firstStageId.Value,
                InitialLevelGroupId = sequenceResolver.GetLevelGroupId(firstStageId),
                LastPlayedAtUtc = lastPlayedAt ?? string.Empty,
            }));

            return LoadSlot(slotNumber);
        }

        public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            var slot = LoadSlot(slotNumber);
            mutation(slot);
            slot.SlotNumber = slotNumber;
            SaveSlot(slot);
        }

        public void DeleteSlot(int slotNumber)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var result = _campaignSaveService.DeleteSlot(slotNumber);
            if (result.Status == CampaignSaveCommandStatus.SlotNotFound)
            {
                return;
            }

            ThrowIfFailed(result);
        }

        public void ClearAll()
        {
            ThrowIfRecoveryPending();
            ThrowIfFailed(_campaignSaveService.ClearAll());
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Campaign profile was cleared.");
        }

        private const string CampaignSaveServiceResultStatusToken = "CampaignProfileDocument";

        private bool IsRecoveryPending()
        {
            return _recoveryPort?.HasPendingReset == true;
        }

        private void ThrowIfRecoveryPending()
        {
            if (IsRecoveryPending())
            {
                LastCampaignLoadReport = CreateRecoveryPendingReport();
                ThrowIfCampaignAccessBlocked(LastCampaignLoadReport);
            }
        }

        private static CampaignSaveLoadReport CreateRecoveryPendingReport()
        {
            return new CampaignSaveLoadReport(
                CampaignSaveLoadStatus.RecoveryPending,
                "Campaign save reset is pending.",
                CampaignSaveServiceResultStatusToken);
        }

        private static void ThrowIfCampaignAccessBlocked(CampaignSaveLoadReport report)
        {
            if (!report.BlocksCampaignAccess)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Campaign save access is blocked ({report.Status}): {report.Reason}");
        }

        private static CampaignSaveLoadReport ToCampaignLoadReport(CampaignSaveServiceResult result)
        {
            if (result == null)
            {
                return new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.IoFailed,
                    "Campaign save command did not return a result.",
                    string.Empty);
            }

            if (!result.HasProfileLoadStatus)
            {
                return new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.IoFailed,
                    result.Message,
                    string.Empty);
            }

            switch (result.ProfileLoadStatus)
            {
                case CampaignProfileLoadStatus.Missing:
                    return CampaignSaveLoadReport.Missing(result.Message);
                case CampaignProfileLoadStatus.Loaded:
                    return CampaignSaveLoadReport.Loaded(result.Message, CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.BackupRecovered:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.BackupRecovered,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.CorruptNoFallback:
                case CampaignProfileLoadStatus.InvalidDocument:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.CorruptRepairRequired,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.UnsupportedVersion:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.SchemaInvalidRepairRequired,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.Unauthorized:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.Unauthorized,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.IoFailed:
                default:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.IoFailed,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
            }
        }

        private static SaveSlotData[] CreateEmptySlots()
        {
            var slots = new SaveSlotData[CampaignSaveSlotPolicy.SlotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = SaveSlotData.CreateEmpty(i + 1);
            }

            return slots;
        }

        private void ThrowIfFailed(CampaignSaveServiceResult result)
        {
            if (result == null)
            {
                LastCampaignLoadReport = ToCampaignLoadReport(null);
                throw new InvalidOperationException("Campaign save command did not return a result.");
            }

            if (!result.Succeeded)
            {
                if (result.Status == CampaignSaveCommandStatus.LoadFailed)
                {
                    LastCampaignLoadReport = ToCampaignLoadReport(result);
                }

                throw new InvalidOperationException(result.Message);
            }
        }
    }
}
