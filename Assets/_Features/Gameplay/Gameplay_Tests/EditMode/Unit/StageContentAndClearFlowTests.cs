using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
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
        public void StageRuntimeContentResolver_CatalogMode_PrefersLaunchContextOverDefaultStageId()
        {
            StageLaunchContextStore.Clear();
            try
            {
                var combinedEntry = CreateEntry("combined-gameplay-showcase");
                var tutorialEntry = CreateEntry("tutorial-scene");
                var provider = CreateCatalogProvider(new[] { combinedEntry, tutorialEntry }, aliasTable: null);
                var resolver = new StageRuntimeContentResolver();
                StageLaunchContextStore.SetCurrent(tutorialEntry.StageId);

                var resolved = resolver.Resolve(
                    new StageLoadRequest(
                        StageLoadSourceMode.CatalogResolvedStageId,
                        provider,
                        combinedEntry.StageId,
                        serializedStageContentEntry: null,
                        legacyStageDefinition: null,
                        legacyEnemyPresentationCatalog: null,
                        legacyStaticEntityPresentationCatalog: null,
                        sceneName: "StageRuntimeContentResolverTests",
                        allowDefaultStageIdFallback: true));

                Assert.That(resolved.Entry, Is.SameAs(tutorialEntry));
                Assert.That(resolved.UsedLaunchContext, Is.True);
                Assert.That(resolved.UsedDefaultStageIdFallback, Is.False);
            }
            finally
            {
                StageLaunchContextStore.Clear();
            }
        }

        [Test]
        public void StageRuntimeContentResolver_CatalogMode_DefaultStageIdFallbackRequiresExplicitOptIn()
        {
            StageLaunchContextStore.Clear();
            var entry = CreateEntry("tutorial-scene");
            var provider = CreateCatalogProvider(new[] { entry }, aliasTable: null);
            var resolver = new StageRuntimeContentResolver();

            var exception = Assert.Throws<InvalidOperationException>(
                () => resolver.Resolve(
                    new StageLoadRequest(
                        StageLoadSourceMode.CatalogResolvedStageId,
                        provider,
                        entry.StageId,
                        serializedStageContentEntry: null,
                        legacyStageDefinition: null,
                        legacyEnemyPresentationCatalog: null,
                        legacyStaticEntityPresentationCatalog: null,
                        sceneName: "StageRuntimeContentResolverTests",
                        allowDefaultStageIdFallback: false)));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("defaultStageId fallback is not permitted", exception.Message);
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
    }
}
