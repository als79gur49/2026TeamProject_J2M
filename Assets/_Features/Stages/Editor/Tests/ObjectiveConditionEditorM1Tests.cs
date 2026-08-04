using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class ObjectiveConditionEditorM1Tests
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
        public void CampaignInventory_ResolvesAllObjectiveConditionRowsAndButtonAssociations()
        {
            var rows = new List<StageObjectiveConditionEditorRow>();
            for (var i = 0; i < ObjectiveStageNames.Length; i++)
            {
                var stageName = ObjectiveStageNames[i];
                var authoring = LoadAuthoring(stageName);
                var serialized = new SerializedObject(authoring);
                serialized.Update();
                var stageRows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);

                Assert.That(stageRows, Has.Count.GreaterThan(0), stageName);
                rows.AddRange(stageRows);
            }

            Assert.That(rows, Has.Count.EqualTo(54));
            Assert.That(
                rows.Count(row => row.Category == StageObjectiveConditionEditorCategory.PrimaryGoal),
                Is.EqualTo(10));
            Assert.That(
                rows.Count(row => row.Category == StageObjectiveConditionEditorCategory.ButtonObjective),
                Is.EqualTo(44));
            Assert.That(
                rows.Count(row => row.Category == StageObjectiveConditionEditorCategory.OtherCondition),
                Is.Zero);
            Assert.That(
                rows.Count(row => row.Category == StageObjectiveConditionEditorCategory.InvalidOrUnresolved),
                Is.Zero);
            Assert.That(rows.Count(row => !string.IsNullOrEmpty(row.AssociationWarning)), Is.Zero);
            Assert.That(
                rows.Count(row =>
                    row.AssociationKind == StageObjectiveConditionEditorAssociationKind.PushBoxButton),
                Is.EqualTo(29));
            Assert.That(
                rows.Count(row =>
                    row.AssociationKind == StageObjectiveConditionEditorAssociationKind.MoonBlockButton),
                Is.EqualTo(15));

            foreach (var row in rows)
            {
                Assert.That(row.StableConditionId, Is.Not.Empty);
                Assert.That(row.AuthoringLabel, Is.Not.Empty);
                Assert.That(row.Condition, Is.Not.Null);
                Assert.That(row.ConditionTypeName, Is.EqualTo(row.Condition.GetType().Name));

                if (row.Category == StageObjectiveConditionEditorCategory.PrimaryGoal)
                {
                    Assert.That(row.StableConditionId, Is.EqualTo("primary-goal"));
                    Assert.That(row.Role, Is.EqualTo(StageObjectiveConditionRole.PrimaryGoal));
                    Assert.That(row.Required, Is.True);
                    Assert.That(row.SortOrder, Is.Zero);
                    Assert.That(row.Condition, Is.TypeOf<PlayerAtAnyZoneConditionAsset>());
                    continue;
                }

                var button = row.Condition as ButtonActivatedConditionAsset;
                Assert.That(button, Is.Not.Null);
                Assert.That(
                    StageObjectiveConditionEditorResolver.TryParseButtonStableTileId(
                        row.StableConditionId,
                        out var stableTileId),
                    Is.True,
                    row.StableConditionId);
                Assert.That(stableTileId, Is.EqualTo(button.TileId));
                Assert.That(row.ButtonTileId, Is.EqualTo(button.TileId));
                Assert.That(row.TileCell.HasValue, Is.True);
                Assert.That(row.BoxSelector.HasValue, Is.True);
                Assert.That(row.Role, Is.EqualTo(StageObjectiveConditionRole.SecondaryGoal));
                Assert.That(row.Required, Is.True);
            }
        }

        [Test]
        public void Resolver_DisplaysOtherInvalidAndSharedConditionsWithoutExposingParameters()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var other = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                authoring.SetObjective(new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ObjectiveTitle = "Other conditions",
                    ObjectiveSummary = "Resolver coverage",
                    ConditionEntries = new[]
                    {
                        Entry(other, "other-a", "Other A", StageObjectiveConditionRole.Challenge, 10),
                        Entry(other, "other-b", "Other B", StageObjectiveConditionRole.Challenge, 20),
                        Entry(null, "invalid", "Invalid", StageObjectiveConditionRole.Challenge, 30),
                    },
                });
                var serialized = new SerializedObject(authoring);
                serialized.Update();

                var rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);

                Assert.That(rows, Has.Count.EqualTo(3));
                Assert.That(rows[0].Category, Is.EqualTo(StageObjectiveConditionEditorCategory.OtherCondition));
                Assert.That(rows[0].IsConditionAssetShared, Is.True);
                Assert.That(rows[1].IsConditionAssetShared, Is.True);
                Assert.That(
                    rows[2].Category,
                    Is.EqualTo(StageObjectiveConditionEditorCategory.InvalidOrUnresolved));
                Assert.That(rows[2].ConditionTypeName, Is.EqualTo("<unresolved>"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void Selection_RestoresAfterReorderByStableIdAndConditionReference()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var first = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var second = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                var firstEntry = Entry(first, "first", "First", StageObjectiveConditionRole.Challenge, 10);
                var secondEntry = Entry(second, "second", "Second", StageObjectiveConditionRole.Challenge, 20);
                authoring.SetObjective(Objective(firstEntry, secondEntry));
                var serialized = new SerializedObject(authoring);
                serialized.Update();
                var rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);
                var selection = new StageObjectiveConditionEditorSelection();
                selection.Select(rows[0]);

                authoring.SetObjective(Objective(secondEntry, firstEntry));
                serialized.Update();
                rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);

                Assert.That(
                    selection.Resolve(rows, out var selected),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
                Assert.That(selected.StableConditionId, Is.EqualTo("first"));
                Assert.That(selected.Condition, Is.SameAs(first));
                Assert.That(selected.EntryIndex, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void Selection_MismatchedStableIdAndReferenceCannotMutateAnyRow()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var first = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var second = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                authoring.SetObjective(Objective(
                    Entry(first, "first", "First", StageObjectiveConditionRole.Challenge, 10),
                    Entry(second, "second", "Second", StageObjectiveConditionRole.Challenge, 20)));
                var serialized = new SerializedObject(authoring);
                var selection = new StageObjectiveConditionEditorSelection();
                selection.Select("first", second);

                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetAuthoringLabel(
                        serialized,
                        authoring,
                        selection,
                        "Must not apply",
                        out var error),
                    Is.False);
                Assert.That(
                    error,
                    Is.EqualTo(StageObjectiveConditionEditorSelection.IdentityMismatchMessage));
                Assert.That(
                    authoring.Objective.ConditionEntries.Select(entry => entry.AuthoringLabel),
                    Is.EqualTo(new[] { "First", "Second" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void PrimaryLabelEdit_SupportsUndoRedoExplicitGenerateParityAndIdempotence()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-2");
            AssertLabelLifecycle(fixture, "primary-goal", "Temporary Primary M1 Label");
        }

        [Test]
        public void ExistingButtonLabelEdit_SupportsUndoRedoExplicitGenerateParityAndIdempotence()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var tileFeatureBefore = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 5);

            AssertLabelLifecycle(fixture, "button-5", "Temporary Button M1 Label");

            var tileFeatureAfter = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 5);
            Assert.That(tileFeatureAfter.TileId, Is.EqualTo(tileFeatureBefore.TileId));
            Assert.That(tileFeatureAfter.Cell, Is.EqualTo(tileFeatureBefore.Cell));
            Assert.That(tileFeatureAfter.BoxSelector, Is.EqualTo(tileFeatureBefore.BoxSelector));
            Assert.That(tileFeatureAfter.Kind, Is.EqualTo(tileFeatureBefore.Kind));
        }

        [Test]
        public void ObjectiveConditionContext_PrimaryExitSelectsCanonicalRow()
        {
            var authoring = LoadAuthoring("stage-0-2");
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(authoring);
                var exit = authoring.TileFeatures.Single(feature => feature.Kind == TileFeatureKind.Exit);
                window.SelectTileFeatureByIdForTests(exit.TileId);

                var selected = window.GetSelectedObjectiveConditionRowForTests();
                var expected = authoring.Objective.ConditionEntries.Single(entry =>
                    entry.StableConditionId == "primary-goal");
                Assert.That(selected, Is.Not.Null);
                Assert.That(selected.StableConditionId, Is.EqualTo("primary-goal"));
                Assert.That(selected.Role, Is.EqualTo(StageObjectiveConditionRole.PrimaryGoal));
                Assert.That(selected.Condition, Is.TypeOf<PlayerAtAnyZoneConditionAsset>());
                Assert.That(selected.Condition, Is.SameAs(expected.Condition));
                Assert.That(window.ObjectiveContextWarningForTests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ExitRequiredFalse_DoesNotResolveContext()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            try
            {
                var rows = new[] { PrimaryRow(condition, required: false) };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectPrimary(
                    rows,
                    condition,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.PrimarySelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        public void ExitRequiredTrue_ResolvesContext()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            try
            {
                var rows = new[] { PrimaryRow(condition, required: true) };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectPrimary(
                    rows,
                    condition,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Resolved));
                Assert.That(warning, Is.Empty);
                Assert.That(selection.Resolve(rows, out var selected),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
                Assert.That(selected, Is.SameAs(rows[0]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        public void ExitWrongRole_DoesNotResolveContext()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            try
            {
                var rows = new[]
                {
                    PrimaryRow(condition, required: true, role: StageObjectiveConditionRole.Challenge),
                };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectPrimary(
                    rows,
                    condition,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.PrimarySelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        public void ExitWrongConditionReference_DoesNotResolveContext()
        {
            var actual = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            var expected = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            try
            {
                var rows = new[] { PrimaryRow(actual, required: true) };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectPrimary(
                    rows,
                    expected,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.PrimarySelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
                UnityEngine.Object.DestroyImmediate(actual);
            }
        }

        [Test]
        public void ExitNavigatorAndValidator_AgreeOnCanonicalMetadata()
        {
            var authoring = UnityEngine.Object.Instantiate(LoadAuthoring("stage-0-2"));
            try
            {
                var exit = authoring.TileFeatures.Single(feature => feature.Kind == TileFeatureKind.Exit);
                Assert.That(
                    StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(
                        authoring,
                        exit.TileId,
                        out var status),
                    Is.True,
                    status.Message);
                var serialized = new SerializedObject(authoring);
                serialized.Update();
                var rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);
                var selection = new StageObjectiveConditionEditorSelection();

                Assert.That(
                    StageObjectiveConditionContextNavigator.SelectPrimary(
                        rows,
                        status.PrimaryGoalCondition,
                        selection,
                        out var warning),
                    Is.EqualTo(StageObjectiveConditionContextResolution.Resolved));
                Assert.That(warning, Is.Empty);

                var objective = authoring.Objective;
                var entries = objective.ConditionEntries.ToArray();
                var primaryIndex = Array.FindIndex(entries, entry =>
                    entry.StableConditionId == "primary-goal");
                entries[primaryIndex].Required = false;
                objective.ConditionEntries = entries;
                authoring.SetObjective(objective);
                serialized.Update();
                rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);

                Assert.That(
                    StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(
                        authoring,
                        exit.TileId,
                        out status),
                    Is.False);
                Assert.That(
                    StageObjectiveConditionContextNavigator.SelectPrimary(
                        rows,
                        status.PrimaryGoalCondition,
                        selection,
                        out warning),
                    Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.PrimarySelectionUnresolved));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void InvalidExitMetadata_PreservesContextWarning()
        {
            var authoring = UnityEngine.Object.Instantiate(LoadAuthoring("stage-0-2"));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                var objective = authoring.Objective;
                var entries = objective.ConditionEntries.ToArray();
                var primaryIndex = Array.FindIndex(entries, entry =>
                    entry.StableConditionId == "primary-goal");
                entries[primaryIndex].Required = false;
                objective.ConditionEntries = entries;
                authoring.SetObjective(objective);
                window.BindForTests(authoring);
                var exit = authoring.TileFeatures.Single(feature => feature.Kind == TileFeatureKind.Exit);

                Assert.That(window.SelectTileFeatureByIdForTests(exit.TileId), Is.True);

                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Null);
                Assert.That(window.ObjectiveContextWarningForTests,
                    Is.EqualTo(StageObjectiveConditionContextNavigator.PrimarySelectionUnresolved));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void InvalidExitMetadata_DoesNotSelectUnrelatedRow()
        {
            var primary = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            var unrelated = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                var rows = new[]
                {
                    PrimaryRow(primary, required: false),
                    OtherRow(unrelated, entryIndex: 1),
                };
                var selection = new StageObjectiveConditionEditorSelection();
                selection.Select(rows[1]);

                var resolution = StageObjectiveConditionContextNavigator.SelectPrimary(
                    rows,
                    primary,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.PrimarySelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unrelated);
                UnityEngine.Object.DestroyImmediate(primary);
            }
        }

        [Test]
        public void ObjectiveConditionContext_ButtonTileFiveSelectsCanonicalRowAndHighlight()
        {
            var authoring = LoadAuthoring("stage-0-1");
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(authoring);
                var feature = authoring.TileFeatures.Single(candidate => candidate.TileId == 5);
                window.SelectTileFeatureByIdForTests(feature.TileId);

                var selected = window.GetSelectedObjectiveConditionRowForTests();
                Assert.That(selected, Is.Not.Null);
                Assert.That(selected.StableConditionId, Is.EqualTo("button-5"));
                Assert.That(selected.Condition, Is.TypeOf<ButtonActivatedConditionAsset>());
                Assert.That(((ButtonActivatedConditionAsset)selected.Condition).TileId, Is.EqualTo(5));
                Assert.That(selected.Condition, Is.SameAs(
                    authoring.Objective.ConditionEntries.Single(entry =>
                        entry.StableConditionId == "button-5").Condition));
                Assert.That(selected.TileCell, Is.EqualTo(feature.Cell));
                Assert.That(selected.BoxSelector, Is.EqualTo(feature.BoxSelector));
                Assert.That(window.ObjectiveHighlightCellForTests, Is.EqualTo(feature.Cell));
                Assert.That(window.ObjectiveContextWarningForTests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SelectingNonObjectiveTileFeature_ClearsObjectiveContextWithoutRepeatedDirtying()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var condition = CreateButtonActivatedCondition(5);
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                authoring.SetTileFeatures(new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = 5,
                        Kind = TileFeatureKind.Button,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                        BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                    },
                    new StageTileFeatureDefinition
                    {
                        TileId = 6,
                        Kind = TileFeatureKind.Slide,
                        Cell = new SurfaceCell(FaceId.Floor, 2, 1),
                    },
                });
                authoring.SetObjective(Objective(
                    Entry(condition, "button-5", "Button", StageObjectiveConditionRole.SecondaryGoal, 10)));
                window.BindForTests(authoring);

                Assert.That(window.SelectTileFeatureByIdForTests(5), Is.True);
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Not.Null);

                Assert.That(window.SelectTileFeatureByIdForTests(6), Is.True);
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Null);
                Assert.That(window.ObjectiveHighlightCellForTests, Is.Null);
                Assert.That(window.ObjectiveContextWarningForTests, Is.Empty);
                Assert.That(window.RefreshObjectiveContextFromSelectedTileFeatureForTests(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(condition);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void UndoRedo_InvalidatesTileFeatureContextMarkerAndRecalculatesCurrentKind()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var condition = CreateButtonActivatedCondition(5);
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                authoring.SetTileFeatures(new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = 5,
                        Kind = TileFeatureKind.Button,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                        BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                    },
                });
                authoring.SetObjective(Objective(
                    Entry(condition, "button-5", "Button", StageObjectiveConditionRole.SecondaryGoal, 10)));
                window.BindForTests(authoring);
                Assert.That(window.SelectTileFeatureByIdForTests(5), Is.True);
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Not.Null);
                Undo.ClearAll();

                Undo.RecordObject(authoring, "Change selected TileFeature kind");
                var changedFeature = authoring.TileFeatures.Single();
                changedFeature.Kind = TileFeatureKind.Slide;
                authoring.SetTileFeatures(new[] { changedFeature });
                EditorUtility.SetDirty(authoring);
                Undo.FlushUndoRecordObjects();

                Undo.PerformUndo();
                window.HandleObjectiveUndoRedoForTests();
                Assert.That(authoring.TileFeatures.Single().Kind, Is.EqualTo(TileFeatureKind.Button));
                Assert.That(window.RefreshObjectiveContextFromSelectedTileFeatureForTests(), Is.True);
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Not.Null);

                Undo.PerformRedo();
                window.HandleObjectiveUndoRedoForTests();
                Assert.That(authoring.TileFeatures.Single().Kind, Is.EqualTo(TileFeatureKind.Slide));
                Assert.That(window.RefreshObjectiveContextFromSelectedTileFeatureForTests(), Is.True);
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Null);
                Assert.That(window.RefreshObjectiveContextFromSelectedTileFeatureForTests(), Is.False);
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(condition);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void ObjectiveConditionContext_StableIdAndButtonTileMismatchClearsSelectionWithoutMutation()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var condition = CreateButtonActivatedCondition(6);
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                authoring.SetTileFeatures(new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = 5,
                        Kind = TileFeatureKind.Button,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                        ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                        BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                    },
                });
                authoring.SetObjective(Objective(
                    Entry(condition, "button-5", "Mismatch", StageObjectiveConditionRole.SecondaryGoal, 10)));
                var before = EditorJsonUtility.ToJson(authoring);
                window.BindForTests(authoring);

                window.SelectTileFeatureByIdForTests(5);

                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Null);
                Assert.That(window.ResolveObjectiveConditionSelectionForTests(),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
                Assert.That(window.ObjectiveContextWarningForTests,
                    Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
                Assert.That(EditorJsonUtility.ToJson(authoring), Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(condition);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [TestCase(false, StageObjectiveConditionRole.SecondaryGoal)]
        [TestCase(true, StageObjectiveConditionRole.Challenge)]
        public void NavigationEligibility_RequiresRequiredSecondaryGoalMetadata(
            bool required,
            StageObjectiveConditionRole role)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var condition = CreateButtonActivatedCondition(5);
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                authoring.SetTileFeatures(new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = 5,
                        Kind = TileFeatureKind.Button,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                        BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                    },
                });
                var entry = Entry(condition, "button-5", "Button", role, 10);
                entry.Required = required;
                authoring.SetObjective(Objective(entry));
                var before = EditorJsonUtility.ToJson(authoring);
                window.BindForTests(authoring);

                Assert.That(window.SelectTileFeatureByIdForTests(5), Is.True);

                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Null);
                Assert.That(window.ObjectiveContextWarningForTests,
                    Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
                Assert.That(EditorJsonUtility.ToJson(authoring), Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(condition);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void ButtonNoAssociation_IsUnresolved()
        {
            var unrelated = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                var rows = new[] { OtherRow(unrelated, entryIndex: 0) };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectButton(
                    rows,
                    5,
                    null,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void NullExpectedCondition_IsNotWildcard()
        {
            var condition = CreateButtonActivatedCondition(5);
            try
            {
                var rows = new[] { ButtonRow(condition, entryIndex: 0, stableConditionId: "button-5") };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectButton(
                    rows,
                    5,
                    null,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        public void NullExpectedCondition_SelectsNoRow()
        {
            var condition = CreateButtonActivatedCondition(5);
            try
            {
                var rows = new[] { ButtonRow(condition, entryIndex: 0, stableConditionId: "button-5") };
                var selection = new StageObjectiveConditionEditorSelection();
                Assert.That(
                    StageObjectiveConditionContextNavigator.SelectButton(
                        rows,
                        5,
                        condition,
                        selection,
                        out _),
                    Is.EqualTo(StageObjectiveConditionContextResolution.Resolved));

                StageObjectiveConditionContextNavigator.SelectButton(
                    rows,
                    5,
                    null,
                    selection,
                    out _);

                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        public void ButtonSingleCanonicalAssociation_SelectsExpectedRow()
        {
            var condition = CreateButtonActivatedCondition(5);
            try
            {
                var rows = new[] { ButtonRow(condition, entryIndex: 0, stableConditionId: "button-5") };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectButton(
                    rows,
                    5,
                    condition,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Resolved));
                Assert.That(warning, Is.Empty);
                Assert.That(selection.Resolve(rows, out var selected),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
                Assert.That(selected, Is.SameAs(rows[0]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        public void ButtonDuplicateSameTileId_DoesNotSelectRow()
        {
            var canonical = CreateButtonActivatedCondition(5);
            var duplicate = CreateButtonActivatedCondition(5);
            try
            {
                var rows = new[]
                {
                    ButtonRow(canonical, entryIndex: 0, stableConditionId: "button-5"),
                    ButtonRow(duplicate, entryIndex: 1, stableConditionId: "alternate-button"),
                };
                var selection = new StageObjectiveConditionEditorSelection();

                var resolution = StageObjectiveConditionContextNavigator.SelectButton(
                    rows,
                    5,
                    canonical,
                    selection,
                    out var warning);

                Assert.That(resolution, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(canonical);
            }
        }

        [Test]
        public void ButtonDuplicateSameTileId_ReportsConflict()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var canonical = CreateButtonActivatedCondition(5);
            var duplicate = CreateButtonActivatedCondition(5);
            try
            {
                var feature = ButtonFeature(5);
                authoring.SetTileFeatures(new[] { feature });
                authoring.SetObjective(Objective(
                    Entry(canonical, "button-5", "Canonical", StageObjectiveConditionRole.SecondaryGoal, 10),
                    Entry(duplicate, "alternate-button", "Duplicate", StageObjectiveConditionRole.SecondaryGoal, 20)));

                var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(authoring, feature);

                Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.DuplicateCondition));
                Assert.That(status.MatchingEntryCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(canonical);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void ButtonDuplicateSameStableId_IsConflict()
        {
            var canonical = CreateButtonActivatedCondition(5);
            var differentTile = CreateButtonActivatedCondition(6);
            try
            {
                var rows = new[]
                {
                    ButtonRow(canonical, entryIndex: 0, stableConditionId: "button-5"),
                    ButtonRow(differentTile, entryIndex: 1, stableConditionId: "button-5"),
                };
                var selection = new StageObjectiveConditionEditorSelection();

                Assert.That(
                    StageObjectiveConditionContextNavigator.SelectButton(
                        rows,
                        5,
                        canonical,
                        selection,
                        out var warning),
                    Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(differentTile);
                UnityEngine.Object.DestroyImmediate(canonical);
            }
        }

        [Test]
        public void ButtonDuplicateDifferentStableIds_IsConflict()
        {
            var canonical = CreateButtonActivatedCondition(5);
            var duplicate = CreateButtonActivatedCondition(5);
            try
            {
                var rows = new[]
                {
                    ButtonRow(canonical, entryIndex: 0, stableConditionId: "button-5"),
                    ButtonRow(duplicate, entryIndex: 1, stableConditionId: "button-copy"),
                };
                var selection = new StageObjectiveConditionEditorSelection();

                Assert.That(
                    StageObjectiveConditionContextNavigator.SelectButton(
                        rows,
                        5,
                        canonical,
                        selection,
                        out var warning),
                    Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(canonical);
            }
        }

        [Test]
        public void ButtonOneCanonicalOneInvalid_IsStillConflict()
        {
            var canonical = CreateButtonActivatedCondition(5);
            var invalid = CreateButtonActivatedCondition(5);
            try
            {
                var rows = new[]
                {
                    ButtonRow(canonical, entryIndex: 0, stableConditionId: "button-5"),
                    ButtonRow(
                        invalid,
                        entryIndex: 1,
                        stableConditionId: "invalid-button",
                        required: false,
                        role: StageObjectiveConditionRole.Challenge),
                };
                var selection = new StageObjectiveConditionEditorSelection();

                Assert.That(
                    StageObjectiveConditionContextNavigator.SelectButton(
                        rows,
                        5,
                        canonical,
                        selection,
                        out var warning),
                    Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalid);
                UnityEngine.Object.DestroyImmediate(canonical);
            }
        }

        [Test]
        public void NavigatorAndRemovalPlanner_AgreeOnDuplicateAssociation()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var canonical = CreateButtonActivatedCondition(5);
            var duplicate = CreateButtonActivatedCondition(5);
            try
            {
                var feature = ButtonFeature(5);
                authoring.SetTileFeatures(new[] { feature });
                authoring.SetObjective(Objective(
                    Entry(canonical, "button-5", "Canonical", StageObjectiveConditionRole.SecondaryGoal, 10),
                    Entry(duplicate, "different-stable-id", "Duplicate", StageObjectiveConditionRole.SecondaryGoal, 20)));
                var serialized = new SerializedObject(authoring);
                serialized.Update();
                var rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);
                var selection = new StageObjectiveConditionEditorSelection();

                var navigation = StageObjectiveConditionContextNavigator.SelectButton(
                    rows,
                    5,
                    canonical,
                    selection,
                    out var warning);
                var linkStatus = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(authoring, feature);
                var removalPlan = StageButtonObjectiveRemovalPlanner.Build(authoring, feature);

                Assert.That(navigation, Is.EqualTo(StageObjectiveConditionContextResolution.Unresolved));
                Assert.That(warning, Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
                Assert.That(linkStatus.State, Is.EqualTo(ButtonObjectiveLinkState.DuplicateCondition));
                Assert.That(linkStatus.MatchingEntryCount, Is.EqualTo(2));
                Assert.That(removalPlan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
                Assert.That(removalPlan.Candidates, Has.Count.EqualTo(2));
                Assert.That(selection.Resolve(rows, out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(canonical);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void ObjectiveConditionValidation_ValidCampaignHasNoObjectiveIssues()
        {
            var rowCount = 0;
            for (var i = 0; i < ObjectiveStageNames.Length; i++)
            {
                var authoring = LoadAuthoring(ObjectiveStageNames[i]);
                var feedback = StageObjectiveConditionEditorFeedbackBuilder.Build(
                    authoring,
                    StageObjectiveConditionEditorFeedbackBuilder.ResolveCatalogEntry(authoring));
                rowCount += authoring.Objective.GetConditionEntriesOrEmpty().Length;

                Assert.That(feedback.Status, Is.EqualTo(StageObjectiveConditionEditorStatus.InSync),
                    ObjectiveStageNames[i] + ": " + feedback.Message);
                Assert.That(feedback.ValidationIssues, Is.Empty, ObjectiveStageNames[i]);
            }

            Assert.That(rowCount, Is.EqualTo(54));
        }

        [Test]
        public void ResolveIssues_MatchesExactRowAndLeavesOtherSameTypeRowGeneral()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var conditionA = CreateButtonActivatedCondition(5);
            var conditionB = CreateButtonActivatedCondition(6);
            try
            {
                authoring.SetTileFeatures(new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = 5,
                        Kind = TileFeatureKind.Button,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                    },
                    new StageTileFeatureDefinition
                    {
                        TileId = 6,
                        Kind = TileFeatureKind.Button,
                        Cell = new SurfaceCell(FaceId.Floor, 2, 1),
                    },
                });
                authoring.SetObjective(Objective(
                    Entry(conditionA, "button-5", "Button A", StageObjectiveConditionRole.SecondaryGoal, 10),
                    Entry(conditionB, "button-6", "Button B", StageObjectiveConditionRole.SecondaryGoal, 20)));
                var serialized = new SerializedObject(authoring);
                serialized.Update();
                var rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);
                var validationIssue = new StageValidationIssue(
                    StageValidationSeverity.Error,
                    "objective.sort-order",
                    "entry[1] has an invalid SortOrder.",
                    fieldName: "Objective.ConditionEntries[1].SortOrder");
                var validationFeedback = new StageObjectiveConditionEditorFeedback(
                    StageObjectiveConditionEditorStatus.InvalidAuthoring,
                    new[] { validationIssue },
                    Array.Empty<StageValidationIssue>(),
                    false,
                    "Objective authoring is invalid.");

                Assert.That(validationFeedback.GetRowMessage(rows[0]),
                    Is.EqualTo("Objective authoring is invalid."));
                Assert.That(validationFeedback.GetRowMessage(rows[1]),
                    Does.StartWith("objective.sort-order:"));

                var driftIssue = new StageValidationIssue(
                    StageValidationSeverity.Warning,
                    "GameplayDrift.ObjectiveMismatch",
                    "entry[1] label differs.",
                    fieldName: "Objective.ConditionEntries[1].AuthoringLabel");
                var driftFeedback = new StageObjectiveConditionEditorFeedback(
                    StageObjectiveConditionEditorStatus.GenerateRequired,
                    Array.Empty<StageValidationIssue>(),
                    new[] { driftIssue },
                    false,
                    "Objective generation is required.");

                Assert.That(driftFeedback.GetRowMessage(rows[0]),
                    Is.EqualTo("Objective generation is required."));
                Assert.That(driftFeedback.GetRowMessage(rows[1]),
                    Is.EqualTo("Objective condition AuthoringLabel differs from generated output."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionA);
                UnityEngine.Object.DestroyImmediate(conditionB);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [TestCase("abc", "abc", true)]
        [TestCase("abc-1", "abc", false)]
        [TestCase("abc-10", "abc-1", false)]
        [TestCase("xabc", "abc", false)]
        [TestCase("abc", "", false)]
        public void StableConditionIdIssueMatcher_UsesExactCanonicalToken(
            string issueToken,
            string stableConditionId,
            bool expected)
        {
            Assert.That(
                StageObjectiveConditionEditorFeedback.HasExactStableConditionIdToken(
                    $"Objective issue for '{issueToken}'.",
                    stableConditionId),
                Is.EqualTo(expected));
        }

        [Test]
        public void StableConditionIdIssueMatcher_PrefixCollisionDoesNotAssignWrongRow()
        {
            var conditionA = CreateButtonActivatedCondition(1);
            var conditionB = CreateButtonActivatedCondition(10);
            try
            {
                var rowA = ButtonRow(conditionA, entryIndex: 0, stableConditionId: "button-1");
                var rowB = ButtonRow(conditionB, entryIndex: 1, stableConditionId: "button-10");
                var issue = new StageValidationIssue(
                    StageValidationSeverity.Error,
                    "objective.stable-id",
                    "Duplicate stable ID 'button-10'.",
                    fieldName: "Objective.ConditionEntries.StableConditionId");
                var feedback = new StageObjectiveConditionEditorFeedback(
                    StageObjectiveConditionEditorStatus.InvalidAuthoring,
                    new[] { issue },
                    Array.Empty<StageValidationIssue>(),
                    false,
                    "Objective authoring is invalid.");

                Assert.That(feedback.GetRowMessage(rowA),
                    Is.EqualTo("Objective authoring is invalid."));
                Assert.That(feedback.GetRowMessage(rowB),
                    Is.EqualTo("objective.stable-id: Duplicate stable ID 'button-10'."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionB);
                UnityEngine.Object.DestroyImmediate(conditionA);
            }
        }

        [Test]
        public void ObjectiveConditionDrift_ValidAuthoringWithoutGeneratedOutputReportsMissing()
        {
            var source = LoadAuthoring("stage-0-2");
            var authoring = UnityEngine.Object.Instantiate(source);
            try
            {
                authoring.AssignGeneratedDefinitions(null, source.GeneratedPresentationDefinition);

                var feedback = StageObjectiveConditionEditorFeedbackBuilder.Build(authoring);

                Assert.That(
                    feedback.Status,
                    Is.EqualTo(StageObjectiveConditionEditorStatus.GeneratedOutputMissing));
                Assert.That(feedback.ValidationIssues, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void GeneratedCatalogObjectiveError_AfterAuthoringCorrection_ReportsGenerateRequired()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var catalogEntry = ScriptableObject.CreateInstance<StageContentEntry>();
            try
            {
                catalogEntry.name = "stage-0-1_Entry";
                catalogEntry.AssignStageId(StageId.CreateOrThrow("stage-0-1"));
                catalogEntry.AssignAuthoringDefinition(fixture.Authoring);
                catalogEntry.AssignGameplayDefinition(fixture.Gameplay);
                catalogEntry.AssignPresentationDefinition(fixture.Presentation);
                SetGeneratedAuthoringLabel(fixture.Gameplay, "button-5", "   ");

                var feedback = StageObjectiveConditionEditorFeedbackBuilder.Build(
                    fixture.Authoring,
                    catalogEntry);

                Assert.That(
                    feedback.Status,
                    Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));
                Assert.That(feedback.ValidationIssues, Is.Empty);
                Assert.That(feedback.DriftIssues, Is.Not.Empty);
                Assert.That(feedback.GeneratedOutputIssues, Is.Not.Empty);
                Assert.That(feedback.IssueSourceKind,
                    Is.EqualTo(StageObjectiveConditionIssueSourceKind.GeneratedOutput));
                Assert.That(feedback.IssueOwner, Is.SameAs(fixture.Gameplay));
                Assert.That(feedback.CanGenerateOrRepair, Is.True);
                Assert.That(feedback.Message, Does.Contain("differs"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalogEntry);
            }
        }

        [TestCase("duplicate-stable-id")]
        [TestCase("duplicate-button-tile")]
        [TestCase("invalid-primary-structure")]
        public void ObjectiveConditionValidation_InvalidStructuresUseExistingValidator(string scenario)
        {
            using var fixture = CampaignPairFixture.Create(
                scenario == "invalid-primary-structure" ? "stage-0-2" : "stage-0-1");
            var entries = fixture.Authoring.Objective.ConditionEntries.ToList();
            var source = scenario == "invalid-primary-structure"
                ? entries.Single(entry => entry.Role == StageObjectiveConditionRole.PrimaryGoal)
                : entries.First(entry => entry.Condition is ButtonActivatedConditionAsset);
            entries.Add(new StageObjectiveConditionEntry
            {
                Condition = source.Condition,
                Required = true,
                Role = scenario == "invalid-primary-structure"
                    ? StageObjectiveConditionRole.PrimaryGoal
                    : StageObjectiveConditionRole.SecondaryGoal,
                StableConditionId = scenario == "duplicate-stable-id"
                    ? source.StableConditionId
                    : scenario == "duplicate-button-tile"
                    ? "button-duplicate"
                    : "primary-duplicate",
                AuthoringLabel = "Duplicate validation entry",
                SortOrder = source.SortOrder + 100,
            });
            fixture.Authoring.SetObjective(new StageObjectiveAuthoring
            {
                CompletionPolicy = fixture.Authoring.Objective.CompletionPolicy,
                ObjectiveTitle = fixture.Authoring.Objective.ObjectiveTitle,
                ObjectiveSummary = fixture.Authoring.Objective.ObjectiveSummary,
                ConditionEntries = entries.ToArray(),
            });

            var feedback = fixture.Window.GetObjectiveConditionFeedbackForTests();

            Assert.That(feedback.Status, Is.EqualTo(StageObjectiveConditionEditorStatus.InvalidAuthoring));
            Assert.That(feedback.ValidationIssues.Any(issue =>
                issue.Code == "authoring.generated-gameplay.invalid"), Is.True, feedback.Message);
        }

        [Test]
        public void SaveReimport_RebuildsRowsAndRestoresSelectionByStableIdentity()
        {
            var folderPath = $"Assets/__ObjectiveConditionEditorM1_{Guid.NewGuid():N}";
            var gameplayPath = $"{folderPath}/Generated.asset";
            var presentationPath = $"{folderPath}/Presentation.asset";
            var authoringPath = $"{folderPath}/Authoring.asset";
            StageAuthoringGridWindow window = null;
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folderPath));
                var source = LoadAuthoring("stage-0-1");
                var gameplay = UnityEngine.Object.Instantiate(source.GeneratedGameplayDefinition);
                var presentation = UnityEngine.Object.Instantiate(source.GeneratedPresentationDefinition);
                var authoring = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(gameplay, gameplayPath);
                AssetDatabase.CreateAsset(presentation, presentationPath);
                authoring.AssignGeneratedDefinitions(gameplay, presentation);
                AssetDatabase.CreateAsset(authoring, authoringPath);
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssetIfDirty(gameplay);
                AssetDatabase.SaveAssetIfDirty(presentation);
                AssetDatabase.SaveAssetIfDirty(authoring);

                window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
                window.BindForTests(authoring);
                var row = window.GetObjectiveConditionRowsForTests()
                    .Single(candidate => candidate.StableConditionId == "button-5");
                Assert.That(
                    window.SelectObjectiveConditionForTests(row.StableConditionId, row.Condition),
                    Is.True);
                Assert.That(
                    window.SetSelectedObjectiveAuthoringLabelForTests(
                        "Persisted Button M1 Label",
                        out var error),
                    Is.True,
                    error);
                AssetDatabase.SaveAssetIfDirty(authoring);
                UnityEngine.Object.DestroyImmediate(window);
                window = null;

                AssetDatabase.ImportAsset(authoringPath, ImportAssetOptions.ForceUpdate);
                var reopened = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(authoringPath);
                var reopenedSerialized = new SerializedObject(reopened);
                reopenedSerialized.Update();
                var reopenedRows =
                    StageObjectiveConditionEditorResolver.BuildRows(reopenedSerialized, reopened);
                var reopenedRow = reopenedRows.Single(candidate =>
                    candidate.StableConditionId == "button-5");
                var selection = new StageObjectiveConditionEditorSelection();
                selection.Select("button-5", reopenedRow.Condition);

                Assert.That(reopenedRow.AuthoringLabel, Is.EqualTo("Persisted Button M1 Label"));
                Assert.That(
                    selection.Resolve(reopenedRows, out var restored),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
                Assert.That(restored.StableConditionId, Is.EqualTo("button-5"));
                Assert.That(restored.Condition, Is.SameAs(reopenedRow.Condition));
                Assert.That(
                    File.ReadAllText(ToAbsoluteProjectPath(authoringPath)),
                    Does.Contain("Persisted Button M1 Label"));
            }
            finally
            {
                if (window != null)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }

                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [TestCase("stage-0-2", "primary-goal")]
        [TestCase("stage-0-1", "button-5")]
        public void EmptyLabel_RemainsVisibleAndBlocksGenerateWithoutGeneratedMutation(
            string stageName,
            string stableConditionId)
        {
            using var fixture = CampaignPairFixture.Create(stageName);
            var generatedBefore = EditorJsonUtility.ToJson(fixture.Gameplay);
            var row = fixture.Window.GetObjectiveConditionRowsForTests()
                .Single(candidate => candidate.StableConditionId == stableConditionId);
            Assert.That(
                fixture.Window.SelectObjectiveConditionForTests(row.StableConditionId, row.Condition),
                Is.True);

            Assert.That(
                fixture.Window.SetSelectedObjectiveAuthoringLabelForTests("   ", out var error),
                Is.True,
                error);
            var editedRow = fixture.Window.GetObjectiveConditionRowsForTests()
                .Single(candidate => candidate.StableConditionId == stableConditionId);
            Assert.That(editedRow.AuthoringLabel, Is.EqualTo("   "));
            Assert.That(editedRow.IsAuthoringLabelMissing, Is.True);
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries
                    .Single(entry => entry.StableConditionId == stableConditionId)
                    .AuthoringLabel,
                Is.EqualTo("   "),
                "The authoring getter must not silently restore a default label.");
            var invalidFeedback = fixture.Window.GetObjectiveConditionFeedbackForTests();
            Assert.That(
                invalidFeedback.Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InvalidAuthoring));
            Assert.That(
                invalidFeedback.ValidationIssues.Any(issue =>
                    issue.Code == StageObjectiveAuthoringMetadataPolicy.AuthoringLabelMissingCode),
                Is.True);

            fixture.Window.GenerateForTests();

            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.True);
            Assert.That(
                fixture.Window.LastReportForTests.Issues.Any(issue =>
                    issue.Code == StageObjectiveAuthoringMetadataPolicy.AuthoringLabelMissingCode),
                Is.True,
                FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(generatedBefore));
        }

        [Test]
        public void ObjectiveConditionRemoval_CanonicalLifecycle_UndoRedoGenerateAndSecondGenerateAreExact()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var button = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 5);
            var condition = fixture.Authoring.Objective.ConditionEntries
                .Single(entry => entry.StableConditionId == "button-5").Condition;
            var conditionPath = AssetDatabase.GetAssetPath(condition);
            var conditionBytesBefore = ReadAssetBytes(conditionPath);
            var generatedBefore = fixture.ReadGameplayBytes();
            fixture.Window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
            fixture.Window.SelectTileFeatureByIdForTests(button.TileId);
            Assert.That(fixture.Window.SelectObjectiveConditionForTests("button-5", condition), Is.True);
            var plan = fixture.Window.GetSelectedButtonObjectiveRemovalPlanForTests();
            Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.SingleCanonical));
            fixture.Window.SetButtonObjectiveRemovalConfirmationForTests(
                new FixedRemovalConfirmation(true));
            Undo.ClearAll();

            Assert.That(
                fixture.Window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error),
                Is.True,
                error);
            Undo.FlushUndoRecordObjects();

            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.False);
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(generatedBefore));
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));
            Assert.That(fixture.Window.ResolveObjectiveConditionSelectionForTests(),
                Is.EqualTo(StageObjectiveConditionSelectionResolution.None));

            Undo.PerformUndo();
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.True);
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));

            Undo.PerformRedo();
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.False);
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));

            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            AssertPairParity(fixture.Authoring, fixture.Gameplay);
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));
            var authoringAfterGenerate = fixture.ReadAuthoringBytes();
            var gameplayAfterGenerate = fixture.ReadGameplayBytes();
            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(fixture.ReadAuthoringBytes(), Is.EqualTo(authoringAfterGenerate));
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(gameplayAfterGenerate));
            Assert.That(ReadAssetBytes(conditionPath), Is.EqualTo(conditionBytesBefore));
            Assert.That(AssetDatabase.Contains(condition), Is.True);
        }

        [Test]
        public void ObjectiveConditionRemoval_SingleRemoveGenerateUndo_RestoresAuthoringOnly()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var button = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 5);
            fixture.Window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
            fixture.Window.SelectTileFeatureByIdForTests(button.TileId);
            fixture.Window.SetButtonObjectiveRemovalConfirmationForTests(
                new FixedRemovalConfirmation(true));
            Undo.ClearAll();

            Assert.That(
                fixture.Window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error),
                Is.True,
                error);
            Undo.FlushUndoRecordObjects();
            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(fixture.Gameplay.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.False);

            Undo.PerformUndo();

            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.True);
            Assert.That(fixture.Gameplay.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.False);
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));
        }

        [Test]
        public void RemovalGenerate_PreservesUnrelatedGameplayPresentationAndMappingUndo()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var button = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 5);
            var placement = fixture.Authoring.Placements.First(candidate =>
                candidate.Kind == StageAuthoringEntityKind.Wall &&
                !string.IsNullOrEmpty(candidate.PresentationId));
            var originalHp = placement.Hp;
            var originalDisplayName = placement.DisplayName;
            var originalPresentationId = placement.PresentationId;
            var placementStableGuid = placement.StableGuid;
            var originalMappingName = fixture.Authoring.EntityIdMappings.Single(mapping =>
                mapping.StableGuid == placementStableGuid).LastKnownDisplayName;
            var gameplayBefore = EditorJsonUtility.ToJson(fixture.Gameplay);
            var presentationBefore = EditorJsonUtility.ToJson(fixture.Presentation);
            fixture.Window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
            fixture.Window.SelectTileFeatureByIdForTests(button.TileId);
            fixture.Window.SetButtonObjectiveRemovalConfirmationForTests(
                new FixedRemovalConfirmation(true));
            Undo.ClearAll();

            Undo.RecordObject(fixture.Authoring, "Stage unrelated authoring drift");
            placement.Hp = originalHp + 1;
            placement.DisplayName = "Review drift wall";
            placement.PresentationId = "box_tutorial";
            EditorUtility.SetDirty(fixture.Authoring);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();

            Assert.That(
                fixture.Window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error),
                Is.True,
                error);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();

            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.Not.EqualTo(gameplayBefore));
            Assert.That(EditorJsonUtility.ToJson(fixture.Presentation), Is.Not.EqualTo(presentationBefore));
            Assert.That(fixture.Authoring.EntityIdMappings.Single(mapping =>
                    mapping.StableGuid == placementStableGuid).LastKnownDisplayName,
                Is.EqualTo("Review drift wall"));

            Undo.PerformUndo();
            placement = fixture.Authoring.Placements.Single(candidate =>
                candidate.StableGuid == placementStableGuid);

            Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(gameplayBefore));
            Assert.That(EditorJsonUtility.ToJson(fixture.Presentation), Is.EqualTo(presentationBefore));
            Assert.That(fixture.Authoring.EntityIdMappings.Single(mapping =>
                    mapping.StableGuid == placementStableGuid).LastKnownDisplayName,
                Is.EqualTo(originalMappingName));
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.False);
            Assert.That(placement.DisplayName, Is.EqualTo("Review drift wall"));

            Undo.PerformUndo();
            placement = fixture.Authoring.Placements.Single(candidate =>
                candidate.StableGuid == placementStableGuid);

            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.True);
            Assert.That(placement.DisplayName, Is.EqualTo("Review drift wall"));

            Undo.PerformUndo();
            placement = fixture.Authoring.Placements.Single(candidate =>
                candidate.StableGuid == placementStableGuid);

            Assert.That(placement.Hp, Is.EqualTo(originalHp));
            Assert.That(placement.DisplayName, Is.EqualTo(originalDisplayName));
            Assert.That(placement.PresentationId, Is.EqualTo(originalPresentationId));
        }

        [Test]
        public void ObjectiveConditionRemoval_RepairLifecycle_GenerateParityUndoAndInvalidGenerateBlock()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var button = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 5);
            var objective = fixture.Authoring.Objective;
            var canonical = objective.ConditionEntries.Single(entry =>
                entry.StableConditionId == "button-5");
            var conditionPath = AssetDatabase.GetAssetPath(canonical.Condition);
            var conditionBytesBefore = ReadAssetBytes(conditionPath);
            var entries = objective.ConditionEntries.ToList();
            var duplicate = canonical;
            duplicate.AuthoringLabel = "Conflict duplicate";
            duplicate.SortOrder += 10;
            entries.Insert(entries.Count - 1, duplicate);
            objective.ConditionEntries = entries.ToArray();
            fixture.Authoring.SetObjective(objective);
            EditorUtility.SetDirty(fixture.Authoring);
            fixture.Window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
            fixture.Window.SelectTileFeatureByIdForTests(button.TileId);
            var plan = fixture.Window.GetSelectedButtonObjectiveRemovalPlanForTests();
            Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
            Assert.That(plan.Candidates.Count, Is.EqualTo(2));
            fixture.Window.SetButtonObjectiveRemovalConfirmationForTests(
                new FixedRemovalConfirmation(true));
            Undo.ClearAll();

            Assert.That(
                fixture.Window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error),
                Is.True,
                error);
            Undo.FlushUndoRecordObjects();
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.False);
            Assert.That(fixture.Gameplay.Objective.ConditionEntries.Count(entry =>
                entry.StableConditionId == "button-5"), Is.EqualTo(1));
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));

            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            AssertPairParity(fixture.Authoring, fixture.Gameplay);
            var gameplayAfterGenerate = fixture.ReadGameplayBytes();
            fixture.Window.GenerateForTests();
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(gameplayAfterGenerate));

            Undo.PerformUndo();
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Count(entry =>
                entry.StableConditionId == "button-5"), Is.EqualTo(2));
            Assert.That(fixture.Gameplay.Objective.ConditionEntries.Any(entry =>
                entry.StableConditionId == "button-5"), Is.False);
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InvalidAuthoring));
            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.True);
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(gameplayAfterGenerate));
            Assert.That(ReadAssetBytes(conditionPath), Is.EqualTo(conditionBytesBefore));
        }

        [Test]
        public void ObjectiveConditionRemoval_StageThreeOne_RemovesMiddleButtonWithoutRenumberingGroupOrder()
        {
            using var fixture = CampaignPairFixture.Create("stage-3-1");
            var button = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 8);
            var before = fixture.Authoring.Objective.ConditionEntries.ToArray();
            var removed = before.Single(entry => entry.StableConditionId == "button-8");
            var expected = before.Where(entry => entry.StableConditionId != "button-8").ToArray();
            var runtimeBefore = StageRuntimeBuilder.Build(fixture.Gameplay)
                .ObjectiveRuntimeDefinition
                .ConditionEntries;
            var removedRuntime = runtimeBefore.Single(entry => entry.StableConditionId == "button-8");
            fixture.Window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
            fixture.Window.SelectTileFeatureByIdForTests(button.TileId);
            fixture.Window.SetButtonObjectiveRemovalConfirmationForTests(
                new FixedRemovalConfirmation(true));

            Assert.That(
                fixture.Window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error),
                Is.True,
                error);

            Assert.That(fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(expected.Select(entry => entry.StableConditionId)));
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.SortOrder),
                Is.EqualTo(expected.Select(entry => entry.SortOrder)));
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Any(entry =>
                entry.SortOrder == removed.SortOrder), Is.False);

            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            var runtimeAfter = StageRuntimeBuilder.Build(fixture.Gameplay)
                .ObjectiveRuntimeDefinition
                .ConditionEntries;
            Assert.That(runtimeAfter.Select(entry => entry.StableConditionId),
                Is.EqualTo(expected.Select(entry => entry.StableConditionId)));
            Assert.That(runtimeAfter
                    .Where(entry => entry.StableGroupKey == removedRuntime.StableGroupKey)
                    .Min(entry => entry.SortOrder),
                Is.EqualTo(10));
        }

        [Test]
        public void ObjectiveConditionRemoval_StageFourTwo_KeepsPrimaryArrayLastAndRuntimeFirst()
        {
            using var fixture = CampaignPairFixture.Create("stage-4-2");
            var button = fixture.Authoring.TileFeatures.Single(feature => feature.TileId == 13);
            var before = fixture.Authoring.Objective.ConditionEntries.ToArray();
            var expected = before.Where(entry => entry.StableConditionId != "button-13").ToArray();
            fixture.Window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
            fixture.Window.SelectTileFeatureByIdForTests(button.TileId);
            fixture.Window.SetButtonObjectiveRemovalConfirmationForTests(
                new FixedRemovalConfirmation(true));

            Assert.That(
                fixture.Window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error),
                Is.True,
                error);

            Assert.That(fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(expected.Select(entry => entry.StableConditionId)));
            var primary = fixture.Authoring.Objective.ConditionEntries[^1];
            Assert.That(primary.Role, Is.EqualTo(StageObjectiveConditionRole.PrimaryGoal));
            Assert.That(primary.SortOrder, Is.Zero);

            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            var runtime = StageRuntimeBuilder.Build(fixture.Gameplay)
                .ObjectiveRuntimeDefinition
                .ConditionEntries;
            var runtimePrimary = runtime.Single(entry => entry.Role == StageObjectiveConditionRole.PrimaryGoal);
            Assert.That(runtimePrimary.AuthoringOrder, Is.EqualTo(runtime.Count - 1));
            Assert.That(runtimePrimary.SortOrder, Is.Zero);
            Assert.That(runtime
                    .OrderBy(entry => entry.SortOrder)
                    .ThenBy(entry => entry.AuthoringOrder)
                    .First(),
                Is.SameAs(runtimePrimary));
        }

        [Test]
        public void UnifiedEditorMutationSource_WritesOnlyAuthorizedMetadataAndKeepsSelectionTransient()
        {
            var mutationSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageObjectiveConditionEditorSelection.cs"));
            var rendererSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageObjectiveConditionEditorRenderer.cs"));
            var windowSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGridWindow.cs"));
            var navigatorSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageObjectiveConditionContextNavigator.cs"));
            var feedbackSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageObjectiveConditionEditorFeedback.cs"));

            Assert.That(mutationSource, Does.Contain("labelProperty.stringValue = nextValue;"));
            Assert.That(mutationSource, Does.Contain("sortOrderProperty.intValue = sortOrder;"));
            Assert.That(mutationSource, Does.Not.Contain(".objectReferenceValue ="));
            Assert.That(mutationSource, Does.Not.Contain(".boolValue ="));
            Assert.That(
                mutationSource.Split(new[] { ".intValue = " }, StringSplitOptions.None).Length - 1,
                Is.EqualTo(1));
            Assert.That(mutationSource, Does.Not.Contain(".arraySize ="));
            Assert.That(mutationSource, Does.Not.Contain("MoveArrayElement"));
            Assert.That(mutationSource, Does.Not.Contain("Undo.RecordObject"));
            Assert.That(mutationSource, Does.Not.Contain("[SerializeField]"));
            Assert.That(rendererSource, Does.Not.Contain("EditorGUILayout.PropertyField"));
            Assert.That(rendererSource, Does.Not.Contain("ApplyModifiedProperties"));
            Assert.That(windowSource, Does.Not.Contain("objectiveConditionSelection = authoring"));
            Assert.That(windowSource, Does.Contain("StageObjectiveConditionEditorRenderer.Draw"));
            Assert.That(rendererSource, Does.Contain("\"Generated Stage Definition\""));
            Assert.That(rendererSource, Does.Not.Contain("GeneratedGameplayDefinition.Set"));
            Assert.That(navigatorSource, Does.Not.Contain("SerializedObject"));
            Assert.That(navigatorSource, Does.Not.Contain("SetObjective("));
            Assert.That(navigatorSource, Does.Not.Contain("AuthoringLabel"));
            Assert.That(feedbackSource, Does.Contain("StageAuthoringDriftComparer.CompareGameplay"));
            Assert.That(feedbackSource, Does.Contain("StageAuthoringGenerator.Generate"));
            Assert.That(feedbackSource, Does.Contain("new StageCatalogValidator().ValidateEntries"));
            Assert.That(feedbackSource, Does.Not.Contain("GeneratedGameplayDefinition.Set"));
            Assert.That(feedbackSource, Does.Not.Contain("AssetDatabase.SaveAssets("));
        }

        [Test]
        public void Generate_SavesOwnedSourceAndOutputsWithoutSavingUnrelatedDirtyAsset()
        {
            var fixtureFolder = $"Assets/__TempStageAuthoringTests_{Guid.NewGuid():N}";
            var authoringPath = $"{fixtureFolder}/Authoring.asset";
            var gameplayPath = $"{fixtureFolder}/Generated.asset";
            var presentationPath = $"{fixtureFolder}/Presentation.asset";
            var sentinelPath = $"{fixtureFolder}/UnrelatedDirtySentinel.asset";
            const string stageName = "stage-0-2";

            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(fixtureFolder));
                Assert.That(
                    AssetDatabase.CopyAsset(GetAuthoringObjectiveStagePath(stageName), authoringPath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(GetGeneratedObjectiveStagePath(stageName), gameplayPath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(
                        $"{ObjectiveStageRoot}/{stageName}/{stageName}_Presentation.asset",
                        presentationPath),
                    Is.True);

                var authoring = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(authoringPath);
                var gameplay = AssetDatabase.LoadAssetAtPath<StageDefinition>(gameplayPath);
                var presentation =
                    AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(presentationPath);
                authoring.AssignGeneratedDefinitions(gameplay, presentation);
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssetIfDirty(authoring);

                var sentinel = ScriptableObject.CreateInstance<TestSentinelAsset>();
                sentinel.SetValue("saved-baseline");
                AssetDatabase.CreateAsset(sentinel, sentinelPath);
                AssetDatabase.SaveAssetIfDirty(sentinel);
                var sentinelDiskBefore = ReadAssetBytes(sentinelPath);

                SetAuthoringLabel(authoring, "primary-goal", "Scoped Save Persistence Label");
                sentinel.SetValue("unsaved-memory-value");
                EditorUtility.SetDirty(sentinel);

                var report = StageAuthoringGenerator.Generate(
                    authoring,
                    StageAuthoringGenerateOptions.WriteAll);

                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(ReadAssetText(authoringPath), Does.Contain("Scoped Save Persistence Label"));
                Assert.That(ReadAssetText(gameplayPath), Does.Contain("Scoped Save Persistence Label"));
                Assert.That(EditorUtility.IsDirty(authoring), Is.False);
                Assert.That(EditorUtility.IsDirty(gameplay), Is.False);
                Assert.That(EditorUtility.IsDirty(presentation), Is.False);
                Assert.That(ReadAssetBytes(sentinelPath), Is.EqualTo(sentinelDiskBefore));
                Assert.That(EditorUtility.IsDirty(sentinel), Is.True);

                var authoringAfterFirstGenerate = ReadAssetBytes(authoringPath);
                var gameplayAfterFirstGenerate = ReadAssetBytes(gameplayPath);
                var presentationAfterFirstGenerate = ReadAssetBytes(presentationPath);

                report = StageAuthoringGenerator.Generate(
                    authoring,
                    StageAuthoringGenerateOptions.WriteAll);

                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(ReadAssetBytes(authoringPath), Is.EqualTo(authoringAfterFirstGenerate));
                Assert.That(ReadAssetBytes(gameplayPath), Is.EqualTo(gameplayAfterFirstGenerate));
                Assert.That(ReadAssetBytes(presentationPath), Is.EqualTo(presentationAfterFirstGenerate));
                Assert.That(ReadAssetBytes(sentinelPath), Is.EqualTo(sentinelDiskBefore));
                Assert.That(EditorUtility.IsDirty(sentinel), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(fixtureFolder);
            }
        }

        [Test]
        public void CanonicalGenerateSources_DoNotUseProjectWideSaveAssets()
        {
            var canonicalSources = new[]
            {
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGeneratedAssetWriter.cs",
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGenerator.cs",
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGridWindow.cs",
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringDefinitionEditor.cs",
            };

            foreach (var sourcePath in canonicalSources)
            {
                var source = ReadAssetText(sourcePath);
                Assert.That(
                    source,
                    Does.Not.Contain("AssetDatabase.SaveAssets("),
                    sourcePath);
            }

            var writerSource = ReadAssetText(canonicalSources[0]);
            Assert.That(
                writerSource,
                Does.Contain("StageAuthoringGenerationSaveSet.SaveTouchedAssets(plan, options);"));
            Assert.That(writerSource, Does.Contain("AssetDatabase.SaveAssetIfDirty("));
            Assert.That(writerSource, Does.Not.Contain("Resources.FindObjectsOfTypeAll"));
            Assert.That(writerSource, Does.Not.Contain("AssetDatabase.GetAllAssetPaths"));

            var gridWindowSource = ReadAssetText(canonicalSources[2]);
            Assert.That(gridWindowSource, Does.Not.Contain("skipSave"));
            Assert.That(gridWindowSource, Does.Not.Contain("AddressableAssetSettings"));
        }

        private static void AssertLabelLifecycle(
            CampaignPairFixture fixture,
            string stableConditionId,
            string temporaryLabel)
        {
            var originalRows = fixture.Window.GetObjectiveConditionRowsForTests();
            var row = originalRows.Single(candidate =>
                candidate.StableConditionId == stableConditionId);
            var originalEntry = fixture.Authoring.Objective.ConditionEntries[row.EntryIndex];
            var generatedBefore = EditorJsonUtility.ToJson(fixture.Gameplay);
            var generatedBoardBefore = fixture.Gameplay.Board;
            var generatedPlayerSpawnsBefore = fixture.Gameplay.PlayerSpawns.ToArray();
            var generatedBoxSpawnsBefore = fixture.Gameplay.BoxSpawns.ToArray();
            var generatedEnemySpawnsBefore = fixture.Gameplay.EnemySpawns.ToArray();
            var generatedWallSpawnsBefore = fixture.Gameplay.WallSpawns.ToArray();
            var generatedZonesBefore = fixture.Gameplay.Zones.ToArray();
            var generatedTileFeaturesBefore = fixture.Gameplay.TileFeatures.ToArray();
            var presentationBefore = EditorJsonUtility.ToJson(fixture.Presentation);
            var conditionBefore = EditorJsonUtility.ToJson(row.Condition);
            var authoringEntriesBefore = fixture.Authoring.Objective.ConditionEntries.ToArray();
            var authoringDiskBefore = fixture.ReadAuthoringBytes();
            var generatedDiskBefore = fixture.ReadGameplayBytes();
            var sentinelDiskBefore = fixture.ReadSentinelBytes();
            Undo.ClearAll();

            Assert.That(
                fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));

            Assert.That(
                fixture.Window.SelectObjectiveConditionForTests(row.StableConditionId, row.Condition),
                Is.True);
            Assert.That(
                fixture.Window.SetSelectedObjectiveAuthoringLabelForTests(temporaryLabel, out var error),
                Is.True,
                error);
            Undo.FlushUndoRecordObjects();

            Assert.That(EditorUtility.IsDirty(fixture.Authoring), Is.True);
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries[row.EntryIndex].AuthoringLabel,
                Is.EqualTo(temporaryLabel));
            AssertOnlySelectedAuthoringLabelChanged(
                authoringEntriesBefore,
                fixture.Authoring.Objective.ConditionEntries,
                row.EntryIndex,
                temporaryLabel);
            Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(generatedBefore));
            Assert.That(fixture.ReadAuthoringBytes(), Is.EqualTo(authoringDiskBefore));
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(generatedDiskBefore));
            Assert.That(fixture.Window.HasObjectiveGeneratedDriftForTests(), Is.True);
            var editedFeedback = fixture.Window.GetObjectiveConditionFeedbackForTests();
            Assert.That(
                editedFeedback.Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));
            Assert.That(editedFeedback.ValidationIssues, Is.Empty);
            Assert.That(
                fixture.Window.ResolveObjectiveConditionSelectionForTests(),
                Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));

            Undo.PerformUndo();
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries[row.EntryIndex].AuthoringLabel,
                Is.EqualTo(originalEntry.AuthoringLabel));
            Assert.That(
                fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));
            Undo.PerformRedo();
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries[row.EntryIndex].AuthoringLabel,
                Is.EqualTo(temporaryLabel));
            Assert.That(
                fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));

            fixture.DirtySentinel();

            fixture.Window.GenerateForTests();

            Assert.That(
                fixture.Window.LastReportForTests.HasErrors,
                Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(
                fixture.Gameplay.Objective.ConditionEntries[row.EntryIndex].AuthoringLabel,
                Is.EqualTo(temporaryLabel));
            Assert.That(fixture.Gameplay.Board, Is.EqualTo(generatedBoardBefore));
            Assert.That(fixture.Gameplay.PlayerSpawns, Is.EqualTo(generatedPlayerSpawnsBefore));
            Assert.That(fixture.Gameplay.BoxSpawns, Is.EqualTo(generatedBoxSpawnsBefore));
            Assert.That(fixture.Gameplay.EnemySpawns, Is.EqualTo(generatedEnemySpawnsBefore));
            Assert.That(fixture.Gameplay.WallSpawns, Is.EqualTo(generatedWallSpawnsBefore));
            Assert.That(fixture.Gameplay.Zones, Is.EqualTo(generatedZonesBefore));
            Assert.That(fixture.Gameplay.TileFeatures, Is.EqualTo(generatedTileFeaturesBefore));
            Assert.That(EditorJsonUtility.ToJson(fixture.Presentation), Is.EqualTo(presentationBefore));
            Assert.That(EditorJsonUtility.ToJson(row.Condition), Is.EqualTo(conditionBefore));
            Assert.That(fixture.ReadAuthoringText(), Does.Contain(temporaryLabel));
            Assert.That(fixture.ReadGameplayText(), Does.Contain(temporaryLabel));
            Assert.That(EditorUtility.IsDirty(fixture.Authoring), Is.False);
            Assert.That(EditorUtility.IsDirty(fixture.Gameplay), Is.False);
            Assert.That(EditorUtility.IsDirty(fixture.Presentation), Is.False);
            Assert.That(fixture.ReadSentinelBytes(), Is.EqualTo(sentinelDiskBefore));
            Assert.That(EditorUtility.IsDirty(fixture.Sentinel), Is.True);
            AssertPairParity(fixture.Authoring, fixture.Gameplay);
            Assert.That(fixture.Window.HasObjectiveGeneratedDriftForTests(), Is.False);
            Assert.That(
                fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));

            var generatedAfterFirstGenerate = EditorJsonUtility.ToJson(fixture.Gameplay);
            var authoringDiskAfterFirstGenerate = fixture.ReadAuthoringBytes();
            var gameplayDiskAfterFirstGenerate = fixture.ReadGameplayBytes();
            var presentationDiskAfterFirstGenerate = fixture.ReadPresentationBytes();
            fixture.Window.GenerateForTests();
            Assert.That(
                fixture.Window.LastReportForTests.HasErrors,
                Is.False,
                FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(
                EditorJsonUtility.ToJson(fixture.Gameplay),
                Is.EqualTo(generatedAfterFirstGenerate));
            Assert.That(fixture.ReadAuthoringBytes(), Is.EqualTo(authoringDiskAfterFirstGenerate));
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(gameplayDiskAfterFirstGenerate));
            Assert.That(fixture.ReadPresentationBytes(), Is.EqualTo(presentationDiskAfterFirstGenerate));
            Assert.That(fixture.ReadSentinelBytes(), Is.EqualTo(sentinelDiskBefore));
            Assert.That(EditorUtility.IsDirty(fixture.Sentinel), Is.True);
            AssertPairParity(fixture.Authoring, fixture.Gameplay);
            Assert.That(
                fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));
        }

        private static void AssertOnlySelectedAuthoringLabelChanged(
            IReadOnlyList<StageObjectiveConditionEntry> before,
            IReadOnlyList<StageObjectiveConditionEntry> after,
            int selectedIndex,
            string expectedLabel)
        {
            Assert.That(after.Count, Is.EqualTo(before.Count));
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i].Condition, Is.SameAs(before[i].Condition), $"entry[{i}].Condition");
                Assert.That(after[i].Required, Is.EqualTo(before[i].Required), $"entry[{i}].Required");
                Assert.That(after[i].Role, Is.EqualTo(before[i].Role), $"entry[{i}].Role");
                Assert.That(
                    after[i].StableConditionId,
                    Is.EqualTo(before[i].StableConditionId),
                    $"entry[{i}].StableConditionId");
                Assert.That(after[i].SortOrder, Is.EqualTo(before[i].SortOrder), $"entry[{i}].SortOrder");
                Assert.That(
                    after[i].AuthoringLabel,
                    Is.EqualTo(i == selectedIndex ? expectedLabel : before[i].AuthoringLabel),
                    $"entry[{i}].AuthoringLabel");
            }
        }

        private static void AssertPairParity(
            StageAuthoringDefinition authoring,
            StageDefinition gameplay)
        {
            var expected = authoring.Objective.ConditionEntries;
            var actual = gameplay.Objective.ConditionEntries;
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].Condition, Is.SameAs(expected[i].Condition), $"entry[{i}].Condition");
                Assert.That(actual[i].Required, Is.EqualTo(expected[i].Required), $"entry[{i}].Required");
                Assert.That(actual[i].Role, Is.EqualTo(expected[i].Role), $"entry[{i}].Role");
                Assert.That(
                    actual[i].StableConditionId,
                    Is.EqualTo(expected[i].StableConditionId),
                    $"entry[{i}].StableConditionId");
                Assert.That(
                    actual[i].AuthoringLabel,
                    Is.EqualTo(expected[i].AuthoringLabel.Trim()),
                    $"entry[{i}].AuthoringLabel");
                Assert.That(actual[i].SortOrder, Is.EqualTo(expected[i].SortOrder), $"entry[{i}].SortOrder");
            }
        }

        private static StageAuthoringDefinition LoadAuthoring(string stageName)
        {
            var path = GetAuthoringObjectiveStagePath(stageName);
            var authoring = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(path);
            Assert.That(authoring, Is.Not.Null, path);
            return authoring;
        }

        private static string GetGeneratedObjectiveStagePath(string stageName)
        {
            return $"{ObjectiveStageRoot}/{stageName}/{stageName}.asset";
        }

        private static string GetAuthoringObjectiveStagePath(string stageName)
        {
            return $"{ObjectiveStageRoot}/{stageName}/{stageName}_Authoring.asset";
        }

        private static void SetAuthoringLabel(
            StageAuthoringDefinition authoring,
            string stableConditionId,
            string label)
        {
            var serialized = new SerializedObject(authoring);
            var entries = serialized.FindProperty("objective").FindPropertyRelative("ConditionEntries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("StableConditionId").stringValue != stableConditionId)
                {
                    continue;
                }

                entry.FindPropertyRelative("AuthoringLabel").stringValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(authoring);
                return;
            }

            Assert.Fail($"Missing objective condition '{stableConditionId}'.");
        }

        private static void SetGeneratedAuthoringLabel(
            StageDefinition gameplay,
            string stableConditionId,
            string label)
        {
            var serialized = new SerializedObject(gameplay);
            var entries = serialized.FindProperty("objective").FindPropertyRelative("ConditionEntries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("StableConditionId").stringValue != stableConditionId)
                {
                    continue;
                }

                entry.FindPropertyRelative("AuthoringLabel").stringValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gameplay);
                return;
            }

            Assert.Fail($"Missing generated objective condition '{stableConditionId}'.");
        }

        private static ButtonActivatedConditionAsset CreateButtonActivatedCondition(int tileId)
        {
            var condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            var serialized = new SerializedObject(condition);
            serialized.FindProperty("tileId").intValue = tileId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return condition;
        }

        private static byte[] ReadAssetBytes(string assetPath)
        {
            return File.ReadAllBytes(ToAbsoluteProjectPath(assetPath));
        }

        private static string ReadAssetText(string assetPath)
        {
            return File.ReadAllText(ToAbsoluteProjectPath(assetPath));
        }

        private static StageObjectiveAuthoring Objective(params StageObjectiveConditionEntry[] entries)
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                ObjectiveTitle = "Objective",
                ObjectiveSummary = "Objective summary",
                ConditionEntries = entries,
            };
        }

        private static StageObjectiveConditionEditorRow PrimaryRow(
            PlayerAtAnyZoneConditionAsset condition,
            bool required,
            StageObjectiveConditionRole role = StageObjectiveConditionRole.PrimaryGoal)
        {
            return new StageObjectiveConditionEditorRow(
                0,
                "primary-goal",
                "Reach the Exit Zone",
                role,
                required,
                0,
                condition,
                nameof(PlayerAtAnyZoneConditionAsset),
                StageObjectiveConditionEditorCategory.PrimaryGoal,
                StageObjectiveConditionEditorAssociationKind.PrimaryGoal,
                null,
                null,
                null,
                false,
                string.Empty);
        }

        private static StageObjectiveConditionEditorRow OtherRow(
            StageConditionAsset condition,
            int entryIndex)
        {
            return new StageObjectiveConditionEditorRow(
                entryIndex,
                "unrelated",
                "Unrelated",
                StageObjectiveConditionRole.Challenge,
                true,
                10,
                condition,
                condition.GetType().Name,
                StageObjectiveConditionEditorCategory.OtherCondition,
                StageObjectiveConditionEditorAssociationKind.Other,
                null,
                null,
                null,
                false,
                string.Empty);
        }

        private static StageObjectiveConditionEditorRow ButtonRow(
            ButtonActivatedConditionAsset condition,
            int entryIndex,
            string stableConditionId,
            bool required = true,
            StageObjectiveConditionRole role = StageObjectiveConditionRole.SecondaryGoal)
        {
            return new StageObjectiveConditionEditorRow(
                entryIndex,
                stableConditionId,
                "Button",
                role,
                required,
                10 + entryIndex * 10,
                condition,
                nameof(ButtonActivatedConditionAsset),
                StageObjectiveConditionEditorCategory.ButtonObjective,
                StageObjectiveConditionEditorAssociationKind.Button,
                condition.TileId,
                null,
                null,
                false,
                string.Empty);
        }

        private static StageTileFeatureDefinition ButtonFeature(int tileId)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Kind = TileFeatureKind.Button,
                Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
            };
        }

        private static StageObjectiveConditionEntry Entry(
            StageConditionAsset condition,
            string stableConditionId,
            string authoringLabel,
            StageObjectiveConditionRole role,
            int sortOrder)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = true,
                Role = role,
                StableConditionId = stableConditionId,
                AuthoringLabel = authoringLabel,
                SortOrder = sortOrder,
            };
        }

        private static string FormatIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(
                "\n",
                report.Issues.Select(issue => $"{issue.Severity}:{issue.Code}:{issue.Message}"));
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private sealed class FixedRemovalConfirmation : IStageButtonObjectiveRemovalConfirmation
        {
            private readonly bool result;

            public FixedRemovalConfirmation(bool result)
            {
                this.result = result;
            }

            public bool Confirm(StageButtonObjectiveRemovalPlan plan)
            {
                return result;
            }
        }

        private sealed class CampaignPairFixture : IDisposable
        {
            private CampaignPairFixture(
                string folderPath,
                string authoringPath,
                string gameplayPath,
                string presentationPath,
                string sentinelPath,
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StagePresentationDefinition presentation,
                TestSentinelAsset sentinel,
                StageAuthoringGridWindow window)
            {
                FolderPath = folderPath;
                AuthoringPath = authoringPath;
                GameplayPath = gameplayPath;
                PresentationPath = presentationPath;
                SentinelPath = sentinelPath;
                Authoring = authoring;
                Gameplay = gameplay;
                Presentation = presentation;
                Sentinel = sentinel;
                Window = window;
            }

            private string FolderPath { get; }

            private string AuthoringPath { get; }

            private string GameplayPath { get; }

            private string PresentationPath { get; }

            private string SentinelPath { get; }

            public StageAuthoringDefinition Authoring { get; }

            public StageDefinition Gameplay { get; }

            public StagePresentationDefinition Presentation { get; }

            public TestSentinelAsset Sentinel { get; }

            public StageAuthoringGridWindow Window { get; }

            public static CampaignPairFixture Create(string stageName)
            {
                var folderPath = $"Assets/__ObjectiveConditionEditorM1_{Guid.NewGuid():N}";
                var authoringPath = $"{folderPath}/Authoring.asset";
                var gameplayPath = $"{folderPath}/Generated.asset";
                var presentationPath = $"{folderPath}/Presentation.asset";
                var sentinelPath = $"{folderPath}/UnrelatedDirtySentinel.asset";
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folderPath));
                Assert.That(
                    AssetDatabase.CopyAsset(GetAuthoringObjectiveStagePath(stageName), authoringPath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(GetGeneratedObjectiveStagePath(stageName), gameplayPath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(
                        $"{ObjectiveStageRoot}/{stageName}/{stageName}_Presentation.asset",
                        presentationPath),
                    Is.True);

                var authoring = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(authoringPath);
                var gameplay = AssetDatabase.LoadAssetAtPath<StageDefinition>(gameplayPath);
                var presentation =
                    AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(presentationPath);
                authoring.AssignGeneratedDefinitions(gameplay, presentation);
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssetIfDirty(authoring);

                var sentinel = ScriptableObject.CreateInstance<TestSentinelAsset>();
                sentinel.SetValue("saved-baseline");
                AssetDatabase.CreateAsset(sentinel, sentinelPath);
                AssetDatabase.SaveAssetIfDirty(sentinel);

                var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
                window.BindForTests(authoring);
                return new CampaignPairFixture(
                    folderPath,
                    authoringPath,
                    gameplayPath,
                    presentationPath,
                    sentinelPath,
                    authoring,
                    gameplay,
                    presentation,
                    sentinel,
                    window);
            }

            public void DirtySentinel()
            {
                Sentinel.SetValue("unsaved-memory-value");
                EditorUtility.SetDirty(Sentinel);
            }

            public byte[] ReadAuthoringBytes()
            {
                return File.ReadAllBytes(ToAbsoluteProjectPath(AuthoringPath));
            }

            public string ReadAuthoringText()
            {
                return File.ReadAllText(ToAbsoluteProjectPath(AuthoringPath));
            }

            public byte[] ReadGameplayBytes()
            {
                return File.ReadAllBytes(ToAbsoluteProjectPath(GameplayPath));
            }

            public string ReadGameplayText()
            {
                return File.ReadAllText(ToAbsoluteProjectPath(GameplayPath));
            }

            public byte[] ReadPresentationBytes()
            {
                return File.ReadAllBytes(ToAbsoluteProjectPath(PresentationPath));
            }

            public byte[] ReadSentinelBytes()
            {
                return File.ReadAllBytes(ToAbsoluteProjectPath(SentinelPath));
            }

            public void Dispose()
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(Window);
                AssetDatabase.DeleteAsset(FolderPath);
            }
        }
    }

}
