using System;
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
    public sealed class StageAuthoringExitGoalHelperCommandTests
    {
        [Test]
        public void Status_NoSelectedExit_ReturnsNoExitSelected()
        {
            WithAuthoring(authoring =>
            {
                StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(authoring, 0, out var status);

                Assert.That(status.Kind, Is.EqualTo(ExitGoalZoneStatusKind.NoExitSelected));
                Assert.That(status.CanSync, Is.False);
            });
        }

        [Test]
        public void Status_SelectedNonExit_ReturnsSelectedTileFeatureIsNotExit()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[] { TileFeature(1, TileFeatureKind.Button, Cell(0, 0)) });

                StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(authoring, 1, out var status);

                Assert.That(status.Kind, Is.EqualTo(ExitGoalZoneStatusKind.SelectedTileFeatureIsNotExit));
                Assert.That(status.CanSync, Is.False);
            });
        }

        [Test]
        public void Status_ObjectiveDisabled_ReturnsObjectiveDisabled()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());

                AssertStatus(authoring, ExitGoalZoneStatusKind.ObjectiveDisabled, canSync: false);
            });
        }

        [Test]
        public void Status_CompletionPolicyUnsupported_ReturnsCompletionPolicyUnsupported()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective((StageCompletionPolicy)99, PrimaryGoal(CreatePlayerAtAnyZone("goal"))));

                AssertStatus(authoring, ExitGoalZoneStatusKind.CompletionPolicyUnsupported, canSync: false);
            });
        }

        [Test]
        public void Status_MissingPrimaryGoal_ReturnsMissingPrimaryGoal()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(StageCompletionPolicy.RequireAllConditions));

                AssertStatus(authoring, ExitGoalZoneStatusKind.MissingPrimaryGoal, canSync: false);
            });
        }

        [Test]
        public void Status_MultiplePrimaryGoals_ReturnsMultiplePrimaryGoals()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal")),
                    PrimaryGoal(CreatePlayerAtAnyZone("goal-2"))));

                AssertStatus(authoring, ExitGoalZoneStatusKind.MultiplePrimaryGoals, canSync: false);
            });
        }

        [Test]
        public void Status_PrimaryGoalNotPlayerAtAnyZone_ReturnsPrimaryGoalNotPlayerAtAnyZone()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreateButtonActivatedCondition(10))));

                AssertStatus(authoring, ExitGoalZoneStatusKind.PrimaryGoalNotPlayerAtAnyZone, canSync: false);
            });
        }

        [Test]
        public void Status_PrimaryGoalReferencesNoZone_ReturnsPrimaryGoalReferencesNoZone()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone(Array.Empty<string>()))));

                AssertStatus(authoring, ExitGoalZoneStatusKind.PrimaryGoalReferencesNoZone, canSync: false);
            });
        }

        [Test]
        public void Status_PrimaryGoalReferencesMultipleZones_ReturnsPrimaryGoalReferencesMultipleZones()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal", "other"))));

                AssertStatus(authoring, ExitGoalZoneStatusKind.PrimaryGoalReferencesMultipleZones, canSync: false);
            });
        }

        [Test]
        public void Status_ReferencedZoneMissing_ReturnsReferencedZoneMissingAndCanSync()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));

                AssertStatus(authoring, ExitGoalZoneStatusKind.ReferencedZoneMissing, canSync: true);
            });
        }

        [Test]
        public void Status_ReferencedZoneShared_ReturnsReferencedZoneShared()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal")),
                    Condition(CreatePlayerAtAnyZone("goal"), required: false, StageObjectiveConditionRole.None, "shared")));
                authoring.SetZones(new[] { Zone("goal", Cell(1, 1)) });

                AssertStatus(authoring, ExitGoalZoneStatusKind.ReferencedZoneShared, canSync: false);
            });
        }

        [Test]
        public void Status_ZoneNotSingleCell_ReturnsZoneNotSingleCellAndCanSync()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                authoring.SetZones(new[]
                {
                    new StageZoneDefinition
                    {
                        ZoneId = "goal",
                        FaceId = FaceId.Floor,
                        Regions = new[]
                        {
                            new StageZoneRegionDefinition
                            {
                                MinInclusive = new Vector2Int(0, 0),
                                MaxInclusive = new Vector2Int(1, 1),
                            },
                        },
                    },
                });

                AssertStatus(authoring, ExitGoalZoneStatusKind.ZoneNotSingleCell, canSync: true);
            });
        }

        [Test]
        public void Status_ZoneCellMismatch_ReturnsZoneCellMismatchAndCanSync()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                authoring.SetZones(new[] { Zone("goal", Cell(2, 1)) });

                StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(authoring, 1, out var status);

                Assert.That(status.Kind, Is.EqualTo(ExitGoalZoneStatusKind.ZoneCellMismatch));
                Assert.That(status.CanSync, Is.True);
                Assert.That(status.HasCurrentZoneCell, Is.True);
                Assert.That(status.CurrentZoneCell, Is.EqualTo(Cell(2, 1)));
            });
        }

        [Test]
        public void Status_ValidExactMatch_ReturnsValid()
        {
            WithSyncedExitAuthoring(authoring =>
            {
                AssertStatus(authoring, ExitGoalZoneStatusKind.Valid, canSync: false);
            });
        }

        [Test]
        public void Sync_UpdatesExistingPrimaryGoalZoneToExitCenter()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                authoring.SetZones(new[] { Zone("goal", Cell(2, 1)) });

                var changed = StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(authoring.Zones.Single().FaceId, Is.EqualTo(FaceId.Floor));
                Assert.That(authoring.Zones.Single().Regions.Single().MinInclusive, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(authoring.Zones.Single().Regions.Single().MaxInclusive, Is.EqualTo(new Vector2Int(1, 1)));
            });
        }

        [Test]
        public void Sync_CreatesMissingReferencedZone()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));

                var changed = StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(authoring.Zones.Single().ZoneId, Is.EqualTo("goal"));
                Assert.That(authoring.Zones.Single().FaceId, Is.EqualTo(FaceId.Floor));
                Assert.That(authoring.Zones.Single().Regions.Single().MinInclusive, Is.EqualTo(new Vector2Int(1, 1)));
            });
        }

        [Test]
        public void Sync_PreservesExitTileFeatureData()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                var before = authoring.TileFeatures.Single();

                Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.True);

                Assert.That(authoring.TileFeatures.Single(), Is.EqualTo(before));
            });
        }

        [Test]
        public void Sync_PreservesVisualBindingData()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = new GameObject("ExitVisualPrefab");
            prefab.AddComponent<TileFeatureVisualTargetView>();
            try
            {
                WithExitAuthoring(authoring =>
                {
                    authoring.AssignGeneratedDefinitions(null, presentation);
                    SetTileFeaturePresentationBindings(
                        presentation,
                        new[]
                        {
                            new TileFeaturePresentationBinding
                            {
                                TileId = 1,
                                VisualPrefab = prefab,
                            },
                        });
                    authoring.SetObjective(Objective(
                        StageCompletionPolicy.RequireAllConditions,
                        PrimaryGoal(CreatePlayerAtAnyZone("goal"))));

                    Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.True);

                    Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                    Assert.That(presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(1));
                    Assert.That(presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void Sync_MarksAuthoringDirty()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                EditorUtility.ClearDirty(authoring);

                Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.True);

                Assert.That(EditorUtility.IsDirty(authoring), Is.True);
            });
        }

        [Test]
        public void Sync_RejectsUnsafeInputs()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[] { TileFeature(1, TileFeatureKind.Button, Cell(1, 1)) });
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));

                Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.False);
            });

            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());

                Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.False);
            });

            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreateButtonActivatedCondition(10))));

                Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.False);
            });

            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal", "other"))));

                Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.False);
            });

            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal")),
                    Condition(CreatePlayerAtAnyZone("goal"), false, StageObjectiveConditionRole.None, "shared")));

                Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out _), Is.False);
            });
        }

        [Test]
        public void GridWindow_SelectedExitStatusAndSync_AreAvailableForTests()
        {
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                WithExitAuthoring(authoring =>
                {
                    authoring.SetObjective(Objective(
                        StageCompletionPolicy.RequireAllConditions,
                        PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                    window.BindForTests(authoring);
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);

                    Assert.That(window.GetSelectedExitGoalZoneStatusForTests().Kind, Is.EqualTo(ExitGoalZoneStatusKind.ReferencedZoneMissing));
                    Assert.That(window.SyncSelectedExitGoalZoneForTests(out var error), Is.True, error);
                    Assert.That(window.GetSelectedExitGoalZoneStatusForTests().Kind, Is.EqualTo(ExitGoalZoneStatusKind.Valid));
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Generation_SyncedAuthoringPassesValidatorAndPreservesVisualBindings()
        {
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = new GameObject("ExitVisualPrefab");
            prefab.AddComponent<TileFeatureVisualTargetView>();
            try
            {
                WithExitAuthoring(authoring =>
                {
                    authoring.AssignGeneratedDefinitions(gameplay, presentation);
                    authoring.SetPlacements(new[]
                    {
                        new StagePlacedEntityAuthoring
                        {
                            StableGuid = "player",
                            DisplayName = "Player",
                            Kind = StageAuthoringEntityKind.Player,
                            Cell = Cell(0, 0),
                            Facing = Direction.Right,
                            Hp = 1,
                            BoxCapabilities = BoxCapabilities.None,
                        },
                    });
                    authoring.SetObjective(Objective(
                        StageCompletionPolicy.RequireAllConditions,
                        PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                    SetTileFeaturePresentationBindings(
                        presentation,
                        new[]
                        {
                            new TileFeaturePresentationBinding
                            {
                                TileId = 1,
                                VisualPrefab = prefab,
                            },
                        });

                    Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(authoring, 1, out var syncError), Is.True, syncError);

                    var report = StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.WriteAll);

                    Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                    Assert.DoesNotThrow(() => StageDefinitionValidator.Validate(gameplay));
                    Assert.That(gameplay.TileFeatures.Single().Cell, Is.EqualTo(Cell(1, 1)));
                    Assert.That(gameplay.Zones.Single().FaceId, Is.EqualTo(FaceId.Floor));
                    Assert.That(gameplay.Zones.Single().Regions.Single().MinInclusive, Is.EqualTo(new Vector2Int(1, 1)));
                    Assert.That(presentation.TileFeaturePresentationBindings.Single().VisualPrefab, Is.SameAs(prefab));
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(gameplay);
            }
        }

        private static void AssertStatus(
            StageAuthoringDefinition authoring,
            ExitGoalZoneStatusKind expectedKind,
            bool canSync)
        {
            StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(authoring, 1, out var status);

            Assert.That(status.Kind, Is.EqualTo(expectedKind));
            Assert.That(status.CanSync, Is.EqualTo(canSync));
        }

        private static void WithSyncedExitAuthoring(Action<StageAuthoringDefinition> action)
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    PrimaryGoal(CreatePlayerAtAnyZone("goal"))));
                authoring.SetZones(new[] { Zone("goal", Cell(1, 1)) });
                action(authoring);
            });
        }

        private static void WithExitAuthoring(Action<StageAuthoringDefinition> action)
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[] { TileFeature(1, TileFeatureKind.Exit, Cell(1, 1)) });
                action(authoring);
            });
        }

        private static void WithAuthoring(Action<StageAuthoringDefinition> action)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(3, 3),
                    InitialBottomFace = FaceId.Floor,
                });
                action(authoring);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        private static StageTileFeatureDefinition TileFeature(
            int tileId,
            TileFeatureKind kind,
            SurfaceCell cell)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Kind = kind,
                Cell = cell,
                ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                Direction = Direction2D.None,
                BoxSelector = kind == TileFeatureKind.Button
                    ? TileFeatureBoxSelector.AnyPushableBox
                    : TileFeatureBoxSelector.None,
                BoundEntityId = 0,
                PresentationKey = string.Empty,
            };
        }

        private static StageZoneDefinition Zone(string zoneId, SurfaceCell cell)
        {
            return new StageZoneDefinition
            {
                ZoneId = zoneId,
                FaceId = cell.face,
                Regions = new[]
                {
                    new StageZoneRegionDefinition
                    {
                        MinInclusive = new Vector2Int(cell.x, cell.y),
                        MaxInclusive = new Vector2Int(cell.x, cell.y),
                    },
                },
            };
        }

        private static StageObjectiveAuthoring Objective(
            StageCompletionPolicy policy,
            params StageObjectiveConditionEntry[] entries)
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = policy,
                ObjectiveTitle = string.Empty,
                ObjectiveSummary = string.Empty,
                ConditionEntries = entries,
            };
        }

        private static StageObjectiveConditionEntry PrimaryGoal(StageConditionAsset condition)
        {
            return Condition(condition, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary-goal");
        }

        private static StageObjectiveConditionEntry Condition(
            StageConditionAsset condition,
            bool required,
            StageObjectiveConditionRole role,
            string stableId)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = required,
                Role = role,
                StableConditionId = stableId,
                DisplayText = string.Empty,
                SortOrder = 0,
            };
        }

        private static PlayerAtAnyZoneConditionAsset CreatePlayerAtAnyZone(params string[] zoneIds)
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            var serializedObject = new SerializedObject(condition);
            var property = serializedObject.FindProperty("zoneIds");
            property.arraySize = zoneIds.Length;
            for (var i = 0; i < zoneIds.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = zoneIds[i];
            }

            serializedObject.FindProperty("requireAlive").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return condition;
        }

        private static ButtonActivatedConditionAsset CreateButtonActivatedCondition(int tileId)
        {
            var condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            var serializedObject = new SerializedObject(condition);
            serializedObject.FindProperty("tileId").intValue = tileId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return condition;
        }

        private static void SetTileFeaturePresentationBindings(
            StagePresentationDefinition presentation,
            TileFeaturePresentationBinding[] bindings)
        {
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("tileFeaturePresentationBindings");
            property.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("TileId").intValue = bindings[i].TileId;
                element.FindPropertyRelative("VisualPrefab").objectReferenceValue = bindings[i].VisualPrefab;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SurfaceCell Cell(int x, int y)
        {
            return new SurfaceCell(FaceId.Floor, x, y);
        }

        private static string FormatIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }
    }
}
