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
                var aliases = new List<StageIdAliasEntry>(aliasTable.Entries)
                {
                    new StageIdAliasEntry
                    {
                        DeprecatedStageId = oldStageId.Value,
                        CurrentStageId = newStageId,
                    },
                };
                aliasTable.SetEntries(aliases.ToArray());
                EditorUtility.SetDirty(aliasTable);
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
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var catalogGuids = AssetDatabase.FindAssets("t:StageCatalog");
            if (catalogGuids == null || catalogGuids.Length == 0)
            {
                return;
            }

            var validator = new StageCatalogValidator();
            var options = new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireClearEvaluationDefinition = true,
                RequireRewardDefinition = true,
                RequireProgressionDefinition = true,
                Timing = StageValidationTiming.PreBuild,
            };

            for (var i = 0; i < catalogGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
                var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(path);
                var validationReport = validator.Validate(catalog, options);
                if (!validationReport.HasErrors)
                {
                    continue;
                }

                throw new BuildFailedException(BuildFailureMessage(path, validationReport));
            }
        }

        private static string BuildFailureMessage(string assetPath, StageValidationReport validationReport)
        {
            var messages = new List<string>
            {
                $"Stage catalog validation failed for '{assetPath}'.",
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
