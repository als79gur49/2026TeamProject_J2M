using System;
using System.Collections.Generic;
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
            if (scenePaths == null)
            {
                return report;
            }

            for (var i = 0; i < scenePaths.Count; i++)
            {
                ValidateScene(scenePaths[i], options, report);
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
                for (var i = 0; i < installers.Length; i++)
                {
                    var stageInstaller = installers[i].GetComponentInChildren<StageBackedGameplayShowcaseInstallerBase>(true);
                    if (stageInstaller == null)
                    {
                        continue;
                    }

                    ValidateInstaller(scenePath, stageInstaller, options, report);
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
            var defaultStageIdProperty = serializedInstaller.FindProperty("defaultStageId");
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

            if (!IsSerializedStageIdValid(defaultStageIdProperty))
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.default-stage-id.invalid",
                    $"Production scene '{scenePath}' is missing a valid defaultStageId fallback.",
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

        private static bool IsSerializedStageIdValid(SerializedProperty property)
        {
            if (property == null)
            {
                return false;
            }

            var valueProperty = property.FindPropertyRelative("value");
            return valueProperty != null && StageIdNormalizer.IsCanonical(valueProperty.stringValue);
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
