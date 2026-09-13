using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayStaticWallCommittedTargetCacheTests
    {
        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_PostCache_DeterministicMatrixHasExactAccounting()
        {
            var samples = CaptureDeterministicMatrix();

            Assert.That(samples, Has.Count.GreaterThanOrEqualTo(12));
            foreach (var sample in samples)
            {
                AssertExactAccounting(sample.Frame, sample.Label);
            }

            var steadyFirst = samples.Single(sample => sample.Label == "steady/initial").Frame;
            var steadySecond = samples.Single(sample => sample.Label == "steady/repeated").Frame;
            Assert.That(steadyFirst.ProvenanceCount, Is.GreaterThan(1));
            Assert.That(steadyFirst.EligibleCount, Is.GreaterThan(1));
            Assert.That(steadySecond.FallbackEntityScanCount, Is.EqualTo(steadyFirst.FallbackEntityScanCount));
            Assert.That(steadySecond.DynamicEntityProjectCount, Is.EqualTo(steadyFirst.DynamicEntityProjectCount));

            Assert.That(steadyFirst.RebuildCount, Is.EqualTo(steadyFirst.EligibleCount));
            Assert.That(steadyFirst.CacheCount, Is.EqualTo(steadyFirst.EligibleCount));
            Assert.That(steadySecond.HitCount, Is.EqualTo(steadySecond.EligibleCount));
            Assert.That(steadySecond.CacheCount, Is.EqualTo(steadySecond.EligibleCount));
            Assert.That(steadySecond.TryProjectEntityCellCount, Is.LessThan(steadyFirst.TryProjectEntityCellCount));
            Assert.That(steadySecond.CreateEntityPoseCount, Is.LessThan(steadyFirst.CreateEntityPoseCount));
            Assert.That(steadySecond.TryGetProjectedEntitySlotCount,
                Is.LessThan(steadyFirst.TryGetProjectedEntitySlotCount));
            Assert.That(steadySecond.ViewResolveCount, Is.EqualTo(steadyFirst.ViewResolveCount));
            Assert.That(steadySecond.InsertCount, Is.EqualTo(steadyFirst.InsertCount));
            Assert.That(steadySecond.OutputEntityIds, Is.EqualTo(steadyFirst.OutputEntityIds));

            var canonicalMatrix = BuildCanonicalMatrix(samples);
            var measurementMatrix = BuildMeasurementMatrix(samples);
            TestContext.WriteLine("PACKAGE4_SCHEMA_SHA256=" +
                                  GameplayCommittedFrameObservation.SelectionSchemaDigest);
            TestContext.WriteLine("PACKAGE4_CANONICAL_MATRIX_BASE64=" +
                                  Convert.ToBase64String(Encoding.UTF8.GetBytes(canonicalMatrix)));
            TestContext.WriteLine("PACKAGE4_MEASUREMENT_MATRIX_BASE64=" +
                                  Convert.ToBase64String(Encoding.UTF8.GetBytes(measurementMatrix)));
            TestContext.WriteLine("PACKAGE4_ENTITY_ROW_MATRIX_BASE64=" +
                                  Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildEntityRowMatrix(samples))));
            TestContext.WriteLine("PACKAGE4_COMMITTED_MAP_MATRIX_BASE64=" +
                                  Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildCommittedMapMatrix(samples))));
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_DirectCompositionHasEmptyUnforgeableProvenance()
        {
            using var fixture = CreateStageFixture();
            var direct = StageSceneCompositionAssembler.Compose(
                fixture.BuildResult,
                StagePresentationAssembler.EmptyResolvedData,
                StageAudioAssembler.EmptyResolvedData);
            using var harness = CreateHarness(StageStaticWallPresentationProvenance.Empty);
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();

            harness.Store(harness.Entities, tickIndex: 1);
            var frame = capture.Frames.Single();

            Assert.That(direct.StaticWallPresentationProvenance, Is.SameAs(StageStaticWallPresentationProvenance.Empty));
            Assert.That(frame.ProvenanceCount, Is.Zero);
            Assert.That(frame.EligibleCount, Is.Zero);
            Assert.That(frame.HitCount, Is.Zero);
            Assert.That(frame.MissCount, Is.Zero);
            Assert.That(frame.RebuildCount, Is.Zero);
            var wallIds = harness.Entities.Where(entity => entity.type == EntityType.Wall)
                .Select(entity => entity.entityId).ToHashSet();
            var wallRows = frame.EntityObservations.Where(row => wallIds.Contains(row.EntityId)).ToArray();
            Assert.That(wallRows, Is.Not.Empty);
            Assert.That(wallRows.All(row => !row.IsProvenanceCandidate &&
                                            row.SelectionKind == StaticWallTargetSelectionKind.FallbackWithoutCache &&
                                            row.DidRunProjection && row.DidRunPose && row.DidRunSlot && row.DidInsert), Is.True);
            Assert.That(typeof(StageStaticWallPresentationProvenance).IsAbstract, Is.True);
            Assert.That(typeof(StageStaticWallPresentationProvenance).GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(method => method.Name.StartsWith("Create", StringComparison.Ordinal)), Is.False);
            Assert.That(typeof(GameplaySceneHostConfiguration).GetFields()
                .Any(field => field.IsPublic && field.Name.Contains(
                    "StaticWallPresentationProvenance",
                    StringComparison.Ordinal)), Is.False);
            Assert.That(typeof(StageRuntimeBuildResult).GetProperties()
                .Any(property => property.Name.Contains("Provenance", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void StaticPresentationBindingWithoutStageProvenance_RemainsFallbackAndConfigurationDefaultsEmpty()
        {
            using var fixture = CreateStageFixture();
            var presentation = new StagePresentationResolvedData(
                displayNameKey: string.Empty,
                backgroundPrefab: null,
                enemyPresentationCatalog: null,
                enemyPresentationArchetypeCatalog: null,
                Array.Empty<EnemyPresentationBinding>(),
                staticEntityPresentationCatalog: null,
                new[]
                {
                    new StaticEntityPresentationBinding
                    {
                        EntityId = fixture.Composition.StaticWallPresentationProvenance.EntityIds[0],
                        PresentationId = "static-wall-binding-only",
                    },
                },
                boardPresentationProfile: null,
                boardTilePresentationCatalog: null,
                boardTileStyleCatalog: null,
                Array.Empty<BoardTilePaintOverride>(),
                tileFeaturePresentationCatalog: null,
                Array.Empty<TileFeaturePresentationResolvedBinding>(),
                worldGuideCatalog: null,
                Array.Empty<StageWorldGuideInstructionResolved>());
            var direct = StageSceneCompositionAssembler.Compose(
                fixture.BuildResult,
                presentation,
                StageAudioAssembler.EmptyResolvedData);
            var configuration = new GameplaySceneHostConfiguration();
            using var harness = CreateHarness(StageStaticWallPresentationProvenance.Empty);
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();

            harness.Store(harness.Entities, tickIndex: 1);
            var wallRows = capture.Frames.Single().EntityObservations.Where(row =>
                harness.Entities.Any(entity => entity.entityId == row.EntityId && entity.type == EntityType.Wall));

            Assert.That(direct.PresentationData.StaticEntityPresentationBindings, Has.Length.EqualTo(1));
            Assert.That(direct.StaticWallPresentationProvenance,
                Is.SameAs(StageStaticWallPresentationProvenance.Empty));
            Assert.That(configuration.StaticWallPresentationProvenance,
                Is.SameAs(StageStaticWallPresentationProvenance.Empty));
            Assert.That(wallRows.All(row => !row.IsProvenanceCandidate &&
                                            row.SelectionKind == StaticWallTargetSelectionKind.FallbackWithoutCache &&
                                            row.DidInsert), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void StageBackedWallProvenance_EmitsSortedOpaqueFullSignatures()
        {
            using var fixture = CreateStageFixture();
            var composition = fixture.Composition;
            var provenance = composition.StaticWallPresentationProvenance;
            var ids = provenance.EntityIds.ToArray();

            Assert.That(ids, Is.Not.Empty);
            Assert.That(ids, Is.Ordered.Ascending);
            Assert.That(ids, Is.Unique);
            Assert.That(provenance.StaticRevision, Has.Length.EqualTo(64));
            foreach (var entityId in ids)
            {
                Assert.That(provenance.TryGetEntry(entityId, out var entry), Is.True);
                Assert.That(entry.EntityId, Is.EqualTo(entityId));
                Assert.That(entry.SourceKind,
                    Is.EqualTo(StageStaticWallProvenanceSourceKind.StageAuthoredStaticWall));
                Assert.That(entry.InitialWall.entityId, Is.EqualTo(entityId));
                Assert.That(entry.InitialWall.type, Is.EqualTo(EntityType.Wall));
                Assert.That(entry.Signature, Has.Length.EqualTo(64));
            }
            Assert.That(provenance.EntityIds, Is.Not.InstanceOf<int[]>());
            Assert.Throws<NotSupportedException>(() => ((IList<int>)provenance.EntityIds)[0] = 9999);
        }

        [Test]
        [Category("Extended")]
        public void StageBackedWallProvenance_RejectsEveryStoredEntityStateFieldDrift()
        {
            using var fixture = CreateStageFixture();
            var stage = fixture.Stage;
            var buildResult = fixture.BuildResult;
            var mutations = BuildStoredFieldMutations();
            var reflectedFields = typeof(EntityState)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                mutations.Select(mutation => mutation.Name).OrderBy(name => name, StringComparer.Ordinal),
                Is.EqualTo(reflectedFields),
                "Every stored EntityState field must have an explicit semantic mutation case.");

            var wallIndex = Array.FindIndex(buildResult.InitialEntities, entity => entity.type == EntityType.Wall);
            Assert.That(wallIndex, Is.GreaterThanOrEqualTo(0));
            foreach (var mutation in mutations)
            {
                var entities = buildResult.InitialEntities.ToArray();
                entities[wallIndex] = mutation.Mutate(entities[wallIndex]);
                var drifted = CloneBuildResult(buildResult, entities);

                var exception = Assert.Throws<InvalidOperationException>(() => ComposeStageBacked(stage, drifted), mutation.Name);
                Assert.That(exception.Message, Is.Not.Empty, mutation.Name);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageBackedWallProvenance_RejectsMissingExtraAndDuplicateRuntimeEntities()
        {
            using var fixture = CreateStageFixture();
            var stage = fixture.Stage;
            var buildResult = fixture.BuildResult;
            var wall = buildResult.InitialEntities.First(entity => entity.type == EntityType.Wall);
            var missing = buildResult.InitialEntities.Where(entity => entity.entityId != wall.entityId).ToArray();
            var extraWall = wall;
            extraWall.entityId += 100000;
            var extra = buildResult.InitialEntities.Concat(new[] { extraWall }).ToArray();
            var duplicate = buildResult.InitialEntities.Concat(new[] { wall }).ToArray();

            Assert.Throws<InvalidOperationException>(() => ComposeStageBacked(stage, CloneBuildResult(buildResult, missing)));
            Assert.Throws<InvalidOperationException>(() => ComposeStageBacked(stage, CloneBuildResult(buildResult, extra)));
            Assert.Throws<InvalidOperationException>(() => ComposeStageBacked(stage, CloneBuildResult(buildResult, duplicate)));
        }

        [Test]
        [Category("Extended")]
        public void StageBackedWallProvenance_RejectsKindTypeAndAuthoredIdentityDrift()
        {
            using var fixture = CreateStageFixture();
            var enemyIndex = Array.FindIndex(fixture.BuildResult.InitialEntities, entity =>
                entity.type == EntityType.Unit && entity.unitRole == UnitRole.Enemy);
            var wallIndex = Array.FindIndex(fixture.BuildResult.InitialEntities, entity => entity.type == EntityType.Wall);
            var nonWallAsWall = fixture.BuildResult.InitialEntities.ToArray();
            nonWallAsWall[enemyIndex].type = EntityType.Wall;
            var wallAsBox = fixture.BuildResult.InitialEntities.ToArray();
            wallAsBox[wallIndex].type = EntityType.Box;

            Assert.Throws<InvalidOperationException>(() => ComposeStageBacked(
                fixture.Stage,
                CloneBuildResult(fixture.BuildResult, nonWallAsWall)));
            Assert.Throws<InvalidOperationException>(() => ComposeStageBacked(
                fixture.Stage,
                CloneBuildResult(fixture.BuildResult, wallAsBox)));

            using var invalidIdStage = new StageDefinitionScope(CreateInMemoryStage(
                CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                CreateSpawn(0, StageSpawnKind.Wall, 1, 0)));
            Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(invalidIdStage.Stage));

            using var duplicateIdStage = new StageDefinitionScope(CreateInMemoryStage(
                CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                CreateSpawn(20, StageSpawnKind.Wall, 1, 0),
                CreateSpawn(20, StageSpawnKind.Wall, 2, 0)));
            Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(duplicateIdStage.Stage));

            using var wrongSourceKindStage = new StageDefinitionScope(CreateDefaultInMemoryStage());
            var serializedObject = new SerializedObject(wrongSourceKindStage.Stage);
            serializedObject.FindProperty("wallSpawns").GetArrayElementAtIndex(0)
                .FindPropertyRelative("Kind").intValue = (int)StageSpawnKind.Enemy;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(wrongSourceKindStage.Stage));
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_RuntimeSpawnedWallNoneAndMutableEntitiesRemainFallbackOnly()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
            var entities = harness.Entities.ToList();
            var prototype = harness.Entities.First();
            entities.Add(CreateRuntimeEntity(prototype, 9001, EntityType.Wall));
            entities.Add(CreateRuntimeEntity(prototype, 9002, EntityType.None));
            entities.Add(CreateRuntimeEntity(prototype, 9003, EntityType.Box));

            harness.Store(entities, tickIndex: 1, presentationData: new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                new[]
                {
                    CreateVisibilityChange(9001, TickVisibilityChangeKind.Spawn, harness),
                    CreateVisibilityChange(9002, TickVisibilityChangeKind.Spawn, harness),
                    CreateVisibilityChange(9003, TickVisibilityChangeKind.Spawn, harness),
                }));

            var rows = capture.Frames.Single().EntityObservations
                .Where(row => row.EntityId >= 9001 && row.EntityId <= 9003).ToArray();
            Assert.That(rows, Has.Length.EqualTo(3));
            Assert.That(rows.All(row => !row.IsProvenanceCandidate &&
                                        !row.IsAdmittedStaticWall &&
                                        row.SelectionKind == StaticWallTargetSelectionKind.FallbackWithoutCache &&
                                        row.DidRunProjection && row.DidRunPose && row.DidRunSlot && row.DidInsert), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetDiagnostics_CommittedMapsStayStableAndCaptureDoesNotChangeOutput()
        {
            string firstDump;
            string secondDump;
            using (var harness = CreateHarness())
            using (GameplayCommittedFrameDiagnostics.BeginCapture())
            {
                harness.Store(harness.Entities, tickIndex: 1);
                firstDump = harness.ApplyAndBuildOutputDump();
                harness.Store(harness.Entities, tickIndex: 2);
                secondDump = harness.ApplyAndBuildOutputDump();
            }

            string diagnosticsOnDump;
            using (var harness = CreateHarness())
            using (GameplayCommittedFrameDiagnostics.BeginCapture())
            {
                harness.Store(harness.Entities, tickIndex: 1);
                diagnosticsOnDump = harness.ApplyAndBuildOutputDump();
            }

            string diagnosticsOffDump;
            using (var harness = CreateHarness())
            {
                harness.Store(harness.Entities, tickIndex: 1);
                diagnosticsOffDump = harness.ApplyAndBuildOutputDump();
            }

            Assert.That(secondDump, Is.EqualTo(firstDump));
            Assert.That(diagnosticsOffDump, Is.EqualTo(diagnosticsOnDump));
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetDiagnostics_LifecycleReasonsAdvanceGenerationExactlyOnce()
        {
            using (var harness = CreateHarness())
            using (var capture = GameplayCommittedFrameDiagnostics.BeginCapture())
            {
                harness.Store(harness.Entities, tickIndex: 1);
                var removed = harness.Entities.Where(entity => entity.entityId != harness.WallEntityId).ToArray();
                harness.Store(
                    removed,
                    tickIndex: 2,
                    presentationData: new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        new[] { CreateVisibilityChange(harness.WallEntityId, TickVisibilityChangeKind.Remove, harness) }));

                var row = capture.Frames[1].EntityObservations.Single(observation =>
                    observation.EntityId == harness.WallEntityId);
                Assert.That(row.ObservationKind, Is.EqualTo(StaticWallTargetObservationKind.MissingLifecycle));
                Assert.That(row.SelectionKind, Is.EqualTo(StaticWallTargetSelectionKind.NotPresentable));
                Assert.That(row.RetireReason, Is.EqualTo(StaticWallTargetRetireReason.RemovedOrExit));
                Assert.That(row.PresentationGeneration, Is.EqualTo(1));
                Assert.That(row.DidRunProjection || row.DidRunPose || row.DidRunSlot || row.DidInsert, Is.False);
            }

            using (var harness = CreateHarness())
            using (var capture = GameplayCommittedFrameDiagnostics.BeginCapture())
            {
                harness.Store(harness.Entities, tickIndex: 1);
                harness.Store(
                    harness.Entities,
                    tickIndex: 2,
                    presentationData: new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        new[]
                        {
                            CreateVisibilityChange(harness.WallEntityId, TickVisibilityChangeKind.Remove, harness),
                            CreateVisibilityChange(harness.WallEntityId, TickVisibilityChangeKind.Spawn, harness),
                        }));

                var row = capture.Frames[1].EntityObservations.Single(observation =>
                    observation.EntityId == harness.WallEntityId);
                Assert.That(row.PresentationGeneration, Is.EqualTo(1));
                Assert.That(row.RetireReason, Is.EqualTo(StaticWallTargetRetireReason.SpawnOrGeneration));
                Assert.That(row.SelectionKind, Is.EqualTo(StaticWallTargetSelectionKind.FallbackWithoutCache));
            }
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_RepeatedUnchangedWall_SecondCommittedFrameHits()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();

            harness.Store(harness.Entities, tickIndex: 1);
            harness.Store(harness.Entities, tickIndex: 2);
            var second = capture.Frames[1];

            Assert.That(second.EligibleCount, Is.GreaterThan(0), "fixture must contain presentable stage-backed Walls");
            Assert.That(second.HitCount, Is.GreaterThan(0),
                "PACKAGE4_EXPECTED_RED_SECOND_FRAME_WALL_CACHE_HIT");
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_RepeatedUnchangedWall_ReducesProjectionPoseAndSlotWork()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();

            harness.Store(harness.Entities, tickIndex: 1);
            harness.Store(harness.Entities, tickIndex: 2);
            var first = capture.Frames[0];
            var second = capture.Frames[1];
            var allWallValueWorkReduced =
                second.TryProjectEntityCellCount < first.TryProjectEntityCellCount &&
                second.CreateEntityPoseCount < first.CreateEntityPoseCount &&
                second.TryGetProjectedEntitySlotCount < first.TryGetProjectedEntitySlotCount;

            Assert.That(allWallValueWorkReduced, Is.True,
                "PACKAGE4_EXPECTED_RED_PROJECTION_POSE_SLOT_REDUCTION");
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_DynamicEnemyAndSlidingBoxChanges_PreserveWallHits()
        {
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
            using (var enemyHarness = CreateHarness())
            {
                enemyHarness.Store(enemyHarness.Entities, tickIndex: 1);
                var enemyChanged = CloneWithDynamicMutation(
                    enemyHarness,
                    enemyHarness.Entities,
                    EntityType.Unit,
                    UnitRole.Enemy);
                enemyHarness.Store(enemyChanged, tickIndex: 2);
            }

            using (var boxHarness = CreateHarness())
            {
                boxHarness.Store(boxHarness.Entities, tickIndex: 11);
                var boxChanged = CloneWithSlidingBoxMutation(boxHarness, boxHarness.Entities);
                boxHarness.Store(boxChanged, tickIndex: 12);
            }

            var minimumHitCount = Math.Min(capture.Frames[1].HitCount, capture.Frames[3].HitCount);

            Assert.That(minimumHitCount, Is.GreaterThan(0),
                "PACKAGE4_EXPECTED_RED_DYNAMIC_ONLY_MUTATION_PRESERVES_WALL_HIT");
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_TopologyProjectorAndSessionChanges_RebuildBeforeSelection()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();

            harness.Store(harness.Entities, tickIndex: 1);
            harness.Store(harness.Entities, tickIndex: 2);
            var inactiveTopology = new CubeTopologyState(
                FaceIdUtility.GetNext(FaceIdUtility.GetNext(harness.Topology.BottomFace)));
            harness.Store(harness.Entities, tickIndex: 3, topology: inactiveTopology);
            harness.Store(harness.Entities, tickIndex: 4);
            var replacementProjector = new GameplayCubeProjector(
                harness.Projector.BoardBounds,
                harness.Projector.CellSize,
                harness.Projector.FaceSeamGap);
            harness.Store(harness.Entities, tickIndex: 5, projector: replacementProjector);
            harness.ResetSession();
            harness.Store(harness.Entities, tickIndex: 6);

            var initial = capture.Frames[0];
            var repeated = capture.Frames[1];
            var inactive = capture.Frames[2];
            var reactivated = capture.Frames[3];
            var projectorReset = capture.Frames[4];
            var sessionReset = capture.Frames[5];
            Assert.That(initial.RebuildCount, Is.EqualTo(initial.EligibleCount));
            Assert.That(repeated.HitCount, Is.EqualTo(repeated.EligibleCount));
            Assert.That(inactive.OutputEntityIds, Has.None.EqualTo(harness.WallEntityId));
            Assert.That(inactive.CacheCount, Is.Zero);
            Assert.That(reactivated.HitCount, Is.Zero);
            Assert.That(reactivated.RebuildCount, Is.EqualTo(reactivated.EligibleCount));
            Assert.That(reactivated.InvalidationHistogram.Topology, Is.GreaterThan(0));
            Assert.That(projectorReset.HitCount, Is.Zero);
            Assert.That(projectorReset.RebuildCount, Is.EqualTo(projectorReset.EligibleCount));
            Assert.That(projectorReset.InvalidationHistogram.ProjectorProfile, Is.GreaterThan(0));
            Assert.That(sessionReset.SessionId, Is.GreaterThan(projectorReset.SessionId));
            Assert.That(sessionReset.HitCount, Is.Zero);
            Assert.That(sessionReset.RebuildCount, Is.EqualTo(sessionReset.EligibleCount));
            Assert.That(sessionReset.InvalidationHistogram.Session, Is.GreaterThan(0));
            Assert.That(sessionReset.CacheCount, Is.EqualTo(sessionReset.EligibleCount));
            Assert.That(sessionReset.CacheHighWaterMark, Is.EqualTo(sessionReset.CacheCount));

            using var changedStage = new StageDefinitionScope(CreateInMemoryStage(
                CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                CreateSpawn(20, StageSpawnKind.Wall, 3, 0),
                CreateSpawn(21, StageSpawnKind.Wall, 2, 0),
                CreateSpawn(30, StageSpawnKind.Enemy, 0, 1),
                CreateSpawn(40, StageSpawnKind.Box, 1, 1)));
            var changedBuild = StageRuntimeBuilder.Build(changedStage.Stage);
            var changedComposition = ComposeStageBacked(changedStage.Stage, changedBuild);
            harness.ResetSession(changedComposition.StaticWallPresentationProvenance);
            harness.Store(changedBuild.InitialEntities, tickIndex: 7);
            var staticRevisionReset = capture.Frames[6];
            Assert.That(staticRevisionReset.HitCount, Is.Zero);
            Assert.That(staticRevisionReset.RebuildCount, Is.EqualTo(staticRevisionReset.EligibleCount));
            Assert.That(staticRevisionReset.InvalidationHistogram.Session, Is.GreaterThan(0));
            Assert.That(staticRevisionReset.InvalidationHistogram.StaticRevision, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_EveryStoredWallMutation_PermanentlyRetiresCandidate()
        {
            foreach (var mutation in BuildStoredFieldMutations().Where(item =>
                         item.Name != nameof(EntityState.entityId)))
            {
                using var harness = CreateHarness();
                using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
                harness.Store(harness.Entities, tickIndex: 1);
                var mutated = harness.Entities.ToArray();
                var wallIndex = Array.FindIndex(mutated, entity => entity.entityId == harness.WallEntityId);
                mutated[wallIndex] = mutation.Mutate(mutated[wallIndex]);
                harness.Store(mutated, tickIndex: 2);
                harness.Store(harness.Entities, tickIndex: 3);

                var mutationRow = capture.Frames[1].EntityObservations.Single(row =>
                    row.EntityId == harness.WallEntityId);
                var restoredRow = capture.Frames[2].EntityObservations.Single(row =>
                    row.EntityId == harness.WallEntityId);
                Assert.That(mutationRow.RetireReason,
                    Is.EqualTo(StaticWallTargetRetireReason.SignatureChanged), mutation.Name);
                Assert.That(mutationRow.IsAdmittedStaticWall, Is.False, mutation.Name);
                Assert.That(capture.Frames[1].CacheCount, Is.LessThan(capture.Frames[0].CacheCount), mutation.Name);
                Assert.That(restoredRow.IsAdmittedStaticWall, Is.False, mutation.Name);
                Assert.That(restoredRow.SelectionKind,
                    Is.EqualTo(StaticWallTargetSelectionKind.FallbackWithoutCache), mutation.Name);
            }
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_ExitAndSpawnSameTick_AdvancesOnceAndPermanentlyRetires()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
            harness.Store(harness.Entities, tickIndex: 1);
            harness.Store(
                harness.Entities,
                tickIndex: 2,
                presentationData: CreateExitAndSpawnPresentationData(harness));
            harness.Store(harness.Entities, tickIndex: 3);

            var lifecycleRow = capture.Frames[1].EntityObservations.Single(row =>
                row.EntityId == harness.WallEntityId);
            var laterRow = capture.Frames[2].EntityObservations.Single(row =>
                row.EntityId == harness.WallEntityId);
            Assert.That(lifecycleRow.PresentationGeneration, Is.EqualTo(1));
            Assert.That(lifecycleRow.RetireReason,
                Is.EqualTo(StaticWallTargetRetireReason.SpawnOrGeneration));
            Assert.That(lifecycleRow.SelectionKind,
                Is.EqualTo(StaticWallTargetSelectionKind.FallbackWithoutCache));
            Assert.That(laterRow.IsAdmittedStaticWall, Is.False);
            Assert.That(laterRow.SelectionKind,
                Is.EqualTo(StaticWallTargetSelectionKind.FallbackWithoutCache));
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_SameIdWallToMutableOrNoneType_NeverHitsOldTarget()
        {
            foreach (var replacement in new[]
                     {
                         (EntityType.Box, UnitRole.None),
                         (EntityType.Unit, UnitRole.Player),
                         (EntityType.Unit, UnitRole.Enemy),
                         (EntityType.None, UnitRole.None),
                     })
            {
                using var harness = CreateHarness();
                using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
                harness.Store(harness.Entities, tickIndex: 1);
                var entities = harness.Entities.ToArray();
                var index = Array.FindIndex(entities, entity => entity.entityId == harness.WallEntityId);
                entities[index].type = replacement.Item1;
                entities[index].unitRole = replacement.Item2;
                harness.Store(entities, tickIndex: 2);
                harness.Store(harness.Entities, tickIndex: 3);

                var replacementRow = capture.Frames[1].EntityObservations.Single(row =>
                    row.EntityId == harness.WallEntityId);
                var restoredRow = capture.Frames[2].EntityObservations.Single(row =>
                    row.EntityId == harness.WallEntityId);
                Assert.That(replacementRow.SelectionKind,
                    Is.EqualTo(StaticWallTargetSelectionKind.FallbackWithoutCache), replacement.ToString());
                Assert.That(replacementRow.RetireReason,
                    Is.EqualTo(StaticWallTargetRetireReason.SignatureChanged), replacement.ToString());
                Assert.That(capture.Frames[1].HitCount, Is.LessThan(capture.Frames[0].EligibleCount), replacement.ToString());
                Assert.That(restoredRow.IsAdmittedStaticWall, Is.False, replacement.ToString());
                Assert.That(restoredRow.SelectionKind,
                    Is.EqualTo(StaticWallTargetSelectionKind.FallbackWithoutCache), replacement.ToString());
            }
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_DuplicateSameIdUnknownGeneration_NormalizesOneFallbackAndRetires()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
            harness.Store(harness.Entities, tickIndex: 1);
            var duplicateInput = harness.Entities.Concat(new[]
            {
                harness.Entities.Single(entity => entity.entityId == harness.WallEntityId),
            }).ToArray();

            Assert.DoesNotThrow(() => harness.Store(duplicateInput, tickIndex: 2));
            harness.Store(harness.Entities, tickIndex: 3);

            var unknownFrame = capture.Frames[1];
            var unknownRows = unknownFrame.EntityObservations.Where(row =>
                row.EntityId == harness.WallEntityId).ToArray();
            Assert.That(unknownRows, Has.Length.EqualTo(1));
            Assert.That(unknownRows[0].PresentationGeneration, Is.EqualTo(1));
            Assert.That(unknownRows[0].RetireReason,
                Is.EqualTo(StaticWallTargetRetireReason.UnknownGeneration));
            Assert.That(unknownRows[0].SelectionKind,
                Is.EqualTo(StaticWallTargetSelectionKind.FallbackWithoutCache));
            Assert.That(unknownFrame.OutputEntityIds.Count(id => id == harness.WallEntityId), Is.EqualTo(1));
            Assert.That(capture.Frames[2].EntityObservations.Single(row =>
                row.EntityId == harness.WallEntityId).IsAdmittedStaticWall, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_CacheHitStillResolvesDestroyedAndReplacementView()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
            harness.Store(harness.Entities, tickIndex: 1);
            var initialView = harness.StateStore.ViewsByEntityId[harness.WallEntityId];
            var initialCreateCount = harness.ViewCreateCount;
            UnityEngine.Object.DestroyImmediate(initialView.gameObject);

            harness.Store(harness.Entities, tickIndex: 2);

            var wallRow = capture.Frames[1].EntityObservations.Single(row =>
                row.EntityId == harness.WallEntityId);
            var replacementView = harness.StateStore.ViewsByEntityId[harness.WallEntityId];
            Assert.That(wallRow.SelectionKind, Is.EqualTo(StaticWallTargetSelectionKind.Hit));
            Assert.That(wallRow.DidRunViewResolve && wallRow.ViewResolved, Is.True);
            Assert.That(harness.ViewCreateCount, Is.EqualTo(initialCreateCount + 1));
            Assert.That(replacementView, Is.Not.Null);
            Assert.That(replacementView, Is.Not.SameAs(initialView));
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_UnprojectableFallback_DoesNotCreateViewOrInsertTarget()
        {
            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
            var entities = harness.Entities.ToList();
            var unprojectable = CreateRuntimeEntity(entities[0], 9901, EntityType.Box);
            unprojectable.position = new SurfaceCell(
                harness.Topology.BottomFace,
                harness.Projector.BoardBounds.MaxInclusive.x + 100,
                harness.Projector.BoardBounds.MaxInclusive.y + 100);
            entities.Add(unprojectable);

            harness.Store(entities, tickIndex: 1);

            var row = capture.Frames.Single().EntityObservations.Single(observation =>
                observation.EntityId == unprojectable.entityId);
            Assert.That(row.DidRunProjection, Is.True);
            Assert.That(row.DidRunViewResolve, Is.False);
            Assert.That(row.SelectionKind, Is.EqualTo(StaticWallTargetSelectionKind.NotPresentable));
            Assert.That(row.DidInsert, Is.False);
            Assert.That(harness.StateStore.ViewsByEntityId.ContainsKey(unprojectable.entityId), Is.False);
            Assert.That(harness.StateStore.CommittedLocalTargetPoses.ContainsKey(unprojectable.entityId), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_CachedCommittedValuesMatchTestLocalUncachedOracle()
        {
            using var harness = CreateHarness();
            harness.Store(harness.Entities, tickIndex: 1);
            harness.Store(harness.Entities, tickIndex: 2);

            Assert.That(
                BuildCommittedValueDump(harness.StateStore),
                Is.EqualTo(BuildPureUncachedOracleDump(harness.Entities, harness.Topology, harness.Projector)));
        }

        [Test]
        [Category("Extended")]
        public void StaticWallTargetCache_ValueTypeHasNoUnityOwnershipAndCapacityIsBounded()
        {
            var cacheType = typeof(GameplayCommittedFrameBuilder).GetNestedType(
                "StaticWallCommittedTarget",
                BindingFlags.NonPublic);
            Assert.That(cacheType, Is.Not.Null);
            Assert.That(cacheType.IsValueType, Is.True);
            Assert.That(cacheType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Any(type => typeof(UnityEngine.Object).IsAssignableFrom(type) ||
                             type == typeof(EntityState) ||
                             (type != typeof(string) &&
                              typeof(System.Collections.IEnumerable).IsAssignableFrom(type))), Is.False);

            using var harness = CreateHarness();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();
            for (var i = 0; i < 8; i++)
            {
                var projector = new GameplayCubeProjector(
                    harness.Projector.BoardBounds,
                    1f + (i * 0.01f),
                    harness.Projector.FaceSeamGap);
                harness.Store(harness.Entities, tickIndex: 100 + i, projector: projector);
            }

            Assert.That(capture.Frames.All(frame => frame.CacheCount <= frame.ProvenanceCount), Is.True);
            Assert.That(capture.Frames.All(frame => frame.CacheHighWaterMark <= frame.ProvenanceCount), Is.True);
        }

        private static List<(string Label, GameplayCommittedFrameObservation Frame, string MapDump)> CaptureDeterministicMatrix()
        {
            var samples = new List<(string, GameplayCommittedFrameObservation, string)>();
            using var capture = GameplayCommittedFrameDiagnostics.BeginCapture();

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "steady/initial", harness, harness.Entities, 1);
                Capture(samples, capture, "steady/repeated", harness, harness.Entities, 2);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "dynamic/enemy-initial", harness, harness.Entities, 3);
                var enemyMoved = CloneWithDynamicMutation(harness, harness.Entities, EntityType.Unit, UnitRole.Enemy);
                Capture(samples, capture, "dynamic/enemy-only-move", harness, enemyMoved, 4);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "dynamic/sliding-box-initial", harness, harness.Entities, 5);
                var boxMoved = CloneWithSlidingBoxMutation(harness, harness.Entities);
                Capture(samples, capture, "dynamic/sliding-box-only-move-timer", harness, boxMoved, 6);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "dynamic/player-initial", harness, harness.Entities, 7);
                var playerMoved = CloneWithDynamicMutation(harness, harness.Entities, EntityType.Unit, UnitRole.Player);
                var playerIndex = Array.FindIndex(playerMoved, entity =>
                    entity.type == EntityType.Unit && entity.unitRole == UnitRole.Player);
                playerMoved[playerIndex].stateTimer++;
                Capture(samples, capture, "dynamic/player-only-move-timer", harness, playerMoved, 8);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "topology/active", harness, harness.Entities, 11);
                var inactive = new CubeTopologyState(
                    FaceIdUtility.GetNext(FaceIdUtility.GetNext(harness.Topology.BottomFace)));
                Capture(samples, capture, "topology/inactive", harness, harness.Entities, 12, inactive);
                Capture(samples, capture, "topology/reactivated", harness, harness.Entities, 13);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "signature/initial", harness, harness.Entities, 21);
                var mutated = harness.Entities.ToArray();
                var wallIndex = Array.FindIndex(mutated, entity => entity.entityId == harness.WallEntityId);
                mutated[wallIndex].hp++;
                Capture(samples, capture, "signature/mutated", harness, mutated, 22);
                Capture(samples, capture, "signature/restored", harness, harness.Entities, 23);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "removal/initial", harness, harness.Entities, 31);
                var removed = harness.Entities.Where(entity => entity.entityId != harness.WallEntityId).ToArray();
                Capture(samples, capture, "removal/absent", harness, removed, 32);
                Capture(samples, capture, "removal/reappeared", harness, harness.Entities, 33);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "reuse/initial", harness, harness.Entities, 41);
                var replaced = harness.Entities.ToArray();
                var wallIndex = Array.FindIndex(replaced, entity => entity.entityId == harness.WallEntityId);
                replaced[wallIndex].type = EntityType.Box;
                Capture(samples, capture, "reuse/same-id-box", harness, replaced, 42);
            }

            using (var harness = CreateHarness())
            {
                Capture(samples, capture, "projector/initial", harness, harness.Entities, 51);
                var replacement = new GameplayCubeProjector(
                    harness.Projector.BoardBounds,
                    harness.Projector.CellSize,
                    harness.Projector.FaceSeamGap);
                Capture(samples, capture, "projector/replaced", harness, harness.Entities, 52, projector: replacement);
            }

            return samples;
        }

        private static void Capture(
            ICollection<(string Label, GameplayCommittedFrameObservation Frame, string MapDump)> samples,
            GameplayCommittedFrameDiagnostics.CaptureScope capture,
            string label,
            Harness harness,
            IReadOnlyList<EntityState> entities,
            int tickIndex,
            CubeTopologyState? topology = null,
            GameplayCubeProjector projector = null)
        {
            harness.Store(entities, tickIndex, topology, projector);
            samples.Add((label, capture.Frames[capture.Frames.Count - 1], BuildCommittedMapDump(harness.StateStore)));
        }

        private static void AssertExactAccounting(GameplayCommittedFrameObservation frame, string label)
        {
            var inputRows = frame.EntityObservations.Where(row => row.HasInputEntityState).ToArray();
            var missingRows = frame.EntityObservations.Where(row => !row.HasInputEntityState).ToArray();
            var insertedRows = frame.EntityObservations.Where(row => row.DidInsert)
                .OrderBy(row => row.InsertOrdinal)
                .ToArray();
            var eligibleRows = frame.EntityObservations.Where(row =>
                row.IsAdmittedStaticWall && row.SelectionKind != StaticWallTargetSelectionKind.NotPresentable).ToArray();

            Assert.That(inputRows, Has.Length.EqualTo(frame.InputCount), label);
            Assert.That(inputRows.Select(row => row.EntityId), Is.Unique, label);
            Assert.That(missingRows.Select(row => row.EntityId), Is.Unique, label);
            Assert.That(missingRows.All(row => row.ObservationKind == StaticWallTargetObservationKind.MissingLifecycle), Is.True, label);
            Assert.That(missingRows.All(row => inputRows.All(input => input.EntityId != row.EntityId)), Is.True, label);
            Assert.That(missingRows.All(row => row.IsProvenanceCandidate &&
                                               !row.IsAdmittedStaticWall &&
                                               !row.IsPolicyPresentable &&
                                               row.SelectionKind == StaticWallTargetSelectionKind.NotPresentable &&
                                               row.RetireReason == StaticWallTargetRetireReason.RemovedOrExit &&
                                               !row.DidRunProjection && !row.DidRunPose && !row.DidRunSlot &&
                                               !row.DidRunViewResolve && !row.DidInsert), Is.True, label);
            Assert.That(frame.EligibleCount, Is.EqualTo(eligibleRows.Length), label);
            Assert.That(frame.EligibleCount, Is.EqualTo(frame.HitCount + frame.MissCount), label);
            Assert.That(frame.MissCount, Is.EqualTo(
                frame.RebuildCount + eligibleRows.Count(row =>
                    row.SelectionKind == StaticWallTargetSelectionKind.FallbackWithoutCache)), label);
            Assert.That(frame.FallbackEntityScanCount, Is.EqualTo(inputRows.Length), label);
            Assert.That(frame.DynamicEntityProjectCount, Is.EqualTo(inputRows.Count(row =>
                !row.IsProvenanceCandidate && row.DidRunProjection)), label);
            Assert.That(frame.TryProjectEntityCellCount, Is.EqualTo(frame.EntityObservations.Count(row => row.DidRunProjection)), label);
            Assert.That(frame.CreateEntityPoseCount, Is.EqualTo(frame.EntityObservations.Count(row => row.DidRunPose)), label);
            Assert.That(frame.TryGetProjectedEntitySlotCount, Is.EqualTo(frame.EntityObservations.Count(row => row.DidRunSlot)), label);
            Assert.That(frame.ViewResolveCount, Is.EqualTo(frame.EntityObservations.Count(row => row.DidRunViewResolve)), label);
            Assert.That(frame.InsertCount, Is.EqualTo(insertedRows.Length), label);
            Assert.That(frame.OutputEntityIds, Is.EqualTo(insertedRows.Select(row => row.EntityId)), label);
            Assert.That(frame.OutputEntityIds, Is.Ordered.Ascending, label);
            Assert.That(frame.OutputEntityIds, Is.Unique, label);
            Assert.That(insertedRows.Select(row => row.InsertOrdinal), Is.EqualTo(Enumerable.Range(0, insertedRows.Length)), label);
            Assert.That(frame.EntityObservations.Where(row =>
                row.SelectionKind != StaticWallTargetSelectionKind.NotPresentable).All(row => row.DidInsert), Is.True, label);
            Assert.That(frame.EntityObservations.Where(row =>
                row.SelectionKind == StaticWallTargetSelectionKind.Hit).All(row =>
                !row.DidRunProjection && !row.DidRunPose && !row.DidRunSlot && row.DidInsert), Is.True, label);
            Assert.That(frame.EntityObservations.Where(row =>
                row.SelectionKind == StaticWallTargetSelectionKind.Rebuild ||
                row.SelectionKind == StaticWallTargetSelectionKind.FallbackWithoutCache).All(row =>
                row.DidRunProjection && row.DidRunPose && row.DidRunSlot && row.DidInsert), Is.True, label);
            Assert.That(frame.EntityObservations.Where(row =>
                row.HasInputEntityState && row.RetireReason != StaticWallTargetRetireReason.None &&
                row.IsPolicyPresentable).All(row =>
                row.SelectionKind == StaticWallTargetSelectionKind.FallbackWithoutCache), Is.True, label);

            Assert.That(frame.RetireHistogram.RemovedOrExit, Is.EqualTo(frame.EntityObservations.Count(row =>
                row.RetireReason == StaticWallTargetRetireReason.RemovedOrExit)), label);
            Assert.That(frame.RetireHistogram.SignatureChanged, Is.EqualTo(frame.EntityObservations.Count(row =>
                row.RetireReason == StaticWallTargetRetireReason.SignatureChanged)), label);
            Assert.That(frame.RetireHistogram.SpawnOrGeneration, Is.EqualTo(frame.EntityObservations.Count(row =>
                row.RetireReason == StaticWallTargetRetireReason.SpawnOrGeneration)), label);
            Assert.That(frame.RetireHistogram.UnknownGeneration, Is.EqualTo(frame.EntityObservations.Count(row =>
                row.RetireReason == StaticWallTargetRetireReason.UnknownGeneration)), label);
            foreach (var reason in new[]
                     {
                         StaticWallTargetInvalidation.Session,
                         StaticWallTargetInvalidation.Generation,
                         StaticWallTargetInvalidation.Signature,
                         StaticWallTargetInvalidation.StaticRevision,
                         StaticWallTargetInvalidation.Topology,
                         StaticWallTargetInvalidation.ProjectorProfile,
                     })
            {
                Assert.That(frame.InvalidationHistogram.Count(reason), Is.EqualTo(frame.EntityObservations.Count(row =>
                    (row.Invalidations & reason) != 0)), $"{label}/{reason}");
            }

            foreach (var row in frame.EntityObservations)
            {
                Assert.That(row.SessionId, Is.EqualTo(frame.SessionId), label);
                Assert.That(row.FrameOrdinal, Is.EqualTo(frame.FrameOrdinal), label);
                Assert.That(row.StaticRevision, Has.Length.EqualTo(64), label);
                Assert.That(row.TopologyRevision, Is.GreaterThanOrEqualTo(0), label);
                Assert.That(row.ProjectorProfileRevision, Is.GreaterThanOrEqualTo(1), label);
                if (row.HasInputEntityState)
                {
                    Assert.That(row.CanonicalInputEntityState, Is.Not.Empty, label);
                }
                if (row.IsProvenanceCandidate)
                {
                    Assert.That(row.ProvenanceSignature, Has.Length.EqualTo(64), label);
                    Assert.That(row.PresentationGeneration, Is.GreaterThanOrEqualTo(0), label);
                }
            }

            Assert.That(frame.InputSemanticFingerprint, Has.Length.EqualTo(64), label);
            Assert.That(frame.CanonicalInputDump, Does.Contain("provenanceRevision="), label);
            Assert.That(frame.CanonicalInputDump, Does.Contain("|sourceKind="), label);
            Assert.That(frame.CanonicalInputDump, Does.Contain("|signature="), label);
            Assert.That(frame.CacheHighWaterMark, Is.GreaterThanOrEqualTo(frame.CacheCount), label);
            Assert.That(frame.CacheHighWaterMark, Is.LessThanOrEqualTo(frame.ProvenanceCount), label);
        }

        private static string BuildCanonicalMatrix(
            IReadOnlyList<(string Label, GameplayCommittedFrameObservation Frame, string MapDump)> samples)
        {
            var builder = new StringBuilder();
            foreach (var sample in samples)
            {
                builder.Append("scenario=").Append(sample.Label).Append('\n')
                    .Append(sample.Frame.CanonicalInputDump)
                    .Append("scenarioEnd\n");
            }

            return builder.ToString();
        }

        private static string BuildMeasurementMatrix(
            IReadOnlyList<(string Label, GameplayCommittedFrameObservation Frame, string MapDump)> samples)
        {
            var builder = new StringBuilder();
            foreach (var sample in samples)
            {
                var frame = sample.Frame;
                builder.Append(sample.Label)
                    .Append("|session=").Append(frame.SessionId.ToString(CultureInfo.InvariantCulture))
                    .Append("|frame=").Append(frame.FrameOrdinal.ToString(CultureInfo.InvariantCulture))
                    .Append("|tick=").Append(frame.TickIndex.ToString(CultureInfo.InvariantCulture))
                    .Append("|reason=").Append(((int)frame.Reason).ToString(CultureInfo.InvariantCulture))
                    .Append("|input=").Append(frame.InputCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|provenance=").Append(frame.ProvenanceCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|eligible=").Append(frame.EligibleCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|hit=").Append(frame.HitCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|miss=").Append(frame.MissCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|rebuild=").Append(frame.RebuildCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|retire=").Append(frame.RetireHistogram.RemovedOrExit.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.RetireHistogram.SignatureChanged.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.RetireHistogram.SpawnOrGeneration.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.RetireHistogram.UnknownGeneration.ToString(CultureInfo.InvariantCulture))
                    .Append("|invalidate=").Append(frame.InvalidationHistogram.Session.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.InvalidationHistogram.Generation.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.InvalidationHistogram.Signature.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.InvalidationHistogram.StaticRevision.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.InvalidationHistogram.Topology.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(frame.InvalidationHistogram.ProjectorProfile.ToString(CultureInfo.InvariantCulture))
                    .Append("|scan=").Append(frame.FallbackEntityScanCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|dynamicProject=").Append(frame.DynamicEntityProjectCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|project=").Append(frame.TryProjectEntityCellCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|pose=").Append(frame.CreateEntityPoseCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|slot=").Append(frame.TryGetProjectedEntitySlotCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|view=").Append(frame.ViewResolveCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|insert=").Append(frame.InsertCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|output=").Append(string.Join(",", frame.OutputEntityIds.Select(
                        id => id.ToString(CultureInfo.InvariantCulture))))
                    .Append("|fingerprint=").Append(frame.InputSemanticFingerprint)
                    .Append("|cache=").Append(frame.CacheCount.ToString(CultureInfo.InvariantCulture))
                    .Append("|highWater=").Append(frame.CacheHighWaterMark.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            return builder.ToString();
        }

        private static string BuildEntityRowMatrix(
            IReadOnlyList<(string Label, GameplayCommittedFrameObservation Frame, string MapDump)> samples)
        {
            var builder = new StringBuilder();
            foreach (var sample in samples)
            {
                builder.Append("scenario=").Append(sample.Label).Append('\n');
                foreach (var row in sample.Frame.EntityObservations.OrderBy(row => row.EntityId))
                {
                    builder.Append("row=");
                    AppendInvariant(builder, row.SessionId).Append('|');
                    AppendInvariant(builder, row.FrameOrdinal).Append('|');
                    AppendInvariant(builder, row.EntityId).Append('|');
                    AppendInvariant(builder, (int)row.ObservationKind).Append('|');
                    AppendInvariant(builder, row.HasInputEntityState ? 1 : 0).Append('|')
                        .Append(row.CanonicalInputEntityState).Append('|');
                    AppendInvariant(builder, row.IsProvenanceCandidate ? 1 : 0).Append('|')
                        .Append(row.ProvenanceSignature).Append('|');
                    AppendInvariant(builder, row.PresentationGeneration).Append('|')
                        .Append(row.StaticRevision).Append('|');
                    AppendInvariant(builder, row.TopologyRevision).Append('|');
                    AppendInvariant(builder, row.ProjectorProfileRevision).Append('|');
                    AppendInvariant(builder, row.IsAdmittedStaticWall ? 1 : 0).Append('|');
                    AppendInvariant(builder, row.IsPolicyPresentable ? 1 : 0).Append('|');
                    AppendInvariant(builder, (int)row.SelectionKind).Append('|');
                    AppendInvariant(builder, (int)row.RetireReason).Append('|');
                    AppendInvariant(builder, (int)row.Invalidations).Append('|');
                    AppendInvariant(builder, row.DidRunProjection ? 1 : 0).Append('|');
                    AppendInvariant(builder, row.DidRunPose ? 1 : 0).Append('|');
                    AppendInvariant(builder, row.DidRunSlot ? 1 : 0).Append('|');
                    AppendInvariant(builder, row.DidRunViewResolve ? 1 : 0).Append('|');
                    AppendInvariant(builder, row.ViewResolved ? 1 : 0).Append('|');
                    AppendInvariant(builder, row.DidUseFallbackValuePath ? 1 : 0).Append('|');
                    AppendInvariant(builder, row.InsertOrdinal).Append('\n');
                }
                builder.Append("scenarioEnd\n");
            }

            return builder.ToString();
        }

        private static string BuildCommittedMapMatrix(
            IReadOnlyList<(string Label, GameplayCommittedFrameObservation Frame, string MapDump)> samples)
        {
            var builder = new StringBuilder();
            foreach (var sample in samples)
            {
                builder.Append("scenario=").Append(sample.Label).Append('\n')
                    .Append(sample.MapDump)
                    .Append("scenarioEnd\n");
            }

            return builder.ToString();
        }

        private static string BuildCommittedMapDump(GameplayPresentationStateStore stateStore)
        {
            var builder = new StringBuilder();
            builder.Append("topology=");
            AppendInvariant(builder, (int)stateStore.CommittedTopology.BottomFace).Append('\n');
            foreach (var pair in stateStore.CommittedLocalTargetPoses.OrderBy(pair => pair.Key))
            {
                var pose = pair.Value;
                builder.Append("pose=");
                AppendInvariant(builder, pair.Key).Append('|')
                    .Append(pose.Position.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Position.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Position.z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(pose.Rotation.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.w.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            foreach (var pair in stateStore.CommittedFacesByEntityId.OrderBy(pair => pair.Key))
            {
                builder.Append("face=");
                AppendInvariant(builder, pair.Key).Append('|');
                AppendInvariant(builder, (int)pair.Value).Append('\n');
            }
            foreach (var pair in stateStore.CommittedProjectedSlotsByEntityId.OrderBy(pair => pair.Key))
            {
                builder.Append("slot=");
                AppendInvariant(builder, pair.Key).Append('|');
                AppendInvariant(builder, (int)pair.Value).Append('\n');
            }
            builder.Append("processing=")
                .Append(string.Join(",", stateStore.BuildProcessingEntityIds()
                    .Select(id => id.ToString(CultureInfo.InvariantCulture))))
                .Append('\n');
            builder.Append("views=")
                .Append(string.Join(",", stateStore.ViewsByEntityId.Keys.OrderBy(id => id)
                    .Select(id => id.ToString(CultureInfo.InvariantCulture))))
                .Append('\n');
            return builder.ToString();
        }

        private static string BuildCommittedValueDump(GameplayPresentationStateStore stateStore)
        {
            var builder = new StringBuilder();
            AppendCommittedValues(
                builder,
                stateStore.CommittedLocalTargetPoses,
                stateStore.CommittedFacesByEntityId,
                stateStore.CommittedProjectedSlotsByEntityId);
            return builder.ToString();
        }

        private static string BuildPureUncachedOracleDump(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            GameplayCubeProjector projector)
        {
            var stateStore = new GameplayPresentationStateStore();
            var poseResolver = new GameplayPoseResolver(stateStore, new GameplayPresentationTrackState());
            var poses = new Dictionary<int, GameplayEntityPose>();
            var faces = new Dictionary<int, FaceId>();
            var slots = new Dictionary<int, GameplayProjectedFaceSlot>();
            foreach (var entity in entities.OrderBy(entity => entity.entityId))
            {
                if (entity.boardPresence != EntityBoardPresence.Occupying ||
                    !topology.IsFaceActive(entity.position.face) ||
                    !projector.TryProjectEntityCell(entity.position, topology, entity.type, out var projectedPose))
                {
                    continue;
                }

                poses.Add(entity.entityId, poseResolver.CreateEntityPose(
                    projector,
                    entity.position,
                    topology,
                    projectedPose,
                    entity.facing));
                faces.Add(entity.entityId, entity.position.face);
                if (projector.TryGetProjectedEntitySlot(entity.position, topology, out var slot))
                {
                    slots.Add(entity.entityId, slot);
                }
            }

            var builder = new StringBuilder();
            AppendCommittedValues(builder, poses, faces, slots);
            return builder.ToString();
        }

        private static void AppendCommittedValues(
            StringBuilder builder,
            IReadOnlyDictionary<int, GameplayEntityPose> poses,
            IReadOnlyDictionary<int, FaceId> faces,
            IReadOnlyDictionary<int, GameplayProjectedFaceSlot> slots)
        {
            foreach (var pair in poses.OrderBy(pair => pair.Key))
            {
                var pose = pair.Value;
                builder.Append("pose=");
                AppendInvariant(builder, pair.Key).Append('|')
                    .Append(pose.Position.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Position.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Position.z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(pose.Rotation.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.w.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }

            foreach (var pair in faces.OrderBy(pair => pair.Key))
            {
                builder.Append("face=");
                AppendInvariant(builder, pair.Key).Append('|');
                AppendInvariant(builder, (int)pair.Value).Append('\n');
            }

            foreach (var pair in slots.OrderBy(pair => pair.Key))
            {
                builder.Append("slot=");
                AppendInvariant(builder, pair.Key).Append('|');
                AppendInvariant(builder, (int)pair.Value).Append('\n');
            }
        }

        private static string BuildAppliedOutputDump(GameplayPresentationStateStore stateStore)
        {
            var builder = new StringBuilder(BuildCommittedMapDump(stateStore));
            foreach (var pair in stateStore.PresentedLocalPosesByEntityId.OrderBy(pair => pair.Key))
            {
                var pose = pair.Value;
                builder.Append("appliedPose=");
                AppendInvariant(builder, pair.Key).Append('|')
                    .Append(pose.Position.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Position.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Position.z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(pose.Rotation.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(pose.Rotation.w.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            foreach (var pair in stateStore.ViewsByEntityId.OrderBy(pair => pair.Key))
            {
                var view = pair.Value;
                builder.Append("viewTransform=");
                AppendInvariant(builder, pair.Key).Append('|');
                AppendInvariant(builder, view != null && view.gameObject.activeSelf ? 1 : 0).Append('|')
                    .Append(view.transform.localPosition.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(view.transform.localPosition.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(view.transform.localPosition.z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(view.transform.localRotation.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(view.transform.localRotation.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(view.transform.localRotation.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(view.transform.localRotation.w.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }

            return builder.ToString();
        }

        private static GameplayTimingProfile CreateTimingProfile()
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8);
        }

        private static StringBuilder AppendInvariant(StringBuilder builder, long value)
        {
            return builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static EntityState[] CloneWithDynamicMutation(
            Harness harness,
            IReadOnlyList<EntityState> source,
            EntityType type,
            UnitRole role)
        {
            var clone = source.ToArray();
            var index = Array.FindIndex(clone, entity => entity.type == type && entity.unitRole == role);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"fixture must contain {type}/{role}");
            var entity = clone[index];
            var nextX = entity.position.x < harness.Projector.BoardBounds.MaxInclusive.x
                ? entity.position.x + 1
                : entity.position.x - 1;
            entity.position = new SurfaceCell(entity.position.face, nextX, entity.position.y);
            entity.stateTimer++;
            clone[index] = entity;
            return clone;
        }

        private static EntityState[] CloneWithSlidingBoxMutation(
            Harness harness,
            IReadOnlyList<EntityState> source)
        {
            var clone = source.ToArray();
            var index = Array.FindIndex(clone, entity => entity.type == EntityType.Box);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "fixture must contain a Box");
            var entity = clone[index];
            var nextX = entity.position.x < harness.Projector.BoardBounds.MaxInclusive.x
                ? entity.position.x + 1
                : entity.position.x - 1;
            entity.position = new SurfaceCell(entity.position.face, nextX, entity.position.y);
            entity.state = EntityPhaseState.Sliding;
            entity.stateTimer = Math.Max(1, entity.stateTimer + 1);
            entity.boxCapabilities |= BoxCapabilities.Push;
            clone[index] = entity;
            return clone;
        }

        private static EntityState CreateRuntimeEntity(
            EntityState prototype,
            int entityId,
            EntityType type)
        {
            prototype.entityId = entityId;
            prototype.type = type;
            prototype.unitRole = UnitRole.None;
            prototype.state = EntityPhaseState.Idle;
            prototype.stateTimer = 0;
            prototype.spawnTick = 77;
            prototype.boxCapabilities = type == EntityType.Box ? BoxCapabilities.Push : BoxCapabilities.None;
            return prototype;
        }

        private static TickVisibilityChange CreateVisibilityChange(
            int entityId,
            TickVisibilityChangeKind kind,
            Harness harness)
        {
            return new TickVisibilityChange(
                entityId,
                kind,
                harness.Entities[0].position,
                harness.Topology,
                harness.Entities[0].facing);
        }

        private static TickPresentationData CreateExitAndSpawnPresentationData(Harness harness)
        {
            var wall = harness.Entities.Single(entity => entity.entityId == harness.WallEntityId);
            return new TickPresentationData(
                entityMotions: Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: new[]
                {
                    new TickEntityExitPresentationSignal(
                        wall.entityId,
                        TickEntityExitCause.OutOfBounds,
                        wall.position,
                        harness.Topology,
                        wall.facing,
                        wall.type),
                },
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                entitySpawnSignals: new[]
                {
                    new EntitySpawnPresentationSignal(
                        wall.entityId,
                        EntityPresentationKind.Unknown,
                        EntitySpawnPresentationReason.ScriptedSpawn,
                        wall.position,
                        harness.Topology,
                        wall.facing,
                        sourceTileFeature: null),
                });
        }

        private static IReadOnlyList<(string Name, Func<EntityState, EntityState> Mutate)> BuildStoredFieldMutations()
        {
            return new (string, Func<EntityState, EntityState>)[]
            {
                (nameof(EntityState.entityId), entity => { entity.entityId += 10000; return entity; }),
                (nameof(EntityState.position), entity => { entity.position = new SurfaceCell(FaceIdUtility.GetNext(entity.position.face), entity.position.x + 1, entity.position.y); return entity; }),
                (nameof(EntityState.hp), entity => { entity.hp++; return entity; }),
                (nameof(EntityState.maxHp), entity => { entity.maxHp++; return entity; }),
                (nameof(EntityState.teamId), entity => { entity.teamId++; return entity; }),
                (nameof(EntityState.type), entity => { entity.type = EntityType.Box; return entity; }),
                (nameof(EntityState.unitRole), entity => { entity.unitRole = UnitRole.Enemy; return entity; }),
                (nameof(EntityState.unitMobilityKind), entity => { entity.unitMobilityKind = UnitMobilityKind.Air; return entity; }),
                (nameof(EntityState.state), entity => { entity.state = EntityPhaseState.Acting; return entity; }),
                (nameof(EntityState.stateTimer), entity => { entity.stateTimer++; return entity; }),
                (nameof(EntityState.facing), entity => { entity.facing = entity.facing == Direction.Left ? Direction.Right : Direction.Left; return entity; }),
                (nameof(EntityState.boardPresence), entity => { entity.boardPresence = EntityBoardPresence.Detached; return entity; }),
                (nameof(EntityState.markedForDeath), entity => { entity.markedForDeath = !entity.markedForDeath; return entity; }),
                (nameof(EntityState.spawnTick), entity => { entity.spawnTick++; return entity; }),
                (nameof(EntityState.boxCapabilities), entity => { entity.boxCapabilities = BoxCapabilities.Push; return entity; }),
                (nameof(EntityState.boxArchetype), entity => { entity.boxArchetype = BoxArchetype.Moon; return entity; }),
                (nameof(EntityState.gravityFieldPhase), entity => { entity.gravityFieldPhase = GravityFieldPhase.Charging; return entity; }),
                (nameof(EntityState.gravityFieldTimerTicks), entity => { entity.gravityFieldTimerTicks++; return entity; }),
                (nameof(EntityState.kineticInstigatorEntityId), entity => { entity.kineticInstigatorEntityId++; return entity; }),
                (nameof(EntityState.kineticInstigatorTeamId), entity => { entity.kineticInstigatorTeamId++; return entity; }),
                (nameof(EntityState.aiMode), entity => { entity.aiMode = EnemyAiMode.Patrol; return entity; }),
                (nameof(EntityState.aiStateTimer), entity => { entity.aiStateTimer++; return entity; }),
                (nameof(EntityState.enemyLocomotionCooldownTicks), entity => { entity.enemyLocomotionCooldownTicks++; return entity; }),
                (nameof(EntityState.enemyAttackCooldownTicks), entity => { entity.enemyAttackCooldownTicks++; return entity; }),
                (nameof(EntityState.enemyAttackCooldownTotalTicks), entity => { entity.enemyAttackCooldownTotalTicks++; return entity; }),
            };
        }

        private static Harness CreateHarness(StageStaticWallPresentationProvenance provenanceOverride = null)
        {
            var stage = CreateDefaultInMemoryStage();
            var buildResult = StageRuntimeBuilder.Build(stage);
            var composition = ComposeStageBacked(stage, buildResult);
            var provenance = provenanceOverride ?? composition.StaticWallPresentationProvenance;
            var sourceProvenance = composition.StaticWallPresentationProvenance;
            var firstWallId = sourceProvenance.EntityIds[0];
            Assert.That(sourceProvenance.TryGetEntry(firstWallId, out var wallEntry), Is.True);
            var topology = new CubeTopologyState(wallEntry.InitialWall.position.face);
            var root = new GameObject("Package4_StaticWallCache_Harness");
            var registry = root.AddComponent<GameplayEntityViewRegistry>();
            registry.ConfigureSearchRoot(root.transform);
            var viewFactory = new HarnessViewFactory(root.transform);
            var viewBinder = new GameplayEntityViewBinder(registry, viewFactory);
            var runtime = GameplayPresentationRuntimeCompositionFactory.Create();
            var projector = new GameplayCubeProjector(buildResult.BoardBounds, 1f, 1f);
            runtime.StateStore.ResetSession(topology);
            runtime.CommittedFrameBuilder.ResetSession(provenance, projector);
            var entities = buildResult.InitialEntities.ToArray();
            var boxIndex = Array.FindIndex(entities, entity => entity.type == EntityType.Box);
            entities[boxIndex].state = EntityPhaseState.Sliding;
            entities[boxIndex].stateTimer = 3;
            return new Harness(
                root,
                stage,
                runtime,
                runtime.CommittedFrameBuilder,
                runtime.StateStore,
                entities,
                firstWallId,
                topology,
                projector,
                viewBinder,
                viewFactory,
                provenance);
        }

        private static StageFixture CreateStageFixture()
        {
            var stage = CreateDefaultInMemoryStage();
            var buildResult = StageRuntimeBuilder.Build(stage);
            return new StageFixture(stage, buildResult, ComposeStageBacked(stage, buildResult));
        }

        private static StageDefinition CreateDefaultInMemoryStage()
        {
            return CreateInMemoryStage(
                CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                CreateSpawn(20, StageSpawnKind.Wall, 1, 0),
                CreateSpawn(21, StageSpawnKind.Wall, 2, 0),
                CreateSpawn(30, StageSpawnKind.Enemy, 0, 1),
                CreateSpawn(40, StageSpawnKind.Box, 1, 1));
        }

        private static StageDefinition CreateInMemoryStage(params StageSpawnDefinition[] spawns)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.name = "Package4_InMemoryStage";
            var serializedObject = new SerializedObject(stage);
            var board = serializedObject.FindProperty("board");
            board.FindPropertyRelative("MinInclusive").vector2IntValue = Vector2Int.zero;
            board.FindPropertyRelative("MaxInclusive").vector2IntValue = new Vector2Int(4, 4);
            board.FindPropertyRelative("InitialBottomFace").intValue = (int)FaceId.Floor;
            SetSpawnArray(serializedObject.FindProperty("playerSpawns"), spawns, StageSpawnKind.Player);
            SetSpawnArray(serializedObject.FindProperty("enemySpawns"), spawns, StageSpawnKind.Enemy);
            SetSpawnArray(serializedObject.FindProperty("boxSpawns"), spawns, StageSpawnKind.Box);
            SetSpawnArray(serializedObject.FindProperty("wallSpawns"), spawns, StageSpawnKind.Wall);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }

        private static StageSpawnDefinition CreateSpawn(
            int entityId,
            StageSpawnKind kind,
            int x,
            int y)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = kind,
                Cell = new SurfaceCell(FaceId.Floor, x, y),
                Facing = Direction.Right,
                Hp = 3,
                UnitMobilityKind = UnitMobilityKind.Ground,
                BoxCapabilities = BoxCapabilities.Push,
                EnemyAiMode = EnemyAiMode.None,
                UnitStackGroup = string.Empty,
            };
        }

        private static void SetSpawnArray(
            SerializedProperty property,
            IReadOnlyList<StageSpawnDefinition> spawns,
            StageSpawnKind kind)
        {
            var selected = spawns.Where(spawn => spawn.Kind == kind).ToArray();
            property.arraySize = selected.Length;
            for (var i = 0; i < selected.Length; i++)
            {
                var spawn = selected[i];
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = spawn.EntityId;
                element.FindPropertyRelative("Kind").intValue = (int)spawn.Kind;
                var cell = element.FindPropertyRelative("Cell");
                cell.FindPropertyRelative("face").intValue = (int)spawn.Cell.face;
                cell.FindPropertyRelative("x").intValue = spawn.Cell.x;
                cell.FindPropertyRelative("y").intValue = spawn.Cell.y;
                element.FindPropertyRelative("Facing").intValue = (int)spawn.Facing;
                element.FindPropertyRelative("Hp").intValue = spawn.Hp;
                element.FindPropertyRelative("UnitMobilityKind").intValue = (int)spawn.UnitMobilityKind;
                element.FindPropertyRelative("BoxCapabilities").intValue = (int)spawn.BoxCapabilities;
                element.FindPropertyRelative("BoxArchetype").intValue = (int)spawn.BoxArchetype;
                element.FindPropertyRelative("EnemyAiMode").intValue = (int)spawn.EnemyAiMode;
                element.FindPropertyRelative("EnemyAiStateTimer").intValue = spawn.EnemyAiStateTimer;
                element.FindPropertyRelative("EnemyAiProfile").objectReferenceValue = spawn.EnemyAiProfile;
                element.FindPropertyRelative("UnitStackGroup").stringValue = spawn.UnitStackGroup ?? string.Empty;
            }
        }

        private static StageSceneCompositionData ComposeStageBacked(
            StageDefinition stage,
            StageRuntimeBuildResult buildResult)
        {
            return StageSceneCompositionAssembler.ComposeStageBacked(
                stage,
                buildResult,
                StagePresentationAssembler.EmptyResolvedData,
                StageAudioAssembler.EmptyResolvedData);
        }

        private static StageRuntimeBuildResult CloneBuildResult(
            StageRuntimeBuildResult source,
            EntityState[] entities)
        {
            return new StageRuntimeBuildResult(
                source.BoardBounds,
                source.InitialTopology,
                entities,
                source.InitialTileFeatures,
                source.TileFeatureDefinitions,
                source.MoonBlockRespawnDefinitions,
                source.PlayerEntityId,
                source.ObjectiveRuntimeDefinition,
                source.EnemyAiProfileOverrides);
        }

        private sealed class Harness : IDisposable
        {
            private readonly GameObject _root;
            private readonly StageDefinition _stage;
            private readonly GameplayPresentationRuntimeComposition _runtime;
            private readonly GameplayCommittedFrameBuilder _builder;
            private readonly GameplayEntityViewBinder _viewBinder;
            private readonly HarnessViewFactory _viewFactory;
            private readonly StageStaticWallPresentationProvenance _provenance;

            internal Harness(
                GameObject root,
                StageDefinition stage,
                GameplayPresentationRuntimeComposition runtime,
                GameplayCommittedFrameBuilder builder,
                GameplayPresentationStateStore stateStore,
                EntityState[] entities,
                int wallEntityId,
                CubeTopologyState topology,
                GameplayCubeProjector projector,
                GameplayEntityViewBinder viewBinder,
                HarnessViewFactory viewFactory,
                StageStaticWallPresentationProvenance provenance)
            {
                _root = root;
                _stage = stage;
                _runtime = runtime;
                _builder = builder;
                StateStore = stateStore;
                Entities = entities;
                WallEntityId = wallEntityId;
                Topology = topology;
                Projector = projector;
                _viewBinder = viewBinder;
                _viewFactory = viewFactory;
                _provenance = provenance;
            }

            internal EntityState[] Entities { get; }
            internal int WallEntityId { get; }
            internal CubeTopologyState Topology { get; }
            internal GameplayCubeProjector Projector { get; }
            internal GameplayPresentationStateStore StateStore { get; }
            internal int ViewCreateCount => _viewFactory.CreateCount;

            internal void ResetSession(StageStaticWallPresentationProvenance provenance = null)
            {
                StateStore.ResetSession(Topology);
                _builder.ResetSession(provenance ?? _provenance, Projector);
            }

            internal string ApplyAndBuildOutputDump()
            {
                var frames = new ResolvedPresentationFrameSet();
                var collector = new PresentationPoseCandidateCollector(StateStore, _runtime.TrackState);
                var resolver = new PresentationBasePoseFrameResolver(collector);
                resolver.Resolve(sourceTick: 0, frames);
                _runtime.EntityPresentationApplier.Apply(
                    deltaTime: 0f,
                    hasActiveBoardRotationTween: false,
                    frames,
                    new ResolvedPresentationChannelSet(),
                    new ResolvedPresentationVisibilitySet(),
                    _viewBinder,
                    CreateTimingProfile());
                return BuildAppliedOutputDump(StateStore);
            }

            internal void Store(
                IReadOnlyList<EntityState> entities,
                int tickIndex,
                CubeTopologyState? topology = null,
                GameplayCubeProjector projector = null,
                TickPresentationData presentationData = null)
            {
                _builder.StoreCommittedFrame(
                    entities,
                    topology ?? Topology,
                    projector ?? Projector,
                    _viewBinder,
                    topologyCommitted: null,
                    presentationData,
                    tickIndex: tickIndex,
                    reason: CommittedFrameStoreReason.Tick);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_root);
                UnityEngine.Object.DestroyImmediate(_stage);
            }
        }

        private sealed class StageFixture : IDisposable
        {
            internal StageFixture(
                StageDefinition stage,
                StageRuntimeBuildResult buildResult,
                StageSceneCompositionData composition)
            {
                Stage = stage;
                BuildResult = buildResult;
                Composition = composition;
            }

            internal StageDefinition Stage { get; }
            internal StageRuntimeBuildResult BuildResult { get; }
            internal StageSceneCompositionData Composition { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Stage);
            }
        }

        private sealed class StageDefinitionScope : IDisposable
        {
            internal StageDefinitionScope(StageDefinition stage)
            {
                Stage = stage;
            }

            internal StageDefinition Stage { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Stage);
            }
        }

        private sealed class HarnessViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;

            internal HarnessViewFactory(Transform parent)
            {
                _parent = parent;
            }

            internal int CreateCount { get; private set; }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                CreateCount++;
                var gameObject = new GameObject($"Package4_Entity_{entity.entityId}");
                gameObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = gameObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                return view;
            }
        }
    }
}
