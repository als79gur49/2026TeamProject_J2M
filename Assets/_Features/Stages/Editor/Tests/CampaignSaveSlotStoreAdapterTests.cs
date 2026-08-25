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
        public void SetUp() => ClearTransientState();

        [TearDown]
        public void TearDown() => ClearTransientState();

        [Test]
        public void LoadAll_ProductionAndTransientReturnEquivalentImmutableEntries()
        {
            var transient = CreateTransientStore();
            var adapter = CreateAdapter();
            ImportSeed(transient, 1, "stage-1-1", "level-1", 2);
            ImportSeed(transient, 3, "stage-3-1", "level-3", 1);
            ImportSeed(adapter, 1, "stage-1-1", "level-1", 2);
            ImportSeed(adapter, 3, "stage-3-1", "level-3", 1);

            AssertEntriesEquivalent(transient.LoadAll(), adapter.LoadAll());
            Assert.That(adapter.LastCampaignLoadReport.Status,
                Is.EqualTo(CampaignSaveLoadStatus.Loaded));
        }

        [Test]
        public void LoadSlot_ReturnsImmutableEntryWithImmutableState()
        {
            var adapter = CreateAdapter();
            ImportSeed(adapter, 2, "stage-2-1", "level-2", 1);

            var entry = adapter.LoadSlot(2);

            Assert.That(entry.IsEmpty, Is.False);
            Assert.That(entry.State.SlotNumber, Is.EqualTo(2));
            Assert.That(entry.State.CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(entry.State.RemainingChances, Is.EqualTo(1));
        }

        [Test]
        public void ComicCompletion_ProductionAndTransientMutateOnlyComicProgress()
        {
            var transient = CreateTransientStore();
            var adapter = CreateAdapter();
            ImportSeed(transient, 1, "stage-1-1", "level-1", 2);
            ImportSeed(adapter, 1, "stage-1-1", "level-1", 2);

            transient.MarkIntroComicCompleted(1);
            adapter.MarkIntroComicCompleted(1);
            transient.MarkOutroComicCompleted(1);
            adapter.MarkOutroComicCompleted(1);

            Assert.That(transient.LoadSlot(1).State.IntroComicCompleted, Is.True);
            Assert.That(adapter.LoadSlot(1).State.IntroComicCompleted, Is.True);
            Assert.That(transient.LoadSlot(1).State.OutroComicCompleted, Is.True);
            Assert.That(adapter.LoadSlot(1).State.OutroComicCompleted, Is.True);
            Assert.That(adapter.LoadSlot(1).State.CurrentStageId.Value,
                Is.EqualTo("stage-1-1"));
        }

        [Test]
        public void PrepareContinue_ProductionAndTransientSynchronizeOnlyLevelGroup()
        {
            var transient = CreateTransientStore();
            var adapter = CreateAdapter();
            ImportSeed(transient, 1, "stage-2-2", "level-5", 2);
            ImportSeed(adapter, 1, "stage-2-2", "level-5", 2);
            var command = new CampaignContinuePreparationCommand(
                1,
                StageId.CreateOrThrow("stage-2-2"),
                "level-5",
                "level-2");

            var transientResult = transient.PrepareContinue(command);
            var productionResult = adapter.PrepareContinue(command);

            Assert.That(transientResult.Succeeded, Is.True);
            Assert.That(productionResult.Succeeded, Is.True);
            Assert.That(transientResult.LevelGroupSynchronized, Is.True);
            Assert.That(productionResult.LevelGroupSynchronized, Is.True);
            AssertEntriesEquivalent(transient.LoadAll(), adapter.LoadAll());
        }

        [Test]
        public void PrepareContinue_StalePreconditionDoesNotWrite()
        {
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            ImportSeed(adapter, 1, "stage-2-2", "level-5", 2);
            repository.ResetCounts();

            var result = adapter.PrepareContinue(new CampaignContinuePreparationCommand(
                1,
                StageId.CreateOrThrow("stage-2-2"),
                "level-4",
                "level-2"));

            Assert.That(result.Status,
                Is.EqualTo(CampaignContinuePreparationStatus.StalePrecondition));
            Assert.That(adapter.LoadSlot(1).State.CurrentLevelGroupId,
                Is.EqualTo("level-5"));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
        }

        [TestCase(3, 2, "stage-1-1", "stage-1-1", "level-1")]
        [TestCase(1, 3, "stage-2-2", "stage-2-1", "level-2")]
        public void DeathCommit_ProductionAndTransientHaveSameChanceContract(
            int initialChances,
            int expectedChances,
            string initialStageId,
            string expectedStageId,
            string levelGroupId)
        {
            var transient = CreateTransientStore();
            var adapter = CreateAdapter();
            var planner = new CampaignProgressionTransitionPlanner(
                CampaignStageSequenceTestAsset.LoadProductionResolver());
            ImportSeed(transient, 1, initialStageId, levelGroupId, initialChances);
            ImportSeed(adapter, 1, initialStageId, levelGroupId, initialChances);

            var transientResult = transient.CommitDeath(
                1,
                planner.PlanDeath(transient.LoadSlot(1).State));
            var productionResult = adapter.CommitDeath(
                1,
                planner.PlanDeath(adapter.LoadSlot(1).State));

            Assert.That(transientResult.Slot.RemainingChances, Is.EqualTo(expectedChances));
            Assert.That(productionResult.Slot.RemainingChances, Is.EqualTo(expectedChances));
            Assert.That(transientResult.Slot.CurrentStageId.Value, Is.EqualTo(expectedStageId));
            Assert.That(productionResult.Slot.CurrentStageId.Value, Is.EqualTo(expectedStageId));
            Assert.That(transientResult.Slot.TotalDeaths,
                Is.EqualTo(productionResult.Slot.TotalDeaths));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1)]
        public void SeedImport_InvalidChancesFailBeforeRepositoryWrite(int invalidChances)
        {
            var repository = new RecordingRepository();
            CreateAdapter(repository);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CampaignSlotSeedImportRequest(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    "level-1",
                    invalidChances,
                    FixedNowUtc));
            Assert.That(repository.LoadCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [TestCase(CampaignProfileLoadStatus.CorruptNoFallback, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.InvalidDocument, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.UnsupportedVersion, CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.Unauthorized, CampaignSaveLoadStatus.Unauthorized)]
        [TestCase(CampaignProfileLoadStatus.IoFailed, CampaignSaveLoadStatus.IoFailed)]
        public void SeedImport_BlockedLoadDoesNotWrite(
            CampaignProfileLoadStatus profileStatus,
            CampaignSaveLoadStatus expectedStatus)
        {
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            repository.EnqueueLoadResult(new CampaignProfileLoadResult(
                profileStatus,
                null,
                "blocked"));

            Assert.Throws<InvalidOperationException>(() =>
                ImportSeed(adapter, 1, "stage-1-1", "level-1", 3));

            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(adapter.LastCampaignLoadReport.Status, Is.EqualTo(expectedStatus));
        }

        [Test]
        public void RecoveryPending_BlocksQueriesAndMutationsWithoutRepositoryAccess()
        {
            var repository = new RecordingRepository();
            var recovery = new StubRecoveryPort { HasPendingReset = true };
            var adapter = CreateAdapter(repository, recovery);
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();

            var diagnostic = adapter.LoadAllWithReport();

            Assert.That(diagnostic.Report.Status,
                Is.EqualTo(CampaignSaveLoadStatus.RecoveryPending));
            Assert.Throws<InvalidOperationException>(() => adapter.LoadAll());
            Assert.Throws<InvalidOperationException>(() => adapter.LoadSlot(1));
            Assert.Throws<InvalidOperationException>(() =>
                ImportSeed(adapter, 1, "stage-1-1", "level-1", 3));
            Assert.Throws<InvalidOperationException>(() => adapter.DeleteSlot(1));
            Assert.Throws<InvalidOperationException>(() => adapter.ClearAll());
            Assert.Throws<InvalidOperationException>(() =>
                adapter.InitializeNewGame(1, resolver, FixedNowUtc));
            Assert.That(repository.LoadCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
        }

        [Test]
        public void DeleteAndClear_ProductionAndTransientRemainEquivalent()
        {
            var transient = CreateTransientStore();
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            ImportSeed(transient, 1, "stage-1-1", "level-1", 3);
            ImportSeed(transient, 2, "stage-2-1", "level-2", 2);
            ImportSeed(adapter, 1, "stage-1-1", "level-1", 3);
            ImportSeed(adapter, 2, "stage-2-1", "level-2", 2);

            transient.DeleteSlot(1);
            adapter.DeleteSlot(1);
            AssertEntriesEquivalent(transient.LoadAll(), adapter.LoadAll());

            transient.ClearAll();
            adapter.ClearAll();
            AssertEntriesEquivalent(transient.LoadAll(), adapter.LoadAll());
            Assert.That(repository.SavedDocument.Slots, Is.Empty);
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

        private static TransientCampaignSaveSlotStore CreateTransientStore() =>
            new(TransientStoreNamespace);

        private static CampaignSaveSlotStoreAdapter CreateAdapter(
            RecordingRepository repository = null,
            ICampaignSaveRecoveryPort recoveryPort = null)
        {
            repository ??= new RecordingRepository();
            return new CampaignSaveSlotStoreAdapter(
                new CampaignSaveService(
                    repository,
                    () => FixedNowUtc,
                    "adapter-test-profile",
                    "adapter-test-product"),
                recoveryPort);
        }

        private static CampaignSlotState ImportSeed(
            ICampaignSlotSeedImportPort store,
            int slotNumber,
            string stageId,
            string levelGroupId,
            int remainingChances)
        {
            return store.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                slotNumber,
                StageId.CreateOrThrow(stageId),
                levelGroupId,
                remainingChances,
                FixedNowUtc));
        }

        private static void AssertEntriesEquivalent(
            CampaignSlotEntry[] expected,
            CampaignSlotEntry[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].SlotNumber, Is.EqualTo(expected[index].SlotNumber));
                Assert.That(actual[index].IsEmpty, Is.EqualTo(expected[index].IsEmpty));
                if (expected[index].IsEmpty)
                {
                    continue;
                }

                Assert.That(actual[index].State.CurrentStageId,
                    Is.EqualTo(expected[index].State.CurrentStageId));
                Assert.That(actual[index].State.CurrentLevelGroupId,
                    Is.EqualTo(expected[index].State.CurrentLevelGroupId));
                Assert.That(actual[index].State.RemainingChances,
                    Is.EqualTo(expected[index].State.RemainingChances));
                Assert.That(actual[index].State.TotalDeaths,
                    Is.EqualTo(expected[index].State.TotalDeaths));
            }
        }

        private static void ClearTransientState()
        {
            new TransientCampaignSaveSlotStore(TransientStoreNamespace).ClearAll();
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            private readonly Queue<CampaignProfileLoadResult> _queuedLoadResults = new();

            public CampaignProfileDocument CurrentDocument { get; private set; }
            public CampaignProfileDocument SavedDocument { get; private set; }
            public int LoadCount { get; private set; }
            public int SaveCount { get; private set; }
            public int DestructiveSaveCount { get; private set; }

            public void EnqueueLoadResult(CampaignProfileLoadResult result) =>
                _queuedLoadResults.Enqueue(result);

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
                    : new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Loaded,
                        CurrentDocument,
                        "loaded");
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

            public CampaignSaveResetResult ResetBlockedProfile(
                CampaignSaveLoadStatus expectedStatus) => CampaignSaveResetResult.NotAllowed;

            public CampaignSaveResetResult RetryPendingReset() =>
                CampaignSaveResetResult.NotAllowed;
        }
    }
}
