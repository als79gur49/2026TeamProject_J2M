using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSlotTransitionCharacterizationTests
    {
        private const string FixedCommitUtc = "2026-08-24T12:34:56.0000000Z";
        private const string SeedTimestampUtc = "2026-01-02T03:04:05.0000000Z";
        private const string TransientNamespace =
            "Game.Feature.Stages.Tests.CampaignSlotTransitionCharacterization";

        [SetUp]
        public void SetUp()
        {
            new TransientCampaignSaveSlotStore(TransientNamespace).ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            new TransientCampaignSaveSlotStore(TransientNamespace).ClearAll();
        }

        [Test]
        public void Survival_UnchangedHpDoesNotWriteAndNestedMutationCannotReplaceCommittedSlot()
        {
            var seed = CreateFieldRichSlot("stage-1-1", "level-1", 0);
            seed.GameMode = GameMode.Casual; seed.ResumeHp = 3;
            var pair = CreatePair(seed);
            pair.Production.CommitSurvival(1, new CampaignSurvivalCommitRequest(seed.CurrentStageId, 3, 3));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            var secondService = new CampaignSaveService(pair.Repository);
            var rejected = false;
            pair.Repository.BeforeSave = () =>
            {
                Assert.Throws<InvalidOperationException>(() => secondService.DeleteSlot(1));
                rejected = true;
            };
            pair.Production.CommitSurvival(1, new CampaignSurvivalCommitRequest(seed.CurrentStageId, 3, 2));
            Assert.That(rejected, Is.True);
            Assert.That(pair.Repository.SaveCount, Is.EqualTo(1));
            Assert.That(pair.Production.LoadSlot(1).State.ResumeHp, Is.EqualTo(2));
        }

        [TestCase("stage-1-1", false)]
        [TestCase("stage-1-2", false)]
        [TestCase("stage-4-3", false)]
        [TestCase("stage-2-2", true)]
        public void Casual_SurvivalThenClearOrDeathPreservesHistoryAndOtherSlots(string stage, bool death)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var seed = CreateFieldRichSlot(stage, resolver.GetLevelGroupId(StageId.CreateOrThrow(stage)), 0);
            seed.GameMode = GameMode.Casual;
            seed.ResumeHp = 3;
            var pair = CreatePair(seed);
            var transient = new TransientCampaignSaveSlotStore(TransientNamespace);
            transient.ImportSlotSeed(new CampaignSlotSeedImportRequest(1, seed.CurrentStageId,
                seed.CurrentLevelGroupId, 0, SeedTimestampUtc, GameMode.Casual, 3));
            var other = pair.Production.InitializeNewGame(2, resolver, SeedTimestampUtc, GameMode.Hardcore);
            transient.InitializeNewGame(2, resolver, SeedTimestampUtc, GameMode.Hardcore);
            pair.Repository.ResetAccessCounts();
            var survival = new CampaignSurvivalCommitRequest(seed.CurrentStageId, 3, 2);
            var productionHp = pair.Production.CommitSurvival(1, survival).Slot;
            var transientHp = transient.CommitSurvival(1, survival).Slot;
            Assert.That(productionHp.ResumeHp, Is.EqualTo(2));
            Assert.That(transientHp.ResumeHp, Is.EqualTo(2));
            // A fresh query/continue must retain the last committed HP.
            Assert.That(pair.Production.PrepareContinue(new CampaignContinuePreparationCommand(1, seed.CurrentStageId,
                seed.CurrentLevelGroupId, seed.CurrentLevelGroupId)).CommittedState.ResumeHp, Is.EqualTo(2));
            CampaignSlotState committed;
            CampaignSlotState transientCommitted;
            if (death)
            {
                committed = pair.Production.CommitDeath(1, planner.PlanDeath(productionHp)).Slot;
                transientCommitted = transient.CommitDeath(1, planner.PlanDeath(transientHp)).Slot;
                Assert.That(committed.CurrentStageId, Is.EqualTo(resolver.GetFirstStageInLevelGroupOrNone(seed.CurrentLevelGroupId)));
                Assert.That(committed.TotalDeaths, Is.EqualTo(seed.TotalDeaths + 1));
            }
            else
            {
                var request = new CampaignStageClearCommitRequest { Plan = planner.PlanStageClear(seed.CurrentStageId) };
                var result = pair.Production.CommitStageClear(1, request);
                committed = result.Slot;
                transientCommitted = transient.CommitStageClear(1, request).Slot;
                Assert.That(result.PreviousRemainingChances, Is.Null);
                Assert.That(committed.CampaignCompleted, Is.EqualTo(resolver.IsFinal(seed.CurrentStageId)));
                Assert.That(committed.TotalDeaths, Is.EqualTo(seed.TotalDeaths));
            }
            Assert.That(committed.ResumeHp, Is.EqualTo(3));
            Assert.That(committed.RemainingChances, Is.Zero);
            Assert.That(committed.GameMode, Is.EqualTo(GameMode.Casual));
            Assert.That(transientCommitted.ResumeHp, Is.EqualTo(committed.ResumeHp));
            Assert.That(transientCommitted.CurrentStageId, Is.EqualTo(committed.CurrentStageId));
            Assert.That(committed.IntroComicCompleted, Is.True);
            Assert.That(committed.OutroComicCompleted, Is.True);
            Assert.That(committed.Receipt.Payload.StageRunId, Is.EqualTo(seed.NormalCampaignCompletionReceipt.StageRunId));
            Assert.That(SnapshotFingerprint(CampaignSlotRawDataMapper.ToRaw(committed).StageClearProfileSnapshot),
                Is.EqualTo(SnapshotFingerprint(seed.StageClearProfileSnapshot)));
            Assert.That(pair.Production.LoadSlot(2).State.GameMode, Is.EqualTo(other.GameMode));
            Assert.That(pair.Production.LoadSlot(2).State.RemainingChances, Is.EqualTo(3));
        }

        [TestCase(3, 2, "stage-1-1", "stage-1-1", "level-1")]
        [TestCase(2, 1, "stage-1-1", "stage-1-1", "level-1")]
        [TestCase(1, 3, "stage-2-2", "stage-0-1", "level-0")]
        public void Death_ProductionAndTransientPreserveTheChanceTruthTableAndUntouchedState(
            int initialChances,
            int expectedChances,
            string initialStageId,
            string expectedStageId,
            string expectedLevelGroupId)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var seed = CreateFieldRichSlot(initialStageId, expectedLevelGroupId, initialChances);
            var preservedSnapshot = SnapshotFingerprint(seed.StageClearProfileSnapshot);
            var pair = CreatePair(seed);

            var transientResult = pair.Transient.CommitDeath(
                1,
                planner.PlanDeath(pair.Transient.LoadSlot(1)));
            var productionResult = pair.Production.CommitDeath(
                1,
                planner.PlanDeath(pair.Production.LoadSlot(1).State));
            var transientSlot = CampaignSlotRawDataMapper.ToRaw(
                transientResult.Slot);
            var productionSlot = CampaignSlotRawDataMapper.ToRaw(
                productionResult.Slot);

            Assert.That(pair.Repository.SaveCount, Is.EqualTo(1));
            Assert.That(transientSlot.CurrentStageId.Value, Is.EqualTo(expectedStageId));
            Assert.That(transientSlot.CurrentLevelGroupId, Is.EqualTo(expectedLevelGroupId));
            Assert.That(transientSlot.RemainingChances, Is.EqualTo(expectedChances));
            Assert.That(transientSlot.TotalDeaths, Is.EqualTo(seed.TotalDeaths + 1));
            Assert.That(productionSlot.TotalDeaths, Is.EqualTo(seed.TotalDeaths + 1));
            AssertTransitionParity(transientSlot, productionSlot);
            AssertUntouchedStatePreserved(seed, transientSlot, preservedSnapshot);
            AssertUntouchedStatePreserved(seed, productionSlot, preservedSnapshot);
            Assert.That(transientSlot.LastPlayedAt, Is.EqualTo(FixedCommitUtc));
            Assert.That(productionSlot.LastPlayedAt, Is.EqualTo(FixedCommitUtc));
        }

        [TestCase("stage-1-1", "stage-1-2", "level-1", 2, 2, false)]
        [TestCase("stage-1-2", "stage-2-1", "level-2", 1, 3, false)]
        [TestCase("stage-4-3", "stage-4-3", "level-4", 1, 1, true)]
        public void StageClear_ProductionAndTransientPreserveRouteParity(
            string completedStageIdValue,
            string expectedStageId,
            string expectedLevelGroupId,
            int initialChances,
            int expectedChances,
            bool expectedCompleted)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var completedStageId = StageId.CreateOrThrow(completedStageIdValue);
            var seed = CreateFieldRichSlot(
                completedStageIdValue,
                resolver.GetLevelGroupId(completedStageId),
                initialChances);
            seed.CampaignCompleted = false;
            var preservedSnapshot = SnapshotFingerprint(seed.StageClearProfileSnapshot);
            var pair = CreatePair(seed);
            var request = new CampaignStageClearCommitRequest
            {
                Plan = planner.PlanStageClear(completedStageId),
            };

            var transientResult = pair.Transient.CommitStageClear(1, request);
            var productionResult = pair.Production.CommitStageClear(1, request);
            var transientSlot = CampaignSlotRawDataMapper.ToRaw(
                transientResult.Slot);
            var productionSlot = CampaignSlotRawDataMapper.ToRaw(
                productionResult.Slot);

            Assert.That(pair.Repository.SaveCount, Is.EqualTo(1));
            Assert.That(transientResult.PreviousRemainingChances, Is.EqualTo(initialChances));
            Assert.That(productionResult.PreviousRemainingChances, Is.EqualTo(initialChances));
            Assert.That(transientSlot.CurrentStageId.Value, Is.EqualTo(expectedStageId));
            Assert.That(transientSlot.CurrentLevelGroupId, Is.EqualTo(expectedLevelGroupId));
            Assert.That(transientSlot.RemainingChances, Is.EqualTo(expectedChances));
            Assert.That(transientSlot.CampaignCompleted, Is.EqualTo(expectedCompleted));
            AssertTransitionParity(transientSlot, productionSlot);
            Assert.That(SnapshotFingerprint(transientSlot.StageClearProfileSnapshot),
                Is.EqualTo(preservedSnapshot));
            Assert.That(SnapshotFingerprint(productionSlot.StageClearProfileSnapshot),
                Is.EqualTo(preservedSnapshot));
            Assert.That(transientSlot.LastPlayedAt, Is.EqualTo(FixedCommitUtc));
            Assert.That(productionSlot.LastPlayedAt, Is.EqualTo(FixedCommitUtc));
        }

        [TestCase(ReceiptSeedState.Absent)]
        [TestCase(ReceiptSeedState.PresentWithoutPayload)]
        [TestCase(ReceiptSeedState.PresentWithPayload)]
        public void StageClear_ReceiptPresenceStatesRemainDistinctAndHaveProductionTransientParity(
            ReceiptSeedState receiptSeedState)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var completedStageId = StageId.CreateOrThrow("stage-4-3");
            var seed = CreateFieldRichSlot("stage-4-3", "level-4", 2);
            ApplyReceiptSeed(seed, receiptSeedState);
            var pair = CreatePair(seed);
            var offeredReceipt = CreateReceipt("stage-4-3", "offered-receipt-run");
            var request = new CampaignStageClearCommitRequest
            {
                Plan = planner.PlanStageClear(completedStageId),
                CompletionReceipt = offeredReceipt,
            };

            var transientResult = pair.Transient.CommitStageClear(1, request);
            var productionResult = pair.Production.CommitStageClear(1, request);
            var transientSlot = CampaignSlotRawDataMapper.ToRaw(
                transientResult.Slot);
            var productionSlot = CampaignSlotRawDataMapper.ToRaw(
                productionResult.Slot);

            AssertTransitionParity(transientSlot, productionSlot);
            Assert.That(transientSlot.HasNormalCampaignCompletionReceipt, Is.True);
            switch (receiptSeedState)
            {
                case ReceiptSeedState.Absent:
                    AssertReceipt(transientSlot.NormalCampaignCompletionReceipt, offeredReceipt);
                    break;
                case ReceiptSeedState.PresentWithoutPayload:
                    Assert.That(transientSlot.NormalCampaignCompletionReceipt, Is.Null);
                    break;
                case ReceiptSeedState.PresentWithPayload:
                    AssertReceipt(
                        transientSlot.NormalCampaignCompletionReceipt,
                        seed.NormalCampaignCompletionReceipt);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(receiptSeedState),
                        receiptSeedState,
                        null);
            }
        }

        [TestCase(-1, 7, 7)]
        [TestCase(10, 7, 7)]
        [TestCase(5, 7, 5)]
        [TestCase(7, 7, 7)]
        public void StageClear_PerformanceFirstBetterWorseAndEqualPreserveBestValueWithParity(
            int existingBestOrAbsent,
            int offeredValue,
            int expectedBest)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var completedStageId = StageId.CreateOrThrow("stage-1-1");
            var seed = CreateFieldRichSlot("stage-1-1", "level-1", 2);
            seed.NormalStagePerformanceRecords = existingBestOrAbsent < 0
                ? Array.Empty<NormalStagePerformanceRecord>()
                : new[] { CreatePerformance("stage-1-1", existingBestOrAbsent) };
            var pair = CreatePair(seed);
            var request = new CampaignStageClearCommitRequest
            {
                Plan = planner.PlanStageClear(completedStageId),
                PerformanceRecord = CreatePerformance("stage-1-1", offeredValue),
            };

            var transientResult = pair.Transient.CommitStageClear(1, request);
            var productionResult = pair.Production.CommitStageClear(1, request);
            var transientSlot = CampaignSlotRawDataMapper.ToRaw(
                transientResult.Slot);
            var productionSlot = CampaignSlotRawDataMapper.ToRaw(
                productionResult.Slot);

            AssertTransitionParity(transientSlot, productionSlot);
            Assert.That(transientSlot.NormalStagePerformanceRecords, Has.Length.EqualTo(1));
            Assert.That(
                transientSlot.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(expectedBest));
        }

        [Test]
        public void RawSeed_DuplicatePerformanceInputFailsClosedBeforeTransitionSetup()
        {
            var seed = CreateFieldRichSlot("stage-1-1", "level-1", 2);
            seed.NormalStagePerformanceRecords = new[]
            {
                CreatePerformance("stage-1-1", 10),
                CreatePerformance("stage-1-1", 7),
            };
            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToState(seed));
        }

        [Test]
        public void Death_StalePlanFailsWithoutReplacingEitherStore()
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var original = CreateFieldRichSlot("stage-1-1", "level-1", 3);
            var stalePlan = planner.PlanDeath(CampaignSlotRawDataMapper.ToState(original));
            var replacement = CreateFieldRichSlot("stage-1-2", "level-1", 2);
            var pair = CreatePair(replacement);
            var transientBefore = SlotFingerprint(pair.Transient.LoadSlot(1));
            var productionBefore = SlotFingerprint(pair.Production.LoadSlot(1));

            var serviceResult = pair.Service.CommitDeath(1, stalePlan);
            Assert.Throws<InvalidOperationException>(() =>
                pair.Transient.CommitDeath(1, stalePlan));
            Assert.Throws<InvalidOperationException>(() =>
                pair.Production.CommitDeath(1, stalePlan));

            Assert.That(serviceResult.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidRequest));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(SlotFingerprint(pair.Transient.LoadSlot(1)), Is.EqualTo(transientBefore));
            Assert.That(SlotFingerprint(pair.Production.LoadSlot(1)), Is.EqualTo(productionBefore));
        }

        [Test]
        public void StageClear_StalePlanFailsWithoutReplacingEitherStore()
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var replacement = CreateFieldRichSlot("stage-1-2", "level-1", 2);
            var pair = CreatePair(replacement);
            var transientBefore = SlotFingerprint(pair.Transient.LoadSlot(1));
            var productionBefore = SlotFingerprint(pair.Production.LoadSlot(1));
            var request = new CampaignStageClearCommitRequest
            {
                Plan = planner.PlanStageClear(StageId.CreateOrThrow("stage-1-1")),
            };

            var serviceResult = pair.Service.CommitStageClear(1, request);
            Assert.Throws<InvalidOperationException>(() =>
                pair.Transient.CommitStageClear(1, request));
            Assert.Throws<InvalidOperationException>(() =>
                pair.Production.CommitStageClear(1, request));

            Assert.That(serviceResult.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidRequest));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(SlotFingerprint(pair.Transient.LoadSlot(1)), Is.EqualTo(transientBefore));
            Assert.That(SlotFingerprint(pair.Production.LoadSlot(1)), Is.EqualTo(productionBefore));
        }

        [Test]
        public void InvalidDeathPlanFailsWithoutReplacingEitherStore()
        {
            var pair = CreatePair(CreateFieldRichSlot("stage-1-1", "level-1", 3));
            var transientBefore = SlotFingerprint(pair.Transient.LoadSlot(1));
            var productionBefore = SlotFingerprint(pair.Production.LoadSlot(1));

            var serviceResult = pair.Service.CommitDeath(1, default);
            Assert.Throws<ArgumentException>(() =>
                pair.Transient.CommitDeath(1, default));
            Assert.Throws<InvalidOperationException>(() =>
                pair.Production.CommitDeath(1, default));

            Assert.That(serviceResult.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidRequest));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(SlotFingerprint(pair.Transient.LoadSlot(1)), Is.EqualTo(transientBefore));
            Assert.That(SlotFingerprint(pair.Production.LoadSlot(1)), Is.EqualTo(productionBefore));
        }

        [Test]
        public void TransientDeath_MaxDeathCounterPreservesExceptionAndStoredSlot()
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var seed = CreateFieldRichSlot("stage-1-1", "level-1", 2);
            seed.TotalDeaths = int.MaxValue;
            var transient = new TransitionHarness(
                CampaignSlotRawDataMapper.ToState(seed),
                () => FixedCommitUtc);
            var plan = planner.PlanDeath(transient.LoadSlot(1));
            var before = SlotFingerprint(transient.LoadSlot(1));

            var exception = Assert.Throws<ArgumentException>(() =>
                transient.CommitDeath(1, plan));

            Assert.That(exception.ParamName, Is.EqualTo("slot"));
            Assert.That(SlotFingerprint(transient.LoadSlot(1)), Is.EqualTo(before));
        }

        [Test]
        public void InvalidStageClearRequestFailsWithoutReplacingEitherStore()
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var pair = CreatePair(CreateFieldRichSlot("stage-1-1", "level-1", 3));
            var transientBefore = SlotFingerprint(pair.Transient.LoadSlot(1));
            var productionBefore = SlotFingerprint(pair.Production.LoadSlot(1));
            var request = new CampaignStageClearCommitRequest
            {
                Plan = planner.PlanStageClear(StageId.CreateOrThrow("stage-1-1")),
                PerformanceRecord = CreatePerformance("stage-1-2", 4),
            };

            var serviceResult = pair.Service.CommitStageClear(1, request);
            Assert.Throws<ArgumentException>(() =>
                pair.Transient.CommitStageClear(1, request));
            Assert.Throws<InvalidOperationException>(() =>
                pair.Production.CommitStageClear(1, request));

            Assert.That(serviceResult.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidRequest));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(SlotFingerprint(pair.Transient.LoadSlot(1)), Is.EqualTo(transientBefore));
            Assert.That(SlotFingerprint(pair.Production.LoadSlot(1)), Is.EqualTo(productionBefore));
        }

        [Test]
        public void NullStageClearRequestPreservesTheExistingFailureSurfacesWithoutWrite()
        {
            var pair = CreatePair(CreateFieldRichSlot("stage-1-1", "level-1", 3));
            var transientBefore = SlotFingerprint(pair.Transient.LoadSlot(1));
            var productionBefore = SlotFingerprint(pair.Production.LoadSlot(1));

            var serviceResult = pair.Service.CommitStageClear(1, null);
            Assert.Throws<ArgumentNullException>(() =>
                pair.Transient.CommitStageClear(1, null));
            Assert.Throws<ArgumentNullException>(() =>
                pair.Production.CommitStageClear(1, null));

            Assert.That(serviceResult.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidRequest));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(SlotFingerprint(pair.Transient.LoadSlot(1)), Is.EqualTo(transientBefore));
            Assert.That(SlotFingerprint(pair.Production.LoadSlot(1)), Is.EqualTo(productionBefore));
        }

        [TestCase(TransitionOperation.Death)]
        [TestCase(TransitionOperation.StageClear)]
        public void InvalidSlotNumberPreservesServiceAdapterAndTransientFailureSurfacesWithoutWrite(
            TransitionOperation operation)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var pair = CreatePair(CreateFieldRichSlot("stage-1-1", "level-1", 3));
            var transientBefore = SlotFingerprint(pair.Transient.LoadSlot(1));
            var productionBefore = SlotFingerprint(pair.Production.LoadSlot(1));
            CampaignSaveServiceResult serviceResult;

            if (operation == TransitionOperation.Death)
            {
                var plan = planner.PlanDeath(pair.Production.LoadSlot(1).State);
                serviceResult = pair.Service.CommitDeath(0, plan);
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    pair.Transient.CommitDeath(0, plan));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    pair.Production.CommitDeath(0, plan));
            }
            else
            {
                var request = new CampaignStageClearCommitRequest
                {
                    Plan = planner.PlanStageClear(StageId.CreateOrThrow("stage-1-1")),
                };
                serviceResult = pair.Service.CommitStageClear(0, request);
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    pair.Transient.CommitStageClear(0, request));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    pair.Production.CommitStageClear(0, request));
            }

            Assert.That(serviceResult.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidSlotNumber));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(SlotFingerprint(pair.Transient.LoadSlot(1)), Is.EqualTo(transientBefore));
            Assert.That(SlotFingerprint(pair.Production.LoadSlot(1)), Is.EqualTo(productionBefore));
        }

        [TestCase(TransitionOperation.Death)]
        [TestCase(TransitionOperation.StageClear)]
        public void TransientTransition_ReadsClockOnceAndPersistsThatExactTimestamp(
            TransitionOperation operation)
        {
            var clockReadCount = 0;
            var transient = new TransientCampaignSaveSlotStore(
                TransientNamespace,
                () =>
                {
                    clockReadCount++;
                    return $"clock-read-{clockReadCount}";
                });
            transient.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1,
                StageId.CreateOrThrow("stage-1-1"),
                "level-1",
                2,
                SeedTimestampUtc));
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);

            if (operation == TransitionOperation.Death)
            {
                transient.CommitDeath(
                    1,
                    planner.PlanDeath(transient.LoadSlot(1).State));
            }
            else
            {
                transient.CommitStageClear(
                    1,
                    new CampaignStageClearCommitRequest
                    {
                        Plan = planner.PlanStageClear(
                            StageId.CreateOrThrow("stage-1-1")),
                    });
            }

            Assert.That(clockReadCount, Is.EqualTo(1));
            Assert.That(transient.LoadSlot(1).LastPlayedAt,
                Is.EqualTo("clock-read-1"));
        }

        [TestCase(TransitionOperation.Death)]
        [TestCase(TransitionOperation.StageClear)]
        public void RecoveryPendingBlocksProductionTransitionBeforeRepositoryAccess(
            TransitionOperation operation)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var recovery = new StubRecoveryPort { HasPendingReset = true };
            var pair = CreatePair(
                CreateFieldRichSlot("stage-1-1", "level-1", 3),
                recovery);
            pair.Repository.ResetAccessCounts();

            if (operation == TransitionOperation.Death)
            {
                var plan = planner.PlanDeath(pair.Transient.LoadSlot(1));
                Assert.Throws<InvalidOperationException>(() =>
                    pair.Production.CommitDeath(1, plan));
            }
            else
            {
                var request = new CampaignStageClearCommitRequest
                {
                    Plan = planner.PlanStageClear(StageId.CreateOrThrow("stage-1-1")),
                };
                Assert.Throws<InvalidOperationException>(() =>
                    pair.Production.CommitStageClear(1, request));
            }

            Assert.That(pair.Repository.LoadCount, Is.Zero);
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(
                pair.Production.LastCampaignLoadReport.Status,
                Is.EqualTo(CampaignSaveLoadStatus.RecoveryPending));
        }

        [TestCase(TransitionOperation.Death)]
        [TestCase(TransitionOperation.StageClear)]
        public void ProfileLoadFailurePreservesServiceAndAdapterFailureSurfacesWithoutWrite(
            TransitionOperation operation)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var pair = CreatePair(CreateFieldRichSlot("stage-1-1", "level-1", 3));
            var failure = new CampaignProfileLoadResult(
                CampaignProfileLoadStatus.IoFailed,
                null,
                "transition load failed");
            pair.Repository.EnqueueLoadResult(failure);
            pair.Repository.EnqueueLoadResult(failure);
            CampaignSaveServiceResult serviceResult;

            if (operation == TransitionOperation.Death)
            {
                var plan = planner.PlanDeath(pair.Transient.LoadSlot(1));
                serviceResult = pair.Service.CommitDeath(1, plan);
                Assert.Throws<InvalidOperationException>(() =>
                    pair.Production.CommitDeath(1, plan));
            }
            else
            {
                var request = new CampaignStageClearCommitRequest
                {
                    Plan = planner.PlanStageClear(StageId.CreateOrThrow("stage-1-1")),
                };
                serviceResult = pair.Service.CommitStageClear(1, request);
                Assert.Throws<InvalidOperationException>(() =>
                    pair.Production.CommitStageClear(1, request));
            }

            Assert.That(serviceResult.Status, Is.EqualTo(CampaignSaveCommandStatus.LoadFailed));
            Assert.That(pair.Repository.SaveCount, Is.Zero);
            Assert.That(
                pair.Production.LastCampaignLoadReport.Status,
                Is.EqualTo(CampaignSaveLoadStatus.IoFailed));
        }

        [Test]
        public void TransientTransitionSource_UsesEngineInsideLockWithoutInlineBusinessBranches()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/TransientCampaignState.cs");
            var deathSource = GetMethodSource(
                source,
                "public CampaignDeathCommitResult CommitDeath(",
                "public CampaignStageClearCommitResult CommitStageClear(");
            var clearSource = GetMethodSource(
                source,
                "public CampaignStageClearCommitResult CommitStageClear(",
                "public void DeleteSlot(");

            AssertEngineInsideLock(
                deathSource,
                "CampaignSlotTransitionEngine.ApplyDeath",
                "StoreState(transition.Slot)");
            Assert.That(deathSource, Does.Not.Contain("CommitDeathCore"));
            Assert.That(deathSource, Does.Not.Contain("slot.CurrentStageId ="));
            Assert.That(deathSource, Does.Not.Contain("slot.CurrentLevelGroupId ="));
            Assert.That(deathSource, Does.Not.Contain("slot.RemainingChances ="));
            Assert.That(deathSource, Does.Not.Contain("slot.TotalDeaths +="));
            Assert.That(deathSource, Does.Not.Contain("plan.ExpectedCurrentStageId.IsValid"));

            AssertEngineInsideLock(
                clearSource,
                "CampaignSlotTransitionEngine.ApplyStageClear",
                "StoreState(transition.Slot)");
            Assert.That(clearSource, Does.Not.Contain("CommitStageClearCore"));
            Assert.That(clearSource, Does.Not.Contain("slot.CurrentStageId ="));
            Assert.That(clearSource, Does.Not.Contain("slot.CurrentLevelGroupId ="));
            Assert.That(clearSource, Does.Not.Contain("slot.CampaignCompleted ="));
            Assert.That(clearSource, Does.Not.Contain("slot.RemainingChances ="));
            Assert.That(clearSource, Does.Not.Contain(
                "slot.NormalCampaignCompletionReceipt ="));
            Assert.That(clearSource, Does.Not.Contain(
                "slot.NormalStagePerformanceRecords ="));
            Assert.That(clearSource, Does.Not.Contain("request.Plan.CompletedStageId.IsValid"));

            Assert.That(CountOccurrences(
                source,
                "CampaignSlotTransitionEngine.ApplyDeath"), Is.EqualTo(1));
            Assert.That(CountOccurrences(
                source,
                "CampaignSlotTransitionEngine.ApplyStageClear"), Is.EqualTo(1));
        }

        private static void AssertEngineInsideLock(
            string methodSource,
            string engineCall,
            string replacementCall)
        {
            var lockIndex = methodSource.IndexOf("lock (Gate)", StringComparison.Ordinal);
            var engineIndex = methodSource.IndexOf(engineCall, StringComparison.Ordinal);
            var replacementIndex = methodSource.IndexOf(
                replacementCall,
                StringComparison.Ordinal);
            Assert.That(lockIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(engineIndex, Is.GreaterThan(lockIndex));
            Assert.That(replacementIndex, Is.GreaterThan(engineIndex));
        }

        private static string GetMethodSource(
            string source,
            string methodSignature,
            string nextMethodSignature)
        {
            var start = source.IndexOf(methodSignature, StringComparison.Ordinal);
            var end = source.IndexOf(nextMethodSignature, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return source.Substring(start, end - start);
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static StorePair CreatePair(
            SaveSlotData seed,
            ICampaignSaveRecoveryPort recoveryPort = null)
        {
            var transient = new TransitionHarness(
                CampaignSlotRawDataMapper.ToState(seed.Clone()),
                () => FixedCommitUtc);
            var repository = new RecordingRepository();
            repository.Seed(CampaignSlotRawDataMapper.ToProfileDocument(
                new[] { seed.Clone() },
                "transition-characterization-profile",
                seed.SlotNumber,
                SeedTimestampUtc,
                "transition-characterization-product"));
            var service = new CampaignSaveService(
                repository,
                () => FixedCommitUtc,
                "transition-characterization-profile",
                "transition-characterization-product");
            var production = new CampaignSaveSlotStoreAdapter(service, recoveryPort);
            repository.ResetAccessCounts();
            return new StorePair(transient, production, service, repository);
        }

        private static SaveSlotData CreateFieldRichSlot(
            string stageIdValue,
            string levelGroupId,
            int remainingChances)
        {
            var snapshotStageId = StageId.CreateOrThrow("stage-0-1");
            var record = PlayerStageClearRecord.CreateEmpty(snapshotStageId);
            record.HasAttempted = true;
            record.HasCleared = true;
            record.ClearCount = 4;
            record.ProcessedStageRunIds = new[] { "record-run-a", "record-run-b" };
            return new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow(stageIdValue),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = remainingChances,
                CampaignCompleted = false,
                HasNormalCampaignCompletionReceipt = true,
                NormalCampaignCompletionReceipt = CreateReceipt(
                    "stage-4-3",
                    "preserved-receipt-run"),
                NormalStagePerformanceRecords = new[]
                {
                    CreatePerformance("stage-0-2", 11),
                },
                IntroComicCompleted = true,
                OutroComicCompleted = true,
                TotalDeaths = 6,
                LastPlayedAt = SeedTimestampUtc,
                StageClearProfileSnapshot = new StageClearProfileSnapshot
                {
                    Version = 7,
                    ClearRecordsByStageId = new Dictionary<StageId, PlayerStageClearRecord>
                    {
                        [snapshotStageId] = record,
                    },
                    ProcessedStageRunIds = new HashSet<string>(
                        new[] { "profile-run-a", "profile-run-b" },
                        StringComparer.Ordinal),
                    ProcessedClearAttemptIds = new HashSet<string>(
                        new[] { "profile-attempt-a", "profile-attempt-b" },
                        StringComparer.Ordinal),
                },
            };
        }

        private static void ApplyReceiptSeed(SaveSlotData slot, ReceiptSeedState state)
        {
            switch (state)
            {
                case ReceiptSeedState.Absent:
                    slot.HasNormalCampaignCompletionReceipt = false;
                    slot.NormalCampaignCompletionReceipt = null;
                    return;
                case ReceiptSeedState.PresentWithoutPayload:
                    slot.HasNormalCampaignCompletionReceipt = true;
                    slot.NormalCampaignCompletionReceipt = null;
                    return;
                case ReceiptSeedState.PresentWithPayload:
                    slot.HasNormalCampaignCompletionReceipt = true;
                    slot.NormalCampaignCompletionReceipt = CreateReceipt(
                        "stage-4-3",
                        "existing-receipt-run");
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private static NormalCampaignCompletionReceipt CreateReceipt(
            string completedStageId,
            string stageRunId)
        {
            return new NormalCampaignCompletionReceipt
            {
                Version = NormalCampaignCompletionReceipt.LegacyVersion,
                CompletedStageId = completedStageId,
                StageRunId = stageRunId,
                ClearSource = NormalCampaignCompletionReceipt.LegacyObjectiveClearSource,
            };
        }

        private static NormalStagePerformanceRecord CreatePerformance(
            string stageId,
            int bestCombinedPushFlipUses)
        {
            return new NormalStagePerformanceRecord
            {
                StageId = StageId.CreateOrThrow(stageId),
                BestCombinedPushFlipUses = bestCombinedPushFlipUses,
            };
        }

        private static void AssertTransitionParity(SaveSlotData transient, SaveSlotData production)
        {
            Assert.That(SlotFingerprint(transient), Is.EqualTo(SlotFingerprint(production)));
        }

        private static void AssertUntouchedStatePreserved(
            SaveSlotData seed,
            SaveSlotData actual,
            string preservedSnapshot)
        {
            Assert.That(actual.CampaignCompleted, Is.EqualTo(seed.CampaignCompleted));
            Assert.That(actual.IntroComicCompleted, Is.EqualTo(seed.IntroComicCompleted));
            Assert.That(actual.OutroComicCompleted, Is.EqualTo(seed.OutroComicCompleted));
            AssertReceipt(actual.NormalCampaignCompletionReceipt, seed.NormalCampaignCompletionReceipt);
            Assert.That(
                PerformanceFingerprint(actual.NormalStagePerformanceRecords),
                Is.EqualTo(PerformanceFingerprint(seed.NormalStagePerformanceRecords)));
            Assert.That(SnapshotFingerprint(actual.StageClearProfileSnapshot),
                Is.EqualTo(preservedSnapshot));
        }

        private static void AssertReceipt(
            NormalCampaignCompletionReceipt actual,
            NormalCampaignCompletionReceipt expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Version, Is.EqualTo(expected.Version));
            Assert.That(actual.CompletedStageId, Is.EqualTo(expected.CompletedStageId));
            Assert.That(actual.StageRunId, Is.EqualTo(expected.StageRunId));
            Assert.That(actual.ClearSource, Is.EqualTo(expected.ClearSource));
        }

        private static string SlotFingerprint(SaveSlotData slot)
        {
            var document = CampaignSlotRawDataMapper.ToDocument(slot);
            return JsonUtility.ToJson(document);
        }

        private static string SlotFingerprint(CampaignSlotEntry entry)
        {
            return SlotFingerprint(CampaignSlotRawDataMapper.ToRaw(entry));
        }

        private static string SlotFingerprint(CampaignSlotState state)
        {
            return SlotFingerprint(CampaignSlotRawDataMapper.ToRaw(state));
        }

        private static string SnapshotFingerprint(StageClearProfileSnapshot snapshot)
        {
            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                StageClearProfileSnapshot = snapshot,
            };
            return JsonUtility.ToJson(
                CampaignSlotRawDataMapper.ToDocument(slot)
                    .StageClearProfileSnapshot);
        }

        private static string PerformanceFingerprint(NormalStagePerformanceRecord[] records)
        {
            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                NormalStagePerformanceRecords = records,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
            var wrapper = new PerformanceDocumentWrapper
            {
                Records = CampaignSlotRawDataMapper.ToDocument(slot)
                    .NormalStagePerformanceRecords,
            };
            return JsonUtility.ToJson(wrapper);
        }

        public enum ReceiptSeedState
        {
            Absent,
            PresentWithoutPayload,
            PresentWithPayload,
        }

        public enum TransitionOperation
        {
            Death,
            StageClear,
        }

        [Serializable]
        private sealed class PerformanceDocumentWrapper
        {
            public NormalStagePerformanceRecordDocument[] Records =
                Array.Empty<NormalStagePerformanceRecordDocument>();
        }

        private sealed class StorePair
        {
            public StorePair(
                TransitionHarness transient,
                CampaignSaveSlotStoreAdapter production,
                CampaignSaveService service,
                RecordingRepository repository)
            {
                Transient = transient;
                Production = production;
                Service = service;
                Repository = repository;
            }

            public TransitionHarness Transient { get; }

            public CampaignSaveSlotStoreAdapter Production { get; }

            public CampaignSaveService Service { get; }

            public RecordingRepository Repository { get; }
        }

        private sealed class TransitionHarness
        {
            private CampaignSlotState _state;
            private readonly Func<string> _utcNowProvider;

            public TransitionHarness(
                CampaignSlotState state,
                Func<string> utcNowProvider)
            {
                _state = state ?? throw new ArgumentNullException(nameof(state));
                _utcNowProvider = utcNowProvider ??
                    throw new ArgumentNullException(nameof(utcNowProvider));
            }

            public CampaignSlotState LoadSlot(int slotNumber)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
                return _state;
            }

            public CampaignSurvivalCommitResult CommitSurvival(int slotNumber, CampaignSurvivalCommitRequest request) =>
                throw new NotSupportedException();

            public CampaignDeathCommitResult CommitDeath(
                int slotNumber,
                CampaignDeathTransitionPlan plan)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
                var transition = CampaignSlotTransitionEngine.ApplyDeath(
                    _state,
                    plan,
                    _utcNowProvider());
                if (!transition.Succeeded)
                {
                    if (transition.ReasonCode ==
                        CampaignSlotTransitionReasonCode.DeathPlanInvalid)
                    {
                        throw new ArgumentException(
                            "Death transition plan is invalid.",
                            "plan");
                    }

                    if (transition.ReasonCode ==
                        CampaignSlotTransitionReasonCode.DeathCounterOverflow)
                    {
                        throw new ArgumentException(
                            "Campaign slot does not satisfy the transition contract.",
                            "slot");
                    }

                    throw new InvalidOperationException(
                        "Campaign slot changed after the death transition was planned.");
                }

                _state = transition.Slot;
                return new CampaignDeathCommitResult(_state);
            }

            public CampaignStageClearCommitResult CommitStageClear(
                int slotNumber,
                CampaignStageClearCommitRequest request)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
                var transition = CampaignSlotTransitionEngine.ApplyStageClear(
                    _state,
                    request,
                    _utcNowProvider());
                if (!transition.Succeeded)
                {
                    if (transition.ReasonCode ==
                        CampaignSlotTransitionReasonCode.StageClearRequestNull)
                    {
                        throw new ArgumentNullException("request");
                    }

                    if (transition.ReasonCode ==
                        CampaignSlotTransitionReasonCode.StageClearRequestInvalid)
                    {
                        throw new ArgumentException(
                            "Stage clear request is invalid.",
                            "request");
                    }

                    throw new InvalidOperationException(
                        "Campaign slot changed after the stage clear transition was planned.");
                }

                _state = transition.Slot;
                return new CampaignStageClearCommitResult(
                    _state,
                    transition.PreviousRemainingChances);
            }
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            private readonly Queue<CampaignProfileLoadResult> _loadResults = new();
            private CampaignProfileDocument _document;

            public int LoadCount { get; private set; }

            public int SaveCount { get; private set; }
            public Action BeforeSave { get; set; }

            public void EnqueueLoadResult(CampaignProfileLoadResult result)
            {
                _loadResults.Enqueue(result);
            }

            public void Seed(CampaignProfileDocument document)
            {
                _document = document ?? throw new ArgumentNullException(nameof(document));
            }

            public CampaignProfileLoadResult Load()
            {
                LoadCount++;
                if (_loadResults.Count > 0)
                {
                    return _loadResults.Dequeue();
                }

                return _document == null
                    ? new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Missing,
                        null,
                        "missing")
                    : new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Loaded,
                        _document,
                        "loaded");
            }

            public void Save(CampaignProfileDocument document)
            {
                BeforeSave?.Invoke();
                SaveCount++;
                _document = document;
            }

            public void SaveDestructive(CampaignProfileDocument document)
            {
                SaveCount++;
                _document = document;
            }

            public void ResetAccessCounts()
            {
                LoadCount = 0;
                SaveCount = 0;
            }
        }

        private sealed class StubRecoveryPort : ICampaignSaveRecoveryPort
        {
            public bool HasPendingReset { get; set; }

            public CampaignSaveResetResult ResetBlockedProfile(
                CampaignSaveLoadStatus expectedStatus)
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
