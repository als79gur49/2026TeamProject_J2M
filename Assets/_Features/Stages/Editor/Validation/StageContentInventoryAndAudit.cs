using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages.Editor
{
    internal enum StageAuthoringSurfaceKind
    {
        StageRoot,
        GameplayCompanion,
        PresentationCompanion,
        AudioCompanion,
        RetiredCompanionGuard,
        RetiredLoadGuard,
        RetiredLoadDetector,
        EditorDirectPlaySupport,
        PresentationOnlyBinding,
        WeakHelperReference,
    }

    internal static class StageAuthoringSurfaceClassificationLabels
    {
        public static string GetLabel(StageAuthoringSurfaceKind kind)
        {
            return kind switch
            {
                StageAuthoringSurfaceKind.StageRoot => "Stage Root",
                StageAuthoringSurfaceKind.GameplayCompanion => "Gameplay Companion",
                StageAuthoringSurfaceKind.PresentationCompanion => "Presentation Companion",
                StageAuthoringSurfaceKind.AudioCompanion => "Audio Companion",
                StageAuthoringSurfaceKind.RetiredCompanionGuard => "Retired Companion Guard",
                StageAuthoringSurfaceKind.RetiredLoadGuard => "Retired Load Guard",
                StageAuthoringSurfaceKind.RetiredLoadDetector => "Retired Load Detector",
                StageAuthoringSurfaceKind.EditorDirectPlaySupport => "Editor Direct-Play Support",
                StageAuthoringSurfaceKind.PresentationOnlyBinding => "Presentation-Only Binding",
                StageAuthoringSurfaceKind.WeakHelperReference => "Weak Helper / Reference",
                _ => "Unknown",
            };
        }

        public static string Format(string surface, StageAuthoringSurfaceKind kind)
        {
            return $"{surface}: {GetLabel(kind)}";
        }
    }

    public readonly struct StageGameplayCompanionInventoryItem
    {
        public StageGameplayCompanionInventoryItem(
            string assetGuid,
            string assetPath,
            bool isCanonicalCatalogGameplayCompanion,
            bool isDuplicateLegacyGameplayCompanionAsset)
        {
            AssetGuid = assetGuid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            IsCanonicalCatalogGameplayCompanion = isCanonicalCatalogGameplayCompanion;
            IsDuplicateLegacyGameplayCompanionAsset = isDuplicateLegacyGameplayCompanionAsset;
        }

        public string AssetGuid { get; }

        public string AssetPath { get; }

        public string ClassificationLabel =>
            StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.GameplayCompanion);

        public bool IsCanonicalCatalogGameplayCompanion { get; }

        public bool IsDuplicateLegacyGameplayCompanionAsset { get; }
    }

    public readonly struct StageBuildSceneInventoryItem
    {
        public StageBuildSceneInventoryItem(
            string scenePath,
            int installerCount,
            bool hasCompatModeResidue,
            bool hasDirectStageDefinitionResidue,
            bool hasSerializedEntryResidue,
            bool hasEnemyCatalogResidue,
            bool hasStaticCatalogResidue,
            bool hasDefaultStageIdResidue,
            bool hasDirectPlayCatalogCoverage)
        {
            ScenePath = scenePath ?? string.Empty;
            InstallerCount = installerCount;
            HasCompatModeResidue = hasCompatModeResidue;
            HasDirectStageDefinitionResidue = hasDirectStageDefinitionResidue;
            HasSerializedEntryResidue = hasSerializedEntryResidue;
            HasEnemyCatalogResidue = hasEnemyCatalogResidue;
            HasStaticCatalogResidue = hasStaticCatalogResidue;
            HasDefaultStageIdResidue = hasDefaultStageIdResidue;
            HasDirectPlayCatalogCoverage = hasDirectPlayCatalogCoverage;
        }

        public string ScenePath { get; }

        public string ResidueClassificationLabel =>
            StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.RetiredLoadDetector);

        public string DirectPlayCoverageClassificationLabel =>
            StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.EditorDirectPlaySupport);

        public int InstallerCount { get; }

        public bool HasCompatModeResidue { get; }

        public bool HasDirectStageDefinitionResidue { get; }

        public bool HasSerializedEntryResidue { get; }

        public bool HasEnemyCatalogResidue { get; }

        public bool HasStaticCatalogResidue { get; }

        public bool HasDefaultStageIdResidue { get; }

        public bool HasDirectPlayCatalogCoverage { get; }

        public bool HasAnyResidue =>
            HasCompatModeResidue ||
            HasDirectStageDefinitionResidue ||
            HasSerializedEntryResidue ||
            HasEnemyCatalogResidue ||
            HasStaticCatalogResidue ||
            HasDefaultStageIdResidue;

        public bool HasCoverageGap => InstallerCount > 0 && !HasDirectPlayCatalogCoverage;
    }

    public sealed class StageContentInventorySnapshot
    {
        public StageContentInventorySnapshot(
            string catalogAssetPath,
            string[] canonicalGameplayCompanionAssetGuids,
            StageGameplayCompanionInventoryItem[] gameplayCompanionAssets,
            StageBuildSceneInventoryItem[] buildScenes,
            StageIdAliasEntry[] aliasEntries)
        {
            CatalogAssetPath = catalogAssetPath ?? string.Empty;
            CanonicalGameplayCompanionAssetGuids = canonicalGameplayCompanionAssetGuids ?? Array.Empty<string>();
            GameplayCompanionAssets = gameplayCompanionAssets ?? Array.Empty<StageGameplayCompanionInventoryItem>();
            BuildScenes = buildScenes ?? Array.Empty<StageBuildSceneInventoryItem>();
            AliasEntries = aliasEntries ?? Array.Empty<StageIdAliasEntry>();
        }

        public string CatalogAssetPath { get; }

        public string StageContentRootClassificationLabel =>
            StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.StageRoot);

        public IReadOnlyList<string> CanonicalGameplayCompanionAssetGuids { get; }

        public IReadOnlyList<StageGameplayCompanionInventoryItem> GameplayCompanionAssets { get; }

        public IReadOnlyList<StageBuildSceneInventoryItem> BuildScenes { get; }

        public IReadOnlyList<StageIdAliasEntry> AliasEntries { get; }
    }

    public sealed class StageContentInventoryQuery
    {
        private const string DefaultCatalogAssetPath = StageContentPaths.StageCatalogAssetPath;
        private const string StagesRoot = "Assets/_Features/Stages";
        private static readonly Regex TrailingCopyNumberRegex = new(@"\s+\d+$", RegexOptions.Compiled);

        public StageContentInventorySnapshot Capture(string catalogAssetPath = DefaultCatalogAssetPath)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(catalogAssetPath);
            var directPlayCatalog = StageEditorDirectPlayCatalog.LoadDefault();
            var canonicalGameplayCompanionAssetGuids = BuildCanonicalGameplayCompanionGuidSet(catalog);
            var gameplayCompanionAssets = BuildGameplayCompanionAssetInventory(canonicalGameplayCompanionAssetGuids);
            var buildScenes = BuildEnabledBuildSceneInventory(directPlayCatalog);
            var aliasEntries = catalog?.StageIdAliasTable?.Entries?.ToArray() ?? Array.Empty<StageIdAliasEntry>();

            return new StageContentInventorySnapshot(
                catalogAssetPath,
                canonicalGameplayCompanionAssetGuids.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                gameplayCompanionAssets,
                buildScenes,
                aliasEntries);
        }

        private static HashSet<string> BuildCanonicalGameplayCompanionGuidSet(StageCatalog catalog)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (catalog == null)
            {
                return result;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                var gameplayDefinition = entries[i] != null ? entries[i].GameplayDefinition : null;
                if (gameplayDefinition == null)
                {
                    continue;
                }

                var assetPath = AssetDatabase.GetAssetPath(gameplayDefinition);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                result.Add(AssetDatabase.AssetPathToGUID(assetPath));
            }

            return result;
        }

        private static StageGameplayCompanionInventoryItem[] BuildGameplayCompanionAssetInventory(ISet<string> canonicalGameplayCompanionAssetGuids)
        {
            var guids = AssetDatabase.FindAssets("t:StageDefinition", new[] { StagesRoot });
            var items = new List<StageGameplayCompanionInventoryItem>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var stageDefinition = AssetDatabase.LoadAssetAtPath<StageDefinition>(assetPath);
                if (stageDefinition == null)
                {
                    continue;
                }

                items.Add(new StageGameplayCompanionInventoryItem(
                    guids[i],
                    assetPath,
                    canonicalGameplayCompanionAssetGuids.Contains(guids[i]),
                    IsDuplicateLegacyGameplayCompanionAsset(assetPath)));
            }

            return items
                .OrderBy(item => item.AssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsDuplicateLegacyGameplayCompanionAsset(string assetPath)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
            return TrailingCopyNumberRegex.IsMatch(fileNameWithoutExtension);
        }

        private static StageBuildSceneInventoryItem[] BuildEnabledBuildSceneInventory(
            StageEditorDirectPlayCatalog directPlayCatalog)
        {
            var scenes = EditorBuildSettings.scenes;
            var items = new List<StageBuildSceneInventoryItem>(scenes.Length);
            for (var i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled)
                {
                    continue;
                }

                items.Add(CaptureBuildSceneInventory(scenes[i].path, directPlayCatalog));
            }

            return items.ToArray();
        }

        private static StageBuildSceneInventoryItem CaptureBuildSceneInventory(
            string scenePath,
            StageEditorDirectPlayCatalog directPlayCatalog)
        {
            var installerCount = 0;
            var hasCompatModeResidue = false;
            var hasDirectStageDefinitionResidue = false;
            var hasSerializedEntryResidue = false;
            var hasEnemyCatalogResidue = false;
            var hasStaticCatalogResidue = false;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var rootObjects = scene.GetRootGameObjects();
                for (var i = 0; i < rootObjects.Length; i++)
                {
                    var installer = rootObjects[i].GetComponentInChildren<StageBackedGameplaySceneInstallerBase>(true);
                    if (installer == null)
                    {
                        continue;
                    }

                    installerCount++;
                    var serializedInstaller = new SerializedObject(installer);
                    var enemyCatalogProperty = serializedInstaller.FindProperty("enemyPresentationCatalog");
                    var staticCatalogProperty = serializedInstaller.FindProperty("staticEntityPresentationCatalog");
                    var retiredResidue = RetiredStageLoadPathGuard.InspectInstaller(serializedInstaller);

                    hasCompatModeResidue |= retiredResidue.HasCompatModeResidue;
                    hasDirectStageDefinitionResidue |= retiredResidue.HasDirectStageDefinitionResidue;
                    hasSerializedEntryResidue |= retiredResidue.HasSerializedStageContentEntryResidue;
                    hasEnemyCatalogResidue |= enemyCatalogProperty?.objectReferenceValue != null;
                    hasStaticCatalogResidue |= staticCatalogProperty?.objectReferenceValue != null;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }

            var hasDefaultStageIdResidue = RetiredStageLoadPathGuard
                .InspectSceneText(scenePath)
                .HasRemovedDefaultStageIdFallbackResidue;
            var hasDirectPlayCatalogCoverage = installerCount == 0 ||
                                               (directPlayCatalog != null && directPlayCatalog.IsCanonicalShellScenePath(scenePath));

            return new StageBuildSceneInventoryItem(
                scenePath,
                installerCount,
                hasCompatModeResidue,
                hasDirectStageDefinitionResidue,
                hasSerializedEntryResidue,
                hasEnemyCatalogResidue,
                hasStaticCatalogResidue,
                hasDefaultStageIdResidue,
                hasDirectPlayCatalogCoverage);
        }
    }

    public sealed class StageContentInventoryResidueAuditReport
    {
        public StageContentInventoryResidueAuditReport(
            StageContentInventorySnapshot snapshot,
            string[] duplicateLegacyGameplayCompanionAssetPaths,
            string[] prunableAliasIds,
            string[] buildSceneResiduePaths,
            string[] buildSceneCoverageGapPaths,
            StageAliasUsageScanResult aliasUsage)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            DuplicateLegacyGameplayCompanionAssetPaths = duplicateLegacyGameplayCompanionAssetPaths ?? Array.Empty<string>();
            PrunableAliasIds = prunableAliasIds ?? Array.Empty<string>();
            BuildSceneResiduePaths = buildSceneResiduePaths ?? Array.Empty<string>();
            BuildSceneCoverageGapPaths = buildSceneCoverageGapPaths ?? Array.Empty<string>();
            AliasUsage = aliasUsage ?? new StageAliasUsageScanResult(Array.Empty<StageAliasUsageHit>());
        }

        public StageContentInventorySnapshot Snapshot { get; }

        public IReadOnlyList<string> DuplicateLegacyGameplayCompanionAssetPaths { get; }

        public IReadOnlyList<string> PrunableAliasIds { get; }

        public IReadOnlyList<string> BuildSceneResiduePaths { get; }

        public IReadOnlyList<string> BuildSceneCoverageGapPaths { get; }

        public StageAliasUsageScanResult AliasUsage { get; }
    }

    public sealed class StageContentInventoryResidueAuditor
    {
        private static readonly Regex TrailingCopyNumberRegex = new(@"\s+\d+$", RegexOptions.Compiled);
        private readonly StageContentInventoryQuery inventoryQuery;
        private readonly StageAliasUsageScanner aliasUsageScanner;

        public StageContentInventoryResidueAuditor(
            StageContentInventoryQuery inventoryQuery = null,
            StageAliasUsageScanner aliasUsageScanner = null)
        {
            this.inventoryQuery = inventoryQuery ?? new StageContentInventoryQuery();
            this.aliasUsageScanner = aliasUsageScanner ?? new StageAliasUsageScanner();
        }

        public StageContentInventoryResidueAuditReport Audit(string catalogAssetPath = StageContentPaths.StageCatalogAssetPath)
        {
            return Audit(inventoryQuery.Capture(catalogAssetPath));
        }

        public StageContentInventoryResidueAuditReport Audit(StageContentInventorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var duplicateLegacyGameplayCompanionAssetPaths = snapshot.GameplayCompanionAssets
                .Where(item => item.IsDuplicateLegacyGameplayCompanionAsset)
                .Select(item => item.AssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var prunableAliasIds = snapshot.AliasEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.DeprecatedStageId))
                .Select(entry => entry.DeprecatedStageId.Trim())
                .Where(aliasId => TrailingCopyNumberRegex.IsMatch(aliasId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(aliasId => aliasId, StringComparer.Ordinal)
                .ToArray();
            var buildSceneResiduePaths = snapshot.BuildScenes
                .Where(item => item.HasAnyResidue)
                .Select(item => item.ScenePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var buildSceneCoverageGapPaths = snapshot.BuildScenes
                .Where(item => item.HasCoverageGap)
                .Select(item => item.ScenePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var aliasUsage = aliasUsageScanner.Scan(StageAliasUsageScanner.P3HistoricalAliasIds);

            return new StageContentInventoryResidueAuditReport(
                snapshot,
                duplicateLegacyGameplayCompanionAssetPaths,
                prunableAliasIds,
                buildSceneResiduePaths,
                buildSceneCoverageGapPaths,
                aliasUsage);
        }
    }
}
