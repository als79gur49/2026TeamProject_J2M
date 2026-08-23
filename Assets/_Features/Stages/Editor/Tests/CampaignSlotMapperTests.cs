using System;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSlotMapperTests
    {
        private const string AdapterPath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveSlotStoreAdapter.cs";

        [Test]
        public void SlotMapper_DomainDocumentDomainRoundTripPreservesAllFields()
        {
            var expected = CreatePopulatedSlot(2, "stage-2-1");

            var document = CampaignSlotMapper.ToDocument(expected);
            var actual = CampaignSlotMapper.ToDomain(document);

            Assert.That(actual.SlotNumber, Is.EqualTo(expected.SlotNumber));
            Assert.That(actual.CurrentStageId, Is.EqualTo(expected.CurrentStageId));
            Assert.That(actual.CurrentLevelGroupId, Is.EqualTo(expected.CurrentLevelGroupId));
            Assert.That(actual.RemainingChances, Is.EqualTo(expected.RemainingChances));
            Assert.That(actual.CampaignCompleted, Is.EqualTo(expected.CampaignCompleted));
            Assert.That(actual.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(actual.NormalCampaignCompletionReceipt.Version,
                Is.EqualTo(expected.NormalCampaignCompletionReceipt.Version));
            Assert.That(actual.IntroComicCompleted, Is.EqualTo(expected.IntroComicCompleted));
            Assert.That(actual.OutroComicCompleted, Is.EqualTo(expected.OutroComicCompleted));
            Assert.That(actual.TotalDeaths, Is.EqualTo(expected.TotalDeaths));
            Assert.That(actual.LastPlayedAt, Is.EqualTo(expected.LastPlayedAt));
            Assert.That(actual.NormalStagePerformanceRecords, Has.Length.EqualTo(1));
            Assert.That(actual.NormalStagePerformanceRecords[0].StageId,
                Is.EqualTo(expected.CurrentStageId));
            Assert.That(actual.StageClearProfileSnapshot.Version, Is.EqualTo(2));
            Assert.That(actual.StageClearProfileSnapshot.ClearRecordsByStageId,
                Does.ContainKey(expected.CurrentStageId));
            Assert.That(actual.StageClearProfileSnapshot.ProcessedStageRunIds,
                Does.Contain("run-2"));
            Assert.That(actual.StageClearProfileSnapshot.ProcessedClearAttemptIds,
                Does.Contain("attempt-2"));
        }

        [Test]
        public void ProfileMapper_OmitsEmptySlotsSortsDocumentsAndProducesValidContract()
        {
            var document = CampaignProfileDocumentMapper.ToDocument(
                new SaveSlotData[]
                {
                    CreatePopulatedSlot(3, "stage-3-1"),
                    SaveSlotData.CreateEmpty(2),
                    null,
                    CreatePopulatedSlot(1, "stage-1-1"),
                },
                "profile-mapper",
                3,
                "2026-08-23T00:00:00Z",
                "tests");

            Assert.That(document.Slots, Has.Length.EqualTo(2));
            Assert.That(document.Slots[0].SlotNumber, Is.EqualTo(1));
            Assert.That(document.Slots[1].SlotNumber, Is.EqualTo(3));
            Assert.That(
                CampaignProfileDocumentValidator.Validate(document),
                Is.EqualTo(CampaignProfileDocumentValidationResult.Valid));
        }

        [Test]
        public void ProfileMapper_MissingDocumentsRestoreAsFixedEmptyDomainSlots()
        {
            var document = CampaignProfileDocumentMapper.ToDocument(
                new[] { CreatePopulatedSlot(2, "stage-2-1") },
                "profile-domain",
                2,
                string.Empty,
                "tests");

            var slots = CampaignProfileDocumentMapper.ToDomainSlots(document);

            Assert.That(slots, Has.Length.EqualTo(CampaignSaveSlotPolicy.SlotCount));
            Assert.That(slots[0].SlotNumber, Is.EqualTo(1));
            Assert.That(slots[0].IsEmpty, Is.True);
            Assert.That(slots[1].CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(slots[2].SlotNumber, Is.EqualTo(3));
            Assert.That(slots[2].IsEmpty, Is.True);
        }

        [Test]
        public void SlotMapper_ZeroPersistedChancesUseSharedRuntimeDefaultWithoutChangingDocument()
        {
            var document = CampaignSlotMapper.ToDocument(CreatePopulatedSlot(1, "stage-1-1"));
            document.RemainingChances = 0;

            var slot = CampaignSlotMapper.ToDomain(document);

            Assert.That(document.RemainingChances, Is.Zero);
            Assert.That(slot.RemainingChances,
                Is.EqualTo(CampaignSaveSlotPolicy.DefaultRemainingChances));
            Assert.That(CampaignSaveSlotPolicy.ToRuntimeRemainingChances(0),
                Is.EqualTo(CampaignSaveSlotPolicy.DefaultRemainingChances));
        }

        [Test]
        public void SlotMapper_FullReplacementUpdateUsesDocumentMapping()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            var document = CampaignSlotMapper.ToDocument(slot);

            var update = CampaignSlotMapper.ToFullReplacementUpdate(slot);

            Assert.That(update.StageId, Is.EqualTo(document.StageId));
            Assert.That(update.LevelGroupId, Is.EqualTo(document.LevelGroupId));
            Assert.That(update.RemainingChances, Is.EqualTo(document.RemainingChances));
            Assert.That(update.CampaignCompleted, Is.EqualTo(document.CampaignCompleted));
            Assert.That(update.ReplaceNormalCampaignCompletionReceipt, Is.True);
            Assert.That(update.HasNormalCampaignCompletionReceipt,
                Is.EqualTo(document.HasNormalCampaignCompletionReceipt));
            Assert.That(update.NormalStagePerformanceRecords, Has.Length.EqualTo(
                document.NormalStagePerformanceRecords.Length));
            Assert.That(update.NormalStagePerformanceRecords[0].StageId,
                Is.EqualTo(document.NormalStagePerformanceRecords[0].StageId));
            Assert.That(update.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(document.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses));
            Assert.That(update.StageClearProfileSnapshot.Version,
                Is.EqualTo(document.StageClearProfileSnapshot.Version));
            Assert.That(update.StageClearProfileSnapshot.Records, Has.Length.EqualTo(
                document.StageClearProfileSnapshot.Records.Length));
        }

        [Test]
        public void SlotMapper_RejectsEmptyDomainSlot()
        {
            Assert.Throws<ArgumentException>(() =>
                CampaignSlotMapper.ToDocument(SaveSlotData.CreateEmpty(1)));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsNegativeStageClearProfileVersion()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot.Version = -1;

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsNegativeStageClearCount()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot
                .ClearRecordsByStageId[slot.CurrentStageId]
                .ClearCount = -1;

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsDuplicateProcessedStageRunIds()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot
                .ClearRecordsByStageId[slot.CurrentStageId]
                .ProcessedStageRunIds = new[] { "run-2", "run-2" };

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsBlankProcessedClearAttemptId()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds.Add(" ");

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDomainRejectsInvalidNestedDocument()
        {
            var document = CampaignSlotMapper.ToDocument(
                CreatePopulatedSlot(1, "stage-1-1"));
            document.StageClearProfileSnapshot.Version = -1;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignSlotMapper.ToDomain(document));
        }

        [Test]
        public void SlotMapper_PresentNullCompletionReceiptRoundTripsUnchanged()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.HasNormalCampaignCompletionReceipt = true;
            slot.NormalCampaignCompletionReceipt = null;

            var document = CampaignSlotMapper.ToDocument(slot);
            var roundTripped = CampaignSlotMapper.ToDomain(document);

            Assert.That(document.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(document.NormalCampaignCompletionReceipt, Is.Null);
            Assert.That(roundTripped.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(roundTripped.NormalCampaignCompletionReceipt, Is.Null);
        }

        [Test]
        public void ProfileMapper_RejectsLastPlayedSlotThatIsNotPersisted()
        {
            Assert.Throws<ArgumentException>(() => CampaignProfileDocumentMapper.ToDocument(
                new[] { CreatePopulatedSlot(1, "stage-1-1") },
                "profile-invalid-last-played",
                2,
                string.Empty,
                "tests"));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsUnsupportedSchemaVersion()
        {
            var document = CreateValidProfileDocument();
            document.SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion + 1;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignProfileDocumentMapper.ToDomainSlots(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsBlankProfileId()
        {
            var document = CreateValidProfileDocument();
            document.ProfileId = " ";

            Assert.Throws<InvalidOperationException>(() =>
                CampaignProfileDocumentMapper.ToDomainSlots(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsMissingLastPlayedSlot()
        {
            var document = CreateValidProfileDocument();
            document.LastPlayedSlotNumber = 2;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignProfileDocumentMapper.ToDomainSlots(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsDuplicateSlotDocuments()
        {
            var document = CreateValidProfileDocument();
            document.Slots = new[] { document.Slots[0], document.Slots[0] };

            Assert.Throws<InvalidOperationException>(() =>
                CampaignProfileDocumentMapper.ToDomainSlots(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsOutOfRangeSlotDocument()
        {
            var document = CreateValidProfileDocument();
            document.Slots[0].SlotNumber = CampaignSaveSlotPolicy.SlotCount + 1;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignProfileDocumentMapper.ToDomainSlots(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsInvalidNestedSlotDocument()
        {
            var document = CreateValidProfileDocument();
            document.Slots[0].StageClearProfileSnapshot.Version = -1;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignProfileDocumentMapper.ToDomainSlots(document));
        }

        [Test]
        public void Adapter_SourceDelegatesRepresentationMapping()
        {
            var source = File.ReadAllText(AdapterPath);

            Assert.That(source, Does.Contain("CampaignProfileDocumentMapper.ToDomainSlots"));
            Assert.That(source, Does.Contain("CampaignProfileDocumentMapper.ToFullReplacementUpdate"));
            Assert.That(source, Does.Not.Contain("private static CampaignSlotUpdate ToUpdate"));
            Assert.That(source, Does.Not.Contain("private static SaveSlotData ToSaveSlotData"));
            Assert.That(source, Does.Not.Contain("private static StageClearProfileSnapshot ToStageClearProfileSnapshot"));
        }

        private static SaveSlotData CreatePopulatedSlot(int slotNumber, string stageIdValue)
        {
            var stageId = StageId.CreateOrThrow(stageIdValue);
            var slot = new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = $"level-{slotNumber}",
                RemainingChances = 1,
                CampaignCompleted = true,
                HasNormalCampaignCompletionReceipt = true,
                NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
                {
                    Version = NormalCampaignCompletionReceipt.CurrentVersion,
                    CompletedStageId = stageId.Value,
                    StageRunId = string.Empty,
                    ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
                },
                NormalStagePerformanceRecords = new[]
                {
                    new NormalStagePerformanceRecord
                    {
                        Version = NormalStagePerformanceRecord.CurrentVersion,
                        StageId = stageId,
                        BestCombinedPushFlipUses = 7,
                    },
                },
                IntroComicCompleted = true,
                OutroComicCompleted = true,
                TotalDeaths = 4,
                LastPlayedAt = "2026-08-23T00:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot
                {
                    Version = 2,
                },
            };
            slot.StageClearProfileSnapshot.ClearRecordsByStageId[stageId] =
                new PlayerStageClearRecord
                {
                    StageId = stageId,
                    HasAttempted = true,
                    HasCleared = true,
                    ClearCount = 2,
                    ProcessedStageRunIds = new[] { "run-2" },
                };
            slot.StageClearProfileSnapshot.ProcessedStageRunIds.Add("run-2");
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds.Add("attempt-2");
            return slot;
        }

        private static CampaignProfileDocument CreateValidProfileDocument()
        {
            return CampaignProfileDocumentMapper.ToDocument(
                new[] { CreatePopulatedSlot(1, "stage-1-1") },
                "profile-inbound-validation",
                1,
                "2026-08-23T00:00:00Z",
                "tests");
        }
    }
}
