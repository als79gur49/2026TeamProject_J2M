using System;
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
            Assert.That(entry.DisplayText, Is.EqualTo("Place a push box on the button"));
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
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Single().DisplayText, Is.EqualTo("Activate QA button"));
        }

        [Test]
        public void TryAddRequiredSecondaryGoal_MoonBlockOnlyButton_UsesMoonBlockDisplayText()
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
            Assert.That(fixture.Authoring.Objective.ConditionEntries.Single().DisplayText,
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

            Assert.That(result.Succeeded, Is.False);
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
        public void TryRemoveRequiredSecondaryGoal_LinkedButton_RemovesEntryButKeepsAsset()
        {
            using var fixture = TempStageContentFixture.Create();
            var addResult = StageAuthoringButtonObjectiveHelperCommands.TryAddRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button,
                fixture.Entry.StageId.Value);
            Assert.That(addResult.Succeeded, Is.True, addResult.Message);
            var condition = AssetDatabase.LoadAssetAtPath<ButtonActivatedConditionAsset>(fixture.ExpectedButtonConditionPath);

            var removeResult = StageAuthoringButtonObjectiveHelperCommands.TryRemoveRequiredSecondaryGoal(
                fixture.Authoring,
                fixture.Button);

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

        private static SurfaceCell Cell(int x, int y)
        {
            return new SurfaceCell(FaceId.Floor, x, y);
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
                DisplayText = string.Empty,
                SortOrder = 0,
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
    }
}
