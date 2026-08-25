using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class NormalCampaignCompletionReceiptTests
    {
        [Test]
        public void V1_RequiresCanonicalStageLegacyRunIdAndObjectiveSource()
        {
            var receipt = CreateV1Receipt();

            Assert.That(receipt.IsStructurallyValid, Is.True);

            receipt.StageRunId = string.Empty;
            Assert.That(receipt.IsStructurallyValid, Is.False);
            receipt.StageRunId = "legacy-run-final";
            receipt.ClearSource = 1;
            Assert.That(receipt.IsStructurallyValid, Is.False);
            receipt.ClearSource = NormalCampaignCompletionReceipt.LegacyObjectiveClearSource;
            receipt.CompletedStageId = " Stage-4-3 ";
            Assert.That(receipt.IsStructurallyValid, Is.False);
        }

        [Test]
        public void V2_UsesStageIdOnlySemanticsAndIgnoresLegacyPhysicalFields()
        {
            var receipt = NormalCampaignCompletionReceiptPolicy.CreateV2(
                StageId.CreateOrThrow("stage-4-3"));

            Assert.That(receipt.Version, Is.EqualTo(2));
            Assert.That(receipt.CompletedStageId, Is.EqualTo("stage-4-3"));
            Assert.That(receipt.StageRunId, Is.Empty);
            Assert.That(
                receipt.ClearSource,
                Is.EqualTo(NormalCampaignCompletionReceipt.LegacyClearSourceAbsent));
            Assert.That(receipt.IsStructurallyValid, Is.True);

            receipt.StageRunId = "ignored-legacy-value";
            receipt.ClearSource = NormalCampaignCompletionReceipt.LegacyObjectiveClearSource;
            Assert.That(receipt.IsStructurallyValid, Is.True);
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(99)]
        public void UnknownVersion_FailsClosed(int version)
        {
            var receipt = CreateV1Receipt();
            receipt.Version = version;

            Assert.That(receipt.IsStructurallyValid, Is.False);
            Assert.That(
                NormalCampaignCompletionReceiptPolicy.IsEligiblePersistedReceipt(
                    receipt,
                    CreateResolver()),
                Is.False);
        }

        [Test]
        public void PersistedReceipt_FinalityComesFromInjectedResolver()
        {
            var resolver = CreateResolver();
            var finalReceipt = NormalCampaignCompletionReceiptPolicy.CreateV2(
                StageId.CreateOrThrow("stage-4-3"));
            var nonFinalReceipt = NormalCampaignCompletionReceiptPolicy.CreateV2(
                StageId.CreateOrThrow("stage-4-2"));

            Assert.That(
                NormalCampaignCompletionReceiptPolicy.IsEligiblePersistedReceipt(
                    finalReceipt,
                    resolver),
                Is.True);
            Assert.That(
                NormalCampaignCompletionReceiptPolicy.IsEligiblePersistedReceipt(
                    nonFinalReceipt,
                    resolver),
                Is.False);
        }

        [Test]
        public void CloneAndBothSaveMappings_PreserveV1AndV2PhysicalFields()
        {
            AssertRoundTrip(CreateV1Receipt());
            AssertRoundTrip(NormalCampaignCompletionReceiptPolicy.CreateV2(
                StageId.CreateOrThrow("stage-4-3")));
        }

        [Test]
        public void CampaignDocumentSerialization_RoundTripsV2WithoutRootSchemaMigration()
        {
            var document = CampaignSlotRawDataMapper.ToProfileDocument(
                new[]
                {
                    new SaveSlotData
                    {
                        SlotNumber = 1,
                        CurrentStageId = StageId.CreateOrThrow("stage-4-3"),
                        CampaignCompleted = true,
                        HasNormalCampaignCompletionReceipt = true,
                        NormalCampaignCompletionReceipt =
                            NormalCampaignCompletionReceiptPolicy.CreateV2(
                                StageId.CreateOrThrow("stage-4-3")),
                    },
                },
                "profile",
                1,
                "2026-08-09T00:00:00Z",
                "test");

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignProfileDocument>(json);
            var receipt = CampaignSlotRawDataMapper.FromProfileDocument(roundTripped)[0]
                .NormalCampaignCompletionReceipt;

            Assert.That(roundTripped.SchemaVersion, Is.EqualTo(CampaignProfileDocument.CurrentSchemaVersion));
            Assert.That(receipt.Version, Is.EqualTo(2));
            Assert.That(receipt.IsStructurallyValid, Is.True);
        }

        [Test]
        public void CurrentProfileMissingReceipt_RemainsMissingAndIsNotInferred()
        {
            var root = CreateTemporaryRoot("current-profile");
            try
            {
                File.WriteAllText(
                    Path.Combine(root, FileCampaignProfileRepository.ProfileFileName),
                    $"{{\"SchemaVersion\":{CampaignProfileDocument.CurrentSchemaVersion},\"ProfileId\":\"current-profile\",\"Slots\":[{{\"SlotNumber\":1,\"StageId\":\"stage-4-3\",\"RemainingChances\":3,\"CampaignCompleted\":true}}]}}");
                var repository = new FileCampaignProfileRepository(new AtomicTextFileStore(root));

                var load = repository.Load();

                Assert.That(load.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
                Assert.That(load.Document.Slots[0].CampaignCompleted, Is.True);
                Assert.That(load.Document.Slots[0].HasNormalCampaignCompletionReceipt, Is.False);
                Assert.That(load.Document.Slots[0].NormalCampaignCompletionReceipt, Is.Null);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void CurrentProfileWithV1Receipt_LoadsAndRemainsEligibleWithoutRuntimeIdentityTypes()
        {
            var root = CreateTemporaryRoot("v1-profile");
            try
            {
                File.WriteAllText(
                    Path.Combine(root, FileCampaignProfileRepository.ProfileFileName),
                    $"{{\"SchemaVersion\":{CampaignProfileDocument.CurrentSchemaVersion},\"ProfileId\":\"v1-receipt-profile\",\"Slots\":[" +
                    "{\"SlotNumber\":1,\"StageId\":\"stage-4-3\",\"RemainingChances\":3,\"CampaignCompleted\":true," +
                    "\"HasNormalCampaignCompletionReceipt\":true," +
                    "\"NormalCampaignCompletionReceipt\":{\"Version\":1,\"CompletedStageId\":\"stage-4-3\"," +
                    "\"StageRunId\":\"legacy-run\",\"ClearSource\":0}}]}");
                var repository = new FileCampaignProfileRepository(new AtomicTextFileStore(root));

                var load = repository.Load();
                var receipt = CampaignSlotRawDataMapper.FromProfileDocument(load.Document)[0]
                    .NormalCampaignCompletionReceipt;

                Assert.That(receipt.Version, Is.EqualTo(1));
                Assert.That(receipt.IsStructurallyValid, Is.True);
                Assert.That(
                    NormalCampaignCompletionReceiptPolicy.IsEligiblePersistedReceipt(
                        receipt,
                        CreateResolver()),
                    Is.True);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void InvalidAndPresentNullReceipts_ArePreservedWithoutRepair()
        {
            var invalid = CreateV1Receipt();
            invalid.Version = 99;
            var invalidSlot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-4-3"),
                CampaignCompleted = true,
                HasNormalCampaignCompletionReceipt = true,
                NormalCampaignCompletionReceipt = invalid,
            };
            var invalidClone = invalidSlot.Clone().NormalCampaignCompletionReceipt;
            var presentNullSlot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-4-3"),
            };
            presentNullSlot.CampaignCompleted = true;
            presentNullSlot.HasNormalCampaignCompletionReceipt = true;
            presentNullSlot.NormalCampaignCompletionReceipt = null;
            var presentNullRoundTrip = CampaignSlotRawDataMapper.FromDocument(
                CampaignSlotRawDataMapper.ToDocument(presentNullSlot));

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(invalidSlot));
            Assert.That(invalidClone.Version, Is.EqualTo(99));
            Assert.That(invalidClone.IsStructurallyValid, Is.False);
            Assert.That(presentNullRoundTrip.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(presentNullRoundTrip.NormalCampaignCompletionReceipt, Is.Null);
        }

        [Test]
        public void UnsupportedForwardProfileVersion_FailsClosed()
        {
            var root = CreateTemporaryRoot("forward-profile");
            try
            {
                File.WriteAllText(
                    Path.Combine(root, FileCampaignProfileRepository.ProfileFileName),
                    "{\"SchemaVersion\":99,\"ProfileId\":\"future-profile\",\"Slots\":[]}");
                var load = new FileCampaignProfileRepository(
                    new AtomicTextFileStore(root)).Load();

                Assert.That(load.Status, Is.EqualTo(CampaignProfileLoadStatus.UnsupportedVersion));
                Assert.That(load.Document, Is.Null);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static void AssertRoundTrip(NormalCampaignCompletionReceipt receipt)
        {
            var slot = SaveSlotData.CreateEmpty(1);
            slot.CurrentStageId = StageId.CreateOrThrow("stage-4-3");
            slot.CampaignCompleted = true;
            slot.HasNormalCampaignCompletionReceipt = true;
            slot.NormalCampaignCompletionReceipt = receipt;

            var clone = slot.Clone().NormalCampaignCompletionReceipt;
            var profile = CampaignSlotRawDataMapper.FromDocument(
                CampaignSlotRawDataMapper.ToDocument(slot))
                .NormalCampaignCompletionReceipt;

            AssertPhysicalEquality(receipt, clone);
            AssertPhysicalEquality(receipt, profile);
        }

        private static void AssertPhysicalEquality(
            NormalCampaignCompletionReceipt expected,
            NormalCampaignCompletionReceipt actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Version, Is.EqualTo(expected.Version));
            Assert.That(actual.CompletedStageId, Is.EqualTo(expected.CompletedStageId));
            Assert.That(actual.StageRunId, Is.EqualTo(expected.StageRunId));
            Assert.That(actual.ClearSource, Is.EqualTo(expected.ClearSource));
        }

        private static NormalCampaignCompletionReceipt CreateV1Receipt()
        {
            return new NormalCampaignCompletionReceipt
            {
                Version = NormalCampaignCompletionReceipt.LegacyVersion,
                CompletedStageId = "stage-4-3",
                StageRunId = "legacy-run-final",
                ClearSource = NormalCampaignCompletionReceipt.LegacyObjectiveClearSource,
            };
        }

        private static CampaignStageSequenceResolver CreateResolver()
        {
            var definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            definition.SetEntries(new[]
            {
                CreateEntry("stage-4-2", "level-4"),
                CreateEntry("stage-4-3", "level-4"),
            });
            var resolver = new CampaignStageSequenceResolver(definition);
            UnityEngine.Object.DestroyImmediate(definition);
            return resolver;
        }

        private static CampaignStageSequenceEntry CreateEntry(string stageId, string levelGroupId)
        {
            var entry = new CampaignStageSequenceEntry();
            entry.Set(StageId.CreateOrThrow(stageId), levelGroupId);
            return entry;
        }

        private static string CreateTemporaryRoot(string suffix)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "j2m-receipt-" + suffix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
