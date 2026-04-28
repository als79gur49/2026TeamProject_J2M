using System;
using System.Collections.Generic;
using Game.Shared.AudioContracts;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class StageCatalogValidator
    {
        private const string CanonicalContentRoot = "Assets/_Features/Stages/Content";

        public StageValidationReport Validate(
            StageCatalog catalog,
            StageCatalogValidationOptions options = null)
        {
            options ??= StageCatalogValidationOptions.Default;
            var report = new StageValidationReport();
            if (catalog == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "catalog.null",
                    "StageCatalog reference cannot be null.",
                    timing: options.Timing);
                return report;
            }

            ValidateEntries(catalog.Entries, catalog.StageIdAliasTable, options, report);
            ValidateStageIdAliases(catalog.StageIdAliasTable, options, report);
            ValidateProgressionGraph(catalog.Entries, options, report);
            return report;
        }

        public StageValidationReport ValidateEntries(
            IReadOnlyList<StageContentEntry> entries,
            StageIdAliasTable aliasTable,
            StageCatalogValidationOptions options = null)
        {
            var report = new StageValidationReport();
            ValidateEntries(entries, aliasTable, options ?? StageCatalogValidationOptions.Default, report);
            ValidateStageIdAliases(aliasTable, options ?? StageCatalogValidationOptions.Default, report);
            ValidateProgressionGraph(entries, options ?? StageCatalogValidationOptions.Default, report);
            return report;
        }

        private static void ValidateEntries(
            IReadOnlyList<StageContentEntry> entries,
            StageIdAliasTable aliasTable,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var ownerByCompanion = new Dictionary<StageCompanionDefinitionBase, StageContentEntry>();
            var entriesByStageId = new Dictionary<StageId, StageContentEntry>();

            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "entry.null",
                        $"StageCatalog entries[{i}] is null.",
                        timing: options.Timing);
                    continue;
                }

                var entryPath = GetAssetPath(entry);
                if (!entry.StageId.IsValid)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.invalid",
                        $"StageContentEntry '{entry.name}' does not contain a valid canonical StageId.",
                        entry,
                        entryPath,
                        options.Timing);
                }
                else if (!entriesByStageId.TryAdd(entry.StageId, entry))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.duplicate",
                        $"Duplicate StageId '{entry.StageId.Value}' is assigned to multiple StageContentEntry assets.",
                        entry,
                        entryPath,
                        options.Timing);
                }

                ValidateEntryPath(entry, entryPath, options, report);
                ValidateAuthoringDefinition(entry, ownerByCompanion, options, report);
                ValidateGameplayDefinition(entry, entryPath, options, report);
                ValidateCompanion(
                    entry,
                    entry.PresentationDefinition,
                    ownerByCompanion,
                    options.RequirePresentationDefinition,
                    "presentation",
                    options,
                    report);
                ValidateCompanion(
                    entry,
                    entry.ClearEvaluationDefinition,
                    ownerByCompanion,
                    options.RequireClearEvaluationDefinition,
                    "clear-evaluation",
                    options,
                    report);
                ValidateCompanion(
                    entry,
                    entry.RewardDefinition,
                    ownerByCompanion,
                    options.RequireRewardDefinition,
                    "reward",
                    options,
                    report);
                ValidateCompanion(
                    entry,
                    entry.ProgressionDefinition,
                    ownerByCompanion,
                    options.RequireProgressionDefinition,
                    "progression",
                    options,
                    report);

                ValidatePresentationBindings(entry, options, report);
                ValidateLegacyPresentationIds(entry, options, report);
                ValidateEvaluationDefinition(entry, options, report);
                ValidateRewardDefinition(entry, aliasTable, options, report);
                ValidateProgressionDefinition(entry, options, report);
            }

            ValidateAliasTargetsExist(aliasTable, entriesByStageId, options, report);
        }

        private static void ValidateAuthoringDefinition(
            StageContentEntry entry,
            IDictionary<StageCompanionDefinitionBase, StageContentEntry> ownerByCompanion,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var authoring = entry.AuthoringDefinition;
            if (authoring == null)
            {
                if (options.Timing == StageValidationTiming.TestOrCi)
                {
                    report.Add(
                        StageValidationSeverity.Info,
                        "authoring.missing",
                        $"StageContentEntry '{entry.name}' has no StageAuthoringDefinition. Existing direct-authored stages remain supported.",
                        entry,
                        GetAssetPath(entry),
                        options.Timing);
                }

                return;
            }

            ValidateCompanion(entry, authoring, ownerByCompanion, false, "authoring", options, report);
            var authoringPath = GetAssetPath(authoring);
            if (authoring.GeneratedGameplayDefinition != entry.GameplayDefinition)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.output.gameplay-mismatch",
                    $"StageAuthoringDefinition '{authoring.name}' generated gameplay reference must match entry GameplayDefinition.",
                    authoring,
                    authoringPath,
                    options.Timing);
            }

            if (authoring.GeneratedPresentationDefinition != entry.PresentationDefinition)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.output.presentation-mismatch",
                    $"StageAuthoringDefinition '{authoring.name}' generated presentation reference must match entry PresentationDefinition.",
                    authoring,
                    authoringPath,
                    options.Timing);
            }

            ValidateAuthoringSourceData(entry, authoring, options, report);
            if (entry.GameplayDefinition != null && entry.PresentationDefinition != null)
            {
                ValidateAuthoringGeneratedSync(entry, authoring, options, report);
            }
        }

        private static void ValidateAuthoringSourceData(
            StageContentEntry entry,
            StageAuthoringDefinition authoring,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var severity = ResolveAuthoringSyncSeverity(authoring);
            var authoringPath = GetAssetPath(authoring);
            var stableGuids = new HashSet<string>(StringComparer.Ordinal);
            var mappingsByGuid = new HashSet<string>(StringComparer.Ordinal);
            var mappingEntityIds = new HashSet<int>();
            var boardBoundsValid = authoring.Board.MaxInclusive.x >= authoring.Board.MinInclusive.x &&
                                   authoring.Board.MaxInclusive.y >= authoring.Board.MinInclusive.y;
            var boardBounds = boardBoundsValid
                ? new Game.Feature.Gameplay.BoardState.BoardBounds(
                    authoring.Board.MinInclusive,
                    authoring.Board.MaxInclusive)
                : Game.Feature.Gameplay.BoardState.BoardBounds.Unbounded;

            if (!boardBoundsValid)
            {
                report.Add(
                    severity,
                    "authoring.board.invalid",
                    $"StageAuthoringDefinition '{authoring.name}' has invalid board bounds.",
                    authoring,
                    authoringPath,
                    options.Timing);
            }

            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null)
                {
                    report.Add(
                        severity,
                        "authoring.placement.null",
                        $"StageAuthoringDefinition '{authoring.name}' placement[{i}] is null.",
                        authoring,
                        authoringPath,
                        options.Timing);
                    continue;
                }

                var stableGuid = Normalize(placement.StableGuid);
                if (string.IsNullOrEmpty(stableGuid))
                {
                    report.Add(
                        severity,
                        "authoring.stable-guid.empty",
                        $"StageAuthoringDefinition '{authoring.name}' placement[{i}] has no stable guid.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (!stableGuids.Add(stableGuid))
                {
                    report.Add(
                        severity,
                        "authoring.stable-guid.duplicate",
                        $"StageAuthoringDefinition '{authoring.name}' contains duplicate stable guid '{stableGuid}'.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (boardBoundsValid && !boardBounds.Contains(placement.Cell.PlanarPosition))
                {
                    report.Add(
                        severity,
                        "authoring.surface-cell.invalid",
                        $"StageAuthoringDefinition '{authoring.name}' placement '{stableGuid}' is outside board bounds at {placement.Cell}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
            }

            var mappings = authoring.EntityIdMappings;
            for (var i = 0; i < mappings.Count; i++)
            {
                var mapping = mappings[i];
                var stableGuid = Normalize(mapping.StableGuid);
                if (string.IsNullOrEmpty(stableGuid))
                {
                    report.Add(
                        severity,
                        "authoring.entity-id-mapping.guid-empty",
                        $"StageAuthoringDefinition '{authoring.name}' mapping[{i}] has no stable guid.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (!mappingsByGuid.Add(stableGuid))
                {
                    report.Add(
                        severity,
                        "authoring.entity-id-mapping.guid-duplicate",
                        $"StageAuthoringDefinition '{authoring.name}' contains duplicate mapping guid '{stableGuid}'.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (mapping.EntityId <= 0)
                {
                    report.Add(
                        severity,
                        "authoring.entity-id-mapping.non-positive",
                        $"StageAuthoringDefinition '{authoring.name}' mapping '{stableGuid}' must use a positive EntityId.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (!mappingEntityIds.Add(mapping.EntityId))
                {
                    report.Add(
                        severity,
                        "authoring.entity-id-mapping.id-duplicate",
                        $"StageAuthoringDefinition '{authoring.name}' contains duplicate mapped EntityId {mapping.EntityId}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
            }
        }

        private static void ValidateAuthoringGeneratedSync(
            StageContentEntry entry,
            StageAuthoringDefinition authoring,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var severity = ResolveAuthoringSyncSeverity(authoring);
            var authoringPath = GetAssetPath(authoring);
            var mappingsByGuid = BuildEntityIdMappings(authoring);
            var expected = BuildExpectedAuthoringOutput(authoring, mappingsByGuid, severity, options, report);
            if (expected == null)
            {
                return;
            }

            if (!StageBoardsEqual(entry.GameplayDefinition.Board, expected.Board))
            {
                report.Add(
                    severity,
                    "authoring.generated-output-mismatch",
                    $"Stage '{entry.StageId.Value}' generated board data is out of sync with StageAuthoringDefinition.",
                    authoring,
                    authoringPath,
                    options.Timing);
            }

            CompareSpawnGroup(entry, authoring, "playerSpawns", entry.GameplayDefinition.PlayerSpawns, expected.PlayerSpawns, severity, options, report);
            CompareSpawnGroup(entry, authoring, "enemySpawns", entry.GameplayDefinition.EnemySpawns, expected.EnemySpawns, severity, options, report);
            CompareSpawnGroup(entry, authoring, "boxSpawns", entry.GameplayDefinition.BoxSpawns, expected.BoxSpawns, severity, options, report);
            CompareSpawnGroup(entry, authoring, "wallSpawns", entry.GameplayDefinition.WallSpawns, expected.WallSpawns, severity, options, report);
            ComparePresentationBindings(entry, authoring, expected, severity, options, report);
        }

        private static Dictionary<string, StageAuthoringIdMapping> BuildEntityIdMappings(StageAuthoringDefinition authoring)
        {
            var result = new Dictionary<string, StageAuthoringIdMapping>(StringComparer.Ordinal);
            var mappings = authoring.EntityIdMappings;
            for (var i = 0; i < mappings.Count; i++)
            {
                var stableGuid = Normalize(mappings[i].StableGuid);
                if (!string.IsNullOrEmpty(stableGuid) && mappings[i].EntityId > 0)
                {
                    result[stableGuid] = mappings[i];
                }
            }

            return result;
        }

        private static ExpectedAuthoringOutput BuildExpectedAuthoringOutput(
            StageAuthoringDefinition authoring,
            IReadOnlyDictionary<string, StageAuthoringIdMapping> mappingsByGuid,
            StageValidationSeverity severity,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var expected = new ExpectedAuthoringOutput(authoring.Board);
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null)
                {
                    continue;
                }

                var stableGuid = Normalize(placement.StableGuid);
                if (string.IsNullOrEmpty(stableGuid))
                {
                    continue;
                }

                if (!mappingsByGuid.TryGetValue(stableGuid, out var mapping) || mapping.EntityId <= 0)
                {
                    report.Add(
                        severity,
                        "authoring.entity-id-drift",
                        $"StageAuthoringDefinition '{authoring.name}' placement '{stableGuid}' has no positive EntityId mapping.",
                        authoring,
                        GetAssetPath(authoring),
                        options.Timing);
                    continue;
                }

                var spawn = new StageSpawnDefinition
                {
                    EntityId = mapping.EntityId,
                    Kind = ToSpawnKind(placement.Kind),
                    Cell = placement.Cell,
                    Facing = placement.Facing,
                    Hp = placement.Hp,
                    BoxCapabilities = placement.BoxCapabilities,
                    EnemyAiMode = placement.EnemyAiMode,
                    EnemyAiStateTimer = placement.EnemyAiStateTimer,
                    EnemyAiProfile = placement.EnemyAiProfileOverride,
                    PresentationId = string.Empty,
                    UnitStackGroup = Normalize(placement.UnitStackGroup),
                };

                switch (placement.Kind)
                {
                    case StageAuthoringEntityKind.Player:
                        expected.PlayerSpawns.Add(spawn);
                        break;
                    case StageAuthoringEntityKind.Enemy:
                        expected.EnemySpawns.Add(spawn);
                        AddEnemyBinding(expected, spawn.EntityId, placement.PresentationId);
                        break;
                    case StageAuthoringEntityKind.Box:
                        expected.BoxSpawns.Add(spawn);
                        AddStaticBinding(expected, spawn.EntityId, placement.PresentationId);
                        break;
                    case StageAuthoringEntityKind.Wall:
                        expected.WallSpawns.Add(spawn);
                        AddStaticBinding(expected, spawn.EntityId, placement.PresentationId);
                        break;
                }
            }

            SortSpawns(expected.PlayerSpawns);
            SortSpawns(expected.EnemySpawns);
            SortSpawns(expected.BoxSpawns);
            SortSpawns(expected.WallSpawns);
            expected.EnemyBindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            expected.StaticBindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            return expected;
        }

        private static void AddEnemyBinding(ExpectedAuthoringOutput expected, int entityId, string presentationId)
        {
            var normalized = Normalize(presentationId);
            if (!string.IsNullOrEmpty(normalized))
            {
                expected.EnemyBindings.Add(new Game.Feature.Gameplay.Host.EnemyPresentationBinding
                {
                    EntityId = entityId,
                    PresentationId = normalized,
                });
            }
        }

        private static void AddStaticBinding(ExpectedAuthoringOutput expected, int entityId, string presentationId)
        {
            var normalized = Normalize(presentationId);
            if (!string.IsNullOrEmpty(normalized))
            {
                expected.StaticBindings.Add(new Game.Feature.Gameplay.Host.StaticEntityPresentationBinding
                {
                    EntityId = entityId,
                    PresentationId = normalized,
                });
            }
        }

        private static void CompareSpawnGroup(
            StageContentEntry entry,
            StageAuthoringDefinition authoring,
            string groupName,
            IReadOnlyList<StageSpawnDefinition> actual,
            IReadOnlyList<StageSpawnDefinition> expected,
            StageValidationSeverity severity,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var sortedActual = new List<StageSpawnDefinition>(actual ?? Array.Empty<StageSpawnDefinition>());
            SortSpawns(sortedActual);
            if (sortedActual.Count != expected.Count)
            {
                report.Add(
                    severity,
                    "authoring.generated-output-mismatch",
                    $"Stage '{entry.StageId.Value}' {groupName} count {sortedActual.Count} does not match generated count {expected.Count}.",
                    authoring,
                    GetAssetPath(authoring),
                    options.Timing);
                return;
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (!StageSpawnsEqual(sortedActual[i], expected[i]))
                {
                    report.Add(
                        severity,
                        "authoring.generated-output-mismatch",
                        $"Stage '{entry.StageId.Value}' {groupName}[{i}] is out of sync with StageAuthoringDefinition.",
                        authoring,
                        GetAssetPath(authoring),
                        options.Timing);
                    return;
                }
            }
        }

        private static void ComparePresentationBindings(
            StageContentEntry entry,
            StageAuthoringDefinition authoring,
            ExpectedAuthoringOutput expected,
            StageValidationSeverity severity,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var actualEnemy = new List<Game.Feature.Gameplay.Host.EnemyPresentationBinding>(
                entry.PresentationDefinition.EnemyPresentationBindings);
            var actualStatic = new List<Game.Feature.Gameplay.Host.StaticEntityPresentationBinding>(
                entry.PresentationDefinition.StaticEntityPresentationBindings);
            actualEnemy.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            actualStatic.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));

            if (!EnemyBindingsEqual(actualEnemy, expected.EnemyBindings) ||
                !StaticBindingsEqual(actualStatic, expected.StaticBindings))
            {
                report.Add(
                    severity,
                    "authoring.presentation-binding-drift",
                    $"Stage '{entry.StageId.Value}' presentation bindings are out of sync with StageAuthoringDefinition.",
                    authoring,
                    GetAssetPath(authoring),
                    options.Timing);
            }
        }

        private static bool StageBoardsEqual(StageBoardDefinition left, StageBoardDefinition right)
        {
            return left.MinInclusive == right.MinInclusive &&
                   left.MaxInclusive == right.MaxInclusive &&
                   left.InitialBottomFace == right.InitialBottomFace;
        }

        private static bool StageSpawnsEqual(StageSpawnDefinition left, StageSpawnDefinition right)
        {
            return left.EntityId == right.EntityId &&
                   left.Kind == right.Kind &&
                   left.Cell == right.Cell &&
                   left.Facing == right.Facing &&
                   left.Hp == right.Hp &&
                   left.BoxCapabilities == right.BoxCapabilities &&
                   left.EnemyAiMode == right.EnemyAiMode &&
                   left.EnemyAiStateTimer == right.EnemyAiStateTimer &&
                   left.EnemyAiProfile == right.EnemyAiProfile &&
                   string.Equals(Normalize(left.PresentationId), string.Empty, StringComparison.Ordinal) &&
                   string.Equals(Normalize(left.UnitStackGroup), Normalize(right.UnitStackGroup), StringComparison.Ordinal);
        }

        private static bool EnemyBindingsEqual(
            IReadOnlyList<Game.Feature.Gameplay.Host.EnemyPresentationBinding> left,
            IReadOnlyList<Game.Feature.Gameplay.Host.EnemyPresentationBinding> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].EntityId != right[i].EntityId ||
                    !string.Equals(Normalize(left[i].PresentationId), Normalize(right[i].PresentationId), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StaticBindingsEqual(
            IReadOnlyList<Game.Feature.Gameplay.Host.StaticEntityPresentationBinding> left,
            IReadOnlyList<Game.Feature.Gameplay.Host.StaticEntityPresentationBinding> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].EntityId != right[i].EntityId ||
                    !string.Equals(Normalize(left[i].PresentationId), Normalize(right[i].PresentationId), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static StageSpawnKind ToSpawnKind(StageAuthoringEntityKind kind)
        {
            return kind switch
            {
                StageAuthoringEntityKind.Player => StageSpawnKind.Player,
                StageAuthoringEntityKind.Enemy => StageSpawnKind.Enemy,
                StageAuthoringEntityKind.Box => StageSpawnKind.Box,
                StageAuthoringEntityKind.Wall => StageSpawnKind.Wall,
                _ => StageSpawnKind.Player,
            };
        }

        private static StageValidationSeverity ResolveAuthoringSyncSeverity(StageAuthoringDefinition authoring)
        {
            return authoring != null && authoring.EnforceGeneratedSync
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        private static void SortSpawns(List<StageSpawnDefinition> spawns)
        {
            spawns.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private sealed class ExpectedAuthoringOutput
        {
            public ExpectedAuthoringOutput(StageBoardDefinition board)
            {
                Board = board;
            }

            public StageBoardDefinition Board { get; }

            public List<StageSpawnDefinition> PlayerSpawns { get; } = new();

            public List<StageSpawnDefinition> EnemySpawns { get; } = new();

            public List<StageSpawnDefinition> BoxSpawns { get; } = new();

            public List<StageSpawnDefinition> WallSpawns { get; } = new();

            public List<Game.Feature.Gameplay.Host.EnemyPresentationBinding> EnemyBindings { get; } = new();

            public List<Game.Feature.Gameplay.Host.StaticEntityPresentationBinding> StaticBindings { get; } = new();
        }

        private static void ValidateEntryPath(
            StageContentEntry entry,
            string entryPath,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (!entry.StageId.IsValid || string.IsNullOrEmpty(entryPath))
            {
                return;
            }

            var expectedFolder = $"{CanonicalContentRoot}/{entry.StageId.Value}";
            var expectedFileName = $"{entry.StageId.Value}_Entry.asset";
            if (!entryPath.StartsWith(expectedFolder, StringComparison.Ordinal) ||
                !entryPath.EndsWith(expectedFileName, StringComparison.Ordinal))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "entry.path-drift",
                    $"StageContentEntry '{entry.name}' should live under '{expectedFolder}' with file name '{expectedFileName}'.",
                    entry,
                    entryPath,
                    options.Timing);
            }
        }

        private static void ValidateGameplayDefinition(
            StageContentEntry entry,
            string entryPath,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (entry.GameplayDefinition == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "gameplay.null",
                    $"StageContentEntry '{entry.name}' is missing a gameplay StageDefinition reference.",
                    entry,
                    entryPath,
                    options.Timing);
                return;
            }

            var gameplayAssetPath = GetAssetPath(entry.GameplayDefinition);
            if (entry.StageId.IsValid)
            {
                var expectedFolder = $"{CanonicalContentRoot}/{entry.StageId.Value}";
                if (!gameplayAssetPath.StartsWith(expectedFolder, StringComparison.Ordinal))
                {
                    report.Add(
                        ResolveGameplayPathSeverity(options),
                        "gameplay.path.noncanonical",
                        $"Gameplay StageDefinition '{entry.GameplayDefinition.name}' must live under canonical folder '{expectedFolder}'.",
                        entry.GameplayDefinition,
                        gameplayAssetPath,
                        options.Timing);
                }
            }

            var stageDefinitionName = entry.GameplayDefinition.name ?? string.Empty;
            if (entry.StageId.IsValid &&
                !string.Equals(stageDefinitionName, entry.StageId.Value, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(stageDefinitionName, entry.StageId.Value, StringComparison.Ordinal))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "gameplay.name-drift",
                    $"Gameplay StageDefinition '{stageDefinitionName}' does not match StageId '{entry.StageId.Value}'.",
                    entry.GameplayDefinition,
                    gameplayAssetPath,
                    options.Timing);
            }

            try
            {
                StageDefinitionValidator.Validate(entry.GameplayDefinition);
            }
            catch (Exception exception)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "gameplay.definition.invalid",
                    $"Gameplay StageDefinition '{stageDefinitionName}' is invalid: {exception.Message}",
                    entry.GameplayDefinition,
                    gameplayAssetPath,
                    options.Timing);
            }
        }

        private static void ValidateCompanion(
            StageContentEntry entry,
            StageCompanionDefinitionBase companion,
            IDictionary<StageCompanionDefinitionBase, StageContentEntry> ownerByCompanion,
            bool required,
            string companionKind,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (companion == null)
            {
                report.Add(
                    required
                        ? ResolveNullCompanionSeverity(options)
                        : StageValidationSeverity.Warning,
                    $"companion.{companionKind}.null",
                    $"StageContentEntry '{entry.name}' is missing its {companionKind} companion asset.",
                    entry,
                    GetAssetPath(entry),
                    options.Timing);
                return;
            }

            if (ownerByCompanion.TryGetValue(companion, out var firstOwner) && firstOwner != entry)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    $"companion.{companionKind}.reused",
                    $"{companionKind} companion '{companion.name}' is reused by both '{firstOwner.name}' and '{entry.name}'.",
                    companion,
                    GetAssetPath(companion),
                    options.Timing);
            }
            else
            {
                ownerByCompanion[companion] = entry;
            }

            var companionPath = GetAssetPath(companion);
            var expectedFolder = entry.StageId.IsValid
                ? $"{CanonicalContentRoot}/{entry.StageId.Value}"
                : string.Empty;
            var expectedPrefix = entry.StageId.IsValid
                ? $"{entry.StageId.Value}_"
                : string.Empty;
            if (!string.IsNullOrEmpty(expectedFolder) &&
                (!companionPath.StartsWith(expectedFolder, StringComparison.Ordinal) ||
                 !System.IO.Path.GetFileName(companionPath).StartsWith(expectedPrefix, StringComparison.Ordinal)))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    $"companion.{companionKind}.path-drift",
                    $"{companionKind} companion '{companion.name}' should live under '{expectedFolder}' and share StageId prefix '{expectedPrefix}'.",
                    companion,
                    companionPath,
                    options.Timing);
            }

            if (companion.OwnerEntry != entry)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    $"companion.{companionKind}.owner-mismatch",
                    $"{companionKind} companion '{companion.name}' owner entry reference does not point back to '{entry.name}'.",
                    companion,
                    companionPath,
                    options.Timing);
            }

            var entryGuid = GetAssetGuid(entry);
            if (!string.IsNullOrEmpty(entryGuid) &&
                !string.Equals(companion.OwnerEntryGuid, entryGuid, StringComparison.Ordinal))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    $"companion.{companionKind}.owner-guid-mismatch",
                    $"{companionKind} companion '{companion.name}' owner guid does not match entry '{entry.name}'.",
                    companion,
                    companionPath,
                    options.Timing);
            }
        }

        private static void ValidatePresentationBindings(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (entry.PresentationDefinition == null || entry.GameplayDefinition == null)
            {
                return;
            }

            var spawns = entry.GameplayDefinition.Spawns;
            var spawnByEntityId = new Dictionary<int, StageSpawnDefinition>();
            for (var i = 0; i < spawns.Length; i++)
            {
                spawnByEntityId[spawns[i].EntityId] = spawns[i];
            }

            var enemyBindings = entry.PresentationDefinition.EnemyPresentationBindings;
            for (var i = 0; i < enemyBindings.Length; i++)
            {
                var binding = enemyBindings[i];
                if (!spawnByEntityId.TryGetValue(binding.EntityId, out var spawn))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.enemy-binding.missing-entity",
                        $"Enemy presentation binding references missing entity id {binding.EntityId}.",
                        entry.PresentationDefinition,
                        GetAssetPath(entry.PresentationDefinition),
                        options.Timing);
                    continue;
                }

                if (spawn.Kind != StageSpawnKind.Enemy)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.enemy-binding.kind-mismatch",
                        $"Enemy presentation binding entity id {binding.EntityId} points to spawn kind {spawn.Kind}.",
                        entry.PresentationDefinition,
                        GetAssetPath(entry.PresentationDefinition),
                        options.Timing);
                }
            }

            var staticBindings = entry.PresentationDefinition.StaticEntityPresentationBindings;
            for (var i = 0; i < staticBindings.Length; i++)
            {
                var binding = staticBindings[i];
                if (!spawnByEntityId.TryGetValue(binding.EntityId, out var spawn))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.static-binding.missing-entity",
                        $"Static presentation binding references missing entity id {binding.EntityId}.",
                        entry.PresentationDefinition,
                        GetAssetPath(entry.PresentationDefinition),
                        options.Timing);
                    continue;
                }

                if (spawn.Kind != StageSpawnKind.Box &&
                    spawn.Kind != StageSpawnKind.Wall)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.static-binding.kind-mismatch",
                        $"Static presentation binding entity id {binding.EntityId} points to spawn kind {spawn.Kind}.",
                        entry.PresentationDefinition,
                        GetAssetPath(entry.PresentationDefinition),
                        options.Timing);
                }
            }

            ValidateBgmReference(entry.PresentationDefinition.BgmReference, entry.PresentationDefinition, options, report);
        }

        private static void ValidateLegacyPresentationIds(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (!options.EnforceCanonicalLegacyPresentationBridgeWarnings ||
                entry.GameplayDefinition == null)
            {
                return;
            }

            var spawns = entry.GameplayDefinition.Spawns;
            var hasLegacyPresentationIds = false;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(spawns[i].PresentationId))
                {
                    hasLegacyPresentationIds = true;
                    break;
                }
            }

            if (!hasLegacyPresentationIds)
            {
                return;
            }

            report.Add(
                ResolveLegacyPresentationIdSeverity(options),
                "presentation.legacy-fallback.non-empty",
                $"Gameplay StageDefinition '{entry.GameplayDefinition.name}' still contains legacy PresentationId authoring. StagePresentationDefinition is the canonical source of truth.",
                entry.GameplayDefinition,
                GetAssetPath(entry.GameplayDefinition),
                options.Timing);
        }

        private static void ValidateEvaluationDefinition(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var definition = entry.ClearEvaluationDefinition;
            if (definition == null)
            {
                return;
            }

            var rankIds = new HashSet<string>(StringComparer.Ordinal);
            var ranks = definition.RankThresholds;
            for (var i = 0; i < ranks.Length; i++)
            {
                var rankId = ranks[i].RankId?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(rankId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "evaluation.rank-id.empty",
                        "Rank thresholds must declare a non-empty rank id.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                    continue;
                }

                if (!rankIds.Add(rankId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "evaluation.rank-id.duplicate",
                        $"Duplicate rank id '{rankId}' detected.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                }
            }

            var challengeIds = new HashSet<string>(StringComparer.Ordinal);
            var challenges = definition.Challenges;
            for (var i = 0; i < challenges.Length; i++)
            {
                var challengeId = challenges[i].ChallengeId?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(challengeId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "evaluation.challenge-id.empty",
                        "Challenge definitions must declare a non-empty challenge id.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                    continue;
                }

                if (!challengeIds.Add(challengeId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "evaluation.challenge-id.duplicate",
                        $"Duplicate challenge id '{challengeId}' detected.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                }
            }
        }

        private static void ValidateRewardDefinition(
            StageContentEntry entry,
            StageIdAliasTable aliasTable,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var definition = entry.RewardDefinition;
            if (definition == null)
            {
                return;
            }

            var evaluationDefinition = entry.ClearEvaluationDefinition;
            var rankIds = new HashSet<string>(StringComparer.Ordinal);
            var challengeIds = new HashSet<string>(StringComparer.Ordinal);
            if (evaluationDefinition != null)
            {
                var ranks = evaluationDefinition.RankThresholds;
                for (var i = 0; i < ranks.Length; i++)
                {
                    rankIds.Add(ranks[i].RankId?.Trim() ?? string.Empty);
                }

                var challenges = evaluationDefinition.Challenges;
                for (var i = 0; i < challenges.Length; i++)
                {
                    challengeIds.Add(challenges[i].ChallengeId?.Trim() ?? string.Empty);
                }
            }

            var currentRuleIds = new HashSet<string>(StringComparer.Ordinal);
            var aliasedRuleIds = new HashSet<string>(StringComparer.Ordinal);
            var rules = definition.Rules;
            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (!TryValidateRewardRuleId(rule.RuleId, out var normalizedRuleId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "reward.rule-id.invalid",
                        $"Reward rule id '{rule.RuleId}' is invalid. Use lower-kebab-case.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                    continue;
                }

                if (!currentRuleIds.Add(normalizedRuleId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "reward.rule-id.duplicate",
                        $"Duplicate reward rule id '{normalizedRuleId}' detected.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                }

                var deprecatedIds = rule.DeprecatedRuleIds;
                for (var deprecatedIndex = 0; deprecatedIndex < deprecatedIds.Length; deprecatedIndex++)
                {
                    var deprecatedId = deprecatedIds[deprecatedIndex]?.Trim() ?? string.Empty;
                    if (!TryValidateRewardRuleId(deprecatedId, out var normalizedDeprecatedId))
                    {
                        report.Add(
                            StageValidationSeverity.Error,
                            "reward.rule-id.alias-invalid",
                            $"Deprecated reward rule id '{deprecatedId}' is invalid.",
                            definition,
                            GetAssetPath(definition),
                            options.Timing);
                        continue;
                    }

                    if (string.Equals(normalizedDeprecatedId, normalizedRuleId, StringComparison.Ordinal))
                    {
                        report.Add(
                            StageValidationSeverity.Warning,
                            "reward.rule-id.alias-self",
                            $"Reward rule '{normalizedRuleId}' lists itself as a deprecated alias.",
                            definition,
                            GetAssetPath(definition),
                            options.Timing);
                    }

                    if (!aliasedRuleIds.Add(normalizedDeprecatedId))
                    {
                        report.Add(
                            StageValidationSeverity.Error,
                            "reward.rule-id.alias-duplicate",
                            $"Deprecated reward rule id '{normalizedDeprecatedId}' is declared more than once.",
                            definition,
                            GetAssetPath(definition),
                            options.Timing);
                    }
                }

                if (!string.IsNullOrWhiteSpace(rule.RequiredRankId) &&
                    !rankIds.Contains(rule.RequiredRankId.Trim()))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "reward.rank-target.missing",
                        $"Reward rule '{normalizedRuleId}' references missing rank id '{rule.RequiredRankId}'.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                }

                if (!string.IsNullOrWhiteSpace(rule.RequiredChallengeId) &&
                    !challengeIds.Contains(rule.RequiredChallengeId.Trim()))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "reward.challenge-target.missing",
                        $"Reward rule '{normalizedRuleId}' references missing challenge id '{rule.RequiredChallengeId}'.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                }
            }
        }

        private static void ValidateProgressionDefinition(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var definition = entry.ProgressionDefinition;
            if (definition == null)
            {
                return;
            }

            var rules = definition.UnlockRules;
            for (var i = 0; i < rules.Length; i++)
            {
                if (rules[i].RequiredStageId.IsValid &&
                    rules[i].RequiredStageId.Equals(entry.StageId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "progression.self-reference",
                        $"Stage '{entry.StageId.Value}' cannot list itself as an unlock prerequisite.",
                        definition,
                        GetAssetPath(definition),
                        options.Timing);
                }
            }
        }

        private static void ValidateStageIdAliases(
            StageIdAliasTable aliasTable,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (aliasTable == null)
            {
                return;
            }

            var seenAliases = new HashSet<string>(StringComparer.Ordinal);
            var entries = aliasTable.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!StageIdNormalizer.TryNormalize(entry.DeprecatedStageId, out var normalizedAlias, out _))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.alias.invalid",
                        $"Deprecated stage id alias '{entry.DeprecatedStageId}' is invalid.",
                        aliasTable,
                        GetAssetPath(aliasTable),
                        options.Timing);
                    continue;
                }

                if (!entry.CurrentStageId.IsValid)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.alias.target-invalid",
                        $"Alias '{normalizedAlias}' points to an invalid current StageId.",
                        aliasTable,
                        GetAssetPath(aliasTable),
                        options.Timing);
                }

                if (string.Equals(normalizedAlias, entry.CurrentStageId.Value, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.alias.self",
                        $"Alias '{normalizedAlias}' cannot point to itself.",
                        aliasTable,
                        GetAssetPath(aliasTable),
                        options.Timing);
                }

                if (!seenAliases.Add(normalizedAlias))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.alias.duplicate",
                        $"Alias '{normalizedAlias}' is declared more than once.",
                        aliasTable,
                        GetAssetPath(aliasTable),
                        options.Timing);
                }
            }
        }

        private static void ValidateAliasTargetsExist(
            StageIdAliasTable aliasTable,
            IReadOnlyDictionary<StageId, StageContentEntry> entriesByStageId,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (aliasTable == null)
            {
                return;
            }

            var entries = aliasTable.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!entries[i].CurrentStageId.IsValid)
                {
                    continue;
                }

                if (!entriesByStageId.ContainsKey(entries[i].CurrentStageId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.alias.target-missing",
                        $"Alias '{entries[i].DeprecatedStageId}' targets missing StageId '{entries[i].CurrentStageId.Value}'.",
                        aliasTable,
                        GetAssetPath(aliasTable),
                        options.Timing);
                }
            }
        }

        private static void ValidateProgressionGraph(
            IReadOnlyList<StageContentEntry> entries,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (entries == null)
            {
                return;
            }

            var entriesByStageId = new Dictionary<StageId, StageContentEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].StageId.IsValid)
                {
                    entriesByStageId[entries[i].StageId] = entries[i];
                }
            }

            foreach (var pair in entriesByStageId)
            {
                var progression = pair.Value.ProgressionDefinition;
                if (progression == null)
                {
                    continue;
                }

                var rules = progression.UnlockRules;
                for (var i = 0; i < rules.Length; i++)
                {
                    var requiredStageId = rules[i].RequiredStageId;
                    if (!requiredStageId.IsValid)
                    {
                        continue;
                    }

                    if (!entriesByStageId.ContainsKey(requiredStageId))
                    {
                        report.Add(
                            StageValidationSeverity.Error,
                            "progression.missing-prerequisite",
                            $"Stage '{pair.Key.Value}' references missing prerequisite stage '{requiredStageId.Value}'.",
                            progression,
                            GetAssetPath(progression),
                            options.Timing);
                    }
                }
            }

            var visiting = new HashSet<StageId>();
            var visited = new HashSet<StageId>();

            foreach (var pair in entriesByStageId)
            {
                DetectCycle(pair.Key, entriesByStageId, visiting, visited, options, report);
            }
        }

        private static void DetectCycle(
            StageId stageId,
            IReadOnlyDictionary<StageId, StageContentEntry> entriesByStageId,
            ISet<StageId> visiting,
            ISet<StageId> visited,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (visited.Contains(stageId))
            {
                return;
            }

            if (!visiting.Add(stageId))
            {
                if (entriesByStageId.TryGetValue(stageId, out var entry))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "progression.cycle",
                        $"Progression graph cycle detected at stage '{stageId.Value}'.",
                        entry.ProgressionDefinition,
                        GetAssetPath(entry.ProgressionDefinition),
                        options.Timing);
                }

                return;
            }

            if (entriesByStageId.TryGetValue(stageId, out var currentEntry) &&
                currentEntry.ProgressionDefinition != null)
            {
                var rules = currentEntry.ProgressionDefinition.UnlockRules;
                for (var i = 0; i < rules.Length; i++)
                {
                    var prerequisiteStageId = rules[i].RequiredStageId;
                    if (prerequisiteStageId.IsValid && entriesByStageId.ContainsKey(prerequisiteStageId))
                    {
                        DetectCycle(prerequisiteStageId, entriesByStageId, visiting, visited, options, report);
                    }
                }
            }

            visiting.Remove(stageId);
            visited.Add(stageId);
        }

        private static void ValidateBgmReference(
            StageBgmReference bgmReference,
            UnityEngine.Object context,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (!bgmReference.HasValue)
            {
                return;
            }

            if (!TryValidateBgmKey(bgmReference.BgmKey))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "bgm-key.invalid",
                    $"BgmKey '{bgmReference.BgmKey}' is invalid. Use lower-kebab-case or slash-separated tokens.",
                    context,
                    GetAssetPath(context),
                    options.Timing);
                return;
            }

            if (options.KnownBgmKeys != null &&
                !options.KnownBgmKeys.Contains(bgmReference.BgmKey))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "bgm-key.unknown",
                    $"BgmKey '{bgmReference.BgmKey}' is not present in the optional validation catalog.",
                    context,
                    GetAssetPath(context),
                    options.Timing);
            }
        }

        private static bool TryValidateRewardRuleId(string candidate, out string normalized)
        {
            normalized = candidate?.Trim() ?? string.Empty;
            return StageIdNormalizer.IsCanonical(normalized);
        }

        private static bool TryValidateBgmKey(string bgmKey)
        {
            if (string.IsNullOrWhiteSpace(bgmKey))
            {
                return false;
            }

            for (var i = 0; i < bgmKey.Length; i++)
            {
                var c = bgmKey[i];
                var isValid = (c >= 'a' && c <= 'z') ||
                              (c >= '0' && c <= '9') ||
                              c == '-' ||
                              c == '/';
                if (!isValid)
                {
                    return false;
                }
            }

            return true;
        }

        private static StageValidationSeverity ResolveNullCompanionSeverity(StageCatalogValidationOptions options)
        {
            return options.Phase >= StageValidationPhase.Phase2_MigrationAnalysis
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        private static StageValidationSeverity ResolveGameplayPathSeverity(StageCatalogValidationOptions options)
        {
            return options.Phase >= StageValidationPhase.Phase5_Hardening
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        private static StageValidationSeverity ResolveLegacyPresentationIdSeverity(StageCatalogValidationOptions options)
        {
            return options.Phase >= StageValidationPhase.Phase5_Hardening
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        private static string GetAssetPath(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            return asset == null ? string.Empty : UnityEditor.AssetDatabase.GetAssetPath(asset);
#else
            return string.Empty;
#endif
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            var path = GetAssetPath(asset);
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : UnityEditor.AssetDatabase.AssetPathToGUID(path);
#else
            return string.Empty;
#endif
        }
    }
}
