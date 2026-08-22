using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveSlotStoreAdapterTests
    {
        private const string FixedNowUtc = "2026-07-07T00:00:00Z";
        private const string TransientStoreNamespace =
            "Game.Feature.Stages.Tests.CampaignSaveSlotStoreAdapter.saves";

        [SetUp]
        public void SetUp()
        {
            ClearTransientState();
            new TransientCampaignSaveSlotStore().ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            ClearTransientState();
            new TransientCampaignSaveSlotStore().ClearAll();
        }

        [Test]
        public void LoadAll_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 2));
            legacy.SaveSlot(CreateSlot(3, "stage-3-1", "level-3", 1));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 2));
            adapter.SaveSlot(CreateSlot(3, "stage-3-1", "level-3", 1));

            AssertSlotsEquivalent(legacy.LoadAll(), adapter.LoadAll());
            Assert.That(adapter.LastCampaignLoadReport.Status, Is.EqualTo(CampaignSaveLoadStatus.Loaded));
        }

        [Test]
        public void LoadSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 1));
            adapter.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 1));

            AssertSlotEquivalent(legacy.LoadSlot(2), adapter.LoadSlot(2));
        }

        [Test]
        public void SaveSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            var slot = CreateSlot(1, "stage-1-1", "level-1", 2);
            slot.IntroComicCompleted = true;
            slot.TotalDeaths = 4;
            slot.NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
            {
                Version = 1,
                CompletedStageId = "stage-4-3",
                StageRunId = "adapter-receipt-run",
                ClearSource = NormalCampaignCompletionReceipt.LegacyObjectiveClearSource,
            };

            legacy.SaveSlot(slot);
            adapter.SaveSlot(slot);

            AssertSlotEquivalent(legacy.LoadSlot(1), adapter.LoadSlot(1));
        }

        [Test]
        public void SaveSlot_NullReceiptClearsExistingReceiptAsFullReplacement()
        {
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            var completed = CreateSlot(1, "stage-4-3", "level-4", 3);
            completed.CampaignCompleted = true;
            completed.NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
            {
                Version = 1,
                CompletedStageId = "stage-4-3",
                StageRunId = "receipt-to-clear",
                ClearSource = NormalCampaignCompletionReceipt.LegacyObjectiveClearSource,
            };
            adapter.SaveSlot(completed);

            var replacement = CreateSlot(1, "stage-0-1", "level-0", 3);
            adapter.SaveSlot(replacement);

            Assert.That(adapter.LoadSlot(1).NormalCampaignCompletionReceipt, Is.Null);
            Assert.That(
                repository.SavedDocument.Slots[0].HasNormalCampaignCompletionReceipt,
                Is.False);
            Assert.That(
                repository.SavedDocument.Slots[0].NormalCampaignCompletionReceipt,
                Is.Null);
        }

        [Test]
        public void SaveSlot_EmptyInputDeletesSlotAndRemainsEmpty()
        {
            var adapter = CreateAdapter();
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 2));

            adapter.SaveSlot(SaveSlotData.CreateEmpty(1));

            Assert.That(adapter.LoadSlot(1).IsEmpty, Is.True);
        }

        [Test]
        public void InitializeNewGame_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();

            var legacySlot = legacy.InitializeNewGame(1, resolver, FixedNowUtc);
            var adapterSlot = adapter.InitializeNewGame(1, resolver, FixedNowUtc);

            AssertSlotEquivalent(legacySlot, adapterSlot);
        }

        [Test]
        public void UpdateSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));

            legacy.UpdateSlot(1, slot =>
            {
                slot.CurrentStageId = StageId.CreateOrThrow("stage-1-2");
                slot.RemainingChances = 1;
                slot.TotalDeaths = 2;
                slot.StageClearProfileSnapshot.Version = 5;
            });
            adapter.UpdateSlot(1, slot =>
            {
                slot.CurrentStageId = StageId.CreateOrThrow("stage-1-2");
                slot.RemainingChances = 1;
                slot.TotalDeaths = 2;
                slot.StageClearProfileSnapshot.Version = 5;
            });

            AssertSlotEquivalent(legacy.LoadSlot(1), adapter.LoadSlot(1));
        }

        [Test]
        public void UpdateSlot_PreservesInvalidReceiptPresenceAcrossCompatibilityRoundTrip()
        {
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            repository.CurrentDocument.Slots[0].HasNormalCampaignCompletionReceipt = true;
            repository.CurrentDocument.Slots[0].NormalCampaignCompletionReceipt = null;

            adapter.UpdateSlot(1, slot =>
            {
                Assert.That(slot.HasNormalCampaignCompletionReceipt, Is.True);
                Assert.That(slot.NormalCampaignCompletionReceipt, Is.Null);
                slot.RemainingChances = 2;
            });

            Assert.That(
                repository.SavedDocument.Slots[0].HasNormalCampaignCompletionReceipt,
                Is.True);
            Assert.That(
                repository.SavedDocument.Slots[0].NormalCampaignCompletionReceipt,
                Is.Null);
        }

        [TestCase(CampaignProfileLoadStatus.CorruptNoFallback, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.InvalidDocument, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.UnsupportedVersion, CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.Unauthorized, CampaignSaveLoadStatus.Unauthorized)]
        [TestCase(CampaignProfileLoadStatus.IoFailed, CampaignSaveLoadStatus.IoFailed)]
        public void UpdateSlot_FirstLoadBlocked_DoesNotMutateRetryOrWrite(
            CampaignProfileLoadStatus profileStatus,
            CampaignSaveLoadStatus expectedStatus)
        {
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            repository.ResetCounts();
            repository.EnqueueLoadResult(new CampaignProfileLoadResult(profileStatus, null, "blocked"));
            var mutationInvoked = false;

            Assert.Throws<InvalidOperationException>(() => adapter.UpdateSlot(1, slot =>
            {
                mutationInvoked = true;
                slot.RemainingChances = 1;
            }));

            Assert.That(mutationInvoked, Is.False);
            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(adapter.LastCampaignLoadReport.Status, Is.EqualTo(expectedStatus));
            Assert.That(adapter.LastCampaignLoadReport.Reason, Is.EqualTo("blocked"));
        }

        [Test]
        public void UpdateSlot_SecondLoadBlocked_DoesNotWriteAndReportsLatestFailure()
        {
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            repository.ResetCounts();
            repository.EnqueueLoadResult(new CampaignProfileLoadResult(
                CampaignProfileLoadStatus.Loaded,
                repository.CurrentDocument,
                "loaded"));
            repository.EnqueueLoadResult(new CampaignProfileLoadResult(
                CampaignProfileLoadStatus.IoFailed,
                null,
                "second load failed"));
            var mutationInvoked = false;

            Assert.Throws<InvalidOperationException>(() => adapter.UpdateSlot(1, slot =>
            {
                mutationInvoked = true;
                slot.RemainingChances = 1;
            }));

            Assert.That(mutationInvoked, Is.True);
            Assert.That(repository.LoadCount, Is.EqualTo(2));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(adapter.LastCampaignLoadReport.Status, Is.EqualTo(CampaignSaveLoadStatus.IoFailed));
            Assert.That(adapter.LastCampaignLoadReport.Reason, Is.EqualTo("second load failed"));
        }

        [Test]
        public void RecoveryPending_BlocksImplicitLoadsAndEveryWriteWithoutRepositoryAccess()
        {
            var repository = new RecordingRepository();
            var recovery = new StubRecoveryPort { HasPendingReset = true };
            var adapter = CreateAdapter(repository, recovery);
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();

            var diagnostic = adapter.LoadAllWithReport();

            Assert.That(diagnostic.Report.Status, Is.EqualTo(CampaignSaveLoadStatus.RecoveryPending));
            Assert.That(diagnostic.Slots, Has.Length.EqualTo(CampaignSaveSlotPolicy.SlotCount));
            Assert.Throws<InvalidOperationException>(() => adapter.LoadAll());
            Assert.Throws<InvalidOperationException>(() => adapter.LoadSlot(1));
            Assert.Throws<InvalidOperationException>(() => adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3)));
            Assert.Throws<InvalidOperationException>(() => adapter.UpdateSlot(1, _ => { }));
            Assert.Throws<InvalidOperationException>(() => adapter.DeleteSlot(1));
            Assert.Throws<InvalidOperationException>(() => adapter.ClearAll());
            Assert.Throws<InvalidOperationException>(() => adapter.InitializeNewGame(1, resolver, FixedNowUtc));
            Assert.That(repository.LoadCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(adapter.LastCampaignLoadReport.Status, Is.EqualTo(CampaignSaveLoadStatus.RecoveryPending));
        }

        [Test]
        public void DeleteSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            legacy.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 2));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            adapter.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 2));

            legacy.DeleteSlot(1);
            adapter.DeleteSlot(1);

            AssertSlotsEquivalent(legacy.LoadAll(), adapter.LoadAll());
        }

        [Test]
        public void ClearAll_Parity()
        {
            var legacy = CreateLegacyStore();
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));

            legacy.ClearAll();
            adapter.ClearAll();

            AssertSlotsEquivalent(legacy.LoadAll(), adapter.LoadAll());
            Assert.That(repository.SavedDocument.Slots, Is.Empty);
        }

        [Test]
        public void TransientStore_DefaultNamespaceRemainsNonPersistent()
        {
            var store = new TransientCampaignSaveSlotStore();

            store.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 2));

            Assert.That(store.DiagnosticsKey, Is.EqualTo(TransientCampaignSaveSlotStore.DefaultDiagnosticsKey));
            Assert.That(store.LoadSlot(1).CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-1-1")));
        }

        [Test]
        public void ProductionComposition_DoesNotConstructConcreteAdapterDirectly()
        {
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveSlotStoreAdapter"));
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveSlotStoreAdapter"));
            Assert.That(
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Not.Contain("CampaignSaveSlotStoreAdapter"));
        }

        private static TransientCampaignSaveSlotStore CreateLegacyStore()
        {
            return new TransientCampaignSaveSlotStore(TransientStoreNamespace);
        }

        private static CampaignSaveSlotStoreAdapter CreateAdapter(
            RecordingRepository repository = null,
            ICampaignSaveRecoveryPort recoveryPort = null)
        {
            repository ??= new RecordingRepository();
            var service = new CampaignSaveService(
                repository,
                () => FixedNowUtc,
                "adapter-test-profile",
                "adapter-test-product");
            return new CampaignSaveSlotStoreAdapter(service, recoveryPort);
        }

        private static SaveSlotData CreateSlot(
            int slotNumber,
            string stageId,
            string levelGroupId,
            int remainingChances)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = remainingChances,
                LastPlayedAt = FixedNowUtc,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private static void AssertSlotsEquivalent(SaveSlotData[] expected, SaveSlotData[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                AssertSlotEquivalent(expected[i], actual[i]);
            }
        }

        private static void AssertSlotEquivalent(SaveSlotData expected, SaveSlotData actual)
        {
            Assert.That(actual.SlotNumber, Is.EqualTo(expected.SlotNumber));
            Assert.That(actual.CurrentStageId, Is.EqualTo(expected.CurrentStageId));
            Assert.That(actual.CurrentLevelGroupId, Is.EqualTo(expected.CurrentLevelGroupId));
            Assert.That(actual.RemainingChances, Is.EqualTo(expected.RemainingChances));
            Assert.That(actual.CampaignCompleted, Is.EqualTo(expected.CampaignCompleted));
            Assert.That(
                actual.HasNormalCampaignCompletionReceipt,
                Is.EqualTo(expected.HasNormalCampaignCompletionReceipt));
            Assert.That(
                actual.NormalCampaignCompletionReceipt?.Version,
                Is.EqualTo(expected.NormalCampaignCompletionReceipt?.Version));
            Assert.That(
                actual.NormalCampaignCompletionReceipt?.CompletedStageId,
                Is.EqualTo(expected.NormalCampaignCompletionReceipt?.CompletedStageId));
            Assert.That(
                actual.NormalCampaignCompletionReceipt?.StageRunId,
                Is.EqualTo(expected.NormalCampaignCompletionReceipt?.StageRunId));
            Assert.That(
                actual.NormalCampaignCompletionReceipt?.ClearSource,
                Is.EqualTo(expected.NormalCampaignCompletionReceipt?.ClearSource));
            Assert.That(actual.IntroComicCompleted, Is.EqualTo(expected.IntroComicCompleted));
            Assert.That(actual.OutroComicCompleted, Is.EqualTo(expected.OutroComicCompleted));
            Assert.That(actual.TotalDeaths, Is.EqualTo(expected.TotalDeaths));
            Assert.That(actual.LastPlayedAt, Is.EqualTo(expected.LastPlayedAt));
            Assert.That(
                actual.StageClearProfileSnapshot.Version,
                Is.EqualTo(expected.StageClearProfileSnapshot.Version));
            Assert.That(
                actual.StageClearProfileSnapshot.ClearRecordsByStageId.Count,
                Is.EqualTo(expected.StageClearProfileSnapshot.ClearRecordsByStageId.Count));
            Assert.That(
                actual.StageClearProfileSnapshot.ProcessedStageRunIds,
                Is.EquivalentTo(expected.StageClearProfileSnapshot.ProcessedStageRunIds));
            Assert.That(
                actual.StageClearProfileSnapshot.ProcessedClearAttemptIds,
                Is.EquivalentTo(expected.StageClearProfileSnapshot.ProcessedClearAttemptIds));
        }

        private static void ClearTransientState()
        {
            new TransientCampaignSaveSlotStore(TransientStoreNamespace).ClearAll();
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            private readonly Queue<CampaignProfileLoadResult> _queuedLoadResults = new();

            public CampaignProfileDocument CurrentDocument { get; set; }

            public CampaignProfileDocument SavedDocument { get; private set; }

            public int LoadCount { get; private set; }

            public int SaveCount { get; private set; }

            public int DestructiveSaveCount { get; private set; }

            public void EnqueueLoadResult(CampaignProfileLoadResult result)
            {
                _queuedLoadResults.Enqueue(result);
            }

            public void ResetCounts()
            {
                LoadCount = 0;
                SaveCount = 0;
                DestructiveSaveCount = 0;
                SavedDocument = null;
            }

            public CampaignProfileLoadResult Load()
            {
                LoadCount++;
                if (_queuedLoadResults.Count > 0)
                {
                    return _queuedLoadResults.Dequeue();
                }

                return CurrentDocument == null
                    ? new CampaignProfileLoadResult(CampaignProfileLoadStatus.Missing, null, "missing")
                    : new CampaignProfileLoadResult(CampaignProfileLoadStatus.Loaded, CurrentDocument, "loaded");
            }

            public void Save(CampaignProfileDocument document)
            {
                SaveCount++;
                SavedDocument = document;
                CurrentDocument = document;
            }

            public void SaveDestructive(CampaignProfileDocument document)
            {
                DestructiveSaveCount++;
                SavedDocument = document;
                CurrentDocument = document;
            }
        }

        private sealed class StubRecoveryPort : ICampaignSaveRecoveryPort
        {
            public bool HasPendingReset { get; set; }

            public CampaignSaveResetResult ResetBlockedProfile(CampaignSaveLoadStatus expectedStatus)
            {
                return CampaignSaveResetResult.NotAllowed;
            }

            public CampaignSaveResetResult RetryPendingReset()
            {
                return CampaignSaveResetResult.NotAllowed;
            }
        }

    }
}
