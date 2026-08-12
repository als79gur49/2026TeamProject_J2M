using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageCampaignMainContentSmokeCheck
    {
        private const string UiAudioScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string ReportDirectory = "Temp/StageCampaignMainSmoke";

        public static string ReportPath =>
            Path.Combine(GetProjectRoot(), ReportDirectory, "stage-campaign-main-smoke.md");

        public static void RunFromCommandLine()
        {
            EditorApplication.Exit(Run());
        }

        public static int Run()
        {
            AssetDatabase.Refresh();

            var errors = new List<string>();
            var facts = new List<string>();

            var provider = LoadRequired<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath,
                errors);
            var catalog = LoadRequired<StageCatalog>(StageContentPaths.StageCatalogAssetPath, errors);
            var aliasTable = LoadRequired<StageIdAliasTable>(StageContentPaths.StageIdAliasTableAssetPath, errors);
            var sequence = LoadRequired<CampaignStageSequenceDefinition>(
                CampaignStageSequenceAssetLoader.CanonicalAssetPath,
                errors);

            if (provider != null && catalog != null && provider.Catalog != catalog)
            {
                errors.Add("CampaignMain_StageCatalogProvider does not reference CampaignMain_StageCatalog.");
            }

            if (catalog != null && aliasTable != null && catalog.StageIdAliasTable != aliasTable)
            {
                errors.Add("CampaignMain_StageCatalog does not reference CampaignMain_StageIdAliasTable.");
            }

            if (provider != null && catalog != null)
            {
                ValidateCatalogGraph(provider, catalog, errors, facts);
            }

            if (provider != null && catalog != null && aliasTable != null && sequence != null)
            {
                ValidateCampaignSequenceSmoke(provider, catalog, aliasTable, sequence, errors, facts);
            }

            ValidateDirectPlayCatalog(errors);
            ValidateForbiddenFolders(errors, facts);
            ValidateMissingScripts(errors);
            ValidateGovernance(errors);

            facts.Add($"CampaignRootAssetCount={CountAssets(StageContentPaths.CampaignRoot)}");
            facts.Add($"CampaignSharedAssetCount={CountAssets(StageContentPaths.CampaignSharedRoot)}");
            facts.Add($"CampaignLevel01StageAssetCount={CountAssets(StageContentPaths.CampaignLevel01StagesRoot)}");

            WriteReport(errors, facts);

            if (errors.Count > 0)
            {
                for (var i = 0; i < errors.Count; i++)
                {
                    Debug.LogError($"Campaign-main content smoke check: {errors[i]}");
                }

                Debug.LogError($"Campaign-main content smoke check failed. See {ReportPath}");
                return 1;
            }

            Debug.Log($"Campaign-main content smoke check passed. See {ReportPath}");
            return 0;
        }

        private static void ValidateCatalogGraph(
            ScriptableObjectStageCatalogProvider provider,
            StageCatalog catalog,
            List<string> errors,
            List<string> facts)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = catalog.Entries;
            facts.Add($"CatalogEntryCount={entries.Length}");

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    errors.Add($"CampaignMain_StageCatalog entry {i} is null.");
                    continue;
                }

                var stageId = entry.StageId.Value;
                if (!entry.StageId.IsValid)
                {
                    errors.Add($"StageContentEntry '{entry.name}' has an invalid StageId.");
                    continue;
                }

                if (!seen.Add(stageId))
                {
                    errors.Add($"CampaignMain_StageCatalog has duplicate StageId '{stageId}'.");
                }

                var entryPath = AssetDatabase.GetAssetPath(entry);
                var expectedFolder = $"{StageContentPaths.CampaignLevel01StagesRoot}/{stageId}";
                if (!IsUnder(entryPath, expectedFolder))
                {
                    errors.Add($"StageContentEntry '{stageId}' is outside its campaign stage folder. Path='{entryPath}'.");
                }

                ValidateEntryCompanions(entry, entryPath, errors);
                ValidateStageDefinitionReferences(entry, errors);
                ValidatePresentationReferences(entry, errors);
            }

        }

        private static void ValidateCampaignSequenceSmoke(
            ScriptableObjectStageCatalogProvider provider,
            StageCatalog catalog,
            StageIdAliasTable aliasTable,
            CampaignStageSequenceDefinition sequence,
            List<string> errors,
            List<string> facts)
        {
            var validator = new CampaignStageSequenceValidator();
            var authoritativeReport = validator.ValidateAuthoritativeAsset(
                sequence,
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);
            AddValidationErrors("Authoritative sequence", authoritativeReport, errors);

            for (var i = 0; i < authoritativeReport.Issues.Count; i++)
            {
                var issue = authoritativeReport.Issues[i];
                if (issue.Severity != StageValidationSeverity.Error)
                {
                    facts.Add($"SequenceDiagnostic[{issue.Code}]={issue.Message}");
                }
            }

            if (sequence.Entries.Count == 0)
            {
                errors.Add("CampaignMain_StageSequence has no entries.");
                return;
            }

            CampaignStageSequenceResolver sequenceResolver;
            try
            {
                sequenceResolver = new CampaignStageSequenceResolver(sequence);
            }
            catch (Exception exception)
            {
                errors.Add($"CampaignMain_StageSequence resolver creation failed: {exception.Message}");
                return;
            }

            facts.Add($"CampaignSequenceEntryCount={sequenceResolver.Entries.Count}");
            var catalogResolver = new StageCatalogResolver(provider);
            var sampleStageIds = GetFirstMiddleFinalStageIds(sequenceResolver);
            for (var i = 0; i < sampleStageIds.Count; i++)
            {
                RequireCatalogResolve(catalogResolver, sampleStageIds[i], errors);
                ValidateLaunchContextResolver(provider, sampleStageIds[i], errors);
            }

            if (!sequenceResolver.FirstStageId.IsValid || !sequenceResolver.FinalStageId.IsValid)
            {
                errors.Add("CampaignMain_StageSequence first and final StageIds must both be canonical.");
            }

            try
            {
                for (var i = 0; i < sequenceResolver.Entries.Count; i++)
                {
                    var entry = sequenceResolver.Entries[i];
                    sequenceResolver.GetEntryOrThrow(entry.StageId);
                    var levelGroupId = sequenceResolver.GetLevelGroupId(entry.StageId);
                    if (string.IsNullOrWhiteSpace(levelGroupId) ||
                        !sequenceResolver.TryGetFirstStageInLevelGroup(levelGroupId, out _))
                    {
                        errors.Add($"Campaign sequence StageId '{entry.StageId.Value}' has no resolvable level group.");
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add($"Campaign sequence enumeration failed: {exception.Message}");
            }

            if (sequenceResolver.TryGetNext(sequenceResolver.FinalStageId, out var nextAfterFinal) ||
                nextAfterFinal.IsValid)
            {
                errors.Add("Campaign sequence final StageId must not resolve a next stage.");
            }

            var unknownStageIdValue = sequenceResolver.FirstStageId.Value + "-smoke-unknown";
            while (sequenceResolver.Contains(StageId.CreateOrThrow(unknownStageIdValue)))
            {
                unknownStageIdValue += "-x";
            }

            if (sequenceResolver.Contains(StageId.CreateOrThrow(unknownStageIdValue)))
            {
                errors.Add("Campaign sequence resolver unexpectedly accepted a derived unknown StageId.");
            }
        }

        private static void ValidateEntryCompanions(StageContentEntry entry, string entryPath, List<string> errors)
        {
            RequireCompanion(entry, entry.GameplayDefinition, nameof(entry.GameplayDefinition), errors);
            RequireCompanion(entry, entry.PresentationDefinition, nameof(entry.PresentationDefinition), errors);
            RequireCompanion(entry, entry.AudioDefinition, nameof(entry.AudioDefinition), errors);

            var entryGuid = AssetDatabase.AssetPathToGUID(entryPath);
            ValidateOwner(entry, entry.GameplayDefinition, nameof(entry.GameplayDefinition), entryGuid, errors);
            ValidateOwner(entry, entry.PresentationDefinition, nameof(entry.PresentationDefinition), entryGuid, errors);
        }

        private static void ValidateStageDefinitionReferences(StageContentEntry entry, List<string> errors)
        {
            var definition = entry.GameplayDefinition;
            if (definition == null)
            {
                return;
            }

            var stageId = entry.StageId.Value;
            var archetypeCatalog = definition.EnemyUnitArchetypeCatalog;
            if (archetypeCatalog != null)
            {
                RequirePathUnder(
                    archetypeCatalog,
                    StageContentPaths.SharedEnemyAiRoot,
                    $"StageDefinition '{stageId}' EnemyUnitArchetypeCatalog",
                    errors);
            }

            var enemySpawns = definition.EnemySpawns;
            for (var i = 0; i < enemySpawns.Length; i++)
            {
                var profile = enemySpawns[i].EnemyAiProfile;
                if (profile == null)
                {
                    continue;
                }

                RequirePathUnder(
                    profile,
                    StageContentPaths.SharedEnemyAiRoot,
                    $"StageDefinition '{stageId}' EnemySpawns[{i}].EnemyAiProfile",
                    errors);
            }

            ValidateConditionReferences(definition.Objective, $"StageDefinition '{stageId}'", errors);
            if (entry.AuthoringDefinition != null)
            {
                ValidateConditionReferences(entry.AuthoringDefinition.Objective, $"StageAuthoringDefinition '{stageId}'", errors);
            }
        }

        private static void ValidatePresentationReferences(StageContentEntry entry, List<string> errors)
        {
            var presentation = entry.PresentationDefinition;
            if (presentation == null)
            {
                return;
            }

            var stageId = entry.StageId.Value;
            RequirePathUnderIfPresent(
                presentation.EnemyPresentationCatalog,
                StageContentPaths.SharedPresentationRoot,
                $"StagePresentationDefinition '{stageId}' EnemyPresentationCatalog",
                errors);
            RequirePathUnderIfPresent(
                presentation.EnemyPresentationArchetypeCatalog,
                StageContentPaths.SharedPresentationRoot,
                $"StagePresentationDefinition '{stageId}' EnemyPresentationArchetypeCatalog",
                errors);
            RequirePathUnderIfPresent(
                presentation.StaticEntityPresentationCatalog,
                StageContentPaths.SharedPresentationRoot,
                $"StagePresentationDefinition '{stageId}' StaticEntityPresentationCatalog",
                errors);
            RequirePathUnderIfPresent(
                presentation.BoardTilePresentationCatalog,
                StageContentPaths.SharedPresentationRoot,
                $"StagePresentationDefinition '{stageId}' BoardTilePresentationCatalog",
                errors);
            RequirePathUnderIfPresent(
                presentation.TileFeaturePresentationCatalog,
                StageContentPaths.SharedPresentationRoot,
                $"StagePresentationDefinition '{stageId}' TileFeaturePresentationCatalog",
                errors);
            RequirePathUnderIfPresent(
                presentation.BackgroundPrefab,
                StageContentPaths.SharedPresentationRoot,
                $"StagePresentationDefinition '{stageId}' BackgroundPrefab",
                errors);

            ValidatePresentationCatalogDependencies(presentation.EnemyPresentationCatalog, errors);
            ValidatePresentationCatalogDependencies(presentation.EnemyPresentationArchetypeCatalog, errors);
            ValidatePresentationCatalogDependencies(presentation.StaticEntityPresentationCatalog, errors);
            ValidatePresentationCatalogDependencies(presentation.BoardTilePresentationCatalog, errors);
            ValidatePresentationCatalogDependencies(presentation.TileFeaturePresentationCatalog, errors);
        }

        private static void ValidatePresentationCatalogDependencies(UnityEngine.Object catalog, List<string> errors)
        {
            if (catalog == null)
            {
                return;
            }

            var catalogPath = AssetDatabase.GetAssetPath(catalog);
            foreach (var dependency in AssetDatabase.GetDependencies(catalogPath, recursive: false))
            {
                if (!IsContentDependency(dependency))
                {
                    continue;
                }

                if (!IsUnder(dependency, StageContentPaths.SharedPresentationRoot))
                {
                    errors.Add(
                        $"Presentation catalog dependency must remain under Campaign _Shared/Presentation. Catalog='{catalogPath}' Dependency='{dependency}'.");
                }
            }
        }

        private static void ValidateConditionReferences(
            StageObjectiveAuthoring objective,
            string ownerDescription,
            List<string> errors)
        {
            var entries = objective.GetConditionEntriesOrEmpty();
            for (var i = 0; i < entries.Length; i++)
            {
                var condition = entries[i].Condition;
                if (condition == null)
                {
                    continue;
                }

                RequirePathUnder(
                    condition,
                    StageContentPaths.SharedConditionsRoot,
                    $"{ownerDescription} Objective.ConditionEntries[{i}].Condition",
                    errors);
            }
        }

        private static void ValidateLaunchContextResolver(
            ScriptableObjectStageCatalogProvider provider,
            StageId stageId,
            List<string> errors)
        {
            try
            {
                StageLaunchContextStore.Clear();
                StageLaunchContextStore.SetCurrent(stageId);
                var request = StageLoadRequest.CreateLaunchContextOnly(provider, $"Smoke:{stageId.Value}");
                var resolved = new StageRuntimeContentResolver().Resolve(request);
                if (!resolved.UsedLaunchContext ||
                    !resolved.Entry.StageId.Equals(stageId))
                {
                    errors.Add($"StageRuntimeContentResolver did not resolve launch-context StageId '{stageId.Value}'.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"StageRuntimeContentResolver failed for StageId '{stageId.Value}': {exception.Message}");
            }
            finally
            {
                StageLaunchContextStore.Clear();
            }
        }

        private static void ValidateDirectPlayCatalog(List<string> errors)
        {
            var directPlayCatalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (directPlayCatalog == null)
            {
                errors.Add($"Missing direct-play catalog at '{StageEditorDirectPlayCatalog.DefaultAssetPath}'.");
                return;
            }

            if (!directPlayCatalog.IsCanonicalShellScenePath(UiAudioScenePath))
            {
                errors.Add($"Direct-play catalog canonical shell must be '{UiAudioScenePath}'.");
            }

        }

        private static void ValidateForbiddenFolders(List<string> errors, List<string> facts)
        {
            RequireNoAssets("Assets/_Features/Stages/Stage_CombinedGameplayShowcase", errors, facts);
            RequireNoAssets("Assets/_Features/Stages/Stage_TutorialScene", errors, facts);
            RequireNoDirectories(StageContentPaths.CampaignLevelsRoot, "_Shared", errors);
            RequireNoDirectories(StageContentPaths.CampaignLevel01StagesRoot, "_Shared", errors);
            RequireNoDirectories(StageContentPaths.CampaignLevel01StagesRoot, "_Overrides", errors);
        }

        private static void ValidateMissingScripts(List<string> errors)
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { StageContentPaths.CampaignRoot });
            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add($"Prefab failed to load after migration. Path='{path}'.");
                    continue;
                }

                var missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab);
                if (missingScriptCount > 0)
                {
                    errors.Add($"Prefab has missing scripts after migration. Path='{path}' MissingScriptCount={missingScriptCount}.");
                }
            }
        }

        private static void ValidateGovernance(List<string> errors)
        {
            var report = new StageCampaignContentGovernanceValidator().Validate(StageValidationTiming.TestOrCi);
            if (!report.HasErrors)
            {
                return;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                if (issue.Severity == StageValidationSeverity.Error)
                {
                    errors.Add($"Governance {issue.Code}: {issue.Message}");
                }
            }
        }

        private static T LoadRequired<T>(string path, List<string> errors)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                errors.Add($"Required asset is missing or has the wrong type. Path='{path}' Type='{typeof(T).Name}'.");
            }

            return asset;
        }

        private static void RequireCompanion(
            StageContentEntry entry,
            UnityEngine.Object companion,
            string fieldName,
            List<string> errors)
        {
            if (companion == null)
            {
                errors.Add($"StageContentEntry '{entry.StageId.Value}' has null {fieldName}.");
            }
        }

        private static void ValidateOwner(
            StageContentEntry entry,
            UnityEngine.Object asset,
            string fieldName,
            string entryGuid,
            List<string> errors)
        {
            if (asset == null)
            {
                return;
            }

            if (asset is not StageCompanionDefinitionBase companion)
            {
                return;
            }

            if (companion.OwnerEntry != entry)
            {
                errors.Add($"StageContentEntry '{entry.StageId.Value}' {fieldName} OwnerEntry does not point back to the entry.");
            }

            if (!string.Equals(companion.OwnerEntryGuid, entryGuid, StringComparison.Ordinal))
            {
                errors.Add(
                    $"StageContentEntry '{entry.StageId.Value}' {fieldName} OwnerEntryGuid mismatch. Expected='{entryGuid}' Actual='{companion.OwnerEntryGuid}'.");
            }
        }

        private static void RequireCatalogResolve(
            StageCatalogResolver resolver,
            StageId stageId,
            List<string> errors)
        {
            if (!resolver.TryResolve(stageId, out var entry) || entry == null)
            {
                errors.Add($"CampaignMain_StageCatalog cannot resolve StageId '{stageId.Value}'.");
            }
        }

        private static void RequirePathUnderIfPresent(
            UnityEngine.Object asset,
            string requiredRoot,
            string description,
            List<string> errors)
        {
            if (asset == null)
            {
                return;
            }

            RequirePathUnder(asset, requiredRoot, description, errors);
        }

        private static void RequirePathUnder(
            UnityEngine.Object asset,
            string requiredRoot,
            string description,
            List<string> errors)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (!IsUnder(path, requiredRoot))
            {
                errors.Add($"{description} must live under '{requiredRoot}'. Path='{path}'.");
            }
        }

        private static IReadOnlyList<StageId> GetFirstMiddleFinalStageIds(
            CampaignStageSequenceResolver resolver)
        {
            var samples = new List<StageId>(3);
            AddDistinct(samples, resolver.FirstStageId);
            AddDistinct(samples, resolver.Entries[resolver.Entries.Count / 2].StageId);
            AddDistinct(samples, resolver.FinalStageId);
            return samples;
        }

        private static void AddDistinct(ICollection<StageId> values, StageId stageId)
        {
            foreach (var value in values)
            {
                if (value.Equals(stageId))
                {
                    return;
                }
            }

            values.Add(stageId);
        }

        private static void AddValidationErrors(
            string scope,
            StageValidationReport report,
            ICollection<string> errors)
        {
            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                if (issue.Severity == StageValidationSeverity.Error)
                {
                    errors.Add($"{scope} {issue.Code}: {issue.Message}");
                }
            }
        }

        private static void RequireNoAssets(string assetRoot, List<string> errors, List<string> facts)
        {
            var absoluteRoot = ToAbsolutePath(assetRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                facts.Add($"DeletedLegacyFolder={assetRoot}");
                return;
            }

            var files = Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories);
            var assetCount = 0;
            for (var i = 0; i < files.Length; i++)
            {
                var path = NormalizeAssetPath(files[i]);
                if (path.EndsWith(".meta", StringComparison.Ordinal) ||
                    string.Equals(Path.GetFileName(path), "README_Deprecated_DoNotUse.md", StringComparison.Ordinal))
                {
                    continue;
                }

                assetCount++;
                errors.Add($"Legacy folder still contains an asset. Root='{assetRoot}' Path='{path}'.");
            }

            if (assetCount == 0)
            {
                facts.Add($"LegacyFolderHasNoAssets={assetRoot}");
            }
        }

        private static void RequireNoDirectories(string assetRoot, string directoryName, List<string> errors)
        {
            var absoluteRoot = ToAbsolutePath(assetRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return;
            }

            var directories = Directory.GetDirectories(absoluteRoot, directoryName, SearchOption.AllDirectories);
            for (var i = 0; i < directories.Length; i++)
            {
                errors.Add($"Forbidden Phase 1 folder exists: '{NormalizeAssetPath(directories[i])}'.");
            }
        }

        private static int CountAssets(string assetRoot)
        {
            var absoluteRoot = ToAbsolutePath(assetRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return 0;
            }

            var count = 0;
            foreach (var file in Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".meta", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
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

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static void WriteReport(IReadOnlyList<string> errors, IReadOnlyList<string> facts)
        {
            var reportDirectory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            using var writer = new StreamWriter(ReportPath, append: false);
            writer.WriteLine("# Campaign-Main Content Smoke Check");
            writer.WriteLine();
            writer.WriteLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
            writer.WriteLine($"Status: {(errors.Count == 0 ? "Passed" : "Failed")}");
            writer.WriteLine();
            writer.WriteLine("## Facts");
            for (var i = 0; i < facts.Count; i++)
            {
                writer.WriteLine($"- {facts[i]}");
            }

            writer.WriteLine();
            writer.WriteLine("## Errors");
            if (errors.Count == 0)
            {
                writer.WriteLine("None");
                return;
            }

            for (var i = 0; i < errors.Count; i++)
            {
                writer.WriteLine($"- {errors[i]}");
            }
        }
    }
}
