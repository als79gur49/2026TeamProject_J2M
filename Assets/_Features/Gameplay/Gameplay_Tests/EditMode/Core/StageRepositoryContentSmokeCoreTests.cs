using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
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

        [Test]
        [Category("Core")]
        public void StageRuntime_Stage32_M1CanonicalOutput_IsByteStableAcrossRepeatedRuns()
        {
            var entry = LoadCatalogEntries().Single(candidate => candidate.StageId.Value == "stage-3-2");
            var first = CaptureStage32CanonicalOutput(entry);
            var second = CaptureStage32CanonicalOutput(entry);

            Assert.That(second, Is.EqualTo(first));
            UnityEngine.Debug.Log("M1_STAGE_CANONICAL_BEGIN\n" + first + "\nM1_STAGE_CANONICAL_END");
        }

        private static string CaptureStage32CanonicalOutput(StageContentEntry entry)
        {
            var build = StageRuntimeBuilder.Build(entry.GameplayDefinition);
            var worldState = GameplayCompositionRoot.CreateWorldState(
                build.InitialEntities,
                build.BoardBounds,
                build.InitialTopology,
                build.InitialTileFeatures);
            var initialSnapshot = worldState.CreateSnapshot();
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
                configuration.CreateUnitKinematicLocomotionTimingSnapshot(),
                configuration.CreatePlayerContinuousLocomotionSnapshot(),
                build.TileFeatureDefinitions,
                build.MoonBlockRespawnDefinitions);

            TickResult firstTick;
            TickResult secondTick;
            WorldSnapshot firstSnapshot;
            WorldSnapshot secondSnapshot;
            IReadOnlyList<DelayedAttackEffectRecord> firstPendingEffects;
            IReadOnlyList<DelayedAttackEffectRecord> secondPendingEffects;
            GameplayTickWorkloadCounts counts;
            using (var scope = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                firstTick = pipeline.RunTick(new TickInput(1));
                firstSnapshot = worldState.CreateSnapshot();
                firstPendingEffects = CapturePendingDelayedAttackEffects(pipeline);
                secondTick = pipeline.RunTick(new TickInput(2));
                secondSnapshot = worldState.CreateSnapshot();
                secondPendingEffects = CapturePendingDelayedAttackEffects(pipeline);
                counts = scope.Counts;
            }

            var initialData = new TickResultData(
                build.InitialEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>());
            var firstData = CreateTickResultData(firstTick, firstPendingEffects);
            var secondData = CreateTickResultData(secondTick, secondPendingEffects);
            Assert.That(new DeterminismHashBuilder().Build(1, firstSnapshot, firstData), Is.EqualTo(firstTick.DeterminismHash));
            Assert.That(new DeterminismHashBuilder().Build(2, secondSnapshot, secondData), Is.EqualTo(secondTick.DeterminismHash));

            return string.Join("\n", new[]
            {
                "SCHEMA=M1_STAGE_CANONICAL_V2",
                "STAGE_ID=" + entry.StageId.Value,
                "STAGE_ENTRY_PATH=" + AssetDatabase.GetAssetPath(entry),
                "INITIAL_ENTITIES=" + SerializeEntities(build.InitialEntities),
                "INITIAL_SNAPSHOT=" + SerializeFullCanonicalCarrier(0, initialSnapshot, initialData),
                "TICK1_RESULT_DATA=" + SerializeTickResultData(firstTick, firstSnapshot, firstData),
                "TICK1_EVENT_LOG=" + SerializeStrings(firstTick.EventLog),
                "TICK1_MOVEMENT_REJECTIONS=" + SerializeStrings(firstTick.MovementPhaseResult.RejectedReasons),
                "TICK1_ATTACK_REJECTIONS=" + SerializeStrings(firstTick.AttackPhaseResult.RejectedReasons),
                "TICK1_PHASE_TRACE=" + SerializeStrings(firstTick.PhaseTrace),
                "TICK1_TRACE=" + Escape(firstTick.Trace.Text),
                "TICK1_HASH=" + firstTick.DeterminismHash,
                "TICK1_SNAPSHOT=" + SerializeFullCanonicalCarrier(1, firstSnapshot, firstData),
                "TICK2_RESULT_DATA=" + SerializeTickResultData(secondTick, secondSnapshot, secondData),
                "TICK2_EVENT_LOG=" + SerializeStrings(secondTick.EventLog),
                "TICK2_MOVEMENT_REJECTIONS=" + SerializeStrings(secondTick.MovementPhaseResult.RejectedReasons),
                "TICK2_ATTACK_REJECTIONS=" + SerializeStrings(secondTick.AttackPhaseResult.RejectedReasons),
                "TICK2_PHASE_TRACE=" + SerializeStrings(secondTick.PhaseTrace),
                "TICK2_TRACE=" + Escape(secondTick.Trace.Text),
                "TICK2_HASH=" + secondTick.DeterminismHash,
                "TICK2_SNAPSHOT=" + SerializeFullCanonicalCarrier(2, secondSnapshot, secondData),
                "DIAGNOSTICS=" + SerializeDiagnostics(counts),
            });
        }

        private static TickResultData CreateTickResultData(
            TickResult result,
            IReadOnlyList<DelayedAttackEffectRecord> pendingEffects)
        {
            return new TickResultData(
                result.FinalEntities,
                pendingEffects,
                result.EventLog,
                result.PresentationData,
                result.ObjectiveResult);
        }

        private static string SerializeTickResultData(
            TickResult result,
            WorldSnapshot snapshot,
            TickResultData data)
        {
            return "FullCanonical=" + SerializeFullCanonicalCarrier(result.TickIndex, snapshot, data) +
                   ",Phases=" + SerializeStrings(result.CompletedPhases.Select(value => ((int)value).ToString()).ToArray()) +
                   ",FinalTopology=" + Escape(result.FinalTopology.ToString()) +
                   ",Presentation=" + Escape(SerializePublicCarrier(result.PresentationData));
        }

        private static string SerializeFullCanonicalCarrier(
            int tickIndex,
            WorldSnapshot snapshot,
            TickResultData data)
        {
            var method = typeof(DeterminismHashBuilder).GetMethod(
                "BuildCanonicalDump",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return Escape((string)method.Invoke(null, new object[] { tickIndex, snapshot, data }));
        }

        private static IReadOnlyList<DelayedAttackEffectRecord> CapturePendingDelayedAttackEffects(
            TickPipeline pipeline)
        {
            var field = typeof(TickPipeline).GetField(
                "_delayedAttackEffectQueue",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            var queue = field.GetValue(pipeline);
            var snapshot = queue.GetType().GetMethod(
                "Snapshot",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(snapshot, Is.Not.Null);
            return (IReadOnlyList<DelayedAttackEffectRecord>)snapshot.Invoke(queue, null);
        }

        private static string SerializeEntities(IEnumerable<EntityState> entities)
        {
            return string.Join(";", entities.OrderBy(entity => entity.entityId).Select(SerializeEntity));
        }

        private static string SerializeEntity(EntityState entity)
        {
            return string.Join(",", new[]
            {
                entity.entityId.ToString(), ((int)entity.type).ToString(), ((int)entity.position.face).ToString(),
                entity.position.x.ToString(), entity.position.y.ToString(), entity.hp.ToString(), entity.maxHp.ToString(),
                entity.teamId.ToString(), ((int)entity.unitRole).ToString(), ((int)entity.unitMobilityKind).ToString(),
                ((int)entity.state).ToString(), entity.stateTimer.ToString(), ((int)entity.facing).ToString(),
                ((int)entity.boardPresence).ToString(), entity.markedForDeath ? "1" : "0", entity.spawnTick.ToString(),
                ((int)entity.boxCapabilities).ToString(), ((int)entity.boxArchetype).ToString(),
                ((int)entity.gravityFieldPhase).ToString(), entity.gravityFieldTimerTicks.ToString(),
                entity.kineticInstigatorEntityId.ToString(), entity.kineticInstigatorTeamId.ToString(),
                ((int)entity.aiMode).ToString(), entity.aiStateTimer.ToString(),
                entity.enemyLocomotionCooldownTicks.ToString(), entity.enemyAttackCooldownTicks.ToString(),
                entity.enemyAttackCooldownTotalTicks.ToString(),
            });
        }

        private static string SerializeDiagnostics(GameplayTickWorkloadCounts counts)
        {
            var metrics = counts.EntityLogicBuildMetrics;
            var wallProperty = typeof(EntityLogicBuildMetrics).GetProperty("Wall");
            var wall = wallProperty == null ? default : (EntityLogicTypeMetrics)wallProperty.GetValue(metrics);
            return string.Join(",", new[]
            {
                counts.EntityLogicProviderBuildCount.ToString(), metrics.EntityVisitedCount.ToString(),
                metrics.RegisteredFactoryCount.ToString(), metrics.FactoryOpportunityCount.ToString(),
                metrics.PrefilterSkipCount.ToString(), metrics.CanCreateProbeCount.ToString(),
                metrics.CreatedLogicCount.ToString(), metrics.AcceptedLogicCount.ToString(),
                metrics.ConflictRejectedLogicCount.ToString(), SerializeTypeMetrics(metrics.None),
                SerializeTypeMetrics(metrics.Unit), SerializeTypeMetrics(metrics.Box), SerializeTypeMetrics(wall),
                SerializeTypeMetrics(metrics.Unknown),
            });
        }

        private static string SerializeTypeMetrics(EntityLogicTypeMetrics metrics)
        {
            return string.Join("/", new[]
            {
                metrics.EntityVisitedCount.ToString(), metrics.FactoryOpportunityCount.ToString(),
                metrics.PrefilterSkipCount.ToString(), metrics.CanCreateProbeCount.ToString(),
                metrics.CreatedLogicCount.ToString(), metrics.AcceptedLogicCount.ToString(),
                metrics.ConflictRejectedLogicCount.ToString(),
            });
        }

        private static string SerializeStrings(IEnumerable<string> values)
        {
            return string.Join(";", values.Select(Escape));
        }

        private static string SerializePublicCarrier(object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            if (value is string text)
            {
                return Escape(text);
            }

            var type = value.GetType();
            if (type.IsEnum)
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            }

            if (type.IsPrimitive || value is decimal)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is IEnumerable sequence)
            {
                var items = new List<string>();
                foreach (var item in sequence)
                {
                    items.Add(SerializePublicCarrier(item));
                }

                return "[" + string.Join(",", items) + "]";
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();
            return type.FullName + "{" + string.Join(",", properties.Select(
                property => property.Name + "=" + SerializePublicCarrier(property.GetValue(value)))) + "}";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace(";", "\\;");
        }

        private static IReadOnlyList<string> RunFirstTicks(StageContentEntry entry)
        {
            var build = StageRuntimeBuilder.Build(entry.GameplayDefinition);
            var worldState = GameplayCompositionRoot.CreateWorldState(
                build.InitialEntities,
                build.BoardBounds,
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
                configuration.CreateUnitKinematicLocomotionTimingSnapshot(),
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
