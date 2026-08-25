using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSlotRawDataMapperTests
    {
        private const string AdapterPath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveSlotStoreAdapter.cs";

        [Test]
        public void SlotMapper_DomainDocumentDomainRoundTripPreservesAllFields()
        {
            var expected = CreatePopulatedSlot(2, "stage-2-1");

            var document = CampaignSlotRawDataMapper.ToDocument(expected);
            var actual = CampaignSlotRawDataMapper.FromDocument(document);

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
        public void SlotMapper_ToDomainPreservesValidatedPerformanceRecordOrder()
        {
            var document = CampaignSlotRawDataMapper.ToDocument(
                CreatePopulatedSlot(1, "stage-1-1"));
            document.NormalStagePerformanceRecords = new[]
            {
                new NormalStagePerformanceRecordDocument
                {
                    Version = NormalStagePerformanceRecord.CurrentVersion,
                    StageId = "stage-1-2",
                    BestCombinedPushFlipUses = 5,
                },
                new NormalStagePerformanceRecordDocument
                {
                    Version = NormalStagePerformanceRecord.CurrentVersion,
                    StageId = "stage-1-1",
                    BestCombinedPushFlipUses = 7,
                },
            };

            var slot = CampaignSlotRawDataMapper.FromDocument(document);

            Assert.That(slot.NormalStagePerformanceRecords, Has.Length.EqualTo(2));
            Assert.That(slot.NormalStagePerformanceRecords[0].StageId.Value,
                Is.EqualTo("stage-1-2"));
            Assert.That(slot.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(5));
            Assert.That(slot.NormalStagePerformanceRecords[1].StageId.Value,
                Is.EqualTo("stage-1-1"));
            Assert.That(slot.NormalStagePerformanceRecords[1].BestCombinedPushFlipUses,
                Is.EqualTo(7));
        }

        [Test]
        public void ProfileMapper_OmitsEmptySlotsSortsDocumentsAndProducesValidContract()
        {
            var document = CampaignSlotRawDataMapper.ToProfileDocument(
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
        public void SaveSlotClone_PreservesRawShapeWhileDeepCopyingNestedState()
        {
            var firstStageId = StageId.CreateOrThrow("stage-1-1");
            var secondStageId = StageId.CreateOrThrow("stage-1-2");
            var originalRecord = new PlayerStageClearRecord
            {
                StageId = firstStageId,
                ClearCount = -7,
                ProcessedStageRunIds = null,
            };
            var originalPerformance = new NormalStagePerformanceRecord
            {
                Version = NormalStagePerformanceRecord.CurrentVersion + 1,
                StageId = firstStageId,
                BestCombinedPushFlipUses = -3,
            };
            var original = new SaveSlotData
            {
                SlotNumber = 9,
                CurrentStageId = firstStageId,
                CurrentLevelGroupId = null,
                RemainingChances = 0,
                TotalDeaths = -4,
                LastPlayedAt = null,
                NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
                {
                    Version = 99,
                    CompletedStageId = null,
                    StageRunId = null,
                    ClearSource = -99,
                },
                NormalStagePerformanceRecords = new[]
                {
                    originalPerformance,
                    null,
                    originalPerformance,
                },
                StageClearProfileSnapshot = new StageClearProfileSnapshot
                {
                    Version = -2,
                    ClearRecordsByStageId = new Dictionary<StageId, PlayerStageClearRecord>
                    {
                        [firstStageId] = originalRecord,
                        [secondStageId] = null,
                    },
                    ProcessedStageRunIds = null,
                    ProcessedClearAttemptIds = new HashSet<string>(StringComparer.Ordinal)
                    {
                        string.Empty,
                    },
                },
            };

            var clone = original.Clone();

            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.SlotNumber, Is.EqualTo(9));
            Assert.That(clone.CurrentLevelGroupId, Is.Null);
            Assert.That(clone.RemainingChances, Is.Zero);
            Assert.That(clone.TotalDeaths, Is.EqualTo(-4));
            Assert.That(clone.LastPlayedAt, Is.Null);
            Assert.That(clone.NormalCampaignCompletionReceipt, Is.Not.SameAs(
                original.NormalCampaignCompletionReceipt));
            Assert.That(clone.NormalCampaignCompletionReceipt.Version, Is.EqualTo(99));
            Assert.That(clone.NormalCampaignCompletionReceipt.CompletedStageId, Is.Null);
            Assert.That(clone.NormalCampaignCompletionReceipt.StageRunId, Is.Null);
            Assert.That(clone.NormalCampaignCompletionReceipt.ClearSource, Is.EqualTo(-99));
            Assert.That(clone.NormalStagePerformanceRecords, Is.Not.SameAs(
                original.NormalStagePerformanceRecords));
            Assert.That(clone.NormalStagePerformanceRecords, Has.Length.EqualTo(3));
            Assert.That(clone.NormalStagePerformanceRecords[0], Is.Not.SameAs(originalPerformance));
            Assert.That(clone.NormalStagePerformanceRecords[0].Version,
                Is.EqualTo(originalPerformance.Version));
            Assert.That(clone.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(-3));
            Assert.That(clone.NormalStagePerformanceRecords[1], Is.Null);
            Assert.That(clone.NormalStagePerformanceRecords[2].BestCombinedPushFlipUses,
                Is.EqualTo(-3));
            Assert.That(clone.StageClearProfileSnapshot, Is.Not.SameAs(
                original.StageClearProfileSnapshot));
            Assert.That(clone.StageClearProfileSnapshot.Version, Is.EqualTo(-2));
            Assert.That(clone.StageClearProfileSnapshot.ClearRecordsByStageId, Is.Not.SameAs(
                original.StageClearProfileSnapshot.ClearRecordsByStageId));
            Assert.That(clone.StageClearProfileSnapshot.ClearRecordsByStageId[firstStageId],
                Is.Not.SameAs(originalRecord));
            Assert.That(clone.StageClearProfileSnapshot.ClearRecordsByStageId[firstStageId]
                .ProcessedStageRunIds, Is.Null);
            Assert.That(clone.StageClearProfileSnapshot.ClearRecordsByStageId[secondStageId],
                Is.Null);
            Assert.That(clone.StageClearProfileSnapshot.ProcessedStageRunIds, Is.Null);
            Assert.That(clone.StageClearProfileSnapshot.ProcessedClearAttemptIds,
                Is.Not.SameAs(original.StageClearProfileSnapshot.ProcessedClearAttemptIds));
            Assert.That(clone.StageClearProfileSnapshot.ProcessedClearAttemptIds,
                Does.Contain(string.Empty));
        }

        [Test]
        public void SaveSlotClone_PreservesNullTopLevelCollections()
        {
            var original = new SaveSlotData
            {
                NormalStagePerformanceRecords = null,
                StageClearProfileSnapshot = null,
            };

            var clone = original.Clone();

            Assert.That(clone.NormalStagePerformanceRecords, Is.Null);
            Assert.That(clone.StageClearProfileSnapshot, Is.Null);
        }

        [Test]
        public void RawMapper_RejectsNonCanonicalCarrierWithoutMutatingDiagnosticEvidence()
        {
            var firstStageId = StageId.CreateOrThrow("stage-1-1");
            var secondStageId = StageId.CreateOrThrow("stage-1-2");
            var original = CreatePopulatedSlot(1, firstStageId.Value);
            original.CurrentLevelGroupId = null;
            original.LastPlayedAt = null;
            original.HasNormalCampaignCompletionReceipt = false;
            original.StageClearProfileSnapshot = null;
            original.NormalStagePerformanceRecords = new[]
            {
                new NormalStagePerformanceRecord
                {
                    StageId = secondStageId,
                    BestCombinedPushFlipUses = 9,
                },
                new NormalStagePerformanceRecord
                {
                    StageId = firstStageId,
                    BestCombinedPushFlipUses = 8,
                },
                new NormalStagePerformanceRecord
                {
                    StageId = secondStageId,
                    BestCombinedPushFlipUses = 4,
                },
            };

            var diagnosticClone = original.Clone();

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(original));
            Assert.That(original.CurrentLevelGroupId, Is.Null);
            Assert.That(original.LastPlayedAt, Is.Null);
            Assert.That(original.HasNormalCampaignCompletionReceipt, Is.False);
            Assert.That(original.StageClearProfileSnapshot, Is.Null);
            Assert.That(original.NormalStagePerformanceRecords, Has.Length.EqualTo(3));
            Assert.That(diagnosticClone.CurrentLevelGroupId, Is.Null);
            Assert.That(diagnosticClone.LastPlayedAt, Is.Null);
            Assert.That(diagnosticClone.HasNormalCampaignCompletionReceipt, Is.False);
            Assert.That(diagnosticClone.StageClearProfileSnapshot, Is.Null);
            Assert.That(diagnosticClone.NormalStagePerformanceRecords, Has.Length.EqualTo(3));
        }

        [TestCase(null, null, true)]
        [TestCase("", "", true)]
        [TestCase(null, "", false)]
        [TestCase("", null, false)]
        public void RawMapper_ReceiptSerializerResidueAcceptsOnlyExactPairedStringShapes(
            string completedStageId,
            string stageRunId,
            bool shouldMap)
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.HasNormalCampaignCompletionReceipt = false;
            slot.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceipt
                {
                    Version = 0,
                    CompletedStageId = completedStageId,
                    StageRunId = stageRunId,
                    ClearSource = 0,
                };

            if (shouldMap)
            {
                var document = CampaignSlotRawDataMapper.ToDocument(slot);

                Assert.That(document.NormalCampaignCompletionReceipt.CompletedStageId,
                    Is.EqualTo(completedStageId));
                Assert.That(document.NormalCampaignCompletionReceipt.StageRunId,
                    Is.EqualTo(stageRunId));
            }
            else
            {
                Assert.Throws<ArgumentException>(() =>
                    CampaignSlotRawDataMapper.ToDocument(slot));
                Assert.Throws<ArgumentException>(() =>
                    CampaignSlotRawDataMapper.ToState(slot));
                Assert.Throws<ArgumentException>(() =>
                    CampaignSlotRawDataMapper.ToEntry(slot));
                Assert.Throws<ArgumentException>(() =>
                    CampaignSlotRawDataMapper.ToEntries(new[] { slot }));
                Assert.Throws<ArgumentException>(() =>
                    CampaignSlotRawDataMapper.ToProfileDocument(
                        new[] { slot },
                        "profile-mixed-receipt-residue",
                        1,
                        string.Empty,
                        "tests"));
            }

            Assert.That(slot.NormalCampaignCompletionReceipt.CompletedStageId,
                Is.EqualTo(completedStageId));
            Assert.That(slot.NormalCampaignCompletionReceipt.StageRunId,
                Is.EqualTo(stageRunId));
        }

        [Test]
        public void RawMapper_AbsentReceiptWithStructurallyValidPayloadFailsClosed()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.HasNormalCampaignCompletionReceipt = false;
            slot.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceipt
                {
                    Version = NormalCampaignCompletionReceipt.CurrentVersion,
                    CompletedStageId = "stage-1-1",
                    StageRunId = string.Empty,
                    ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
                };

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(slot));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RawMapper_DuplicateSlotDocumentsFailClosedWithoutLosingReceiptState(
            bool useToRawSlotsAlias)
        {
            var presentWithoutPayload = CreatePopulatedSlot(1, "stage-1-1");
            presentWithoutPayload.HasNormalCampaignCompletionReceipt = true;
            presentWithoutPayload.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceipt
                {
                    Version = 0,
                    CompletedStageId = null,
                    StageRunId = null,
                    ClearSource = 0,
                };
            var absent = CreatePopulatedSlot(1, "stage-1-1");
            absent.HasNormalCampaignCompletionReceipt = false;
            absent.NormalCampaignCompletionReceipt = null;
            var documents = new[]
            {
                CampaignSlotRawDataMapper.ToDocument(presentWithoutPayload),
                CampaignSlotRawDataMapper.ToDocument(absent),
            };

            Assert.Throws<ArgumentException>(() =>
            {
                if (useToRawSlotsAlias)
                {
                    CampaignSlotRawDataMapper.ToRawSlots(documents);
                }
                else
                {
                    CampaignSlotRawDataMapper.FromDocuments(documents);
                }
            });

            Assert.That(documents[0].HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(documents[0].NormalCampaignCompletionReceipt, Is.Not.Null);
            Assert.That(documents[0].NormalCampaignCompletionReceipt.CompletedStageId,
                Is.Null);
            Assert.That(documents[0].NormalCampaignCompletionReceipt.StageRunId, Is.Null);
            Assert.That(documents[1].HasNormalCampaignCompletionReceipt, Is.False);
            Assert.That(documents[1].NormalCampaignCompletionReceipt, Is.Null);
        }

        [TestCase(0)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1)]
        public void ProfileMapper_RejectsInvalidEmptyShapedSlot(int remainingChances)
        {
            var invalid = SaveSlotData.CreateEmpty(1);
            invalid.RemainingChances = remainingChances;

            Assert.That(invalid.IsEmpty, Is.False);
            Assert.Throws<ArgumentException>(() => CampaignSlotRawDataMapper.ToProfileDocument(
                new[] { invalid },
                "profile-invalid-empty-shape",
                0,
                string.Empty,
                "tests"));
        }

        [Test]
        public void ProfileMapper_MissingDocumentsRestoreAsFixedEmptyDomainSlots()
        {
            var document = CampaignSlotRawDataMapper.ToProfileDocument(
                new[] { CreatePopulatedSlot(2, "stage-2-1") },
                "profile-domain",
                2,
                string.Empty,
                "tests");

            var slots = CampaignSlotRawDataMapper.FromProfileDocument(document);

            Assert.That(slots, Has.Length.EqualTo(CampaignSaveSlotPolicy.SlotCount));
            Assert.That(slots[0].SlotNumber, Is.EqualTo(1));
            Assert.That(slots[0].IsEmpty, Is.True);
            Assert.That(slots[1].CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(slots[2].SlotNumber, Is.EqualTo(3));
            Assert.That(slots[2].IsEmpty, Is.True);
        }

        [Test]
        public void SlotMapper_ZeroChancesAreInvalidInDocumentAndDomainDirections()
        {
            var document = CampaignSlotRawDataMapper.ToDocument(CreatePopulatedSlot(1, "stage-1-1"));
            document.RemainingChances = 0;
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.RemainingChances = 0;

            Assert.Throws<InvalidOperationException>(() => CampaignSlotRawDataMapper.FromDocument(document));
            Assert.Throws<ArgumentException>(() => CampaignSlotRawDataMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_FullReplacementDocumentContainsEveryPersistedField()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            var document = CampaignSlotRawDataMapper.ToDocument(slot);

            Assert.That(document.StageId, Is.EqualTo(slot.CurrentStageId.Value));
            Assert.That(document.LevelGroupId, Is.EqualTo(slot.CurrentLevelGroupId));
            Assert.That(document.RemainingChances, Is.EqualTo(slot.RemainingChances));
            Assert.That(document.CampaignCompleted, Is.EqualTo(slot.CampaignCompleted));
            Assert.That(document.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(document.NormalCampaignCompletionReceipt.CompletedStageId,
                Is.EqualTo(slot.NormalCampaignCompletionReceipt.CompletedStageId));
            Assert.That(document.IntroComicCompleted, Is.EqualTo(slot.IntroComicCompleted));
            Assert.That(document.OutroComicCompleted, Is.EqualTo(slot.OutroComicCompleted));
            Assert.That(document.TotalDeaths, Is.EqualTo(slot.TotalDeaths));
            Assert.That(document.LastPlayedAtUtc, Is.EqualTo(slot.LastPlayedAt));
            Assert.That(document.NormalStagePerformanceRecords, Has.Length.EqualTo(
                slot.NormalStagePerformanceRecords.Length));
            Assert.That(document.NormalStagePerformanceRecords[0].StageId,
                Is.EqualTo(slot.NormalStagePerformanceRecords[0].StageId.Value));
            Assert.That(document.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(slot.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses));
            Assert.That(document.StageClearProfileSnapshot.Version,
                Is.EqualTo(slot.StageClearProfileSnapshot.Version));
            Assert.That(document.StageClearProfileSnapshot.Records, Has.Length.EqualTo(
                slot.StageClearProfileSnapshot.ClearRecordsByStageId.Count));
        }

        [Test]
        public void SlotMapper_RejectsEmptyDomainSlot()
        {
            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(SaveSlotData.CreateEmpty(1)));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsNegativeStageClearProfileVersion()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot.Version = -1;

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsNegativeStageClearCount()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot
                .ClearRecordsByStageId[slot.CurrentStageId]
                .ClearCount = -1;

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsDuplicateProcessedStageRunIds()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot
                .ClearRecordsByStageId[slot.CurrentStageId]
                .ProcessedStageRunIds = new[] { "run-2", "run-2" };

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDocumentRejectsBlankProcessedClearAttemptId()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds.Add(" ");

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(slot));
        }

        [Test]
        public void SlotMapper_ToDomainRejectsInvalidNestedDocument()
        {
            var document = CampaignSlotRawDataMapper.ToDocument(
                CreatePopulatedSlot(1, "stage-1-1"));
            document.StageClearProfileSnapshot.Version = -1;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignSlotRawDataMapper.FromDocument(document));
        }

        [Test]
        public void SlotMapper_PresentNullCompletionReceiptRoundTripsUnchanged()
        {
            var slot = CreatePopulatedSlot(1, "stage-1-1");
            slot.HasNormalCampaignCompletionReceipt = true;
            slot.NormalCampaignCompletionReceipt = null;

            var document = CampaignSlotRawDataMapper.ToDocument(slot);
            var roundTripped = CampaignSlotRawDataMapper.FromDocument(document);

            Assert.That(document.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(document.NormalCampaignCompletionReceipt, Is.Null);
            Assert.That(roundTripped.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(roundTripped.NormalCampaignCompletionReceipt, Is.Null);
        }

        [Test]
        public void ProfileMapper_RejectsLastPlayedSlotThatIsNotPersisted()
        {
            Assert.Throws<ArgumentException>(() => CampaignSlotRawDataMapper.ToProfileDocument(
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
                CampaignSlotRawDataMapper.FromProfileDocument(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsBlankProfileId()
        {
            var document = CreateValidProfileDocument();
            document.ProfileId = " ";

            Assert.Throws<InvalidOperationException>(() =>
                CampaignSlotRawDataMapper.FromProfileDocument(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsMissingLastPlayedSlot()
        {
            var document = CreateValidProfileDocument();
            document.LastPlayedSlotNumber = 2;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignSlotRawDataMapper.FromProfileDocument(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsDuplicateSlotDocuments()
        {
            var document = CreateValidProfileDocument();
            document.Slots = new[] { document.Slots[0], document.Slots[0] };

            Assert.Throws<InvalidOperationException>(() =>
                CampaignSlotRawDataMapper.FromProfileDocument(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsOutOfRangeSlotDocument()
        {
            var document = CreateValidProfileDocument();
            document.Slots[0].SlotNumber = CampaignSaveSlotPolicy.SlotCount + 1;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignSlotRawDataMapper.FromProfileDocument(document));
        }

        [Test]
        public void ProfileMapper_ToDomainSlotsRejectsInvalidNestedSlotDocument()
        {
            var document = CreateValidProfileDocument();
            document.Slots[0].StageClearProfileSnapshot.Version = -1;

            Assert.Throws<InvalidOperationException>(() =>
                CampaignSlotRawDataMapper.FromProfileDocument(document));
        }

        [Test]
        public void Adapter_SourceUsesImmutableParserAndStateMapperOnly()
        {
            var source = File.ReadAllText(AdapterPath);

            Assert.That(source, Does.Not.Contain("CampaignSlotRawDataMapper.FromProfileDocument"));
            Assert.That(source, Does.Not.Contain("CampaignSlotRawDataMapper"));
            Assert.That(source, Does.Contain("CampaignSlotParser.ParseEntry"));
            Assert.That(source, Does.Contain(
                "public CampaignSlotEntry[] LoadAll()"));
            Assert.That(source, Does.Not.Contain("CampaignSlotMaintenanceReplacement"));
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
            return CampaignSlotRawDataMapper.ToProfileDocument(
                new[] { CreatePopulatedSlot(1, "stage-1-1") },
                "profile-inbound-validation",
                1,
                "2026-08-23T00:00:00Z",
                "tests");
        }
    }
}
