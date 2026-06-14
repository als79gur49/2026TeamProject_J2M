using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class StageCompletionPayloadMapperTests
    {
        [Test]
        public void StageResultPayloadMapper_MapsNavigationRequests_WithoutScoreRankResultData()
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
                CreateNavigationRequest("payload-stage", StageNavigationKind.Continue),
                CreateNavigationRequest("payload-stage", StageNavigationKind.Retry),
                StageNavigationRequest.None);

            var payload = StageResultPayloadMapper.Map(readModel);

            var payloadProperties = typeof(StageResultScreenPayload)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name);
            Assert.That(payloadProperties, Does.Not.Contain("TitleText"));
            Assert.That(payloadProperties, Does.Not.Contain("DetailText"));
            Assert.That(payloadProperties, Does.Not.Contain("ContinueLabel"));
            Assert.That(payload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
        }

        [Test]
        public void StageResultTextSchema_IsNotExposedByStagePresentationTypes()
        {
            var removedNames = new[]
            {
                "ResultTitle",
                "ResultSummaryText",
                "ResultDetailText",
                "resultTitle",
                "resultSummaryText",
                "resultDetailText",
                "ResultContinueLabel",
                "resultContinueLabel",
            };
            var definitionMembers = typeof(StagePresentationDefinition)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(member => member.Name);
            var resolvedMembers = typeof(StagePresentationResolvedData)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(member => member.Name);

            foreach (var removedName in removedNames)
            {
                Assert.That(definitionMembers, Does.Not.Contain(removedName));
                Assert.That(resolvedMembers, Does.Not.Contain(removedName));
            }

            Assert.That(typeof(StagePresentationDefinition).GetProperty("ResultContinueLabel"), Is.Null);
            Assert.That(typeof(StagePresentationResolvedData).GetProperty("ResultContinueLabel"), Is.Null);
        }

        [Test]
        public void StageResultPayloadMapper_NonFinalClear_MapsNextStageAndContinueAsStageNavigationRequests()
        {
            var payload = StageResultPayloadMapper.Map(CreateMinimalReadModel("stage-1-1"));

            Assert.That(payload.NextStageRequest.IsValid, Is.True);
            Assert.That(payload.NextStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(payload.NextStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.NextStage));
            Assert.That(payload.ContinueStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(payload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.NextStage));
            Assert.That(payload.ContinueStageRequest.TransitionHint.Kind, Is.EqualTo(StageTransitionKind.StageClearNext));
        }

        [Test]
        public void StageResultPayloadMapper_FinalClear_HasNoNextStageAndContinueFallsBackToCurrentStage()
        {
            var finalPayload = StageResultPayloadMapper.Map(CreateMinimalReadModel("stage-4-2"));

            Assert.That(finalPayload.NextStageRequest.IsValid, Is.False);
            Assert.That(finalPayload.ContinueStageRequest.IsValid, Is.True);
            Assert.That(finalPayload.ContinueStageRequest.StageId.Value, Is.EqualTo("stage-4-2"));
            Assert.That(finalPayload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
            Assert.That(finalPayload.ContinueStageRequest.TransitionHint.Kind, Is.EqualTo(StageTransitionKind.StageClearNext));
        }

        [Test]
        public void StageResultPayloadMapper_RetryRequest_StaysStageIdBasedAndDoesNotDependOnSceneLocalDefault()
        {
            var payload = StageResultPayloadMapper.Map(CreateMinimalReadModel("stage-2-2"));

            Assert.That(
                payload.RetryStageRequest.StageId.Value,
                Is.EqualTo("stage-2-2"),
                "Retry is a StageNavigationRequest boundary from the StageCompletionReadModel stage id; UI does not rebuild stage runtime or fall back to a scene-local default stage.");
            Assert.That(payload.RetryStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(payload.RetryStageRequest.Source, Is.EqualTo("stage-result-retry"));
            Assert.That(payload.RetryStageRequest.TransitionHint.Kind, Is.EqualTo(StageTransitionKind.StageRetryManual));
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

    }
}
