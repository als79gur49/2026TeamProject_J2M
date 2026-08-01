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
