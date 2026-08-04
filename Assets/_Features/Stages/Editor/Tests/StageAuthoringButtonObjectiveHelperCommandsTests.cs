using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringButtonObjectiveHelperCommandsTests
    {
        [Test]
        public void TryAddRequiredSecondaryGoal_SelectedButton_CreatesConditionAndObjectiveEntry()
        {
            using var fixture = TempStageContentFixture.Create();

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(AssetDatabase.LoadAssetAtPath<ButtonActivatedConditionAsset>(fixture.ExpectedButtonConditionPath), Is.Not.Null);
            var entry = fixture.Authoring.Objective.ConditionEntries.Single();
            Assert.That(entry.Condition, Is.TypeOf<ButtonActivatedConditionAsset>());
            Assert.That(((ButtonActivatedConditionAsset)entry.Condition).TileId, Is.EqualTo(901));
            Assert.That(entry.Required, Is.True);
            Assert.That(entry.Role, Is.EqualTo(StageObjectiveConditionRole.SecondaryGoal));
            Assert.That(entry.StableConditionId, Is.EqualTo("button-901"));
            Assert.That(entry.AuthoringLabel, Is.EqualTo("Place a push box on the button"));
            Assert.That(entry.SortOrder, Is.EqualTo(10));
            Assert.That(fixture.Authoring.Objective.CompletionPolicy, Is.EqualTo(StageCompletionPolicy.RequireAllConditions));
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_NonButton_Fails()
        {
            using var fixture = TempStageContentFixture.Create(tileFeatures: new[] { TileFeature(901, TileFeatureKind.Exit, Cell(1, 1)) });

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Authoring.TileFeatures[0],
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_ExistingSameTileCondition_ReusesAsset()
        {
            using var fixture = TempStageContentFixture.Create();
            var existing = fixture.CreateExpectedButtonCondition(901);

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value,
                "Activate QA button");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Single().Condition, Is.SameAs(existing));
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Single().AuthoringLabel, Is.EqualTo("Activate QA button"));
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_MoonBlockOnlyButton_UsesMoonBlockAuthoringLabel()
        {
            using var fixture = TempStageContentFixture.Create(tileFeatures: new[]
            {
                TileFeature(901, TileFeatureKind.Button, Cell(1, 1), TileFeatureBoxSelector.MoonBlockOnly),
            });

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Single().AuthoringLabel,
                Is.EqualTo("Place the MoonBlock on the button"));
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_ExistingDifferentTileAtPath_Fails()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.CreateExpectedButtonCondition(902);

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("references TileId 902"));
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_DuplicateTileEntry_DoesNotAddAgain()
        {
            using var fixture = TempStageContentFixture.Create();
            var condition = CreateButtonActivatedCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                Condition(condition, true, StageObjectiveConditionRole.SecondaryGoal, "custom-button")));

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Has.Length.EqualTo(1));
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_DuplicateStableConditionId_DoesNotAddAgain()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                Condition(CreateButtonActivatedCondition(902), true, StageObjectiveConditionRole.SecondaryGoal, "button-901")));

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Has.Length.EqualTo(1));
            Assert.That(StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(fixture.Authoring, fixture.Button).State,
                Is.EqualTo(ButtonObjectiveLinkState.StableConditionIdConflict));
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_ObjectiveDisabled_SetsRequireAllConditions()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.Authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Authoring.Objective.CompletionPolicy, Is.EqualTo(StageCompletionPolicy.RequireAllConditions));
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_ExitStageWithoutPrimaryGoal_Fails()
        {
            using var fixture = TempStageContentFixture.Create(tileFeatures: new[]
            {
                TileFeature(1, TileFeatureKind.Exit, Cell(0, 0)),
                TileFeature(901, TileFeatureKind.Button, Cell(1, 1)),
            });
            fixture.Authoring.SetObjective(Objective(StageCompletionPolicy.RequireAllConditions));

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Exit PrimaryGoal"));
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);
        }

        [Test]
        public void ObjectiveConditionSortOrder_ButtonHelper_MaxSecondaryOverflow_FailsBeforeAnyMutation()
        {
            using var fixture = TempStageContentFixture.Create();
            var boundaryEntry = Condition(
                null,
                true,
                StageObjectiveConditionRole.SecondaryGoal,
                "existing-secondary");
            boundaryEntry.SortOrder = int.MaxValue;
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                boundaryEntry));
            EditorUtility.SetDirty(fixture.Authoring);
            AssetDatabase.SaveAssetIfDirty(fixture.Authoring);
            var authoringBefore = EditorJsonUtility.ToJson(fixture.Authoring);
            var assetPath = AssetDatabase.GetAssetPath(fixture.Authoring);
            var diskBefore = File.ReadAllBytes(ToAbsoluteProjectPath(assetPath));

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("No additional automatic Sort Order can be allocated."));
            Assert.That(EditorJsonUtility.ToJson(fixture.Authoring), Is.EqualTo(authoringBefore));
            Assert.That(File.ReadAllBytes(ToAbsoluteProjectPath(assetPath)), Is.EqualTo(diskBefore));
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fixture.ExpectedButtonConditionPath), Is.Null);
        }

        [Test]
        public void ObjectiveConditionSortOrder_ButtonHelper_ExactAllocationBoundary_UsesIntMaxValue()
        {
            using var fixture = TempStageContentFixture.Create();
            var boundaryEntry = Condition(
                null,
                true,
                StageObjectiveConditionRole.SecondaryGoal,
                "existing-secondary");
            boundaryEntry.SortOrder = int.MaxValue - 10;
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                boundaryEntry));

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Has.Length.EqualTo(2));
            Assert.That(fixture.Authoring.Objective.ConditionEntries[0].SortOrder, Is.EqualTo(int.MaxValue - 10));
            Assert.That(fixture.Authoring.Objective.ConditionEntries[1].SortOrder, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void AllocateSortOrder_ConsidersEntriesAcrossAllGovernedRoles()
        {
            using var fixture = TempStageContentFixture.Create();
            var primary = Condition(null, true, StageObjectiveConditionRole.PrimaryGoal, "primary");
            primary.SortOrder = 10;
            var secondary = Condition(null, true, StageObjectiveConditionRole.SecondaryGoal, "secondary");
            secondary.SortOrder = 20;
            var challenge = Condition(null, false, StageObjectiveConditionRole.Challenge, "challenge");
            challenge.SortOrder = 30;
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                primary,
                secondary,
                challenge));

            var result = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);

            Assert.That(result.Succeeded, Is.True, result.Message);
            var sortOrders = fixture.Authoring.Objective.ConditionEntries
                .Select(entry => entry.SortOrder)
                .ToArray();
            Assert.That(sortOrders, Is.EqualTo(new[] { 10, 20, 30, 40 }));
            Assert.That(sortOrders.Distinct().Count(), Is.EqualTo(sortOrders.Length));
        }

        [Test]
        public void TryRemoveRequiredSecondaryGoal_LinkedButton_RemovesEntryButKeepsAsset()
        {
            using var fixture = TempStageContentFixture.Create();
            var addResult = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);
            Assert.That(addResult.Succeeded, Is.True, addResult.Message);
            var condition = AssetDatabase.LoadAssetAtPath<ButtonActivatedConditionAsset>(fixture.ExpectedButtonConditionPath);

            var serializedAuthoring = new SerializedObject(fixture.Authoring);
            var plan = StageButtonObjectiveRemovalPlanner.Build(
                serializedAuthoring,
                fixture.Authoring,
                fixture.Button);
            var removeResult = StageButtonObjectiveRemovalExecutor.TryExecute(
                serializedAuthoring,
                fixture.Authoring,
                fixture.Button,
                plan);

            Assert.That(removeResult.Succeeded, Is.True, removeResult.Message);
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<ButtonActivatedConditionAsset>(fixture.ExpectedButtonConditionPath), Is.SameAs(condition));
        }

        [Test]
        public void GetLinkStatus_InvalidAsset_ReportsConditionAssetInvalid()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                Condition(CreatePlayerAtAnyZone("goal"), true, StageObjectiveConditionRole.SecondaryGoal, "button-901")));

            var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(fixture.Authoring, fixture.Button);

            Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.ConditionAssetInvalid));
        }

        [Test]
        public void InvalidButtonConditionType_DoesNotNavigate()
        {
            var snapshot = CaptureInvalidButtonConditionTypeNavigation();

            Assert.That(snapshot.LinkState, Is.EqualTo(ButtonObjectiveLinkState.ConditionAssetInvalid));
            Assert.That(snapshot.SelectedRow, Is.False);
        }

        [Test]
        public void InvalidButtonConditionType_PreservesWarning()
        {
            var snapshot = CaptureInvalidButtonConditionTypeNavigation();

            Assert.That(snapshot.Warning,
                Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
        }

        [Test]
        public void InvalidButtonConditionType_SelectsNoRow()
        {
            var snapshot = CaptureInvalidButtonConditionTypeNavigation();

            Assert.That(snapshot.ConditionAssetWasNull, Is.True);
            Assert.That(snapshot.SelectedRow, Is.False);
        }

        [Test]
        public void ValidLinkedButtonStatus_SelectsCanonicalRow()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Canonical button", 10)));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(
                    fixture.Authoring,
                    fixture.Button);
                window.BindForTests(fixture.Authoring);
                Assert.That(window.SelectTileFeatureByIdForTests(901), Is.True);

                Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.Linked));
                Assert.That(status.ConditionAsset, Is.SameAs(canonical));
                Assert.That(window.GetSelectedObjectiveConditionRowForTests()?.Condition,
                    Is.SameAs(canonical));
                Assert.That(window.ObjectiveContextWarningForTests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReferenceMismatch_RemainsConflict()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.CreateExpectedButtonCondition(901);
            var mismatched = CreateButtonActivatedCondition(901);
            try
            {
                fixture.Authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    ButtonEntry(mismatched, "button-901", "Mismatched reference", 10)));

                var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(
                    fixture.Authoring,
                    fixture.Button);
                var plan = StageButtonObjectiveRemovalPlanner.Build(fixture.Authoring, fixture.Button);

                Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.ConditionReferenceMismatch));
                Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mismatched);
            }
        }

        [Test]
        public void DuplicateAssociation_RemainsConflict()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            var duplicate = CreateButtonActivatedCondition(901);
            try
            {
                fixture.Authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    ButtonEntry(canonical, "button-901", "Canonical", 10),
                    ButtonEntry(duplicate, "alternate-button", "Duplicate", 20)));

                var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(
                    fixture.Authoring,
                    fixture.Button);
                var plan = StageButtonObjectiveRemovalPlanner.Build(fixture.Authoring, fixture.Button);

                Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.DuplicateCondition));
                Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void MissingCanonicalCondition_IsUnresolved()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.Authoring.SetObjective(Objective(StageCompletionPolicy.RequireAllConditions));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(
                    fixture.Authoring,
                    fixture.Button);
                window.BindForTests(fixture.Authoring);
                Assert.That(window.SelectTileFeatureByIdForTests(901), Is.True);

                Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.NotLinked));
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Null);
                Assert.That(window.ObjectiveContextWarningForTests,
                    Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void NavigatorAndRemovalPlanner_AgreeForInvalidConditionAsset()
        {
            var snapshot = CaptureInvalidButtonConditionTypeNavigation();

            Assert.That(snapshot.LinkState, Is.EqualTo(ButtonObjectiveLinkState.ConditionAssetInvalid));
            Assert.That(snapshot.SelectedRow, Is.False);
            Assert.That(snapshot.Warning,
                Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));
            Assert.That(snapshot.RemovalMode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
        }

        [Test]
        public void GetLinkStatus_MissingAsset_ReportsConditionAssetMissing()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                Condition(null, true, StageObjectiveConditionRole.SecondaryGoal, "button-901")));

            var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(fixture.Authoring, fixture.Button);

            Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.ConditionAssetMissing));
        }

        [Test]
        public void GetLinkStatus_ConditionReferencesNonButtonTile_ReportsInvalid()
        {
            using var fixture = TempStageContentFixture.Create(tileFeatures: new[]
            {
                TileFeature(901, TileFeatureKind.Exit, Cell(1, 1)),
            });
            var staleButtonSelection = TileFeature(901, TileFeatureKind.Button, Cell(1, 1));
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                Condition(CreateButtonActivatedCondition(901), true, StageObjectiveConditionRole.SecondaryGoal, "button-901")));

            var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(fixture.Authoring, staleButtonSelection);

            Assert.That(status.State, Is.EqualTo(ButtonObjectiveLinkState.ConditionReferencesNonButtonTile));
        }

        [Test]
        public void ExistingExitHelperRegression_StatusStillValid()
        {
            using var fixture = TempStageContentFixture.Create(tileFeatures: new[]
            {
                TileFeature(1, TileFeatureKind.Exit, Cell(1, 1)),
            });
            fixture.Authoring.SetZones(new[] { Zone("goal", Cell(1, 1)) });
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                Condition(CreatePlayerAtAnyZone("goal"), true, StageObjectiveConditionRole.PrimaryGoal, "primary-goal")));

            StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(
                fixture.Authoring,
                1,
                out var status);

            Assert.That(status.Kind, Is.EqualTo(ExitGoalZoneStatusKind.Valid));
        }

        [Test]
        public void StageAuthoringGridWindow_ButtonSelected_ExposesButtonClearConditionFlow()
        {
            using var fixture = TempStageContentFixture.Create();
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(fixture.Authoring);
                window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                window.SelectTileFeatureByIdForTests(901);

                Assert.That(window.GetSelectedButtonObjectiveLinkStatusForTests().State,
                    Is.EqualTo(ButtonObjectiveLinkState.ObjectiveDisabled));
                Assert.That(window.AddSelectedButtonRequiredSecondaryGoalForTests(out var error), Is.True, error);
                Assert.That(window.GetSelectedButtonObjectiveLinkStatusForTests().State,
                    Is.EqualTo(ButtonObjectiveLinkState.Linked));
                var selected = window.GetSelectedObjectiveConditionRowForTests();
                Assert.That(selected, Is.Not.Null);
                Assert.That(selected.StableConditionId, Is.EqualTo("button-901"));
                Assert.That(selected.Condition, Is.SameAs(
                    fixture.Authoring.Objective.ConditionEntries.Single().Condition));
                Assert.That(selected.ButtonTileId, Is.EqualTo(901));

                window.PingSelectedButtonConditionAssetForTests();
                Assert.That(window.GetSelectedObjectiveConditionRowForTests().Condition, Is.SameAs(selected.Condition));

                window.SetButtonObjectiveRemovalConfirmationForTests(new FixedRemovalConfirmation(true));
                Assert.That(window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out error), Is.True, error);
                Assert.That(window.ResolveObjectiveConditionSelectionForTests(),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_NonButtonSelected_ReportsNotButton()
        {
            using var fixture = TempStageContentFixture.Create(tileFeatures: new[] { TileFeature(901, TileFeatureKind.Exit, Cell(1, 1)) });
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(fixture.Authoring);
                window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                window.SelectTileFeatureByIdForTests(901);

                Assert.That(window.GetSelectedButtonObjectiveLinkStatusForTests().State,
                    Is.EqualTo(ButtonObjectiveLinkState.NotButton));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ObjectiveConditionRemoval_CanonicalEntry_ClassifiesSingleCanonical()
        {
            using var fixture = TempStageContentFixture.Create();
            var condition = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(condition, "button-901", "Canonical button", 30)));

            var plan = StageButtonObjectiveRemovalPlanner.Build(
                new SerializedObject(fixture.Authoring),
                fixture.Authoring,
                fixture.Button);

            Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.SingleCanonical));
            Assert.That(plan.Candidates.Count, Is.EqualTo(1));
            Assert.That(plan.Candidates[0].MatchReason,
                Is.EqualTo(StageButtonObjectiveRemovalMatchReason.StableAndTileMatch));
            Assert.That(plan.Candidates[0].ConditionReferenceMatches, Is.True);
        }

        [Test]
        public void ClassifySingleCanonical_ExcludesNonRequiredEntry()
        {
            using var fixture = TempStageContentFixture.Create();
            var condition = fixture.CreateExpectedButtonCondition(901);
            var entry = ButtonEntry(condition, "button-901", "Optional button", 30);
            entry.Required = false;
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                entry));

            var plan = StageButtonObjectiveRemovalPlanner.Build(
                new SerializedObject(fixture.Authoring),
                fixture.Authoring,
                fixture.Button);

            Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
            Assert.That(plan.Candidates, Has.Count.EqualTo(1));
            Assert.That(plan.Candidates[0].Required, Is.False);
            Assert.That(StageButtonObjectiveRemovalConfirmationMessage.BuildRepair(plan),
                Does.Contain("conflicting Button Objective entries"));
        }

        [Test]
        public void ObjectiveConditionRemoval_ConflictVariants_ClassifyConflictRepair()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            var duplicateStable = CreateButtonActivatedCondition(902);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Canonical", 10),
                ButtonEntry(duplicateStable, "button-901", "Stable conflict", 20),
                ButtonEntry(canonical, "custom-button", "Tile conflict", 30)));

            var plan = StageButtonObjectiveRemovalPlanner.Build(
                new SerializedObject(fixture.Authoring),
                fixture.Authoring,
                fixture.Button);

            Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
            Assert.That(plan.Candidates.Count, Is.EqualTo(3));
            Assert.That(plan.Candidates.Select(candidate => candidate.MatchReason), Is.EquivalentTo(new[]
            {
                StageButtonObjectiveRemovalMatchReason.StableAndTileMatch,
                StageButtonObjectiveRemovalMatchReason.StableIdMatch,
                StageButtonObjectiveRemovalMatchReason.TileIdMatch,
            }));
        }

        [Test]
        public void ObjectiveConditionRemoval_SingleIdentityMismatch_ClassifiesConflictRepair()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(CreateButtonActivatedCondition(902), "button-901", "Mismatched", 40)));

            var plan = StageButtonObjectiveRemovalPlanner.Build(
                new SerializedObject(fixture.Authoring),
                fixture.Authoring,
                fixture.Button);

            Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
            Assert.That(plan.Candidates.Count, Is.EqualTo(1));
            Assert.That(plan.Candidates[0].ConditionReferenceMatches, Is.False);
        }

        [Test]
        public void CanonicalButtonConditionReferenceMismatch_FailsClosedAcrossStatusNavigationAndRemoval()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            var mismatched = CreateButtonActivatedCondition(901);
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                fixture.Authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    ButtonEntry(mismatched, "button-901", "Mismatched reference", 30)));

                var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(
                    fixture.Authoring,
                    fixture.Button);

                Assert.That(status.State,
                    Is.EqualTo(ButtonObjectiveLinkState.ConditionReferenceMismatch));
                Assert.That(status.ConditionAsset, Is.SameAs(canonical));
                Assert.That(status.ExpectedConditionPath, Is.EqualTo(fixture.ExpectedButtonConditionPath));
                Assert.That(status.MatchingEntryCount, Is.EqualTo(1));

                window.BindForTests(fixture.Authoring);
                Assert.That(window.SelectTileFeatureByIdForTests(901), Is.True);
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Null);
                Assert.That(window.ObjectiveContextWarningForTests,
                    Is.EqualTo(StageObjectiveConditionContextNavigator.ButtonSelectionUnresolved));

                var plan = StageButtonObjectiveRemovalPlanner.Build(
                    new SerializedObject(fixture.Authoring),
                    fixture.Authoring,
                    fixture.Button);
                Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
                Assert.That(plan.CanonicalCondition, Is.SameAs(canonical));
                Assert.That(plan.Candidates, Has.Count.EqualTo(1));
                Assert.That(plan.Candidates[0].ConditionReferenceMatches, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(mismatched);
            }
        }

        [Test]
        public void ObjectiveConditionRemoval_NoCandidate_ClassifiesUnavailable()
        {
            using var fixture = TempStageContentFixture.Create();

            var plan = StageButtonObjectiveRemovalPlanner.Build(
                new SerializedObject(fixture.Authoring),
                fixture.Authoring,
                fixture.Button);

            Assert.That(plan.Mode, Is.EqualTo(StageButtonObjectiveRemovalMode.Unavailable));
            Assert.That(plan.Candidates, Is.Empty);
        }

        [Test]
        public void ObjectiveConditionRemoval_SingleCancel_PreservesBytesSelectionAndConditionAsset()
        {
            using var fixture = TempStageContentFixture.Create();
            var condition = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(condition, "button-901", "Canonical button", 30)));
            EditorUtility.SetDirty(fixture.Authoring);
            AssetDatabase.SaveAssetIfDirty(fixture.Authoring);
            var authoringPath = AssetDatabase.GetAssetPath(fixture.Authoring);
            var authoringBefore = File.ReadAllBytes(ToAbsoluteProjectPath(authoringPath));
            var conditionBefore = File.ReadAllBytes(ToAbsoluteProjectPath(fixture.ExpectedButtonConditionPath));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(fixture.Authoring);
                window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                window.SelectTileFeatureByIdForTests(901);
                Assert.That(window.SelectObjectiveConditionForTests("button-901", condition), Is.True);
                window.SetButtonObjectiveRemovalConfirmationForTests(new FixedRemovalConfirmation(false));

                Assert.That(window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error), Is.False);

                Assert.That(error, Does.Contain("cancelled"));
                Assert.That(File.ReadAllBytes(ToAbsoluteProjectPath(authoringPath)), Is.EqualTo(authoringBefore));
                Assert.That(File.ReadAllBytes(ToAbsoluteProjectPath(fixture.ExpectedButtonConditionPath)),
                    Is.EqualTo(conditionBefore));
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Not.Null);
                Assert.That(EditorUtility.IsDirty(fixture.Authoring), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ObjectiveConditionRemoval_SingleConfirm_RemovesExactlyOneAndClearsSelection()
        {
            using var fixture = TempStageContentFixture.Create();
            var condition = fixture.CreateExpectedButtonCondition(901);
            var survivor = Condition(CreatePlayerAtAnyZone("goal"), true,
                StageObjectiveConditionRole.PrimaryGoal, "primary-goal");
            survivor.AuthoringLabel = "Primary";
            survivor.SortOrder = 0;
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                survivor,
                ButtonEntry(condition, "button-901", "Canonical button", 30)));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(fixture.Authoring);
                window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                window.SelectTileFeatureByIdForTests(901);
                Assert.That(window.SelectObjectiveConditionForTests("button-901", condition), Is.True);
                window.SetButtonObjectiveRemovalConfirmationForTests(new FixedRemovalConfirmation(true));

                Assert.That(window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out var error), Is.True, error);

                Assert.That(fixture.Authoring.Objective.ConditionEntries, Has.Length.EqualTo(1));
                Assert.That(fixture.Authoring.Objective.ConditionEntries[0].StableConditionId,
                    Is.EqualTo("primary-goal"));
                Assert.That(fixture.Authoring.Objective.ConditionEntries[0].SortOrder, Is.Zero);
                Assert.That(window.ResolveObjectiveConditionSelectionForTests(),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.None));
                Assert.That(window.SelectedTileFeatureIdForTests, Is.EqualTo(901));
                Assert.That(AssetDatabase.LoadAssetAtPath<ButtonActivatedConditionAsset>(
                    fixture.ExpectedButtonConditionPath), Is.SameAs(condition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ObjectiveConditionRemoval_RepairCancel_PreservesCandidateSetAndSelection()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Canonical", 20),
                ButtonEntry(canonical, "tile-match", "Conflict", 40)));
            EditorUtility.SetDirty(fixture.Authoring);
            AssetDatabase.SaveAssetIfDirty(fixture.Authoring);
            var authoringBefore = File.ReadAllBytes(ToAbsoluteProjectPath(
                AssetDatabase.GetAssetPath(fixture.Authoring)));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                window.BindForTests(fixture.Authoring);
                window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                window.SelectTileFeatureByIdForTests(901);
                Assert.That(window.SelectObjectiveConditionForTests("button-901", canonical), Is.True);
                Assert.That(window.GetSelectedButtonObjectiveRemovalPlanForTests().Mode,
                    Is.EqualTo(StageButtonObjectiveRemovalMode.ConflictRepair));
                window.SetButtonObjectiveRemovalConfirmationForTests(new FixedRemovalConfirmation(false));

                Assert.That(window.RemoveSelectedButtonRequiredSecondaryGoalForTests(out _), Is.False);

                Assert.That(fixture.Authoring.Objective.ConditionEntries, Has.Length.EqualTo(2));
                Assert.That(File.ReadAllBytes(ToAbsoluteProjectPath(
                    AssetDatabase.GetAssetPath(fixture.Authoring))), Is.EqualTo(authoringBefore));
                Assert.That(window.GetSelectedObjectiveConditionRowForTests(), Is.Not.Null);
                Assert.That(EditorUtility.IsDirty(fixture.Authoring), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ObjectiveConditionRemoval_RepairConfirm_RemovesConfirmedSetAndPreservesSurvivorOrder()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            var mismatch = CreateButtonActivatedCondition(902);
            var before = Condition(CreatePlayerAtAnyZone("before"), true,
                StageObjectiveConditionRole.PrimaryGoal, "before");
            before.SortOrder = 0;
            var after = Condition(CreatePlayerAtAnyZone("after"), false,
                StageObjectiveConditionRole.Challenge, "after");
            after.SortOrder = 70;
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                before,
                ButtonEntry(canonical, "button-901", "Canonical", 20),
                ButtonEntry(mismatch, "button-901", "Stable conflict", 40),
                after,
                ButtonEntry(canonical, "custom-button", "Tile conflict", 60)));
            var serialized = new SerializedObject(fixture.Authoring);
            var plan = StageButtonObjectiveRemovalPlanner.Build(serialized, fixture.Authoring, fixture.Button);

            var result = StageButtonObjectiveRemovalExecutor.TryExecute(
                serialized,
                fixture.Authoring,
                fixture.Button,
                plan);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.RemovedCount, Is.EqualTo(3));
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(new[] { "before", "after" }));
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.SortOrder),
                Is.EqualTo(new[] { 0, 70 }));
            Assert.That(AssetDatabase.Contains(canonical), Is.True);
        }

        [Test]
        public void ObjectiveConditionRemoval_ChangedDisplayedField_BlocksStalePlanWithoutRemoval()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Before", 30)));
            var serialized = new SerializedObject(fixture.Authoring);
            var plan = StageButtonObjectiveRemovalPlanner.Build(serialized, fixture.Authoring, fixture.Button);
            var objective = fixture.Authoring.Objective;
            var entries = objective.ConditionEntries;
            entries[0].AuthoringLabel = "Changed after dialog";
            objective.ConditionEntries = entries;
            fixture.Authoring.SetObjective(objective);
            var afterExternalChange = EditorJsonUtility.ToJson(fixture.Authoring);

            var result = StageButtonObjectiveRemovalExecutor.TryExecute(
                serialized,
                fixture.Authoring,
                fixture.Button,
                plan);

            Assert.That(result.Code,
                Is.EqualTo(StageButtonObjectiveRemovalResultCode.ButtonObjectiveRemovalTargetChanged));
            Assert.That(EditorJsonUtility.ToJson(fixture.Authoring), Is.EqualTo(afterExternalChange));
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Has.Length.EqualTo(1));
        }

        [TestCase("candidate-added")]
        [TestCase("candidate-removed")]
        [TestCase("stable-id")]
        [TestCase("condition-reference")]
        [TestCase("condition-tile-id")]
        [TestCase("role")]
        [TestCase("sort-order")]
        [TestCase("authoring-label")]
        public void ObjectiveConditionRemoval_ConfirmedPlanFieldChanged_BlocksAllMutation(string mutation)
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Before", 30)));
            var serialized = new SerializedObject(fixture.Authoring);
            var plan = StageButtonObjectiveRemovalPlanner.Build(serialized, fixture.Authoring, fixture.Button);
            var objective = fixture.Authoring.Objective;
            var entries = objective.ConditionEntries.ToList();
            switch (mutation)
            {
                case "candidate-added":
                    entries.Add(ButtonEntry(canonical, "tile-match", "Added", 40));
                    break;
                case "candidate-removed":
                    entries.Clear();
                    break;
                case "stable-id":
                    entries[0] = WithStableId(entries[0], "custom-stable");
                    break;
                case "condition-reference":
                    entries[0] = WithCondition(entries[0], CreateButtonActivatedCondition(901));
                    break;
                case "condition-tile-id":
                    SetButtonConditionTileId(canonical, 902);
                    break;
                case "role":
                    entries[0] = WithRole(entries[0], StageObjectiveConditionRole.Challenge);
                    break;
                case "sort-order":
                    entries[0] = WithSortOrder(entries[0], 31);
                    break;
                case "authoring-label":
                    entries[0] = WithAuthoringLabel(entries[0], "After");
                    break;
            }

            objective.ConditionEntries = entries.ToArray();
            fixture.Authoring.SetObjective(objective);
            var expectedAfterExternalChange = EditorJsonUtility.ToJson(fixture.Authoring);

            var result = StageButtonObjectiveRemovalExecutor.TryExecute(
                serialized,
                fixture.Authoring,
                fixture.Button,
                plan);

            Assert.That(result.Code,
                Is.EqualTo(StageButtonObjectiveRemovalResultCode.ButtonObjectiveRemovalTargetChanged));
            Assert.That(EditorJsonUtility.ToJson(fixture.Authoring), Is.EqualTo(expectedAfterExternalChange));
        }

        [Test]
        public void ObjectiveConditionRemoval_CandidateArrayMovement_WithSameIdentitySet_IsAllowed()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            var first = ButtonEntry(canonical, "button-901", "Stable and tile", 20);
            var second = ButtonEntry(canonical, "tile-match", "Tile only", 40);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                first,
                second));
            var serialized = new SerializedObject(fixture.Authoring);
            var plan = StageButtonObjectiveRemovalPlanner.Build(serialized, fixture.Authoring, fixture.Button);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                second,
                first));

            var result = StageButtonObjectiveRemovalExecutor.TryExecute(
                serialized,
                fixture.Authoring,
                fixture.Button,
                plan);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.RemovedCount, Is.EqualTo(2));
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);
        }

        [Test]
        public void ObjectiveConditionRemoval_StaticArchitectureGuards_KeepRemovalEditorOnlyAndBounded()
        {
            var removalSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageButtonObjectiveRemoval.cs"));
            var gridSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGridWindow.cs"));
            var helperSource = File.ReadAllText(ToAbsoluteProjectPath(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringButtonObjectiveHelperCommands.cs"));

            Assert.That(removalSource, Does.Contain("SingleCanonical"));
            Assert.That(removalSource, Does.Contain("ConflictRepair"));
            Assert.That(removalSource, Does.Contain("BUTTON_OBJECTIVE_REMOVAL_TARGET_CHANGED"));
            Assert.That(removalSource, Does.Contain("OrderByDescending"));
            Assert.That(removalSource, Does.Not.Contain("AssetDatabase.DeleteAsset"));
            Assert.That(removalSource, Does.Not.Contain("AssetDatabase.SaveAssets"));
            Assert.That(removalSource, Does.Not.Contain("StageAuthoringGenerator.Generate"));
            Assert.That(gridSource, Does.Contain("buttonObjectiveRemovalConfirmation.Confirm(plan)"));
            Assert.That(gridSource, Does.Contain("StageButtonObjectiveRemovalExecutor.TryExecute"));
            Assert.That(gridSource, Does.Not.Contain("TryRemoveRequiredSecondaryGoal"));
            Assert.That(helperSource, Does.Contain("CollectButtonObjectiveRepairCandidateIndices"));
            Assert.That(helperSource, Does.Not.Contain("TryRemoveRequiredSecondaryGoal"));
        }

        [Test]
        public void ObjectiveConditionRemoval_RepairUndoRedo_RestoresAllMetadataAndRelativePositions()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            var mismatch = CreateButtonActivatedCondition(902);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Canonical", 20),
                ButtonEntry(mismatch, "button-901", "Mismatch", 40)));
            var expectedBefore = EditorJsonUtility.ToJson(fixture.Authoring);
            var serialized = new SerializedObject(fixture.Authoring);
            var plan = StageButtonObjectiveRemovalPlanner.Build(serialized, fixture.Authoring, fixture.Button);
            Undo.ClearUndo(fixture.Authoring);

            var result = StageButtonObjectiveRemovalExecutor.TryExecute(
                serialized,
                fixture.Authoring,
                fixture.Button,
                plan);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);

            Undo.PerformUndo();
            serialized.Update();
            Assert.That(EditorJsonUtility.ToJson(fixture.Authoring), Is.EqualTo(expectedBefore));

            Undo.PerformRedo();
            serialized.Update();
            Assert.That(fixture.Authoring.Objective.ConditionEntries, Is.Empty);
        }

        [Test]
        public void ObjectiveConditionRemoval_RepairMessage_ListsEveryCandidateAndRetentionWarnings()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Canonical", 20),
                ButtonEntry(canonical, "custom", "Tile conflict", 40)));
            var plan = StageButtonObjectiveRemovalPlanner.Build(
                new SerializedObject(fixture.Authoring), fixture.Authoring, fixture.Button);

            var message = StageButtonObjectiveRemovalConfirmationMessage.BuildRepair(plan);

            Assert.That(message, Does.Contain("Remove 2 conflicting"));
            Assert.That(message, Does.Contain("button-901"));
            Assert.That(message, Does.Contain("custom"));
            Assert.That(message, Does.Contain("Match: Stable ID + Tile ID"));
            Assert.That(message, Does.Contain("Match: Tile ID"));
            Assert.That(message, Does.Contain("condition assets will be retained"));
            Assert.That(message, Does.Contain("will not change until Generate"));
        }

        [Test]
        public void ObjectiveConditionRemoval_SingleMessage_ListsExactTargetAndLifecycleWarnings()
        {
            using var fixture = TempStageContentFixture.Create();
            var canonical = fixture.CreateExpectedButtonCondition(901);
            fixture.Authoring.SetObjective(Objective(
                StageCompletionPolicy.RequireAllConditions,
                ButtonEntry(canonical, "button-901", "Canonical", 30)));
            var plan = StageButtonObjectiveRemovalPlanner.Build(
                new SerializedObject(fixture.Authoring), fixture.Authoring, fixture.Button);

            var message = StageButtonObjectiveRemovalConfirmationMessage.BuildSingle(plan);

            Assert.That(message, Does.Contain("Remove this Button Objective"));
            Assert.That(message, Does.Contain("Canonical"));
            Assert.That(message, Does.Contain("button-901"));
            Assert.That(message, Does.Contain("TileId 901"));
            Assert.That(message, Does.Contain("Any Pushable Box"));
            Assert.That(message, Does.Contain("Sort Order:\n  30"));
            Assert.That(message, Does.Contain("condition asset will be retained"));
            Assert.That(message, Does.Contain("will not change until Generate"));
        }

        private static StageTileFeatureDefinition TileFeature(
            int tileId,
            TileFeatureKind kind,
            SurfaceCell cell,
            TileFeatureBoxSelector boxSelector = TileFeatureBoxSelector.None)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Kind = kind,
                Cell = cell,
                ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                Direction = Direction2D.None,
                BoxSelector = boxSelector != TileFeatureBoxSelector.None
                    ? boxSelector
                    : kind == TileFeatureKind.Button
                    ? TileFeatureBoxSelector.AnyPushableBox
                    : TileFeatureBoxSelector.None,
                BoundEntityId = 0,
                PresentationKey = string.Empty,
            };
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

        private static SurfaceCell Cell(int x, int y)
        {
            return new SurfaceCell(FaceId.Floor, x, y);
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
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
                AuthoringLabel = string.Empty,
                SortOrder = 0,
            };
        }

        private static StageObjectiveConditionEntry ButtonEntry(
            StageConditionAsset condition,
            string stableId,
            string label,
            int sortOrder)
        {
            var entry = Condition(
                condition,
                true,
                StageObjectiveConditionRole.SecondaryGoal,
                stableId);
            entry.AuthoringLabel = label;
            entry.SortOrder = sortOrder;
            return entry;
        }

        private static StageObjectiveConditionEntry WithStableId(
            StageObjectiveConditionEntry entry,
            string stableId)
        {
            entry.StableConditionId = stableId;
            return entry;
        }

        private static StageObjectiveConditionEntry WithCondition(
            StageObjectiveConditionEntry entry,
            StageConditionAsset condition)
        {
            entry.Condition = condition;
            return entry;
        }

        private static StageObjectiveConditionEntry WithRole(
            StageObjectiveConditionEntry entry,
            StageObjectiveConditionRole role)
        {
            entry.Role = role;
            return entry;
        }

        private static StageObjectiveConditionEntry WithSortOrder(
            StageObjectiveConditionEntry entry,
            int sortOrder)
        {
            entry.SortOrder = sortOrder;
            return entry;
        }

        private static StageObjectiveConditionEntry WithAuthoringLabel(
            StageObjectiveConditionEntry entry,
            string authoringLabel)
        {
            entry.AuthoringLabel = authoringLabel;
            return entry;
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
            SetButtonConditionTileId(condition, tileId);
            return condition;
        }

        private static InvalidButtonNavigationSnapshot CaptureInvalidButtonConditionTypeNavigation()
        {
            using var fixture = TempStageContentFixture.Create();
            fixture.CreateInvalidExpectedButtonCondition();
            var rowCondition = CreateButtonActivatedCondition(901);
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                fixture.Authoring.SetObjective(Objective(
                    StageCompletionPolicy.RequireAllConditions,
                    ButtonEntry(rowCondition, "button-901", "Canonical-looking row", 10)));
                var status = StageAuthoringButtonObjectiveHelperCommands.GetLinkStatus(
                    fixture.Authoring,
                    fixture.Button);

                window.BindForTests(fixture.Authoring);
                window.SelectTileFeatureByIdForTests(901);
                var plan = StageButtonObjectiveRemovalPlanner.Build(fixture.Authoring, fixture.Button);

                return new InvalidButtonNavigationSnapshot(
                    status.State,
                    status.ConditionAsset == null,
                    window.GetSelectedObjectiveConditionRowForTests() != null,
                    window.ObjectiveContextWarningForTests,
                    plan.Mode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(rowCondition);
            }
        }

        private static void SetButtonConditionTileId(ButtonActivatedConditionAsset condition, int tileId)
        {
            var serializedObject = new SerializedObject(condition);
            serializedObject.FindProperty("tileId").intValue = tileId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class TempStageContentFixture : IDisposable
        {
            private const string CanonicalContentRoot = StageContentPaths.CampaignLevel01StagesRoot;

            private TempStageContentFixture(
                string stageFolder,
                StageContentEntry entry,
                StageAuthoringDefinition authoring)
            {
                StageFolder = stageFolder;
                Entry = entry;
                Authoring = authoring;
                ExpectedButtonConditionPath =
                    $"{StageContentPaths.SharedConditionsRoot}/CampaignMain_{SanitizeName(entry.StageId.Value)}_ButtonActivated_Tile901.asset";
            }

            public string StageFolder { get; }

            public string ExpectedButtonConditionPath { get; }

            public StageContentEntry Entry { get; }

            public StageAuthoringDefinition Authoring { get; }

            public StageTileFeatureDefinition Button => Authoring.TileFeatures.First(feature => feature.TileId == 901);

            public static TempStageContentFixture Create(StageTileFeatureDefinition[] tileFeatures = null)
            {
                var stageIdValue = $"button-helper-test-{Guid.NewGuid():N}".Substring(0, 31);
                var stageFolder = $"{CanonicalContentRoot}/{stageIdValue}";
                EnsureFolder(CanonicalContentRoot);
                EnsureFolder(stageFolder);

                var entry = ScriptableObject.CreateInstance<StageContentEntry>();
                var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
                entry.name = $"{stageIdValue}_Entry";
                authoring.name = $"{stageIdValue}_Authoring";
                entry.AssignStageId(StageId.CreateOrThrow(stageIdValue));
                entry.AssignAuthoringDefinition(authoring);

                AssetDatabase.CreateAsset(entry, $"{stageFolder}/{stageIdValue}_Entry.asset");
                AssetDatabase.CreateAsset(authoring, $"{stageFolder}/{stageIdValue}_Authoring.asset");
                var entryGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry));
                authoring.SetOwnerMetadata(entry, entryGuid);
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(3, 3),
                    InitialBottomFace = FaceId.Floor,
                });
                authoring.SetTileFeatures(tileFeatures ?? new[] { TileFeature(901, TileFeatureKind.Button, Cell(1, 1)) });
                authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());
                EditorUtility.SetDirty(entry);
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssets();
                return new TempStageContentFixture(stageFolder, entry, authoring);
            }

            public ButtonActivatedConditionAsset CreateExpectedButtonCondition(int tileId)
            {
                EnsureFolder(StageContentPaths.SharedConditionsRoot);
                var condition = CreateButtonActivatedCondition(tileId);
                condition.name = System.IO.Path.GetFileNameWithoutExtension(ExpectedButtonConditionPath);
                AssetDatabase.CreateAsset(condition, ExpectedButtonConditionPath);
                AssetDatabase.SaveAssets();
                return condition;
            }

            public StageConditionAsset CreateInvalidExpectedButtonCondition()
            {
                EnsureFolder(StageContentPaths.SharedConditionsRoot);
                var condition = CreatePlayerAtAnyZone("goal");
                condition.name = System.IO.Path.GetFileNameWithoutExtension(ExpectedButtonConditionPath);
                AssetDatabase.CreateAsset(condition, ExpectedButtonConditionPath);
                AssetDatabase.SaveAssets();
                return condition;
            }

            public void Dispose()
            {
                AssetDatabase.DeleteAsset(ExpectedButtonConditionPath);
                AssetDatabase.DeleteAsset(StageFolder);
                AssetDatabase.SaveAssets();
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

            private static string SanitizeName(string value)
            {
                return string.IsNullOrWhiteSpace(value)
                    ? "Unnamed"
                    : string.Concat(value.Split('-', StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            }
        }

        private readonly struct InvalidButtonNavigationSnapshot
        {
            public InvalidButtonNavigationSnapshot(
                ButtonObjectiveLinkState linkState,
                bool conditionAssetWasNull,
                bool selectedRow,
                string warning,
                StageButtonObjectiveRemovalMode removalMode)
            {
                LinkState = linkState;
                ConditionAssetWasNull = conditionAssetWasNull;
                SelectedRow = selectedRow;
                Warning = warning;
                RemovalMode = removalMode;
            }

            public ButtonObjectiveLinkState LinkState { get; }

            public bool ConditionAssetWasNull { get; }

            public bool SelectedRow { get; }

            public string Warning { get; }

            public StageButtonObjectiveRemovalMode RemovalMode { get; }
        }
    }
}
