using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    [Category("Core")]
    public sealed class CampaignSlotTransitionEngineTests
    {
        private const string CommittedAtUtc = "2026-08-24T13:14:15.0000000Z";
        private const string OriginalTimestampUtc = "2026-01-02T03:04:05.0000000Z";

        [TestCase(3, 2, "stage-1-1", "stage-1-1", "level-1")]
        [TestCase(2, 1, "stage-1-1", "stage-1-1", "level-1")]
        [TestCase(1, 3, "stage-2-2", "stage-0-1", "level-0")]
        public void ApplyDeath_UsesTheCompleteChanceTruthTable(
            int initialChances,
            int expectedChances,
            string currentStageValue,
            string expectedStageValue,
            string persistedLevelGroupId)
        {
            var current = ToState(CreateRichSlot(currentStageValue, initialChances));
            var plan = CreateDeathPlan(
                current.CurrentStageId,
                initialChances,
                StageId.CreateOrThrow(expectedStageValue),
                expectedChances,
                persistedLevelGroupId);

            var result = CampaignSlotTransitionEngine.ApplyDeath(
                current,
                plan,
                CommittedAtUtc);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailureKind, Is.EqualTo(CampaignSlotTransitionFailureKind.None));
            Assert.That(result.ReasonCode, Is.EqualTo(CampaignSlotTransitionReasonCode.None));
            Assert.That(result.PreviousRemainingChances, Is.Null);
            Assert.That(result.Slot.CurrentStageId.Value, Is.EqualTo(expectedStageValue));
            Assert.That(result.Slot.CurrentLevelGroupId, Is.EqualTo(persistedLevelGroupId));
            Assert.That(result.Slot.RemainingChances, Is.EqualTo(expectedChances));
            Assert.That(result.Slot.TotalDeaths, Is.EqualTo(current.TotalDeaths + 1));
            Assert.That(result.Slot.LastPlayedAt, Is.EqualTo(CommittedAtUtc));
        }

        [Test]
        public void ApplyDeath_ReturnsAnIndependentSlotAndPreservesUntouchedNestedState()
        {
            var current = ToState(CreateRichSlot("stage-1-1", 2));
            var originalFingerprint = Fingerprint(ToLegacy(current));
            var result = CampaignSlotTransitionEngine.ApplyDeath(
                current,
                CreateDeathPlan(
                    current.CurrentStageId,
                    2,
                    current.CurrentStageId,
                    1,
                    "level-1"),
                CommittedAtUtc);

            var firstRead = result.Slot;
            var secondRead = result.Slot;

            Assert.That(Fingerprint(ToLegacy(current)), Is.EqualTo(originalFingerprint));
            Assert.That(firstRead, Is.Not.SameAs(current));
            Assert.That(firstRead, Is.SameAs(secondRead));
            Assert.That(firstRead.Receipt, Is.SameAs(current.Receipt));
            Assert.That(firstRead.NormalStagePerformanceRecords,
                Is.Not.SameAs(current.NormalStagePerformanceRecords));
            Assert.That(firstRead.StageClearProfile,
                Is.SameAs(current.StageClearProfile));
            AssertUntouchedState(
                ToLegacy(current),
                ToLegacy(firstRead),
                preservesDeaths: false);
        }

        [Test]
        public void ApplyDeath_DefaultPlanReturnsTypedInvalidPlanWithoutSlot()
        {
            var result = CampaignSlotTransitionEngine.ApplyDeath(
                ToState(CreateRichSlot("stage-1-1", 3)),
                default,
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.InvalidPlan,
                CampaignSlotTransitionReasonCode.DeathPlanInvalid);
        }

        [Test]
        public void ApplyDeath_NullCurrentStateReturnsTypedFailure()
        {
            var plan = CreateDeathPlan(
                StageId.CreateOrThrow("stage-1-1"),
                3,
                StageId.CreateOrThrow("stage-1-1"),
                2,
                "level-1");

            var result = CampaignSlotTransitionEngine.ApplyDeath(
                null,
                plan,
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.InvalidCurrentState,
                CampaignSlotTransitionReasonCode.CurrentStateInvalid);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ApplyDeath_ChangedStageOrChanceReturnsTypedStaleFailure(
            bool changesStage,
            bool changesChance)
        {
            var current = ToState(CreateRichSlot("stage-1-1", 2));
            var expectedStage = changesStage
                ? StageId.CreateOrThrow("stage-1-2")
                : current.CurrentStageId;
            var expectedChances = changesChance ? 3 : current.RemainingChances;
            var originalFingerprint = Fingerprint(ToLegacy(current));

            var result = CampaignSlotTransitionEngine.ApplyDeath(
                current,
                CreateDeathPlan(
                    expectedStage,
                    expectedChances,
                    current.CurrentStageId,
                    1,
                    "level-1"),
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.StalePrecondition,
                CampaignSlotTransitionReasonCode.DeathPreconditionChanged);
            Assert.That(Fingerprint(ToLegacy(current)), Is.EqualTo(originalFingerprint));
        }

        [Test]
        public void ApplyDeath_MaxDeathCounterFailsClosedInsteadOfOverflowing()
        {
            var legacy = CreateRichSlot("stage-1-1", 2);
            legacy.TotalDeaths = int.MaxValue;
            var current = ToState(legacy);

            var result = CampaignSlotTransitionEngine.ApplyDeath(
                current,
                CreateDeathPlan(
                    current.CurrentStageId,
                    2,
                    current.CurrentStageId,
                    1,
                    "level-1"),
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.InvalidCurrentState,
                CampaignSlotTransitionReasonCode.DeathCounterOverflow);
            Assert.That(current.TotalDeaths, Is.EqualTo(int.MaxValue));
        }

        [TestCase("stage-1-1", "stage-1-2", "level-1", 2, 2, false, false)]
        [TestCase("stage-1-2", "stage-2-1", "level-2", 1, 3, false, true)]
        [TestCase("stage-4-3", "stage-4-3", "level-4", 1, 1, true, false)]
        public void ApplyStageClear_UsesSameGroupBoundaryAndFinalTruthTable(
            string completedStageValue,
            string persistedStageValue,
            string persistedLevelGroupId,
            int initialChances,
            int expectedChances,
            bool isCampaignCompleted,
            bool restoresChances)
        {
            var current = ToState(CreateRichSlot(completedStageValue, initialChances));
            var request = CreateClearRequest(
                completedStageValue,
                persistedStageValue,
                persistedLevelGroupId,
                isCampaignCompleted,
                restoresChances);

            var result = CampaignSlotTransitionEngine.ApplyStageClear(
                current,
                request,
                CommittedAtUtc);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.PreviousRemainingChances, Is.EqualTo(initialChances));
            Assert.That(result.Slot.CurrentStageId.Value, Is.EqualTo(persistedStageValue));
            Assert.That(result.Slot.CurrentLevelGroupId, Is.EqualTo(persistedLevelGroupId));
            Assert.That(result.Slot.RemainingChances, Is.EqualTo(expectedChances));
            Assert.That(result.Slot.CampaignCompleted, Is.EqualTo(isCampaignCompleted));
            Assert.That(result.Slot.LastPlayedAt, Is.EqualTo(CommittedAtUtc));
        }

        [TestCase(InvalidClearRequestKind.Null)]
        [TestCase(InvalidClearRequestKind.DefaultPlan)]
        [TestCase(InvalidClearRequestKind.MismatchedReceipt)]
        [TestCase(InvalidClearRequestKind.MismatchedPerformance)]
        public void ApplyStageClear_InvalidRequestReturnsTypedFailure(
            InvalidClearRequestKind kind)
        {
            var current = ToState(CreateRichSlot("stage-1-1", 2));
            var request = CreateInvalidClearRequest(kind);

            var result = CampaignSlotTransitionEngine.ApplyStageClear(
                current,
                request,
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.InvalidPlan,
                kind == InvalidClearRequestKind.Null
                    ? CampaignSlotTransitionReasonCode.StageClearRequestNull
                    : CampaignSlotTransitionReasonCode.StageClearRequestInvalid);
        }

        [Test]
        public void ApplyStageClear_NullCurrentStateReturnsTypedFailure()
        {
            var result = CampaignSlotTransitionEngine.ApplyStageClear(
                null,
                CreateClearRequest(
                    "stage-1-1",
                    "stage-1-2",
                    "level-1",
                    isCampaignCompleted: false,
                    restoresChances: false),
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.InvalidCurrentState,
                CampaignSlotTransitionReasonCode.CurrentStateInvalid);
        }

        [Test]
        public void ApplyStageClear_ChangedStageReturnsTypedStaleFailureWithoutMutation()
        {
            var current = ToState(CreateRichSlot("stage-1-2", 2));
            var originalFingerprint = Fingerprint(ToLegacy(current));

            var result = CampaignSlotTransitionEngine.ApplyStageClear(
                current,
                CreateClearRequest(
                    "stage-1-1",
                    "stage-1-2",
                    "level-1",
                    isCampaignCompleted: false,
                    restoresChances: false),
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.StalePrecondition,
                CampaignSlotTransitionReasonCode.StageClearPreconditionChanged);
            Assert.That(Fingerprint(ToLegacy(current)), Is.EqualTo(originalFingerprint));
        }

        [TestCase(ReceiptState.Absent)]
        [TestCase(ReceiptState.PresentWithoutPayload)]
        [TestCase(ReceiptState.PresentWithPayload)]
        public void ApplyStageClear_PreservesReceiptPresenceAndFirstWritePolicy(
            ReceiptState state)
        {
            var legacy = CreateRichSlot("stage-4-3", 2);
            ApplyReceiptState(legacy, state);
            var current = ToState(legacy);
            var offered = CreateReceipt("stage-4-3", "offered-run");
            var request = CreateClearRequest(
                "stage-4-3",
                "stage-4-3",
                "level-4",
                isCampaignCompleted: true,
                restoresChances: false);
            request.CompletionReceipt = offered;

            var result = CampaignSlotTransitionEngine.ApplyStageClear(
                current,
                request,
                CommittedAtUtc);
            var resultSlot = ToLegacy(result.Slot);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(resultSlot.HasNormalCampaignCompletionReceipt, Is.True);
            switch (state)
            {
                case ReceiptState.Absent:
                    AssertReceipt(resultSlot.NormalCampaignCompletionReceipt, offered);
                    break;
                case ReceiptState.PresentWithoutPayload:
                    Assert.That(resultSlot.NormalCampaignCompletionReceipt, Is.Null);
                    break;
                case ReceiptState.PresentWithPayload:
                    AssertReceipt(
                        resultSlot.NormalCampaignCompletionReceipt,
                        legacy.NormalCampaignCompletionReceipt);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        [TestCase(-1, 7, 7)]
        [TestCase(10, 7, 7)]
        [TestCase(5, 7, 5)]
        [TestCase(7, 7, 7)]
        public void ApplyStageClear_UpsertsOnlyTheBestPerformance(
            int existingBestOrAbsent,
            int offeredValue,
            int expectedBest)
        {
            var legacy = CreateRichSlot("stage-1-1", 2);
            legacy.NormalStagePerformanceRecords = existingBestOrAbsent < 0
                ? Array.Empty<NormalStagePerformanceRecord>()
                : new[] { CreatePerformance("stage-1-1", existingBestOrAbsent) };
            var current = ToState(legacy);
            var request = CreateClearRequest(
                "stage-1-1",
                "stage-1-2",
                "level-1",
                isCampaignCompleted: false,
                restoresChances: false);
            request.PerformanceRecord = CreatePerformance("stage-1-1", offeredValue);

            var result = CampaignSlotTransitionEngine.ApplyStageClear(
                current,
                request,
                CommittedAtUtc);
            var resultSlot = ToLegacy(result.Slot);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(resultSlot.NormalStagePerformanceRecords, Has.Length.EqualTo(1));
            Assert.That(
                resultSlot.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(expectedBest));
        }

        [Test]
        public void ApplyStageClear_PreservesUntouchedNestedStateAndDoesNotMutateInput()
        {
            var current = ToState(CreateRichSlot("stage-1-1", 2));
            var originalFingerprint = Fingerprint(ToLegacy(current));

            var result = CampaignSlotTransitionEngine.ApplyStageClear(
                current,
                CreateClearRequest(
                    "stage-1-1",
                    "stage-1-2",
                    "level-1",
                    isCampaignCompleted: false,
                    restoresChances: false),
                CommittedAtUtc);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(Fingerprint(ToLegacy(current)), Is.EqualTo(originalFingerprint));
            AssertUntouchedState(
                ToLegacy(current),
                ToLegacy(result.Slot),
                preservesDeaths: true);
        }

        [TestCase((int)CampaignComicCompletionKind.Intro, true, false)]
        [TestCase((int)CampaignComicCompletionKind.Outro, false, true)]
        public void ApplyComicCompletion_UpdatesOnlyTheTypedFlagAndTimestamp(
            int completionKindValue,
            bool expectedIntroCompleted,
            bool expectedOutroCompleted)
        {
            var current = CreateRichSlot("stage-1-1", 2);
            current.IntroComicCompleted = false;
            current.OutroComicCompleted = false;
            var state = ToState(current);

            var result = CampaignSlotTransitionEngine.ApplyComicCompletion(
                state,
                new CampaignComicCompletionCommand(
                    (CampaignComicCompletionKind)completionKindValue),
                CommittedAtUtc);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Slot.CurrentStageId, Is.EqualTo(state.CurrentStageId));
            Assert.That(result.Slot.IntroComicCompleted,
                Is.EqualTo(expectedIntroCompleted));
            Assert.That(result.Slot.OutroComicCompleted,
                Is.EqualTo(expectedOutroCompleted));
            Assert.That(result.Slot.LastPlayedAt, Is.EqualTo(CommittedAtUtc));
        }

        [Test]
        public void ApplyComicCompletion_DefaultCommandReturnsTypedFailure()
        {
            var result = CampaignSlotTransitionEngine.ApplyComicCompletion(
                ToState(CreateRichSlot("stage-1-1", 2)),
                default,
                CommittedAtUtc);

            AssertFailure(
                result,
                CampaignSlotTransitionFailureKind.InvalidPlan,
                CampaignSlotTransitionReasonCode.ComicCommandInvalid);
        }

        private static CampaignDeathTransitionPlan CreateDeathPlan(
            StageId expectedStageId,
            int expectedChances,
            StageId nextStageId,
            int nextChances,
            string persistedLevelGroupId)
        {
            return new CampaignDeathTransitionPlan(
                expectedStageId,
                expectedChances,
                persistedLevelGroupId,
                new StageRetryRouteResult(
                    nextChances == CampaignSaveSlotPolicy.DefaultRemainingChances &&
                    expectedChances == 1
                        ? StageRetryRouteKind.ReturnToCampaignFirstStage
                        : StageRetryRouteKind.RetrySameStage,
                    nextStageId,
                    nextChances));
        }

        private static CampaignStageClearCommitRequest CreateClearRequest(
            string completedStageValue,
            string persistedStageValue,
            string persistedLevelGroupId,
            bool isCampaignCompleted,
            bool restoresChances)
        {
            return new CampaignStageClearCommitRequest
            {
                Plan = new CampaignStageClearTransitionPlan(
                    StageId.CreateOrThrow(completedStageValue),
                    StageId.CreateOrThrow(persistedStageValue),
                    "completed-group",
                    persistedLevelGroupId,
                    isCampaignCompleted,
                    restoresChances),
            };
        }

        private static CampaignStageClearCommitRequest CreateInvalidClearRequest(
            InvalidClearRequestKind kind)
        {
            if (kind == InvalidClearRequestKind.Null)
            {
                return null;
            }

            if (kind == InvalidClearRequestKind.DefaultPlan)
            {
                return new CampaignStageClearCommitRequest();
            }

            var request = CreateClearRequest(
                "stage-1-1",
                "stage-1-2",
                "level-1",
                isCampaignCompleted: false,
                restoresChances: false);
            if (kind == InvalidClearRequestKind.MismatchedReceipt)
            {
                request.CompletionReceipt = CreateReceipt("stage-1-2", "wrong-stage-run");
            }
            else
            {
                request.PerformanceRecord = CreatePerformance("stage-1-2", 4);
            }

            return request;
        }

        private static SaveSlotData CreateRichSlot(string stageValue, int remainingChances)
        {
            var currentStageId = StageId.CreateOrThrow(stageValue);
            var recordStageId = StageId.CreateOrThrow("stage-3-1");
            return new SaveSlotData
            {
                SlotNumber = 2,
                CurrentStageId = currentStageId,
                CurrentLevelGroupId = "original-level-group",
                RemainingChances = remainingChances,
                CampaignCompleted = false,
                HasNormalCampaignCompletionReceipt = true,
                NormalCampaignCompletionReceipt = CreateReceipt(
                    "stage-4-3",
                    "original-receipt-run"),
                NormalStagePerformanceRecords = new[]
                {
                    CreatePerformance("stage-3-1", 9),
                },
                IntroComicCompleted = true,
                OutroComicCompleted = true,
                TotalDeaths = 4,
                LastPlayedAt = OriginalTimestampUtc,
                StageClearProfileSnapshot = new StageClearProfileSnapshot
                {
                    Version = 7,
                    ClearRecordsByStageId = new Dictionary<StageId, PlayerStageClearRecord>
                    {
                        [recordStageId] = new PlayerStageClearRecord
                        {
                            StageId = recordStageId,
                            HasAttempted = true,
                            HasCleared = true,
                            ClearCount = 3,
                            ProcessedStageRunIds = new[] { "record-run" },
                        },
                    },
                    ProcessedStageRunIds = new HashSet<string>(
                        new[] { "profile-run" },
                        StringComparer.Ordinal),
                    ProcessedClearAttemptIds = new HashSet<string>(
                        new[] { "profile-attempt" },
                        StringComparer.Ordinal),
                },
            };
        }

        private static CampaignSlotState ToState(SaveSlotData slot)
        {
            var document = CampaignSlotRawDataMapper.ToDocument(slot);
            var result = CampaignSlotParser.ParseEntry(slot.SlotNumber, document);
            Assert.That(result.IsSuccess, Is.True);
            return result.Entry.State;
        }

        private static SaveSlotData ToLegacy(CampaignSlotState state)
        {
            return CampaignSlotRawDataMapper.ToRaw(state);
        }

        private static NormalCampaignCompletionReceipt CreateReceipt(
            string stageValue,
            string stageRunId)
        {
            return new NormalCampaignCompletionReceipt
            {
                Version = NormalCampaignCompletionReceipt.LegacyVersion,
                CompletedStageId = stageValue,
                StageRunId = stageRunId,
                ClearSource = NormalCampaignCompletionReceipt.LegacyObjectiveClearSource,
            };
        }

        private static NormalStagePerformanceRecord CreatePerformance(
            string stageValue,
            int bestUses)
        {
            return new NormalStagePerformanceRecord
            {
                Version = NormalStagePerformanceRecord.CurrentVersion,
                StageId = StageId.CreateOrThrow(stageValue),
                BestCombinedPushFlipUses = bestUses,
            };
        }

        private static void ApplyReceiptState(SaveSlotData slot, ReceiptState state)
        {
            switch (state)
            {
                case ReceiptState.Absent:
                    slot.HasNormalCampaignCompletionReceipt = false;
                    slot.NormalCampaignCompletionReceipt = null;
                    break;
                case ReceiptState.PresentWithoutPayload:
                    slot.HasNormalCampaignCompletionReceipt = true;
                    slot.NormalCampaignCompletionReceipt = null;
                    break;
                case ReceiptState.PresentWithPayload:
                    slot.HasNormalCampaignCompletionReceipt = true;
                    slot.NormalCampaignCompletionReceipt = CreateReceipt(
                        "stage-4-3",
                        "preserved-receipt-run");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private static void AssertFailure(
            CampaignSlotTransitionResult result,
            CampaignSlotTransitionFailureKind failureKind,
            CampaignSlotTransitionReasonCode reasonCode)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(failureKind));
            Assert.That(result.ReasonCode, Is.EqualTo(reasonCode));
            Assert.That(result.Slot, Is.Null);
            Assert.That(result.PreviousRemainingChances, Is.Null);
        }

        private static void AssertUntouchedState(
            SaveSlotData expected,
            SaveSlotData actual,
            bool preservesDeaths)
        {
            Assert.That(actual.SlotNumber, Is.EqualTo(expected.SlotNumber));
            Assert.That(actual.IntroComicCompleted, Is.EqualTo(expected.IntroComicCompleted));
            Assert.That(actual.OutroComicCompleted, Is.EqualTo(expected.OutroComicCompleted));
            Assert.That(actual.HasNormalCampaignCompletionReceipt,
                Is.EqualTo(expected.HasNormalCampaignCompletionReceipt));
            AssertReceipt(
                actual.NormalCampaignCompletionReceipt,
                expected.NormalCampaignCompletionReceipt);
            Assert.That(PerformanceFingerprint(actual.NormalStagePerformanceRecords),
                Is.EqualTo(PerformanceFingerprint(expected.NormalStagePerformanceRecords)));
            Assert.That(SnapshotFingerprint(actual.StageClearProfileSnapshot),
                Is.EqualTo(SnapshotFingerprint(expected.StageClearProfileSnapshot)));
            if (preservesDeaths)
            {
                Assert.That(actual.TotalDeaths, Is.EqualTo(expected.TotalDeaths));
            }
        }

        private static void AssertReceipt(
            NormalCampaignCompletionReceipt actual,
            NormalCampaignCompletionReceipt expected)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Version, Is.EqualTo(expected.Version));
            Assert.That(actual.CompletedStageId, Is.EqualTo(expected.CompletedStageId));
            Assert.That(actual.StageRunId, Is.EqualTo(expected.StageRunId));
            Assert.That(actual.ClearSource, Is.EqualTo(expected.ClearSource));
        }

        private static string Fingerprint(SaveSlotData slot)
        {
            if (slot == null)
            {
                return "<null>";
            }

            return string.Join(
                "|",
                slot.SlotNumber,
                slot.CurrentStageId.Value,
                slot.CurrentLevelGroupId ?? "<null>",
                slot.RemainingChances,
                slot.CampaignCompleted,
                slot.HasNormalCampaignCompletionReceipt,
                ReceiptFingerprint(slot.NormalCampaignCompletionReceipt),
                PerformanceFingerprint(slot.NormalStagePerformanceRecords),
                slot.IntroComicCompleted,
                slot.OutroComicCompleted,
                slot.TotalDeaths,
                slot.LastPlayedAt ?? "<null>",
                SnapshotFingerprint(slot.StageClearProfileSnapshot));
        }

        private static string ReceiptFingerprint(NormalCampaignCompletionReceipt receipt)
        {
            return receipt == null
                ? "<null>"
                : string.Join(
                    ":",
                    receipt.Version,
                    receipt.CompletedStageId ?? "<null>",
                    receipt.StageRunId ?? "<null>",
                    receipt.ClearSource);
        }

        private static string PerformanceFingerprint(NormalStagePerformanceRecord[] records)
        {
            if (records == null)
            {
                return "<null>";
            }

            var values = new string[records.Length];
            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                values[index] = record == null
                    ? "<null>"
                    : $"{record.Version}:{record.StageId.Value}:{record.BestCombinedPushFlipUses}";
            }

            return string.Join(",", values);
        }

        private static string SnapshotFingerprint(StageClearProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "<null>";
            }

            var records = new List<string>();
            foreach (var pair in snapshot.ClearRecordsByStageId)
            {
                var record = pair.Value;
                records.Add(
                    $"{pair.Key.Value}:{record.StageId.Value}:{record.HasAttempted}:" +
                    $"{record.HasCleared}:{record.ClearCount}:" +
                    $"{string.Join(",", record.ProcessedStageRunIds)}");
            }

            records.Sort(StringComparer.Ordinal);
            var processedRuns = new List<string>(snapshot.ProcessedStageRunIds);
            processedRuns.Sort(StringComparer.Ordinal);
            var processedAttempts = new List<string>(snapshot.ProcessedClearAttemptIds);
            processedAttempts.Sort(StringComparer.Ordinal);
            return string.Join(
                "|",
                snapshot.Version,
                string.Join(",", records),
                string.Join(",", processedRuns),
                string.Join(",", processedAttempts));
        }

        public enum InvalidClearRequestKind
        {
            Null,
            DefaultPlan,
            MismatchedReceipt,
            MismatchedPerformance,
        }

        public enum ReceiptState
        {
            Absent,
            PresentWithoutPayload,
            PresentWithPayload,
        }
    }
}
