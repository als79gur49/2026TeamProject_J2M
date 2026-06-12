using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringNormalizedDriftTests
    {
        [Test]
        public void NormalizedDrift_IgnoresSpawnArrayOrder()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                ReverseArray(fixture.Gameplay, "enemySpawns");
                var report = fixture.Validate();
                Assert.That(report.Issues.Any(IsDriftIssue), Is.False, FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedDrift_DetectsSpawnCellChange()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetSpawnCellX(fixture.Gameplay, "enemySpawns", 0, 3);
                AssertHasCode(fixture.Validate(), "GameplayDrift.SpawnFieldMismatch");
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedDrift_DetectsSpawnHpChange()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetSpawnInt(fixture.Gameplay, "enemySpawns", 0, "Hp", 7);
                AssertHasCode(fixture.Validate(), "GameplayDrift.SpawnFieldMismatch");
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringDrift_PlayerMobilityMismatch_IsDetected()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetAuthoringPlacementMobility(fixture.Authoring, StageAuthoringEntityKind.Player, UnitMobilityKind.Air);

                var report = fixture.Validate();

                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == "GameplayDrift.SpawnFieldMismatch" &&
                        issue.FieldName == "UnitMobilityKind"),
                    Is.True,
                    FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringDrift_EnemyMobilityMismatch_IsDetected()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetSpawnInt(fixture.Gameplay, "enemySpawns", 0, "UnitMobilityKind", (int)UnitMobilityKind.Air);

                var report = fixture.Validate();

                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == "GameplayDrift.SpawnFieldMismatch" &&
                        issue.FieldName == "UnitMobilityKind"),
                    Is.True,
                    FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringDrift_UnitMobilityMatch_HasNoDrift()
        {
            var fixture = StageAuthoringTestFixture.Create();
            try
            {
                SetAuthoringPlacementMobility(fixture.Authoring, StageAuthoringEntityKind.Player, UnitMobilityKind.Air);
                SetAuthoringPlacementMobility(fixture.Authoring, StageAuthoringEntityKind.Enemy, UnitMobilityKind.Air);
                var generationReport = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(generationReport.HasErrors, Is.False, StageAuthoringTestFixture.FormatGenerationIssues(generationReport));

                var report = fixture.Validate();

                Assert.That(report.Issues.Any(IsDriftIssue), Is.False, FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringDrift_NonUnitMobilityMismatch_IsIgnoredOrNormalized()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetAuthoringPlacementMobility(fixture.Authoring, StageAuthoringEntityKind.Box, UnitMobilityKind.Air);
                SetSpawnInt(fixture.Gameplay, "boxSpawns", 0, "UnitMobilityKind", (int)UnitMobilityKind.Air);

                var report = fixture.Validate();

                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == "GameplayDrift.SpawnFieldMismatch" &&
                        issue.FieldName == "UnitMobilityKind"),
                    Is.False,
                    FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedDrift_WallFacingAuthoringSupport_DetectsFacingSemanticDrift()
        {
            Assert.That(StageAuthoringKindRegistry.Wall.SupportsFacingAuthoring, Is.True);

            var expected = CreateSingleSpawnSnapshot(StageSpawnKind.Wall, Direction.Up);
            var actual = CreateSingleSpawnSnapshot(StageSpawnKind.Wall, Direction.Down);
            var issues = StageAuthoringDriftComparer.CompareGameplay(
                expected,
                actual,
                new StageAuthoringDriftContext(
                    StageValidationSeverity.Error,
                    StageValidationTiming.TestOrCi,
                    context: null,
                    assetPath: string.Empty,
                    stageId: "wall-facing-authoring",
                    authoringAssetName: string.Empty,
                    outputAssetName: string.Empty));

            Assert.That(
                issues.Any(issue =>
                    issue.Code == "GameplayDrift.SpawnFieldMismatch" &&
                    issue.FieldName == "Facing"),
                Is.True,
                FormatIssues(issues));
        }

        [Test]
        public void NormalizedDrift_ZoneIdMismatch_ReportsGameplayDriftZoneMismatch()
        {
            var expected = CreateZoneSnapshot("goal", FaceId.Floor, new Vector2Int(0, 0), new Vector2Int(0, 0));
            var actual = CreateZoneSnapshot("other", FaceId.Floor, new Vector2Int(0, 0), new Vector2Int(0, 0));

            var issues = CompareGameplay(expected, actual, "zone-id");

            Assert.That(issues.Any(issue => issue.Code == "GameplayDrift.ZoneMismatch"), Is.True, FormatIssues(issues));
        }

        [Test]
        public void NormalizedDrift_ZoneRegionMismatch_ReportsGameplayDriftZoneMismatch()
        {
            var expected = CreateZoneSnapshot("goal", FaceId.Floor, new Vector2Int(0, 0), new Vector2Int(0, 0));
            var actual = CreateZoneSnapshot("goal", FaceId.Floor, new Vector2Int(0, 0), new Vector2Int(1, 1));

            var issues = CompareGameplay(expected, actual, "zone-region");

            Assert.That(issues.Any(issue => issue.Code == "GameplayDrift.ZoneMismatch"), Is.True, FormatIssues(issues));
        }

        [Test]
        public void NormalizedDrift_ZoneCountMismatch_ReportsGameplayDriftZoneMismatch()
        {
            var expected = CreateZoneSnapshot("goal", FaceId.Floor, new Vector2Int(0, 0), new Vector2Int(0, 0));
            var actual = new StageAuthoringNormalizedGameplaySnapshot(
                expected.Board,
                expected.Spawns,
                expected.TileFeatures,
                Array.Empty<StageAuthoringNormalizedZone>(),
                expected.Objective);

            var issues = CompareGameplay(expected, actual, "zone-count");

            Assert.That(issues.Any(issue => issue.Code == "GameplayDrift.ZoneMismatch"), Is.True, FormatIssues(issues));
        }

        [Test]
        public void NormalizedDrift_ObjectiveTitleMismatch_ReportsGameplayDriftObjectiveMismatch()
        {
            var expected = CreateObjectiveSnapshot(
                new StageAuthoringNormalizedObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    Array.Empty<StageAuthoringNormalizedObjectiveCondition>(),
                    "Reach the Exit",
                    "Move to the exit zone."));
            var actual = CreateObjectiveSnapshot(
                new StageAuthoringNormalizedObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    Array.Empty<StageAuthoringNormalizedObjectiveCondition>(),
                    string.Empty,
                    "Move to the exit zone."));

            var issues = CompareGameplay(expected, actual, "objective-title");

            Assert.That(
                issues.Any(issue =>
                    issue.Code == "GameplayDrift.ObjectiveMismatch" &&
                    issue.FieldName == "Objective.ObjectiveTitle"),
                Is.True,
                FormatIssues(issues));
        }

        [Test]
        public void NormalizedDrift_ObjectiveConditionDisplayMismatch_ReportsGameplayDriftObjectiveMismatch()
        {
            var expected = CreateObjectiveSnapshot(
                new StageAuthoringNormalizedObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    new[]
                    {
                        new StageAuthoringNormalizedObjectiveCondition(
                            null,
                            true,
                            StageObjectiveConditionRole.PrimaryGoal,
                            "primary-goal",
                            "Reach the Exit Zone",
                            sortOrder: 0),
                    },
                    "Reach the Exit",
                    "Move to the exit zone."));
            var actual = CreateObjectiveSnapshot(
                new StageAuthoringNormalizedObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    new[]
                    {
                        new StageAuthoringNormalizedObjectiveCondition(
                            null,
                            true,
                            StageObjectiveConditionRole.PrimaryGoal,
                            "primary-goal",
                            string.Empty,
                            sortOrder: 10),
                    },
                    "Reach the Exit",
                    "Move to the exit zone."));

            var issues = CompareGameplay(expected, actual, "objective-display");

            Assert.That(
                issues.Any(issue =>
                    issue.Code == "GameplayDrift.ObjectiveMismatch" &&
                    issue.FieldName == "Objective.ConditionEntries[0].DisplayText"),
                Is.True,
                FormatIssues(issues));
            Assert.That(
                issues.Any(issue =>
                    issue.Code == "GameplayDrift.ObjectiveMismatch" &&
                    issue.FieldName == "Objective.ConditionEntries[0].SortOrder"),
                Is.True,
                FormatIssues(issues));
        }

        [Test]
        public void NormalizedDrift_DetectsEntityIdChange()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetSpawnInt(fixture.Gameplay, "enemySpawns", 0, "EntityId", 99);
                var report = fixture.Validate();
                Assert.That(
                    report.Issues.Any(issue => issue.Code == "GameplayDrift.SpawnMissing") &&
                    report.Issues.Any(issue => issue.Code == "GameplayDrift.SpawnUnexpected"),
                    Is.True,
                    FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedDrift_SpawnPresentationId_IsOwnedByPresentationBindings()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var serializedObject = new SerializedObject(fixture.Gameplay);
                var legacyProperty = serializedObject
                    .FindProperty("enemySpawns")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("PresentationId");

                Assert.That(
                    legacyProperty,
                    Is.Null,
                    "Enemy presentation ids are owned by StagePresentationDefinition bindings, not StageSpawnDefinition.");
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedPresentationDrift_DetectsEnemyBindingPresentationIdChange()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetBindingPresentationId(fixture.Presentation, "enemyPresentationBindings", 0, "changed");
                AssertHasCode(fixture.Validate(), "PresentationDrift.BindingFieldMismatch");
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_PresentationBindingIntegrity_StillReportsDrift()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var generatedBindings = fixture.Presentation.EnemyPresentationBindings;
                Assert.That(generatedBindings.Length, Is.GreaterThanOrEqualTo(2));

                SetEnemyBindings(
                    fixture.Presentation,
                    new[]
                    {
                        generatedBindings[1],
                        generatedBindings[0],
                    });

                var reorderedReport = fixture.Validate();
                Assert.That(
                    reorderedReport.Issues.Any(issue => issue.Code.StartsWith("PresentationDrift.", StringComparison.Ordinal)),
                    Is.False,
                    FormatIssues(reorderedReport));

                var changedBinding = generatedBindings[1];
                changedBinding.PresentationId = "changed";
                SetEnemyBindings(
                    fixture.Presentation,
                    new[]
                    {
                        changedBinding,
                        generatedBindings[0],
                    });

                var changedReport = fixture.Validate();
                AssertHasCode(changedReport, "PresentationDrift.BindingFieldMismatch");
                Assert.That(
                    changedReport.Issues.Any(issue => issue.Code == "PresentationBinding.MissingEnemyBinding"),
                    Is.False,
                    FormatIssues(changedReport));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedPresentationDrift_DoesNotIncludeEnemyCatalogVfxProfile()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            var profile = CreateProfile(GameplayVfxFamily.Enemy);
            try
            {
                SetFirstEnemyCatalogVfxProfile(fixture.Presentation, profile);

                var report = fixture.Validate();

                Assert.That(
                    report.Issues.Any(issue => issue.Code.StartsWith("PresentationDrift.", StringComparison.Ordinal)),
                    Is.False,
                    FormatIssues(report));
            }
            finally
            {
                Destroy(profile);
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedPresentationDrift_DetectsStaticBindingMissing()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetArraySize(fixture.Presentation, "staticEntityPresentationBindings", 0);
                AssertHasCode(fixture.Validate(), "PresentationDrift.BindingMissing");
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void NormalizedPresentationDrift_DoesNotFlagPresentationMetadataChange()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetString(fixture.Presentation, "displayName", "Edited Display");
                var report = fixture.Validate();
                Assert.That(report.Issues.Any(issue => issue.Code.StartsWith("PresentationDrift.", StringComparison.Ordinal)), Is.False, FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void EnforceGeneratedSyncFalse_DriftIsWarning()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced(enforceGeneratedSync: false);
            try
            {
                SetSpawnInt(fixture.Gameplay, "enemySpawns", 0, "Hp", 7);
                var issue = fixture.Validate().Issues.First(found => found.Code == "GameplayDrift.SpawnFieldMismatch");
                Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Warning));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void EnforceGeneratedSyncTrue_DriftIsError()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced(enforceGeneratedSync: true);
            try
            {
                SetSpawnInt(fixture.Gameplay, "enemySpawns", 0, "Hp", 7);
                var issue = fixture.Validate().Issues.First(found => found.Code == "GameplayDrift.SpawnFieldMismatch");
                Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Error));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringSyncUsesNormalizedDriftCodesOnly()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetSpawnInt(fixture.Gameplay, "enemySpawns", 0, "Hp", 7);
                SetBindingPresentationId(fixture.Presentation, "enemyPresentationBindings", 0, "changed");

                var driftIssues = fixture.Validate().Issues
                    .Where(issue =>
                        issue.Code.Contains("Drift", StringComparison.Ordinal) ||
                        issue.Code == "authoring.generated-output-mismatch" ||
                        issue.Code == "authoring.presentation-binding-drift")
                    .ToArray();

                Assert.That(driftIssues, Is.Not.Empty);
                Assert.That(
                    driftIssues.All(IsDriftIssue),
                    Is.True,
                    FormatIssues(driftIssues));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static bool IsDriftIssue(StageValidationIssue issue)
        {
            return issue.Code.StartsWith("GameplayDrift.", StringComparison.Ordinal) ||
                   issue.Code.StartsWith("PresentationDrift.", StringComparison.Ordinal);
        }

        private static void AssertHasCode(StageValidationReport report, string code)
        {
            Assert.That(report.Issues.Any(issue => issue.Code == code), Is.True, FormatIssues(report));
        }

        private static StageAuthoringNormalizedGameplaySnapshot CreateSingleSpawnSnapshot(
            StageSpawnKind kind,
            Direction facing)
        {
            return new StageAuthoringNormalizedGameplaySnapshot(
                new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(4, 4),
                    InitialBottomFace = FaceId.Floor,
                },
                new[]
                {
                    new StageAuthoringNormalizedSpawn(
                        "wall",
                        99,
                        kind,
                        new SurfaceCell(FaceId.Floor, 2, 3),
                        facing,
                        1,
                        UnitMobilityKind.Ground,
                        string.Empty,
                        BoxCapabilities.None,
                        BoxArchetype.Normal,
                        EnemyAiMode.None,
                        0,
                        null),
                },
                Array.Empty<StageAuthoringNormalizedTileFeature>(),
                Array.Empty<StageAuthoringNormalizedZone>(),
                StageAuthoringNormalizedObjective.Empty);
        }

        private static StageAuthoringNormalizedGameplaySnapshot CreateZoneSnapshot(
            string zoneId,
            FaceId face,
            Vector2Int minInclusive,
            Vector2Int maxInclusive)
        {
            return new StageAuthoringNormalizedGameplaySnapshot(
                new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(4, 4),
                    InitialBottomFace = FaceId.Floor,
                },
                Array.Empty<StageAuthoringNormalizedSpawn>(),
                Array.Empty<StageAuthoringNormalizedTileFeature>(),
                new[]
                {
                    new StageAuthoringNormalizedZone(
                        zoneId,
                        face,
                        new[]
                        {
                            new StageAuthoringNormalizedZoneRegion(minInclusive, maxInclusive),
                        }),
                },
                StageAuthoringNormalizedObjective.Empty);
        }

        private static StageAuthoringNormalizedGameplaySnapshot CreateObjectiveSnapshot(
            StageAuthoringNormalizedObjective objective)
        {
            return new StageAuthoringNormalizedGameplaySnapshot(
                new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(4, 4),
                    InitialBottomFace = FaceId.Floor,
                },
                Array.Empty<StageAuthoringNormalizedSpawn>(),
                Array.Empty<StageAuthoringNormalizedTileFeature>(),
                Array.Empty<StageAuthoringNormalizedZone>(),
                objective);
        }

        private static IReadOnlyList<StageValidationIssue> CompareGameplay(
            StageAuthoringNormalizedGameplaySnapshot expected,
            StageAuthoringNormalizedGameplaySnapshot actual,
            string stageId)
        {
            return StageAuthoringDriftComparer.CompareGameplay(
                expected,
                actual,
                new StageAuthoringDriftContext(
                    StageValidationSeverity.Error,
                    StageValidationTiming.TestOrCi,
                    context: null,
                    assetPath: string.Empty,
                    stageId: stageId,
                    authoringAssetName: string.Empty,
                    outputAssetName: string.Empty));
        }

        private static void ReverseArray(UnityEngine.Object target, string fieldName)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property.arraySize > 1)
            {
                property.MoveArrayElement(0, property.arraySize - 1);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArraySize(UnityEngine.Object target, string fieldName, int size)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(fieldName).arraySize = size;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSpawnCellX(StageDefinition stage, string groupName, int index, int x)
        {
            var serializedObject = new SerializedObject(stage);
            serializedObject.FindProperty(groupName).GetArrayElementAtIndex(index).FindPropertyRelative("Cell").FindPropertyRelative("x").intValue = x;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSpawnInt(StageDefinition stage, string groupName, int index, string fieldName, int value)
        {
            var serializedObject = new SerializedObject(stage);
            serializedObject.FindProperty(groupName).GetArrayElementAtIndex(index).FindPropertyRelative(fieldName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAuthoringPlacementMobility(
            StageAuthoringDefinition authoring,
            StageAuthoringEntityKind kind,
            UnitMobilityKind unitMobilityKind)
        {
            authoring.Placements.First(placement => placement.Kind == kind).UnitMobilityKind = unitMobilityKind;
        }

        private static void SetBindingPresentationId(StagePresentationDefinition presentation, string fieldName, int index, string value)
        {
            var serializedObject = new SerializedObject(presentation);
            serializedObject.FindProperty(fieldName).GetArrayElementAtIndex(index).FindPropertyRelative("PresentationId").stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnemyBindings(
            StagePresentationDefinition presentation,
            EnemyPresentationBinding[] bindings)
        {
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("enemyPresentationBindings");
            property.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                element.FindPropertyRelative("PresentationId").stringValue = bindings[i].PresentationId;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFirstEnemyCatalogVfxProfile(
            StagePresentationDefinition presentation,
            VfxProfileAsset profile)
        {
            var catalog = presentation.EnemyPresentationCatalog;
            Assert.That(catalog, Is.Not.Null);
            var serializedObject = new SerializedObject(catalog);
            var entries = serializedObject.FindProperty("entries");
            Assert.That(entries.arraySize, Is.GreaterThan(0));
            entries.GetArrayElementAtIndex(0)
                .FindPropertyRelative("VfxProfileAsset")
                .objectReferenceValue = profile;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static VfxProfileAsset CreateProfile(GameplayVfxFamily family)
        {
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            typeof(VfxProfileAsset)
                .GetField("family", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(profile, family);
            typeof(VfxProfileAsset)
                .GetField("bindings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(profile, Array.Empty<VfxBindingDefinitionAsset>());
            return profile;
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }

        private static void SetString(StagePresentationDefinition presentation, string fieldName, string value)
        {
            var serializedObject = new SerializedObject(presentation);
            serializedObject.FindProperty(fieldName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string FormatIssues(StageValidationReport report)
        {
            return FormatIssues(report.Issues);
        }

        private static string FormatIssues(IEnumerable<StageValidationIssue> issues)
        {
            return string.Join(Environment.NewLine, issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }
    }

    internal sealed class StageAuthoringTestFixture
    {
        private readonly UnityEngine.Object[] ownedObjects;
        private readonly EnemyAiProfile enemyProfile;

        private StageAuthoringTestFixture(
            StageContentEntry entry,
            StageAuthoringDefinition authoring,
            StageDefinition gameplay,
            StagePresentationDefinition presentation,
            EnemyPresentationCatalog enemyCatalog,
            StaticEntityPresentationCatalog staticCatalog,
            GameplayEntityView enemyViewPrefab,
            GameplayEntityView staticViewPrefab,
            EnemyAiProfile enemyProfile)
        {
            Entry = entry;
            Authoring = authoring;
            Gameplay = gameplay;
            Presentation = presentation;
            this.enemyProfile = enemyProfile;
            ownedObjects = new UnityEngine.Object[]
            {
                entry,
                authoring,
                gameplay,
                presentation,
                enemyCatalog,
                staticCatalog,
                enemyViewPrefab != null ? enemyViewPrefab.gameObject : null,
                staticViewPrefab != null ? staticViewPrefab.gameObject : null,
            };
        }

        public StageContentEntry Entry { get; }

        public StageAuthoringDefinition Authoring { get; }

        public StageDefinition Gameplay { get; }

        public StagePresentationDefinition Presentation { get; }

        public static StageAuthoringTestFixture CreateSynced(bool enforceGeneratedSync = true)
        {
            var fixture = Create(enforceGeneratedSync);
            var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
            Assert.That(report.HasErrors, Is.False, FormatGenerationIssues(report));
            return fixture;
        }

        public static StageAuthoringTestFixture Create(bool enforceGeneratedSync = true)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var enemyViewPrefab = CreateViewPrefab("authoring-test_EnemyViewPrefab");
            var staticViewPrefab = CreateViewPrefab("authoring-test_StaticViewPrefab");
            var enemyCatalog = CreateEnemyCatalog("enemy-view", enemyViewPrefab);
            var staticCatalog = CreateStaticCatalog("box-view", staticViewPrefab);
            var enemyProfile = ScriptableObject.CreateInstance<EnemyAiProfile>();

            entry.name = "authoring-test_Entry";
            authoring.name = "authoring-test_Authoring";
            gameplay.name = "authoring-test";
            presentation.name = "authoring-test_Presentation";
            entry.AssignStageId(StageId.CreateOrThrow("authoring-test"));
            entry.AssignAuthoringDefinition(authoring);
            entry.AssignGameplayDefinition(gameplay);
            entry.AssignPresentationDefinition(presentation);
            authoring.AssignGeneratedDefinitions(gameplay, presentation);
            authoring.SetOwnerMetadata(entry, string.Empty);
            presentation.SetOwnerMetadata(entry, string.Empty);
            authoring.SetEnforceGeneratedSync(enforceGeneratedSync);
            authoring.SetBoard(new StageBoardDefinition
            {
                MinInclusive = new Vector2Int(0, 0),
                MaxInclusive = new Vector2Int(4, 4),
                InitialBottomFace = FaceId.Floor,
            });
            authoring.SetPlacements(new[]
            {
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy-a", StageAuthoringEntityKind.Enemy, 1, 0, "enemy-view", enemyProfile),
                Placement("enemy-b", StageAuthoringEntityKind.Enemy, 2, 0, "enemy-view", enemyProfile),
                Placement("box", StageAuthoringEntityKind.Box, 3, 0, "box-view"),
            });
            authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());
            SetPresentationCatalogs(presentation, enemyCatalog, staticCatalog);
            return new StageAuthoringTestFixture(
                entry,
                authoring,
                gameplay,
                presentation,
                enemyCatalog,
                staticCatalog,
                enemyViewPrefab,
                staticViewPrefab,
                enemyProfile);
        }

        public StageValidationReport Validate()
        {
            return new StageCatalogValidator().ValidateEntries(
                new[] { Entry },
                aliasTable: null,
                new StageCatalogValidationOptions { Timing = StageValidationTiming.TestOrCi });
        }

        public void Destroy()
        {
            for (var i = 0; i < ownedObjects.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }

        private static StagePlacedEntityAuthoring Placement(
            string stableGuid,
            StageAuthoringEntityKind kind,
            int x,
            int y,
            string presentationId = "",
            EnemyAiProfile enemyProfileOverride = null)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = stableGuid,
                DisplayName = stableGuid,
                Kind = kind,
                Cell = new SurfaceCell(FaceId.Floor, x, y),
                Facing = Direction.Right,
                Hp = 1,
                UnitStackGroup = string.Empty,
                BoxCapabilities = BoxCapabilities.Push,
                EnemyAiMode = kind == StageAuthoringEntityKind.Enemy ? EnemyAiMode.Patrol : EnemyAiMode.None,
                EnemyAiProfileOverride = enemyProfileOverride,
                PresentationId = presentationId,
            };
        }

        private static EnemyPresentationCatalog CreateEnemyCatalog(string id, GameplayEntityView viewPrefab)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            var serializedObject = new SerializedObject(catalog);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("PresentationId").stringValue = id;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("ViewPrefab").objectReferenceValue = viewPrefab;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static StaticEntityPresentationCatalog CreateStaticCatalog(string id, GameplayEntityView viewPrefab)
        {
            var catalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();
            var serializedObject = new SerializedObject(catalog);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("PresentationId").stringValue = id;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("ViewPrefab").objectReferenceValue = viewPrefab;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static GameplayEntityView CreateViewPrefab(string name)
        {
            return new GameObject(name).AddComponent<GameplayEntityView>();
        }

        private static void SetPresentationCatalogs(
            StagePresentationDefinition presentation,
            EnemyPresentationCatalog enemyCatalog,
            StaticEntityPresentationCatalog staticCatalog)
        {
            var serializedObject = new SerializedObject(presentation);
            serializedObject.FindProperty("enemyPresentationCatalog").objectReferenceValue = enemyCatalog;
            serializedObject.FindProperty("staticEntityPresentationCatalog").objectReferenceValue = staticCatalog;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static string FormatGenerationIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }
    }
}
