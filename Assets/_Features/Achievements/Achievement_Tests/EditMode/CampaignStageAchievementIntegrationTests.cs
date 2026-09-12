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
            SetSequence(("stage-0-1", "level-0"), ("stage-0-3", "level-0"),
                ("stage-1-1", "level-1"), ("stage-1-2", "level-1"),
                ("stage-2-1", "level-2"), ("stage-2-2", "level-2"),
                ("stage-3-1", "level-3"), ("stage-3-3", "level-3"),
                ("stage-4-1", "level-4"), ("stage-4-3", "level-4"));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_definition);
        }

        [TestCase(0, "stage-0-3")]
        [TestCase(1, "stage-1-2")]
        [TestCase(2, "stage-2-2")]
        [TestCase(3, "stage-3-3")]
        [TestCase(4, "stage-4-3")]
        public void LevelFinalClear_EarnsOnlyItsLevel(int level, string stage)
        {
            var sink = new RecordingSink();
            new CampaignStageAchievementIntegration(sink).TryEarnFromCommittedSlot(
                Slot(CreateRecord(stage, 100)), _resolver);
            Assert.That(sink.BatchCount, Is.EqualTo(1));
            Assert.That(sink.Ids, Is.EqualTo(new[]
            {
                GameAchievementId.Require($"campaign.level-{level}.clear"),
            }));
        }

        [TestCase("stage-0-1")]
        [TestCase("stage-1-1")]
        [TestCase("stage-2-1")]
        [TestCase("stage-3-1")]
        [TestCase("stage-4-1")]
        [TestCase("stage-9-9")]
        public void IntermediateOrUnsequencedClear_EarnsNothing(string stage)
        {
            var sink = new RecordingSink();
            new CampaignStageAchievementIntegration(sink).TryEarnFromCommittedSlot(
                Slot(CreateRecord(stage, 0)), _resolver);
            Assert.That(sink.BatchCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(25)]
        [TestCase(26)]
        [TestCase(100)]
        public void LevelClear_HasNoPushFlipLimit(int count)
        {
            var sink = new RecordingSink();
            new CampaignStageAchievementIntegration(sink).TryEarnFromCommittedSlot(
                Slot(CreateRecord("stage-1-2", count)), _resolver);
            Assert.That(sink.Ids, Is.EqualTo(new[] { GameAchievementIds.CampaignLevel1Clear }));
        }

        [Test]
        public void AuthoredOrderAndGroup_OwnFinalityIncludingNoncontiguousGroups()
        {
            SetSequence(("last-by-name", "level-4"), ("middle", "level-0"),
                ("first-by-name", "level-4"), ("unrelated", "level-9"));
            var sink = new RecordingSink();
            var integration = new CampaignStageAchievementIntegration(sink);
            integration.TryEarnFromCommittedSlot(Slot(CreateRecord("last-by-name", 0)), _resolver);
            Assert.That(sink.Ids, Is.Empty);
            integration.TryEarnFromCommittedSlot(Slot(CreateRecord("first-by-name", 0)), _resolver);
            Assert.That(sink.Ids, Is.EqualTo(new[] { GameAchievementIds.CampaignLevel4Clear }));
        }

        [Test]
        public void AppendedStage_ReevaluatesUnawardedHistoryAgainstCurrentFinalStage()
        {
            SetSequence(("stage-1-2", "level-1"), ("new-ending", "level-1"));
            var sink = new RecordingSink();
            var integration = new CampaignStageAchievementIntegration(sink);
            integration.TryEarnFromCommittedSlot(Slot(CreateRecord("stage-1-2", 0)), _resolver);
            Assert.That(sink.Ids, Is.Empty);
            integration.TryEarnFromCommittedSlot(Slot(CreateRecord("new-ending", 0)), _resolver);
            Assert.That(sink.Ids, Is.EqualTo(new[] { GameAchievementIds.CampaignLevel1Clear }));
        }

        [Test]
        public void AllFiveLevelRecords_AreSubmittedInOneBatch()
        {
            var sink = new RecordingSink();
            new CampaignStageAchievementIntegration(sink).TryEarnFromCommittedSlot(
                Slot(CreateRecord("stage-0-3", 0), CreateRecord("stage-1-2", 0),
                    CreateRecord("stage-2-2", 0), CreateRecord("stage-3-3", 0),
                    CreateRecord("stage-4-3", 0)), _resolver);
            Assert.That(sink.BatchCount, Is.EqualTo(1));
            Assert.That(sink.Ids, Is.EqualTo(new[]
            {
                GameAchievementIds.CampaignLevel0Clear, GameAchievementIds.CampaignLevel1Clear,
                GameAchievementIds.CampaignLevel2Clear, GameAchievementIds.CampaignLevel3Clear,
                GameAchievementIds.CampaignLevel4Clear,
            }));
        }

        [Test]
        public void MissingGroupOrResolver_DoesNotInventAnAchievement()
        {
            SetSequence(("stage-1-2", "level-9"));
            var sink = new RecordingSink();
            var integration = new CampaignStageAchievementIntegration(sink);
            integration.TryEarnFromCommittedSlot(Slot(CreateRecord("stage-1-2", 0)), _resolver);
            integration.TryEarnFromCommittedSlot(Slot(CreateRecord("stage-1-2", 0)), null);
            integration.TryEarnFromCommittedSlot(null, _resolver);
            Assert.That(sink.BatchCount, Is.Zero);
        }

        [Test]
        public void ProductionSequence_EarnsExactlyTheFiveDocumentedLevelEndings()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/Catalog/CampaignMain_StageSequence.asset");
            Assert.That(definition, Is.Not.Null);
            var resolver = new CampaignStageSequenceResolver(definition);
            var endings = new[] { "stage-0-3", "stage-1-2", "stage-2-2", "stage-3-3", "stage-4-3" };
            for (var level = 0; level < endings.Length; level++)
            {
                var sink = new RecordingSink();
                new CampaignStageAchievementIntegration(sink).TryEarnFromCommittedSlot(
                    Slot(CreateRecord(endings[level], 0)), resolver);
                Assert.That(sink.Ids, Is.EqualTo(new[]
                {
                    GameAchievementId.Require($"campaign.level-{level}.clear"),
                }));
            }
        }

        [Test]
        public void StartupReconciliation_ReplaysPersistedStageRecordsWithoutSlotMutation()
        {
            var sink = new RecordingSink();
            var reconciler = new CampaignStageAchievementStartupReconciler(
                new CampaignStageAchievementIntegration(sink));
            var store = new ReadOnlyCampaignStore(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-2"),
                CurrentLevelGroupId = "level-1",
                NormalStagePerformanceRecords = new[] { CreateRecord("stage-1-2", 26) },
            });
            var result = reconciler.Reconcile(store, _resolver, EditorDirectPlayContext.None);
            Assert.That(result, Is.EqualTo(CampaignStageAchievementReconciliationResult.Completed));
            Assert.That(sink.Ids, Is.EqualTo(new[] { GameAchievementIds.CampaignLevel1Clear }));
            Assert.That(store.LoadCount, Is.EqualTo(1));
            Assert.That(store.MutationCount, Is.Zero);
        }

        private void SetSequence(params (string stage, string group)[] values)
        {
            var entries = new List<CampaignStageSequenceEntry>();
            foreach (var value in values)
            {
                var entry = new CampaignStageSequenceEntry();
                entry.Set(StageId.CreateOrThrow(value.stage), value.group);
                entries.Add(entry);
            }
            _definition.SetEntries(entries.ToArray());
            _resolver = new CampaignStageSequenceResolver(_definition);
        }

        private static CampaignSlotState Slot(params NormalStagePerformanceRecord[] records)
        {
            return CreateState(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-0-1"),
                CurrentLevelGroupId = "level-0",
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                NormalStagePerformanceRecords = records,
            });
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
