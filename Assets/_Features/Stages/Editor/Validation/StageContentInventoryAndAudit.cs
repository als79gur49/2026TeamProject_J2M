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
    public readonly struct StageGameplayAssetInventoryItem
    {
        public StageGameplayAssetInventoryItem(
            string assetGuid,
            string assetPath,
            bool isCanonicalCatalogGameplay,
            bool isDuplicateLegacyGameplayAsset,
            bool hasLegacyPresentationIds)
        {
            AssetGuid = assetGuid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            IsCanonicalCatalogGameplay = isCanonicalCatalogGameplay;
            IsDuplicateLegacyGameplayAsset = isDuplicateLegacyGameplayAsset;
            HasLegacyPresentationIds = hasLegacyPresentationIds;
        }

        public string AssetGuid { get; }

        public string AssetPath { get; }

        public bool IsCanonicalCatalogGameplay { get; }

        public bool IsDuplicateLegacyGameplayAsset { get; }

        public bool HasLegacyPresentationIds { get; }
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
            string[] canonicalGameplayAssetGuids,
            StageGameplayAssetInventoryItem[] gameplayAssets,
            StageBuildSceneInventoryItem[] buildScenes,
            StageIdAliasEntry[] aliasEntries)
        {
            CatalogAssetPath = catalogAssetPath ?? string.Empty;
            CanonicalGameplayAssetGuids = canonicalGameplayAssetGuids ?? Array.Empty<string>();
            GameplayAssets = gameplayAssets ?? Array.Empty<StageGameplayAssetInventoryItem>();
            BuildScenes = buildScenes ?? Array.Empty<StageBuildSceneInventoryItem>();
            AliasEntries = aliasEntries ?? Array.Empty<StageIdAliasEntry>();
        }

        public string CatalogAssetPath { get; }

        public IReadOnlyList<string> CanonicalGameplayAssetGuids { get; }

        public IReadOnlyList<StageGameplayAssetInventoryItem> GameplayAssets { get; }

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
            var canonicalGameplayAssetGuids = BuildCanonicalGameplayGuidSet(catalog);
            var gameplayAssets = BuildGameplayAssetInventory(canonicalGameplayAssetGuids);
            var buildScenes = BuildEnabledBuildSceneInventory(directPlayCatalog);
            var aliasEntries = catalog?.StageIdAliasTable?.Entries?.ToArray() ?? Array.Empty<StageIdAliasEntry>();

            return new StageContentInventorySnapshot(
                catalogAssetPath,
                canonicalGameplayAssetGuids.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                gameplayAssets,
                buildScenes,
                aliasEntries);
        }

        private static HashSet<string> BuildCanonicalGameplayGuidSet(StageCatalog catalog)
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

        private static StageGameplayAssetInventoryItem[] BuildGameplayAssetInventory(ISet<string> canonicalGameplayAssetGuids)
        {
            var guids = AssetDatabase.FindAssets("t:StageDefinition", new[] { StagesRoot });
            var items = new List<StageGameplayAssetInventoryItem>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var stageDefinition = AssetDatabase.LoadAssetAtPath<StageDefinition>(assetPath);
                if (stageDefinition == null)
                {
                    continue;
                }

                items.Add(new StageGameplayAssetInventoryItem(
                    guids[i],
                    assetPath,
                    canonicalGameplayAssetGuids.Contains(guids[i]),
                    IsDuplicateLegacyGameplayAsset(assetPath),
                    HasLegacyPresentationIds(stageDefinition)));
            }

            return items
                .OrderBy(item => item.AssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsDuplicateLegacyGameplayAsset(string assetPath)
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
                    var installer = rootObjects[i].GetComponentInChildren<StageBackedGameplayShowcaseInstallerBase>(true);
                    if (installer == null)
                    {
                        continue;
                    }

                    installerCount++;
                    var serializedInstaller = new SerializedObject(installer);
                    var modeProperty = serializedInstaller.FindProperty("stageLoadSourceMode");
                    var stageDefinitionProperty = serializedInstaller.FindProperty("stageDefinition");
                    var stageContentEntryProperty = serializedInstaller.FindProperty("stageContentEntry");
                    var enemyCatalogProperty = serializedInstaller.FindProperty("enemyPresentationCatalog");
                    var staticCatalogProperty = serializedInstaller.FindProperty("staticEntityPresentationCatalog");

                    hasCompatModeResidue |= modeProperty != null && modeProperty.enumValueIndex != 0;
                    hasDirectStageDefinitionResidue |= stageDefinitionProperty?.objectReferenceValue != null;
                    hasSerializedEntryResidue |= stageContentEntryProperty?.objectReferenceValue != null;
                    hasEnemyCatalogResidue |= enemyCatalogProperty?.objectReferenceValue != null;
                    hasStaticCatalogResidue |= staticCatalogProperty?.objectReferenceValue != null;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }

            var hasDefaultStageIdResidue = HasSerializedDefaultStageIdResidue(scenePath);
            var hasDirectPlayCatalogCoverage = installerCount == 0 ||
                                               (directPlayCatalog != null && directPlayCatalog.HasScenePath(scenePath));

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

        private static bool HasLegacyPresentationIds(StageDefinition stageDefinition)
        {
            if (stageDefinition == null)
            {
                return false;
            }

            var spawns = stageDefinition.Spawns;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(spawns[i].PresentationId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class StageCompatAuditReport
    {
        public StageCompatAuditReport(
            StageContentInventorySnapshot snapshot,
            string[] canonicalGameplayWithLegacyPresentationIds,
            string[] nonCanonicalGameplayWithLegacyPresentationIds,
            string[] duplicateLegacyGameplayAssetPaths,
            string[] prunableAliasIds,
            string[] buildSceneResiduePaths,
            string[] buildSceneCoverageGapPaths,
            StageAliasUsageScanResult aliasUsage)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            CanonicalGameplayWithLegacyPresentationIds = canonicalGameplayWithLegacyPresentationIds ?? Array.Empty<string>();
            NonCanonicalGameplayWithLegacyPresentationIds = nonCanonicalGameplayWithLegacyPresentationIds ?? Array.Empty<string>();
            DuplicateLegacyGameplayAssetPaths = duplicateLegacyGameplayAssetPaths ?? Array.Empty<string>();
            PrunableAliasIds = prunableAliasIds ?? Array.Empty<string>();
            BuildSceneResiduePaths = buildSceneResiduePaths ?? Array.Empty<string>();
            BuildSceneCoverageGapPaths = buildSceneCoverageGapPaths ?? Array.Empty<string>();
            AliasUsage = aliasUsage ?? new StageAliasUsageScanResult(Array.Empty<StageAliasUsageHit>());
        }

        public StageContentInventorySnapshot Snapshot { get; }

        public IReadOnlyList<string> CanonicalGameplayWithLegacyPresentationIds { get; }

        public IReadOnlyList<string> NonCanonicalGameplayWithLegacyPresentationIds { get; }

        public IReadOnlyList<string> DuplicateLegacyGameplayAssetPaths { get; }

        public IReadOnlyList<string> PrunableAliasIds { get; }

        public IReadOnlyList<string> BuildSceneResiduePaths { get; }

        public IReadOnlyList<string> BuildSceneCoverageGapPaths { get; }

        public StageAliasUsageScanResult AliasUsage { get; }
    }

    public sealed class StageCompatUsageAuditor
    {
        private static readonly Regex TrailingCopyNumberRegex = new(@"\s+\d+$", RegexOptions.Compiled);
        private readonly StageContentInventoryQuery inventoryQuery;
        private readonly StageAliasUsageScanner aliasUsageScanner;

        public StageCompatUsageAuditor(
            StageContentInventoryQuery inventoryQuery = null,
            StageAliasUsageScanner aliasUsageScanner = null)
        {
            this.inventoryQuery = inventoryQuery ?? new StageContentInventoryQuery();
            this.aliasUsageScanner = aliasUsageScanner ?? new StageAliasUsageScanner();
        }

        public StageCompatAuditReport Audit(string catalogAssetPath = StageContentPaths.StageCatalogAssetPath)
        {
            return Audit(inventoryQuery.Capture(catalogAssetPath));
        }

        public StageCompatAuditReport Audit(StageContentInventorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var canonicalGameplayWithLegacyPresentationIds = snapshot.GameplayAssets
                .Where(item => item.IsCanonicalCatalogGameplay && item.HasLegacyPresentationIds)
                .Select(item => item.AssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var nonCanonicalGameplayWithLegacyPresentationIds = snapshot.GameplayAssets
                .Where(item => !item.IsCanonicalCatalogGameplay && item.HasLegacyPresentationIds)
                .Select(item => item.AssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var duplicateLegacyGameplayAssetPaths = snapshot.GameplayAssets
                .Where(item => item.IsDuplicateLegacyGameplayAsset)
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

            return new StageCompatAuditReport(
                snapshot,
                canonicalGameplayWithLegacyPresentationIds,
                nonCanonicalGameplayWithLegacyPresentationIds,
                duplicateLegacyGameplayAssetPaths,
                prunableAliasIds,
                buildSceneResiduePaths,
                buildSceneCoverageGapPaths,
                aliasUsage);
        }
    }
}
