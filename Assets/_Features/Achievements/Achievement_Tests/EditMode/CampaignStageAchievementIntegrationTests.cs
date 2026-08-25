using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Product.Achievements.CampaignIntegration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class CampaignStageAchievementIntegrationTests
    {
        private CampaignStageSequenceDefinition _definition;
        private CampaignStageSequenceResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            var entry = new CampaignStageSequenceEntry();
            entry.Set(StageId.CreateOrThrow("stage-1-2"), "level-1");
            _definition.SetEntries(new[] { entry });
            _resolver = new CampaignStageSequenceResolver(_definition);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_definition);
        }

        [TestCase(24, true)]
        [TestCase(25, true)]
        [TestCase(26, false)]
        public void PushFlipRule_UsesInclusiveTwentyFiveBoundary(
            int combinedUses,
            bool expected)
        {
            var rule = new CampaignStageAchievementRule(
                GameAchievementIds.CampaignStage1_2PushFlipWithin25,
                StageId.CreateOrThrow("stage-1-2"),
                maxCombinedPushFlipUses: 25);
            var readModel = CreateState(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-2"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                NormalStagePerformanceRecords = new[]
                {
                    CreateRecord("stage-1-2", combinedUses),
                },
            }).NormalStagePerformanceRecords[0];

            Assert.That(
                rule.IsSatisfiedBy(readModel),
                Is.EqualTo(expected));
        }

        [Test]
        public void CommittedState_ProvidesCanonicalPerformanceRecordsWithoutRecoveryProjection()
        {
            var readModels = CreateState(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-2"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                NormalStagePerformanceRecords = new[]
                {
                    CreateRecord("stage-1-2", 25),
                    CreateRecord("stage-1-1", 8),
                },
            }).NormalStagePerformanceRecords;

            Assert.That(readModels, Has.Count.EqualTo(2));
            Assert.That(readModels[0].StageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(readModels[0].BestCombinedPushFlipUses, Is.EqualTo(8));
            Assert.That(readModels[1].StageId.Value, Is.EqualTo("stage-1-2"));
            Assert.That(readModels[1].BestCombinedPushFlipUses, Is.EqualTo(25));
        }

        [TestCase(25, 2)]
        [TestCase(26, 1)]
        public void CommittedStageRecord_EarnsClearAndEligibleThresholdAchievements(
            int combinedUses,
            int expectedEarnCount)
        {
            var sink = new RecordingSink();
            var integration = new CampaignStageAchievementIntegration(sink);
            var slot = CreateState(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-2"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                NormalStagePerformanceRecords = new[]
                {
                    CreateRecord("stage-1-2", combinedUses),
                },
            });

            integration.TryEarnFromCommittedSlot(slot, _resolver);

            Assert.That(sink.BatchCount, Is.EqualTo(1));
            Assert.That(sink.Ids, Has.Count.EqualTo(expectedEarnCount));
            Assert.That(sink.Ids, Does.Contain(GameAchievementIds.CampaignStage1_2Clear));
            Assert.That(
                sink.Ids.Contains(GameAchievementIds.CampaignStage1_2PushFlipWithin25),
                Is.EqualTo(combinedUses <= 25));
        }

        [Test]
        public void StartupReconciliation_ReplaysPersistedStageRecordsWithoutSlotMutation()
        {
            var sink = new RecordingSink();
            var stageIntegration = new CampaignStageAchievementIntegration(sink);
            var reconciler = new NormalCampaignCompletionAchievementStartupReconciler(
                new NormalCampaignCompletionAchievementIntegration(sink),
                stageIntegration);
            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-2"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                NormalStagePerformanceRecords = new[]
                {
                    CreateRecord("stage-1-2", 25),
                },
            };
            var store = new ReadOnlyCampaignStore(slot);

            var result = reconciler.Reconcile(
                store,
                _resolver,
                EditorDirectPlayContext.None);

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.NotAttempted));
            Assert.That(sink.Ids, Is.EquivalentTo(new[]
            {
                GameAchievementIds.CampaignStage1_2Clear,
                GameAchievementIds.CampaignStage1_2PushFlipWithin25,
            }));
            Assert.That(store.LoadCount, Is.EqualTo(1));
            Assert.That(store.MutationCount, Is.Zero);
        }

        private static NormalStagePerformanceRecord CreateRecord(
            string stageId,
            int combinedUses)
        {
            return new NormalStagePerformanceRecord
            {
                Version = NormalStagePerformanceRecord.CurrentVersion,
                StageId = StageId.CreateOrThrow(stageId),
                BestCombinedPushFlipUses = combinedUses,
            };
        }

        private sealed class RecordingSink : IProductAchievementEarningSink
        {
            internal List<GameAchievementId> Ids { get; } = new();

            internal int BatchCount { get; private set; }

            public AchievementEarnResult Earn(GameAchievementId achievementId)
            {
                Ids.Add(achievementId);
                return AchievementEarnResult.EarnedNew;
            }

            public AchievementEarnBatchResult EarnBatch(
                IReadOnlyList<GameAchievementId> achievementIds)
            {
                BatchCount++;
                var values = new GameAchievementId[achievementIds.Count];
                for (var i = 0; i < achievementIds.Count; i++)
                {
                    values[i] = achievementIds[i];
                    Ids.Add(achievementIds[i]);
                }

                return new AchievementEarnBatchResult(
                    AchievementEarnResult.EarnedNew,
                    values);
            }
        }

        private sealed class ReadOnlyCampaignStore : ICampaignSaveQuery
        {
            private readonly CampaignSlotEntry _slot;

            internal ReadOnlyCampaignStore(SaveSlotData slot)
            {
                _slot = CreateEntry(slot);
            }

            public string DiagnosticsKey => nameof(ReadOnlyCampaignStore);

            public CampaignSaveLoadReport LastCampaignLoadReport => CreateReport();

            internal int LoadCount { get; private set; }

            internal int MutationCount { get; private set; }

            public CampaignSlotEntry[] LoadAll()
            {
                return LoadAllWithReport().Slots;
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                LoadCount++;
                return new CampaignSaveLoadResult(new[] { _slot }, CreateReport());
            }

            public CampaignSlotEntry LoadSlot(int slotNumber)
            {
                return _slot;
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

            private static CampaignSaveLoadReport CreateReport()
            {
                return new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.Loaded,
                    "loaded",
                    nameof(ReadOnlyCampaignStore));
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
    }
}
