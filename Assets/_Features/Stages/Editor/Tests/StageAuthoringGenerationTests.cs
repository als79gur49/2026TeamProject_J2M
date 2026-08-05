using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGenerationTests
    {
        private const string ObjectiveStageRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/Levels/level-01/Stages";

        private static readonly string[] ObjectiveStageNames =
        {
            "legacy-stage-5-1",
            "stage-0-1",
            "stage-0-2",
            "stage-1-1",
            "stage-2-1",
            "stage-2-2",
            "stage-3-1",
            "stage-3-2",
            "stage-4-1",
            "stage-4-2",
        };

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
        public void GeneratedStageDefinitionPreservesObjectiveConditionAuthoringFields()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("box", StageAuthoringEntityKind.Box, 2, 0));
            var condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            SetButtonConditionTileId(condition, 100);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = 100,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                        Kind = TileFeatureKind.Button,
                        ActivationRule = TileFeatureActivationRule.ActiveFaceOnly,
                        Direction = Direction2D.Right,
                        BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                    },
                });
                fixture.Authoring.SetObjective(new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ObjectiveTitle = "Reach the Exit",
                    ObjectiveSummary = "Clear every required condition.",
                    ConditionEntries = new[]
                    {
                        new StageObjectiveConditionEntry
                        {
                            Condition = condition,
                            Required = true,
                            Role = StageObjectiveConditionRole.SecondaryGoal,
                            StableConditionId = "button-100",
                            AuthoringLabel = "Place a push box on the button",
                            SortOrder = 10,
                        },
                    },
                });

                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                var secondReport = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(secondReport.HasErrors, Is.False, FormatIssues(secondReport));
                Assert.That(fixture.Gameplay.Objective.ObjectiveTitle, Is.EqualTo("Reach the Exit"));
                Assert.That(fixture.Gameplay.Objective.ObjectiveSummary, Is.EqualTo("Clear every required condition."));
                var entry = fixture.Gameplay.Objective.ConditionEntries.Single();
                Assert.That(entry.StableConditionId, Is.EqualTo("button-100"));
                Assert.That(entry.AuthoringLabel, Is.EqualTo("Place a push box on the button"));
                Assert.That(entry.SortOrder, Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
                fixture.Destroy();
            }
        }

        [Test]
        public void StageAuthoringDefinitionObjective_PreservesEmptyPrimaryGoalAuthoringLabelForValidation()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                authoring.SetObjective(new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ObjectiveTitle = "Reach the Exit",
                    ObjectiveSummary = "Move to the exit zone.",
                    ConditionEntries = new[]
                    {
                        new StageObjectiveConditionEntry
                        {
                            Required = true,
                            Role = StageObjectiveConditionRole.PrimaryGoal,
                            StableConditionId = "primary-goal",
                            AuthoringLabel = string.Empty,
                            SortOrder = 0,
                        },
                    },
                });

                var objective = authoring.Objective;

                Assert.That(objective.ObjectiveTitle, Is.EqualTo("Reach the Exit"));
                Assert.That(objective.ObjectiveSummary, Is.EqualTo("Move to the exit zone."));
                Assert.That(objective.ConditionEntries.Single().AuthoringLabel, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void GeneratedAssetWriterPreservesObjectiveTitleSummaryAuthoringLabelAndSortOrder()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            SetButtonConditionTileId(condition, 100);
            var payload = new StageAuthoringGameplayWritePayload(
                new StageBoardDefinition
                {
                    MinInclusive = Vector2Int.zero,
                    MaxInclusive = new Vector2Int(2, 2),
                    InitialBottomFace = FaceId.Floor,
                },
                new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ObjectiveTitle = "Reach the Exit",
                    ObjectiveSummary = "Clear every required condition.",
                    ConditionEntries = new[]
                    {
                        new StageObjectiveConditionEntry
                        {
                            Condition = condition,
                            Required = true,
                            Role = StageObjectiveConditionRole.SecondaryGoal,
                            StableConditionId = "button-100",
                            AuthoringLabel = "Place a push box on the button",
                            SortOrder = 10,
                        },
                    },
                },
                Array.Empty<StageZoneDefinition>());

            try
            {
                StageAuthoringGeneratedAssetWriter.ApplyGameplayOutput(stage, payload, recordUndo: false, markDirty: false);

                Assert.That(stage.Objective.ObjectiveTitle, Is.EqualTo("Reach the Exit"));
                Assert.That(stage.Objective.ObjectiveSummary, Is.EqualTo("Clear every required condition."));
                var entry = stage.Objective.ConditionEntries.Single();
                Assert.That(entry.AuthoringLabel, Is.EqualTo("Place a push box on the button"));
                Assert.That(entry.SortOrder, Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void ObjectiveAuthoringLabelSerialization_LegacyDisplayTextTokenLoadsIntoAuthoringLabel()
        {
            var sourcePath = GetGeneratedObjectiveStagePath("stage-0-2");
            var temporaryPath =
                $"Assets/__ObjectiveAuthoringLabelLegacyFixture_{Guid.NewGuid():N}.asset";

            try
            {
                Assert.That(AssetDatabase.CopyAsset(sourcePath, temporaryPath), Is.True);
                var yaml = File.ReadAllText(ToAbsoluteProjectPath(temporaryPath));
                Assert.That(CountSerializedToken(yaml, "AuthoringLabel"), Is.EqualTo(1));

                yaml = yaml.Replace("AuthoringLabel:", "DisplayText:");
                File.WriteAllText(ToAbsoluteProjectPath(temporaryPath), yaml);
                AssetDatabase.ImportAsset(
                    temporaryPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var loaded = AssetDatabase.LoadAssetAtPath<StageDefinition>(temporaryPath);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(
                    loaded.Objective.ConditionEntries.Single().AuthoringLabel,
                    Is.EqualTo("Reach the Exit Zone"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(temporaryPath);
            }
        }

        [Test]
        public void ObjectiveAuthoringLabelAssets_UseCurrentTokenOnExactMigrationInventory()
        {
            var paths = GetObjectiveAssetPaths().ToArray();

            Assert.That(paths, Has.Length.EqualTo(20));
            Assert.That(paths.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(20));

            var oldTokenCount = 0;
            var newTokenCount = 0;
            for (var i = 0; i < paths.Length; i++)
            {
                Assert.That(File.Exists(ToAbsoluteProjectPath(paths[i])), Is.True, paths[i]);
                var yaml = File.ReadAllText(ToAbsoluteProjectPath(paths[i]));
                oldTokenCount += CountSerializedToken(yaml, "DisplayText");
                newTokenCount += CountSerializedToken(yaml, "AuthoringLabel");
            }

            Assert.That(oldTokenCount, Is.Zero);
            Assert.That(newTokenCount, Is.EqualTo(108));
        }

        [Test]
        public void ObjectiveAuthoringLabelAssets_PreservePairIdentityParityAndZeroDrift()
        {
            for (var i = 0; i < ObjectiveStageNames.Length; i++)
            {
                var stageName = ObjectiveStageNames[i];
                var generatedPath = GetGeneratedObjectiveStagePath(stageName);
                var authoringPath = GetAuthoringObjectiveStagePath(stageName);
                var generated = AssetDatabase.LoadAssetAtPath<StageDefinition>(generatedPath);
                var authoring = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(authoringPath);

                Assert.That(generated, Is.Not.Null, generatedPath);
                Assert.That(authoring, Is.Not.Null, authoringPath);
                Assert.That(authoring.GeneratedGameplayDefinition, Is.SameAs(generated), stageName);

                var generatedEntries = generated.Objective.GetConditionEntriesOrEmpty();
                var authoringEntries = authoring.Objective.GetConditionEntriesOrEmpty();
                Assert.That(authoringEntries, Has.Length.EqualTo(generatedEntries.Length), stageName);
                for (var entryIndex = 0; entryIndex < generatedEntries.Length; entryIndex++)
                {
                    var expected = authoringEntries[entryIndex];
                    var actual = generatedEntries[entryIndex];
                    Assert.That(actual.Condition, Is.SameAs(expected.Condition), $"{stageName}[{entryIndex}].Condition");
                    Assert.That(actual.Required, Is.EqualTo(expected.Required), $"{stageName}[{entryIndex}].Required");
                    Assert.That(actual.Role, Is.EqualTo(expected.Role), $"{stageName}[{entryIndex}].Role");
                    Assert.That(actual.StableConditionId, Is.EqualTo(expected.StableConditionId), $"{stageName}[{entryIndex}].StableConditionId");
                    Assert.That(actual.AuthoringLabel, Is.EqualTo(expected.AuthoringLabel), $"{stageName}[{entryIndex}].AuthoringLabel");
                    Assert.That(actual.SortOrder, Is.EqualTo(expected.SortOrder), $"{stageName}[{entryIndex}].SortOrder");
                }

                var issues = StageAuthoringDriftComparer.CompareGameplay(
                    StageAuthoringProjection.ProjectExpectedGameplay(authoring, allocationPlan: null),
                    StageAuthoringProjection.ProjectActualGameplay(generated),
                    new StageAuthoringDriftContext(
                        StageValidationSeverity.Error,
                        StageValidationTiming.TestOrCi,
                        generated,
                        generatedPath,
                        stageName,
                        authoring.name,
                        generated.name));
                Assert.That(issues, Is.Empty, FormatIssues(issues));
            }
        }

        [Test]
        public void StageAuthoringGridWindow_LabelsAuthoringMetadataAsNonPlayerFacing()
        {
            var source = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGridWindow.cs"));

            Assert.That(source, Does.Contain("\"Authoring Label\""));
            Assert.That(source, Does.Contain("Editor-facing label used for authoring, validation, and drift comparison."));
            Assert.That(source, Does.Contain("It is not player-facing localized copy."));
            Assert.That(source, Does.Not.Contain("\"DisplayText\""));
        }

        [Test]
        public void GeneratedStageDefinitionPreservesZoneAuthoring()
        {
            var fixture = CreateFixture(
                Placement("player", StageAuthoringEntityKind.Player, 0, 0),
                Placement("box", StageAuthoringEntityKind.Box, 2, 0));
            var zone = new StageZoneDefinition
            {
                ZoneId = "goal",
                FaceId = FaceId.Front,
                Regions = new[]
                {
                    new StageZoneRegionDefinition
                    {
                        MinInclusive = new Vector2Int(1, 1),
                        MaxInclusive = new Vector2Int(2, 2),
                    },
                },
            };

            try
            {
                fixture.Authoring.SetZones(new[] { zone });

                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Gameplay.Zones.Length, Is.EqualTo(1));
                Assert.That(fixture.Gameplay.Zones[0].ZoneId, Is.EqualTo(zone.ZoneId));
                Assert.That(fixture.Gameplay.Zones[0].FaceId, Is.EqualTo(zone.FaceId));
                Assert.That(fixture.Gameplay.Zones[0].Regions.Single().MinInclusive, Is.EqualTo(zone.Regions.Single().MinInclusive));
                Assert.That(fixture.Gameplay.Zones[0].Regions.Single().MaxInclusive, Is.EqualTo(zone.Regions.Single().MaxInclusive));
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
                SetPresentationString(fixture.Presentation, "displayNameKey", "stage.original.display_name");

                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Presentation.DisplayNameKey, Is.EqualTo("stage.original.display_name"));
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

        private static IEnumerable<string> GetObjectiveAssetPaths()
        {
            for (var i = 0; i < ObjectiveStageNames.Length; i++)
            {
                yield return GetGeneratedObjectiveStagePath(ObjectiveStageNames[i]);
                yield return GetAuthoringObjectiveStagePath(ObjectiveStageNames[i]);
            }
        }

        private static string GetGeneratedObjectiveStagePath(string stageName)
        {
            return $"{ObjectiveStageRoot}/{stageName}/{stageName}.asset";
        }

        private static string GetAuthoringObjectiveStagePath(string stageName)
        {
            return $"{ObjectiveStageRoot}/{stageName}/{stageName}_Authoring.asset";
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static int CountSerializedToken(string yaml, string token)
        {
            return yaml
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.TrimStart().StartsWith(token + ":", StringComparison.Ordinal));
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
            var enemyProfile = AssignDefaultEnemyProfile(placements);
            authoring.SetPlacements(placements);
            authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());

            var enemyCatalog = CreateEnemyCatalog(enemyPresentationIds);
            var staticCatalog = CreateStaticCatalog(staticPresentationIds);
            SetPresentationCatalogs(presentation, enemyCatalog, staticCatalog);
            return new StageAuthoringFixture(authoring, gameplay, presentation, enemyCatalog, staticCatalog, enemyProfile);
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

        private static EnemyAiProfile AssignDefaultEnemyProfile(IReadOnlyList<StagePlacedEntityAuthoring> placements)
        {
            EnemyAiProfile profile = null;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null ||
                    placement.Kind != StageAuthoringEntityKind.Enemy ||
                    placement.EnemyAiMode == EnemyAiMode.None ||
                    placement.EnemyAiProfileOverride != null)
                {
                    continue;
                }

                profile ??= ScriptableObject.CreateInstance<EnemyAiProfile>();
                placement.EnemyAiProfileOverride = profile;
            }

            return profile;
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

        private static void SetButtonConditionTileId(ButtonActivatedConditionAsset condition, int tileId)
        {
            var serializedObject = new SerializedObject(condition);
            serializedObject.FindProperty("tileId").intValue = tileId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string FormatIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }

        private static string FormatIssues(IEnumerable<StageValidationIssue> issues)
        {
            return string.Join(
                Environment.NewLine,
                issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }

        private sealed class StageAuthoringFixture
        {
            private readonly UnityEngine.Object[] ownedObjects;
            private readonly EnemyAiProfile enemyProfile;

            public StageAuthoringFixture(
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StagePresentationDefinition presentation,
                EnemyPresentationCatalog enemyCatalog,
                StaticEntityPresentationCatalog staticCatalog,
                EnemyAiProfile enemyProfile)
            {
                Authoring = authoring;
                Gameplay = gameplay;
                Presentation = presentation;
                this.enemyProfile = enemyProfile;
                ownedObjects = new UnityEngine.Object[]
                {
                    authoring,
                    gameplay,
                    presentation,
                    enemyCatalog,
                    staticCatalog,
                };
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

                UnityEngine.Object.DestroyImmediate(enemyProfile);
            }
        }
    }
}
