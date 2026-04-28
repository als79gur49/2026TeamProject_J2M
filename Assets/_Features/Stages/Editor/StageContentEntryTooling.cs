using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageContentEntryCreationTool
    {
        private const string CanonicalContentRoot = "Assets/_Features/Stages/Content";

        [MenuItem("Assets/Create/Gameplay/Stages/Create Stage Content Entry From Selected StageDefinition", priority = 401)]
        private static void CreateFromSelectedStageDefinition()
        {
            var stageDefinition = Selection.activeObject as StageDefinition;
            if (stageDefinition == null)
            {
                throw new InvalidOperationException("Select a StageDefinition asset before creating StageContentEntry companions.");
            }

            CreateForStageDefinition(stageDefinition);
        }

        [MenuItem("Assets/Create/Gameplay/Stages/Create Stage Content Entry From Selected StageDefinition", validate = true)]
        private static bool ValidateCreateFromSelectedStageDefinition()
        {
            return Selection.activeObject is StageDefinition;
        }

        public static StageContentEntry CreateForStageDefinition(StageDefinition stageDefinition)
        {
            if (stageDefinition == null)
            {
                throw new ArgumentNullException(nameof(stageDefinition));
            }

            if (!StageId.TryCreate(stageDefinition.name, out var stageId))
            {
                throw new InvalidOperationException($"StageDefinition '{stageDefinition.name}' cannot produce a canonical StageId.");
            }

            return CreateForStageDefinition(stageDefinition, stageId);
        }

        public static StageContentEntry CreateForStageDefinition(
            StageDefinition stageDefinition,
            StageId stageId,
            StagePresentationResolvedData seededPresentation = null)
        {
            if (stageDefinition == null)
            {
                throw new ArgumentNullException(nameof(stageDefinition));
            }

            if (!stageId.IsValid)
            {
                throw new InvalidOperationException("Stage content entry creation requires a canonical StageId.");
            }

            EnsureFolder(CanonicalContentRoot);
            var stageFolder = $"{CanonicalContentRoot}/{stageId.Value}";
            EnsureFolder(stageFolder);

            var entryPath = $"{stageFolder}/{stageId.Value}_Entry.asset";
            var presentationPath = $"{stageFolder}/{stageId.Value}_Presentation.asset";
            var clearEvaluationPath = $"{stageFolder}/{stageId.Value}_ClearEvaluation.asset";
            var rewardPath = $"{stageFolder}/{stageId.Value}_Reward.asset";
            var progressionPath = $"{stageFolder}/{stageId.Value}_Progression.asset";

            if (AssetDatabase.LoadAssetAtPath<StageContentEntry>(entryPath) != null)
            {
                throw new InvalidOperationException($"Stage content entry already exists at '{entryPath}'.");
            }

            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.name = $"{stageId.Value}_Entry";
            entry.AssignStageId(stageId);
            entry.AssignGameplayDefinition(stageDefinition);

            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            presentation.name = $"{stageId.Value}_Presentation";
            presentation.ApplyResolvedData(seededPresentation ?? StagePresentationAssembler.EmptyResolvedData);

            var clearEvaluation = ScriptableObject.CreateInstance<StageClearEvaluationDefinition>();
            clearEvaluation.name = $"{stageId.Value}_ClearEvaluation";

            var reward = ScriptableObject.CreateInstance<StageRewardDefinition>();
            reward.name = $"{stageId.Value}_Reward";

            var progression = ScriptableObject.CreateInstance<StageProgressionDefinition>();
            progression.name = $"{stageId.Value}_Progression";

            AssetDatabase.CreateAsset(entry, entryPath);
            AssetDatabase.CreateAsset(presentation, presentationPath);
            AssetDatabase.CreateAsset(clearEvaluation, clearEvaluationPath);
            AssetDatabase.CreateAsset(reward, rewardPath);
            AssetDatabase.CreateAsset(progression, progressionPath);

            var entryGuid = AssetDatabase.AssetPathToGUID(entryPath);
            presentation.SetOwnerMetadata(entry, entryGuid);
            clearEvaluation.SetOwnerMetadata(entry, entryGuid);
            reward.SetOwnerMetadata(entry, entryGuid);
            progression.SetOwnerMetadata(entry, entryGuid);

            entry.AssignPresentationDefinition(presentation);
            entry.AssignClearEvaluationDefinition(clearEvaluation);
            entry.AssignRewardDefinition(reward);
            entry.AssignProgressionDefinition(progression);

            EditorUtility.SetDirty(entry);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(clearEvaluation);
            EditorUtility.SetDirty(reward);
            EditorUtility.SetDirty(progression);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = entry;
            return entry;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            var folderName = Path.GetFileName(assetFolder);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    public static class StageGoalZoneObjectiveMigrationTool
    {
        private const int PrimaryGoalRoleValue = 1;
        private const int LegacyRequirePlayerOnGoalWithAllConditionsPolicyValue = 1;
        private const string ConditionsFolderName = "Conditions";
        private const string PrimaryGoalAssetName = "PrimaryGoal_PlayerAtAnyZone.asset";

        [MenuItem("Tools/Stages/Migration/Create Primary Goal Conditions For Selected")]
        private static void CreatePrimaryGoalConditionsForSelected()
        {
            var migrated = CreateForSelection(Selection.objects);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Primary goal condition migration created or updated {migrated} stage objective entries.");
        }

        [MenuItem("Tools/Stages/Migration/Create Primary Goal Conditions For Selected", validate = true)]
        private static bool ValidateCreatePrimaryGoalConditionsForSelected()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < selected.Length; i++)
            {
                if (selected[i] is StageDefinition or StageContentEntry or StageCatalog)
                {
                    return true;
                }
            }

            return false;
        }

        public static int CreateForSelection(IReadOnlyList<UnityEngine.Object> selectedObjects)
        {
            if (selectedObjects == null || selectedObjects.Count == 0)
            {
                return 0;
            }

            var visitedStages = new HashSet<StageDefinition>();
            var migrated = 0;
            for (var i = 0; i < selectedObjects.Count; i++)
            {
                switch (selectedObjects[i])
                {
                    case StageDefinition stageDefinition:
                        if (TryCreateForStageDefinition(stageDefinition, visitedStages))
                        {
                            migrated++;
                        }

                        break;

                    case StageContentEntry entry:
                        if (TryCreateForStageDefinition(entry.GameplayDefinition, visitedStages))
                        {
                            migrated++;
                        }

                        break;

                    case StageCatalog catalog:
                        migrated += CreateForCatalog(catalog, visitedStages);
                        break;
                }
            }

            return migrated;
        }

        public static int CreateForCatalog(StageCatalog catalog)
        {
            return CreateForCatalog(catalog, new HashSet<StageDefinition>());
        }

        private static int CreateForCatalog(StageCatalog catalog, ISet<StageDefinition> visitedStages)
        {
            if (catalog == null)
            {
                return 0;
            }

            var migrated = 0;
            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null &&
                    TryCreateForStageDefinition(entries[i].GameplayDefinition, visitedStages))
                {
                    migrated++;
                }
            }

            return migrated;
        }

        public static bool TryCreateForStageDefinition(StageDefinition stageDefinition)
        {
            return TryCreateForStageDefinition(stageDefinition, new HashSet<StageDefinition>());
        }

        private static bool TryCreateForStageDefinition(
            StageDefinition stageDefinition,
            ISet<StageDefinition> visitedStages)
        {
            if (stageDefinition == null ||
                visitedStages == null ||
                !visitedStages.Add(stageDefinition))
            {
                return false;
            }

            var stagePath = AssetDatabase.GetAssetPath(stageDefinition);
            if (string.IsNullOrEmpty(stagePath))
            {
                return false;
            }

            var stageObject = new SerializedObject(stageDefinition);
            var objectiveProperty = stageObject.FindProperty("objective");
            if (objectiveProperty == null)
            {
                return false;
            }

            var goalZoneIdsProperty = objectiveProperty.FindPropertyRelative("GoalZoneIds");
            if (goalZoneIdsProperty == null || goalZoneIdsProperty.arraySize == 0)
            {
                return false;
            }

            var conditionEntriesProperty = objectiveProperty.FindPropertyRelative("ConditionEntries");
            if (HasPrimaryGoalEntry(conditionEntriesProperty))
            {
                return false;
            }

            var zoneIds = ReadStringArray(goalZoneIdsProperty);
            if (zoneIds.Length == 0)
            {
                return false;
            }

            var stageFolder = Path.GetDirectoryName(stagePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(stageFolder))
            {
                return false;
            }

            var conditionsFolder = $"{stageFolder}/{ConditionsFolderName}";
            EnsureFolder(conditionsFolder);
            var conditionPath = $"{conditionsFolder}/{PrimaryGoalAssetName}";
            var conditionAsset = AssetDatabase.LoadAssetAtPath<PlayerAtAnyZoneConditionAsset>(conditionPath);
            if (conditionAsset == null)
            {
                var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(conditionPath);
                if (existingAsset != null)
                {
                    throw new InvalidOperationException(
                        $"Cannot create primary goal condition at '{conditionPath}' because a non-{nameof(PlayerAtAnyZoneConditionAsset)} asset already exists there.");
                }

                conditionAsset = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
                conditionAsset.name = Path.GetFileNameWithoutExtension(PrimaryGoalAssetName);
                AssetDatabase.CreateAsset(conditionAsset, conditionPath);
            }

            ConfigureConditionAsset(conditionAsset, zoneIds);

            conditionEntriesProperty ??= objectiveProperty.FindPropertyRelative("ConditionEntries");
            if (conditionEntriesProperty == null)
            {
                throw new InvalidOperationException("StageObjectiveAuthoring.ConditionEntries serialized property was not found.");
            }

            var index = conditionEntriesProperty.arraySize;
            conditionEntriesProperty.InsertArrayElementAtIndex(index);
            var entryProperty = conditionEntriesProperty.GetArrayElementAtIndex(index);
            entryProperty.FindPropertyRelative("Condition").objectReferenceValue = conditionAsset;
            entryProperty.FindPropertyRelative("Required").boolValue =
                objectiveProperty.FindPropertyRelative("CompletionPolicy").intValue ==
                LegacyRequirePlayerOnGoalWithAllConditionsPolicyValue;
            entryProperty.FindPropertyRelative("Role").intValue = PrimaryGoalRoleValue;
            entryProperty.FindPropertyRelative("StableConditionId").stringValue = "primary-goal";

            // TODO(goal-zone-condition-followup): after assets are migrated and verified, remove legacy GoalZoneIds.
            stageObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(stageDefinition);
            return true;
        }

        private static bool HasPrimaryGoalEntry(SerializedProperty conditionEntriesProperty)
        {
            if (conditionEntriesProperty == null)
            {
                return false;
            }

            for (var i = 0; i < conditionEntriesProperty.arraySize; i++)
            {
                var entry = conditionEntriesProperty.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("Role").intValue == PrimaryGoalRoleValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] ReadStringArray(SerializedProperty arrayProperty)
        {
            var values = new List<string>();
            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                var value = arrayProperty.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }

            return values.ToArray();
        }

        private static void ConfigureConditionAsset(
            PlayerAtAnyZoneConditionAsset conditionAsset,
            IReadOnlyList<string> zoneIds)
        {
            var conditionObject = new SerializedObject(conditionAsset);
            var zoneIdsProperty = conditionObject.FindProperty("zoneIds");
            zoneIdsProperty.arraySize = zoneIds.Count;
            for (var i = 0; i < zoneIds.Count; i++)
            {
                zoneIdsProperty.GetArrayElementAtIndex(i).stringValue = zoneIds[i];
            }

            conditionObject.FindProperty("requireAlive").boolValue = true;
            conditionObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(conditionAsset);
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }

    public static class StageIdRenameTool
    {
        private const string CanonicalContentRoot = "Assets/_Features/Stages/Content";

        public static void Rename(StageContentEntry entry, string rawStageId, StageIdAliasTable aliasTable = null)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!StageId.TryCreate(rawStageId, out var newStageId))
            {
                throw new InvalidOperationException($"'{rawStageId}' is not a valid canonical StageId.");
            }

            var oldStageId = entry.StageId;
            if (oldStageId.Equals(newStageId))
            {
                return;
            }

            var oldEntryPath = AssetDatabase.GetAssetPath(entry);
            var oldFolder = Path.GetDirectoryName(oldEntryPath)?.Replace('\\', '/');
            var newFolder = $"{CanonicalContentRoot}/{newStageId.Value}";
            EnsureFolder(CanonicalContentRoot);

            if (AssetDatabase.IsValidFolder(oldFolder) &&
                !string.Equals(oldFolder, newFolder, StringComparison.Ordinal))
            {
                if (AssetDatabase.IsValidFolder(newFolder))
                {
                    throw new InvalidOperationException($"Target stage content folder '{newFolder}' already exists.");
                }

                var moveError = AssetDatabase.MoveAsset(oldFolder, newFolder);
                if (!string.IsNullOrEmpty(moveError))
                {
                    throw new InvalidOperationException(moveError);
                }
            }
            else if (!AssetDatabase.IsValidFolder(newFolder))
            {
                EnsureFolder(newFolder);
            }

            entry.AssignStageId(newStageId);
            RenameAsset(entry, $"{newStageId.Value}_Entry");
            RenameCompanion(entry.PresentationDefinition, newStageId, "Presentation", entry);
            RenameCompanion(entry.ClearEvaluationDefinition, newStageId, "ClearEvaluation", entry);
            RenameCompanion(entry.RewardDefinition, newStageId, "Reward", entry);
            RenameCompanion(entry.ProgressionDefinition, newStageId, "Progression", entry);

            if (aliasTable != null && oldStageId.IsValid)
            {
                var aliasLedger = StageAliasGovernanceUpdater.LoadOrCreateLedger();
                var aliasEntry = new StageIdAliasEntry
                {
                    DeprecatedStageId = oldStageId.Value,
                    CurrentStageId = newStageId,
                };
                var aliases = new List<StageIdAliasEntry>(aliasTable.Entries)
                {
                    aliasEntry,
                };
                var governanceEntries = new List<StageAliasGovernanceEntry>(aliasLedger.Entries)
                {
                    StageAliasGovernanceUpdater.CreateEntry(
                        aliasEntry,
                        sourceKind: "stage-id-rename",
                        sourceAssetGuid: AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry)),
                        introducedBy: nameof(StageIdRenameTool))
                };
                StageAliasGovernanceUpdater.Apply(aliasTable, aliasLedger, aliases, governanceEntries);
            }

            EditorUtility.SetDirty(entry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RenameCompanion(
            StageCompanionDefinitionBase companion,
            StageId newStageId,
            string suffix,
            StageContentEntry ownerEntry)
        {
            if (companion == null)
            {
                return;
            }

            RenameAsset(companion, $"{newStageId.Value}_{suffix}");
            companion.SetOwnerMetadata(ownerEntry, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(ownerEntry)));
            EditorUtility.SetDirty(companion);
        }

        private static void RenameAsset(UnityEngine.Object asset, string newName)
        {
            if (asset == null)
            {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                asset.name = newName;
                return;
            }

            var renameError = AssetDatabase.RenameAsset(assetPath, newName);
            if (!string.IsNullOrEmpty(renameError))
            {
                throw new InvalidOperationException(renameError);
            }
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }

    public sealed class StageCatalogBuildValidationHook : IPreprocessBuildWithReport
    {
        private const string CanonicalStageCatalogAssetPath = "Assets/_Features/Stages/Content/StageCatalog.asset";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CanonicalStageCatalogAssetPath);
            if (catalog == null)
            {
                throw new BuildFailedException(
                    $"Missing canonical StageCatalog asset at '{CanonicalStageCatalogAssetPath}'.");
            }

            var validator = new StageCatalogValidator();
            var options = new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireClearEvaluationDefinition = true,
                RequireRewardDefinition = true,
                RequireProgressionDefinition = true,
                Timing = StageValidationTiming.PreBuild,
                Phase = StageValidationPhase.Phase4_ProductionBootstrapConversion,
            };

            var validationReport = validator.Validate(catalog, options);
            if (validationReport.HasErrors)
            {
                throw new BuildFailedException(BuildFailureMessage(CanonicalStageCatalogAssetPath, validationReport));
            }

            var sceneReport = new StageSceneBootstrapValidator().ValidateEnabledBuildScenes(options);
            if (sceneReport.HasErrors)
            {
                throw new BuildFailedException(BuildFailureMessage("enabled build scenes", sceneReport));
            }
        }

        private static string BuildFailureMessage(string validationTarget, StageValidationReport validationReport)
        {
            var messages = new List<string>
            {
                $"Stage validation failed for '{validationTarget}'.",
            };

            for (var i = 0; i < validationReport.Issues.Count; i++)
            {
                var issue = validationReport.Issues[i];
                if (issue.Severity == StageValidationSeverity.Error)
                {
                    messages.Add($"[{issue.Code}] {issue.Message}");
                }
            }

            return string.Join(Environment.NewLine, messages);
        }
    }
}
