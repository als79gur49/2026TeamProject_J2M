using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public sealed class StageCampaignContentGovernanceValidator
    {
        private static readonly Type[] AllowedStageFolderAssetTypes =
        {
            typeof(StageContentEntry),
            typeof(StageAuthoringDefinition),
            typeof(StageDefinition),
            typeof(StagePresentationDefinition),
            typeof(StageClearEvaluationDefinition),
            typeof(StageRewardDefinition),
            typeof(StageProgressionDefinition),
        };

        public StageValidationReport Validate(StageValidationTiming timing = StageValidationTiming.EditorAuthoring)
        {
            var report = new StageValidationReport();
            ValidateCampaignRootRule(report, timing);
            ValidateNoOldStageSupportTree(report, timing);
            ValidateNoLooseStageContentRoot(report, timing);
            ValidateStageFoldersAreCompanionOnly(report, timing);
            ValidateNoLevelOrStageSharedFolders(report, timing);
            ValidateSharedDoesNotDependOnStageCompanions(report, timing);
            ValidateNoSiblingStageReferences(report, timing);
            ValidatePresentationCatalogPaths(report, timing);
            ValidateEnemyAiProfileReferencePaths(report, timing);
            ValidateConditionReferencePaths(report, timing);
            return report;
        }

        private static void ValidateCampaignRootRule(StageValidationReport report, StageValidationTiming timing)
        {
            var contentAssetPaths = FindAssetPaths(StageContentPaths.LegacyContentRoot);
            for (var i = 0; i < contentAssetPaths.Count; i++)
            {
                var path = contentAssetPaths[i];
                if (IsMetaOrNote(path) || IsEditorAssetPath(path))
                {
                    continue;
                }

                if (!IsUnder(path, StageContentPaths.CampaignRoot))
                {
                    AddPathError(
                        report,
                        "campaign-content.path.outside-campaign-root",
                        $"Stage content asset must live under '{StageContentPaths.CampaignRoot}' during Phase 1.",
                        path,
                        timing);
                }
            }
        }

        private static void ValidateNoOldStageSupportTree(StageValidationReport report, StageValidationTiming timing)
        {
            var root = StageContentPaths.StagesRoot;
            var oldStageRoots = Directory.Exists(root)
                ? Directory.GetDirectories(root, "Stage_*", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            for (var i = 0; i < oldStageRoots.Length; i++)
            {
                var assetRoot = NormalizeAssetPath(oldStageRoots[i]);
                var assetPaths = FindAssetPaths(assetRoot);
                for (var pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
                {
                    var path = assetPaths[pathIndex];
                    if (IsMetaOrNote(path))
                    {
                        continue;
                    }

                    AddPathError(
                        report,
                        "campaign-content.old-stage-support-tree",
                        $"Legacy stage support tree must not contain assets after Campaign Phase 1 migration: '{assetRoot}'.",
                        path,
                        timing);
                }
            }
        }

        private static void ValidateNoLooseStageContentRoot(StageValidationReport report, StageValidationTiming timing)
        {
            var absoluteRoot = ToAbsolutePath(StageContentPaths.LegacyContentRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return;
            }

            var directories = Directory.GetDirectories(absoluteRoot, "*", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < directories.Length; i++)
            {
                var path = NormalizeAssetPath(directories[i]);
                if (string.Equals(path, StageContentPaths.CampaignRoot, StringComparison.Ordinal) ||
                    string.Equals(Path.GetFileName(path), "Campaigns", StringComparison.Ordinal))
                {
                    continue;
                }

                AddPathError(
                    report,
                    "campaign-content.loose-stage-content-root",
                    $"Legacy loose stage content folder is forbidden in Phase 1: '{path}'.",
                    path,
                    timing);
            }
        }

        private static void ValidateStageFoldersAreCompanionOnly(StageValidationReport report, StageValidationTiming timing)
        {
            var stageRootAbsolute = ToAbsolutePath(StageContentPaths.CampaignLevel01StagesRoot);
            if (!Directory.Exists(stageRootAbsolute))
            {
                return;
            }

            var stageFolders = Directory.GetDirectories(stageRootAbsolute, "*", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < stageFolders.Length; i++)
            {
                var stageFolder = NormalizeAssetPath(stageFolders[i]);
                var assetPaths = FindAssetPaths(stageFolder);
                for (var pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
                {
                    var path = assetPaths[pathIndex];
                    if (IsMetaOrNote(path))
                    {
                        continue;
                    }

                    if (Path.GetDirectoryName(path)?.Replace('\\', '/') != stageFolder)
                    {
                        AddPathError(
                            report,
                            "campaign-content.stage-folder.nested-asset",
                            "Campaign stage folders may not contain nested support assets in Phase 1.",
                            path,
                            timing);
                        continue;
                    }

                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null || !IsAllowedStageFolderAsset(path, stageFolder, asset))
                    {
                        AddPathError(
                            report,
                            "campaign-content.stage-folder.non-companion-asset",
                            "Campaign stage folders may contain only StageContentEntry and companion definitions.",
                            path,
                            timing);
                    }
                }
            }
        }

        private static void ValidateNoLevelOrStageSharedFolders(StageValidationReport report, StageValidationTiming timing)
        {
            var levelsRoot = ToAbsolutePath(StageContentPaths.CampaignLevelsRoot);
            if (!Directory.Exists(levelsRoot))
            {
                return;
            }

            var forbidden = Directory.GetDirectories(levelsRoot, "_Shared", SearchOption.AllDirectories);
            for (var i = 0; i < forbidden.Length; i++)
            {
                AddPathError(
                    report,
                    "campaign-content.level-shared-forbidden",
                    "Level _Shared folders are forbidden in Phase 1.",
                    NormalizeAssetPath(forbidden[i]),
                    timing);
            }

            var stageRoot = ToAbsolutePath(StageContentPaths.CampaignLevel01StagesRoot);
            if (!Directory.Exists(stageRoot))
            {
                return;
            }

            var overrides = Directory.GetDirectories(stageRoot, "_Overrides", SearchOption.AllDirectories);
            for (var i = 0; i < overrides.Length; i++)
            {
                AddPathError(
                    report,
                    "campaign-content.stage-overrides-forbidden",
                    "Stage _Overrides folders are forbidden in Phase 1.",
                    NormalizeAssetPath(overrides[i]),
                    timing);
            }
        }

        private static void ValidateSharedDoesNotDependOnStageCompanions(
            StageValidationReport report,
            StageValidationTiming timing)
        {
            var sharedAssetPaths = FindAssetPaths(StageContentPaths.CampaignSharedRoot);
            for (var i = 0; i < sharedAssetPaths.Count; i++)
            {
                var path = sharedAssetPaths[i];
                foreach (var dependency in AssetDatabase.GetDependencies(path, recursive: false))
                {
                    if (IsUnder(dependency, StageContentPaths.CampaignLevel01StagesRoot))
                    {
                        AddReferenceError(
                            report,
                            "campaign-content.shared-depends-on-stage-companion",
                            "Campaign _Shared assets must not reference stage companion folders.",
                            path,
                            dependency,
                            timing);
                    }
                }
            }
        }

        private static void ValidateNoSiblingStageReferences(StageValidationReport report, StageValidationTiming timing)
        {
            var stageRootAbsolute = ToAbsolutePath(StageContentPaths.CampaignLevel01StagesRoot);
            if (!Directory.Exists(stageRootAbsolute))
            {
                return;
            }

            var stageFolders = Directory.GetDirectories(stageRootAbsolute, "*", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < stageFolders.Length; i++)
            {
                var stageFolder = NormalizeAssetPath(stageFolders[i]);
                var assetPaths = FindAssetPaths(stageFolder);
                for (var pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
                {
                    var path = assetPaths[pathIndex];
                    foreach (var dependency in AssetDatabase.GetDependencies(path, recursive: false))
                    {
                        if (!IsUnder(dependency, StageContentPaths.CampaignLevel01StagesRoot) ||
                            IsUnder(dependency, stageFolder) ||
                            string.Equals(dependency, path, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        AddReferenceError(
                            report,
                            "campaign-content.sibling-stage-reference",
                            "Stage companion assets must not directly reference sibling stage folders.",
                            path,
                            dependency,
                            timing);
                    }
                }
            }
        }

        private static void ValidatePresentationCatalogPaths(StageValidationReport report, StageValidationTiming timing)
        {
            var sharedPresentationPaths = FindAssetPaths(StageContentPaths.SharedPresentationRoot);
            for (var i = 0; i < sharedPresentationPaths.Count; i++)
            {
                var path = sharedPresentationPaths[i];
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || !asset.GetType().Name.Contains("PresentationCatalog", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var dependency in AssetDatabase.GetDependencies(path, recursive: false))
                {
                    if (!IsContentDependency(dependency))
                    {
                        continue;
                    }

                    if (!IsUnder(dependency, StageContentPaths.SharedPresentationRoot))
                    {
                        AddReferenceError(
                            report,
                            "campaign-content.presentation-catalog-path",
                            "Campaign presentation catalog entries must reference Campaign _Shared/Presentation assets.",
                            path,
                            dependency,
                            timing);
                    }
                }
            }
        }

        private static void ValidateEnemyAiProfileReferencePaths(StageValidationReport report, StageValidationTiming timing)
        {
            ValidateObjectReferencePaths(
                FindStageCompanionAssetPaths(),
                "EnemyAiProfile",
                StageContentPaths.SharedEnemyAiRoot,
                "campaign-content.enemy-ai-profile-path",
                "Stage enemy AI profile references must point under Campaign _Shared/Gameplay/EnemyAI.",
                report,
                timing);
        }

        private static void ValidateConditionReferencePaths(StageValidationReport report, StageValidationTiming timing)
        {
            ValidateObjectReferencePaths(
                FindStageCompanionAssetPaths(),
                nameof(StageConditionAsset),
                StageContentPaths.SharedConditionsRoot,
                "campaign-content.condition-path",
                "Stage condition references must point under Campaign _Shared/Gameplay/Conditions.",
                report,
                timing);
        }

        private static void ValidateObjectReferencePaths(
            IReadOnlyList<string> assetPaths,
            string referencedTypeName,
            string requiredRoot,
            string issueCode,
            string message,
            StageValidationReport report,
            StageValidationTiming timing)
        {
            for (var i = 0; i < assetPaths.Count; i++)
            {
                var path = assetPaths[i];
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    continue;
                }

                using var serialized = new SerializedObject(asset);
                var iterator = serialized.GetIterator();
                while (iterator.NextVisible(enterChildren: true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                        iterator.objectReferenceValue == null ||
                        !string.Equals(iterator.objectReferenceValue.GetType().Name, referencedTypeName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var referencedPath = AssetDatabase.GetAssetPath(iterator.objectReferenceValue);
                    if (!IsUnder(referencedPath, requiredRoot))
                    {
                        AddReferenceError(report, issueCode, message, path, referencedPath, timing);
                    }
                }
            }
        }

        private static IReadOnlyList<string> FindStageCompanionAssetPaths()
        {
            return FindAssetPaths(StageContentPaths.CampaignLevel01StagesRoot);
        }

        private static IReadOnlyList<string> FindAssetPaths(string assetRoot)
        {
            var absoluteRoot = ToAbsolutePath(assetRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return Array.Empty<string>();
            }

            var files = Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories);
            var result = new List<string>(files.Length);
            for (var i = 0; i < files.Length; i++)
            {
                var path = NormalizeAssetPath(files[i]);
                if (path.EndsWith(".meta", StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(path);
            }

            return result;
        }

        private static bool IsAllowedStageFolderAsset(
            string assetPath,
            string stageFolder,
            UnityEngine.Object asset)
        {
            if (asset is StageAudioDefinition audio)
            {
                return IsStageAudioCompanionAsset(assetPath, stageFolder, audio);
            }

            return IsAllowedStageFolderAssetType(asset);
        }

        private static bool IsAllowedStageFolderAssetType(UnityEngine.Object asset)
        {
            var type = asset.GetType();
            for (var i = 0; i < AllowedStageFolderAssetTypes.Length; i++)
            {
                if (AllowedStageFolderAssetTypes[i].IsAssignableFrom(type))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStageAudioCompanionAsset(
            string assetPath,
            string stageFolder,
            StageAudioDefinition audio)
        {
            if (audio == null ||
                !IsUnder(stageFolder, StageContentPaths.CampaignLevel01StagesRoot))
            {
                return false;
            }

            var stageIdValue = Path.GetFileName(stageFolder);
            if (!StageId.TryCreate(stageIdValue, out var stageId) ||
                !string.Equals(stageId.Value, stageIdValue, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(
                    Path.GetFileName(assetPath),
                    $"{stageId.Value}_Audio.asset",
                    StringComparison.Ordinal))
            {
                return false;
            }

            var owner = audio.OwnerEntry;
            if (owner == null ||
                !owner.StageId.Equals(stageId))
            {
                return false;
            }

            var ownerPath = AssetDatabase.GetAssetPath(owner);
            if (string.IsNullOrWhiteSpace(ownerPath) ||
                !string.Equals(Path.GetDirectoryName(ownerPath)?.Replace('\\', '/'), stageFolder, StringComparison.Ordinal))
            {
                return false;
            }

            var ownerGuid = AssetDatabase.AssetPathToGUID(ownerPath);
            if (string.IsNullOrWhiteSpace(ownerGuid) ||
                !string.Equals(audio.OwnerEntryGuid, ownerGuid, StringComparison.Ordinal))
            {
                return false;
            }

            var referencedAudio = owner.AudioDefinition;
            if (referencedAudio == null)
            {
                return false;
            }

            var audioGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var referencedAudioPath = AssetDatabase.GetAssetPath(referencedAudio);
            var referencedAudioGuid = AssetDatabase.AssetPathToGUID(referencedAudioPath);
            return !string.IsNullOrWhiteSpace(audioGuid) &&
                   string.Equals(referencedAudioGuid, audioGuid, StringComparison.Ordinal);
        }

        private static bool IsContentDependency(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                path.EndsWith(".cs", StringComparison.Ordinal) ||
                path.EndsWith(".asmdef", StringComparison.Ordinal))
            {
                return false;
            }

            return path.EndsWith(".asset", StringComparison.Ordinal) ||
                   path.EndsWith(".prefab", StringComparison.Ordinal) ||
                   path.EndsWith(".mat", StringComparison.Ordinal) ||
                   path.EndsWith(".png", StringComparison.Ordinal) ||
                   path.EndsWith(".jpg", StringComparison.Ordinal) ||
                   path.EndsWith(".jpeg", StringComparison.Ordinal) ||
                   path.EndsWith(".spriteatlas", StringComparison.Ordinal);
        }

        private static bool IsMetaOrNote(string path)
        {
            var fileName = Path.GetFileName(path);
            return string.Equals(fileName, "README_Deprecated_DoNotUse.md", StringComparison.Ordinal) ||
                   string.Equals(fileName, "README.md", StringComparison.Ordinal);
        }

        private static bool IsEditorAssetPath(string path)
        {
            return IsUnder(path, StageContentPaths.StagesRoot + "/Editor");
        }

        private static bool IsUnder(string path, string root)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   (string.Equals(path, root, StringComparison.Ordinal) ||
                    path.StartsWith(root + "/", StringComparison.Ordinal));
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(assetPath);
        }

        private static string NormalizeAssetPath(string path)
        {
            var normalized = path.Replace('\\', '/');
            var assetsIndex = normalized.IndexOf("Assets/", StringComparison.Ordinal);
            return assetsIndex >= 0 ? normalized[assetsIndex..] : normalized;
        }

        private static void AddPathError(
            StageValidationReport report,
            string code,
            string message,
            string path,
            StageValidationTiming timing)
        {
            report.Add(
                StageValidationSeverity.Error,
                code,
                $"{message} Path='{path}'.",
                AssetDatabase.LoadMainAssetAtPath(path),
                path,
                timing);
        }

        private static void AddReferenceError(
            StageValidationReport report,
            string code,
            string message,
            string ownerPath,
            string dependencyPath,
            StageValidationTiming timing)
        {
            report.Add(
                StageValidationSeverity.Error,
                code,
                $"{message} Owner='{ownerPath}' Dependency='{dependencyPath}'.",
                AssetDatabase.LoadMainAssetAtPath(ownerPath),
                ownerPath,
                timing);
        }
    }
}
