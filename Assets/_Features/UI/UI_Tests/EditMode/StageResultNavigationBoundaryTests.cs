using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class StageResultNavigationBoundaryTests
    {
        private static readonly string[] StageResultSourcePaths =
        {
            "Assets/_Features/UI/UI_Application/Runtime/StageResult/StageResultScreenPresenters.cs",
            "Assets/_Features/UI/UI_Application/Runtime/StageCompletionPayloadMappers.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/SettingsScreenRuntimeBuilder.cs",
            "Assets/_Features/UI/UI_Screens/Runtime/StageResultScreenView.cs",
        };

        [Test]
        public void StageResult_DoesNotReferenceProgressionCommitTypes()
        {
            AssertStageResultSourcesDoNotContain(new[]
            {
                "StageProgressionCommit",
                "ProgressionCommit",
                "ProgressPatch",
                "StageProgressionCommitter",
            });
        }

        [Test]
        public void StageResult_DoesNotReferenceRewardCommitTypes()
        {
            AssertStageResultSourcesDoNotContain(new[]
            {
                "RewardEvaluator",
                "RewardCommit",
                "RewardCommitter",
                "InventoryPatch",
            });
        }

        [Test]
        public void StageResult_IsPresentationEndpoint_NotGameplayAuthority()
        {
            AssertStageResultSourcesDoNotContain(new[]
            {
                "WorldState",
                "TickResult",
                "GameplayPresentationFrame",
                "GameplayPresentationState",
            });
        }

        [Test]
        public void StageResult_EmitsOnlyStageNavigationRequest_ForContinueRetryAndNextStage()
        {
            var payloadNavigationProperties = typeof(StageResultScreenPayload)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.Name.EndsWith("Request", StringComparison.Ordinal))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();
            var payloadNavigationTypes = payloadNavigationProperties
                .Select(property => property.PropertyType)
                .Distinct()
                .ToArray();

            Assert.That(
                payloadNavigationTypes,
                Is.EqualTo(new[] { typeof(StageNavigationRequest) }),
                "StageResult screen actions stay intent-only; continue/retry/next-stage are StageNavigationRequest values, not direct stage reward/progression commits.");
            Assert.That(
                payloadNavigationProperties.Select(property => property.Name).ToArray(),
                Is.EqualTo(new[] { "ContinueStageRequest", "NextStageRequest", "RetryStageRequest" }));
        }

        [Test]
        public void NavigationRequests_AreStageIdBased()
        {
            var stageId = StageId.CreateOrThrow("stage-result-boundary");
            var request = new StageNavigationRequest(stageId, StageNavigationKind.Continue, "boundary-test");
            var payload = new StageResultScreenPayload(
                "Continue",
                request,
                StageNavigationRequest.None,
                StageNavigationRequest.None);

            Assert.That(payload.ContinueStageRequest.IsValid, Is.True);
            Assert.That(payload.ContinueStageRequest.StageId, Is.EqualTo(stageId));
        }

        private static void AssertStageResultSourcesDoNotContain(string[] forbiddenTokens)
        {
            foreach (var sourcePath in StageResultSourcePaths)
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(token),
                        $"{sourcePath}: StageResult is a UI presentation/navigation endpoint; stage reward/progression commit lane remains stage-owned.");
                }
            }
        }
    }
}
