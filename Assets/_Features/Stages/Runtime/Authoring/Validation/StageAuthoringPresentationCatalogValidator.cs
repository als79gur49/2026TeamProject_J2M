using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class StageAuthoringPresentationCatalogEntrySnapshot
    {
        public StageAuthoringPresentationCatalogEntrySnapshot(
            int entryIndex,
            string rawPresentationId,
            string presentationId,
            bool duplicatePresentationId,
            bool viewPrefabMissing)
        {
            EntryIndex = entryIndex;
            RawPresentationId = rawPresentationId ?? string.Empty;
            PresentationId = presentationId ?? string.Empty;
            DuplicatePresentationId = duplicatePresentationId;
            ViewPrefabMissing = viewPrefabMissing;
        }

        public int EntryIndex { get; }

        public string RawPresentationId { get; }

        public string PresentationId { get; }

        public bool EmptyPresentationId => string.IsNullOrEmpty(PresentationId);

        public bool DuplicatePresentationId { get; }

        public bool ViewPrefabMissing { get; }
    }

    public sealed class StageAuthoringPresentationCatalogSnapshot
    {
        public StageAuthoringPresentationCatalogSnapshot(
            bool catalogAssigned,
            string catalogType,
            string catalogName,
            StageAuthoringPresentationCatalogEntrySnapshot[] entries,
            string[] presentationIds)
        {
            CatalogAssigned = catalogAssigned;
            CatalogType = catalogType ?? string.Empty;
            CatalogName = catalogName ?? string.Empty;
            Entries = entries ?? Array.Empty<StageAuthoringPresentationCatalogEntrySnapshot>();
            PresentationIds = presentationIds ?? Array.Empty<string>();
        }

        public bool CatalogAssigned { get; }

        public string CatalogType { get; }

        public string CatalogName { get; }

        public StageAuthoringPresentationCatalogEntrySnapshot[] Entries { get; }

        public string[] PresentationIds { get; }

        public int EntryCount => Entries.Length;

        public bool ContainsPresentationId(string presentationId)
        {
            if (string.IsNullOrEmpty(presentationId))
            {
                return false;
            }

            for (var i = 0; i < PresentationIds.Length; i++)
            {
                if (string.Equals(PresentationIds[i], presentationId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public readonly struct StageAuthoringPresentationBindingSnapshot
    {
        public StageAuthoringPresentationBindingSnapshot(
            int entityId,
            string presentationId,
            int bindingIndex,
            bool enemyBinding)
        {
            EntityId = entityId;
            PresentationId = presentationId ?? string.Empty;
            BindingIndex = bindingIndex;
            EnemyBinding = enemyBinding;
        }

        public int EntityId { get; }

        public string PresentationId { get; }

        public int BindingIndex { get; }

        public bool EnemyBinding { get; }
    }

    public static class StageAuthoringPresentationCatalogValidator
    {
        private const string EnemyCatalogType = "Enemy";
        private const string StaticCatalogType = "Static";

        public static StageAuthoringPresentationCatalogSnapshot BuildEnemyCatalogSnapshot(
            EnemyPresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return new StageAuthoringPresentationCatalogSnapshot(
                    catalogAssigned: false,
                    EnemyCatalogType,
                    string.Empty,
                    Array.Empty<StageAuthoringPresentationCatalogEntrySnapshot>(),
                    Array.Empty<string>());
            }

            var entries = catalog.Entries;
            var snapshots = new List<StageAuthoringPresentationCatalogEntrySnapshot>(entries.Length);
            var ids = new List<string>(entries.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var presentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId);
                var duplicate = !string.IsNullOrEmpty(presentationId) && !seen.Add(presentationId);
                if (!string.IsNullOrEmpty(presentationId) && !duplicate)
                {
                    ids.Add(presentationId);
                }

                snapshots.Add(new StageAuthoringPresentationCatalogEntrySnapshot(
                    i,
                    entry.PresentationId,
                    presentationId,
                    duplicate,
                    entry.ViewPrefab == null));
            }

            return new StageAuthoringPresentationCatalogSnapshot(
                catalogAssigned: true,
                EnemyCatalogType,
                catalog.name,
                snapshots.ToArray(),
                ids.ToArray());
        }

        public static StageAuthoringPresentationCatalogSnapshot BuildStaticCatalogSnapshot(
            StaticEntityPresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return new StageAuthoringPresentationCatalogSnapshot(
                    catalogAssigned: false,
                    StaticCatalogType,
                    string.Empty,
                    Array.Empty<StageAuthoringPresentationCatalogEntrySnapshot>(),
                    Array.Empty<string>());
            }

            var entries = catalog.Entries;
            var snapshots = new List<StageAuthoringPresentationCatalogEntrySnapshot>(entries.Length);
            var ids = new List<string>(entries.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var presentationId = StaticEntityPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId);
                var duplicate = !string.IsNullOrEmpty(presentationId) && !seen.Add(presentationId);
                if (!string.IsNullOrEmpty(presentationId) && !duplicate)
                {
                    ids.Add(presentationId);
                }

                snapshots.Add(new StageAuthoringPresentationCatalogEntrySnapshot(
                    i,
                    entry.PresentationId,
                    presentationId,
                    duplicate,
                    entry.ViewPrefab == null));
            }

            return new StageAuthoringPresentationCatalogSnapshot(
                catalogAssigned: true,
                StaticCatalogType,
                catalog.name,
                snapshots.ToArray(),
                ids.ToArray());
        }

        public static StageValidationIssue[] ValidateEntry(
            StageContentEntry entry,
            StageCatalogValidationOptions options)
        {
            var issues = new List<StageValidationIssue>();
            if (entry == null)
            {
                return issues.ToArray();
            }

            options ??= StageCatalogValidationOptions.Default;
            var authoring = entry.AuthoringDefinition;
            var presentation = entry.PresentationDefinition;
            var gameplay = entry.GameplayDefinition;
            var stageId = entry.StageId.IsValid ? entry.StageId.Value : string.Empty;
            var strict = authoring != null && authoring.EnforceGeneratedSync;

            if (presentation == null)
            {
                if (authoring != null && HasPresentationPlacements(authoring))
                {
                    issues.Add(CreateIssue(
                        ResolveSoftSeverity(strict),
                        "PresentationCatalog.GeneratedPresentationDefinitionMissing",
                        $"StageAuthoringDefinition '{authoring.name}' has presentation-authored placements but no generated presentation definition is assigned.",
                        authoring,
                        GetAssetPath(authoring, options),
                        options.Timing,
                        stageId,
                        authoring.name,
                        string.Empty));
                }

                return issues.ToArray();
            }

            var enemySnapshot = BuildEnemyCatalogSnapshot(presentation.EnemyPresentationCatalog);
            var staticSnapshot = BuildStaticCatalogSnapshot(presentation.StaticEntityPresentationCatalog);
            var presentationPath = GetAssetPath(presentation, options);

            ValidateCatalogEntries(
                presentation.EnemyPresentationCatalog,
                enemySnapshot,
                options,
                stageId,
                presentation.name,
                issues);
            ValidateCatalogEntries(
                presentation.StaticEntityPresentationCatalog,
                staticSnapshot,
                options,
                stageId,
                presentation.name,
                issues);

            var requiredCatalogs = ResolveRequiredCatalogs(authoring, presentation);
            ValidateCatalogAvailability(
                presentation,
                presentationPath,
                enemySnapshot,
                staticSnapshot,
                requiredCatalogs,
                strict,
                options,
                stageId,
                authoring != null ? authoring.name : string.Empty,
                presentation.name,
                issues);

            if (authoring != null)
            {
                ValidateAuthoringPlacements(
                    authoring,
                    enemySnapshot,
                    staticSnapshot,
                    strict,
                    options,
                    stageId,
                    presentation.name,
                    issues);
            }

            if (gameplay != null)
            {
                ValidateGeneratedBindings(
                    gameplay,
                    presentation,
                    enemySnapshot,
                    staticSnapshot,
                    strict,
                    options,
                    stageId,
                    authoring != null ? authoring.name : string.Empty,
                    issues);
            }

            return issues.ToArray();
        }

        private static void ValidateCatalogEntries(
            UnityEngine.Object catalog,
            StageAuthoringPresentationCatalogSnapshot snapshot,
            StageCatalogValidationOptions options,
            string stageId,
            string outputAssetName,
            ICollection<StageValidationIssue> issues)
        {
            if (!snapshot.CatalogAssigned)
            {
                return;
            }

            var catalogPath = GetAssetPath(catalog, options);
            for (var i = 0; i < snapshot.Entries.Length; i++)
            {
                var entry = snapshot.Entries[i];
                var entryField = $"{snapshot.CatalogType}Catalog.Entries[{entry.EntryIndex}]";
                if (entry.EmptyPresentationId)
                {
                    issues.Add(CreateIssue(
                        StageValidationSeverity.Error,
                        "PresentationCatalog.EmptyPresentationId",
                        $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' entry[{entry.EntryIndex}] must declare a non-empty PresentationId.",
                        catalog,
                        catalogPath,
                        options.Timing,
                        stageId,
                        string.Empty,
                        outputAssetName,
                        fieldName: $"{entryField}.PresentationId",
                        expectedValue: "non-empty",
                        actualValue: entry.RawPresentationId,
                        presentationId: entry.PresentationId));
                }

                if (entry.DuplicatePresentationId)
                {
                    issues.Add(CreateIssue(
                        StageValidationSeverity.Error,
                        "PresentationCatalog.DuplicatePresentationId",
                        $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' contains duplicate PresentationId '{entry.PresentationId}'.",
                        catalog,
                        catalogPath,
                        options.Timing,
                        stageId,
                        string.Empty,
                        outputAssetName,
                        fieldName: $"{entryField}.PresentationId",
                        expectedValue: "unique normalized id",
                        actualValue: entry.RawPresentationId,
                        presentationId: entry.PresentationId));
                }

                if (entry.ViewPrefabMissing)
                {
                    issues.Add(CreateIssue(
                        StageValidationSeverity.Error,
                        "PresentationCatalog.ViewPrefabMissing",
                        $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' entry[{entry.EntryIndex}] PresentationId '{entry.PresentationId}' requires a non-null ViewPrefab.",
                        catalog,
                        catalogPath,
                        options.Timing,
                        stageId,
                        string.Empty,
                        outputAssetName,
                        fieldName: $"{entryField}.ViewPrefab",
                        expectedValue: "non-null",
                        actualValue: "null",
                        presentationId: entry.PresentationId));
                }
            }
        }

        private static RequiredCatalogs ResolveRequiredCatalogs(
            StageAuthoringDefinition authoring,
            StagePresentationDefinition presentation)
        {
            var result = new RequiredCatalogs();
            if (authoring != null)
            {
                var placements = authoring.Placements;
                for (var i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    if (placement == null)
                    {
                        continue;
                    }

                    switch (StageAuthoringKindRegistry.GetPresentationLane(placement.Kind))
                    {
                        case StageAuthoringPresentationLane.Enemy:
                            result.Enemy = true;
                            break;
                        case StageAuthoringPresentationLane.Static:
                            result.Static = true;
                            break;
                    }
                }
            }

            result.Enemy |= presentation.EnemyPresentationBindings.Length > 0;
            result.Static |= presentation.StaticEntityPresentationBindings.Length > 0;
            return result;
        }

        private static void ValidateCatalogAvailability(
            StagePresentationDefinition presentation,
            string presentationPath,
            StageAuthoringPresentationCatalogSnapshot enemySnapshot,
            StageAuthoringPresentationCatalogSnapshot staticSnapshot,
            RequiredCatalogs requiredCatalogs,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string authoringAssetName,
            string outputAssetName,
            ICollection<StageValidationIssue> issues)
        {
            if (requiredCatalogs.Enemy)
            {
                if (!enemySnapshot.CatalogAssigned)
                {
                    issues.Add(CreateIssue(
                        ResolveSoftSeverity(strict),
                        "PresentationCatalog.EnemyCatalogMissing",
                        $"StagePresentationDefinition '{presentation.name}' requires an enemy presentation catalog.",
                        presentation,
                        presentationPath,
                        options.Timing,
                        stageId,
                        authoringAssetName,
                        outputAssetName,
                        fieldName: "EnemyPresentationCatalog",
                        expectedValue: "assigned",
                        actualValue: "null"));
                }
                else if (enemySnapshot.PresentationIds.Length == 0)
                {
                    issues.Add(CreateIssue(
                        ResolveSoftSeverity(strict),
                        "PresentationCatalog.EnemyCatalogEmpty",
                        $"StagePresentationDefinition '{presentation.name}' enemy presentation catalog has no usable PresentationId entries.",
                        presentation,
                        presentationPath,
                        options.Timing,
                        stageId,
                        authoringAssetName,
                        outputAssetName,
                        fieldName: "EnemyPresentationCatalog",
                        expectedValue: "at least one usable PresentationId",
                        actualValue: "0"));
                }
            }

            if (!requiredCatalogs.Static)
            {
                return;
            }

            if (!staticSnapshot.CatalogAssigned)
            {
                issues.Add(CreateIssue(
                    ResolveSoftSeverity(strict),
                    "PresentationCatalog.StaticCatalogMissing",
                    $"StagePresentationDefinition '{presentation.name}' requires a static entity presentation catalog.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    outputAssetName,
                    fieldName: "StaticEntityPresentationCatalog",
                    expectedValue: "assigned",
                    actualValue: "null"));
            }
            else if (staticSnapshot.PresentationIds.Length == 0)
            {
                issues.Add(CreateIssue(
                    ResolveSoftSeverity(strict),
                    "PresentationCatalog.StaticCatalogEmpty",
                    $"StagePresentationDefinition '{presentation.name}' static entity presentation catalog has no usable PresentationId entries.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    outputAssetName,
                    fieldName: "StaticEntityPresentationCatalog",
                    expectedValue: "at least one usable PresentationId",
                    actualValue: "0"));
            }
        }

        private static void ValidateAuthoringPlacements(
            StageAuthoringDefinition authoring,
            StageAuthoringPresentationCatalogSnapshot enemySnapshot,
            StageAuthoringPresentationCatalogSnapshot staticSnapshot,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string outputAssetName,
            ICollection<StageValidationIssue> issues)
        {
            var authoringPath = GetAssetPath(authoring, options);
            var allocationPlan = StageAuthoringProjection.BuildAllocationPlan(authoring);
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null || !StageAuthoringKindRegistry.RequiresPresentation(placement.Kind))
                {
                    continue;
                }

                var stableGuid = StageAuthoringProjection.Normalize(placement.StableGuid);
                allocationPlan.EntityIdsByStableGuid.TryGetValue(stableGuid, out var entityId);
                var lane = StageAuthoringKindRegistry.GetPresentationLane(placement.Kind);
                var presentationId = NormalizePlacementPresentationId(lane, placement.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    var staticPlacement = lane == StageAuthoringPresentationLane.Static;
                    issues.Add(CreateIssue(
                        ResolveSoftSeverity(strict),
                        staticPlacement
                            ? "PresentationCatalog.StaticPresentationIdEmpty"
                            : "PresentationCatalog.EnemyPresentationIdEmpty",
                        $"{placement.Kind} placement '{stableGuid}' must declare a non-empty PresentationId.",
                        authoring,
                        authoringPath,
                        options.Timing,
                        stageId,
                        authoring.name,
                        outputAssetName,
                        entityId,
                        stableGuid,
                        $"Placements[{i}].PresentationId",
                        "non-empty",
                        placement.PresentationId,
                        presentationId));
                    continue;
                }

                if (lane == StageAuthoringPresentationLane.Enemy)
                {
                    if (enemySnapshot.PresentationIds.Length > 0 &&
                        !enemySnapshot.ContainsPresentationId(presentationId))
                    {
                        issues.Add(CreateIssue(
                            ResolveSoftSeverity(strict),
                            "PresentationCatalog.EnemyPresentationIdMissing",
                            $"Enemy placement '{stableGuid}' references unresolved PresentationId '{presentationId}'.",
                            authoring,
                            authoringPath,
                            options.Timing,
                            stageId,
                            authoring.name,
                            outputAssetName,
                            entityId,
                            stableGuid,
                            $"Placements[{i}].PresentationId",
                            "id present in enemy presentation catalog",
                            placement.PresentationId,
                            presentationId));
                    }
                }
                else if (lane == StageAuthoringPresentationLane.Static &&
                         staticSnapshot.PresentationIds.Length > 0 &&
                         !staticSnapshot.ContainsPresentationId(presentationId))
                {
                    issues.Add(CreateIssue(
                        ResolveSoftSeverity(strict),
                        "PresentationCatalog.StaticPresentationIdMissing",
                        $"{placement.Kind} placement '{stableGuid}' references unresolved PresentationId '{presentationId}'.",
                        authoring,
                        authoringPath,
                        options.Timing,
                        stageId,
                        authoring.name,
                        outputAssetName,
                        entityId,
                        stableGuid,
                        $"Placements[{i}].PresentationId",
                        "id present in static entity presentation catalog",
                        placement.PresentationId,
                        presentationId));
                }
            }
        }

        private static void ValidateGeneratedBindings(
            StageDefinition gameplay,
            StagePresentationDefinition presentation,
            StageAuthoringPresentationCatalogSnapshot enemySnapshot,
            StageAuthoringPresentationCatalogSnapshot staticSnapshot,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string authoringAssetName,
            ICollection<StageValidationIssue> issues)
        {
            var spawnMap = BuildSpawnMap(gameplay);
            var enemySpawnEntityIds = BuildSpawnEntityIds(gameplay.EnemySpawns);
            var staticSpawnEntityIds = BuildStaticSpawnEntityIds(gameplay);
            var presentationPath = GetAssetPath(presentation, options);
            var enemyBindingsByEntityId = new HashSet<int>();
            var staticBindingsByEntityId = new HashSet<int>();

            var enemyBindings = presentation.EnemyPresentationBindings;
            for (var i = 0; i < enemyBindings.Length; i++)
            {
                var binding = enemyBindings[i];
                enemyBindingsByEntityId.Add(binding.EntityId);
                ValidateBindingTarget(
                    binding.EntityId,
                    binding.PresentationId,
                    i,
                    enemyBinding: true,
                    spawnMap,
                    enemySnapshot,
                    strict,
                    options,
                    stageId,
                    authoringAssetName,
                    presentation,
                    presentationPath,
                    issues);
            }

            var staticBindings = presentation.StaticEntityPresentationBindings;
            for (var i = 0; i < staticBindings.Length; i++)
            {
                var binding = staticBindings[i];
                staticBindingsByEntityId.Add(binding.EntityId);
                ValidateBindingTarget(
                    binding.EntityId,
                    binding.PresentationId,
                    i,
                    enemyBinding: false,
                    spawnMap,
                    staticSnapshot,
                    strict,
                    options,
                    stageId,
                    authoringAssetName,
                    presentation,
                    presentationPath,
                    issues);
            }

            foreach (var entityId in enemySpawnEntityIds)
            {
                if (enemyBindingsByEntityId.Contains(entityId))
                {
                    continue;
                }

                issues.Add(CreateIssue(
                    ResolveSoftSeverity(strict),
                    "PresentationBinding.MissingEnemyBinding",
                    $"Enemy spawn EntityId={entityId} has no generated enemy presentation binding.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: "EnemyPresentationBindings",
                    expectedValue: $"EntityId={entityId}",
                    actualValue: "missing"));
            }

            foreach (var entityId in staticSpawnEntityIds)
            {
                if (staticBindingsByEntityId.Contains(entityId))
                {
                    continue;
                }

                issues.Add(CreateIssue(
                    ResolveSoftSeverity(strict),
                    "PresentationBinding.MissingStaticBinding",
                    $"Static spawn EntityId={entityId} has no generated static entity presentation binding.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: "StaticEntityPresentationBindings",
                    expectedValue: $"EntityId={entityId}",
                    actualValue: "missing"));
            }
        }

        private static void ValidateBindingTarget(
            int entityId,
            string rawPresentationId,
            int bindingIndex,
            bool enemyBinding,
            IReadOnlyDictionary<int, StageSpawnDefinition> spawnMap,
            StageAuthoringPresentationCatalogSnapshot catalogSnapshot,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string authoringAssetName,
            StagePresentationDefinition presentation,
            string presentationPath,
            ICollection<StageValidationIssue> issues)
        {
            var fieldPrefix = enemyBinding
                ? $"EnemyPresentationBindings[{bindingIndex}]"
                : $"StaticEntityPresentationBindings[{bindingIndex}]";
            var presentationId = enemyBinding
                ? EnemyPresentationCatalogResolver.NormalizePresentationId(rawPresentationId)
                : StaticEntityPresentationCatalogResolver.NormalizePresentationId(rawPresentationId);

            if (!spawnMap.TryGetValue(entityId, out var spawn))
            {
                issues.Add(CreateIssue(
                    ResolveSoftSeverity(strict),
                    enemyBinding
                        ? "PresentationBinding.OrphanEnemyBinding"
                        : "PresentationBinding.OrphanStaticBinding",
                    $"{(enemyBinding ? "Enemy" : "Static")} presentation binding references missing EntityId={entityId}.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: $"{fieldPrefix}.EntityId",
                    expectedValue: enemyBinding ? "enemy spawn entity id" : "box/wall spawn entity id",
                    actualValue: entityId.ToString(),
                    presentationId: presentationId));
            }
            else if (!SpawnMatchesPresentationLane(spawn.Kind, ResolveBindingLane(enemyBinding)))
            {
                issues.Add(CreateIssue(
                    StageValidationSeverity.Error,
                    "PresentationBinding.BindingReferencesWrongKind",
                    $"{(enemyBinding ? "Enemy" : "Static")} presentation binding EntityId={entityId} points to spawn kind {spawn.Kind}.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: $"{fieldPrefix}.EntityId",
                    expectedValue: ResolveBindingExpectedSpawnKind(enemyBinding),
                    actualValue: spawn.Kind.ToString(),
                    presentationId: presentationId));
            }

            if (string.IsNullOrEmpty(presentationId))
            {
                issues.Add(CreateIssue(
                    ResolveSoftSeverity(strict),
                    enemyBinding
                        ? "PresentationCatalog.EnemyPresentationIdEmpty"
                        : "PresentationCatalog.StaticPresentationIdEmpty",
                    $"{(enemyBinding ? "Enemy" : "Static")} presentation binding EntityId={entityId} must declare a non-empty PresentationId.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: $"{fieldPrefix}.PresentationId",
                    expectedValue: "non-empty",
                    actualValue: rawPresentationId,
                    presentationId: presentationId));
                return;
            }

            if (catalogSnapshot.PresentationIds.Length == 0 ||
                catalogSnapshot.ContainsPresentationId(presentationId))
            {
                return;
            }

            issues.Add(CreateIssue(
                ResolveSoftSeverity(strict),
                enemyBinding
                    ? "PresentationCatalog.EnemyPresentationIdMissing"
                    : "PresentationCatalog.StaticPresentationIdMissing",
                $"{(enemyBinding ? "Enemy" : "Static")} presentation binding EntityId={entityId} references unresolved PresentationId '{presentationId}'.",
                presentation,
                presentationPath,
                options.Timing,
                stageId,
                authoringAssetName,
                presentation.name,
                entityId,
                fieldName: $"{fieldPrefix}.PresentationId",
                expectedValue: enemyBinding
                    ? "id present in enemy presentation catalog"
                    : "id present in static entity presentation catalog",
                actualValue: rawPresentationId,
                presentationId: presentationId));
        }

        private static Dictionary<int, StageSpawnDefinition> BuildSpawnMap(StageDefinition gameplay)
        {
            var result = new Dictionary<int, StageSpawnDefinition>();
            var spawns = gameplay.Spawns;
            for (var i = 0; i < spawns.Length; i++)
            {
                result[spawns[i].EntityId] = spawns[i];
            }

            return result;
        }

        private static HashSet<int> BuildSpawnEntityIds(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var result = new HashSet<int>();
            if (spawns == null)
            {
                return result;
            }

            for (var i = 0; i < spawns.Count; i++)
            {
                result.Add(spawns[i].EntityId);
            }

            return result;
        }

        private static HashSet<int> BuildStaticSpawnEntityIds(StageDefinition gameplay)
        {
            var result = BuildSpawnEntityIds(gameplay.BoxSpawns);
            var walls = gameplay.WallSpawns;
            for (var i = 0; i < walls.Length; i++)
            {
                result.Add(walls[i].EntityId);
            }

            return result;
        }

        private static bool HasPresentationPlacements(StageAuthoringDefinition authoring)
        {
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement != null && StageAuthoringKindRegistry.RequiresPresentation(placement.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePlacementPresentationId(
            StageAuthoringPresentationLane lane,
            string presentationId)
        {
            return lane switch
            {
                StageAuthoringPresentationLane.Enemy => EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId),
                StageAuthoringPresentationLane.Static => StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId),
                _ => StageAuthoringProjection.Normalize(presentationId),
            };
        }

        private static StageAuthoringPresentationLane ResolveBindingLane(bool enemyBinding)
        {
            return enemyBinding ? StageAuthoringPresentationLane.Enemy : StageAuthoringPresentationLane.Static;
        }

        private static bool SpawnMatchesPresentationLane(StageSpawnKind spawnKind, StageAuthoringPresentationLane lane)
        {
            return lane switch
            {
                StageAuthoringPresentationLane.Enemy => spawnKind == StageSpawnKind.Enemy,
                StageAuthoringPresentationLane.Static => spawnKind == StageSpawnKind.Box ||
                                                         spawnKind == StageSpawnKind.Wall,
                _ => false,
            };
        }

        private static string ResolveBindingExpectedSpawnKind(bool enemyBinding)
        {
            return enemyBinding ? "Enemy" : "Box or Wall";
        }

        private static StageValidationSeverity ResolveSoftSeverity(bool strict)
        {
            return strict ? StageValidationSeverity.Error : StageValidationSeverity.Warning;
        }

        private static StageValidationIssue CreateIssue(
            StageValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context,
            string assetPath,
            StageValidationTiming timing,
            string stageId,
            string authoringAssetName,
            string outputAssetName,
            int entityId = 0,
            string stableGuid = "",
            string fieldName = "",
            string expectedValue = "",
            string actualValue = "",
            string presentationId = "")
        {
            return new StageValidationIssue(
                severity,
                code,
                message,
                context,
                assetPath,
                timing,
                stageId,
                authoringAssetName,
                outputAssetName,
                entityId,
                stableGuid,
                fieldName,
                expectedValue,
                actualValue,
                presentationId);
        }

        private static string GetAssetPath(UnityEngine.Object asset, StageCatalogValidationOptions options)
        {
            return (options?.ResolvedAssetMetadataProvider)?.GetAssetPath(asset) ?? string.Empty;
        }

        private struct RequiredCatalogs
        {
            public bool Enemy;
            public bool Static;
        }
    }
}
