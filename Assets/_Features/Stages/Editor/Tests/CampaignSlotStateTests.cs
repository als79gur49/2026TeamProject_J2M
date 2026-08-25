using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSlotStateTests
    {
        [Test]
        public void Parser_AbsentDocumentCreatesExplicitEmptyEntry()
        {
            var result = CampaignSlotParser.ParseEntry(2, null);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Diagnostic, Is.Null);
            Assert.That(result.Entry.SlotNumber, Is.EqualTo(2));
            Assert.That(result.Entry.IsEmpty, Is.True);
            Assert.That(result.Entry.State, Is.Null);
        }

        [Test]
        public void Parser_InvalidDocumentReturnsTypedDiagnosticWithoutState()
        {
            var document = CreateValidDocument();
            document.RemainingChances = 0;
            document.NormalStagePerformanceRecords = new[]
            {
                new NormalStagePerformanceRecordDocument
                {
                    Version = NormalStagePerformanceRecord.CurrentVersion + 1,
                    StageId = "stage-1-2",
                    BestCombinedPushFlipUses = -4,
                },
                null,
            };

            var result = CampaignSlotParser.ParseEntry(1, document);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Entry, Is.Null);
            Assert.That(result.Diagnostic.FailureKind,
                Is.EqualTo(CampaignSlotParseFailureKind.InvalidDocument));

            var firstCopy = result.Diagnostic.CopyRawDocument();
            var secondCopy = result.Diagnostic.CopyRawDocument();
            Assert.That(firstCopy, Is.Not.SameAs(document));
            Assert.That(firstCopy, Is.Not.SameAs(secondCopy));
            Assert.That(firstCopy.RemainingChances, Is.Zero);
            Assert.That(firstCopy.NormalStagePerformanceRecords, Has.Length.EqualTo(2));
            Assert.That(firstCopy.NormalStagePerformanceRecords[0].Version,
                Is.EqualTo(NormalStagePerformanceRecord.CurrentVersion + 1));
            Assert.That(firstCopy.NormalStagePerformanceRecords[1], Is.Null);

            firstCopy.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses = 99;
            Assert.That(secondCopy.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(-4));
        }

        [Test]
        public void Parser_DocumentSlotMismatchReturnsTypedDiagnostic()
        {
            var document = CreateValidDocument();

            var result = CampaignSlotParser.ParseEntry(2, document);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostic.FailureKind,
                Is.EqualTo(CampaignSlotParseFailureKind.SlotNumberMismatch));
            Assert.That(result.Diagnostic.ExpectedSlotNumber, Is.EqualTo(2));
            Assert.That(result.Diagnostic.CopyRawDocument().SlotNumber, Is.EqualTo(1));
        }

        [Test]
        public void Parser_AllowedSerializerAbsenceMaterializesCanonicalStateWithoutMutatingRaw()
        {
            var document = CreateValidDocument();
            document.LevelGroupId = null;
            document.LastPlayedAtUtc = null;
            document.HasNormalCampaignCompletionReceipt = false;
            document.NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceiptDocument();
            document.NormalStagePerformanceRecords = null;
            document.StageClearProfileSnapshot = null;

            Assert.That(CampaignSlotDocumentValidator.IsValid(document), Is.True);

            var result = CampaignSlotParser.ParseEntry(1, document);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Entry.IsEmpty, Is.False);
            Assert.That(result.Entry.State.CurrentLevelGroupId, Is.Empty);
            Assert.That(result.Entry.State.LastPlayedAt, Is.Empty);
            Assert.That(result.Entry.State.Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.Absent));
            Assert.That(result.Entry.State.NormalStagePerformanceRecords, Is.Empty);
            Assert.That(result.Entry.State.StageClearProfile.Records, Is.Empty);
            Assert.That(document.LevelGroupId, Is.Null);
            Assert.That(document.LastPlayedAtUtc, Is.Null);
            Assert.That(document.NormalCampaignCompletionReceipt, Is.Not.Null);
            Assert.That(document.NormalStagePerformanceRecords, Is.Null);
            Assert.That(document.StageClearProfileSnapshot, Is.Null);
        }

        [Test]
        public void JsonUtility_EmptyReceiptObjectMaterializesExactClrDefaultResidue()
        {
            const string json =
                "{\"SlotNumber\":1,\"StageId\":\"stage-1-1\",\"RemainingChances\":3," +
                "\"HasNormalCampaignCompletionReceipt\":false," +
                "\"NormalCampaignCompletionReceipt\":{}}";

            var document = JsonUtility.FromJson<CampaignSlotDocument>(json);

            Assert.That(document.NormalCampaignCompletionReceipt, Is.Not.Null);
            Assert.That(document.NormalCampaignCompletionReceipt.Version, Is.Zero);
            Assert.That(document.NormalCampaignCompletionReceipt.CompletedStageId, Is.Null);
            Assert.That(document.NormalCampaignCompletionReceipt.StageRunId, Is.Null);
            Assert.That(document.NormalCampaignCompletionReceipt.ClearSource, Is.Zero);
            Assert.That(CampaignSlotDocumentValidator.IsValid(document), Is.True);
            Assert.That(
                CampaignSlotParser.ParseEntry(1, document).Entry.State.Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.Absent));
        }

        [Test]
        public void JsonUtility_MixedNullAndEmptyReceiptStringsCanonicalizeToEmptyStrings()
        {
            const string json =
                "{\"SlotNumber\":1,\"StageId\":\"stage-1-1\",\"RemainingChances\":3," +
                "\"HasNormalCampaignCompletionReceipt\":false," +
                "\"NormalCampaignCompletionReceipt\":{" +
                "\"Version\":0,\"CompletedStageId\":\"\",\"StageRunId\":null," +
                "\"ClearSource\":0}}";

            var document = JsonUtility.FromJson<CampaignSlotDocument>(json);

            Assert.That(document.NormalCampaignCompletionReceipt.CompletedStageId, Is.Empty);
            Assert.That(document.NormalCampaignCompletionReceipt.StageRunId, Is.Empty);
            Assert.That(CampaignSlotDocumentValidator.IsValid(document), Is.True);
        }

        [Test]
        public void Parser_AbsentReceiptWithWriterSerializerResidueReturnsAbsent()
        {
            var document = CreateValidDocument();
            document.HasNormalCampaignCompletionReceipt = false;
            document.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceiptDocument
                {
                    Version = 0,
                    CompletedStageId = string.Empty,
                    StageRunId = string.Empty,
                    ClearSource = 0,
                };

            var result = CampaignSlotParser.ParseEntry(1, document);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Entry.State.Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.Absent));
        }

        [TestCase("", null)]
        [TestCase(null, "")]
        [TestCase(" ", null)]
        [TestCase(null, " ")]
        public void Parser_AbsentReceiptWithNonExactStringResidueReturnsInvalidDocumentDiagnostic(
            string completedStageId,
            string stageRunId)
        {
            var document = CreateValidDocument();
            document.HasNormalCampaignCompletionReceipt = false;
            document.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceiptDocument
                {
                    Version = 0,
                    CompletedStageId = completedStageId,
                    StageRunId = stageRunId,
                    ClearSource = 0,
                };

            var result = CampaignSlotParser.ParseEntry(1, document);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Entry, Is.Null);
            Assert.That(result.Diagnostic.FailureKind,
                Is.EqualTo(CampaignSlotParseFailureKind.InvalidDocument));
            Assert.That(
                result.Diagnostic.CopyRawDocument().NormalCampaignCompletionReceipt
                    .CompletedStageId,
                Is.EqualTo(completedStageId));
            Assert.That(
                result.Diagnostic.CopyRawDocument().NormalCampaignCompletionReceipt.StageRunId,
                Is.EqualTo(stageRunId));
        }

        [Test]
        public void Parser_AbsentReceiptWithPopulatedPayloadReturnsInvalidDocumentDiagnostic()
        {
            var document = CreateValidDocument();
            document.HasNormalCampaignCompletionReceipt = false;
            document.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceiptDocument
                {
                    Version = NormalCampaignCompletionReceipt.CurrentVersion,
                    CompletedStageId = "stage-1-1",
                    StageRunId = string.Empty,
                    ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
                };

            var result = CampaignSlotParser.ParseEntry(1, document);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Entry, Is.Null);
            Assert.That(result.Diagnostic.FailureKind,
                Is.EqualTo(CampaignSlotParseFailureKind.InvalidDocument));
            Assert.That(
                result.Diagnostic.CopyRawDocument().NormalCampaignCompletionReceipt.Version,
                Is.EqualTo(NormalCampaignCompletionReceipt.CurrentVersion));
        }

        [TestCase(1, null, null, 0)]
        [TestCase(1, "", "", 0)]
        [TestCase(0, null, null, 1)]
        [TestCase(0, "", "", 1)]
        public void Parser_AbsentReceiptWithExactStringPairAndNonDefaultNumericResidueReturnsInvalidDocumentDiagnostic(
            int version,
            string completedStageId,
            string stageRunId,
            int clearSource)
        {
            var document = CreateValidDocument();
            document.HasNormalCampaignCompletionReceipt = false;
            document.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceiptDocument
                {
                    Version = version,
                    CompletedStageId = completedStageId,
                    StageRunId = stageRunId,
                    ClearSource = clearSource,
                };

            var result = CampaignSlotParser.ParseEntry(1, document);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Entry, Is.Null);
            Assert.That(result.Diagnostic.FailureKind,
                Is.EqualTo(CampaignSlotParseFailureKind.InvalidDocument));
            var rawReceipt = result.Diagnostic.CopyRawDocument()
                .NormalCampaignCompletionReceipt;
            Assert.That(rawReceipt.Version, Is.EqualTo(version));
            Assert.That(rawReceipt.CompletedStageId, Is.EqualTo(completedStageId));
            Assert.That(rawReceipt.StageRunId, Is.EqualTo(stageRunId));
            Assert.That(rawReceipt.ClearSource, Is.EqualTo(clearSource));
        }

        [Test]
        public void Parser_PreservesReceiptAbsentPresentNullAndPresentPayloadStates()
        {
            var absent = CreateValidDocument();
            absent.HasNormalCampaignCompletionReceipt = false;
            absent.NormalCampaignCompletionReceipt = null;
            var presentNull = CreateValidDocument();
            presentNull.HasNormalCampaignCompletionReceipt = true;
            presentNull.NormalCampaignCompletionReceipt = null;
            var presentPayload = CreateValidDocument();

            var absentState = ParseState(absent);
            var presentNullState = ParseState(presentNull);
            var presentPayloadState = ParseState(presentPayload);

            Assert.That(absentState.Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.Absent));
            Assert.That(absentState.Receipt.Payload, Is.Null);
            Assert.That(presentNullState.Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.PresentWithoutPayload));
            Assert.That(presentNullState.Receipt.Payload, Is.Null);
            Assert.That(presentPayloadState.Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.PresentWithPayload));
            Assert.That(presentPayloadState.Receipt.Payload.Version,
                Is.EqualTo(NormalCampaignCompletionReceipt.CurrentVersion));
            Assert.That(presentPayloadState.Receipt.Payload.CompletedStageId.Value,
                Is.EqualTo("stage-1-1"));

            var absentRoundTrip = CampaignSlotStateDocumentMapper.ToDocument(absentState);
            var presentNullRoundTrip =
                CampaignSlotStateDocumentMapper.ToDocument(presentNullState);
            var presentPayloadRoundTrip =
                CampaignSlotStateDocumentMapper.ToDocument(presentPayloadState);

            Assert.That(absentRoundTrip.HasNormalCampaignCompletionReceipt, Is.False);
            Assert.That(absentRoundTrip.NormalCampaignCompletionReceipt, Is.Null);
            Assert.That(presentNullRoundTrip.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(presentNullRoundTrip.NormalCampaignCompletionReceipt, Is.Null);
            Assert.That(presentPayloadRoundTrip.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(presentPayloadRoundTrip.NormalCampaignCompletionReceipt, Is.Not.Null);
            Assert.That(ParseState(absentRoundTrip).Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.Absent));
            Assert.That(ParseState(presentNullRoundTrip).Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.PresentWithoutPayload));
            Assert.That(ParseState(presentPayloadRoundTrip).Receipt.Presence,
                Is.EqualTo(CampaignReceiptPresence.PresentWithPayload));
        }

        [Test]
        public void State_DeepCopiesInputsAndReadOnlyCollectionsRejectExternalMutation()
        {
            var document = CreateValidDocument();
            var state = ParseState(document);

            document.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses = 99;
            document.StageClearProfileSnapshot.Records[0].ClearCount = 99;
            document.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds[0] = "changed";
            document.StageClearProfileSnapshot.ProcessedStageRunIds[0] = "changed";

            Assert.That(state.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses,
                Is.EqualTo(7));
            Assert.That(state.StageClearProfile.Records[0].ClearCount, Is.EqualTo(2));
            Assert.That(state.StageClearProfile.Records[0].ProcessedStageRunIds[0],
                Is.EqualTo("run-1"));
            Assert.That(state.StageClearProfile.ProcessedStageRunIds[0],
                Is.EqualTo("run-1"));

            Assert.Throws<NotSupportedException>(() =>
                ((IList<CampaignStagePerformanceState>)state.NormalStagePerformanceRecords)
                .Add(state.NormalStagePerformanceRecords[0]));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CampaignStageClearRecordState>)state.StageClearProfile.Records)
                .Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)state.StageClearProfile.ProcessedStageRunIds)
                .Add("run-external"));
        }

        [Test]
        public void StateTypes_HaveNoPublicConstructorsOrPublicPropertySetters()
        {
            var stateTypes = new[]
            {
                typeof(CampaignSlotState),
                typeof(CampaignSlotEntry),
                typeof(CampaignReceiptState),
                typeof(CampaignCompletionReceiptState),
                typeof(CampaignStagePerformanceState),
                typeof(CampaignStageClearProfileState),
                typeof(CampaignStageClearRecordState),
            };

            foreach (var stateType in stateTypes)
            {
                Assert.That(
                    stateType.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
                    Is.Empty,
                    stateType.FullName);
                foreach (var property in stateType.GetProperties(
                             BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.That(property.SetMethod, Is.Null,
                        $"{stateType.FullName}.{property.Name}");
                }
            }
        }

        [Test]
        public void StateDocumentRoundTrip_IsCanonicalAndLossless()
        {
            var document = CreateValidDocument();
            document.NormalStagePerformanceRecords = new[]
            {
                CreatePerformanceDocument("stage-1-2", 9),
                CreatePerformanceDocument("stage-1-1", 7),
            };
            document.StageClearProfileSnapshot.Records = new[]
            {
                CreateClearRecordDocument("stage-1-2", 3, "run-2"),
                CreateClearRecordDocument("stage-1-1", 2, "run-1"),
            };
            document.StageClearProfileSnapshot.ProcessedStageRunIds =
                new[] { "run-2", "run-1" };
            document.StageClearProfileSnapshot.ProcessedClearAttemptIds =
                new[] { "attempt-2", "attempt-1" };

            var firstState = ParseState(document);
            var canonicalDocument = CampaignSlotStateDocumentMapper.ToDocument(firstState);
            var secondState = ParseState(canonicalDocument);

            Assert.That(canonicalDocument.NormalStagePerformanceRecords[0].StageId,
                Is.EqualTo("stage-1-1"));
            Assert.That(canonicalDocument.NormalStagePerformanceRecords[1].StageId,
                Is.EqualTo("stage-1-2"));
            Assert.That(canonicalDocument.StageClearProfileSnapshot.Records[0].StageId,
                Is.EqualTo("stage-1-1"));
            Assert.That(canonicalDocument.StageClearProfileSnapshot.ProcessedStageRunIds,
                Is.EqualTo(new[] { "run-1", "run-2" }));
            AssertStateEquivalent(firstState, secondState);
            Assert.That(CampaignSlotDocumentValidator.IsValid(canonicalDocument), Is.True);
        }

        [Test]
        public void StateFactory_NewGameCreatesCanonicalOccupiedState()
        {
            var definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            try
            {
                var entry = new CampaignStageSequenceEntry();
                entry.Set(StageId.CreateOrThrow("stage-1-1"), "level-1");
                definition.SetEntries(new[] { entry });
                var resolver = new CampaignStageSequenceResolver(definition);

                var state = CampaignSlotStateFactory.CreateNewGame(
                    3,
                    resolver,
                    null);

                Assert.That(state.SlotNumber, Is.EqualTo(3));
                Assert.That(state.CurrentStageId.Value, Is.EqualTo("stage-1-1"));
                Assert.That(state.CurrentLevelGroupId, Is.EqualTo("level-1"));
                Assert.That(state.RemainingChances,
                    Is.EqualTo(CampaignSaveSlotPolicy.DefaultRemainingChances));
                Assert.That(state.Receipt.Presence,
                    Is.EqualTo(CampaignReceiptPresence.Absent));
                Assert.That(state.NormalStagePerformanceRecords, Is.Empty);
                Assert.That(state.StageClearProfile.Records, Is.Empty);
                Assert.That(state.LastPlayedAt, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static CampaignSlotState ParseState(CampaignSlotDocument document)
        {
            var result = CampaignSlotParser.ParseEntry(document.SlotNumber, document);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Entry.IsEmpty, Is.False);
            return result.Entry.State;
        }

        private static CampaignSlotDocument CreateValidDocument()
        {
            return new CampaignSlotDocument
            {
                SlotNumber = 1,
                StageId = "stage-1-1",
                LevelGroupId = "level-1",
                RemainingChances = 2,
                CampaignCompleted = false,
                HasNormalCampaignCompletionReceipt = true,
                NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceiptDocument
                {
                    Version = NormalCampaignCompletionReceipt.CurrentVersion,
                    CompletedStageId = "stage-1-1",
                    StageRunId = string.Empty,
                    ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
                },
                IntroComicCompleted = true,
                OutroComicCompleted = false,
                NormalStagePerformanceRecords = new[]
                {
                    CreatePerformanceDocument("stage-1-1", 7),
                },
                TotalDeaths = 4,
                LastPlayedAtUtc = "2026-08-24T00:00:00Z",
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument
                {
                    Version = 2,
                    Records = new[]
                    {
                        CreateClearRecordDocument("stage-1-1", 2, "run-1"),
                    },
                    ProcessedStageRunIds = new[] { "run-1" },
                    ProcessedClearAttemptIds = new[] { "attempt-1" },
                },
            };
        }

        private static NormalStagePerformanceRecordDocument CreatePerformanceDocument(
            string stageId,
            int bestCombinedPushFlipUses)
        {
            return new NormalStagePerformanceRecordDocument
            {
                Version = NormalStagePerformanceRecord.CurrentVersion,
                StageId = stageId,
                BestCombinedPushFlipUses = bestCombinedPushFlipUses,
            };
        }

        private static PlayerStageClearRecordDocument CreateClearRecordDocument(
            string stageId,
            int clearCount,
            string processedStageRunId)
        {
            return new PlayerStageClearRecordDocument
            {
                StageId = stageId,
                HasAttempted = true,
                HasCleared = true,
                ClearCount = clearCount,
                ProcessedStageRunIds = new[] { processedStageRunId },
            };
        }

        private static void AssertStateEquivalent(
            CampaignSlotState expected,
            CampaignSlotState actual)
        {
            Assert.That(actual.SlotNumber, Is.EqualTo(expected.SlotNumber));
            Assert.That(actual.CurrentStageId, Is.EqualTo(expected.CurrentStageId));
            Assert.That(actual.CurrentLevelGroupId, Is.EqualTo(expected.CurrentLevelGroupId));
            Assert.That(actual.RemainingChances, Is.EqualTo(expected.RemainingChances));
            Assert.That(actual.CampaignCompleted, Is.EqualTo(expected.CampaignCompleted));
            Assert.That(actual.Receipt.Presence, Is.EqualTo(expected.Receipt.Presence));
            if (expected.Receipt.Payload == null)
            {
                Assert.That(actual.Receipt.Payload, Is.Null);
            }
            else
            {
                Assert.That(actual.Receipt.Payload, Is.Not.Null);
                Assert.That(actual.Receipt.Payload.Version,
                    Is.EqualTo(expected.Receipt.Payload.Version));
                Assert.That(actual.Receipt.Payload.CompletedStageId,
                    Is.EqualTo(expected.Receipt.Payload.CompletedStageId));
                Assert.That(actual.Receipt.Payload.StageRunId,
                    Is.EqualTo(expected.Receipt.Payload.StageRunId));
                Assert.That(actual.Receipt.Payload.ClearSource,
                    Is.EqualTo(expected.Receipt.Payload.ClearSource));
            }

            Assert.That(actual.IntroComicCompleted, Is.EqualTo(expected.IntroComicCompleted));
            Assert.That(actual.OutroComicCompleted, Is.EqualTo(expected.OutroComicCompleted));
            Assert.That(actual.TotalDeaths, Is.EqualTo(expected.TotalDeaths));
            Assert.That(actual.LastPlayedAt, Is.EqualTo(expected.LastPlayedAt));
            Assert.That(actual.NormalStagePerformanceRecords.Count,
                Is.EqualTo(expected.NormalStagePerformanceRecords.Count));
            for (var index = 0; index < expected.NormalStagePerformanceRecords.Count; index++)
            {
                Assert.That(actual.NormalStagePerformanceRecords[index].Version,
                    Is.EqualTo(expected.NormalStagePerformanceRecords[index].Version));
                Assert.That(actual.NormalStagePerformanceRecords[index].StageId,
                    Is.EqualTo(expected.NormalStagePerformanceRecords[index].StageId));
                Assert.That(actual.NormalStagePerformanceRecords[index]
                        .BestCombinedPushFlipUses,
                    Is.EqualTo(expected.NormalStagePerformanceRecords[index]
                        .BestCombinedPushFlipUses));
            }

            Assert.That(actual.StageClearProfile.Version,
                Is.EqualTo(expected.StageClearProfile.Version));
            Assert.That(actual.StageClearProfile.Records.Count,
                Is.EqualTo(expected.StageClearProfile.Records.Count));
            for (var index = 0; index < expected.StageClearProfile.Records.Count; index++)
            {
                var expectedRecord = expected.StageClearProfile.Records[index];
                var actualRecord = actual.StageClearProfile.Records[index];
                Assert.That(actualRecord.StageId, Is.EqualTo(expectedRecord.StageId));
                Assert.That(actualRecord.HasAttempted,
                    Is.EqualTo(expectedRecord.HasAttempted));
                Assert.That(actualRecord.HasCleared,
                    Is.EqualTo(expectedRecord.HasCleared));
                Assert.That(actualRecord.ClearCount,
                    Is.EqualTo(expectedRecord.ClearCount));
                Assert.That(actualRecord.ProcessedStageRunIds,
                    Is.EqualTo(expectedRecord.ProcessedStageRunIds));
            }

            Assert.That(actual.StageClearProfile.ProcessedStageRunIds,
                Is.EqualTo(expected.StageClearProfile.ProcessedStageRunIds));
            Assert.That(actual.StageClearProfile.ProcessedClearAttemptIds,
                Is.EqualTo(expected.StageClearProfile.ProcessedClearAttemptIds));
        }
    }
}
