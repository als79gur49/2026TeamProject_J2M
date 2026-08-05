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
        public void ObjectiveHelper_EnableObjective_IsExplicit()
        {
            WithExitAuthoring(authoring =>
            {
                authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());

                var changed = StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(authoring, 1, out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(authoring.Objective.CompletionPolicy, Is.EqualTo(StageCompletionPolicy.RequireAllConditions));
                Assert.That(authoring.Objective.ConditionEntries, Is.Empty);
                Assert.That(authoring.Zones, Is.Empty);
            });
        }

        [Test]
        public void ObjectiveHelper_CreatePrimaryGoal_UsesPlayerAtAnyZone()
        {
            using var fixture = TempStageContentFixture.Create();

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            Assert.That(
                StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                    fixture.Authoring,
                    1,
                    out var condition,
                    out var createError),
                Is.True,
                createError);

            var entry = fixture.Authoring.Objective.ConditionEntries.Single();
            Assert.That(condition, Is.Not.Null);
            Assert.That(entry.Condition, Is.SameAs(condition));
            Assert.That(entry.Condition, Is.TypeOf<PlayerAtAnyZoneConditionAsset>());
            Assert.That(entry.Required, Is.True);
            Assert.That(entry.Role, Is.EqualTo(StageObjectiveConditionRole.PrimaryGoal));
            Assert.That(entry.StableConditionId, Is.EqualTo("primary-goal"));
            Assert.That(entry.AuthoringLabel, Is.EqualTo("Reach the Exit Zone"));
            Assert.That(entry.SortOrder, Is.Zero);
        }

        [Test]
        public void ObjectiveHelper_CreatePrimaryGoal_CreatesConditionAssetAtExpectedPath()
        {
            using var fixture = TempStageContentFixture.Create();

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            Assert.That(
                StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                    fixture.Authoring,
                    1,
                    out var condition,
                    out var createError),
                Is.True,
                createError);

            Assert.That(AssetDatabase.GetAssetPath(condition), Is.EqualTo(fixture.ExpectedConditionPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<PlayerAtAnyZoneConditionAsset>(fixture.ExpectedConditionPath), Is.SameAs(condition));
            Assert.That(
                condition.name,
                Is.EqualTo(System.IO.Path.GetFileNameWithoutExtension(fixture.ExpectedConditionPath)));
        }

        [Test]
        public void ObjectiveHelper_CreatePrimaryGoal_ReusesExpectedPathAsset()
        {
            using var fixture = TempStageContentFixture.Create();
            var existing = fixture.CreateExpectedPrimaryGoalCondition("goal");

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            Assert.That(
                StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                    fixture.Authoring,
                    1,
                    out var condition,
                    out var createError),
                Is.True,
                createError);

            Assert.That(condition, Is.SameAs(existing));
            Assert.That(condition.ZoneIds, Is.EqualTo(new[] { "goal" }));
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Single().Condition, Is.SameAs(existing));
        }

        [Test]
        public void ObjectiveHelper_CreatePrimaryGoal_SetsZoneId()
        {
            using var fixture = TempStageContentFixture.Create();

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            Assert.That(
                StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                    fixture.Authoring,
                    1,
                    out var condition,
                    out var createError),
                Is.True,
                createError);

            Assert.That(condition.ZoneIds, Is.EqualTo(new[] { "exit" }));
        }

        [Test]
        public void ObjectiveHelper_CreatePrimaryGoal_DoesNotOverwriteExistingAsset()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.EnsureConditionsFolder();
            var existing = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            existing.name = "ExistingNonConditionAsset";
            AssetDatabase.CreateAsset(existing, fixture.ExpectedConditionPath);

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            var changed = StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                fixture.Authoring,
                1,
                out var condition,
                out var error);

            Assert.That(changed, Is.False);
            Assert.That(condition, Is.Null);
            Assert.That(error, Does.Contain("already contains"));
            Assert.That(AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(fixture.ExpectedConditionPath), Is.SameAs(existing));
            Assert.That(AssetDatabase.LoadAssetAtPath<PlayerAtAnyZoneConditionAsset>(fixture.ExpectedConditionPath), Is.Null);
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);
        }

        [Test]
        public void ObjectiveHelper_RejectsSharedConditionAsset()
        {
            using var first = TempStageContentFixture.Create();
            using var second = TempStageContentFixture.Create();
            var sharedCondition = second.CreateExpectedPrimaryGoalCondition("exit");
            first.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                PrimaryGoal(sharedCondition)));
            EditorUtility.SetDirty(first.Authoring);
            AssetDatabase.SaveAssets();

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(second.Authoring, 1, out var enableError), Is.True, enableError);
            var changed = StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                second.Authoring,
                1,
                out var condition,
                out var error);

            Assert.That(changed, Is.False);
            Assert.That(condition, Is.Null);
            Assert.That(error, Does.Contain("referenced by another StageContentEntry"));
            Assert.That(second.Authoring.Objective.ConditionEntries, Is.Empty);
        }

        [Test]
        public void ObjectiveHelper_RepairExitContract_GeneratedStageDefinitionValidates()
        {
            using var fixture = TempStageContentFixture.Create(withGeneratedCompanions: true);
            fixture.Authoring.SetPlacements(new[]
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

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            Assert.That(
                StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                    fixture.Authoring,
                    1,
                    out _,
                    out var createError),
                Is.True,
                createError);
            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(fixture.Authoring, 1, out var syncError), Is.True, syncError);

            var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);

            Assert.That(report.HasErrors, Is.False, FormatIssues(report));
            Assert.DoesNotThrow(() => StageDefinitionValidator.Validate(fixture.Gameplay));
            Assert.That(fixture.Gameplay.Objective.ConditionEntries.Single().Condition, Is.TypeOf<PlayerAtAnyZoneConditionAsset>());
            Assert.That(fixture.Gameplay.Zones.Single().ZoneId, Is.EqualTo("exit"));
        }

        [Test]
        public void ObjectiveHelper_DoesNotTouchStagePresentationDefinition()
        {
            using var fixture = TempStageContentFixture.Create(withGeneratedCompanions: true);
            var prefab = fixture.CreateTileFeatureVisualPrefab();
            SetTileFeaturePresentationBindings(
                fixture.Presentation,
                new[]
                {
                    new TileFeaturePresentationBinding
                    {
                        TileId = 1,
                        VisualPrefab = prefab,
                    },
                });

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            Assert.That(
                StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                    fixture.Authoring,
                    1,
                    out _,
                    out var createError),
                Is.True,
                createError);

            Assert.That(fixture.Presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
            Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(1));
            Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
        }

        [Test]
        public void ObjectiveHelper_PreservesTileFeatureVisualBindings()
        {
            using var fixture = TempStageContentFixture.Create(withGeneratedCompanions: true);
            var prefab = fixture.CreateTileFeatureVisualPrefab();
            SetTileFeaturePresentationBindings(
                fixture.Presentation,
                new[]
                {
                    new TileFeaturePresentationBinding
                    {
                        TileId = 1,
                        VisualPrefab = prefab,
                    },
                });

            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(fixture.Authoring, 1, out var enableError), Is.True, enableError);
            Assert.That(
                StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                    fixture.Authoring,
                    1,
                    out _,
                    out var createError),
                Is.True,
                createError);
            Assert.That(StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(fixture.Authoring, 1, out var syncError), Is.True, syncError);

            Assert.That(fixture.Presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
            Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(1));
            Assert.That(fixture.Presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
        }

        [Test]
        public void ObjectiveHelper_GridWindowCreateFlow_IsAvailableForTests()
        {
            using var fixture = TempStageContentFixture.Create();
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(fixture.Authoring);
                window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                window.SelectTileFeatureByIdForTests(1);

                Assert.That(window.EnableSelectedExitObjectiveForTests(out var enableError), Is.True, enableError);
                Assert.That(window.CreateSelectedExitPrimaryGoalConditionForTests(out var createError), Is.True, createError);

                var entry = fixture.Authoring.Objective.ConditionEntries.Single();
                Assert.That(entry.Condition, Is.TypeOf<PlayerAtAnyZoneConditionAsset>());
                var selected = window.GetSelectedObjectiveConditionRowForTests();
                Assert.That(selected, Is.Not.Null);
                Assert.That(selected.StableConditionId, Is.EqualTo("primary-goal"));
                Assert.That(selected.Condition, Is.SameAs(entry.Condition));
                Assert.That(selected.Role, Is.EqualTo(StageObjectiveConditionRole.PrimaryGoal));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
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

                    var selectedBefore = window.GetSelectedObjectiveConditionRowForTests();
                    Assert.That(selectedBefore, Is.Not.Null);
                    Assert.That(selectedBefore.StableConditionId, Is.EqualTo("primary-goal"));

                    Assert.That(window.GetSelectedExitGoalZoneStatusForTests().Kind, Is.EqualTo(ExitGoalZoneStatusKind.ReferencedZoneMissing));
                    Assert.That(window.SyncSelectedExitGoalZoneForTests(out var error), Is.True, error);
                    Assert.That(window.GetSelectedExitGoalZoneStatusForTests().Kind, Is.EqualTo(ExitGoalZoneStatusKind.Valid));
                    var selectedAfter = window.GetSelectedObjectiveConditionRowForTests();
                    Assert.That(selectedAfter, Is.Not.Null);
                    Assert.That(selectedAfter.StableConditionId, Is.EqualTo("primary-goal"));
                    Assert.That(selectedAfter.Condition, Is.SameAs(selectedBefore.Condition));
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
                ActivationRule = kind == TileFeatureKind.Exit
                    ? TileFeatureActivationRule.ActiveFaceOnly
                    : TileFeatureActivationRule.BottomFaceOnly,
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
                AuthoringLabel = role == StageObjectiveConditionRole.PrimaryGoal
                    ? "Reach the Exit Zone"
                    : stableId,
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

        private sealed class TempStageContentFixture : IDisposable
        {
            private const string CanonicalContentRoot = StageContentPaths.CampaignLevel01StagesRoot;

            private TempStageContentFixture(
                string stageFolder,
                StageContentEntry entry,
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StagePresentationDefinition presentation)
            {
                StageFolder = stageFolder;
                Entry = entry;
                Authoring = authoring;
                Gameplay = gameplay;
                Presentation = presentation;
                ExpectedConditionPath =
                    $"{StageContentPaths.SharedConditionsRoot}/CampaignMain_{SanitizeName(entry.StageId.Value)}_PrimaryGoal_PlayerAtAnyZone.asset";
            }

            public string StageFolder { get; }

            public string ExpectedConditionPath { get; }

            public StageContentEntry Entry { get; }

            public StageAuthoringDefinition Authoring { get; }

            public StageDefinition Gameplay { get; }

            public StagePresentationDefinition Presentation { get; }

            public static TempStageContentFixture Create(bool withGeneratedCompanions = false)
            {
                var stageIdValue = $"objective-helper-test-{Guid.NewGuid():N}".Substring(0, 30);
                var stageFolder = $"{CanonicalContentRoot}/{stageIdValue}";
                EnsureFolder(CanonicalContentRoot);
                EnsureFolder(stageFolder);

                var entry = ScriptableObject.CreateInstance<StageContentEntry>();
                var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
                var gameplay = withGeneratedCompanions ? ScriptableObject.CreateInstance<StageDefinition>() : null;
                var presentation = withGeneratedCompanions ? ScriptableObject.CreateInstance<StagePresentationDefinition>() : null;

                entry.name = $"{stageIdValue}_Entry";
                authoring.name = $"{stageIdValue}_Authoring";
                if (gameplay != null)
                {
                    gameplay.name = stageIdValue;
                }

                if (presentation != null)
                {
                    presentation.name = $"{stageIdValue}_Presentation";
                }

                entry.AssignStageId(StageId.CreateOrThrow(stageIdValue));
                entry.AssignAuthoringDefinition(authoring);
                if (gameplay != null)
                {
                    entry.AssignGameplayDefinition(gameplay);
                }

                if (presentation != null)
                {
                    entry.AssignPresentationDefinition(presentation);
                }

                AssetDatabase.CreateAsset(entry, $"{stageFolder}/{stageIdValue}_Entry.asset");
                AssetDatabase.CreateAsset(authoring, $"{stageFolder}/{stageIdValue}_Authoring.asset");
                if (gameplay != null)
                {
                    AssetDatabase.CreateAsset(gameplay, $"{stageFolder}/{stageIdValue}.asset");
                }

                if (presentation != null)
                {
                    AssetDatabase.CreateAsset(presentation, $"{stageFolder}/{stageIdValue}_Presentation.asset");
                }

                var entryGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry));
                authoring.SetOwnerMetadata(entry, entryGuid);
                if (presentation != null)
                {
                    presentation.SetOwnerMetadata(entry, entryGuid);
                }

                authoring.AssignGeneratedDefinitions(gameplay, presentation);
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(3, 3),
                    InitialBottomFace = FaceId.Floor,
                });
                authoring.SetTileFeatures(new[] { TileFeature(1, TileFeatureKind.Exit, Cell(1, 1)) });
                authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());
                EditorUtility.SetDirty(entry);
                EditorUtility.SetDirty(authoring);
                if (gameplay != null)
                {
                    EditorUtility.SetDirty(gameplay);
                }

                if (presentation != null)
                {
                    EditorUtility.SetDirty(presentation);
                }

                AssetDatabase.SaveAssets();
                return new TempStageContentFixture(stageFolder, entry, authoring, gameplay, presentation);
            }

            public PlayerAtAnyZoneConditionAsset CreateExpectedPrimaryGoalCondition(params string[] zoneIds)
            {
                EnsureConditionsFolder();
                var condition = CreatePlayerAtAnyZone(zoneIds);
                condition.name = "PrimaryGoal_PlayerAtAnyZone";
                AssetDatabase.CreateAsset(condition, ExpectedConditionPath);
                AssetDatabase.SaveAssets();
                return condition;
            }

            public void EnsureConditionsFolder()
            {
                EnsureFolder(StageContentPaths.SharedConditionsRoot);
            }

            public GameObject CreateTileFeatureVisualPrefab()
            {
                var source = new GameObject("ExitVisualPrefab");
                source.AddComponent<TileFeatureVisualTargetView>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, $"{StageFolder}/ExitVisualPrefab.prefab");
                UnityEngine.Object.DestroyImmediate(source);
                return prefab;
            }

            public void Dispose()
            {
                AssetDatabase.DeleteAsset(ExpectedConditionPath);
                AssetDatabase.DeleteAsset(StageFolder);
                AssetDatabase.SaveAssets();
            }

            private static string SanitizeName(string value)
            {
                return string.IsNullOrWhiteSpace(value)
                    ? "Unnamed"
                    : string.Concat(value.Split('-', StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            }

            private static void EnsureFolder(string assetFolder)
            {
                if (AssetDatabase.IsValidFolder(assetFolder))
                {
                    return;
                }

                var parent = System.IO.Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }

                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(assetFolder));
            }
        }
    }
}
