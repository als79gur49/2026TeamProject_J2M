using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGenerationTests
    {
        [Test]
        public void StableIdPreservedWhenPlacementReordered()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0),
                Placement("box", StageAuthoringEntityKind.Box, 2, 0));

            try
            {
                var firstReport = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(firstReport.HasErrors, Is.False, FormatIssues(firstReport));
                var idsBefore = fixture.Authoring.EntityIdMappings.ToDictionary(mapping => mapping.StableGuid, mapping => mapping.EntityId);

                fixture.Authoring.SetPlacements(fixture.Authoring.Placements.Reverse());
                var secondReport = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(secondReport.HasErrors, Is.False, FormatIssues(secondReport));

                foreach (var mapping in fixture.Authoring.EntityIdMappings.Where(mapping => !mapping.Retired))
                {
                    Assert.That(mapping.EntityId, Is.EqualTo(idsBefore[mapping.StableGuid]));
                }
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GeneratedStageDefinitionHasUniquePositiveEntityIds()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0),
                Placement("box", StageAuthoringEntityKind.Box, 2, 0),
                Placement("wall", StageAuthoringEntityKind.Wall, 3, 0));

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                var ids = fixture.Gameplay.Spawns.Select(spawn => spawn.EntityId).ToArray();
                Assert.That(ids, Is.All.GreaterThan(0));
                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GeneratedStageDefinitionBuildsRuntimeSeed()
        {
            var player = Placement("player", StageAuthoringEntityKind.Player, 0, 0);
            var fixture = CreateFixture(
                player,
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0));

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                var build = StageRuntimeBuilder.Build(fixture.Gameplay);
                var playerId = fixture.Authoring.EntityIdMappings.Single(mapping => mapping.StableGuid == player.StableGuid).EntityId;
                Assert.That(build.PlayerEntityId, Is.EqualTo(playerId));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringGeneration_PlayerMobility_IsWrittenToGeneratedStageDefinition()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0, unitMobilityKind: UnitMobilityKind.Air),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0));

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Gameplay.PlayerSpawns[0].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringGeneration_EnemyMobility_IsWrittenToGeneratedStageDefinition()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0, unitMobilityKind: UnitMobilityKind.Air));

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Gameplay.EnemySpawns[0].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringGeneration_MissingMobility_DefaultsToGround()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0));

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Gameplay.PlayerSpawns[0].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
                Assert.That(fixture.Gameplay.EnemySpawns[0].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringGeneration_NonUnitMobility_IsNormalizedToGround()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("box", StageAuthoringEntityKind.Box, 1, 0, unitMobilityKind: UnitMobilityKind.Air),
                Placement("wall", StageAuthoringEntityKind.Wall, 2, 0, unitMobilityKind: UnitMobilityKind.Air));

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Gameplay.PlayerSpawns[0].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
                Assert.That(fixture.Gameplay.BoxSpawns[0].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
                Assert.That(fixture.Gameplay.WallSpawns[0].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GeneratedStageDefinitionPreservesTileFeatureAuthoring()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("box", StageAuthoringEntityKind.Box, 2, 0));
            var tileFeature = new StageTileFeatureDefinition
            {
                TileId = 100,
                Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                Kind = TileFeatureKind.Button,
                ActivationRule = TileFeatureActivationRule.ActiveFaceOnly,
                Direction = Direction2D.Left,
                BoxSelector = TileFeatureBoxSelector.BoundEntity,
                BoundEntityId = 2,
                PresentationKey = "button-a",
            };

            try
            {
                fixture.Authoring.SetTileFeatures(new[] { tileFeature });

                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Gameplay.TileFeatures.Length, Is.EqualTo(1));
                Assert.That(fixture.Gameplay.TileFeatures[0].TileId, Is.EqualTo(tileFeature.TileId));
                Assert.That(fixture.Gameplay.TileFeatures[0].Cell, Is.EqualTo(tileFeature.Cell));
                Assert.That(fixture.Gameplay.TileFeatures[0].Kind, Is.EqualTo(tileFeature.Kind));
                Assert.That(fixture.Gameplay.TileFeatures[0].ActivationRule, Is.EqualTo(tileFeature.ActivationRule));
                Assert.That(fixture.Gameplay.TileFeatures[0].Direction, Is.EqualTo(tileFeature.Direction));
                Assert.That(fixture.Gameplay.TileFeatures[0].BoxSelector, Is.EqualTo(tileFeature.BoxSelector));
                Assert.That(fixture.Gameplay.TileFeatures[0].BoundEntityId, Is.EqualTo(tileFeature.BoundEntityId));
                Assert.That(fixture.Gameplay.TileFeatures[0].PresentationKey, Is.EqualTo(tileFeature.PresentationKey));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void PresentationBindingsUseGeneratedEntityIds()
        {
            var enemy = Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0, presentationId: "enemy-view");
            var box = Placement("box", StageAuthoringEntityKind.Box, 2, 0, presentationId: "box-view");
            var wall = Placement("wall", StageAuthoringEntityKind.Wall, 3, 0, presentationId: "wall-view");
            var fixture = CreateFixture(
                new[] { "enemy-view" },
                new[] { "box-view", "wall-view" },
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                enemy,
                box,
                wall);

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                var idsByGuid = fixture.Authoring.EntityIdMappings.ToDictionary(mapping => mapping.StableGuid, mapping => mapping.EntityId);

                Assert.That(fixture.Presentation.EnemyPresentationBindings.Single().EntityId, Is.EqualTo(idsByGuid[enemy.StableGuid]));
                CollectionAssert.AreEquivalent(
                    new[] { idsByGuid[box.StableGuid], idsByGuid[wall.StableGuid] },
                    fixture.Presentation.StaticEntityPresentationBindings.Select(binding => binding.EntityId).ToArray());
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GeneratedPresentationBindings_DoNotClearTileFeatureVisualBindings()
        {
            var fixture = CreateFixture(
                new[] { "enemy-view" },
                Array.Empty<string>(),
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0, presentationId: "enemy-view"));
            var prefab = new GameObject("ButtonTileVisualPrefab");
            prefab.AddComponent<TileFeatureVisualTargetView>();

            try
            {
                SetTileFeaturePresentationBindings(
                    fixture.Presentation,
                    new[]
                    {
                        new TileFeaturePresentationBinding
                        {
                            TileId = 100,
                            VisualPrefab = prefab,
                        },
                    });

                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));

                Assert.That(fixture.Presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(100));
                Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                fixture.Destroy();
            }
        }

        [Test]
        public void GenerateGameplay_DoesNotMutateTileFeatureVisualBindings()
        {
            var fixture = CreateFixture(
                new[] { "enemy-view" },
                Array.Empty<string>(),
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0, presentationId: "enemy-view"));
            var prefab = new GameObject("ButtonTileVisualPrefab");
            prefab.AddComponent<TileFeatureVisualTargetView>();

            try
            {
                SetTileFeaturePresentationBindings(
                    fixture.Presentation,
                    new[]
                    {
                        new TileFeaturePresentationBinding
                        {
                            TileId = 100,
                            VisualPrefab = prefab,
                        },
                    });

                var report = StageAuthoringGenerator.Generate(
                    fixture.Authoring,
                    new StageAuthoringGenerateOptions
                    {
                        DryRun = false,
                        WriteGameplay = true,
                        WritePresentationBindings = false,
                        ValidateAfterGenerate = true,
                    });
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));

                Assert.That(fixture.Presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(100));
                Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                fixture.Destroy();
            }
        }

        [Test]
        public void GenerateDoesNotMutatePresentationMetadata()
        {
            var fixture = CreateFixture(
                new[] { "enemy-view" },
                Array.Empty<string>(),
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 1, 0, presentationId: "enemy-view"));

            try
            {
                SetPresentationString(fixture.Presentation, "displayName", "Original Display");
                SetPresentationString(fixture.Presentation, "resultTitle", "Original Result");

                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Presentation.DisplayName, Is.EqualTo("Original Display"));
                Assert.That(fixture.Presentation.ResultTitle, Is.EqualTo("Original Result"));
                Assert.That(fixture.Presentation.EnemyPresentationBindings.Length, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void DuplicateStableGuidFailsValidation()
        {
            var fixture = CreateFixture(
                Placement("dup", StageAuthoringEntityKind.Player, 0, 0),
                Placement("dup", StageAuthoringEntityKind.Enemy, 1, 0));

            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.stable-guid.duplicate"), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void DuplicateCellFailsUnlessUnitStackOptIn()
        {
            var invalid = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 0, 0));
            var valid = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0, unitStackGroup: "stack"),
                Placement("enemy", StageAuthoringEntityKind.Enemy, 0, 0, unitStackGroup: "stack"));

            try
            {
                var invalidReport = StageAuthoringGenerator.Generate(invalid.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(invalidReport.HasErrors, Is.True);
                Assert.That(invalidReport.Issues.Any(issue => issue.Code == "authoring.generated-gameplay.invalid"), Is.True);

                var validReport = StageAuthoringGenerator.Generate(valid.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(validReport.HasErrors, Is.False, FormatIssues(validReport));
            }
            finally
            {
                invalid.Destroy();
                valid.Destroy();
            }
        }

        [Test]
        public void NoSceneAsAuthoritativeSource()
        {
            var authoringEditorRoot = Path.GetFullPath("Assets/_Features/Stages/Editor/Authoring");
            var forbiddenHits = Directory
                .GetFiles(authoringEditorRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("FindObjectsOfType") ||
                           source.Contains("GameObject.Find") ||
                           source.Contains("SceneManager.GetActiveScene");
                })
                .ToArray();

            Assert.That(forbiddenHits, Is.Empty);
        }

        private static StageAuthoringFixture CreateFixture(params StagePlacedEntityAuthoring[] placements)
        {
            return CreateFixture(Array.Empty<string>(), Array.Empty<string>(), placements);
        }

        private static StageAuthoringFixture CreateFixture(
            string[] enemyPresentationIds,
            string[] staticPresentationIds,
            params StagePlacedEntityAuthoring[] placements)
        {
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            gameplay.name = "GeneratedGameplay";
            presentation.name = "GeneratedPresentation";
            authoring.name = "Authoring";
            authoring.AssignGeneratedDefinitions(gameplay, presentation);
            authoring.SetBoard(new StageBoardDefinition
            {
                MinInclusive = new Vector2Int(0, 0),
                MaxInclusive = new Vector2Int(4, 4),
                InitialBottomFace = FaceId.Floor,
            });
            authoring.SetPlacements(placements);
            authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());

            var enemyCatalog = CreateEnemyCatalog(enemyPresentationIds);
            var staticCatalog = CreateStaticCatalog(staticPresentationIds);
            SetPresentationCatalogs(presentation, enemyCatalog, staticCatalog);
            return new StageAuthoringFixture(authoring, gameplay, presentation, enemyCatalog, staticCatalog);
        }

        private static StagePlacedEntityAuthoring Placement(
            string stableGuid,
            StageAuthoringEntityKind kind,
            int x,
            int y,
            string presentationId = "",
            string unitStackGroup = "",
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = stableGuid,
                DisplayName = stableGuid,
                Kind = kind,
                Cell = new SurfaceCell(FaceId.Floor, x, y),
                Facing = Direction.Right,
                Hp = 1,
                UnitMobilityKind = unitMobilityKind,
                UnitStackGroup = unitStackGroup,
                BoxCapabilities = BoxCapabilities.Push,
                EnemyAiMode = kind == StageAuthoringEntityKind.Enemy ? EnemyAiMode.Patrol : EnemyAiMode.None,
                PresentationId = presentationId,
            };
        }

        private static EnemyPresentationCatalog CreateEnemyCatalog(IReadOnlyList<string> ids)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            var serializedObject = new SerializedObject(catalog);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = ids.Count;
            for (var i = 0; i < ids.Count; i++)
            {
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("PresentationId").stringValue = ids[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static StaticEntityPresentationCatalog CreateStaticCatalog(IReadOnlyList<string> ids)
        {
            var catalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();
            var serializedObject = new SerializedObject(catalog);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = ids.Count;
            for (var i = 0; i < ids.Count; i++)
            {
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("PresentationId").stringValue = ids[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
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

        private static void SetPresentationString(
            StagePresentationDefinition presentation,
            string fieldName,
            string value)
        {
            var serializedObject = new SerializedObject(presentation);
            serializedObject.FindProperty(fieldName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetTileFeaturePresentationBindings(
            StagePresentationDefinition presentation,
            IReadOnlyList<TileFeaturePresentationBinding> bindings)
        {
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("tileFeaturePresentationBindings");
            property.arraySize = bindings.Count;
            for (var i = 0; i < bindings.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("TileId").intValue = bindings[i].TileId;
                element.FindPropertyRelative("VisualPrefab").objectReferenceValue = bindings[i].VisualPrefab;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string FormatIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }

        private sealed class StageAuthoringFixture
        {
            private readonly UnityEngine.Object[] ownedObjects;

            public StageAuthoringFixture(
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StagePresentationDefinition presentation,
                EnemyPresentationCatalog enemyCatalog,
                StaticEntityPresentationCatalog staticCatalog)
            {
                Authoring = authoring;
                Gameplay = gameplay;
                Presentation = presentation;
                ownedObjects = new UnityEngine.Object[] { authoring, gameplay, presentation, enemyCatalog, staticCatalog };
            }

            public StageAuthoringDefinition Authoring { get; }

            public StageDefinition Gameplay { get; }

            public StagePresentationDefinition Presentation { get; }

            public void Destroy()
            {
                for (var i = 0; i < ownedObjects.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }
        }
    }
}
