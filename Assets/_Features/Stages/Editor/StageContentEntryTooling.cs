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
        private const string CanonicalContentRoot = StageContentPaths.CampaignLevel01StagesRoot;

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
            var authoringPath = $"{stageFolder}/{stageId.Value}_Authoring.asset";
            var presentationPath = $"{stageFolder}/{stageId.Value}_Presentation.asset";
            var audioPath = $"{stageFolder}/{stageId.Value}_Audio.asset";

            if (AssetDatabase.LoadAssetAtPath<StageContentEntry>(entryPath) != null)
            {
                throw new InvalidOperationException($"Stage content entry already exists at '{entryPath}'.");
            }

            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.name = $"{stageId.Value}_Entry";
            entry.AssignStageId(stageId);
            entry.AssignGameplayDefinition(stageDefinition);

            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            authoring.name = $"{stageId.Value}_Authoring";

            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            presentation.name = $"{stageId.Value}_Presentation";
            presentation.ApplyResolvedData(seededPresentation ?? StagePresentationAssembler.EmptyResolvedData);

            var audio = ScriptableObject.CreateInstance<StageAudioDefinition>();
            audio.name = $"{stageId.Value}_Audio";

            AssetDatabase.CreateAsset(entry, entryPath);
            AssetDatabase.CreateAsset(authoring, authoringPath);
            AssetDatabase.CreateAsset(presentation, presentationPath);
            AssetDatabase.CreateAsset(audio, audioPath);

            var entryGuid = AssetDatabase.AssetPathToGUID(entryPath);
            authoring.SetOwnerMetadata(entry, entryGuid);
            presentation.SetOwnerMetadata(entry, entryGuid);
            audio.SetOwnerMetadata(entry, entryGuid);

            authoring.AssignGeneratedDefinitions(stageDefinition, presentation);
            StageAuthoringMigrationTool.PopulateFromOutputs(
                authoring,
                stageId,
                stageDefinition,
                presentation,
                overwriteGeneratedReferences: true);
            entry.AssignAuthoringDefinition(authoring);
            entry.AssignPresentationDefinition(presentation);
            entry.AssignAudioDefinition(audio);

            EditorUtility.SetDirty(entry);
            EditorUtility.SetDirty(authoring);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(audio);
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
        private const string CanonicalContentRoot = StageContentPaths.CampaignLevel01StagesRoot;

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
            RenameCompanion(entry.AuthoringDefinition, newStageId, "Authoring", entry);
            RenameCompanion(entry.PresentationDefinition, newStageId, "Presentation", entry);
            RenameCompanion(entry.AudioDefinition, newStageId, "Audio", entry);

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
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateForBuild(campaignSequenceOverride: null);
        }

        internal static void ValidateForBuild(
            CampaignStageSequenceProductionValidation campaignSequenceOverride)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            if (catalog == null)
            {
                throw new BuildFailedException(
                    $"Missing canonical StageCatalog asset at '{StageContentPaths.StageCatalogAssetPath}'.");
            }

            var validator = new StageCatalogValidator();
            var options = new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireAudioDefinition = true,
                Timing = StageValidationTiming.PreBuild,
                Phase = StageValidationPhase.Phase4_ProductionBootstrapConversion,
            };

            var validationReport = validator.Validate(catalog, options);
            validationReport.AddRange(new StageCampaignContentGovernanceValidator()
                .Validate(StageValidationTiming.PreBuild)
                .Issues);
            var campaignSequenceValidation = campaignSequenceOverride ??
                CampaignStageSequenceProductionValidation.Validate(catalog, options.Timing);
            validationReport.AddRange(campaignSequenceValidation.SourceReport.Issues);
            validationReport.AddRange(campaignSequenceValidation.AuthoritativeReport.Issues);
            if (validationReport.HasErrors)
            {
                throw new BuildFailedException(BuildFailureMessage(
                    $"{StageContentPaths.StageCatalogAssetPath} and {CampaignStageSequenceAssetLoader.CanonicalAssetPath}",
                    validationReport));
            }

            var sceneReport = new StageSceneBootstrapValidator().ValidateEnabledBuildScenes(
                options,
                campaignSequenceValidation.Definition);
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
