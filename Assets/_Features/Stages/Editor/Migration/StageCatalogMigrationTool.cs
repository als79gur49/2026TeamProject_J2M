using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageCatalogMigrationTool
    {
        private const string StagesRoot = "Assets/_Features/Stages";
        private const string CanonicalContentRoot = "Assets/_Features/Stages/Content";
        private const string StageCatalogAssetPath = "Assets/_Features/Stages/Content/StageCatalog.asset";
        private const string StageCatalogProviderAssetPath = "Assets/_Features/Stages/Content/StageCatalogProvider.asset";
        private const string StageIdAliasTableAssetPath = "Assets/_Features/Stages/Content/StageIdAliasTable.asset";
        private const string StageAliasGovernanceLedgerAssetPath =
            StageAliasGovernanceUpdater.DefaultAliasGovernanceLedgerAssetPath;
        private const string DefaultPlanAssetPath =
            "Assets/_Features/Stages/Editor/Migration/StageCatalogMigrationPlan.asset";
        private const string ReportRoot = "Temp/StageCatalogMigration";
        private const string ToolVersion = "p1-canonicalization-v1";

        [MenuItem("Tools/Stages/Migration/Dry Run Canonical Stage Catalog")]
        private static void DryRunMenu()
        {
            var report = Analyze(LoadDefaultPlan());
            WriteReports(report);
            Debug.Log($"Stage catalog dry-run report written to '{Path.Combine(ReportRoot, report.runId)}'.");
        }

        [MenuItem("Tools/Stages/Migration/Apply Canonical Stage Catalog")]
        private static void ApplyMenu()
        {
            var report = Apply(LoadDefaultPlan());
            Debug.Log($"Stage catalog migration apply completed. Report: '{Path.Combine(ReportRoot, report.runId)}'.");
        }

        public static StageCatalogMigrationReport Analyze(StageCatalogMigrationPlan plan = null)
        {
            var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var sceneRefsByGuid = ScanSceneReferences();
            var reportItems = AnalyzeItems(plan, sceneRefsByGuid);
            var dryRunHash = ComputeDryRunHash(reportItems);
            var report = new StageCatalogMigrationReport
            {
                runId = runId,
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                toolVersion = ToolVersion,
                catalogPath = StageCatalogAssetPath,
                providerPath = StageCatalogProviderAssetPath,
                aliasTablePath = StageIdAliasTableAssetPath,
                aliasGovernanceLedgerPath = StageAliasGovernanceLedgerAssetPath,
                dryRunHash = dryRunHash,
                rollbackJournalPath = Path.Combine(ReportRoot, runId, "rollback.json").Replace('\\', '/'),
                items = reportItems,
                summary = BuildSummary(reportItems),
                review = new StageCatalogMigrationReportReview
                {
                    requiresHumanReview = reportItems.Any(item =>
                        string.Equals(item.confidence, StageCatalogMigrationConfidence.Low.ToString(), StringComparison.Ordinal) ||
                        string.Equals(item.disposition, StageCatalogMigrationDisposition.Block.ToString(), StringComparison.Ordinal)),
                    reviewApprovedBy = string.Empty,
                    reviewChecklistVersion = "p1-canonical-stage-migration-v1",
                },
            };

            return report;
        }

        public static StageCatalogMigrationReport Apply(StageCatalogMigrationPlan plan = null)
        {
            var report = Analyze(plan);
            EnsureApplyAllowed(report, plan);

            EnsureFolder(CanonicalContentRoot);
            var catalog = LoadOrCreateCatalog();
            var provider = LoadOrCreateProvider(catalog);
            var aliasTable = LoadOrCreateAliasTable();
            var aliasGovernanceLedger = LoadOrCreateAliasGovernanceLedger();
            var previousCatalogEntryGuids = catalog.Entries
                .Where(entry => entry != null)
                .Select(entry => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry)))
                .ToArray();
            var previousAliasEntries = aliasTable.Entries.ToArray();
            var previousAliasGovernanceEntries = aliasGovernanceLedger.Entries.ToArray();

            var migratedEntries = new List<StageContentEntry>();
            var mergedAliases = new List<StageIdAliasEntry>(previousAliasEntries);
            var mergedAliasGovernanceEntries = new List<StageAliasGovernanceEntry>(previousAliasGovernanceEntries);
            var createdAssets = new List<string>();
            var updatedAssets = new List<string>();

            for (var i = 0; i < report.items.Length; i++)
            {
                var item = report.items[i];
                if (!TryParseDisposition(item.disposition, out var disposition))
                {
                    continue;
                }

                if (disposition == StageCatalogMigrationDisposition.Skip ||
                    disposition == StageCatalogMigrationDisposition.Block)
                {
                    continue;
                }

                if (disposition == StageCatalogMigrationDisposition.AliasOnly)
                {
                    MergeAliases(item, mergedAliases, mergedAliasGovernanceEntries);
                    continue;
                }

                var sourceStageDefinition = AssetDatabase.LoadAssetAtPath<StageDefinition>(item.sourceAssetPath);
                if (sourceStageDefinition == null || !StageId.TryCreate(item.chosenStageId, out var chosenStageId))
                {
                    continue;
                }

                var entry = UpsertCanonicalStageContent(
                    sourceStageDefinition,
                    chosenStageId,
                    item,
                    createdAssets,
                    updatedAssets);
                if (entry != null)
                {
                    migratedEntries.Add(entry);
                }

                MergeAliases(item, mergedAliases, mergedAliasGovernanceEntries);
            }

            catalog.SetEntries(migratedEntries
                .OrderBy(entry => entry.StageId.Value, StringComparer.Ordinal)
                .ToArray());
            StageAliasGovernanceUpdater.Apply(aliasTable, aliasGovernanceLedger, mergedAliases, mergedAliasGovernanceEntries);
            catalog.AssignStageIdAliasTable(aliasTable);
            provider.AssignCatalog(catalog);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(provider);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var validationReport = new StageCatalogValidator().Validate(
                catalog,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    RequireClearEvaluationDefinition = true,
                    RequireRewardDefinition = true,
                    RequireProgressionDefinition = true,
                    Timing = StageValidationTiming.EditorAuthoring,
                    Phase = StageValidationPhase.Phase3_CanonicalContentApply,
                });
            if (validationReport.HasErrors)
            {
                throw new InvalidOperationException("Canonical stage catalog migration apply produced validation errors.");
            }

            for (var i = 0; i < report.items.Length; i++)
            {
                report.items[i].createdAssets = createdAssets.ToArray();
                report.items[i].updatedAssets = updatedAssets.ToArray();
            }

            var rollback = new StageCatalogMigrationRollbackJournal
            {
                createdAssetPaths = createdAssets.ToArray(),
                catalogAssetPath = StageCatalogAssetPath,
                providerAssetPath = StageCatalogProviderAssetPath,
                aliasTableAssetPath = StageIdAliasTableAssetPath,
                aliasGovernanceLedgerAssetPath = StageAliasGovernanceLedgerAssetPath,
                previousCatalogEntryGuids = previousCatalogEntryGuids,
                previousAliasEntries = previousAliasEntries,
                previousAliasGovernanceEntries = previousAliasGovernanceEntries,
            };

            WriteReports(report);
            WriteRollbackJournal(report, rollback);
            return report;
        }

        public static void Rollback(string rollbackJournalPath)
        {
            if (string.IsNullOrWhiteSpace(rollbackJournalPath) || !File.Exists(rollbackJournalPath))
            {
                throw new FileNotFoundException("Missing rollback journal.", rollbackJournalPath);
            }

            var journal = JsonUtility.FromJson<StageCatalogMigrationRollbackJournal>(File.ReadAllText(rollbackJournalPath));
            if (journal == null)
            {
                throw new InvalidOperationException("Rollback journal is invalid.");
            }

            var createdPaths = journal.createdAssetPaths ?? Array.Empty<string>();
            for (var i = 0; i < createdPaths.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(createdPaths[i]) == null)
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(createdPaths[i]);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(journal.catalogAssetPath);
            var aliasTable = AssetDatabase.LoadAssetAtPath<StageIdAliasTable>(journal.aliasTableAssetPath);
            var aliasGovernanceLedger = AssetDatabase.LoadAssetAtPath<StageAliasGovernanceLedger>(journal.aliasGovernanceLedgerAssetPath);
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(journal.providerAssetPath);
            if (catalog != null)
            {
                var restoredEntries = (journal.previousCatalogEntryGuids ?? Array.Empty<string>())
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(AssetDatabase.LoadAssetAtPath<StageContentEntry>)
                    .Where(entry => entry != null)
                    .ToArray();
                catalog.SetEntries(restoredEntries);
                if (aliasTable != null)
                {
                    catalog.AssignStageIdAliasTable(aliasTable);
                }

                EditorUtility.SetDirty(catalog);
            }

            if (aliasTable != null)
            {
                StageAliasGovernanceUpdater.Apply(
                    aliasTable,
                    aliasGovernanceLedger ?? StageAliasGovernanceUpdater.LoadOrCreateLedger(),
                    journal.previousAliasEntries ?? Array.Empty<StageIdAliasEntry>(),
                    journal.previousAliasGovernanceEntries ?? Array.Empty<StageAliasGovernanceEntry>());
            }

            if (provider != null && catalog != null)
            {
                provider.AssignCatalog(catalog);
                EditorUtility.SetDirty(provider);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static StageCatalogMigrationPlan LoadDefaultPlan()
        {
            return AssetDatabase.LoadAssetAtPath<StageCatalogMigrationPlan>(DefaultPlanAssetPath);
        }

        private static StageCatalogMigrationReportItem[] AnalyzeItems(
            StageCatalogMigrationPlan plan,
            IReadOnlyDictionary<string, List<string>> sceneRefsByGuid)
        {
            var assetGuids = AssetDatabase.FindAssets("t:StageDefinition", new[] { StagesRoot });
            var groupedGuids = assetGuids
                .GroupBy(guid => Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid))?.Replace('\\', '/') ?? string.Empty)
                .ToArray();
            var items = new List<StageCatalogMigrationReportItem>();

            for (var groupIndex = 0; groupIndex < groupedGuids.Length; groupIndex++)
            {
                var group = groupedGuids[groupIndex].ToArray();
                var groupAssets = group.Select(guid => new StageDefinitionCandidate(
                        guid,
                        AssetDatabase.GUIDToAssetPath(guid),
                        AssetDatabase.LoadAssetAtPath<StageDefinition>(AssetDatabase.GUIDToAssetPath(guid)),
                        sceneRefsByGuid.TryGetValue(guid, out var refs) ? refs : new List<string>()))
                    .Where(candidate => candidate.StageDefinition != null)
                    .ToArray();
                if (groupAssets.Length == 0)
                {
                    continue;
                }

                var folderPath = groupAssets[0].FolderPath;
                var folderCandidate = TryDeriveCanonicalStageIdFromFolder(folderPath, out var folderStageId)
                    ? folderStageId.Value
                    : string.Empty;
                var exactFolderMatches = groupAssets
                    .Where(candidate => string.Equals(candidate.AssetFileName, candidate.FolderName, StringComparison.Ordinal))
                    .ToArray();
                var sceneReferenced = groupAssets.Where(candidate => candidate.DetectedSceneRefs.Count > 0).ToArray();
                var primary = ResolvePrimaryCandidate(groupAssets, exactFolderMatches, sceneReferenced, out var primaryReason, out var conflicts);

                for (var assetIndex = 0; assetIndex < groupAssets.Length; assetIndex++)
                {
                    items.Add(BuildItem(
                        groupAssets[assetIndex],
                        folderCandidate,
                        primary,
                        primaryReason,
                        conflicts,
                        plan));
                }
            }

            return items
                .OrderBy(item => item.folderPath, StringComparer.Ordinal)
                .ThenBy(item => item.sourceAssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        private static StageCatalogMigrationReportItem BuildItem(
            StageDefinitionCandidate candidate,
            string folderCandidate,
            StageDefinitionCandidate primary,
            string primaryReason,
            IReadOnlyList<string> groupConflicts,
            StageCatalogMigrationPlan plan)
        {
            var candidateStageIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(folderCandidate))
            {
                candidateStageIds.Add(folderCandidate);
            }

            if (StageId.TryCreate(candidate.StageDefinition.name, out var legacyStageId) &&
                !candidateStageIds.Contains(legacyStageId.Value))
            {
                candidateStageIds.Add(legacyStageId.Value);
            }

            var isPrimary = string.Equals(candidate.Guid, primary.Guid, StringComparison.Ordinal);
            var hasNumericSuffixDuplicate = !isPrimary && EndsWithNumericSuffix(candidate.AssetFileName);
            var aliasPlan = new List<string>();
            var conflicts = new List<string>(groupConflicts);
            var confidence = StageCatalogMigrationConfidence.High;
            var disposition = StageCatalogMigrationDisposition.Skip;
            var chosenStageId = folderCandidate;

            if (isPrimary)
            {
                chosenStageId = string.IsNullOrWhiteSpace(folderCandidate) && candidateStageIds.Count > 0
                    ? candidateStageIds[0]
                    : folderCandidate;
                disposition = StageCatalogMigrationDisposition.Migrate;
                if (!string.IsNullOrWhiteSpace(candidate.StageDefinition.name) &&
                    StageId.TryCreate(candidate.StageDefinition.name, out var normalizedLegacyStageId) &&
                    !string.Equals(normalizedLegacyStageId.Value, chosenStageId, StringComparison.Ordinal))
                {
                    aliasPlan.Add(candidate.StageDefinition.name);
                    disposition = StageCatalogMigrationDisposition.MigrateAndAlias;
                    confidence = StageCatalogMigrationConfidence.Medium;
                }

                if (groupConflicts.Count > 0 || string.IsNullOrWhiteSpace(chosenStageId))
                {
                    confidence = StageCatalogMigrationConfidence.Low;
                    disposition = StageCatalogMigrationDisposition.Block;
                }
                else if (candidate.DetectedSceneRefs.Count == 0 && candidateStageIds.Count == 1)
                {
                    confidence = StageCatalogMigrationConfidence.Medium;
                }
            }
            else if (hasNumericSuffixDuplicate)
            {
                disposition = StageCatalogMigrationDisposition.Skip;
                confidence = StageCatalogMigrationConfidence.Medium;
                conflicts.Add("stale-duplicate");
            }
            else
            {
                disposition = StageCatalogMigrationDisposition.Block;
                confidence = StageCatalogMigrationConfidence.Low;
                conflicts.Add("true-ambiguity");
            }

            var overrideApplied = false;
            if (plan != null && plan.TryGetOverride(candidate.Guid, out var overrideEntry))
            {
                overrideApplied = true;
                disposition = overrideEntry.Disposition;
                if (StageId.TryCreate(overrideEntry.CanonicalStageId, out var overriddenStageId))
                {
                    chosenStageId = overriddenStageId.Value;
                }

                aliasPlan = (overrideEntry.AliasSourceIds ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (overrideEntry.ForcePrimary && disposition == StageCatalogMigrationDisposition.Skip)
                {
                    disposition = StageCatalogMigrationDisposition.Migrate;
                }
            }

            return new StageCatalogMigrationReportItem
            {
                sourceAssetGuid = candidate.Guid,
                sourceAssetPath = candidate.AssetPath,
                folderPath = candidate.FolderPath,
                detectedSceneRefs = candidate.DetectedSceneRefs.ToArray(),
                candidateStageIds = candidateStageIds.ToArray(),
                chosenStageId = chosenStageId,
                confidence = confidence.ToString(),
                disposition = disposition.ToString(),
                primaryReason = primaryReason,
                conflicts = conflicts.ToArray(),
                aliasPlan = aliasPlan.ToArray(),
                overrideApplied = overrideApplied,
                createdAssets = Array.Empty<string>(),
                updatedAssets = Array.Empty<string>(),
                validationIssues = Array.Empty<string>(),
            };
        }

        private static StageDefinitionCandidate ResolvePrimaryCandidate(
            StageDefinitionCandidate[] candidates,
            StageDefinitionCandidate[] exactFolderMatches,
            StageDefinitionCandidate[] sceneReferenced,
            out string primaryReason,
            out List<string> conflicts)
        {
            conflicts = new List<string>();
            primaryReason = "folder exact match";

            if (exactFolderMatches.Length == 1)
            {
                var primary = exactFolderMatches[0];
                if (sceneReferenced.Length > 0 &&
                    sceneReferenced.All(candidate => !string.Equals(candidate.Guid, primary.Guid, StringComparison.Ordinal)))
                {
                    conflicts.Add("folder-scene-conflict");
                }

                return primary;
            }

            if (sceneReferenced.Length == 1)
            {
                primaryReason = "scene referenced asset";
                return sceneReferenced[0];
            }

            if (candidates.Length == 1)
            {
                primaryReason = "single asset";
                return candidates[0];
            }

            if (exactFolderMatches.Length > 1)
            {
                conflicts.Add("multiple-folder-exact-matches");
                return exactFolderMatches[0];
            }

            if (sceneReferenced.Length > 1)
            {
                conflicts.Add("multiple-scene-references");
                return sceneReferenced[0];
            }

            conflicts.Add("no-primary-candidate");
            return candidates[0];
        }

        private static IReadOnlyDictionary<string, List<string>> ScanSceneReferences()
        {
            var sceneRefsByGuid = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            var stageGuids = AssetDatabase.FindAssets("t:StageDefinition", new[] { StagesRoot });
            var sceneTexts = scenePaths.ToDictionary(
                path => path,
                path => File.ReadAllText(ToAbsoluteAssetPath(path)));

            for (var guidIndex = 0; guidIndex < stageGuids.Length; guidIndex++)
            {
                var guid = stageGuids[guidIndex];
                for (var sceneIndex = 0; sceneIndex < scenePaths.Length; sceneIndex++)
                {
                    var scenePath = scenePaths[sceneIndex];
                    if (!sceneTexts[scenePath].Contains(guid, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!sceneRefsByGuid.TryGetValue(guid, out var refs))
                    {
                        refs = new List<string>();
                        sceneRefsByGuid[guid] = refs;
                    }

                    refs.Add(scenePath);
                }
            }

            return sceneRefsByGuid;
        }

        private static void EnsureApplyAllowed(StageCatalogMigrationReport report, StageCatalogMigrationPlan plan)
        {
            if (report.summary.lowCount > 0)
            {
                throw new InvalidOperationException("Migration apply is blocked because Low confidence items remain unresolved.");
            }

            if (report.summary.blockerCount > 0)
            {
                throw new InvalidOperationException("Migration apply is blocked because at least one item is still marked Block.");
            }

            if (plan == null)
            {
                return;
            }

            var overrides = plan.Entries;
            for (var i = 0; i < overrides.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(overrides[i].ApprovedBy) ||
                    !string.Equals(overrides[i].DryRunHash, report.dryRunHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Migration plan override for '{overrides[i].SourceAssetGuid}' is missing ApprovedBy or DryRunHash confirmation.");
                }
            }
        }

        private static StageContentEntry UpsertCanonicalStageContent(
            StageDefinition stageDefinition,
            StageId stageId,
            StageCatalogMigrationReportItem item,
            ICollection<string> createdAssets,
            ICollection<string> updatedAssets)
        {
            EnsureFolder(CanonicalContentRoot);
            var stageFolder = $"{CanonicalContentRoot}/{stageId.Value}";
            EnsureFolder(stageFolder);

            var entryPath = $"{stageFolder}/{stageId.Value}_Entry.asset";
            var presentationPath = $"{stageFolder}/{stageId.Value}_Presentation.asset";
            var clearPath = $"{stageFolder}/{stageId.Value}_ClearEvaluation.asset";
            var rewardPath = $"{stageFolder}/{stageId.Value}_Reward.asset";
            var progressionPath = $"{stageFolder}/{stageId.Value}_Progression.asset";

            EnsureLegacyPresentationBridgeIsNotRequired(stageDefinition, item.sourceAssetPath);

            var entry = AssetDatabase.LoadAssetAtPath<StageContentEntry>(entryPath);
            if (entry == null)
            {
                entry = StageContentEntryCreationTool.CreateForStageDefinition(stageDefinition, stageId);
                createdAssets.Add(entryPath);
                createdAssets.Add(presentationPath);
                createdAssets.Add(clearPath);
                createdAssets.Add(rewardPath);
                createdAssets.Add(progressionPath);
                return entry;
            }

            var presentation = LoadOrCreateCompanion<StagePresentationDefinition>(presentationPath, $"{stageId.Value}_Presentation", createdAssets);
            var clearEvaluation = LoadOrCreateCompanion<StageClearEvaluationDefinition>(clearPath, $"{stageId.Value}_ClearEvaluation", createdAssets);
            var reward = LoadOrCreateCompanion<StageRewardDefinition>(rewardPath, $"{stageId.Value}_Reward", createdAssets);
            var progression = LoadOrCreateCompanion<StageProgressionDefinition>(progressionPath, $"{stageId.Value}_Progression", createdAssets);

            entry.AssignStageId(stageId);
            entry.AssignGameplayDefinition(stageDefinition);
            entry.AssignPresentationDefinition(presentation);
            entry.AssignClearEvaluationDefinition(clearEvaluation);
            entry.AssignRewardDefinition(reward);
            entry.AssignProgressionDefinition(progression);
            var entryGuid = AssetDatabase.AssetPathToGUID(entryPath);
            presentation.SetOwnerMetadata(entry, entryGuid);
            clearEvaluation.SetOwnerMetadata(entry, entryGuid);
            reward.SetOwnerMetadata(entry, entryGuid);
            progression.SetOwnerMetadata(entry, entryGuid);

            EditorUtility.SetDirty(entry);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(clearEvaluation);
            EditorUtility.SetDirty(reward);
            EditorUtility.SetDirty(progression);
            updatedAssets.Add(entryPath);
            updatedAssets.Add(presentationPath);
            updatedAssets.Add(clearPath);
            updatedAssets.Add(rewardPath);
            updatedAssets.Add(progressionPath);
            AssetDatabase.SaveAssets();
            return entry;
        }

        private static void EnsureLegacyPresentationBridgeIsNotRequired(
            StageDefinition stageDefinition,
            string sourceAssetPath)
        {
            if (stageDefinition == null)
            {
                throw new ArgumentNullException(nameof(stageDefinition));
            }

            var spawns = stageDefinition.Spawns;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(spawns[i].PresentationId))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"StageDefinition '{sourceAssetPath}' still serializes legacy PresentationId authoring. Canonical migration apply no longer supports bridge-seeded rescue; author a {nameof(StagePresentationDefinition)} explicitly before apply.");
            }
        }

        private static T LoadOrCreateCompanion<T>(string assetPath, string assetName, ICollection<string> createdAssets)
            where T : StageCompanionDefinitionBase
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, assetPath);
            createdAssets.Add(assetPath);
            return asset;
        }

        private static void MergeAliases(
            StageCatalogMigrationReportItem item,
            ICollection<StageIdAliasEntry> aliases,
            ICollection<StageAliasGovernanceEntry> governanceEntries)
        {
            if (!StageId.TryCreate(item.chosenStageId, out var currentStageId))
            {
                return;
            }

            var aliasPlan = item.aliasPlan ?? Array.Empty<string>();
            for (var i = 0; i < aliasPlan.Length; i++)
            {
                var aliasEntry = new StageIdAliasEntry
                {
                    DeprecatedStageId = aliasPlan[i],
                    CurrentStageId = currentStageId,
                };
                aliases.Add(aliasEntry);
                governanceEntries.Add(StageAliasGovernanceUpdater.CreateEntry(
                    aliasEntry,
                    sourceKind: "catalog-migration",
                    sourceAssetGuid: item.sourceAssetGuid,
                    introducedBy: nameof(StageCatalogMigrationTool),
                    reason: "Stage catalog migration rename compatibility bridge."));
            }
        }

        private static StageCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageCatalogAssetPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<StageCatalog>();
            catalog.name = "StageCatalog";
            AssetDatabase.CreateAsset(catalog, StageCatalogAssetPath);
            return catalog;
        }

        private static ScriptableObjectStageCatalogProvider LoadOrCreateProvider(StageCatalog catalog)
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(StageCatalogProviderAssetPath);
            if (provider == null)
            {
                provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
                provider.name = "StageCatalogProvider";
                AssetDatabase.CreateAsset(provider, StageCatalogProviderAssetPath);
            }

            provider.AssignCatalog(catalog);
            return provider;
        }

        private static StageIdAliasTable LoadOrCreateAliasTable()
        {
            var aliasTable = AssetDatabase.LoadAssetAtPath<StageIdAliasTable>(StageIdAliasTableAssetPath);
            if (aliasTable != null)
            {
                return aliasTable;
            }

            aliasTable = ScriptableObject.CreateInstance<StageIdAliasTable>();
            aliasTable.name = "StageIdAliasTable";
            AssetDatabase.CreateAsset(aliasTable, StageIdAliasTableAssetPath);
            return aliasTable;
        }

        private static StageAliasGovernanceLedger LoadOrCreateAliasGovernanceLedger()
        {
            return StageAliasGovernanceUpdater.LoadOrCreateLedger(StageAliasGovernanceLedgerAssetPath);
        }

        private static StageCatalogMigrationReportSummary BuildSummary(IEnumerable<StageCatalogMigrationReportItem> items)
        {
            var summary = new StageCatalogMigrationReportSummary();
            foreach (var item in items)
            {
                if (string.Equals(item.confidence, StageCatalogMigrationConfidence.High.ToString(), StringComparison.Ordinal))
                {
                    summary.highCount++;
                }
                else if (string.Equals(item.confidence, StageCatalogMigrationConfidence.Medium.ToString(), StringComparison.Ordinal))
                {
                    summary.mediumCount++;
                }
                else if (string.Equals(item.confidence, StageCatalogMigrationConfidence.Low.ToString(), StringComparison.Ordinal))
                {
                    summary.lowCount++;
                }

                if (string.Equals(item.disposition, StageCatalogMigrationDisposition.Block.ToString(), StringComparison.Ordinal))
                {
                    summary.blockerCount++;
                }

                if (string.Equals(item.disposition, StageCatalogMigrationDisposition.Skip.ToString(), StringComparison.Ordinal))
                {
                    summary.skippedCount++;
                }

                if (item.conflicts != null && item.conflicts.Contains("stale-duplicate"))
                {
                    summary.staleDuplicateCount++;
                }

                if (item.aliasPlan != null && item.aliasPlan.Length > 0)
                {
                    summary.typoAliasCandidateCount++;
                }
            }

            return summary;
        }

        private static void WriteReports(StageCatalogMigrationReport report)
        {
            var reportDirectory = Path.Combine(ReportRoot, report.runId);
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(
                Path.Combine(reportDirectory, "migration-report.json"),
                JsonUtility.ToJson(report, prettyPrint: true));
            using var writer = new StreamWriter(Path.Combine(reportDirectory, "migration-report.md"), append: false);
            writer.WriteLine("# Stage Catalog Migration Report");
            writer.WriteLine();
            writer.WriteLine($"RunId: {report.runId}");
            writer.WriteLine($"GeneratedAtUtc: {report.generatedAtUtc}");
            writer.WriteLine($"DryRunHash: {report.dryRunHash}");
            writer.WriteLine();
            foreach (var item in report.items)
            {
                writer.WriteLine($"- `{item.sourceAssetPath}` -> `{item.chosenStageId}` [{item.confidence}] `{item.disposition}`");
            }
        }

        private static void WriteRollbackJournal(
            StageCatalogMigrationReport report,
            StageCatalogMigrationRollbackJournal rollbackJournal)
        {
            var reportDirectory = Path.Combine(ReportRoot, report.runId);
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(
                Path.Combine(reportDirectory, "rollback.json"),
                JsonUtility.ToJson(rollbackJournal, prettyPrint: true));
        }

        private static string ComputeDryRunHash(IEnumerable<StageCatalogMigrationReportItem> items)
        {
            var builder = new StringBuilder();
            foreach (var item in items.OrderBy(entry => entry.sourceAssetGuid, StringComparer.Ordinal))
            {
                builder.Append(item.sourceAssetGuid).Append('|')
                    .Append(item.chosenStageId).Append('|')
                    .Append(item.confidence).Append('|')
                    .Append(item.disposition).AppendLine();
            }

            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var hashBuilder = new StringBuilder(hashBytes.Length * 2);
            for (var i = 0; i < hashBytes.Length; i++)
            {
                hashBuilder.Append(hashBytes[i].ToString("x2"));
            }

            return hashBuilder.ToString();
        }

        private static bool TryDeriveCanonicalStageIdFromFolder(string folderPath, out StageId stageId)
        {
            var folderName = Path.GetFileName(folderPath)?.Trim() ?? string.Empty;
            if (folderName.StartsWith("Stage_", StringComparison.OrdinalIgnoreCase))
            {
                folderName = folderName.Substring("Stage_".Length);
            }
            else if (folderName.StartsWith("Stage-", StringComparison.OrdinalIgnoreCase))
            {
                folderName = folderName.Substring("Stage-".Length);
            }
            else if (folderName.StartsWith("Stage ", StringComparison.OrdinalIgnoreCase))
            {
                folderName = folderName.Substring("Stage ".Length);
            }

            return StageId.TryCreate(InsertWordSeparators(folderName), out stageId);
        }

        private static bool TryParseDisposition(string rawValue, out StageCatalogMigrationDisposition disposition)
        {
            return Enum.TryParse(rawValue, ignoreCase: false, out disposition);
        }

        private static bool EndsWithNumericSuffix(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            for (var i = candidate.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(candidate[i]))
                {
                    continue;
                }

                return candidate[i] == ' ';
            }

            return false;
        }

        private static string InsertWordSeparators(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(rawValue.Length * 2);
            for (var i = 0; i < rawValue.Length; i++)
            {
                var current = rawValue[i];
                if (i > 0 &&
                    char.IsUpper(current) &&
                    char.IsLetterOrDigit(rawValue[i - 1]) &&
                    !char.IsUpper(rawValue[i - 1]))
                {
                    builder.Append('-');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private readonly struct StageDefinitionCandidate
        {
            public StageDefinitionCandidate(
                string guid,
                string assetPath,
                StageDefinition stageDefinition,
                IReadOnlyList<string> detectedSceneRefs)
            {
                Guid = guid;
                AssetPath = assetPath;
                StageDefinition = stageDefinition;
                DetectedSceneRefs = detectedSceneRefs ?? Array.Empty<string>();
            }

            public string Guid { get; }

            public string AssetPath { get; }

            public StageDefinition StageDefinition { get; }

            public IReadOnlyList<string> DetectedSceneRefs { get; }

            public string FolderPath => Path.GetDirectoryName(AssetPath)?.Replace('\\', '/') ?? string.Empty;

            public string FolderName => Path.GetFileName(FolderPath) ?? string.Empty;

            public string AssetFileName => Path.GetFileNameWithoutExtension(AssetPath) ?? string.Empty;
        }
    }
}
