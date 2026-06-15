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
            int launchContextCatalogResolvedInstallers,
            int retiredSerializedStageContentEntryResidue,
            int retiredLegacyStageDefinitionResidue,
            int removedDefaultStageIdFallbackResidue,
            int removedDirectStageDefinitionLoadResidue)
        {
            GuardSummary = RetiredStageLoadPathGuard.CreateSummary(
                launchContextCatalogResolvedInstallers,
                retiredSerializedStageContentEntryResidue,
                retiredLegacyStageDefinitionResidue,
                removedDefaultStageIdFallbackResidue,
                removedDirectStageDefinitionLoadResidue);
        }

        public RetiredStageLoadPathGuardSummary GuardSummary { get; }

        public int CatalogResolvedStageIdCount => GuardSummary.LaunchContextCatalogResolvedInstallers;

        public int SerializedStageContentEntryCount => GuardSummary.RetiredSerializedStageContentEntryResidue;

        public int LegacyStageDefinitionCount => GuardSummary.RetiredLegacyStageDefinitionResidue;

        public int RemovedDefaultStageIdFallbackResidueCount => GuardSummary.RemovedDefaultStageIdFallbackResidue;

        public int RemovedDirectStageDefinitionLoadResidueCount => GuardSummary.RemovedDirectStageDefinitionLoadResidue;
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
            var defaultStageIdResidueCount = 0;
            var directStageDefinitionResidueCount = 0;
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
                            .GetComponentInChildren<StageBackedGameplaySceneInstallerBase>(true);
                        if (stageInstaller == null)
                        {
                            continue;
                        }

                        var residue = RetiredStageLoadPathGuard.InspectInstaller(stageInstaller);
                        if (residue.HasDirectStageDefinitionResidue)
                        {
                            directStageDefinitionResidueCount++;
                        }

                        switch (residue.StageLoadSourceModeValue)
                        {
                            case RetiredStageLoadPathGuard.LaunchContextCatalogResolvedModeValue:
                                catalogResolvedCount++;
                                break;
                            case RetiredStageLoadPathGuard.RetiredSerializedStageContentEntryModeValue:
                                serializedEntryCount++;
                                break;
                            default:
                                legacyDefinitionCount++;
                                break;
                            }
                        }

                        if (RetiredStageLoadPathGuard
                            .InspectSceneText(scenes[i].path)
                            .HasRemovedDefaultStageIdFallbackResidue)
                        {
                            defaultStageIdResidueCount++;
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
                legacyDefinitionCount,
                defaultStageIdResidueCount,
                directStageDefinitionResidueCount);
        }

        private static void ValidateScene(
            string scenePath,
            StageEditorDirectPlayCatalog directPlayCatalog,
            IDictionary<StageId, string> cameraTopologyPresetPathByStageId,
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
                    var stageInstaller = installers[i].GetComponentInChildren<StageBackedGameplaySceneInstallerBase>(true);
                    if (stageInstaller == null)
                    {
                        continue;
                    }

                    hasStageInstaller = true;
                    ValidateInstaller(
                        scenePath,
                        stageInstaller,
                        directPlayCatalog,
                        cameraTopologyPresetPathByStageId,
                        options,
                        report);
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
            StageBackedGameplaySceneInstallerBase installer,
            StageEditorDirectPlayCatalog directPlayCatalog,
            IDictionary<StageId, string> cameraTopologyPresetPathByStageId,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var serializedInstaller = new SerializedObject(installer);
            var providerProperty = serializedInstaller.FindProperty("stageCatalogProvider");
            var enemyCatalogProperty = serializedInstaller.FindProperty("enemyPresentationCatalog");
            var staticCatalogProperty = serializedInstaller.FindProperty("staticEntityPresentationCatalog");
            var retiredResidue = RetiredStageLoadPathGuard.InspectInstaller(serializedInstaller);

            if (retiredResidue.HasDirectStageDefinitionResidue)
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

            if (retiredResidue.HasCompatModeResidue)
            {
                AddSceneIssue(
                    report,
                    ResolveProductionSceneContractSeverity(options),
                    "scene.compat-mode.production",
                    $"Production scene '{scenePath}' uses compat stage load mode value '{retiredResidue.StageLoadSourceModeValue}'.",
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

            if (retiredResidue.HasSerializedStageContentEntryResidue)
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
            if (RetiredStageLoadPathGuard
                .InspectSceneText(scenePath)
                .HasRemovedDefaultStageIdFallbackResidue)
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
