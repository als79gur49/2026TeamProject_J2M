using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Product.Achievements.CampaignIntegration;
using Game.Product.Achievements.Composition;
using Game.Product.Achievements.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class CampaignStageAchievementStartupTests
    {
        private string _saveRoot;
        private CampaignStageSequenceDefinition _sequenceDefinition;
        private CampaignStageSequenceResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(
                Path.GetTempPath(),
                "j2m-campaign-achievement-" + Guid.NewGuid().ToString("N"),
                "Saves");
            _sequenceDefinition = CreateSequenceDefinition();
            _resolver = new CampaignStageSequenceResolver(_sequenceDefinition);
            ProductAchievementEarningSinkHandoff.ResetForTests();
            if (_sequenceDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(_sequenceDefinition);
            }
        }

        [TearDown]
        public void TearDown()
        {
            ProductAchievementEarningSinkHandoff.ResetForTests();
            if (Directory.Exists(Path.GetDirectoryName(_saveRoot)))
            {
                Directory.Delete(Path.GetDirectoryName(_saveRoot), recursive: true);
            }
        }

        [Test]
        public void Startup_ClearedRecordsAmongMultipleSlots_AreAllEvaluated()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "slot-one-run"),
                CreateValidCompletedSlot(2, "slot-two-run"),
                SaveSlotData.CreateEmpty(3));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.Completed));
            Assert.That(store.LoadCount, Is.EqualTo(1));
            Assert.That(sink.EarnCount, Is.EqualTo(2));
            Assert.That(sink.LastAchievementId, Is.EqualTo(GameAchievementIds.CampaignLevel4Clear));
        }

        [TestCase(EditorDirectPlayMode.NonCampaign)]
        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        public void Startup_EveryDirectPlayMode_SkipsCanonicalProfileRead(
            EditorDirectPlayMode mode)
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "production-valid-run"));
            var context = new EditorDirectPlayContext(
                mode,
                StageId.CreateOrThrow("stage-4-3"),
                3,
                suppressCampaignFlow: false);

            var result = reconciler.Reconcile(store, _resolver, context);

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.DirectPlayExcluded));
            Assert.That(store.LoadCount, Is.Zero);
            Assert.That(sink.EarnCount, Is.Zero);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        public void Startup_UnusableProfileStatus_EarnsAndMutatesZero(
            CampaignSaveLoadStatus status)
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                status,
                CreateValidCompletedSlot(1, "must-not-be-used"));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.ProfileUnavailable));
            Assert.That(sink.EarnCount, Is.Zero);
            Assert.That(store.MutationCount, Is.Zero);
        }

        [Test]
        public void Startup_MigrationOrLoadException_IsContained()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "must-not-be-used"))
            {
                LoadException = new IOException("simulated migration failure"),
            };

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.ProfileUnavailable));
            Assert.That(sink.EarnCount, Is.Zero);
        }

        [Test]
        public void Startup_BackupRecoveredProfile_EarnsOnce()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.BackupRecovered,
                CreateValidCompletedSlot(1, "backup-run"));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.Completed));
            Assert.That(sink.EarnCount, Is.EqualTo(1));
        }

        [Test]
        public void Startup_ReadinessReplay_IsSessionBoundedToOneScan()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "one-shot-run"));

            var first = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);
            var second = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(first, Is.EqualTo(CampaignStageAchievementReconciliationResult.Completed));
            Assert.That(second, Is.EqualTo(CampaignStageAchievementReconciliationResult.AlreadyReconciled));
            Assert.That(store.LoadCount, Is.EqualTo(1));
            Assert.That(sink.EarnCount, Is.EqualTo(1));
        }

        [Test]
        public void Startup_CrashRecovery_WritesEarnedAndPendingWithUnavailablePublisher()
        {
            using var host = ProductAchievementApplicationHost.CreateForSaveRoot(_saveRoot);
            Assert.That(host.Initialize(), Is.True);
            var reconciler = new CampaignStageAchievementStartupReconciler(
                new CampaignStageAchievementIntegration(host.EarningSink));
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "durable-crash-recovery-run"));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);
            var snapshot = host.Coordinator.GetSnapshot();

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.Completed));
            Assert.That(snapshot.EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.PendingAchievementPublicationIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.EarnedAchievementIds[0], Is.EqualTo(GameAchievementIds.CampaignLevel4Clear));
            Assert.That(snapshot.PendingAchievementPublicationIds[0], Is.EqualTo(GameAchievementIds.CampaignLevel4Clear));
            Assert.That(File.Exists(AchievementPath), Is.True);
        }

        [Test]
        public void Startup_CanonicalCampaignProfile_CrashRecoveryUsesPublicSaveBoundary()
        {
            var campaignStore = new CampaignSaveSlotStoreAdapter(
                new CampaignSaveService(
                    new FileCampaignProfileRepository(
                        new AtomicTextFileStore(_saveRoot))));
            campaignStore.InitializeNewGame(1, _resolver, "2026-08-10T00:00:00.0000000Z");
            var finalStageId = StageId.CreateOrThrow("stage-4-3");
            campaignStore.SetActiveStageForDiagnostics(
                1,
                finalStageId,
                _resolver.GetLevelGroupId(finalStageId));
            campaignStore.CommitStageClear(
                1,
                new CampaignStageClearCommitRequest
                {
                    Plan = new CampaignProgressionTransitionPlanner(_resolver)
                        .PlanStageClear(finalStageId),
                    PerformanceRecord = new NormalStagePerformanceRecord
                    {
                        StageId = finalStageId,
                        BestCombinedPushFlipUses = 30,
                    },
                    CompletionReceipt = new NormalCampaignCompletionReceipt
                    {
                        Version = NormalCampaignCompletionReceipt.CurrentVersion,
                        CompletedStageId = finalStageId.Value,
                    },
                });
            Assert.That(File.Exists(Path.Combine(_saveRoot, "profile.json")), Is.True);
            Assert.That(File.Exists(AchievementPath), Is.False);

            using var host = ProductAchievementApplicationHost.CreateForSaveRoot(_saveRoot);
            Assert.That(host.Initialize(), Is.True);
            var reconciler = new CampaignStageAchievementStartupReconciler(
                new CampaignStageAchievementIntegration(host.EarningSink));

            var result = reconciler.Reconcile(
                campaignStore,
                _resolver,
                EditorDirectPlayContext.None);
            var snapshot = host.Coordinator.GetSnapshot();

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.Completed));
            Assert.That(snapshot.EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.PendingAchievementPublicationIds.Count, Is.EqualTo(1));
            Assert.That(File.Exists(AchievementPath), Is.True);
        }

        [Test]
        public void Startup_AlreadyEarnedStage_CreatesNoDuplicateDurableSave()
        {
            var repository = new RecordingRepository(new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                PendingAchievementPublicationIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
            });
            using var coordinator = new ProductAchievementCoordinator(
                repository,
                GameAchievementCatalog.Production,
                new UnavailableAchievementPublicationSink());
            Assert.That(coordinator.Initialize(), Is.True);
            var reconciler = new CampaignStageAchievementStartupReconciler(
                new CampaignStageAchievementIntegration(coordinator));
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "already-earned-run"));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.Completed));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(
                coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void Handoff_DifferentSecondSinkFailsClosedAndExactOwnerClearIsStable()
        {
            var first = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var second = new RecordingEarningSink(AchievementEarnResult.EarnedNew);

            Assert.That(ProductAchievementEarningSinkHandoff.TryRegister(first), Is.True);
            Assert.That(ProductAchievementEarningSinkHandoff.TryRegister(first), Is.True);
            Assert.That(ProductAchievementEarningSinkHandoff.TryRegister(second), Is.False);
            Assert.That(ProductAchievementEarningSinkHandoff.TryGet(out var observed), Is.True);
            Assert.That(observed, Is.SameAs(first));

            ProductAchievementEarningSinkHandoff.Clear(second);
            Assert.That(ProductAchievementEarningSinkHandoff.TryGet(out observed), Is.True);
            Assert.That(observed, Is.SameAs(first));

            ProductAchievementEarningSinkHandoff.Clear(first);
            Assert.That(ProductAchievementEarningSinkHandoff.TryGet(out _), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Startup_ReceiptWithoutNormalStageRecord_DoesNotGrantAnyLevel(int version)
        {
            var slot = CreateValidCompletedSlot(1);
            slot.NormalStagePerformanceRecords = Array.Empty<NormalStagePerformanceRecord>();
            slot.NormalCampaignCompletionReceipt.Version = version;
            if (version == 1)
            {
                slot.NormalCampaignCompletionReceipt.StageRunId = "old-run";
                slot.NormalCampaignCompletionReceipt.ClearSource = 0;
            }
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var store = new RecordingCampaignStore(CampaignSaveLoadStatus.Loaded, slot);
            CreateReconciler(sink).Reconcile(store, _resolver, EditorDirectPlayContext.None);
            Assert.That(sink.EarnCount, Is.Zero);
        }

        [Test]
        public void Startup_MultipleSlotsAndRestarts_DeduplicateInProductLedger()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            using var coordinator = new ProductAchievementCoordinator(repository,
                GameAchievementCatalog.Production, new UnavailableAchievementPublicationSink());
            Assert.That(coordinator.Initialize(), Is.True);
            var store = new RecordingCampaignStore(CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1), CreateValidCompletedSlot(2));
            CreateReconciler(coordinator).Reconcile(store, _resolver, EditorDirectPlayContext.None);
            CreateReconciler(coordinator).Reconcile(store, _resolver, EditorDirectPlayContext.None);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds,
                Is.EqualTo(new[] { GameAchievementIds.CampaignLevel4Clear }));
        }

        [Test]
        public void Startup_NormalClearRecordEarnsNewLevelWhileOldAchievementRemainsInactive()
        {
            var repository = new RecordingRepository(new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { "campaign.complete" },
                PendingAchievementPublicationIds = new[] { "campaign.complete" },
            });
            using var coordinator = new ProductAchievementCoordinator(repository,
                GameAchievementCatalog.Production, new UnavailableAchievementPublicationSink());
            Assert.That(coordinator.Initialize(), Is.True);
            Assert.That(repository.SaveCount, Is.Zero);
            var store = new RecordingCampaignStore(CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1));
            CreateReconciler(coordinator).Reconcile(store, _resolver, EditorDirectPlayContext.None);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds, Is.EquivalentTo(new[]
            {
                GameAchievementId.Require("campaign.complete"),
                GameAchievementIds.CampaignLevel4Clear,
            }));
            Assert.That(coordinator.GetSnapshot().PendingAchievementPublicationIds, Is.EquivalentTo(new[]
            {
                GameAchievementId.Require("campaign.complete"),
                GameAchievementIds.CampaignLevel4Clear,
            }));
        }

        private string AchievementPath => Path.Combine(
            _saveRoot,
            FileProductAchievementRepository.AchievementFileName);

        private CampaignStageAchievementStartupReconciler CreateReconciler(
            IProductAchievementEarningSink sink)
        {
            return new CampaignStageAchievementStartupReconciler(
                new CampaignStageAchievementIntegration(sink));
        }

        private static SaveSlotData CreateValidCompletedSlot(
            int slotNumber,
            string ignoredLegacyStageRunId = null)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow("stage-4-3"),
                CurrentLevelGroupId = "level-4",
                NormalStagePerformanceRecords = new[]
                {
                    new NormalStagePerformanceRecord
                    {
                        StageId = StageId.CreateOrThrow("stage-4-3"),
                        BestCombinedPushFlipUses = 30,
                    },
                },
                CampaignCompleted = true,
                HasNormalCampaignCompletionReceipt = true,
                NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
                {
                    Version = NormalCampaignCompletionReceipt.CurrentVersion,
                    CompletedStageId = "stage-4-3",
                    StageRunId = string.Empty,
                    ClearSource = -1,
                },
            };
        }

        private static CampaignStageSequenceDefinition CreateSequenceDefinition()
        {
            var definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            definition.SetEntries(new[]
            {
                CreateSequenceEntry("stage-4-2", "level-4"),
                CreateSequenceEntry("stage-4-3", "level-4"),
            });
            return definition;
        }

        private static CampaignStageSequenceEntry CreateSequenceEntry(
            string stageId,
            string levelGroupId)
        {
            var entry = new CampaignStageSequenceEntry();
            entry.Set(StageId.CreateOrThrow(stageId), levelGroupId);
            return entry;
        }

        private sealed class RecordingEarningSink : IProductAchievementEarningSink
        {
            private readonly AchievementEarnResult _result;
            private readonly Exception _exception;

            public RecordingEarningSink(
                AchievementEarnResult result,
                Exception exception = null)
            {
                _result = result;
                _exception = exception;
            }

            public int EarnCount { get; private set; }

            public GameAchievementId LastAchievementId { get; private set; }

            public AchievementEarnResult Earn(GameAchievementId achievementId)
            {
                EarnCount++;
                LastAchievementId = achievementId;
                if (_exception != null)
                {
                    throw _exception;
                }

                return _result;
            }

            public AchievementEarnBatchResult EarnBatch(
                IReadOnlyList<GameAchievementId> achievementIds)
            {
                var newlyEarned = new GameAchievementId[achievementIds.Count];
                for (var i = 0; i < achievementIds.Count; i++)
                {
                    Earn(achievementIds[i]);
                    newlyEarned[i] = achievementIds[i];
                }

                return new AchievementEarnBatchResult(_result, newlyEarned);
            }
        }

        private sealed class RecordingCampaignStore : ICampaignSaveQuery
        {
            private readonly CampaignSaveLoadStatus _status;
            private readonly CampaignSlotEntry[] _slots;

            public RecordingCampaignStore(
                CampaignSaveLoadStatus status,
                params SaveSlotData[] slots)
            {
                _status = status;
                var source = slots ?? Array.Empty<SaveSlotData>();
                _slots = new CampaignSlotEntry[source.Length];
                for (var index = 0; index < source.Length; index++)
                {
                    _slots[index] = CreateEntry(source[index]);
                }
            }

            public string DiagnosticsKey => nameof(RecordingCampaignStore);

            public CampaignSaveLoadReport LastCampaignLoadReport => CreateReport();

            public int LoadCount { get; private set; }

            public int MutationCount { get; private set; }

            public Exception LoadException { get; set; }

            public CampaignSlotEntry[] LoadAll()
            {
                return LoadAllWithReport().Slots;
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                LoadCount++;
                if (LoadException != null)
                {
                    throw LoadException;
                }

                return new CampaignSaveLoadResult(_slots, CreateReport());
            }

            public CampaignSlotEntry LoadSlot(int slotNumber)
            {
                throw new NotSupportedException();
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                MutationCount++;
                return SaveSlotData.CreateEmpty(slotNumber);
            }

            public void DeleteSlot(int slotNumber)
            {
                MutationCount++;
            }

            public void ClearAll()
            {
                MutationCount++;
            }

            private CampaignSaveLoadReport CreateReport()
            {
                return new CampaignSaveLoadReport(
                    _status,
                    "recording campaign profile status",
                    nameof(RecordingCampaignStore));
            }
        }

        private static CampaignSlotState CreateState(SaveSlotData slot)
        {
            return CreateEntry(slot).State;
        }

        private static CampaignSlotEntry CreateEntry(SaveSlotData slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                return CampaignSlotStateFactory.CreateEmptyEntry(slot?.SlotNumber ?? 1);
            }

            return CampaignSlotRawDataMapper.ToEntry(slot);
        }

        private sealed class RecordingRepository : IAchievementDocumentRepository
        {
            private readonly ProductAchievementDocument _document;

            public RecordingRepository(ProductAchievementDocument document)
            {
                _document = document;
            }

            public int SaveCount { get; private set; }

            public AchievementDocumentLoadResult Load()
            {
                return new AchievementDocumentLoadResult(
                    AchievementDocumentLoadStatus.Loaded,
                    _document,
                    "recording repository loaded");
            }

            public AchievementDocumentSaveResult Save(ProductAchievementDocument document)
            {
                SaveCount++;
                return AchievementDocumentSaveResult.Saved();
            }
        }

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }
    }
}
