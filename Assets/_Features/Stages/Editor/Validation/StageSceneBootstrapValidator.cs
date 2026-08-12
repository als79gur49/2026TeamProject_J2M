using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Composition;
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
        internal const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        internal const string GameplayUiAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        public StageValidationReport ValidateEnabledBuildScenes(
            StageCatalogValidationOptions options = null,
            CampaignStageSequenceDefinition expectedCampaignSequence = null)
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

            var report = ValidateScenes(scenePaths, options, expectedCampaignSequence);
            RequireEnabledBuildScene(scenePaths, MainMenuScenePath, options, report);
            RequireEnabledBuildScene(scenePaths, GameplayUiAudioScenePath, options, report);
            return report;
        }

        public StageValidationReport ValidateScenes(
            IReadOnlyList<string> scenePaths,
            StageCatalogValidationOptions options = null,
            CampaignStageSequenceDefinition expectedCampaignSequence = null)
        {
            options ??= StageCatalogValidationOptions.Default;
            var report = new StageValidationReport();
            if (expectedCampaignSequence == null)
            {
                expectedCampaignSequence = CampaignStageSequenceAssetLoader.LoadCanonical(report, options.Timing);
            }

            var directPlayCatalog = StageEditorDirectPlayCatalog.LoadDefault();
            var cameraTopologyPresetPathByStageId = new Dictionary<StageId, string>();
            if (scenePaths == null)
            {
                return report;
            }

            for (var i = 0; i < scenePaths.Count; i++)
            {
                ValidateScene(
                    scenePaths[i],
                    directPlayCatalog,
                    cameraTopologyPresetPathByStageId,
                    expectedCampaignSequence,
                    options,
                    report);
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

                var scene = SceneManager.GetSceneByPath(scenes[i].path);
                var openedForValidation = !scene.IsValid() || !scene.isLoaded;
                var previousActiveScene = SceneManager.GetActiveScene();
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(scenes[i].path, OpenSceneMode.Additive);
                }

                try
                {
                    var installers = scene.GetRootGameObjects();
                    for (var rootIndex = 0; rootIndex < installers.Length; rootIndex++)
                    {
                        var stageInstaller = installers[rootIndex]
                            .GetComponentInChildren<StageBackedGameplaySceneInstallerBase>(true);
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
                    if (openedForValidation)
                    {
                        EditorSceneManager.CloseScene(scene, removeScene: true);
                        RestoreActiveScene(previousActiveScene);
                    }
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
            IDictionary<StageId, string> cameraTopologyPresetPathByStageId,
            CampaignStageSequenceDefinition expectedCampaignSequence,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedForValidation = !scene.IsValid() || !scene.isLoaded;
            var previousActiveScene = SceneManager.GetActiveScene();
            try
            {
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
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
                    var stageInstallers = installers[i]
                        .GetComponentsInChildren<StageBackedGameplaySceneInstallerBase>(true);
                    for (var installerIndex = 0; installerIndex < stageInstallers.Length; installerIndex++)
                    {
                        hasStageInstaller = true;
                        ValidateInstaller(
                            scenePath,
                            stageInstallers[installerIndex],
                            directPlayCatalog,
                            cameraTopologyPresetPathByStageId,
                            expectedCampaignSequence,
                            options,
                            report);
                    }

                    var mainMenuInstallers = installers[i]
                        .GetComponentsInChildren<MainMenuUiFlowInstaller>(true);
                    for (var installerIndex = 0; installerIndex < mainMenuInstallers.Length; installerIndex++)
                    {
                        ValidateSerializedCampaignSequenceReference(
                            scenePath,
                            mainMenuInstallers[installerIndex],
                            "_campaignStageSequenceDefinition",
                            expectedCampaignSequence,
                            options,
                            report);
                    }
                }

                if (hasStageInstaller)
                {
                    ValidateSceneLevelContracts(scenePath, directPlayCatalog, options, report);
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                    RestoreActiveScene(previousActiveScene);
                }
            }
        }

        private static void ValidateInstaller(
            string scenePath,
            StageBackedGameplaySceneInstallerBase installer,
            StageEditorDirectPlayCatalog directPlayCatalog,
            IDictionary<StageId, string> cameraTopologyPresetPathByStageId,
            CampaignStageSequenceDefinition expectedCampaignSequence,
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

            ValidateSerializedCampaignSequenceReference(
                scenePath,
                installer,
                "campaignStageSequenceDefinition",
                expectedCampaignSequence,
                options,
                report);

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

            ValidateCameraTopologyAuthoring(
                scenePath,
                installer,
                directPlayCatalog,
                cameraTopologyPresetPathByStageId,
                options,
                report);
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

            if (!directPlayCatalog.IsCanonicalShellScenePath(scenePath))
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.direct-play.catalog.missing",
                    $"Production scene '{scenePath}' is not the canonical gameplay shell in '{StageEditorDirectPlayCatalog.DefaultAssetPath}'.",
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

        private static void ValidateCameraTopologyAuthoring(
            string scenePath,
            StageBackedGameplaySceneInstallerBase installer,
            StageEditorDirectPlayCatalog directPlayCatalog,
            IDictionary<StageId, string> cameraTopologyPresetPathByStageId,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>();
            if (authoring == null)
            {
                return;
            }

            var serializedAuthoring = new SerializedObject(authoring);
            var sourceModeProperty = serializedAuthoring.FindProperty("sourceMode");
            var presetProperty = serializedAuthoring.FindProperty("preset");
            var sourceModeValue = sourceModeProperty == null
                ? (int)GameplayCameraTopologySourceMode.Inline
                : sourceModeProperty.enumValueIndex;
            var productionSeverity = ResolveProductionSceneContractSeverity(options);

            if (sourceModeValue != (int)GameplayCameraTopologySourceMode.Preset)
            {
                AddSceneIssue(
                    report,
                    productionSeverity,
                    "scene.camera-topology.inline.production",
                    $"Production scene '{scenePath}' still uses inline camera topology authoring. Stage-scoped preset mode is the canonical production path.",
                    authoring,
                    scenePath,
                    options);
                return;
            }

            if (presetProperty?.objectReferenceValue == null)
            {
                AddSceneIssue(
                    report,
                    productionSeverity,
                    "scene.camera-topology.preset.null",
                    $"Production scene '{scenePath}' uses preset camera topology mode but has no preset reference. Inline shared-tuning fallback is not authoritative in Preset mode.",
                    authoring,
                    scenePath,
                    options);
                return;
            }

            if (directPlayCatalog == null || !directPlayCatalog.IsCanonicalShellScenePath(scenePath))
            {
                return;
            }

            var presetPath = AssetDatabase.GetAssetPath(presetProperty.objectReferenceValue);
            if (string.IsNullOrWhiteSpace(presetPath))
            {
                return;
            }

            var shellKey = StageId.CreateOrThrow("canonical-shell");
            if (!cameraTopologyPresetPathByStageId.TryGetValue(shellKey, out var expectedPresetPath))
            {
                cameraTopologyPresetPathByStageId[shellKey] = presetPath;
                return;
            }

            if (!string.Equals(expectedPresetPath, presetPath, StringComparison.Ordinal))
            {
                AddSceneIssue(
                    report,
                    productionSeverity,
                    "scene.camera-topology.stage-preset.mismatch",
                    $"Production scene '{scenePath}' references camera topology preset '{presetPath}' instead of canonical shell preset '{expectedPresetPath}'.",
                    authoring,
                    scenePath,
                    options);
            }
        }

        private static StageValidationSeverity ResolveProductionSceneContractSeverity(StageCatalogValidationOptions options)
        {
            return options.Phase >= StageValidationPhase.Phase4_ProductionBootstrapConversion
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        internal static void ValidateCampaignSequenceReference(
            string scenePath,
            UnityEngine.Object owner,
            CampaignStageSequenceDefinition actualSequence,
            CampaignStageSequenceDefinition expectedSequence,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            options ??= StageCatalogValidationOptions.Default;
            if (actualSequence == null)
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.campaign-sequence.null",
                    $"Enabled production scene '{scenePath}' has a null campaign sequence reference.",
                    owner,
                    scenePath,
                    options.Timing);
                return;
            }

            if (expectedSequence != null && actualSequence != expectedSequence)
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.campaign-sequence.non-authoritative",
                    $"Enabled production scene '{scenePath}' references campaign sequence '{AssetDatabase.GetAssetPath(actualSequence)}' instead of authoritative asset '{CampaignStageSequenceAssetLoader.CanonicalAssetPath}'.",
                    owner,
                    scenePath,
                    options.Timing);
            }
        }

        private static void ValidateSerializedCampaignSequenceReference(
            string scenePath,
            UnityEngine.Object owner,
            string propertyName,
            CampaignStageSequenceDefinition expectedSequence,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var serializedOwner = new SerializedObject(owner);
            var sequenceProperty = serializedOwner.FindProperty(propertyName);
            if (sequenceProperty == null)
            {
                report.Add(
                    ResolveProductionSceneContractSeverity(options),
                    "scene.campaign-sequence.field-missing",
                    $"Enabled production scene '{scenePath}' component '{owner.GetType().Name}' has no serialized campaign sequence field '{propertyName}'.",
                    owner,
                    scenePath,
                    options.Timing);
                return;
            }

            ValidateCampaignSequenceReference(
                scenePath,
                owner,
                sequenceProperty.objectReferenceValue as CampaignStageSequenceDefinition,
                expectedSequence,
                options,
                report);
        }

        private static void RequireEnabledBuildScene(
            IReadOnlyList<string> enabledScenePaths,
            string requiredScenePath,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            for (var i = 0; i < enabledScenePaths.Count; i++)
            {
                if (string.Equals(enabledScenePaths[i], requiredScenePath, StringComparison.Ordinal))
                {
                    return;
                }
            }

            report.Add(
                ResolveProductionSceneContractSeverity(options ?? StageCatalogValidationOptions.Default),
                "scene.campaign-sequence.required-build-scene-disabled",
                $"Required production scene '{requiredScenePath}' must be enabled so its authoritative campaign sequence reference is included in Player builds.",
                assetPath: requiredScenePath,
                timing: options?.Timing ?? StageValidationTiming.EditorAuthoring);
        }

        private static void RestoreActiveScene(Scene previousActiveScene)
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
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
