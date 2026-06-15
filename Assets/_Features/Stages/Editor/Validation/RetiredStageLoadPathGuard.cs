using System;
using System.IO;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public readonly struct RetiredStageLoadPathInstallerResidue
    {
        public RetiredStageLoadPathInstallerResidue(
            int compatModeValue,
            bool hasDirectStageDefinitionResidue,
            bool hasSerializedStageContentEntryResidue)
        {
            CompatModeValue = compatModeValue;
            HasDirectStageDefinitionResidue = hasDirectStageDefinitionResidue;
            HasSerializedStageContentEntryResidue = hasSerializedStageContentEntryResidue;
        }

        public int CompatModeValue { get; }

        public bool HasCompatModeResidue => CompatModeValue != RetiredStageLoadPathGuard.LaunchContextCatalogResolvedModeValue;

        public bool HasDirectStageDefinitionResidue { get; }

        public bool HasSerializedStageContentEntryResidue { get; }
    }

    public readonly struct RetiredStageLoadPathSceneResidue
    {
        public RetiredStageLoadPathSceneResidue(bool hasRemovedDefaultStageIdFallbackResidue)
        {
            HasRemovedDefaultStageIdFallbackResidue = hasRemovedDefaultStageIdFallbackResidue;
        }

        public bool HasRemovedDefaultStageIdFallbackResidue { get; }
    }

    public readonly struct RetiredStageLoadPathGuardSummary
    {
        public RetiredStageLoadPathGuardSummary(
            int launchContextCatalogResolvedInstallers,
            int retiredSerializedStageContentEntryResidue,
            int retiredLegacyStageDefinitionResidue,
            int removedDefaultStageIdFallbackResidue,
            int removedDirectStageDefinitionLoadResidue)
        {
            LaunchContextCatalogResolvedInstallers = launchContextCatalogResolvedInstallers;
            RetiredSerializedStageContentEntryResidue = retiredSerializedStageContentEntryResidue;
            RetiredLegacyStageDefinitionResidue = retiredLegacyStageDefinitionResidue;
            RemovedDefaultStageIdFallbackResidue = removedDefaultStageIdFallbackResidue;
            RemovedDirectStageDefinitionLoadResidue = removedDirectStageDefinitionLoadResidue;
        }

        public int LaunchContextCatalogResolvedInstallers { get; }

        public int RetiredSerializedStageContentEntryResidue { get; }

        public int RetiredLegacyStageDefinitionResidue { get; }

        public int RemovedDefaultStageIdFallbackResidue { get; }

        public int RemovedDirectStageDefinitionLoadResidue { get; }
    }

    public static class RetiredStageLoadPathGuard
    {
        public const int LaunchContextCatalogResolvedModeValue = 0;
        public const int RetiredSerializedStageContentEntryModeValue = 1;

        public const string CompatModePropertyName = "stageLoadSourceMode";
        public const string DirectStageDefinitionPropertyName = "stageDefinition";
        public const string SerializedStageContentEntryPropertyName = "stageContentEntry";
        public const string RemovedDefaultStageIdFallbackFieldToken = "\ndefaultStageId:";

        public static RetiredStageLoadPathInstallerResidue InspectInstaller(
            StageBackedGameplaySceneInstallerBase installer)
        {
            if (installer == null)
            {
                return default;
            }

            return InspectInstaller(new SerializedObject(installer));
        }

        public static RetiredStageLoadPathInstallerResidue InspectInstaller(SerializedObject serializedInstaller)
        {
            if (serializedInstaller == null)
            {
                return default;
            }

            var modeProperty = serializedInstaller.FindProperty(CompatModePropertyName);
            var stageDefinitionProperty = serializedInstaller.FindProperty(DirectStageDefinitionPropertyName);
            var stageContentEntryProperty = serializedInstaller.FindProperty(SerializedStageContentEntryPropertyName);

            return new RetiredStageLoadPathInstallerResidue(
                modeProperty == null ? LaunchContextCatalogResolvedModeValue : modeProperty.enumValueIndex,
                stageDefinitionProperty?.objectReferenceValue != null,
                stageContentEntryProperty?.objectReferenceValue != null);
        }

        public static RetiredStageLoadPathSceneResidue InspectSceneText(string scenePath)
        {
            return new RetiredStageLoadPathSceneResidue(HasRemovedDefaultStageIdFallbackResidue(scenePath));
        }

        public static bool HasRemovedDefaultStageIdFallbackResidue(string scenePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.Combine(projectRoot, (scenePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return false;
            }

            var sceneText = File.ReadAllText(fullPath);
            return sceneText.Contains(RemovedDefaultStageIdFallbackFieldToken, StringComparison.Ordinal);
        }

        public static RetiredStageLoadPathGuardSummary CreateSummary(
            int launchContextCatalogResolvedInstallers,
            int retiredSerializedStageContentEntryResidue,
            int retiredLegacyStageDefinitionResidue,
            int removedDefaultStageIdFallbackResidue,
            int removedDirectStageDefinitionLoadResidue)
        {
            return new RetiredStageLoadPathGuardSummary(
                launchContextCatalogResolvedInstallers,
                retiredSerializedStageContentEntryResidue,
                retiredLegacyStageDefinitionResidue,
                removedDefaultStageIdFallbackResidue,
                removedDirectStageDefinitionLoadResidue);
        }
    }
}
