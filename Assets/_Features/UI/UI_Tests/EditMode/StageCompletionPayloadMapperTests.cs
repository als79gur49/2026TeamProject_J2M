using System;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class StageCompletionPayloadMapperTests
    {
        [Test]
        public void StageResultPayloadMapper_UsesPresentationText_WithoutScoreRankReward()
        {
            var readModel = new MinimalStageCompletionReadModel(
                StageId.CreateOrThrow("payload-stage"),
                "Payload Stage",
                new MinimalStageCompletionResult(
                    StageId.CreateOrThrow("payload-stage"),
                    new StageRunId("run-a"),
                    new StageCompletionAttemptId("attempt-a"),
                    StageTerminalReason.Cleared,
                    wasCleared: true,
                    finalTickIndex: 24,
                    new StageObjectiveProgressSnapshot(true, true, true, true, 1, 1),
                    StageClearSource.Objective),
                "Presentation Title",
                "Presentation Summary",
                "Presentation Detail",
                "Continue",
                CreateNavigationRequest("payload-stage", StageNavigationKind.Continue),
                CreateNavigationRequest("payload-stage", StageNavigationKind.Retry),
                StageNavigationRequest.None);

            var payload = StageResultPayloadMapper.Map(readModel);

            Assert.That(payload.TitleText, Is.EqualTo("Presentation Title"));
            Assert.That(payload.SummaryText, Is.EqualTo("Presentation Summary"));
            Assert.That(payload.DetailText, Is.EqualTo("Presentation Detail"));
            Assert.That(payload.ContinueLabel, Is.EqualTo("Continue"));
            Assert.That(payload.SummaryText, Does.Not.Contain("Score"));
            Assert.That(payload.DetailText, Does.Not.Contain("Rank"));
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
            var payload = StageResultPayloadMapper.Map(CreateMinimalReadModel("stage-1-1"));
            var finalPayload = StageResultPayloadMapper.Map(CreateMinimalReadModel("stage-4-2"));

            Assert.That(payload.NextStageRequest.IsValid, Is.True);
            Assert.That(payload.NextStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(payload.ContinueStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(finalPayload.NextStageRequest.IsValid, Is.False);
        }

        [Test]
        public void ContinueRequest_RemainsValid_WithoutProgressionDefinition()
        {
            var payload = StageResultPayloadMapper.Map(CreateMinimalReadModel("free-stage"));

            Assert.That(payload.ContinueStageRequest.IsValid, Is.True);
            Assert.That(payload.ContinueStageRequest.StageId.Value, Is.EqualTo("free-stage"));
            Assert.That(payload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
        }

        [Test]
        public void RetryRequest_RemainsValid_WithoutProgressionDefinition()
        {
            var payload = StageResultPayloadMapper.Map(CreateMinimalReadModel("free-stage"));

            Assert.That(payload.RetryStageRequest.IsValid, Is.True);
            Assert.That(payload.RetryStageRequest.StageId.Value, Is.EqualTo("free-stage"));
            Assert.That(payload.RetryStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
        }

        private static MinimalStageCompletionReadModel CreateMinimalReadModel(string stageIdValue)
        {
            var stageId = StageId.CreateOrThrow(stageIdValue);
            return MinimalStageCompletionReadModelBuilder.Build(
                entry: null,
                new StageClearResult(
                    stageId,
                    new StageRunId("run-" + stageIdValue),
                    StageTerminalReason.Cleared,
                    wasCleared: true,
                    finalTickIndex: 1,
                    default,
                    Array.Empty<StageSessionMetricValue>(),
                    Array.Empty<StageChallengeRuntimeState>()));
        }

        private static StageNavigationRequest CreateNavigationRequest(
            string stageIdValue,
            StageNavigationKind navigationKind)
        {
            return new StageNavigationRequest(
                StageId.CreateOrThrow(stageIdValue),
                navigationKind,
                "test");
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
