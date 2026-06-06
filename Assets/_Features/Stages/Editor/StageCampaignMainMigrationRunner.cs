using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageCampaignMainMigrationRunner
    {
        private const string ReportRelativePath = "Temp/StageCampaignMainMigrationReport.md";
        private static readonly string[] LegacyCatalogAssetPaths =
        {
            "Assets/_Features/Stages/Content/StageCatalog.asset",
            "Assets/_Features/Stages/Content/StageCatalogProvider.asset",
            "Assets/_Features/Stages/Content/StageIdAliasTable.asset",
            "Assets/_Features/Stages/Content/CampaignStageSequence.asset",
        };

        private static readonly string[] SupportRoots =
        {
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase",
            "Assets/_Features/Stages/Stage_TutorialScene",
        };

        public static void ExecuteFromCommandLine()
        {
            try
            {
                var report = Execute();
                Debug.Log($"Campaign-main stage content migration completed. Report: {GetReportPath()}");
                if (report.GovernanceReport.HasErrors)
                {
                    Debug.LogError("Campaign-main stage content migration completed with governance errors.");
                    LogIssues("Campaign Governance", report.GovernanceReport);
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Stages/Migrations/Run Campaign Main Content Migration")]
        public static void ExecuteFromMenu()
        {
            Execute();
        }

        public static StageCampaignMigrationReport Execute()
        {
            var report = new StageCampaignMigrationReport();

            EnsureCampaignFolders();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MoveAndRenameCatalogAssets(report);
            CreateMetadataAssets(report);
            MoveStageContentFolders(report);
            MoveCampaignStageConditionAssets(report);
            MoveSupportAssets(report);
            MoveExternalPresentationCatalogDependencies(report);
            DeleteEmptyStageSubfolders(report);
            DeleteLegacyFolders(report);
            SyncCampaignCatalogEntries(report);
            SyncDirectPlayCatalog(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.CampaignAssetCount = CountAssetsUnder(StageContentPaths.CampaignRoot);

            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            report.CatalogReport = new StageCatalogValidator().Validate(
                catalog,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    RequireClearEvaluationDefinition = true,
                    RequireRewardDefinition = true,
                    RequireProgressionDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                    Phase = StageValidationPhase.Phase6_SunsetFinalization,
                    AssetMetadataProvider = EditorAssetMetadataProvider.Instance,
                });
            report.GovernanceReport = new StageCampaignContentGovernanceValidator()
                .Validate(StageValidationTiming.TestOrCi);
            report.AddressablesResult = IsAddressablesInstalled()
                ? "Addressables package is installed, but no AddressableAssetsData settings asset was found; no groups were changed."
                : "not applicable: com.unity.addressables is not installed.";

            WriteReport(report);
            return report;
        }

        private static void EnsureCampaignFolders()
        {
            var folders = new[]
            {
                StageContentPaths.CampaignRoot,
                StageContentPaths.CampaignCatalogRoot,
                StageContentPaths.CampaignSharedRoot,
                StageContentPaths.SharedGameplayRoot,
                StageContentPaths.SharedEnemyAiRoot + "/Profiles",
                StageContentPaths.SharedEnemyAiRoot + "/Core",
                StageContentPaths.SharedEnemyAiRoot + "/Brain",
                StageContentPaths.SharedEnemyAiRoot + "/Capabilities",
                StageContentPaths.SharedEnemyAiRoot + "/Catalogs",
                StageContentPaths.SharedBoxGameplayRoot + "/AuthoringPresets",
                StageContentPaths.SharedBoxGameplayRoot + "/PresentationProfiles",
                StageContentPaths.SharedBoxGameplayRoot + "/Catalogs",
                StageContentPaths.SharedConditionsRoot,
                StageContentPaths.SharedGameplayRoot + "/ObjectiveTemplates",
                StageContentPaths.SharedGameplayRoot + "/StageRules",
                StageContentPaths.SharedGameplayRoot + "/SpawnPresets",
                StageContentPaths.SharedPresentationRoot,
                StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs",
                StageContentPaths.SharedEnemyPresentationRoot + "/Profiles",
                StageContentPaths.SharedEnemyPresentationRoot + "/Catalogs",
                StageContentPaths.SharedStaticPresentationRoot + "/Prefabs",
                StageContentPaths.SharedStaticPresentationRoot + "/Profiles",
                StageContentPaths.SharedStaticPresentationRoot + "/Catalogs",
                StageContentPaths.SharedBoxPresentationRoot + "/Prefabs",
                StageContentPaths.SharedBoxPresentationRoot + "/Profiles",
                StageContentPaths.SharedBoxPresentationRoot + "/Catalogs",
                StageContentPaths.SharedBoardPresentationRoot + "/Prefabs",
                StageContentPaths.SharedBoardPresentationRoot + "/Materials",
                StageContentPaths.SharedBoardPresentationRoot + "/Profiles",
                StageContentPaths.SharedBoardPresentationRoot + "/Catalogs",
                StageContentPaths.SharedPresentationRoot + "/Background/Prefabs",
                StageContentPaths.SharedPresentationRoot + "/Background/Sprites",
                StageContentPaths.SharedPresentationRoot + "/Background/Profiles",
                StageContentPaths.SharedTopologyPresentationRoot + "/BridgePrefabs",
                StageContentPaths.SharedTopologyPresentationRoot + "/PostFxProfiles",
                StageContentPaths.SharedTopologyPresentationRoot + "/CameraProfiles",
                StageContentPaths.SharedVfxPresentationRoot,
                StageContentPaths.SharedPresentationRoot + "/UI/Icons",
                StageContentPaths.SharedPresentationRoot + "/UI/StageResult",
                StageContentPaths.SharedPresentationRoot + "/UI/ObjectiveIcons",
                StageContentPaths.SharedAudioRoot + "/Bgm",
                StageContentPaths.SharedAudioRoot + "/Sfx",
                StageContentPaths.SharedAudioRoot + "/References",
                StageContentPaths.SharedAudioRoot + "/Profiles",
                StageContentPaths.CampaignLevel01Root,
                StageContentPaths.CampaignLevel01StagesRoot,
            };

            for (var i = 0; i < folders.Length; i++)
            {
                EnsureFolder(folders[i]);
            }
        }

        private static void MoveAndRenameCatalogAssets(StageCampaignMigrationReport report)
        {
            MoveAssetIfPresent(
                "Assets/_Features/Stages/Content/StageCatalog.asset",
                StageContentPaths.StageCatalogAssetPath,
                report);
            MoveAssetIfPresent(
                "Assets/_Features/Stages/Content/StageCatalogProvider.asset",
                StageContentPaths.StageCatalogProviderAssetPath,
                report);
            MoveAssetIfPresent(
                "Assets/_Features/Stages/Content/StageIdAliasTable.asset",
                StageContentPaths.StageIdAliasTableAssetPath,
                report);
            MoveAssetIfPresent(
                "Assets/_Features/Stages/Content/CampaignStageSequence.asset",
                StageContentPaths.CampaignStageSequenceAssetPath,
                report);

            RenameAssetIfPresent(StageContentPaths.StageCatalogAssetPath, "CampaignMain_StageCatalog", report);
            RenameAssetIfPresent(StageContentPaths.StageCatalogProviderAssetPath, "CampaignMain_StageCatalogProvider", report);
            RenameAssetIfPresent(StageContentPaths.StageIdAliasTableAssetPath, "CampaignMain_StageIdAliasTable", report);
            RenameAssetIfPresent(StageContentPaths.CampaignStageSequenceAssetPath, "CampaignMain_StageSequence", report);

            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            var aliasTable = AssetDatabase.LoadAssetAtPath<StageIdAliasTable>(StageContentPaths.StageIdAliasTableAssetPath);
            if (catalog != null)
            {
                catalog.AssignStageIdAliasTable(aliasTable);
                EditorUtility.SetDirty(catalog);
            }

            if (provider != null)
            {
                provider.AssignCatalog(catalog);
                EditorUtility.SetDirty(provider);
            }
        }

        private static void CreateMetadataAssets(StageCampaignMigrationReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<CampaignContentMetadata>(StageContentPaths.CampaignMainAssetPath) == null)
            {
                DeleteAssetIfWrongType<CampaignContentMetadata>(StageContentPaths.CampaignMainAssetPath);
                var campaign = ScriptableObject.CreateInstance<CampaignContentMetadata>();
                campaign.name = "CampaignMain";
                campaign.Set("campaign-main", "Campaign Main");
                AssetDatabase.CreateAsset(campaign, StageContentPaths.CampaignMainAssetPath);
                report.CreatedMetadataAssets.Add(StageContentPaths.CampaignMainAssetPath);
            }

            if (AssetDatabase.LoadAssetAtPath<CampaignLevelMetadata>(StageContentPaths.Level01AssetPath) == null)
            {
                DeleteAssetIfWrongType<CampaignLevelMetadata>(StageContentPaths.Level01AssetPath);
                var level = ScriptableObject.CreateInstance<CampaignLevelMetadata>();
                level.name = "Level01";
                level.Set("level-01", "Level 01");
                AssetDatabase.CreateAsset(level, StageContentPaths.Level01AssetPath);
                report.CreatedMetadataAssets.Add(StageContentPaths.Level01AssetPath);
            }
        }

        private static void MoveStageContentFolders(StageCampaignMigrationReport report)
        {
            var contentRoot = ToAbsolutePath(StageContentPaths.LegacyContentRoot);
            if (!Directory.Exists(contentRoot))
            {
                return;
            }

            var stageFolders = Directory.GetDirectories(contentRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(NormalizeAssetPath)
                .Where(path =>
                    !string.Equals(Path.GetFileName(path), "Campaigns", StringComparison.Ordinal) &&
                    !string.Equals(Path.GetFileName(path), "CampaignS", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < stageFolders.Length; i++)
            {
                var source = stageFolders[i];
                var stageId = Path.GetFileName(source);
                var destination = $"{StageContentPaths.CampaignLevel01StagesRoot}/{stageId}";
                MoveAssetIfPresent(source, destination, report);
                MoveStageConditions(destination, stageId, report);
            }
        }

        private static void MoveStageConditions(string stageFolder, string stageId, StageCampaignMigrationReport report)
        {
            var conditionsFolder = $"{stageFolder}/Conditions";
            if (!AssetDatabase.IsValidFolder(conditionsFolder))
            {
                return;
            }

            var conditionGuids = AssetDatabase.FindAssets("t:StageConditionAsset", new[] { conditionsFolder });
            for (var i = 0; i < conditionGuids.Length; i++)
            {
                var source = AssetDatabase.GUIDToAssetPath(conditionGuids[i]);
                var fileName = Path.GetFileName(source);
                var targetName = $"CampaignMain_{SanitizeName(stageId)}_{fileName}";
                var destination = MakeUniqueAssetPath($"{StageContentPaths.SharedConditionsRoot}/{targetName}");
                MoveAssetIfPresent(source, destination, report);
            }

            DeleteFolderIfEmpty(conditionsFolder, report);
        }

        private static void MoveCampaignStageConditionAssets(StageCampaignMigrationReport report)
        {
            var stagesRoot = ToAbsolutePath(StageContentPaths.CampaignLevel01StagesRoot);
            if (!Directory.Exists(stagesRoot))
            {
                return;
            }

            var stageFolders = Directory.GetDirectories(stagesRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(NormalizeAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            for (var i = 0; i < stageFolders.Length; i++)
            {
                MoveStageConditions(stageFolders[i], Path.GetFileName(stageFolders[i]), report);
            }
        }

        private static void MoveSupportAssets(StageCampaignMigrationReport report)
        {
            for (var i = 0; i < SupportRoots.Length; i++)
            {
                var root = SupportRoots[i];
                if (!AssetDatabase.IsValidFolder(root))
                {
                    continue;
                }

                var guids = AssetDatabase.FindAssets(string.Empty, new[] { root });
                var paths = guids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !AssetDatabase.IsValidFolder(path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                for (var pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                {
                    var source = paths[pathIndex];
                    var destination = ResolveSupportDestination(source);
                    MoveAssetIfPresent(source, MakeUniqueAssetPath(destination), report);
                }
            }
        }

        private static string ResolveSupportDestination(string source)
        {
            var normalized = source.Replace('\\', '/');
            var fileName = Path.GetFileName(normalized);
            var parentName = Path.GetFileName(Path.GetDirectoryName(normalized) ?? string.Empty);

            if (normalized.Contains("/Enemy/Catalogs/", StringComparison.Ordinal))
            {
                var root = fileName.StartsWith("EnemyPresentation", StringComparison.Ordinal)
                    ? StageContentPaths.SharedEnemyPresentationRoot + "/Catalogs"
                    : StageContentPaths.SharedEnemyAiRoot + "/Catalogs";
                return $"{root}/{fileName}";
            }

            if (normalized.Contains("/Enemy/Prefabs/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedEnemyPresentationRoot}/Prefabs/{fileName}";
            }

            if (normalized.Contains("/Enemy/VFX/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedVfxPresentationRoot}/{parentName}/{fileName}";
            }

            if (normalized.Contains("/Enemy/Profiles/", StringComparison.Ordinal))
            {
                var root = ResolveEnemyProfileRoot(fileName);
                return $"{root}/{parentName}/{fileName}";
            }

            if (normalized.Contains("/Static/Catalogs/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedStaticPresentationRoot}/Catalogs/{fileName}";
            }

            if (normalized.Contains("/Static/Prefabs/", StringComparison.Ordinal))
            {
                var root = IsBoxAssetName(fileName)
                    ? StageContentPaths.SharedBoxPresentationRoot + "/Prefabs"
                    : StageContentPaths.SharedStaticPresentationRoot + "/Prefabs";
                return $"{root}/{fileName}";
            }

            if (normalized.Contains("/Static/Materials/", StringComparison.Ordinal))
            {
                var root = IsBoxAssetName(fileName)
                    ? StageContentPaths.SharedBoxPresentationRoot + "/Profiles"
                    : StageContentPaths.SharedStaticPresentationRoot + "/Profiles";
                return $"{root}/{fileName}";
            }

            if (normalized.Contains("/Board/Catalogs/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedBoardPresentationRoot}/Catalogs/{fileName}";
            }

            if (normalized.Contains("/Board/Prefabs/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedBoardPresentationRoot}/Prefabs/{fileName}";
            }

            if (normalized.Contains("/Board/Materials/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedBoardPresentationRoot}/Materials/{fileName}";
            }

            if (normalized.Contains("/TileFeature/Catalogs/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedBoardPresentationRoot}/Catalogs/{fileName}";
            }

            if (normalized.Contains("/TileFeature/Prefabs/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedBoardPresentationRoot}/Prefabs/{fileName}";
            }

            if (normalized.Contains("/TileFeature/Materials/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedBoardPresentationRoot}/Materials/{fileName}";
            }

            if (normalized.Contains("/Camera/Presets/", StringComparison.Ordinal))
            {
                return $"{StageContentPaths.SharedTopologyPresentationRoot}/CameraProfiles/{fileName}";
            }

            return $"{StageContentPaths.CampaignSharedRoot}/Unclassified/{fileName}";
        }

        private static void MoveExternalPresentationCatalogDependencies(StageCampaignMigrationReport report)
        {
            var catalogGuids = AssetDatabase.FindAssets("t:Object", new[] { StageContentPaths.SharedPresentationRoot });
            var dependenciesToMove = new SortedSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < catalogGuids.Length; i++)
            {
                var catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
                var catalog = AssetDatabase.LoadMainAssetAtPath(catalogPath);
                if (catalog == null || !catalog.GetType().Name.Contains("PresentationCatalog", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var dependency in AssetDatabase.GetDependencies(catalogPath, recursive: false))
                {
                    if (!IsMovablePresentationDependency(dependency))
                    {
                        continue;
                    }

                    dependenciesToMove.Add(dependency);
                }
            }

            foreach (var dependency in dependenciesToMove)
            {
                MoveAssetIfPresent(
                    dependency,
                    MakeUniqueAssetPath(ResolveExternalPresentationDependencyDestination(dependency)),
                    report);
            }
        }

        private static bool IsMovablePresentationDependency(string dependency)
        {
            if (string.IsNullOrWhiteSpace(dependency) ||
                IsUnder(dependency, StageContentPaths.SharedPresentationRoot) ||
                dependency.EndsWith(".cs", StringComparison.Ordinal) ||
                dependency.EndsWith(".asmdef", StringComparison.Ordinal))
            {
                return false;
            }

            return dependency.EndsWith(".prefab", StringComparison.Ordinal) ||
                   dependency.EndsWith(".asset", StringComparison.Ordinal) ||
                   dependency.EndsWith(".mat", StringComparison.Ordinal);
        }

        private static string ResolveExternalPresentationDependencyDestination(string dependency)
        {
            var fileName = Path.GetFileName(dependency);
            var extension = Path.GetExtension(dependency);
            if (dependency.Contains("EnemyView_", StringComparison.Ordinal) ||
                dependency.Contains("/Enemy", StringComparison.Ordinal))
            {
                var root = string.Equals(extension, ".prefab", StringComparison.Ordinal)
                    ? StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs"
                    : StageContentPaths.SharedEnemyPresentationRoot + "/Profiles";
                return $"{root}/{fileName}";
            }

            if (IsBoxAssetName(fileName))
            {
                var root = string.Equals(extension, ".prefab", StringComparison.Ordinal)
                    ? StageContentPaths.SharedBoxPresentationRoot + "/Prefabs"
                    : StageContentPaths.SharedBoxPresentationRoot + "/Profiles";
                return $"{root}/{fileName}";
            }

            var defaultRoot = string.Equals(extension, ".prefab", StringComparison.Ordinal)
                ? StageContentPaths.SharedStaticPresentationRoot + "/Prefabs"
                : StageContentPaths.SharedStaticPresentationRoot + "/Profiles";
            return $"{defaultRoot}/{fileName}";
        }

        private static string ResolveEnemyProfileRoot(string fileName)
        {
            if (fileName.StartsWith("EnemyAi_", StringComparison.Ordinal) ||
                fileName.StartsWith("EnemyUnitArchetype_", StringComparison.Ordinal))
            {
                return StageContentPaths.SharedEnemyAiRoot + "/Profiles";
            }

            if (fileName.StartsWith("EnemyCore_", StringComparison.Ordinal))
            {
                return StageContentPaths.SharedEnemyAiRoot + "/Core";
            }

            if (fileName.StartsWith("EnemyCapability_", StringComparison.Ordinal))
            {
                return StageContentPaths.SharedEnemyAiRoot + "/Capabilities";
            }

            if (fileName.StartsWith("EnemyPresentationArchetype_", StringComparison.Ordinal))
            {
                return StageContentPaths.SharedEnemyPresentationRoot + "/Profiles";
            }

            return StageContentPaths.SharedEnemyAiRoot + "/Brain";
        }

        private static bool IsBoxAssetName(string fileName)
        {
            return fileName.Contains("Box", StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteLegacyFolders(StageCampaignMigrationReport report)
        {
            for (var i = 0; i < SupportRoots.Length; i++)
            {
                DeleteFolderIfEmpty(SupportRoots[i], report);
            }

            for (var i = 0; i < LegacyCatalogAssetPaths.Length; i++)
            {
                var path = LegacyCatalogAssetPaths[i];
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    report.BrokenReferences.Add($"Legacy catalog asset remains at {path}");
                }
            }
        }

        private static void DeleteEmptyStageSubfolders(StageCampaignMigrationReport report)
        {
            var stagesRoot = ToAbsolutePath(StageContentPaths.CampaignLevel01StagesRoot);
            if (!Directory.Exists(stagesRoot))
            {
                return;
            }

            var folders = Directory.GetDirectories(stagesRoot, "*", SearchOption.AllDirectories)
                .Select(NormalizeAssetPath)
                .OrderByDescending(path => path.Length)
                .ToArray();
            for (var i = 0; i < folders.Length; i++)
            {
                DeleteFolderIfEmpty(folders[i], report);
            }
        }

        private static void SyncCampaignCatalogEntries(StageCampaignMigrationReport report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            if (catalog == null || !AssetDatabase.IsValidFolder(StageContentPaths.CampaignLevel01StagesRoot))
            {
                return;
            }

            var currentEntries = catalog.Entries
                .Where(entry => entry != null && IsUnder(AssetDatabase.GetAssetPath(entry), StageContentPaths.CampaignLevel01StagesRoot))
                .ToList();
            var currentGuids = new HashSet<string>(
                currentEntries
                    .Select(entry => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry)))
                    .Where(guid => !string.IsNullOrWhiteSpace(guid)),
                StringComparer.Ordinal);
            var discoveredEntries = AssetDatabase
                .FindAssets("t:StageContentEntry", new[] { StageContentPaths.CampaignLevel01StagesRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new
                {
                    Path = path,
                    Entry = AssetDatabase.LoadAssetAtPath<StageContentEntry>(path),
                    Guid = AssetDatabase.AssetPathToGUID(path),
                })
                .Where(candidate => candidate.Entry != null && !string.IsNullOrWhiteSpace(candidate.Guid))
                .ToArray();

            var changed = currentEntries.Count != catalog.Entries.Length;
            for (var i = 0; i < discoveredEntries.Length; i++)
            {
                var candidate = discoveredEntries[i];
                if (currentGuids.Contains(candidate.Guid))
                {
                    continue;
                }

                currentEntries.Add(candidate.Entry);
                currentGuids.Add(candidate.Guid);
                changed = true;
                report.ReferenceRepairs.Add($"Added StageCatalog entry: {candidate.Entry.StageId.Value} ({candidate.Path})");
            }

            if (!changed)
            {
                return;
            }

            catalog.SetEntries(currentEntries.ToArray());
            EditorUtility.SetDirty(catalog);
        }

        private static void SyncDirectPlayCatalog(StageCampaignMigrationReport report)
        {
            var directPlayCatalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (directPlayCatalog == null)
            {
                return;
            }

            var entries = new[]
            {
                CreateDirectPlayEntry("combined-gameplay-showcase"),
                CreateDirectPlayEntry("tutorial-scene"),
                CreateDirectPlayEntry("stage-0-1"),
                CreateDirectPlayEntry("stage-1-1"),
            };

            if (string.Equals(
                    directPlayCatalog.CanonicalShellScenePath,
                    "Assets/Scenes/UIAudioScene.unity",
                    StringComparison.Ordinal) &&
                DirectPlayEntriesEqual(directPlayCatalog.SupportedStages, entries))
            {
                return;
            }

            directPlayCatalog.Configure("Assets/Scenes/UIAudioScene.unity", entries);
            EditorUtility.SetDirty(directPlayCatalog);
            report.ReferenceRepairs.Add(
                "Updated StageEditorDirectPlayCatalog to use UIAudioScene as the canonical shell for supported stage ids.");
        }

        private static StageEditorDirectPlayStageEntry CreateDirectPlayEntry(string stageId)
        {
            return new StageEditorDirectPlayStageEntry
            {
                StageId = StageId.CreateOrThrow(stageId),
            };
        }

        private static bool DirectPlayEntriesEqual(
            IReadOnlyList<StageEditorDirectPlayStageEntry> current,
            StageEditorDirectPlayStageEntry[] expected)
        {
            if (current == null || current.Count != expected.Length)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (!current[i].StageId.Equals(expected[i].StageId))
                {
                    return false;
                }
            }

            return true;
        }

        private static void MoveAssetIfPresent(string source, string destination, StageCampaignMigrationReport report)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(destination) ||
                AssetDatabase.LoadMainAssetAtPath(source) == null && !AssetDatabase.IsValidFolder(source))
            {
                return;
            }

            EnsureFolder(Path.GetDirectoryName(destination)?.Replace('\\', '/'));
            var assetCount = AssetDatabase.IsValidFolder(source)
                ? AssetDatabase.FindAssets(string.Empty, new[] { source }).Length
                : 1;
            if (string.Equals(source, destination, StringComparison.Ordinal))
            {
                return;
            }

            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Failed to move '{source}' to '{destination}': {error}");
            }

            report.MovedAssetCount += assetCount;
            report.MovedAssets.Add($"{source} -> {destination}");
        }

        private static void RenameAssetIfPresent(string assetPath, string newName, StageCampaignMigrationReport report)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null || string.Equals(asset.name, newName, StringComparison.Ordinal))
            {
                return;
            }

            var error = AssetDatabase.RenameAsset(assetPath, newName);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Failed to rename '{assetPath}' to '{newName}': {error}");
            }

            report.RenamedAssets.Add($"{assetPath} -> {newName}");
        }

        private static void DeleteFolderIfEmpty(string folderPath, StageCampaignMigrationReport report)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var remaining = AssetDatabase.FindAssets(string.Empty, new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .ToArray();
            if (remaining.Length > 0)
            {
                return;
            }

            if (AssetDatabase.DeleteAsset(folderPath))
            {
                report.DeletedLegacyFolders.Add(folderPath);
            }
        }

        private static string MakeUniqueAssetPath(string path)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var index = 2;
            while (true)
            {
                var candidate = $"{directory}/{fileName}_{index}{extension}";
                if (AssetDatabase.LoadMainAssetAtPath(candidate) == null && !AssetDatabase.IsValidFolder(candidate))
                {
                    return candidate;
                }

                index++;
            }
        }

        private static string SanitizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Unnamed"
                : string.Concat(value.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrWhiteSpace(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            Directory.CreateDirectory(ToAbsolutePath(assetFolder));
            AssetDatabase.ImportAsset(assetFolder, ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(assetFolder))
            {
                throw new InvalidOperationException($"Failed to create folder '{assetFolder}'.");
            }
        }

        private static void DeleteAssetIfWrongType<T>(string assetPath)
            where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null && existing is not T)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static bool IsAddressablesInstalled()
        {
            var manifestPath = "Packages/manifest.json";
            return File.Exists(manifestPath) &&
                   File.ReadAllText(manifestPath).Contains("com.unity.addressables", StringComparison.Ordinal);
        }

        private static int CountAssetsUnder(string assetRoot)
        {
            return AssetDatabase.IsValidFolder(assetRoot)
                ? AssetDatabase.FindAssets(string.Empty, new[] { assetRoot }).Length
                : 0;
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

        private static bool IsUnder(string path, string root)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   (string.Equals(path, root, StringComparison.Ordinal) ||
                    path.StartsWith(root + "/", StringComparison.Ordinal));
        }

        private static string GetReportPath()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot, ReportRelativePath).Replace('\\', '/');
        }

        private static void WriteReport(StageCampaignMigrationReport report)
        {
            var reportPath = GetReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? "Temp");
            using var writer = new StreamWriter(reportPath, append: false);
            writer.WriteLine("# Campaign Main Stage Content Migration Report");
            writer.WriteLine();
            writer.WriteLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
            writer.WriteLine();
            writer.WriteLine($"MovedAssetCount: {report.MovedAssetCount}");
            writer.WriteLine($"CampaignAssetCount: {report.CampaignAssetCount}");
            writer.WriteLine($"Addressables: {report.AddressablesResult}");
            writer.WriteLine();
            WriteLines(writer, "Deleted Legacy Folders", report.DeletedLegacyFolders);
            WriteLines(writer, "Created Metadata Assets", report.CreatedMetadataAssets);
            WriteLines(writer, "Renamed Assets", report.RenamedAssets);
            WriteLines(writer, "Moved Assets", report.MovedAssets);
            WriteLines(writer, "Reference Repairs", report.ReferenceRepairs);
            WriteLines(writer, "Runtime/Editor Code Kept In Place", new[]
            {
                "Assets/_Features/Stages/Runtime",
                "Assets/_Features/Stages/Editor",
                "Assets/_Features/Gameplay",
                "Assets/_Features/UI",
                "Assets/_Features/Flow/Flow_Audio",
                "Assets/_Shared/Audio",
                "Assets/_Shared/AudioContracts",
            });
            WriteIssues(writer, "Catalog Validation Issues", report.CatalogReport);
            WriteIssues(writer, "Campaign Governance Issues", report.GovernanceReport);
            WriteLines(writer, "Broken References Or Remaining Work", report.BrokenReferences);
        }

        private static void WriteLines(StreamWriter writer, string title, IEnumerable<string> lines)
        {
            writer.WriteLine($"## {title}");
            var wrote = false;
            foreach (var line in lines ?? Array.Empty<string>())
            {
                wrote = true;
                writer.WriteLine($"- {line}");
            }

            if (!wrote)
            {
                writer.WriteLine("None");
            }

            writer.WriteLine();
        }

        private static void WriteIssues(StreamWriter writer, string title, StageValidationReport report)
        {
            writer.WriteLine($"## {title}");
            if (report == null || report.Issues.Count == 0)
            {
                writer.WriteLine("None");
                writer.WriteLine();
                return;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                writer.WriteLine($"- [{issue.Severity}] {issue.Code}: {issue.Message} ({issue.AssetPath})");
            }

            writer.WriteLine();
        }

        private static void LogIssues(string title, StageValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
            {
                return;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                Debug.LogError(
                    $"{title}: [{issue.Severity}] {issue.Code}: {issue.Message} ({issue.AssetPath})");
            }
        }
    }

    public sealed class StageCampaignMigrationReport
    {
        public int MovedAssetCount { get; set; }
        public int CampaignAssetCount { get; set; }
        public List<string> MovedAssets { get; } = new();
        public List<string> RenamedAssets { get; } = new();
        public List<string> DeletedLegacyFolders { get; } = new();
        public List<string> CreatedMetadataAssets { get; } = new();
        public List<string> ReferenceRepairs { get; } = new();
        public List<string> BrokenReferences { get; } = new();
        public string AddressablesResult { get; set; } = string.Empty;
        public StageValidationReport CatalogReport { get; set; } = new();
        public StageValidationReport GovernanceReport { get; set; } = new();
    }

    internal sealed class EditorAssetMetadataProvider : IStageValidationAssetMetadataProvider
    {
        public static readonly EditorAssetMetadataProvider Instance = new();

        public string GetAssetPath(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
        }

        public string GetAssetGuid(UnityEngine.Object asset)
        {
            var path = GetAssetPath(asset);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }
    }
}
