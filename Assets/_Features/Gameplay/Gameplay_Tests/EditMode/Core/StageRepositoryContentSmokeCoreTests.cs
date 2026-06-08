using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class StageRepositoryContentSmokeCoreTests
    {
        private const int FirstTickSmokeCount = 5;

        [Test]
        [Category("Core")]
        public void StageContentEntry_AllCompanions_ResolveAndValidate()
        {
            var catalog = LoadCatalog();
            var repositoryEntries = LoadAllRepositoryEntries();
            Assert.That(repositoryEntries, Is.Not.Empty, "Repository scan found no StageContentEntry assets.");

            var catalogStageIds = new HashSet<StageId>(catalog.Entries.Select(entry => entry.StageId));
            var missingFromCatalog = repositoryEntries
                .Where(entry => entry.StageId.IsValid && !catalogStageIds.Contains(entry.StageId))
                .Select(DescribeEntry)
                .ToArray();
            Assert.That(missingFromCatalog, Is.Empty, "StageContentEntry assets missing from canonical catalog:\n" + string.Join("\n", missingFromCatalog));

            var report = new StageCatalogValidator().Validate(catalog, CreateCatalogValidationOptions());
            Assert.That(
                report.Issues.Where(issue => issue.Severity == StageValidationSeverity.Error).Select(FormatIssue).ToArray(),
                Is.Empty,
                "Stage catalog validation errors:\n" + string.Join("\n", report.Issues.Select(FormatIssue)));
        }

        [Test]
        [Category("Core")]
        public void StageCatalog_AllEntries_ResolveBuildPresentationAndEnemyProfiles()
        {
            var failures = new List<string>();
            foreach (var entry in LoadCatalogEntries())
            {
                try
                {
                    var build = StageRuntimeBuilder.Build(entry.GameplayDefinition);
                    StagePresentationAssembler.Resolve(entry.GameplayDefinition, entry.PresentationDefinition);
                    StageAudioAssembler.Resolve(entry.AudioDefinition);
                    CompileEnemyRuntime(entry, build);
                }
                catch (Exception exception)
                {
                    failures.Add($"{DescribeEntry(entry)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Stage runtime build/presentation/profile smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void StageRuntime_AllCatalogEntries_FirstFiveTicks_NoRuntimeGuardExceptionAndStableHash()
        {
            var failures = new List<string>();
            foreach (var entry in LoadCatalogEntries())
            {
                try
                {
                    var firstRunHashes = RunFirstTicks(entry);
                    var secondRunHashes = RunFirstTicks(entry);
                    CollectionAssert.AreEqual(firstRunHashes, secondRunHashes, DescribeEntry(entry));
                }
                catch (Exception exception)
                {
                    failures.Add($"{DescribeEntry(entry)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Stage first-five-ticks smoke failures:\n" + string.Join("\n", failures));
        }

        private static IReadOnlyList<string> RunFirstTicks(StageContentEntry entry)
        {
            var build = StageRuntimeBuilder.Build(entry.GameplayDefinition);
            var worldState = GameplayCompositionRoot.CreateWorldState(
                build.InitialEntities,
                build.BoardBounds,
                build.InitialTerrain,
                build.InitialTopology,
                build.InitialTileFeatures);
            var configuration = CreateHostConfiguration(entry, build);
            var enemyRuntime = configuration.CreateEnemyAiRuntimeSnapshot();
            var provider = GameplayEntityLogicProviderFactory.CreateDefault(
                enemyRuntime.DefaultDefinition,
                enemyRuntime.DefinitionsByEntityId,
                enemyRuntime.DefinitionsByArchetypeId,
                enemyRuntime.HasDefaultDefinition);
            var bootstrapper = new GameplayBootstrapper(provider, enemyRuntime.SpawnDefaultsByArchetypeId);
            var respawnTiming = configuration.CreatePlayerRespawnTimingSnapshot();
            var pipeline = bootstrapper.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                configuration.CreateTimingProfile(),
                configuration.CreatePlayerControlTimingSnapshot(),
                respawnTiming.RespawnDelayTicks,
                build.ObjectiveRuntimeDefinition,
                allowPlayerRespawn: true,
                configuration.CreateRuntimeFeatureFlags(),
                configuration.CreatePlayerKinematicLocomotionTimingSnapshot(),
                configuration.CreatePlayerContinuousLocomotionSnapshot(),
                build.TileFeatureDefinitions,
                build.MoonBlockRespawnDefinitions);

            var hashes = new List<string>(FirstTickSmokeCount);
            for (var tickIndex = 1; tickIndex <= FirstTickSmokeCount; tickIndex++)
            {
                var result = pipeline.RunTick(new TickInput(tickIndex));
                hashes.Add(result.DeterminismHash);
            }

            return hashes;
        }

        private static void CompileEnemyRuntime(StageContentEntry entry, StageRuntimeBuildResult build)
        {
            var configuration = CreateHostConfiguration(entry, build);
            var enemyRuntime = configuration.CreateEnemyAiRuntimeSnapshot();
            foreach (var definition in EnumerateDefinitions(enemyRuntime))
            {
                definition.Validate(nameof(definition));
            }
        }

        private static IEnumerable<EnemyAiRuntimeDefinition> EnumerateDefinitions(
            EnemyAiRuntimeCollectionSnapshot enemyRuntime)
        {
            if (enemyRuntime.HasDefaultDefinition)
            {
                yield return enemyRuntime.DefaultDefinition;
            }

            if (enemyRuntime.DefinitionsByEntityId != null)
            {
                foreach (var key in enemyRuntime.DefinitionsByEntityId.Keys.OrderBy(key => key))
                {
                    yield return enemyRuntime.DefinitionsByEntityId[key];
                }
            }

            if (enemyRuntime.DefinitionsByArchetypeId != null)
            {
                var keys = enemyRuntime.DefinitionsByArchetypeId.Keys.ToList();
                keys.Sort(EnemyUnitArchetypeId.OrderingComparer);
                foreach (var key in keys)
                {
                    yield return enemyRuntime.DefinitionsByArchetypeId[key];
                }
            }
        }

        private static GameplaySceneHostConfiguration CreateHostConfiguration(
            StageContentEntry entry,
            StageRuntimeBuildResult build)
        {
            return new GameplaySceneHostConfiguration
            {
                EnemyAiProfileOverrides = build.EnemyAiProfileOverrides,
                EnemyUnitArchetypeCatalog = entry.GameplayDefinition.EnemyUnitArchetypeCatalog,
            };
        }

        private static StageCatalogValidationOptions CreateCatalogValidationOptions()
        {
            return new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireAudioDefinition = true,
                RequireClearEvaluationDefinition = true,
                RequireRewardDefinition = true,
                RequireProgressionDefinition = true,
                Timing = StageValidationTiming.TestOrCi,
                Phase = StageValidationPhase.Phase6_SunsetFinalization,
                AssetMetadataProvider = new AssetDatabaseMetadataProvider(),
            };
        }

        private static IReadOnlyList<StageContentEntry> LoadCatalogEntries()
        {
            var entries = LoadCatalog().Entries;
            Assert.That(entries, Is.Not.Empty, $"{StageContentPaths.StageCatalogAssetPath} contains no entries.");
            Assert.That(entries.Any(entry => entry == null), Is.False, $"{StageContentPaths.StageCatalogAssetPath} contains null entries.");
            return entries.OrderBy(entry => entry.StageId.Value, StringComparer.Ordinal).ToArray();
        }

        private static StageCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            Assert.That(catalog, Is.Not.Null, $"Missing StageCatalog at '{StageContentPaths.StageCatalogAssetPath}'.");
            return catalog;
        }

        private static IReadOnlyList<StageContentEntry> LoadAllRepositoryEntries()
        {
            var entries = new List<StageContentEntry>();
            var guids = AssetDatabase.FindAssets("t:StageContentEntry");
            Array.Sort(guids, StringComparer.Ordinal);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var entry = AssetDatabase.LoadAssetAtPath<StageContentEntry>(path);
                Assert.That(entry, Is.Not.Null, $"Failed to load StageContentEntry at '{path}'.");
                entries.Add(entry);
            }

            return entries;
        }

        private static string DescribeEntry(StageContentEntry entry)
        {
            if (entry == null)
            {
                return "<null StageContentEntry>";
            }

            return $"StageId='{entry.StageId.Value}' Path='{AssetDatabase.GetAssetPath(entry)}'";
        }

        private static string FormatIssue(StageValidationIssue issue)
        {
            return $"[{issue.Severity}] {issue.Code}: StageId='{issue.StageId}' EntityId='{issue.EntityId}' Expected='{issue.ExpectedValue}' Actual='{issue.ActualValue}' Path='{issue.AssetPath}' Message='{issue.Message}'";
        }

        private sealed class AssetDatabaseMetadataProvider : IStageValidationAssetMetadataProvider
        {
            public string GetAssetPath(UnityEngine.Object asset)
            {
                return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            }

            public string GetAssetGuid(UnityEngine.Object asset)
            {
                var path = GetAssetPath(asset);
                return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            }
        }
    }
}
