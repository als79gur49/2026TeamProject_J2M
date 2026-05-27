using System;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class StageCompletionPayloadMapperTests
    {
        [Test]
        public void StageResultPayloadMapper_MapsReadModel_WithFallbackSummaryAndDetail()
        {
            var readModel = new StageCompletionReadModel(
                StageId.CreateOrThrow("payload-stage"),
                "Payload Stage",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new StageClearResult(
                    StageId.CreateOrThrow("payload-stage"),
                    new StageRunId("run-a"),
                    StageTerminalReason.Cleared,
                    wasCleared: true,
                    finalTickIndex: 24,
                    default,
                    Array.Empty<StageSessionMetricValue>(),
                    Array.Empty<StageChallengeRuntimeState>()),
                new StageClearEvaluationResult(
                    StageId.CreateOrThrow("payload-stage"),
                    new StageRunId("run-a"),
                    wasCleared: true,
                    score: 320,
                    starsEarned: 2,
                    rankId: "a",
                    challengeResults: Array.Empty<StageChallengeEvaluationResult>()),
                new RewardGrantResult(
                    StageId.CreateOrThrow("payload-stage"),
                    new StageRunId("run-a"),
                    Array.Empty<RewardGrantEntry>(),
                    Array.Empty<string>(),
                    Array.Empty<RewardGrantId>(),
                    wasFirstClear: false),
                PlayerStageProgress.CreateEmpty(StageId.CreateOrThrow("payload-stage")));

            var payload = StageResultPayloadMapper.Map(readModel);

            Assert.That(payload.TitleText, Is.EqualTo("Stage Cleared"));
            Assert.That(payload.SummaryText, Does.Contain("Payload Stage"));
            Assert.That(payload.DetailText, Does.Contain("Tick 24"));
            Assert.That(payload.ContinueLabel, Is.EqualTo("Continue"));
        }

        [Test]
        public void RewardPopupPayloadMapper_MapsGrantedRewards_ToUiItems()
        {
            var readModel = new StageCompletionReadModel(
                StageId.CreateOrThrow("reward-payload"),
                "Reward Payload",
                "Result",
                string.Empty,
                string.Empty,
                "Continue",
                new StageClearResult(
                    StageId.CreateOrThrow("reward-payload"),
                    new StageRunId("run-a"),
                    StageTerminalReason.Cleared,
                    wasCleared: true,
                    finalTickIndex: 4,
                    default,
                    Array.Empty<StageSessionMetricValue>(),
                    Array.Empty<StageChallengeRuntimeState>()),
                new StageClearEvaluationResult(
                    StageId.CreateOrThrow("reward-payload"),
                    new StageRunId("run-a"),
                    wasCleared: true,
                    score: 100,
                    starsEarned: 1,
                    rankId: "b",
                    challengeResults: Array.Empty<StageChallengeEvaluationResult>()),
                new RewardGrantResult(
                    StageId.CreateOrThrow("reward-payload"),
                    new StageRunId("run-a"),
                    new[]
                    {
                        new RewardGrantEntry(
                            "first-clear",
                            new RewardEntry
                            {
                                RewardId = "coin",
                                Amount = 50,
                            },
                            new RewardGrantId("reward-payload:first-clear")),
                    },
                    new[] { "first-clear" },
                    new[] { new RewardGrantId("reward-payload:first-clear") },
                    wasFirstClear: true),
                PlayerStageProgress.CreateEmpty(StageId.CreateOrThrow("reward-payload")));

            var payload = RewardPopupPayloadMapper.Map(readModel);

            Assert.That(payload.Items.Count, Is.EqualTo(1));
            Assert.That(payload.Items[0].LabelText, Is.EqualTo("coin"));
            Assert.That(payload.Items[0].Amount, Is.EqualTo(50));
            Assert.That(payload.SummaryText, Does.Contain("First-clear"));
        }

        [Test]
        public void StageResultPayloadMapper_MapsCanonicalCampaignNextStageRequest()
        {
            var payload = StageResultPayloadMapper.Map(CreateReadModel("stage-1-1"));
            var finalPayload = StageResultPayloadMapper.Map(CreateReadModel("stage-4-2"));

            Assert.That(payload.NextStageRequest.IsValid, Is.True);
            Assert.That(payload.NextStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(payload.ContinueStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(finalPayload.NextStageRequest.IsValid, Is.False);
        }

        private static StageCompletionReadModel CreateReadModel(string stageIdValue)
        {
            var stageId = StageId.CreateOrThrow(stageIdValue);
            return new StageCompletionReadModel(
                stageId,
                stageIdValue,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new StageClearResult(
                    stageId,
                    new StageRunId("run-" + stageIdValue),
                    StageTerminalReason.Cleared,
                    wasCleared: true,
                    finalTickIndex: 1,
                    default,
                    Array.Empty<StageSessionMetricValue>(),
                    Array.Empty<StageChallengeRuntimeState>()),
                new StageClearEvaluationResult(
                    stageId,
                    new StageRunId("run-" + stageIdValue),
                    wasCleared: true,
                    score: 0,
                    starsEarned: 0,
                    rankId: string.Empty,
                    challengeResults: Array.Empty<StageChallengeEvaluationResult>()),
                new RewardGrantResult(
                    stageId,
                    new StageRunId("run-" + stageIdValue),
                    Array.Empty<RewardGrantEntry>(),
                    Array.Empty<string>(),
                    Array.Empty<RewardGrantId>(),
                    wasFirstClear: false),
                PlayerStageProgress.CreateEmpty(stageId));
        }
    }
}
