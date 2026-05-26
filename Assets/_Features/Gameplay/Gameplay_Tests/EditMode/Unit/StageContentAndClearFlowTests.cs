using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.UIAccess.DebugCommands;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageContentAndClearFlowTests
    {
        [Test]
        public void StageIdNormalizer_NormalizesWhitespaceUnderscoreAndSlash_IntoCanonicalLowerKebabCase()
        {
            Assert.That(StageIdNormalizer.Normalize(" Boss_Stage/01 "), Is.EqualTo("boss-stage-01"));
            Assert.That(StageId.TryCreate(" Boss_Stage/01 ", out var stageId), Is.True);
            Assert.That(stageId.Value, Is.EqualTo("boss-stage-01"));
            Assert.That(StageIdNormalizer.TryNormalize("boss@stage", out _, out _), Is.False);
        }

        [Test]
        public void StageCatalogResolver_ResolvesDeprecatedAlias_ToCurrentStageId()
        {
            var entry = CreateEntry("tutorial-stage");
            var aliasTable = ScriptableObject.CreateInstance<StageIdAliasTable>();
            aliasTable.SetEntries(new[]
            {
                new StageIdAliasEntry
                {
                    DeprecatedStageId = " Tutorial Stage ",
                    CurrentStageId = entry.StageId,
                },
            });

            var resolver = new StageCatalogResolver(new TestStageCatalogProvider(new[] { entry }, aliasTable));

            Assert.That(resolver.TryResolve("Tutorial Stage", out var resolvedEntry), Is.True);
            Assert.That(resolvedEntry, Is.SameAs(entry));
        }

        [Test]
        public void StageCatalogResolver_ResolveOrThrow_ByCanonicalStageId_ReturnsEntry()
        {
            var entry = CreateEntry("combined-gameplay-showcase");
            var resolver = new StageCatalogResolver(new TestStageCatalogProvider(new[] { entry }, aliasTable: null));

            var resolved = resolver.ResolveOrThrow(StageId.CreateOrThrow("combined-gameplay-showcase"));

            Assert.That(resolved, Is.SameAs(entry));
        }

        [Test]
        public void StageRuntimeContentResolver_CatalogMode_ResolvesLaunchContextOnly()
        {
            StageLaunchContextStore.Clear();
            try
            {
                var tutorialEntry = CreateEntry("tutorial-scene");
                var provider = CreateCatalogProvider(new[] { CreateEntry("combined-gameplay-showcase"), tutorialEntry }, aliasTable: null);
                var resolver = new StageRuntimeContentResolver();
                StageLaunchContextStore.SetCurrent(tutorialEntry.StageId);

                var resolved = resolver.Resolve(
                    StageLoadRequest.CreateLaunchContextOnly(
                        provider,
                        sceneName: "StageRuntimeContentResolverTests"));

                Assert.That(resolved.Entry, Is.SameAs(tutorialEntry));
                Assert.That(resolved.UsedLaunchContext, Is.True);
            }
            finally
            {
                StageLaunchContextStore.Clear();
            }
        }

        [Test]
        public void StageRuntimeContentResolver_CatalogMode_ThrowsWhenLaunchContextIsMissing()
        {
            StageLaunchContextStore.Clear();
            var entry = CreateEntry("tutorial-scene");
            var provider = CreateCatalogProvider(new[] { entry }, aliasTable: null);
            var resolver = new StageRuntimeContentResolver();

            var exception = Assert.Throws<InvalidOperationException>(
                () => resolver.Resolve(
                    StageLoadRequest.CreateLaunchContextOnly(
                        provider,
                        sceneName: "StageRuntimeContentResolverTests")));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("Tools/Stages/Direct Play/Launch Current Scene", exception.Message);
        }

        [Test]
        public void StageCatalogValidator_DetectsDuplicateStageIds_AndSharedPresentationReuse()
        {
            var sharedPresentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var entryA = CreateEntry("shared-stage", presentationDefinition: sharedPresentation);
            var entryB = CreateEntry("shared-stage", presentationDefinition: sharedPresentation);
            sharedPresentation.SetOwnerMetadata(entryA, string.Empty);

            var validator = new StageCatalogValidator();
            var report = validator.ValidateEntries(
                new[] { entryA, entryB },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "stage-id.duplicate"), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == "companion.presentation.reused"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_InvalidBgmKey_ReportsError()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentation, "bgmReference", new Game.Shared.AudioContracts.StageBgmReference("Invalid Key"));
            var entry = CreateEntry("stage-a", presentationDefinition: presentation);
            presentation.SetOwnerMetadata(entry, string.Empty);

            var validator = new StageCatalogValidator();
            var report = validator.ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "bgm-key.invalid"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_WithValidPrefab_Passes()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 100, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(
                report.Issues.Any(issue => issue.Code.StartsWith("presentation.tile-feature", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsNonPositiveTileId()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 0, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.tile-id-non-positive"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsDuplicateTileId()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(
                gameplayTileId: 100,
                bindings: new[]
                {
                    new TileFeaturePresentationBinding
                    {
                        TileId = 100,
                        VisualPrefab = prefab,
                    },
                    new TileFeaturePresentationBinding
                    {
                        TileId = 100,
                        VisualPrefab = prefab,
                    },
                });

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.tile-id-duplicate"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsMissingGameplayTileFeature()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 404, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.tile-id-missing"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsNullPrefab()
        {
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 100, visualPrefab: null);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.prefab-null"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsPrefabWithoutTarget()
        {
            var prefab = new GameObject("MissingTargetPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 100, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.prefab-target-missing"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_InvalidGameplayDefinition_ReportsError()
        {
            var entry = CreateEntry("invalid-objective");
            var invalidStage = CreateMinimalStageDefinition("invalid-objective");
            SetPrivateField(
                invalidStage,
                "objective",
                new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ConditionEntries = Array.Empty<StageObjectiveConditionEntry>(),
                });
            entry.AssignGameplayDefinition(invalidStage);

            var validator = new StageCatalogValidator();
            var report = validator.ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "gameplay.definition.invalid"), Is.True);
            Assert.That(
                report.Issues.Any(issue => issue.Message.Contains("requires at least one required condition")),
                Is.True);
        }

        [Test]
        public void CampaignStageObjectiveAudit_FlagsDisabledObjectiveInProductionStages()
        {
            var entry = CreateEntry("stage-2-2");

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "objective.production-disabled"), Is.True);
        }

        [Test]
        public void ObjectiveConditionEntry_MissingDisplayText_WarnsOrErrors()
        {
            var entry = CreateEntry("stage-2-2");
            var condition = CreatePlayerAtExitCondition();
            AssignExitObjective(
                entry.GameplayDefinition,
                condition,
                displayText: string.Empty);

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "objective.display-text-missing"), Is.True);

            UnityEngine.Object.DestroyImmediate(condition);
        }

        [Test]
        public void ObjectiveStableConditionId_Duplicate_WarnsOrErrors()
        {
            var entry = CreateEntry("stage-2-2");
            var condition = CreatePlayerAtExitCondition();
            AssignExitObjective(
                entry.GameplayDefinition,
                condition,
                displayText: "Reach the exit zone",
                duplicateStableId: true);

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(
                report.Issues.Any(issue =>
                    issue.Code == "gameplay.definition.invalid" &&
                    issue.Message.Contains("duplicate condition stable id")),
                Is.True);

            UnityEngine.Object.DestroyImmediate(condition);
        }

        [Test]
        public void StageSessionTracker_TerminalResult_IsEmittedOnlyOnce_AndResetCreatesNewRun()
        {
            var tracker = new StageSessionTracker();
            var stageId = StageId.CreateOrThrow("session-stage");
            var firstState = tracker.Start(stageId, startTickIndex: 0);
            var tickResult = new TickResult(3, new[] { TickPhase.Plan, TickPhase.Resolve }, Array.Empty<string>());

            tracker.Advance(tickResult, forcedTerminalReason: StageTerminalReason.Cleared);

            Assert.That(tracker.TryCreateClearResult(out var clearResult), Is.True);
            Assert.That(clearResult.StageRunId, Is.EqualTo(firstState.RunId));
            Assert.That(clearResult.WasCleared, Is.True);
            Assert.That(tracker.TryCreateClearResult(out _), Is.False);

            tracker.Reset();
            var restarted = tracker.Start(stageId, startTickIndex: 5);

            Assert.That(restarted.RunId, Is.Not.EqualTo(firstState.RunId));
            Assert.That(restarted.CurrentTickIndex, Is.EqualTo(5));
            Assert.That(restarted.SessionMetrics, Is.Empty);
        }

        [Test]
        public void StageSessionState_DoesNotExposeWorldAuthorityFields()
        {
            var propertyNames = typeof(StageSessionState)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(propertyNames, Has.No.Member("WorldState"));
            Assert.That(propertyNames, Has.No.Member("InitialEntities"));
            Assert.That(propertyNames, Has.No.Member("EntityStates"));
            Assert.That(propertyNames, Has.No.Member("Occupancy"));
            Assert.That(propertyNames, Has.No.Member("Hp"));
            Assert.That(propertyNames, Has.No.Member("Position"));
        }

        [Test]
        public void StageConditionAssets_DoNotImplementMutableRuntimeInterfaces_Directly()
        {
            var conditionAssetTypes = typeof(StageConditionAsset).Assembly
                .GetTypes()
                .Where(type => typeof(StageConditionAsset).IsAssignableFrom(type) && !type.IsAbstract)
                .ToArray();

            Assert.That(conditionAssetTypes, Is.Not.Empty);
            for (var i = 0; i < conditionAssetTypes.Length; i++)
            {
                Assert.That(typeof(IStageConditionRuntime).IsAssignableFrom(conditionAssetTypes[i]), Is.False, conditionAssetTypes[i].FullName);
            }
        }

        [Test]
        public void RewardEvaluator_GrantOnce_UsesPreUpdateProgress_AndDeprecatedRuleIds()
        {
            var rewardDefinition = ScriptableObject.CreateInstance<StageRewardDefinition>();
            var rewardRule = new StageRewardRuleDefinition
            {
                TriggerKind = StageRewardTriggerKind.Clear,
                GrantOnce = true,
                Rewards = new[]
                {
                    new RewardEntry
                    {
                        RewardId = "coin",
                        Amount = 100,
                    },
                },
            };
            rewardRule.SetRuleId("first-clear");
            rewardRule.SetDeprecatedRuleIds(new[] { "legacy-first-clear" });
            SetPrivateField(rewardDefinition, "rules", new[] { rewardRule });

            var stageId = StageId.CreateOrThrow("reward-stage");
            var evaluationResult = new StageClearEvaluationResult(
                stageId,
                new StageRunId("run-a"),
                wasCleared: true,
                score: 200,
                starsEarned: 3,
                rankId: "s",
                challengeResults: Array.Empty<StageChallengeEvaluationResult>());

            var consumedByAlias = PlayerStageProgress.CreateEmpty(stageId);
            consumedByAlias.ConsumedRewardRuleIds = new[] { "legacy-first-clear" };
            var skipped = RewardEvaluator.Evaluate(rewardDefinition, evaluationResult, consumedByAlias);
            Assert.That(skipped.AnyGranted, Is.False);

            var preUpdate = PlayerStageProgress.CreateEmpty(stageId);
            var granted = RewardEvaluator.Evaluate(rewardDefinition, evaluationResult, preUpdate);
            Assert.That(granted.AnyGranted, Is.True);
            Assert.That(granted.WasFirstClear, Is.True);
            Assert.That(granted.GrantedRewards[0].Reward.RewardId, Is.EqualTo("coin"));
        }

        [Test]
        public void StageCompletionCommitter_CommitsAtomically_AndIsIdempotentByStageRun()
        {
            var stageId = StageId.CreateOrThrow("commit-stage");
            var clearResult = new StageClearResult(
                stageId,
                new StageRunId("run-1"),
                StageTerminalReason.Cleared,
                wasCleared: true,
                finalTickIndex: 12,
                default,
                Array.Empty<StageSessionMetricValue>(),
                Array.Empty<StageChallengeRuntimeState>());
            var evaluationResult = new StageClearEvaluationResult(
                stageId,
                clearResult.StageRunId,
                wasCleared: true,
                score: 500,
                starsEarned: 3,
                rankId: "s",
                challengeResults: Array.Empty<StageChallengeEvaluationResult>());
            var rewardResult = new RewardGrantResult(
                stageId,
                clearResult.StageRunId,
                new[]
                {
                    new RewardGrantEntry(
                        "first-clear",
                        new RewardEntry
                        {
                            RewardId = "coin",
                            Amount = 100,
                        },
                        new RewardGrantId($"{stageId.Value}:first-clear")),
                },
                new[] { "first-clear" },
                new[] { new RewardGrantId($"{stageId.Value}:first-clear") },
                wasFirstClear: true);
            var transaction = StageCompletionTransactionBuilder.Build(
                clearResult,
                evaluationResult,
                rewardResult,
                PlayerStageProgress.CreateEmpty(stageId));

            var store = new InMemoryStageCompletionProfileStore();
            var committer = new StageCompletionCommitter(store);

            var firstCommit = committer.Commit(transaction);
            var secondCommit = committer.Commit(transaction);

            Assert.That(firstCommit.CommittedNow, Is.True);
            Assert.That(secondCommit.AlreadyCommitted, Is.True);
            Assert.That(store.Snapshot.InventoryBalances["coin"], Is.EqualTo(100));
            Assert.That(store.Snapshot.ProgressByStageId[stageId].ConsumedRewardRuleIds, Has.Member("first-clear"));
        }

        [Test]
        public void DebugForceClearResultOnly_DoesNotCommitProgressOrReward()
        {
            var entry = CreateEntry("debug-clear-stage");
            var store = new InMemoryStageCompletionProfileStore();
            var runtime = new GameplayHostStageCompletionRuntime(entry, store);

            var readModel = runtime.ForceClearResultOnly();

            Assert.That(readModel, Is.Not.Null);
            Assert.That(readModel.RewardGrantResult.AnyGranted, Is.False);
            Assert.That(readModel.UpdatedProgress.HasCleared, Is.False);
            Assert.That(store.Snapshot.Version, Is.Zero);
            Assert.That(store.Snapshot.ProgressByStageId, Is.Empty);
            Assert.That(store.Snapshot.InventoryBalances, Is.Empty);
        }

        [Test]
        public void DebugForceClearResultOnly_ProducesDebugMarkedStageResultPayload()
        {
            var entry = CreateEntry("debug-marked-stage");
            var runtime = new GameplayHostStageCompletionRuntime(entry, new InMemoryStageCompletionProfileStore());

            var readModel = runtime.ForceClearResultOnly();

            Assert.That(readModel.ResultTitle, Does.Contain("DEBUG FORCED CLEAR"));
            Assert.That(readModel.ResultDetailText, Does.Contain("DEBUG FORCED CLEAR"));
            Assert.That(readModel.ResultDetailText, Does.Contain("NO SAVE / NO REWARD"));
            Assert.That(readModel.RewardGrantResult.AnyGranted, Is.False);
        }

        [Test]
        public void DebugForceClearResultOnly_WhenCanGoNextStageFalse_DisablesContinue()
        {
            var hostObject = new GameObject("DebugForceClearUnavailableHost");
            var presenterObject = new GameObject("DebugForceClearUnavailablePresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("debug-force-current");
                var nextStage = StageId.CreateOrThrow("debug-force-next");
                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    new FixedDebugStageNavigationResolver(nextStage),
                    new RejectingDebugStageLaunchConstraint("Debug launch blocked by test constraint."));
                port.BindStageLaunchGateway(new LaunchContextRecordingGateway());

                var availability = port.GetAvailability();
                var result = port.ForceClearResultOnly();

                Assert.That(availability.NextStageId, Is.EqualTo(nextStage));
                Assert.That(availability.CanGoNextStage, Is.False);
                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Success));
                Assert.That(result.TargetStageId.IsValid, Is.False);
                Assert.That(result.StageNavigationRequest.IsValid, Is.False);
                Assert.That(result.StageResultReadModel, Is.Not.Null);
                Assert.That(result.StageResultReadModel.ResultContinueLabel, Is.EqualTo("Next stage unavailable"));
                Assert.That(result.StageResultReadModel.ResultDetailText, Does.Contain("Next stage unavailable"));
                Assert.That(result.StageResultReadModel.ResultDetailText, Does.Contain("Debug launch blocked by test constraint"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugForceClearResultOnly_WhenCampaignActiveSlotMismatch_DoesNotCreateNextStageRequest()
        {
            StageLaunchContextStore.Clear();
            var saveKey = $"DebugForceClearMismatchSave-{Guid.NewGuid():N}";
            var activeKey = $"DebugForceClearMismatchActive-{Guid.NewGuid():N}";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("DebugForceClearMismatchHost");
            var presenterObject = new GameObject("DebugForceClearMismatchPresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("stage-0-1");
                var nextStage = StageId.CreateOrThrow("stage-0-2");
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = currentStage,
                });
                activeSlotProvider.SetActiveSlot(1);

                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    new FixedDebugStageNavigationResolver(nextStage),
                    new CampaignActiveSlotDebugStageLaunchConstraint(saveStore, activeSlotProvider));
                port.BindStageLaunchGateway(new LaunchContextRecordingGateway());

                var availability = port.GetAvailability();
                var result = port.ForceClearResultOnly();

                Assert.That(availability.NextStageId, Is.EqualTo(nextStage));
                Assert.That(availability.CanGoNextStage, Is.False);
                Assert.That(availability.ReasonText, Does.Contain("campaign active slot stage"));
                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Success));
                Assert.That(result.StageNavigationRequest.IsValid, Is.False);
                Assert.That(result.StageResultReadModel.ResultContinueLabel, Is.EqualTo("Next stage unavailable"));
                Assert.That(result.StageResultReadModel.ResultDetailText, Does.Contain("campaign active slot stage"));
                Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
            }
            finally
            {
                StageLaunchContextStore.Clear();
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugForceClearResultOnly_WhenCanGoNextStageTrue_AllowsContinue()
        {
            var hostObject = new GameObject("DebugForceClearAllowedHost");
            var presenterObject = new GameObject("DebugForceClearAllowedPresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("debug-force-allowed-current");
                var nextStage = StageId.CreateOrThrow("debug-force-allowed-next");
                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    new FixedDebugStageNavigationResolver(nextStage));
                port.BindStageLaunchGateway(new LaunchContextRecordingGateway());

                var availability = port.GetAvailability();
                var result = port.ForceClearResultOnly();

                Assert.That(availability.CanGoNextStage, Is.True);
                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Success));
                Assert.That(result.TargetStageId, Is.EqualTo(nextStage));
                Assert.That(result.StageNavigationRequest.IsValid, Is.True);
                Assert.That(result.StageNavigationRequest.StageId, Is.EqualTo(nextStage));
                Assert.That(result.StageNavigationRequest.NavigationKind, Is.EqualTo(StageNavigationKind.NextStage));
                Assert.That(result.StageResultReadModel.ResultContinueLabel, Is.Not.EqualTo("Next stage unavailable"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugNextStage_RoutesThroughLaunchContextStore()
        {
            StageLaunchContextStore.Clear();
            var hostObject = new GameObject("DebugNextStageHost");
            var presenterObject = new GameObject("DebugNextStagePresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("debug-current-stage");
                var nextStage = StageId.CreateOrThrow("debug-next-stage");
                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    new FixedDebugStageNavigationResolver(nextStage));
                port.BindStageLaunchGateway(new LaunchContextRecordingGateway());

                var result = port.GoToNextStage();

                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Success));
                Assert.That(result.StageNavigationRequest.StageId, Is.EqualTo(nextStage));
                Assert.That(result.StageNavigationRequest.NavigationKind, Is.EqualTo(StageNavigationKind.NextStage));
                Assert.That(StageLaunchContextStore.TryGetCurrent(out var storedStage), Is.True);
                Assert.That(storedStage, Is.EqualTo(nextStage));
            }
            finally
            {
                StageLaunchContextStore.Clear();
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugNextStage_NoNextStageReturnsUnavailable()
        {
            var hostObject = new GameObject("DebugNoNextStageHost");
            var presenterObject = new GameObject("DebugNoNextStagePresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("debug-last-stage");
                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    MissingDebugStageNavigationResolver.Instance);

                var result = port.GoToNextStage();

                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Unavailable));
                Assert.That(result.Message, Does.Contain("No next stage"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugNextStage_WhenSceneTransitionInProgress_ReturnsUnavailable()
        {
            var hostObject = new GameObject("DebugTransitionBusyHost");
            var presenterObject = new GameObject("DebugTransitionBusyPresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("debug-transition-current");
                var nextStage = StageId.CreateOrThrow("debug-transition-next");
                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    new FixedDebugStageNavigationResolver(nextStage));
                port.BindStageLaunchGateway(new BusyDebugStageLaunchGateway());

                var availability = port.GetAvailability();
                var result = port.GoToNextStage();

                Assert.That(availability.CanGoNextStage, Is.False);
                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Unavailable));
                Assert.That(result.Message, Does.Contain("transition"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugNextStage_WhenCampaignActiveSlotMismatchWouldOccur_ReturnsUnavailable()
        {
            StageLaunchContextStore.Clear();
            var saveKey = $"DebugNextStageSave-{Guid.NewGuid():N}";
            var activeKey = $"DebugNextStageActive-{Guid.NewGuid():N}";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("DebugCampaignMismatchHost");
            var presenterObject = new GameObject("DebugCampaignMismatchPresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("stage-0-1");
                var nextStage = StageId.CreateOrThrow("stage-0-2");
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = currentStage,
                });
                activeSlotProvider.SetActiveSlot(1);

                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    new FixedDebugStageNavigationResolver(nextStage),
                    new CampaignActiveSlotDebugStageLaunchConstraint(saveStore, activeSlotProvider));
                port.BindStageLaunchGateway(new LaunchContextRecordingGateway());

                var availability = port.GetAvailability();
                var result = port.GoToNextStage();

                Assert.That(availability.CanGoNextStage, Is.False);
                Assert.That(availability.ReasonText, Does.Contain("campaign active slot stage"));
                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Unavailable));
                Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
            }
            finally
            {
                StageLaunchContextStore.Clear();
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugNextStage_RouterFailureDoesNotReturnSuccess()
        {
            var hostObject = new GameObject("DebugRouterFailureHost");
            var presenterObject = new GameObject("DebugRouterFailurePresenter");
            try
            {
                var currentStage = StageId.CreateOrThrow("debug-router-current");
                var nextStage = StageId.CreateOrThrow("debug-router-next");
                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                using var feed = new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    CreateEntry(currentStage.Value),
                    new InMemoryStageCompletionProfileStore());
                var port = new GameplayHostDebugStageCommandPort(
                    CreateEntry(currentStage.Value),
                    feed,
                    new FixedDebugStageNavigationResolver(nextStage));
                port.BindStageLaunchGateway(new FailingDebugStageLaunchGateway());

                var result = port.GoToNextStage();

                Assert.That(result.Status, Is.EqualTo(DebugCommandStatus.Unavailable));
                Assert.That(result.IsSuccess, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DebugCommandRuntime_ProductionDisabled()
        {
            Assert.That(DebugCommandBuildGate.IsRuntimeEnabled(isEditor: false, isDebugBuild: false), Is.False);

            var access = DebugCommandAccess.Disabled;
            var availability = access.StageCommandPort.GetAvailability();

            Assert.That(access.IsEnabled, Is.False);
            Assert.That(availability.CanOpenPanel, Is.False);
            Assert.That(availability.CanGoNextStage, Is.False);
            Assert.That(availability.CanForceClearResultOnly, Is.False);
            Assert.That(access.StageCommandPort.GoToNextStage().Status, Is.EqualTo(DebugCommandStatus.Unavailable));
            Assert.That(access.StageCommandPort.ForceClearResultOnly().Status, Is.EqualTo(DebugCommandStatus.Unavailable));
        }

        [Test]
        public void StageProgressionEvaluator_DerivesUnlock_FromSavedProgress()
        {
            var progressionDefinition = ScriptableObject.CreateInstance<StageProgressionDefinition>();
            SetPrivateField(progressionDefinition, "unlockedByDefault", false);
            SetPrivateField(
                progressionDefinition,
                "unlockRules",
                new[]
                {
                    new StageUnlockRuleDefinition
                    {
                        RequiredStageId = StageId.CreateOrThrow("tutorial-stage"),
                        MinimumStars = 2,
                    },
                });

            var progress = PlayerStageProgress.CreateEmpty(StageId.CreateOrThrow("tutorial-stage"));
            progress.HasCleared = true;
            progress.BestStars = 3;

            var progressByStageId = new Dictionary<StageId, PlayerStageProgress>
            {
                [progress.StageId] = progress,
            };

            Assert.That(StageProgressionEvaluator.IsUnlocked(progressionDefinition, progressByStageId), Is.True);

            progress.BestStars = 1;
            Assert.That(StageProgressionEvaluator.IsUnlocked(progressionDefinition, progressByStageId), Is.False);
        }

        private static StageContentEntry CreateEntry(
            string rawStageId,
            StagePresentationDefinition presentationDefinition = null,
            StageClearEvaluationDefinition clearEvaluationDefinition = null,
            StageRewardDefinition rewardDefinition = null,
            StageProgressionDefinition progressionDefinition = null)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var stageId = StageId.CreateOrThrow(rawStageId);
            entry.AssignStageId(stageId);
            entry.name = $"{stageId.Value}_Entry";
            entry.AssignGameplayDefinition(CreateMinimalStageDefinition(stageId.Value));

            presentationDefinition ??= ScriptableObject.CreateInstance<StagePresentationDefinition>();
            clearEvaluationDefinition ??= ScriptableObject.CreateInstance<StageClearEvaluationDefinition>();
            rewardDefinition ??= ScriptableObject.CreateInstance<StageRewardDefinition>();
            progressionDefinition ??= ScriptableObject.CreateInstance<StageProgressionDefinition>();

            presentationDefinition.SetOwnerMetadata(entry, string.Empty);
            clearEvaluationDefinition.SetOwnerMetadata(entry, string.Empty);
            rewardDefinition.SetOwnerMetadata(entry, string.Empty);
            progressionDefinition.SetOwnerMetadata(entry, string.Empty);

            entry.AssignPresentationDefinition(presentationDefinition);
            entry.AssignClearEvaluationDefinition(clearEvaluationDefinition);
            entry.AssignRewardDefinition(rewardDefinition);
            entry.AssignProgressionDefinition(progressionDefinition);
            return entry;
        }

        private static StageDefinition CreateMinimalStageDefinition(string stageName)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.name = stageName;
            SetPrivateField(
                stage,
                "board",
                new StageBoardDefinition
                {
                    MinInclusive = Vector2Int.zero,
                    MaxInclusive = new Vector2Int(2, 2),
                    InitialBottomFace = FaceId.Floor,
                });
            SetPrivateField(
                stage,
                "playerSpawns",
                new[]
                {
                    new StageSpawnDefinition
                    {
                        EntityId = 1,
                        Kind = StageSpawnKind.Player,
                        Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                        Facing = Direction.Right,
                        Hp = 3,
                    },
                });
            SetPrivateField(stage, "boxSpawns", Array.Empty<StageSpawnDefinition>());
            SetPrivateField(stage, "enemySpawns", Array.Empty<StageSpawnDefinition>());
            SetPrivateField(stage, "wallSpawns", Array.Empty<StageSpawnDefinition>());
            return stage;
        }

        private static StageContentEntry CreateEntryWithTileFeatureVisualBinding(
            int gameplayTileId,
            int bindingTileId,
            GameObject visualPrefab)
        {
            return CreateEntryWithTileFeatureVisualBinding(
                gameplayTileId,
                new[]
                {
                    new TileFeaturePresentationBinding
                    {
                        TileId = bindingTileId,
                        VisualPrefab = visualPrefab,
                    },
                });
        }

        private static StageContentEntry CreateEntryWithTileFeatureVisualBinding(
            int gameplayTileId,
            TileFeaturePresentationBinding[] bindings)
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentation, "tileFeaturePresentationBindings", bindings);
            var entry = CreateEntry("stage-a", presentationDefinition: presentation);
            SetPrivateField(
                entry.GameplayDefinition,
                "tileFeatures",
                new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = gameplayTileId,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                        Kind = TileFeatureKind.Button,
                        ActivationRule = TileFeatureActivationRule.ActiveFaceOnly,
                    },
                });
            return entry;
        }

        private static GameObject CreateTileFeatureVisualPrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<TileFeatureVisualTargetView>();
            return prefab;
        }

        private static StageValidationReport ValidateSingleEntry(StageContentEntry entry)
        {
            return new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                });
        }

        private static PlayerAtAnyZoneConditionAsset CreatePlayerAtExitCondition()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            SetPrivateField(condition, "zoneIds", new[] { "exit" });
            SetPrivateField(condition, "requireAlive", true);
            return condition;
        }

        private static void AssignExitObjective(
            StageDefinition stage,
            StageConditionAsset condition,
            string displayText,
            bool duplicateStableId = false)
        {
            SetPrivateField(
                stage,
                "zones",
                new[]
                {
                    new StageZoneDefinition
                    {
                        ZoneId = "exit",
                        FaceId = FaceId.Floor,
                        Regions = new[]
                        {
                            new StageZoneRegionDefinition
                            {
                                MinInclusive = new Vector2Int(1, 1),
                                MaxInclusive = new Vector2Int(1, 1),
                            },
                        },
                    },
                });

            var entries = duplicateStableId
                ? new[]
                {
                    CreateObjectiveConditionEntry(condition, displayText),
                    CreateObjectiveConditionEntry(condition, displayText),
                }
                : new[]
                {
                    CreateObjectiveConditionEntry(condition, displayText),
                };
            SetPrivateField(
                stage,
                "objective",
                new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ObjectiveTitle = "Reach the Exit",
                    ObjectiveSummary = "Move to the exit zone.",
                    ConditionEntries = entries,
                });
        }

        private static StageObjectiveConditionEntry CreateObjectiveConditionEntry(
            StageConditionAsset condition,
            string displayText)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = true,
                Role = StageObjectiveConditionRole.PrimaryGoal,
                StableConditionId = "primary-goal",
                DisplayText = displayText,
                SortOrder = 0,
            };
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static ScriptableObjectStageCatalogProvider CreateCatalogProvider(
            IReadOnlyList<StageContentEntry> entries,
            StageIdAliasTable aliasTable)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            catalog.SetEntries(entries as StageContentEntry[] ?? new List<StageContentEntry>(entries).ToArray());
            catalog.AssignStageIdAliasTable(aliasTable);

            var provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            provider.AssignCatalog(catalog);
            return provider;
        }

        private sealed class TestStageCatalogProvider : IStageCatalogProvider
        {
            private readonly IReadOnlyList<StageContentEntry> entries;

            public TestStageCatalogProvider(IReadOnlyList<StageContentEntry> entries, StageIdAliasTable aliasTable)
            {
                this.entries = entries;
                AliasTable = aliasTable;
            }

            public IReadOnlyList<StageContentEntry> LoadEntries()
            {
                return entries;
            }

            public StageIdAliasTable AliasTable { get; }
        }

        private sealed class InMemoryStageCompletionProfileStore : IStageCompletionProfileStore
        {
            public StageCompletionProfileSnapshot Snapshot { get; private set; } = new();

            public StageCompletionProfileSnapshot Load()
            {
                return Snapshot.Clone();
            }

            public void Save(StageCompletionProfileSnapshot snapshot)
            {
                Snapshot = snapshot.Clone();
            }
        }

        private sealed class FixedDebugStageNavigationResolver : IDebugStageNavigationResolver
        {
            private readonly StageId nextStageId;

            public FixedDebugStageNavigationResolver(StageId nextStageId)
            {
                this.nextStageId = nextStageId;
            }

            public bool TryResolveNextStage(StageId currentStageId, out StageId nextStageId)
            {
                nextStageId = this.nextStageId;
                return true;
            }
        }

        private sealed class MissingDebugStageNavigationResolver : IDebugStageNavigationResolver
        {
            public static MissingDebugStageNavigationResolver Instance { get; } = new();

            public bool TryResolveNextStage(StageId currentStageId, out StageId nextStageId)
            {
                nextStageId = StageId.None;
                return false;
            }
        }

        private sealed class RejectingDebugStageLaunchConstraint : IDebugStageLaunchConstraint
        {
            private readonly string reasonText;

            public RejectingDebugStageLaunchConstraint(string reasonText)
            {
                this.reasonText = reasonText;
            }

            public bool CanLaunch(StageId targetStageId, out string reasonText)
            {
                reasonText = this.reasonText;
                return false;
            }
        }

        private sealed class LaunchContextRecordingGateway : IDebugStageLaunchGateway
        {
            public bool IsLaunchInProgress => false;

            public DebugCommandResult TryLaunch(StageNavigationRequest request)
            {
                StageLaunchContextStore.SetCurrent(request.StageId);
                return DebugCommandResult.Success("Recorded launch context.", request.StageId, request);
            }
        }

        private sealed class BusyDebugStageLaunchGateway : IDebugStageLaunchGateway
        {
            public bool IsLaunchInProgress => true;

            public DebugCommandResult TryLaunch(StageNavigationRequest request)
            {
                return DebugCommandResult.Unavailable("Scene transition is already in progress.");
            }
        }

        private sealed class FailingDebugStageLaunchGateway : IDebugStageLaunchGateway
        {
            public bool IsLaunchInProgress => false;

            public DebugCommandResult TryLaunch(StageNavigationRequest request)
            {
                return DebugCommandResult.Unavailable("Scene transition could not be started.");
            }
        }
    }
}
