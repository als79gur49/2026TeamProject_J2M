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
    public sealed class NormalCampaignCompletionAchievementIntegrationTests
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
        public void Immediate_ValidCommittedReceipt_MapsOnlyNormalCampaignComplete()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var integration = new NormalCampaignCompletionAchievementIntegration(sink);

            var result = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-3"),
                _resolver,
                CreateValidCompletedSlot(1));

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(sink.EarnCount, Is.EqualTo(1));
            Assert.That(sink.LastAchievementId, Is.EqualTo(GameAchievementIds.NormalCampaignComplete));
        }

        [TestCase("presence-false")]
        [TestCase("present-null")]
        [TestCase("campaign-incomplete")]
        [TestCase("version-zero")]
        [TestCase("future-version")]
        [TestCase("invalid-stage")]
        [TestCase("non-final-stage")]
        public void Immediate_InvalidPersistedReceiptMatrix_EarnsZero(string invalidCase)
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var integration = new NormalCampaignCompletionAchievementIntegration(sink);
            var slot = CreateInvalidCompletedSlot(invalidCase);

            var result = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-3"),
                _resolver,
                slot);

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.InvalidReceipt));
            Assert.That(sink.EarnCount, Is.Zero);
        }

        [Test]
        public void Immediate_V2LegacyPhysicalFields_DoNotAffectEligibility()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var integration = new NormalCampaignCompletionAchievementIntegration(sink);
            var slot = CreateValidCompletedSlot(1);
            slot.NormalCampaignCompletionReceipt.StageRunId = "ignored-v2-legacy-run";
            slot.NormalCampaignCompletionReceipt.ClearSource = 0;

            var result = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-3"),
                _resolver,
                slot);

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(sink.EarnCount, Is.EqualTo(1));
        }

        [Test]
        public void Immediate_NonFinalFactDoesNotReplayHistoricalFinalReceipt()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var integration = new NormalCampaignCompletionAchievementIntegration(sink);
            var historicalSlot = CreateValidCompletedSlot(1);

            var nonFinalResult = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-2"),
                _resolver,
                historicalSlot);

            Assert.That(nonFinalResult, Is.EqualTo(NormalCampaignCompletionAchievementResult.NotAttempted));
            Assert.That(sink.EarnCount, Is.Zero);
        }

        [TestCase(
            AchievementEarnResult.PersistenceFailed,
            NormalCampaignCompletionAchievementResult.PersistenceFailed)]
        [TestCase(
            AchievementEarnResult.UnavailableState,
            NormalCampaignCompletionAchievementResult.ProductUnavailable)]
        [TestCase(
            AchievementEarnResult.InvalidAchievement,
            NormalCampaignCompletionAchievementResult.InvalidAchievement)]
        [TestCase(
            AchievementEarnResult.AlreadyEarned,
            NormalCampaignCompletionAchievementResult.AlreadyEarned)]
        public void Immediate_ProductResult_IsContainedAndMapped(
            AchievementEarnResult earnResult,
            NormalCampaignCompletionAchievementResult expected)
        {
            var integration = new NormalCampaignCompletionAchievementIntegration(
                new RecordingEarningSink(earnResult));

            var result = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-3"),
                _resolver,
                CreateValidCompletedSlot(1));

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Immediate_EarnException_IsContained()
        {
            var integration = new NormalCampaignCompletionAchievementIntegration(
                new RecordingEarningSink(
                    AchievementEarnResult.EarnedNew,
                    new InvalidOperationException("simulated product failure")));

            var result = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-3"),
                _resolver,
                CreateValidCompletedSlot(1));

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.ExceptionContained));
        }

        [Test]
        public void Startup_ValidReceiptAmongMultipleSlots_InvokesSinkOnce()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "slot-one-run"),
                CreateValidCompletedSlot(2, "slot-two-run"),
                SaveSlotData.CreateEmpty(3));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(store.LoadCount, Is.EqualTo(1));
            Assert.That(sink.EarnCount, Is.EqualTo(1));
            Assert.That(sink.LastAchievementId, Is.EqualTo(GameAchievementIds.NormalCampaignComplete));
        }

        [TestCase("presence-false")]
        [TestCase("present-null")]
        [TestCase("campaign-incomplete")]
        [TestCase("version-zero")]
        [TestCase("future-version")]
        [TestCase("invalid-stage")]
        [TestCase("non-final-stage")]
        public void Startup_InvalidOrBareReceiptMatrix_EarnsZero(string invalidCase)
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateInvalidCompletedSlot(invalidCase));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.NotAttempted));
            Assert.That(sink.EarnCount, Is.Zero);
            Assert.That(store.MutationCount, Is.Zero);
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

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.DirectPlayExcluded));
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

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.ProfileUnavailable));
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

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.ProfileUnavailable));
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

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(sink.EarnCount, Is.EqualTo(1));
        }

        [Test]
        public void Startup_ValidV1Receipt_RemainsRecoverable()
        {
            var sink = new RecordingEarningSink(AchievementEarnResult.EarnedNew);
            var reconciler = CreateReconciler(sink);
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidV1CompletedSlot(1));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
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

            Assert.That(first, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(second, Is.EqualTo(NormalCampaignCompletionAchievementResult.AlreadyReconciled));
            Assert.That(store.LoadCount, Is.EqualTo(1));
            Assert.That(sink.EarnCount, Is.EqualTo(1));
        }

        [Test]
        public void Startup_CrashRecovery_WritesEarnedAndPendingWithUnavailablePublisher()
        {
            using var host = ProductAchievementApplicationHost.CreateForSaveRoot(_saveRoot);
            Assert.That(host.Initialize(), Is.True);
            var reconciler = new NormalCampaignCompletionAchievementStartupReconciler(
                new NormalCampaignCompletionAchievementIntegration(host.EarningSink));
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "durable-crash-recovery-run"));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);
            var snapshot = host.Coordinator.GetSnapshot();

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(snapshot.EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.PendingAchievementPublicationIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.EarnedAchievementIds[0], Is.EqualTo(GameAchievementIds.NormalCampaignComplete));
            Assert.That(snapshot.PendingAchievementPublicationIds[0], Is.EqualTo(GameAchievementIds.NormalCampaignComplete));
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
            campaignStore.UpdateSlot(
                1,
                slot =>
                {
                    slot.CurrentStageId = StageId.CreateOrThrow("stage-4-3");
                    slot.CurrentLevelGroupId = "level-4";
                    slot.CampaignCompleted = true;
                    slot.HasNormalCampaignCompletionReceipt = true;
                    slot.NormalCampaignCompletionReceipt =
                        CreateValidCompletedSlot(1, "canonical-profile-run")
                            .NormalCampaignCompletionReceipt;
                });
            Assert.That(File.Exists(Path.Combine(_saveRoot, "profile.json")), Is.True);
            Assert.That(File.Exists(AchievementPath), Is.False);

            using var host = ProductAchievementApplicationHost.CreateForSaveRoot(_saveRoot);
            Assert.That(host.Initialize(), Is.True);
            var reconciler = new NormalCampaignCompletionAchievementStartupReconciler(
                new NormalCampaignCompletionAchievementIntegration(host.EarningSink));

            var result = reconciler.Reconcile(
                campaignStore,
                _resolver,
                EditorDirectPlayContext.None);
            var snapshot = host.Coordinator.GetSnapshot();

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(snapshot.EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.PendingAchievementPublicationIds.Count, Is.EqualTo(1));
            Assert.That(File.Exists(AchievementPath), Is.True);
        }

        [Test]
        public void Startup_AlreadyEarnedReceipt_CreatesNoDuplicateDurableSave()
        {
            var repository = new RecordingRepository(new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { GameAchievementIds.NormalCampaignComplete.Value },
                PendingAchievementPublicationIds = new[] { GameAchievementIds.NormalCampaignComplete.Value },
            });
            using var coordinator = new ProductAchievementCoordinator(
                repository,
                GameAchievementCatalog.Production,
                new UnavailableAchievementPublicationSink());
            Assert.That(coordinator.Initialize(), Is.True);
            var reconciler = new NormalCampaignCompletionAchievementStartupReconciler(
                new NormalCampaignCompletionAchievementIntegration(coordinator));
            var store = new RecordingCampaignStore(
                CampaignSaveLoadStatus.Loaded,
                CreateValidCompletedSlot(1, "already-earned-run"));

            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.AlreadyEarned));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(
                coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void ImmediateAcrossDifferentSlots_ProductGlobalLedgerRemainsOneEarnedOnePending()
        {
            using var host = ProductAchievementApplicationHost.CreateForSaveRoot(_saveRoot);
            Assert.That(host.Initialize(), Is.True);
            var integration = new NormalCampaignCompletionAchievementIntegration(host.EarningSink);

            var first = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-3"),
                _resolver,
                CreateValidCompletedSlot(1, "slot-one-receipt"));
            var second = integration.TryEarnAfterCommittedCompletion(
                CreateCompletionFact("stage-4-3"),
                _resolver,
                CreateValidCompletedSlot(2, "slot-two-receipt"));
            var snapshot = host.Coordinator.GetSnapshot();

            Assert.That(first, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(second, Is.EqualTo(NormalCampaignCompletionAchievementResult.AlreadyEarned));
            Assert.That(snapshot.EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.PendingAchievementPublicationIds.Count, Is.EqualTo(1));
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

        private string AchievementPath => Path.Combine(
            _saveRoot,
            FileProductAchievementRepository.AchievementFileName);

        private NormalCampaignCompletionAchievementStartupReconciler CreateReconciler(
            IProductAchievementEarningSink sink)
        {
            return new NormalCampaignCompletionAchievementStartupReconciler(
                new NormalCampaignCompletionAchievementIntegration(sink));
        }

        private static NormalCampaignCompletionFact CreateCompletionFact(
            string stageId)
        {
            return new NormalCampaignCompletionFact(StageId.CreateOrThrow(stageId));
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

        private static SaveSlotData CreateInvalidCompletedSlot(string invalidCase)
        {
            var slot = CreateValidCompletedSlot(1, "valid-run");
            switch (invalidCase)
            {
                case "presence-false":
                    slot.HasNormalCampaignCompletionReceipt = false;
                    break;
                case "present-null":
                    slot.NormalCampaignCompletionReceipt = null;
                    break;
                case "campaign-incomplete":
                    slot.CampaignCompleted = false;
                    break;
                case "version-zero":
                    slot.NormalCampaignCompletionReceipt.Version = 0;
                    break;
                case "future-version":
                    slot.NormalCampaignCompletionReceipt.Version =
                        NormalCampaignCompletionReceipt.CurrentVersion + 1;
                    break;
                case "invalid-stage":
                    slot.NormalCampaignCompletionReceipt.CompletedStageId = "invalid stage";
                    break;
                case "non-final-stage":
                    slot.NormalCampaignCompletionReceipt.CompletedStageId = "stage-4-2";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, null);
            }

            return slot;
        }

        private static SaveSlotData CreateValidV1CompletedSlot(int slotNumber)
        {
            var slot = CreateValidCompletedSlot(slotNumber);
            slot.NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
            {
                Version = NormalCampaignCompletionReceipt.LegacyVersion,
                CompletedStageId = "stage-4-3",
                StageRunId = "persisted-v1-run",
                ClearSource = 0,
            };
            return slot;
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

        private sealed class RecordingCampaignStore : ICampaignSaveSlotStore
        {
            private readonly CampaignSaveLoadStatus _status;
            private readonly SaveSlotData[] _slots;

            public RecordingCampaignStore(
                CampaignSaveLoadStatus status,
                params SaveSlotData[] slots)
            {
                _status = status;
                _slots = slots ?? Array.Empty<SaveSlotData>();
            }

            public string DiagnosticsKey => nameof(RecordingCampaignStore);

            public CampaignSaveLoadReport LastCampaignLoadReport => CreateReport();

            public int LoadCount { get; private set; }

            public int MutationCount { get; private set; }

            public Exception LoadException { get; set; }

            public SaveSlotData[] LoadAll()
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

                var clones = new SaveSlotData[_slots.Length];
                for (var i = 0; i < _slots.Length; i++)
                {
                    clones[i] = _slots[i]?.Clone();
                }

                return new CampaignSaveLoadResult(clones, CreateReport());
            }

            public SaveSlotData LoadSlot(int slotNumber)
            {
                throw new NotSupportedException();
            }

            public void SaveSlot(SaveSlotData slot)
            {
                MutationCount++;
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                MutationCount++;
                return SaveSlotData.CreateEmpty(slotNumber);
            }

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
            {
                MutationCount++;
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
