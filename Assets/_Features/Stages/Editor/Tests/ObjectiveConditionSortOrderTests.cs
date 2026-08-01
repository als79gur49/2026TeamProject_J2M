using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Objectives;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class ObjectiveConditionSortOrderTests
    {
        private const string ObjectiveStageRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/Levels/level-01/Stages";

        [Test]
        public void ObjectiveConditionSortOrder_SecondaryMutation_AllowsPositiveGapOneAndIntMaxWithoutReordering()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var primary = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var selected = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var trailing = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                authoring.SetObjective(Objective(
                    Entry(primary, "primary-goal", StageObjectiveConditionRole.PrimaryGoal, 3),
                    Entry(selected, "secondary-a", StageObjectiveConditionRole.SecondaryGoal, 10),
                    Entry(trailing, "secondary-b", StageObjectiveConditionRole.SecondaryGoal, 30)));
                var serialized = new SerializedObject(authoring);
                var selection = Select(serialized, authoring, "secondary-a");
                var before = authoring.Objective.ConditionEntries.ToArray();

                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                        serialized,
                        authoring,
                        selection,
                        25,
                        out var gapValidation),
                    Is.True,
                    gapValidation.Message);
                Assert.That(gapValidation.IsValid, Is.True);
                AssertOnlySelectedSortOrderChanged(before, authoring.Objective.ConditionEntries, 1, 25);

                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                        serialized,
                        authoring,
                        selection,
                        1,
                        out var oneValidation),
                    Is.True,
                    oneValidation.Message);
                Assert.That(oneValidation.IsValid, Is.True);
                Assert.That(authoring.Objective.ConditionEntries[1].SortOrder, Is.EqualTo(1));

                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                        serialized,
                        authoring,
                        selection,
                        int.MaxValue,
                        out var maxValidation),
                    Is.True,
                    maxValidation.Message);
                Assert.That(maxValidation.IsValid, Is.True);
                Assert.That(authoring.Objective.ConditionEntries[1].SortOrder, Is.EqualTo(int.MaxValue));
                Assert.That(
                    authoring.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                    Is.EqualTo(new[] { "primary-goal", "secondary-a", "secondary-b" }));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(trailing);
                UnityEngine.Object.DestroyImmediate(selected);
                UnityEngine.Object.DestroyImmediate(primary);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void ObjectiveConditionSortOrder_InvalidDuplicateAndIdentityMismatch_AreBlockedBeforeMutation()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var primary = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var selected = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var other = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                authoring.SetObjective(Objective(
                    Entry(primary, "primary-goal", StageObjectiveConditionRole.PrimaryGoal, 3),
                    Entry(selected, "secondary-a", StageObjectiveConditionRole.SecondaryGoal, 10),
                    Entry(other, "secondary-b", StageObjectiveConditionRole.SecondaryGoal, 20)));
                var serialized = new SerializedObject(authoring);
                var selection = Select(serialized, authoring, "secondary-a");
                var before = EditorJsonUtility.ToJson(authoring);

                AssertBlocked(
                    serialized,
                    authoring,
                    selection,
                    0,
                    StageObjectiveConditionSortOrderValidationError.NotPositive,
                    "Secondary Objective Sort Order must be greater than zero.",
                    before);
                AssertBlocked(
                    serialized,
                    authoring,
                    selection,
                    -1,
                    StageObjectiveConditionSortOrderValidationError.NotPositive,
                    "Secondary Objective Sort Order must be greater than zero.",
                    before);

                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                        serialized,
                        authoring,
                        selection,
                        3,
                        out var primaryConflict),
                    Is.False);
                Assert.That(primaryConflict.Error, Is.EqualTo(StageObjectiveConditionSortOrderValidationError.Duplicate));
                Assert.That(primaryConflict.Message, Is.EqualTo("Sort Order 3 is already used by primary-goal."));
                Assert.That(primaryConflict.ConflictingEntryIndex, Is.Zero);
                Assert.That(primaryConflict.ConflictingStableConditionId, Is.EqualTo("primary-goal"));
                Assert.That(primaryConflict.ConflictingRole, Is.EqualTo(StageObjectiveConditionRole.PrimaryGoal));
                Assert.That(primaryConflict.ConflictingSortOrder, Is.EqualTo(3));
                Assert.That(EditorJsonUtility.ToJson(authoring), Is.EqualTo(before));

                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                        serialized,
                        authoring,
                        selection,
                        20,
                        out var secondaryConflict),
                    Is.False);
                Assert.That(secondaryConflict.Message, Is.EqualTo("Sort Order 20 is already used by secondary-b."));
                Assert.That(secondaryConflict.ConflictingEntryIndex, Is.EqualTo(2));
                Assert.That(secondaryConflict.ConflictingRole, Is.EqualTo(StageObjectiveConditionRole.SecondaryGoal));
                Assert.That(EditorJsonUtility.ToJson(authoring), Is.EqualTo(before));

                var mismatchedSelection = new StageObjectiveConditionEditorSelection();
                mismatchedSelection.Select("secondary-a", other);
                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                        serialized,
                        authoring,
                        mismatchedSelection,
                        25,
                        out var identityValidation),
                    Is.False);
                Assert.That(
                    identityValidation.Error,
                    Is.EqualTo(StageObjectiveConditionSortOrderValidationError.IdentityMismatch));
                Assert.That(
                    identityValidation.Message,
                    Is.EqualTo(StageObjectiveConditionEditorSelection.IdentityMismatchMessage));
                Assert.That(EditorJsonUtility.ToJson(authoring), Is.EqualTo(before));
                Assert.That(
                    selection.Resolve(
                        StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring),
                        out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(selected);
                UnityEngine.Object.DestroyImmediate(primary);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void ObjectiveConditionSortOrder_PrimaryOtherAndInvalidRows_AreReadOnlyAndPreserveCurrentPrimaryValue()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var primary = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var other = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            try
            {
                authoring.SetObjective(Objective(
                    Entry(primary, "primary-goal", StageObjectiveConditionRole.PrimaryGoal, 3),
                    Entry(other, "challenge-a", StageObjectiveConditionRole.Challenge, 10),
                    Entry(null, "invalid-a", StageObjectiveConditionRole.SecondaryGoal, 20)));
                var serialized = new SerializedObject(authoring);
                var rows = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring);
                var primarySelection = Select(serialized, authoring, "primary-goal");
                var before = EditorJsonUtility.ToJson(authoring);

                Assert.That(StageObjectiveConditionEditorRenderer.CanEditSortOrder(rows[0]), Is.False);
                Assert.That(StageObjectiveConditionEditorRenderer.CanEditSortOrder(rows[1]), Is.False);
                Assert.That(StageObjectiveConditionEditorRenderer.CanEditSortOrder(rows[2]), Is.False);
                Assert.That(
                    StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                        serialized,
                        authoring,
                        primarySelection,
                        25,
                        out var validation),
                    Is.False);
                Assert.That(validation.Error, Is.EqualTo(StageObjectiveConditionSortOrderValidationError.NotSecondaryGoal));
                Assert.That(authoring.Objective.ConditionEntries[0].SortOrder, Is.EqualTo(3));
                Assert.That(EditorJsonUtility.ToJson(authoring), Is.EqualTo(before));
                Assert.That(
                    primarySelection.Resolve(
                        StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring),
                        out _),
                    Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(primary);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void ObjectiveConditionSortOrder_StageZeroOne_SupportsUndoRedoGenerateParityAndIdempotence()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var row = fixture.Window.GetObjectiveConditionRowsForTests().Single(candidate =>
                candidate.StableConditionId == "button-5");
            Assert.That(row.SortOrder, Is.EqualTo(30));
            var beforeEntries = fixture.Authoring.Objective.ConditionEntries.ToArray();
            var beforeStableIds = beforeEntries.Select(entry => entry.StableConditionId).ToArray();
            var generatedMemoryBefore = EditorJsonUtility.ToJson(fixture.Gameplay);
            var authoringDiskBefore = fixture.ReadAuthoringBytes();
            var gameplayDiskBefore = fixture.ReadGameplayBytes();
            var presentationBefore = fixture.ReadPresentationBytes();
            var conditionBefore = EditorJsonUtility.ToJson(row.Condition);
            var completionPolicyBefore = fixture.Authoring.Objective.CompletionPolicy;
            Undo.ClearAll();

            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));
            Assert.That(fixture.Window.SelectObjectiveConditionForTests(row.StableConditionId, row.Condition), Is.True);
            Assert.That(
                fixture.Window.SetSelectedObjectiveSecondarySortOrderForTests(25, out var validation),
                Is.True,
                validation.Message);
            Undo.FlushUndoRecordObjects();

            AssertOnlySelectedSortOrderChanged(
                beforeEntries,
                fixture.Authoring.Objective.ConditionEntries,
                row.EntryIndex,
                25);
            Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(generatedMemoryBefore));
            Assert.That(fixture.ReadAuthoringBytes(), Is.EqualTo(authoringDiskBefore));
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(gameplayDiskBefore));
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));
            Assert.That(
                fixture.Window.GetObjectiveConditionFeedbackForTests().GetRowMessage(
                    fixture.Window.GetSelectedObjectiveConditionRowForTests()),
                Is.EqualTo("Objective condition SortOrder differs from generated output."));
            Assert.That(fixture.Window.ResolveObjectiveConditionSelectionForTests(),
                Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));

            Undo.PerformUndo();
            Assert.That(fixture.Authoring.Objective.ConditionEntries[row.EntryIndex].SortOrder, Is.EqualTo(30));
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));
            Undo.PerformRedo();
            Assert.That(fixture.Authoring.Objective.ConditionEntries[row.EntryIndex].SortOrder, Is.EqualTo(25));
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.GenerateRequired));

            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False, FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(fixture.Gameplay.Objective.ConditionEntries[row.EntryIndex].SortOrder, Is.EqualTo(25));
            Assert.That(fixture.Authoring.Objective.CompletionPolicy, Is.EqualTo(completionPolicyBefore));
            Assert.That(fixture.Gameplay.Objective.CompletionPolicy, Is.EqualTo(completionPolicyBefore));
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(beforeStableIds));
            Assert.That(
                fixture.Gameplay.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(beforeStableIds));
            Assert.That(EditorJsonUtility.ToJson(row.Condition), Is.EqualTo(conditionBefore));
            Assert.That(fixture.ReadPresentationBytes(), Is.EqualTo(presentationBefore));
            Assert.That(fixture.Window.GetObjectiveConditionFeedbackForTests().Status,
                Is.EqualTo(StageObjectiveConditionEditorStatus.InSync));

            var authoringAfterGenerate = fixture.ReadAuthoringBytes();
            var gameplayAfterGenerate = fixture.ReadGameplayBytes();
            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False, FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(fixture.ReadAuthoringBytes(), Is.EqualTo(authoringAfterGenerate));
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(gameplayAfterGenerate));
            Assert.That(fixture.ReadPresentationBytes(), Is.EqualTo(presentationBefore));
        }

        [Test]
        public void ObjectiveConditionSortOrder_InvalidCampaignEdits_KeepAuthoringGeneratedBytesAndSelection()
        {
            using var fixture = CampaignPairFixture.Create("stage-0-1");
            var row = fixture.Window.GetObjectiveConditionRowsForTests().Single(candidate =>
                candidate.StableConditionId == "button-5");
            var conflicting = fixture.Authoring.Objective.ConditionEntries.First(entry =>
                entry.StableConditionId != row.StableConditionId && entry.SortOrder > 0);
            Assert.That(fixture.Window.SelectObjectiveConditionForTests(row.StableConditionId, row.Condition), Is.True);
            var authoringMemoryBefore = EditorJsonUtility.ToJson(fixture.Authoring);
            var authoringDiskBefore = fixture.ReadAuthoringBytes();
            var gameplayMemoryBefore = EditorJsonUtility.ToJson(fixture.Gameplay);
            var gameplayDiskBefore = fixture.ReadGameplayBytes();

            Assert.That(
                fixture.Window.SetSelectedObjectiveSecondarySortOrderForTests(
                    conflicting.SortOrder,
                    out var duplicateValidation),
                Is.False);
            Assert.That(duplicateValidation.Error, Is.EqualTo(StageObjectiveConditionSortOrderValidationError.Duplicate));
            Assert.That(duplicateValidation.ConflictingStableConditionId, Is.EqualTo(conflicting.StableConditionId));
            Assert.That(
                fixture.Window.SetSelectedObjectiveSecondarySortOrderForTests(0, out var zeroValidation),
                Is.False);
            Assert.That(zeroValidation.Error, Is.EqualTo(StageObjectiveConditionSortOrderValidationError.NotPositive));
            Assert.That(
                fixture.Window.SetSelectedObjectiveSecondarySortOrderForTests(-1, out var negativeValidation),
                Is.False);
            Assert.That(negativeValidation.Error, Is.EqualTo(StageObjectiveConditionSortOrderValidationError.NotPositive));

            Assert.That(EditorJsonUtility.ToJson(fixture.Authoring), Is.EqualTo(authoringMemoryBefore));
            Assert.That(fixture.ReadAuthoringBytes(), Is.EqualTo(authoringDiskBefore));
            Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(gameplayMemoryBefore));
            Assert.That(fixture.ReadGameplayBytes(), Is.EqualTo(gameplayDiskBefore));
            Assert.That(fixture.Window.ResolveObjectiveConditionSelectionForTests(),
                Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
        }

        [Test]
        public void ObjectiveConditionSortOrder_StageThreeOne_PreservesExistingGapsWithoutRenumbering()
        {
            using var fixture = CampaignPairFixture.Create("stage-3-1");
            var beforeEntries = fixture.Authoring.Objective.ConditionEntries.ToArray();
            var beforeIds = beforeEntries.Select(entry => entry.StableConditionId).ToArray();
            var row = fixture.Window.GetObjectiveConditionRowsForTests().Single(candidate =>
                candidate.StableConditionId == "button-8");
            Assert.That(row.SortOrder, Is.EqualTo(20));

            Assert.That(fixture.Window.SelectObjectiveConditionForTests(row.StableConditionId, row.Condition), Is.True);
            Assert.That(
                fixture.Window.SetSelectedObjectiveSecondarySortOrderForTests(25, out var validation),
                Is.True,
                validation.Message);
            AssertOnlySelectedSortOrderChanged(
                beforeEntries,
                fixture.Authoring.Objective.ConditionEntries,
                row.EntryIndex,
                25);
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(beforeIds));
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.SortOrder),
                Is.EqualTo(new[] { 0, 10, 25, 50, 70, 90, 110, 120, 130 }));

            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False, FormatIssues(fixture.Window.LastReportForTests));
            var runtimeEntries = StageRuntimeBuilder.Build(fixture.Gameplay)
                .ObjectiveRuntimeDefinition
                .ConditionEntries;
            Assert.That(
                runtimeEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(beforeIds));
            Assert.That(
                runtimeEntries.Select(entry => entry.SortOrder),
                Is.EqualTo(new[] { 0, 10, 25, 50, 70, 90, 110, 120, 130 }));
            Assert.That(
                runtimeEntries.Select(entry => entry.AuthoringOrder),
                Is.EqualTo(Enumerable.Range(0, beforeEntries.Length)));
        }

        [Test]
        public void ObjectiveConditionSortOrder_StageFourTwo_PreservesArrayOrderAndPrimaryRuntimePriorityMetadata()
        {
            using var fixture = CampaignPairFixture.Create("stage-4-2");
            var beforeEntries = fixture.Authoring.Objective.ConditionEntries.ToArray();
            var beforeIds = beforeEntries.Select(entry => entry.StableConditionId).ToArray();
            var primaryIndex = Array.FindIndex(beforeEntries, entry => entry.Role == StageObjectiveConditionRole.PrimaryGoal);
            Assert.That(primaryIndex, Is.EqualTo(beforeEntries.Length - 1));
            Assert.That(beforeEntries[primaryIndex].SortOrder, Is.Zero);
            var row = fixture.Window.GetObjectiveConditionRowsForTests().First(candidate =>
                candidate.Role == StageObjectiveConditionRole.SecondaryGoal);

            Assert.That(fixture.Window.SelectObjectiveConditionForTests(row.StableConditionId, row.Condition), Is.True);
            Assert.That(
                fixture.Window.SetSelectedObjectiveSecondarySortOrderForTests(5, out var validation),
                Is.True,
                validation.Message);
            Assert.That(
                fixture.Authoring.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(beforeIds));
            fixture.Window.GenerateForTests();
            Assert.That(fixture.Window.LastReportForTests.HasErrors, Is.False, FormatIssues(fixture.Window.LastReportForTests));
            Assert.That(
                fixture.Gameplay.Objective.ConditionEntries.Select(entry => entry.StableConditionId),
                Is.EqualTo(beforeIds));

            var runtimeEntries = StageRuntimeBuilder.Build(fixture.Gameplay)
                .ObjectiveRuntimeDefinition
                .ConditionEntries;
            var runtimePrimary = runtimeEntries.Single(entry =>
                entry.Role == StageObjectiveConditionRole.PrimaryGoal);
            Assert.That(runtimePrimary.SortOrder, Is.Zero);
            Assert.That(runtimePrimary.AuthoringOrder, Is.EqualTo(primaryIndex));
            Assert.That(runtimeEntries[row.EntryIndex].SortOrder, Is.EqualTo(5));
            Assert.That(runtimeEntries[row.EntryIndex].AuthoringOrder, Is.EqualTo(row.EntryIndex));
            Assert.That(fixture.Window.ResolveObjectiveConditionSelectionForTests(),
                Is.EqualTo(StageObjectiveConditionSelectionResolution.Resolved));
        }

        [Test]
        public void ObjectiveConditionSortOrder_MutationSources_KeepCanonicalFieldAndArrayBoundaries()
        {
            var mutationSource = ReadAssetText(
                "Assets/_Features/Stages/Editor/Authoring/StageObjectiveConditionEditorSelection.cs");
            var rendererSource = ReadAssetText(
                "Assets/_Features/Stages/Editor/Authoring/StageObjectiveConditionEditorRenderer.cs");
            var helperSource = ReadAssetText(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringButtonObjectiveHelperCommands.cs");

            Assert.That(mutationSource, Does.Contain("sortOrderProperty.intValue = sortOrder;"));
            Assert.That(mutationSource, Does.Not.Contain("MoveArrayElement"));
            Assert.That(mutationSource, Does.Not.Contain("Undo.RecordObject"));
            Assert.That(rendererSource, Does.Not.Contain("MoveArrayElement"));
            Assert.That(rendererSource, Does.Contain("Primary Goal ordering is preserved by this editor."));
            Assert.That(rendererSource, Does.Contain("Gaps are allowed."));
            Assert.That(helperSource, Does.Contain("maxSecondary > int.MaxValue - 10"));
            Assert.That(helperSource, Does.Contain("No additional automatic Sort Order can be allocated."));
        }

        private static void AssertBlocked(
            SerializedObject serialized,
            StageAuthoringDefinition authoring,
            StageObjectiveConditionEditorSelection selection,
            int sortOrder,
            StageObjectiveConditionSortOrderValidationError expectedError,
            string expectedMessage,
            string authoringBefore)
        {
            Assert.That(
                StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                    serialized,
                    authoring,
                    selection,
                    sortOrder,
                    out var validation),
                Is.False);
            Assert.That(validation.Error, Is.EqualTo(expectedError));
            Assert.That(validation.Message, Is.EqualTo(expectedMessage));
            Assert.That(EditorJsonUtility.ToJson(authoring), Is.EqualTo(authoringBefore));
        }

        private static StageObjectiveConditionEditorSelection Select(
            SerializedObject serialized,
            StageAuthoringDefinition authoring,
            string stableConditionId)
        {
            serialized.Update();
            var row = StageObjectiveConditionEditorResolver.BuildRows(serialized, authoring).Single(candidate =>
                candidate.StableConditionId == stableConditionId);
            var selection = new StageObjectiveConditionEditorSelection();
            selection.Select(row);
            return selection;
        }

        private static void AssertOnlySelectedSortOrderChanged(
            IReadOnlyList<StageObjectiveConditionEntry> before,
            IReadOnlyList<StageObjectiveConditionEntry> after,
            int selectedIndex,
            int expectedSortOrder)
        {
            Assert.That(after.Count, Is.EqualTo(before.Count));
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i].Condition, Is.SameAs(before[i].Condition), $"entry[{i}].Condition");
                Assert.That(after[i].Required, Is.EqualTo(before[i].Required), $"entry[{i}].Required");
                Assert.That(after[i].Role, Is.EqualTo(before[i].Role), $"entry[{i}].Role");
                Assert.That(after[i].StableConditionId, Is.EqualTo(before[i].StableConditionId), $"entry[{i}].StableConditionId");
                Assert.That(after[i].AuthoringLabel, Is.EqualTo(before[i].AuthoringLabel), $"entry[{i}].AuthoringLabel");
                Assert.That(
                    after[i].SortOrder,
                    Is.EqualTo(i == selectedIndex ? expectedSortOrder : before[i].SortOrder),
                    $"entry[{i}].SortOrder");
            }
        }

        private static StageObjectiveAuthoring Objective(params StageObjectiveConditionEntry[] entries)
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                ObjectiveTitle = "Objective",
                ObjectiveSummary = "Sort order coverage",
                ConditionEntries = entries,
            };
        }

        private static StageObjectiveConditionEntry Entry(
            StageConditionAsset condition,
            string stableConditionId,
            StageObjectiveConditionRole role,
            int sortOrder)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = true,
                Role = role,
                StableConditionId = stableConditionId,
                AuthoringLabel = stableConditionId,
                SortOrder = sortOrder,
            };
        }

        private static string ReadAssetText(string assetPath)
        {
            return File.ReadAllText(ToAbsoluteProjectPath(assetPath));
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

        private sealed class CampaignPairFixture : IDisposable
        {
            private CampaignPairFixture(
                string folderPath,
                string authoringPath,
                string gameplayPath,
                string presentationPath,
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StageAuthoringGridWindow window)
            {
                FolderPath = folderPath;
                AuthoringPath = authoringPath;
                GameplayPath = gameplayPath;
                PresentationPath = presentationPath;
                Authoring = authoring;
                Gameplay = gameplay;
                Window = window;
            }

            private string FolderPath { get; }

            private string AuthoringPath { get; }

            private string GameplayPath { get; }

            private string PresentationPath { get; }

            public StageAuthoringDefinition Authoring { get; }

            public StageDefinition Gameplay { get; }

            public StageAuthoringGridWindow Window { get; }

            public static CampaignPairFixture Create(string stageName)
            {
                var folderPath = $"Assets/__ObjectiveConditionSortOrder_{Guid.NewGuid():N}";
                var authoringPath = $"{folderPath}/Authoring.asset";
                var gameplayPath = $"{folderPath}/Generated.asset";
                var presentationPath = $"{folderPath}/Presentation.asset";
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folderPath));
                Assert.That(AssetDatabase.CopyAsset(GetAuthoringPath(stageName), authoringPath), Is.True);
                Assert.That(AssetDatabase.CopyAsset(GetGameplayPath(stageName), gameplayPath), Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(
                        $"{ObjectiveStageRoot}/{stageName}/{stageName}_Presentation.asset",
                        presentationPath),
                    Is.True);

                var authoring = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(authoringPath);
                var gameplay = AssetDatabase.LoadAssetAtPath<StageDefinition>(gameplayPath);
                var presentation = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(presentationPath);
                authoring.AssignGeneratedDefinitions(gameplay, presentation);
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssetIfDirty(authoring);
                var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
                window.BindForTests(authoring);
                return new CampaignPairFixture(
                    folderPath,
                    authoringPath,
                    gameplayPath,
                    presentationPath,
                    authoring,
                    gameplay,
                    window);
            }

            public byte[] ReadAuthoringBytes()
            {
                return File.ReadAllBytes(ToAbsoluteProjectPath(AuthoringPath));
            }

            public byte[] ReadGameplayBytes()
            {
                return File.ReadAllBytes(ToAbsoluteProjectPath(GameplayPath));
            }

            public byte[] ReadPresentationBytes()
            {
                return File.ReadAllBytes(ToAbsoluteProjectPath(PresentationPath));
            }

            public void Dispose()
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(Window);
                AssetDatabase.DeleteAsset(FolderPath);
            }

            private static string GetAuthoringPath(string stageName)
            {
                return $"{ObjectiveStageRoot}/{stageName}/{stageName}_Authoring.asset";
            }

            private static string GetGameplayPath(string stageName)
            {
                return $"{ObjectiveStageRoot}/{stageName}/{stageName}.asset";
            }
        }
    }
}
