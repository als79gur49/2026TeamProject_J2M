using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class NormalCampaignCompletionReceiptTests
    {
        [Test]
        public void StructurallyValid_RequiresV1FinalFactFieldsAndObjectiveSource()
        {
            var receipt = CreateReceipt();

            Assert.That(receipt.IsStructurallyValid, Is.True);

            receipt.Version = 0;
            Assert.That(receipt.IsStructurallyValid, Is.False);
            receipt.Version = NormalCampaignCompletionReceipt.CurrentVersion + 1;
            Assert.That(receipt.IsStructurallyValid, Is.False);
            receipt.Version = NormalCampaignCompletionReceipt.CurrentVersion;
            receipt.CompletedStageId = string.Empty;
            Assert.That(receipt.IsStructurallyValid, Is.False);
            receipt.CompletedStageId = " Stage-4-3 ";
            Assert.That(receipt.IsStructurallyValid, Is.False);
            receipt.CompletedStageId = "stage-4-3";
            receipt.StageRunId = string.Empty;
            Assert.That(receipt.IsStructurallyValid, Is.False);
            receipt.StageRunId = "run-final";
            receipt.ClearSource = (int)StageClearSource.ForcedByDemoStageControl;
            Assert.That(receipt.IsStructurallyValid, Is.False);
        }

        [Test]
        public void Evaluate_NormalObjectiveCanonicalFinalStage_CreatesV1ReceiptFromStageRunId()
        {
            var result = NormalCampaignCompletionReceiptPolicy.Evaluate(
                EditorDirectPlayContext.None,
                CreateCompletion("stage-4-3"),
                StageId.CreateOrThrow("stage-4-3"),
                CreateResolver());

            Assert.That(result.Eligibility, Is.EqualTo(NormalCampaignCompletionReceiptEligibility.Eligible));
            Assert.That(result.Receipt.Version, Is.EqualTo(1));
            Assert.That(result.Receipt.CompletedStageId, Is.EqualTo("stage-4-3"));
            Assert.That(result.Receipt.StageRunId, Is.EqualTo("run-final"));
            Assert.That(result.Receipt.ClearSource, Is.EqualTo((int)StageClearSource.Objective));
        }

        [Test]
        public void Evaluate_NonFinalAndNonCampaignStages_AreIneligible()
        {
            var resolver = CreateResolver();

            var nonFinal = NormalCampaignCompletionReceiptPolicy.Evaluate(
                EditorDirectPlayContext.None,
                CreateCompletion("stage-4-2"),
                StageId.CreateOrThrow("stage-4-2"),
                resolver);
            var nonCampaign = NormalCampaignCompletionReceiptPolicy.Evaluate(
                EditorDirectPlayContext.None,
                CreateCompletion("debug-stage"),
                StageId.CreateOrThrow("debug-stage"),
                resolver);

            Assert.That(nonFinal.Eligibility, Is.EqualTo(NormalCampaignCompletionReceiptEligibility.NotFinalStage));
            Assert.That(nonFinal.Receipt, Is.Null);
            Assert.That(nonCampaign.Eligibility, Is.EqualTo(NormalCampaignCompletionReceiptEligibility.NotCampaignStage));
            Assert.That(nonCampaign.Receipt, Is.Null);
        }

        [Test]
        public void Evaluate_ForcedClear_IsIneligible()
        {
            var result = NormalCampaignCompletionReceiptPolicy.Evaluate(
                EditorDirectPlayContext.None,
                CreateCompletion(
                    "stage-4-3",
                    clearSource: StageClearSource.ForcedByDemoStageControl),
                StageId.CreateOrThrow("stage-4-3"),
                CreateResolver());

            Assert.That(result.Eligibility, Is.EqualTo(NormalCampaignCompletionReceiptEligibility.NonObjectiveSource));
            Assert.That(result.Receipt, Is.Null);
        }

        [TestCase(EditorDirectPlayMode.NonCampaign)]
        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        public void Evaluate_EveryDirectPlayMode_IsIneligible(EditorDirectPlayMode mode)
        {
            var result = NormalCampaignCompletionReceiptPolicy.Evaluate(
                new EditorDirectPlayContext(
                    mode,
                    StageId.CreateOrThrow("stage-4-3"),
                    string.Empty,
                    string.Empty,
                    3,
                    suppressCampaignFlow: false),
                CreateCompletion("stage-4-3"),
                StageId.CreateOrThrow("stage-4-3"),
                CreateResolver());

            Assert.That(result.Eligibility, Is.EqualTo(NormalCampaignCompletionReceiptEligibility.DirectPlay));
            Assert.That(result.Receipt, Is.Null);
        }

        [Test]
        public void Evaluate_MissingOrInvalidProvenance_FailsClosed()
        {
            var resolver = CreateResolver();
            var finalStage = StageId.CreateOrThrow("stage-4-3");

            Assert.That(
                NormalCampaignCompletionReceiptPolicy.Evaluate(
                    EditorDirectPlayContext.None,
                    null,
                    finalStage,
                    resolver).Eligibility,
                Is.EqualTo(NormalCampaignCompletionReceiptEligibility.MissingCompletionResult));
            Assert.That(
                NormalCampaignCompletionReceiptPolicy.Evaluate(
                    EditorDirectPlayContext.None,
                    CreateCompletion("stage-4-3", wasCleared: false),
                    finalStage,
                    resolver).Eligibility,
                Is.EqualTo(NormalCampaignCompletionReceiptEligibility.NotCleared));
            Assert.That(
                NormalCampaignCompletionReceiptPolicy.Evaluate(
                    EditorDirectPlayContext.None,
                    CreateCompletion("stage-4-3", runId: string.Empty),
                    finalStage,
                    resolver).Eligibility,
                Is.EqualTo(NormalCampaignCompletionReceiptEligibility.InvalidRunId));
            Assert.That(
                NormalCampaignCompletionReceiptPolicy.Evaluate(
                    EditorDirectPlayContext.None,
                    CreateCompletion("stage-4-3"),
                    StageId.None,
                    resolver).Eligibility,
                Is.EqualTo(NormalCampaignCompletionReceiptEligibility.InvalidStage));
            Assert.That(
                NormalCampaignCompletionReceiptPolicy.Evaluate(
                    EditorDirectPlayContext.None,
                    CreateCompletion("stage-4-3", clearSource: (StageClearSource)999),
                    finalStage,
                    resolver).Eligibility,
                Is.EqualTo(NormalCampaignCompletionReceiptEligibility.NonObjectiveSource));
        }

        [Test]
        public void CloneAndBothSaveMappings_PreserveReceiptValues()
        {
            var slot = SaveSlotData.CreateEmpty(1);
            slot.CurrentStageId = StageId.CreateOrThrow("stage-4-3");
            slot.CampaignCompleted = true;
            slot.NormalCampaignCompletionReceipt = CreateReceipt();

            var clone = slot.Clone();
            var compatibilityRoundTrip = SaveSlotDtoMapper.FromDto(
                SaveSlotDtoMapper.ToDto(new[] { slot }))[0];
            var document = CampaignProfileDocumentMapper.ToSlotDocument(slot);
            var documentRoundTrip = CampaignProfileDocumentMapper.ToReceipt(
                document.NormalCampaignCompletionReceipt);

            AssertReceipt(clone.NormalCampaignCompletionReceipt);
            AssertReceipt(compatibilityRoundTrip.NormalCampaignCompletionReceipt);
            AssertReceipt(documentRoundTrip);
            Assert.That(clone.NormalCampaignCompletionReceipt, Is.Not.SameAs(slot.NormalCampaignCompletionReceipt));
        }

        [Test]
        public void CampaignDocumentSerialization_RoundTripsReceipt()
        {
            var document = CampaignProfileDocumentMapper.ToDocument(
                new[]
                {
                    new SaveSlotData
                    {
                        SlotNumber = 1,
                        CurrentStageId = StageId.CreateOrThrow("stage-4-3"),
                        CampaignCompleted = true,
                        NormalCampaignCompletionReceipt = CreateReceipt(),
                    },
                },
                "profile",
                1,
                "2026-08-09T00:00:00Z",
                "test");

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignProfileDocument>(json);

            AssertReceipt(CampaignProfileDocumentMapper.ToReceipt(
                roundTripped.Slots[0].NormalCampaignCompletionReceipt));
        }

        [Test]
        public void MissingReceiptInOldProfile_LoadsAsNullAndPreservesCampaignState()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "j2m-old-profile-receipt-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(
                    Path.Combine(root, FileCampaignProfileRepository.ProfileFileName),
                    "{\"SchemaVersion\":1,\"ProfileId\":\"old-profile\",\"Slots\":[{\"SlotNumber\":1,\"StageId\":\"stage-4-3\",\"CampaignCompleted\":true}]}");
                var repository = new FileCampaignProfileRepository(new AtomicTextFileStore(root));

                var load = repository.Load();

                Assert.That(load.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
                Assert.That(load.Document.Slots[0].CampaignCompleted, Is.True);
                Assert.That(load.Document.Slots[0].StageId, Is.EqualTo("stage-4-3"));
                Assert.That(load.Document.Slots[0].NormalCampaignCompletionReceipt, Is.Null);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void MixedOldAndReceiptSlots_PreservePerSlotFieldPresence()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "j2m-mixed-profile-receipt-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(
                    Path.Combine(root, FileCampaignProfileRepository.ProfileFileName),
                    "{\"SchemaVersion\":1,\"ProfileId\":\"mixed-profile\",\"Slots\":[" +
                    "{\"SlotNumber\":1,\"StageId\":\"stage-4-2\"}," +
                    "{\"SlotNumber\":2,\"StageId\":\"stage-4-3\",\"CampaignCompleted\":true," +
                    "\"HasNormalCampaignCompletionReceipt\":true," +
                    "\"NormalCampaignCompletionReceipt\":{\"Version\":1,\"CompletedStageId\":\"stage-4-3\"," +
                    "\"StageRunId\":\"run-slot-2\",\"ClearSource\":0}}]}");
                var repository = new FileCampaignProfileRepository(new AtomicTextFileStore(root));

                var load = repository.Load();

                Assert.That(load.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
                Assert.That(load.Document.Slots[0].NormalCampaignCompletionReceipt, Is.Null);
                Assert.That(load.Document.Slots[1].NormalCampaignCompletionReceipt, Is.Not.Null);
                Assert.That(
                    load.Document.Slots[1].NormalCampaignCompletionReceipt.StageRunId,
                    Is.EqualTo("run-slot-2"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void UnsupportedForwardProfileVersion_FailsClosed()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "j2m-forward-profile-receipt-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(
                    Path.Combine(root, FileCampaignProfileRepository.ProfileFileName),
                    "{\"SchemaVersion\":2,\"ProfileId\":\"future-profile\",\"Slots\":[]}");
                var repository = new FileCampaignProfileRepository(new AtomicTextFileStore(root));

                var load = repository.Load();

                Assert.That(load.Status, Is.EqualTo(CampaignProfileLoadStatus.SchemaInvalid));
                Assert.That(load.Document, Is.Null);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void InvalidReceipt_RemainsPresentButCannotBecomeEligible()
        {
            var receipt = CreateReceipt();
            receipt.Version = 99;
            var mapped = CampaignProfileDocumentMapper.ToReceipt(
                CampaignProfileDocumentMapper.ToReceiptDocument(receipt));

            Assert.That(mapped, Is.Not.Null);
            Assert.That(mapped.Version, Is.EqualTo(99));
            Assert.That(mapped.IsStructurallyValid, Is.False);
            Assert.That(
                NormalCampaignCompletionReceiptPolicy.IsEligiblePersistedReceipt(
                    mapped,
                    CreateResolver()),
                Is.False);
        }

        [Test]
        public void InvalidReceipt_LoadsWithoutRepairAndRemainsIneligible()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "j2m-invalid-profile-receipt-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(
                    Path.Combine(root, FileCampaignProfileRepository.ProfileFileName),
                    "{\"SchemaVersion\":1,\"ProfileId\":\"invalid-receipt-profile\",\"Slots\":[" +
                    "{\"SlotNumber\":1,\"StageId\":\"stage-4-3\",\"CampaignCompleted\":true," +
                    "\"HasNormalCampaignCompletionReceipt\":true," +
                    "\"NormalCampaignCompletionReceipt\":{\"Version\":99,\"CompletedStageId\":\"stage-4-3\"," +
                    "\"StageRunId\":\"invalid-version-run\",\"ClearSource\":0}}]}");
                var repository = new FileCampaignProfileRepository(new AtomicTextFileStore(root));

                var load = repository.Load();
                Assert.That(load.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
                var receipt = CampaignProfileDocumentMapper.ToReceipt(
                    load.Document.Slots[0].NormalCampaignCompletionReceipt);

                Assert.That(receipt, Is.Not.Null);
                Assert.That(receipt.Version, Is.EqualTo(99));
                Assert.That(
                    NormalCampaignCompletionReceiptPolicy.IsEligiblePersistedReceipt(
                        receipt,
                        CreateResolver()),
                    Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void NewGameAndEmptySlot_StartWithoutReceipt()
        {
            var resolver = CreateResolver();

            Assert.That(SaveSlotData.CreateEmpty(1).NormalCampaignCompletionReceipt, Is.Null);
            Assert.That(
                SaveSlotData.CreateNewGame(1, resolver, "2026-08-09T00:00:00Z")
                    .NormalCampaignCompletionReceipt,
                Is.Null);
        }

        private static NormalCampaignCompletionReceipt CreateReceipt()
        {
            return new NormalCampaignCompletionReceipt
            {
                Version = 1,
                CompletedStageId = "stage-4-3",
                StageRunId = "run-final",
                ClearSource = (int)StageClearSource.Objective,
            };
        }

        private static MinimalStageCompletionResult CreateCompletion(
            string stageId,
            string runId = "run-final",
            bool wasCleared = true,
            StageClearSource clearSource = StageClearSource.Objective)
        {
            var parsedStageId = StageId.CreateOrThrow(stageId);
            return new MinimalStageCompletionResult(
                parsedStageId,
                new StageRunId(runId),
                new StageCompletionAttemptId("derived-attempt"),
                StageTerminalReason.Cleared,
                wasCleared,
                100,
                new StageObjectiveProgressSnapshot(true, true, true, wasCleared, 1, 1),
                clearSource);
        }

        private static CampaignStageSequenceResolver CreateResolver()
        {
            return new CampaignStageSequenceResolver(
                CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
        }

        private static void AssertReceipt(NormalCampaignCompletionReceipt receipt)
        {
            Assert.That(receipt, Is.Not.Null);
            Assert.That(receipt.Version, Is.EqualTo(1));
            Assert.That(receipt.CompletedStageId, Is.EqualTo("stage-4-3"));
            Assert.That(receipt.StageRunId, Is.EqualTo("run-final"));
            Assert.That(receipt.ClearSource, Is.EqualTo((int)StageClearSource.Objective));
        }
    }
}
