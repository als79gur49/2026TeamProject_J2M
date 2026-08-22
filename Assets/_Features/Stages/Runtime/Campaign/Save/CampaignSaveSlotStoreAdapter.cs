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
            return new CampaignSaveLoadResult(ToSaveSlotDataArray(result.Document), LastCampaignLoadReport);
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

            ThrowIfFailed(_campaignSaveService.UpdateSlot(slot.SlotNumber, ToUpdate(slot)));
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

        private static CampaignSlotUpdate ToUpdate(SaveSlotData slot)
        {
            return new CampaignSlotUpdate
            {
                StageId = slot.CurrentStageId.IsValid ? slot.CurrentStageId.Value : string.Empty,
                LevelGroupId = slot.CurrentLevelGroupId ?? string.Empty,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                ReplaceNormalCampaignCompletionReceipt = true,
                HasNormalCampaignCompletionReceipt =
                    slot.HasNormalCampaignCompletionReceipt ||
                    slot.NormalCampaignCompletionReceipt != null,
                NormalCampaignCompletionReceipt = CampaignProfileDocumentMapper.ToReceiptDocument(
                    slot.NormalCampaignCompletionReceipt),
                IntroComicCompleted = slot.IntroComicCompleted,
                OutroComicCompleted = slot.OutroComicCompleted,
                NormalStagePerformanceRecords =
                    CampaignProfileDocumentMapper.ToPerformanceRecordDocuments(
                        slot.NormalStagePerformanceRecords),
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = CampaignProfileDocumentMapper.ToStageClearProfileDocument(
                    slot.StageClearProfileSnapshot),
            };
        }

        private static SaveSlotData[] ToSaveSlotDataArray(CampaignProfileDocument document)
        {
            var slots = CreateEmptySlots();
            var seenSlotNumbers = new bool[CampaignSaveSlotPolicy.SlotCount];
            var documentSlots = document?.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < documentSlots.Length; i++)
            {
                var slot = documentSlots[i];
                if (slot == null || !CampaignSaveSlotPolicy.IsValidSlotNumber(slot.SlotNumber))
                {
                    throw new InvalidOperationException(
                        "Campaign profile repository returned an invalid slot document.");
                }

                if (seenSlotNumbers[slot.SlotNumber - 1])
                {
                    throw new InvalidOperationException(
                        $"Campaign profile repository returned duplicate slot '{slot.SlotNumber}'.");
                }

                seenSlotNumbers[slot.SlotNumber - 1] = true;
                slots[slot.SlotNumber - 1] = ToSaveSlotData(slot);
            }

            return slots;
        }

        private static SaveSlotData ToSaveSlotData(CampaignSlotDocument slot)
        {
            var stageId = StageId.TryCreate(slot.StageId, out var parsedStageId)
                ? parsedStageId
                : StageId.None;
            return new SaveSlotData
            {
                SlotNumber = slot.SlotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = slot.LevelGroupId ?? string.Empty,
                RemainingChances = slot.RemainingChances > 0
                    ? slot.RemainingChances
                    : CampaignSaveSlotPolicy.DefaultRemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    slot.HasNormalCampaignCompletionReceipt,
                NormalCampaignCompletionReceipt = CampaignProfileDocumentMapper.ToReceipt(
                    slot.NormalCampaignCompletionReceipt),
                IntroComicCompleted = slot.IntroComicCompleted,
                OutroComicCompleted = slot.OutroComicCompleted,
                NormalStagePerformanceRecords =
                    CampaignProfileDocumentMapper.ToPerformanceRecords(
                        slot.NormalStagePerformanceRecords),
                TotalDeaths = Math.Max(0, slot.TotalDeaths),
                LastPlayedAt = slot.LastPlayedAtUtc ?? string.Empty,
                StageClearProfileSnapshot = ToStageClearProfileSnapshot(slot.StageClearProfileSnapshot),
            };
        }

        private static StageClearProfileSnapshot ToStageClearProfileSnapshot(
            CampaignStageClearProfileDocument document)
        {
            var snapshot = new StageClearProfileSnapshot();
            if (document == null)
            {
                return snapshot;
            }

            snapshot.Version = Math.Max(0, document.Version);
            var records = document.Records ?? Array.Empty<PlayerStageClearRecordDocument>();
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (record == null || !StageId.TryCreate(record.StageId, out var stageId))
                {
                    continue;
                }

                snapshot.ClearRecordsByStageId[stageId] = new PlayerStageClearRecord
                {
                    StageId = stageId,
                    HasAttempted = record.HasAttempted,
                    HasCleared = record.HasCleared,
                    ClearCount = Math.Max(0, record.ClearCount),
                    ProcessedStageRunIds = CloneArray(record.ProcessedStageRunIds),
                };
            }

            snapshot.ProcessedStageRunIds = new System.Collections.Generic.HashSet<string>(
                document.ProcessedStageRunIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            snapshot.ProcessedClearAttemptIds = new System.Collections.Generic.HashSet<string>(
                document.ProcessedClearAttemptIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            return snapshot;
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

        private static string[] CloneArray(string[] values)
        {
            return (string[])(values ?? Array.Empty<string>()).Clone();
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
