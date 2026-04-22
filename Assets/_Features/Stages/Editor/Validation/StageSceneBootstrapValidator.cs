using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages.Editor
{
    public readonly struct StageSceneBootstrapUsageSummary
    {
        public StageSceneBootstrapUsageSummary(
            int catalogResolvedStageIdCount,
            int serializedStageContentEntryCount,
            int legacyStageDefinitionCount)
        {
            CatalogResolvedStageIdCount = catalogResolvedStageIdCount;
            SerializedStageContentEntryCount = serializedStageContentEntryCount;
            LegacyStageDefinitionCount = legacyStageDefinitionCount;
        }

        public int CatalogResolvedStageIdCount { get; }

        public int SerializedStageContentEntryCount { get; }

        public int LegacyStageDefinitionCount { get; }
    }

    public sealed class StageSceneBootstrapValidator
    {
        public StageValidationReport ValidateEnabledBuildScenes(StageCatalogValidationOptions options = null)
        {
            var scenePaths = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    scenePaths.Add(scenes[i].path);
                }
            }

            return ValidateScenes(scenePaths, options);
        }

        public StageValidationReport ValidateScenes(
            IReadOnlyList<string> scenePaths,
            StageCatalogValidationOptions options = null)
        {
            options ??= StageCatalogValidationOptions.Default;
            var report = new StageValidationReport();
            var directPlayCatalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (scenePaths == null)
            {
                return report;
            }

            for (var i = 0; i < scenePaths.Count; i++)
            {
                ValidateScene(scenePaths[i], directPlayCatalog, options, report);
            }

            return report;
        }

        public StageSceneBootstrapUsageSummary SummarizeEnabledBuildSceneModes()
        {
            var catalogResolvedCount = 0;
            var serializedEntryCount = 0;
            var legacyDefinitionCount = 0;
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled)
                {
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenes[i].path, OpenSceneMode.Additive);
                try
                {
                    var installers = scene.GetRootGameObjects();
                    for (var rootIndex = 0; rootIndex < installers.Length; rootIndex++)
                    {
                        var stageInstaller = installers[rootIndex]
                            .GetComponentInChildren<StageBackedGameplayShowcaseInstallerBase>(true);
                        if (stageInstaller == null)
                        {
                            continue;
                        }

                        var serializedInstaller = new SerializedObject(stageInstaller);
                        var modeProperty = serializedInstaller.FindProperty("stageLoadSourceMode");
                        var modeValue = modeProperty == null ? 0 : modeProperty.enumValueIndex;
                        switch (modeValue)
                        {
                            case 0:
                                catalogResolvedCount++;
                                break;
                            case 1:
                                serializedEntryCount++;
                                break;
                            default:
                                legacyDefinitionCount++;
                                break;
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }

            return new StageSceneBootstrapUsageSummary(
                catalogResolvedCount,
                serializedEntryCount,
                legacyDefinitionCount);
        }

        private static void ValidateScene(
            string scenePath,
            StageEditorDirectPlayCatalog directPlayCatalog,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            catch (Exception exception)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "scene.open.failed",
                    $"Failed to open scene '{scenePath}' for bootstrap validation: {exception.Message}",
                    assetPath: scenePath,
                    timing: options.Timing);
                return;
            }

            try
            {
                var installers = scene.GetRootGameObjects();
                var hasStageInstaller = false;
                for (var i = 0; i < installers.Length; i++)
                {
                    var stageInstaller = installers[i].GetComponentInChildren<StageBackedGameplayShowcaseInstallerBase>(true);
                    if (stageInstaller == null)
                    {
                        continue;
                    }

                    hasStageInstaller = true;
                    ValidateInstaller(scenePath, stageInstaller, options, report);
                }

                if (hasStageInstaller)
                {
                    ValidateSceneLevelContracts(scenePath, directPlayCatalog, options, report);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ValidateInstaller(
            string scenePath,
            StageBackedGameplayShowcaseInstallerBase installer,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var serializedInstaller = new SerializedObject(installer);
            var modeProperty = serializedInstaller.FindProperty("stageLoadSourceMode");
            var providerProperty = serializedInstaller.FindProperty("stageCatalogProvider");
            var stageDefinitionProperty = serializedInstaller.FindProperty("stageDefinition");
            var stageContentEntryProperty = serializedInstaller.FindProperty("stageContentEntry");
            var enemyCatalogProperty = serializedInstaller.FindProperty("enemyPresentationCatalog");
            var staticCatalogProperty = serializedInstaller.FindProperty("staticEntityPresentationCatalog");
            var hasCompatModeProperty = modeProperty != null;
            var usesCompatMode = hasCompatModeProperty && modeProperty.enumValueIndex != 0;

            if (stageDefinitionProperty != null && stageDefinitionProperty.objectReferenceValue != null)
            {
                AddSceneIssue(
                    report,
                    ResolveProductionSceneContractSeverity(options),
                    "scene.stage-definition.direct-ref",
                    $"Production scene '{scenePath}' still serializes a direct stageDefinition reference.",
                    installer,
                    scenePath,
                    options);
            }

            if (usesCompatMode)
            {
                AddSceneIssue(
                    report,
                    ResolveProductionSceneContractSeverity(options),
                    "scene.compat-mode.production",
                    $"Production scene '{scenePath}' uses compat stage load mode value '{modeProperty.enumValueIndex}'.",
                    installer,
                    scenePath,
                    options);
            }

            if (providerProperty?.objectReferenceValue == null)
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.catalog-provider.null",
                    $"Production scene '{scenePath}' is missing its stageCatalogProvider reference.",
                    installer,
                    scenePath,
                    options.Timing);
            }

            if (stageContentEntryProperty?.objectReferenceValue != null)
            {
                AddSceneIssue(
                    report,
                    ResolveProductionSceneContractSeverity(options),
                    "scene.serialized-entry.residue",
                    $"Production scene '{scenePath}' still serializes a compat StageContentEntry reference.",
                    installer,
                    scenePath,
                    options);
            }

            if (enemyCatalogProperty?.objectReferenceValue != null)
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.enemy-catalog.residue",
                    $"Production scene '{scenePath}' still serializes an enemyPresentationCatalog compat fallback.",
                    installer,
                    scenePath,
                    options.Timing);
            }

            if (staticCatalogProperty?.objectReferenceValue != null)
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.static-catalog.residue",
                    $"Production scene '{scenePath}' still serializes a staticEntityPresentationCatalog compat fallback.",
                    installer,
                    scenePath,
                    options.Timing);
            }
        }

        private static void ValidateSceneLevelContracts(
            string scenePath,
            StageEditorDirectPlayCatalog directPlayCatalog,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (HasSerializedDefaultStageIdResidue(scenePath))
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.default-stage-id.residue",
                    $"Production scene '{scenePath}' still contains serialized defaultStageId residue.",
                    assetPath: scenePath,
                    timing: options.Timing);
            }

            if (directPlayCatalog == null)
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.direct-play.catalog.null",
                    $"Production scene '{scenePath}' requires a {nameof(StageEditorDirectPlayCatalog)} asset at '{StageEditorDirectPlayCatalog.DefaultAssetPath}'.",
                    assetPath: scenePath,
                    timing: options.Timing);
                return;
            }

            if (!directPlayCatalog.TryResolveScenePath(scenePath, out _))
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.direct-play.catalog.missing",
                    $"Production scene '{scenePath}' is not registered in '{StageEditorDirectPlayCatalog.DefaultAssetPath}'.",
                    directPlayCatalog,
                    StageEditorDirectPlayCatalog.DefaultAssetPath,
                    options.Timing);
            }
        }

        private static bool HasSerializedDefaultStageIdResidue(string scenePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.Combine(projectRoot, scenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return false;
            }

            var sceneText = File.ReadAllText(fullPath);
            return sceneText.Contains("\ndefaultStageId:", StringComparison.Ordinal);
        }

        private static StageValidationSeverity ResolveProductionSceneContractSeverity(StageCatalogValidationOptions options)
        {
            return options.Phase >= StageValidationPhase.Phase4_ProductionBootstrapConversion
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        private static void AddSceneIssue(
            StageValidationReport report,
            StageValidationSeverity severity,
            string issueCode,
            string message,
            UnityEngine.Object context,
            string scenePath,
            StageCatalogValidationOptions options)
        {
            if (options.WaiverList != null &&
                options.WaiverList.IsWaived(issueCode, scenePath, options.Phase))
            {
                return;
            }

            report.Add(severity, issueCode, message, context, scenePath, options.Timing);
        }
    }
}
