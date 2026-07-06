using System;

namespace Game.Feature.Stages
{
    public sealed class SaveSlotStoreCompatibilityAdapter
    {
        private readonly CampaignSaveService _campaignSaveService;

        public SaveSlotStoreCompatibilityAdapter(CampaignSaveService campaignSaveService)
        {
            _campaignSaveService = campaignSaveService ?? throw new ArgumentNullException(nameof(campaignSaveService));
            LastLoadReport = StageClearSaveLoadReport.Empty("Load has not run.");
        }

        public StageClearSaveLoadReport LastLoadReport { get; private set; }

        public SaveSlotData[] LoadAll()
        {
            var result = _campaignSaveService.GetSlots();
            if (!result.Succeeded)
            {
                LastLoadReport = new StageClearSaveLoadReport(
                    StageClearSavePayloadStatus.InvalidRejected,
                    result.Message,
                    string.Empty);
                return CreateEmptySlots();
            }

            LastLoadReport = new StageClearSaveLoadReport(
                StageClearSavePayloadStatus.Current,
                "Campaign profile loaded successfully.",
                CampaignSaveServiceResultStatusToken);
            return ToSaveSlotDataArray(result.Document);
        }

        public SaveSlotData LoadSlot(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            return LoadAll()[slotNumber - 1].Clone();
        }

        public void SaveSlot(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            SaveSlotStore.ThrowIfInvalidSlotNumber(slot.SlotNumber);
            ThrowIfFailed(_campaignSaveService.UpdateSlot(slot.SlotNumber, ToUpdate(slot)));
        }

        public SaveSlotData InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
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
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
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
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            var result = _campaignSaveService.DeleteSlot(slotNumber);
            if (result.Status == CampaignSaveCommandStatus.SlotNotFound)
            {
                return;
            }

            ThrowIfFailed(result);
        }

        public void ClearAll()
        {
            ThrowIfFailed(_campaignSaveService.ClearAll());
            LastLoadReport = StageClearSaveLoadReport.Empty("Campaign profile was cleared.");
        }

        private const string CampaignSaveServiceResultStatusToken = "CampaignProfileDocument";

        private static CampaignSlotUpdate ToUpdate(SaveSlotData slot)
        {
            return new CampaignSlotUpdate
            {
                StageId = slot.CurrentStageId.IsValid ? slot.CurrentStageId.Value : string.Empty,
                LevelGroupId = slot.CurrentLevelGroupId ?? string.Empty,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                IntroPlayed = slot.IntroPlayed,
                OutroPlayed = slot.OutroPlayed,
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = CampaignProfileDocumentMapper.ToStageClearProfileDocument(
                    slot.StageClearProfileSnapshot),
            };
        }

        private static SaveSlotData[] ToSaveSlotDataArray(CampaignProfileDocument document)
        {
            var slots = CreateEmptySlots();
            var documentSlots = document?.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < documentSlots.Length; i++)
            {
                var slot = documentSlots[i];
                if (slot == null || !SaveSlotStore.IsValidSlotNumber(slot.SlotNumber))
                {
                    continue;
                }

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
                    : SaveSlotStore.DefaultRemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                IntroPlayed = slot.IntroPlayed,
                OutroPlayed = slot.OutroPlayed,
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
            var slots = new SaveSlotData[SaveSlotStore.SlotCount];
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

        private static void ThrowIfFailed(CampaignSaveServiceResult result)
        {
            if (result == null)
            {
                throw new InvalidOperationException("Campaign save command did not return a result.");
            }

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Message);
            }
        }
    }
}
