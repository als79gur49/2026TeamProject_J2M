using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
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
        public void NormalizedDrift_WallFacingAuthoringSupportDoesNotMakeFacingSemanticDrift()
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
                Is.False,
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
        public void NormalizedDrift_IgnoresLegacySpawnPresentationId()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetSpawnString(fixture.Gameplay, "enemySpawns", 0, "PresentationId", "legacy-id");
                var report = fixture.Validate();
                Assert.That(report.Issues.Any(issue => issue.Code.StartsWith("GameplayDrift.", StringComparison.Ordinal)), Is.False, FormatIssues(report));
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
                SetString(fixture.Presentation, "resultTitle", "Edited Result");
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
                        string.Empty,
                        BoxCapabilities.None,
                        EnemyAiMode.None,
                        0,
                        null),
                },
                Array.Empty<StageAuthoringNormalizedZone>(),
                StageAuthoringNormalizedObjective.Empty);
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

        private static void SetSpawnString(StageDefinition stage, string groupName, int index, string fieldName, string value)
        {
            var serializedObject = new SerializedObject(stage);
            serializedObject.FindProperty(groupName).GetArrayElementAtIndex(index).FindPropertyRelative(fieldName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBindingPresentationId(StagePresentationDefinition presentation, string fieldName, int index, string value)
        {
            var serializedObject = new SerializedObject(presentation);
            serializedObject.FindProperty(fieldName).GetArrayElementAtIndex(index).FindPropertyRelative("PresentationId").stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
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

        private StageAuthoringTestFixture(
            StageContentEntry entry,
            StageAuthoringDefinition authoring,
            StageDefinition gameplay,
            StagePresentationDefinition presentation,
            EnemyPresentationCatalog enemyCatalog,
            StaticEntityPresentationCatalog staticCatalog,
            GameplayEntityView enemyViewPrefab,
            GameplayEntityView staticViewPrefab)
        {
            Entry = entry;
            Authoring = authoring;
            Gameplay = gameplay;
            Presentation = presentation;
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
                Placement("enemy-a", StageAuthoringEntityKind.Enemy, 1, 0, "enemy-view"),
                Placement("enemy-b", StageAuthoringEntityKind.Enemy, 2, 0, "enemy-view"),
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
                staticViewPrefab);
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
        }

        private static StagePlacedEntityAuthoring Placement(
            string stableGuid,
            StageAuthoringEntityKind kind,
            int x,
            int y,
            string presentationId = "")
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

        private static string FormatGenerationIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }
    }
}
