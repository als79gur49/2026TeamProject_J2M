using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Shared.AudioContracts;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class StageCatalogValidator
    {
        private const string CanonicalContentRoot = StageContentPaths.CampaignLevel01StagesRoot;
        private static IStageValidationAssetMetadataProvider defaultMetadataProvider;
        private static readonly IStageValidationAssetMetadataProvider NoOpMetadataProvider =
            new NoOpAssetMetadataProvider();

        public static void ConfigureDefaultAssetMetadataProvider(IStageValidationAssetMetadataProvider provider)
        {
            defaultMetadataProvider = provider;
        }

        public static void ClearDefaultAssetMetadataProvider()
        {
            defaultMetadataProvider = null;
        }

        public static IDisposable UseDefaultAssetMetadataProvider(IStageValidationAssetMetadataProvider provider)
        {
            var previous = defaultMetadataProvider;
            defaultMetadataProvider = provider;
            return new DefaultAssetMetadataProviderScope(previous);
        }

        public static IStageValidationAssetMetadataProvider GetDefaultAssetMetadataProviderForTests()
        {
            return defaultMetadataProvider;
        }

        public StageValidationReport Validate(
            StageCatalog catalog,
            StageCatalogValidationOptions options = null)
        {
            options = ResolveOptions(options);
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
            options = ResolveOptions(options);
            ValidateEntries(entries, aliasTable, options, report);
            ValidateStageIdAliases(aliasTable, options, report);
            ValidateProgressionGraph(entries, options, report);
            return report;
        }

        private static StageCatalogValidationOptions ResolveOptions(StageCatalogValidationOptions options)
        {
            options ??= StageCatalogValidationOptions.Default;
            return options.CloneWithResolvedAssetMetadataProvider(
                options.AssetMetadataProvider ?? defaultMetadataProvider ?? NoOpMetadataProvider);
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

                var entryPath = GetAssetPath(entry, options);
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

                ValidatePresentationCatalogIntegrity(entry, options, report);
                ValidateLegacyPresentationIds(entry, options, report);
                ValidateObjectiveDisplay(entry, options, report);
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
                        GetAssetPath(entry, options),
                        options.Timing);
                }

                return;
            }

            ValidateCompanion(entry, authoring, ownerByCompanion, false, "authoring", options, report);
            var authoringPath = GetAssetPath(authoring, options);
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
            var authoringPath = GetAssetPath(authoring, options);
            var stableGuids = new HashSet<string>(StringComparer.Ordinal);
            var mappingsByGuid = new HashSet<string>(StringComparer.Ordinal);
            var mappingEntityIds = new HashSet<int>();
            var wallCells = new HashSet<SurfaceCell>();
            var boardBoundsValid = authoring.Board.MaxInclusive.x >= authoring.Board.MinInclusive.x &&
                                   authoring.Board.MaxInclusive.y >= authoring.Board.MinInclusive.y;
            var boardBounds = boardBoundsValid
                ? new BoardBounds(
                    authoring.Board.MinInclusive,
                    authoring.Board.MaxInclusive)
                : BoardBounds.Unbounded;

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

                if (placement.Kind == StageAuthoringEntityKind.Wall)
                {
                    wallCells.Add(placement.Cell);
                }
            }

            ValidateAuthoringTileFeatures(authoring, authoringPath, options, severity, boardBoundsValid, boardBounds, wallCells, report);

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

        private static void ValidateAuthoringTileFeatures(
            StageAuthoringDefinition authoring,
            string authoringPath,
            StageCatalogValidationOptions options,
            StageValidationSeverity severity,
            bool boardBoundsValid,
            BoardBounds boardBounds,
            HashSet<SurfaceCell> wallCells,
            StageValidationReport report)
        {
            var tileIds = new HashSet<int>();
            var slideCells = new HashSet<SurfaceCell>();
            var barricadeCells = new HashSet<SurfaceCell>();
            var moonBlockGeneratorCells = new HashSet<SurfaceCell>();
            var exitCount = 0;
            var entranceCount = 0;
            var moonBlockGeneratorCount = 0;
            var playerCell = FindAuthoringPlayerCell(authoring);
            var tileFeatures = authoring.TileFeatures;
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.TileId <= 0)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.id-non-positive",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] must use a positive TileId.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (!tileIds.Add(tileFeature.TileId))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.id-duplicate",
                        $"StageAuthoringDefinition '{authoring.name}' contains duplicate TileFeature TileId {tileFeature.TileId}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (boardBoundsValid && !boardBounds.Contains(tileFeature.Cell.PlanarPosition))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.surface-cell.invalid",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] is outside board bounds at {tileFeature.Cell}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Unknown ||
                    !Enum.IsDefined(typeof(TileFeatureKind), tileFeature.Kind))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.kind-invalid",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] must use a known TileFeatureKind.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (!Enum.IsDefined(typeof(TileFeatureActivationRule), tileFeature.ActivationRule))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.activation-invalid",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] has invalid activation rule value {(int)tileFeature.ActivationRule}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Destroy &&
                    !TileFeatureActivationQueries.IsSupportedDestroyActivation(tileFeature.ActivationRule))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.destroy-activation-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] DestroyTile must use a FaceOnly activation rule.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Slide &&
                    tileFeature.ActivationRule != TileFeatureActivationRule.FrontFaceOnly)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.slide-activation-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] SlideTile must use FrontFaceOnly activation.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Barricade &&
                    tileFeature.ActivationRule != TileFeatureActivationRule.FrontFaceOnly)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.barricade-activation-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Barricade must use FrontFaceOnly activation.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Exit &&
                    tileFeature.ActivationRule != TileFeatureActivationRule.ActiveFaceOnly)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.exit-activation-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Exit must use ActiveFaceOnly activation.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Entrance &&
                    tileFeature.ActivationRule != TileFeatureActivationRule.BottomFaceOnly)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.entrance-activation-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Entrance must use BottomFaceOnly activation.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.MoonBlockGenerator &&
                    tileFeature.ActivationRule != TileFeatureActivationRule.BottomFaceOnly)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.moon-block-generator-activation-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] MoonBlockGenerator must use BottomFaceOnly activation.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (!Enum.IsDefined(typeof(Direction2D), tileFeature.Direction))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.direction-invalid",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] has invalid Direction2D value {(int)tileFeature.Direction}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Barricade &&
                         tileFeature.Direction != Direction2D.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.barricade-direction-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Barricade must use Direction2D.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Slide &&
                         !IsCardinalDirection(tileFeature.Direction))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.slide-direction-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] SlideTile must use a cardinal Direction2D.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Exit &&
                         tileFeature.Direction != Direction2D.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.exit-direction-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Exit must use Direction2D.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Entrance &&
                         tileFeature.Direction != Direction2D.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.entrance-direction-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Entrance must use Direction2D.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.MoonBlockGenerator &&
                         tileFeature.Direction != Direction2D.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.moon-block-generator-direction-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] MoonBlockGenerator must use Direction2D.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (!Enum.IsDefined(typeof(TileFeatureBoxSelector), tileFeature.BoxSelector))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.box-selector-invalid",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] has invalid TileFeatureBoxSelector value {(int)tileFeature.BoxSelector}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Barricade &&
                         tileFeature.BoxSelector != TileFeatureBoxSelector.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.barricade-box-selector-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Barricade must use TileFeatureBoxSelector.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Slide &&
                         tileFeature.BoxSelector != TileFeatureBoxSelector.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.slide-box-selector-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] SlideTile must use TileFeatureBoxSelector.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Exit &&
                         tileFeature.BoxSelector != TileFeatureBoxSelector.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.exit-box-selector-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Exit must use TileFeatureBoxSelector.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.Entrance &&
                         tileFeature.BoxSelector != TileFeatureBoxSelector.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.entrance-box-selector-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Entrance must use TileFeatureBoxSelector.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
                else if (tileFeature.Kind == TileFeatureKind.MoonBlockGenerator &&
                         tileFeature.BoxSelector != TileFeatureBoxSelector.None)
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.moon-block-generator-box-selector-unsupported",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] MoonBlockGenerator must use TileFeatureBoxSelector.None.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Slide &&
                    !slideCells.Add(tileFeature.Cell))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.slide-cell-duplicate",
                        $"StageAuthoringDefinition '{authoring.name}' contains duplicate SlideTile at {tileFeature.Cell}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Barricade &&
                    !barricadeCells.Add(tileFeature.Cell))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.barricade-cell-duplicate",
                        $"StageAuthoringDefinition '{authoring.name}' contains duplicate Barricade at {tileFeature.Cell}.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }

                if (tileFeature.Kind == TileFeatureKind.Exit)
                {
                    exitCount++;
                    if (exitCount > 1)
                    {
                        report.Add(
                            severity,
                            "authoring.tile-feature.exit-duplicate",
                            $"StageAuthoringDefinition '{authoring.name}' contains more than one Exit TileFeature.",
                            authoring,
                            authoringPath,
                            options.Timing);
                    }
                }

                if (tileFeature.Kind == TileFeatureKind.Entrance)
                {
                    entranceCount++;
                    if (entranceCount > 1)
                    {
                        report.Add(
                            severity,
                            "authoring.tile-feature.entrance-duplicate",
                            $"StageAuthoringDefinition '{authoring.name}' contains more than one Entrance TileFeature.",
                            authoring,
                            authoringPath,
                            options.Timing);
                    }

                    if (playerCell.HasValue &&
                        !tileFeature.Cell.Equals(playerCell.Value))
                    {
                        report.Add(
                            severity,
                            "authoring.tile-feature.entrance-player-cell-mismatch",
                            $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Entrance cell must match the player placement cell {playerCell.Value}.",
                            authoring,
                            authoringPath,
                            options.Timing);
                    }

                    if (tileFeature.BoundEntityId != 0)
                    {
                        report.Add(
                            severity,
                            "authoring.tile-feature.entrance-bound-id-unsupported",
                            $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] Entrance must use BoundEntityId 0.",
                            authoring,
                            authoringPath,
                            options.Timing);
                    }
                }

                if (tileFeature.Kind == TileFeatureKind.MoonBlockGenerator)
                {
                    if (!moonBlockGeneratorCells.Add(tileFeature.Cell))
                    {
                        report.Add(
                            severity,
                            "authoring.tile-feature.moon-block-generator-cell-duplicate",
                            $"StageAuthoringDefinition '{authoring.name}' contains duplicate MoonBlockGenerator at {tileFeature.Cell}.",
                            authoring,
                            authoringPath,
                            options.Timing);
                    }

                    moonBlockGeneratorCount++;
                    if (moonBlockGeneratorCount > 1)
                    {
                        report.Add(
                            severity,
                            "authoring.tile-feature.moon-block-generator-duplicate",
                            $"StageAuthoringDefinition '{authoring.name}' contains more than one MoonBlockGenerator TileFeature.",
                            authoring,
                            authoringPath,
                            options.Timing);
                    }

                    if (tileFeature.BoundEntityId <= 0)
                    {
                        report.Add(
                            severity,
                            "authoring.tile-feature.moon-block-generator-bound-id-non-positive",
                            $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] MoonBlockGenerator must bind a positive BoundEntityId.",
                            authoring,
                            authoringPath,
                            options.Timing);
                    }
                }

                if (wallCells.Contains(tileFeature.Cell))
                {
                    report.Add(
                        severity,
                        "authoring.tile-feature.wall-overlap",
                        $"StageAuthoringDefinition '{authoring.name}' tileFeature[{i}] overlaps a wall-like solid occupant.",
                        authoring,
                        authoringPath,
                        options.Timing);
                }
            }
        }

        private static SurfaceCell? FindAuthoringPlayerCell(StageAuthoringDefinition authoring)
        {
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement != null &&
                    placement.Kind == StageAuthoringEntityKind.Player)
                {
                    return placement.Cell;
                }
            }

            return null;
        }

        private static bool IsCardinalDirection(Direction2D direction)
        {
            return direction == Direction2D.Up ||
                   direction == Direction2D.Right ||
                   direction == Direction2D.Down ||
                   direction == Direction2D.Left;
        }

        private static void ValidateAuthoringGeneratedSync(
            StageContentEntry entry,
            StageAuthoringDefinition authoring,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var severity = ResolveAuthoringSyncSeverity(authoring);
            var authoringPath = GetAssetPath(authoring, options);
            var allocationPlan = StageAuthoringProjection.BuildAllocationPlan(authoring);
            var expectedGameplay = StageAuthoringProjection.ProjectExpectedGameplay(authoring, allocationPlan);
            var actualGameplay = StageAuthoringProjection.ProjectActualGameplay(entry.GameplayDefinition);
            var expectedPresentation = StageAuthoringProjection.ProjectExpectedPresentation(authoring, allocationPlan);
            var actualPresentation = StageAuthoringProjection.ProjectActualPresentation(entry.PresentationDefinition);

            var gameplayContext = new StageAuthoringDriftContext(
                severity,
                options.Timing,
                authoring,
                authoringPath,
                entry.StageId.IsValid ? entry.StageId.Value : string.Empty,
                authoring.name,
                entry.GameplayDefinition != null ? entry.GameplayDefinition.name : string.Empty);
            report.AddRange(StageAuthoringDriftComparer.CompareGameplay(expectedGameplay, actualGameplay, gameplayContext));

            var presentationContext = new StageAuthoringDriftContext(
                severity,
                options.Timing,
                authoring,
                authoringPath,
                entry.StageId.IsValid ? entry.StageId.Value : string.Empty,
                authoring.name,
                entry.PresentationDefinition != null ? entry.PresentationDefinition.name : string.Empty);
            report.AddRange(StageAuthoringDriftComparer.ComparePresentation(expectedPresentation, actualPresentation, presentationContext));

            for (var i = 0; i < allocationPlan.NewMappings.Count; i++)
            {
                var mapping = allocationPlan.NewMappings[i];
                report.Add(
                    severity,
                    "authoring.entity-id-drift",
                    $"StageAuthoringDefinition '{authoring.name}' placement '{mapping.StableGuid}' has no persisted positive EntityId mapping.",
                    authoring,
                    authoringPath,
                    options.Timing);
            }
        }

        private static StageValidationSeverity ResolveAuthoringSyncSeverity(StageAuthoringDefinition authoring)
        {
            return authoring != null && authoring.EnforceGeneratedSync
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void ValidateObjectiveDisplay(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (entry.GameplayDefinition == null ||
                !IsProductionCampaignStage(entry.StageId))
            {
                return;
            }

            var gameplayAssetPath = GetAssetPath(entry.GameplayDefinition, options);
            var objective = entry.GameplayDefinition.Objective;
            if (objective.CompletionPolicy == Game.Feature.Gameplay.Objectives.StageCompletionPolicy.Disabled)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "objective.production-disabled",
                    $"Production campaign stage '{entry.StageId.Value}' must author StageDefinition.Objective for objective UI.",
                    entry.GameplayDefinition,
                    gameplayAssetPath,
                    options.Timing);
                return;
            }

            if (string.IsNullOrWhiteSpace(objective.ObjectiveTitle))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "objective.title-missing",
                    $"Production campaign stage '{entry.StageId.Value}' objective is missing ObjectiveTitle.",
                    entry.GameplayDefinition,
                    gameplayAssetPath,
                    options.Timing);
            }

            if (string.IsNullOrWhiteSpace(objective.ObjectiveSummary))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "objective.summary-missing",
                    $"Production campaign stage '{entry.StageId.Value}' objective is missing ObjectiveSummary.",
                    entry.GameplayDefinition,
                    gameplayAssetPath,
                    options.Timing);
            }

            var conditionEntries = objective.GetConditionEntriesOrEmpty();
            for (var i = 0; i < conditionEntries.Length; i++)
            {
                var conditionEntry = conditionEntries[i];
                if (string.IsNullOrWhiteSpace(conditionEntry.StableConditionId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "objective.stable-id-missing",
                        $"Production campaign stage '{entry.StageId.Value}' objective condition entry[{i}] is missing StableConditionId.",
                        entry.GameplayDefinition,
                        gameplayAssetPath,
                        options.Timing);
                }

                if (string.IsNullOrWhiteSpace(conditionEntry.DisplayText))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "objective.display-text-missing",
                        $"Production campaign stage '{entry.StageId.Value}' objective condition entry[{i}] is missing user-facing DisplayText.",
                        entry.GameplayDefinition,
                        gameplayAssetPath,
                        options.Timing);
                }
            }
        }

        private static bool IsProductionCampaignStage(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                return false;
            }

            var stageIdValue = stageId.Value;
            for (var i = 0; i < CampaignStageSequenceDefinition.CanonicalStageIdValues.Length; i++)
            {
                if (string.Equals(
                        CampaignStageSequenceDefinition.CanonicalStageIdValues[i],
                        stageIdValue,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

            var gameplayAssetPath = GetAssetPath(entry.GameplayDefinition, options);
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
                    GetAssetPath(entry, options),
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
                    GetAssetPath(companion, options),
                    options.Timing);
            }
            else
            {
                ownerByCompanion[companion] = entry;
            }

            var companionPath = GetAssetPath(companion, options);
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

            var entryGuid = GetAssetGuid(entry, options);
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

        private static void ValidatePresentationCatalogIntegrity(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            report.AddRange(StageAuthoringPresentationCatalogValidator.ValidateEntry(entry, options));

            if (entry.PresentationDefinition != null)
            {
                ValidateBgmReference(entry.PresentationDefinition.BgmReference, entry.PresentationDefinition, options, report);
                ValidateBoardTilePresentationCatalog(entry, options, report);
                ValidateBoardTilePresentationOverrides(entry, options, report);
                ValidateBoardTileStyleCatalog(entry, options, report);
                ValidateBoardTilePaintOverrides(entry, options, report);
                ValidateTileFeaturePresentationCatalog(entry, options, report);
                ValidateTileFeaturePresentationBindings(entry, options, report);
            }
        }

        private static void ValidateBoardTilePresentationCatalog(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var presentation = entry.PresentationDefinition;
            var catalog = presentation != null ? presentation.BoardTilePresentationCatalog : null;
            if (catalog == null)
            {
                return;
            }

            var catalogPath = GetAssetPath(catalog, options);
            var entries = catalog.Entries;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var defaultRoles = new HashSet<BoardTileVisualRole>();
            for (var i = 0; i < entries.Count; i++)
            {
                var catalogEntry = entries[i];
                var fieldPrefix = $"BoardTilePresentationCatalog.Entries[{i}]";
                if (catalogEntry == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.catalog.entry-null",
                        $"BoardTilePresentationCatalog '{catalog.name}' entry[{i}] is null.",
                        catalog,
                        catalogPath,
                        options.Timing);
                    continue;
                }

                var presentationKey = catalogEntry.PresentationKey;
                if (string.IsNullOrEmpty(presentationKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.catalog.key-empty",
                        $"BoardTilePresentationCatalog '{catalog.name}' {fieldPrefix} must declare a non-empty PresentationKey.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }
                else if (!seenKeys.Add(presentationKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.catalog.key-duplicate",
                        $"BoardTilePresentationCatalog '{catalog.name}' contains duplicate PresentationKey '{presentationKey}'.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (!Enum.IsDefined(typeof(BoardTileVisualRole), catalogEntry.Role))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.catalog.role-invalid",
                        $"BoardTilePresentationCatalog '{catalog.name}' {fieldPrefix} has invalid BoardTileVisualRole value {(int)catalogEntry.Role}.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (catalogEntry.IsDefaultForRole &&
                    !defaultRoles.Add(catalogEntry.Role))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.catalog.default-duplicate",
                        $"BoardTilePresentationCatalog '{catalog.name}' has multiple default entries for {catalogEntry.Role}.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (catalogEntry.TilePrefab == null &&
                    catalogEntry.MaterialFallback == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.catalog.visual-missing",
                        $"BoardTilePresentationCatalog '{catalog.name}' {fieldPrefix} must assign a tile prefab or material fallback.",
                        catalog,
                        catalogPath,
                        options.Timing);
                    continue;
                }

                if (catalogEntry.TilePrefab != null &&
                    catalogEntry.TilePrefab.GetComponentInChildren<Renderer>(includeInactive: true) == null)
                {
                    report.Add(
                        StageValidationSeverity.Warning,
                        "presentation.board-tile.catalog.prefab-renderer-missing",
                        $"BoardTilePresentationCatalog '{catalog.name}' {fieldPrefix} prefab '{catalogEntry.TilePrefab.name}' has no Renderer in self or children.",
                        catalogEntry.TilePrefab,
                        GetAssetPath(catalogEntry.TilePrefab, options),
                        options.Timing);
                }
            }

            if (!catalog.TryGetDefaultEntry(BoardTileVisualRole.ActiveBottom, out _))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "presentation.board-tile.catalog.default-active-bottom-missing",
                    $"BoardTilePresentationCatalog '{catalog.name}' has no default entry for {BoardTileVisualRole.ActiveBottom}.",
                    catalog,
                    catalogPath,
                    options.Timing);
            }

            if (!catalog.TryGetDefaultEntry(BoardTileVisualRole.ActiveFront, out _))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "presentation.board-tile.catalog.default-active-front-missing",
                    $"BoardTilePresentationCatalog '{catalog.name}' has no default entry for {BoardTileVisualRole.ActiveFront}.",
                    catalog,
                    catalogPath,
                    options.Timing);
            }
        }

        private static void ValidateBoardTilePresentationOverrides(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var presentation = entry.PresentationDefinition;
            if (presentation == null)
            {
                return;
            }

            var overrides = presentation.BoardTilePresentationOverrides;
            if (overrides.Count == 0)
            {
                return;
            }

            var catalog = presentation.BoardTilePresentationCatalog;
            var presentationPath = GetAssetPath(presentation, options);
            var boardBoundsValid = TryGetBoardBounds(entry.GameplayDefinition, out var boardBounds);
            var cells = new HashSet<SurfaceCell>();
            for (var i = 0; i < overrides.Count; i++)
            {
                var boardOverride = overrides[i];
                var fieldPrefix = $"BoardTilePresentationOverrides[{i}]";
                if (boardOverride == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.override-null",
                        $"StagePresentationDefinition '{presentation.name}' board tile presentation override[{i}] is null.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                var cell = boardOverride.Cell;
                if (!Enum.IsDefined(typeof(FaceId), cell.face))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.override-cell-face-invalid",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} has invalid SurfaceCell face value {(int)cell.face}.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                if (boardBoundsValid && !boardBounds.Contains(cell.PlanarPosition))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.override-cell-outside-bounds",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} cell {cell} is outside board bounds.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                if (!cells.Add(cell))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.override-cell-duplicate",
                        $"StagePresentationDefinition '{presentation.name}' contains duplicate board tile presentation override for cell {cell}.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                var presentationKey = boardOverride.PresentationKey;
                if (string.IsNullOrEmpty(presentationKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.override-key-empty",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} must declare a non-empty PresentationKey.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (catalog == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.override-catalog-missing",
                        $"StagePresentationDefinition '{presentation.name}' has board tile override key '{presentationKey}' but no BoardTilePresentationCatalog.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (!catalog.TryGetEntry(presentationKey, out var catalogEntry))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile.override-key-missing",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} PresentationKey '{presentationKey}' is missing from BoardTilePresentationCatalog '{catalog.name}'.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (catalogEntry.TilePrefab == null &&
                    catalogEntry.MaterialFallback != null)
                {
                    report.Add(
                        StageValidationSeverity.Warning,
                        "presentation.board-tile.override-material-only",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} PresentationKey '{presentationKey}' resolves to a material-only board tile entry.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }
            }
        }

        private static void ValidateBoardTileStyleCatalog(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var presentation = entry.PresentationDefinition;
            var catalog = presentation != null ? presentation.BoardTileStyleCatalog : null;
            if (catalog == null)
            {
                return;
            }

            var catalogPath = GetAssetPath(catalog, options);
            var entries = catalog.Entries;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var catalogEntry = entries[i];
                var fieldPrefix = $"BoardTileStyleCatalog.Entries[{i}]";
                if (catalogEntry == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-style.catalog.entry-null",
                        $"BoardTileStyleCatalog '{catalog.name}' entry[{i}] is null.",
                        catalog,
                        catalogPath,
                        options.Timing);
                    continue;
                }

                var styleKey = catalogEntry.StyleKey;
                if (string.IsNullOrEmpty(styleKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-style.catalog.key-empty",
                        $"BoardTileStyleCatalog '{catalog.name}' {fieldPrefix} must declare a non-empty StyleKey.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }
                else if (!seenKeys.Add(styleKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-style.catalog.key-duplicate",
                        $"BoardTileStyleCatalog '{catalog.name}' contains duplicate StyleKey '{styleKey}'.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }
            }
        }

        private static void ValidateBoardTilePaintOverrides(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var presentation = entry.PresentationDefinition;
            if (presentation == null)
            {
                return;
            }

            var overrides = presentation.BoardTilePaintOverrides;
            if (overrides.Count == 0)
            {
                return;
            }

            var catalog = presentation.BoardTileStyleCatalog;
            var presentationPath = GetAssetPath(presentation, options);
            var boardBoundsValid = TryGetBoardBounds(entry.GameplayDefinition, out var boardBounds);
            var cells = new HashSet<SurfaceCell>();
            for (var i = 0; i < overrides.Count; i++)
            {
                var paintOverride = overrides[i];
                var fieldPrefix = $"BoardTilePaintOverrides[{i}]";
                if (paintOverride == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-paint.override-null",
                        $"StagePresentationDefinition '{presentation.name}' board tile paint override[{i}] is null.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                var cell = paintOverride.Cell;
                if (!Enum.IsDefined(typeof(FaceId), cell.face))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-paint.override-cell-face-invalid",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} has invalid SurfaceCell face value {(int)cell.face}.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                if (boardBoundsValid && !boardBounds.Contains(cell.PlanarPosition))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-paint.override-cell-outside-bounds",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} cell {cell} is outside board bounds.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                if (!cells.Add(cell))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-paint.override-cell-duplicate",
                        $"StagePresentationDefinition '{presentation.name}' contains duplicate board tile paint override for cell {cell}.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                var styleKey = paintOverride.StyleKey;
                if (string.IsNullOrEmpty(styleKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-paint.override-key-empty",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} must declare a non-empty StyleKey.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (catalog == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-paint.override-catalog-missing",
                        $"StagePresentationDefinition '{presentation.name}' has board tile paint override key '{styleKey}' but no BoardTileStyleCatalog.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (!catalog.TryGetEntry(styleKey, out _))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.board-tile-paint.override-key-missing",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} StyleKey '{styleKey}' is missing from BoardTileStyleCatalog '{catalog.name}'.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }
            }
        }

        private static void ValidateTileFeaturePresentationCatalog(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var presentation = entry.PresentationDefinition;
            var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
            if (presentation == null ||
                catalog == null)
            {
                ValidateTileFeatureCatalogUsage(entry, options, report);
                return;
            }

            var catalogPath = GetAssetPath(catalog, options);
            var entries = catalog.Entries;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var defaultKinds = new HashSet<TileFeatureKind>();
            for (var i = 0; i < entries.Count; i++)
            {
                var catalogEntry = entries[i];
                var fieldPrefix = $"TileFeaturePresentationCatalog.Entries[{i}]";
                if (catalogEntry == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.entry-null",
                        $"TileFeaturePresentationCatalog '{catalog.name}' entry[{i}] is null.",
                        catalog,
                        catalogPath,
                        options.Timing);
                    continue;
                }

                var presentationKey = catalogEntry.PresentationKey;
                if (string.IsNullOrEmpty(presentationKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.key-empty",
                        $"TileFeaturePresentationCatalog '{catalog.name}' {fieldPrefix} must declare a non-empty PresentationKey.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }
                else if (!seenKeys.Add(presentationKey))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.key-duplicate",
                        $"TileFeaturePresentationCatalog '{catalog.name}' contains duplicate PresentationKey '{presentationKey}'.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (catalogEntry.Kind == TileFeatureKind.Unknown ||
                    !Enum.IsDefined(typeof(TileFeatureKind), catalogEntry.Kind))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.kind-invalid",
                        $"TileFeaturePresentationCatalog '{catalog.name}' {fieldPrefix} must use a known TileFeatureKind.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (!Enum.IsDefined(typeof(TileFeatureVisualPlacementMode), catalogEntry.PlacementMode))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.placement-mode-invalid",
                        $"TileFeaturePresentationCatalog '{catalog.name}' {fieldPrefix} has invalid TileFeatureVisualPlacementMode value {(int)catalogEntry.PlacementMode}.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (!Enum.IsDefined(typeof(TileFeatureVisualFootprintMode), catalogEntry.FootprintMode))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.footprint-mode-invalid",
                        $"TileFeaturePresentationCatalog '{catalog.name}' {fieldPrefix} has invalid TileFeatureVisualFootprintMode value {(int)catalogEntry.FootprintMode}.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (catalogEntry.IsDefaultForKind &&
                    !defaultKinds.Add(catalogEntry.Kind))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.default-duplicate",
                        $"TileFeaturePresentationCatalog '{catalog.name}' has multiple default entries for {catalogEntry.Kind}.",
                        catalog,
                        catalogPath,
                        options.Timing);
                }

                if (catalogEntry.VisualPrefab == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.prefab-null",
                        $"TileFeaturePresentationCatalog '{catalog.name}' {fieldPrefix} must assign a visual prefab.",
                        catalog,
                        catalogPath,
                        options.Timing);
                    continue;
                }

                if (!PrefabHasConfigurableTileFeatureVisualTarget(catalogEntry.VisualPrefab))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.catalog.prefab-target-missing",
                        $"TileFeaturePresentationCatalog '{catalog.name}' {fieldPrefix} prefab '{catalogEntry.VisualPrefab.name}' must provide a configurable TileFeature visual target.",
                        catalogEntry.VisualPrefab,
                        GetAssetPath(catalogEntry.VisualPrefab, options),
                        options.Timing);
                }
            }

            ValidateTileFeatureCatalogUsage(entry, options, report);
            ValidateReplaceBaseTilePolicy(entry, options, report);
        }

        private static void ValidateTileFeatureCatalogUsage(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (entry.GameplayDefinition == null ||
                entry.PresentationDefinition == null)
            {
                return;
            }

            var presentation = entry.PresentationDefinition;
            var catalog = presentation.TileFeaturePresentationCatalog;
            var presentationPath = GetAssetPath(presentation, options);
            var directTileIds = BuildDirectTileFeatureBindingIds(presentation);
            var tileFeatures = entry.GameplayDefinition.TileFeatures;
            for (var i = 0; i < tileFeatures.Length; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.TileId <= 0)
                {
                    continue;
                }

                var hasDirectOverride = directTileIds.Contains(tileFeature.TileId);
                var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(tileFeature.PresentationKey);
                if (catalog == null)
                {
                    if (!hasDirectOverride)
                    {
                        report.Add(
                            StageValidationSeverity.Warning,
                            "presentation.tile-feature.catalog.missing",
                            $"StagePresentationDefinition '{presentation.name}' has no TileFeaturePresentationCatalog for TileId {tileFeature.TileId}; visual will be skipped unless a direct override is added.",
                            presentation,
                            presentationPath,
                            options.Timing);
                    }

                    continue;
                }

                if (string.IsNullOrEmpty(presentationKey))
                {
                    if (!hasDirectOverride &&
                        !catalog.TryGetDefaultEntry(tileFeature.Kind, out _))
                    {
                        report.Add(
                            StageValidationSeverity.Warning,
                            "presentation.tile-feature.key-empty-no-default",
                            $"TileFeature TileId {tileFeature.TileId} has no PresentationKey and no default catalog entry for {tileFeature.Kind}.",
                            presentation,
                            presentationPath,
                            options.Timing);
                    }

                    continue;
                }

                var hasCatalogEntry = catalog.TryGetEntry(presentationKey, out var catalogEntry);
                if (!hasCatalogEntry)
                {
                    if (!hasDirectOverride)
                    {
                        report.Add(
                            StageValidationSeverity.Warning,
                            "presentation.tile-feature.key-missing",
                            $"TileFeature TileId {tileFeature.TileId} PresentationKey '{presentationKey}' is missing from TileFeaturePresentationCatalog '{catalog.name}'.",
                            presentation,
                            presentationPath,
                            options.Timing);
                    }

                    continue;
                }

                if (hasDirectOverride)
                {
                    report.Add(
                        StageValidationSeverity.Info,
                        "presentation.tile-feature.direct-override-active",
                        $"TileFeature TileId {tileFeature.TileId} has a direct visual override; catalog PresentationKey '{presentationKey}' is bypassed at runtime.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                if (catalogEntry.Kind != tileFeature.Kind)
                {
                    report.Add(
                        StageValidationSeverity.Warning,
                        "presentation.tile-feature.catalog-kind-mismatch",
                        $"TileFeature TileId {tileFeature.TileId} kind {tileFeature.Kind} does not match catalog PresentationKey '{presentationKey}' kind {catalogEntry.Kind}.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }

                if (catalogEntry.DirectionHint != Direction2D.None &&
                    tileFeature.Kind == TileFeatureKind.Slide &&
                    catalogEntry.DirectionHint != tileFeature.Direction)
                {
                    report.Add(
                        StageValidationSeverity.Warning,
                        "presentation.tile-feature.catalog-direction-mismatch",
                        $"TileFeature TileId {tileFeature.TileId} Slide direction {tileFeature.Direction} does not match catalog PresentationKey '{presentationKey}' hint {catalogEntry.DirectionHint}.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }
            }
        }

        private static HashSet<int> BuildDirectTileFeatureBindingIds(StagePresentationDefinition presentation)
        {
            var tileIds = new HashSet<int>();
            if (presentation == null)
            {
                return tileIds;
            }

            var bindings = presentation.TileFeaturePresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding != null &&
                    binding.TileId > 0)
                {
                    tileIds.Add(binding.TileId);
                }
            }

            return tileIds;
        }

        private static void ValidateReplaceBaseTilePolicy(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (entry.GameplayDefinition == null ||
                entry.PresentationDefinition == null ||
                entry.PresentationDefinition.TileFeaturePresentationCatalog == null)
            {
                return;
            }

            var presentation = entry.PresentationDefinition;
            var presentationPath = GetAssetPath(presentation, options);
            var directBindings = BuildDirectTileFeatureBindingsById(presentation);
            var replaceTileIdsByCell = new Dictionary<SurfaceCell, List<int>>();
            var hasBoardBounds = TryGetBoardBounds(entry.GameplayDefinition, out var boardBounds);
            var tileFeatures = entry.GameplayDefinition.TileFeatures;
            for (var i = 0; i < tileFeatures.Length; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.TileId <= 0)
                {
                    continue;
                }

                if (!TryResolveEffectiveTileFeatureVisual(
                        presentation.TileFeaturePresentationCatalog,
                        directBindings,
                        tileFeature,
                        out var placementMode,
                        out var footprintMode,
                        out var visualPrefab))
                {
                    continue;
                }

                if (placementMode != TileFeatureVisualPlacementMode.ReplaceBaseTile)
                {
                    continue;
                }

                if (visualPrefab == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.replace-base-tile.visual-unresolved",
                        $"TileFeature TileId {tileFeature.TileId} resolves to ReplaceBaseTile but has no resolved visual prefab.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                IReadOnlyList<SurfaceCell> suppressedCells = Array.Empty<SurfaceCell>();
                if (hasBoardBounds &&
                    !TryBuildReplaceBaseTileSuppressedCells(
                        tileFeature,
                        footprintMode,
                        boardBounds,
                        out suppressedCells,
                        out var invalidCell))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.replace-base-tile.footprint-out-of-bounds",
                        $"TileFeature TileId {tileFeature.TileId} ReplaceBaseTile footprint {footprintMode} includes out-of-bounds cell {invalidCell}.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (!hasBoardBounds)
                {
                    suppressedCells = new[] { tileFeature.Cell };
                }

                for (var cellIndex = 0; cellIndex < suppressedCells.Count; cellIndex++)
                {
                    var suppressedCell = suppressedCells[cellIndex];
                    if (!replaceTileIdsByCell.TryGetValue(suppressedCell, out var tileIds))
                    {
                        tileIds = new List<int>();
                        replaceTileIdsByCell.Add(suppressedCell, tileIds);
                    }

                    tileIds.Add(tileFeature.TileId);
                }
            }

            foreach (var pair in replaceTileIdsByCell)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                report.Add(
                    StageValidationSeverity.Error,
                    "presentation.tile-feature.replace-base-tile.cell-duplicate",
                    $"SurfaceCell {pair.Key} is suppressed by multiple ReplaceBaseTile TileFeatures: {string.Join(", ", pair.Value)}.",
                    presentation,
                    presentationPath,
                    options.Timing);
            }
        }

        private static bool TryBuildReplaceBaseTileSuppressedCells(
            StageTileFeatureDefinition tileFeature,
            TileFeatureVisualFootprintMode footprintMode,
            BoardBounds boardBounds,
            out IReadOnlyList<SurfaceCell> cells,
            out SurfaceCell invalidCell)
        {
            var result = new List<SurfaceCell>();
            invalidCell = default;
            switch (footprintMode)
            {
                case TileFeatureVisualFootprintMode.ThreeByThreeSameFace:
                    for (var yOffset = -1; yOffset <= 1; yOffset++)
                    {
                        for (var xOffset = -1; xOffset <= 1; xOffset++)
                        {
                            var candidate = new SurfaceCell(
                                tileFeature.Cell.face,
                                tileFeature.Cell.x + xOffset,
                                tileFeature.Cell.y + yOffset);
                            if (!boardBounds.Contains(candidate.PlanarPosition))
                            {
                                cells = Array.Empty<SurfaceCell>();
                                invalidCell = candidate;
                                return false;
                            }

                            result.Add(candidate);
                        }
                    }

                    cells = result;
                    return true;
                case TileFeatureVisualFootprintMode.SingleCell:
                default:
                    if (!boardBounds.Contains(tileFeature.Cell.PlanarPosition))
                    {
                        cells = Array.Empty<SurfaceCell>();
                        invalidCell = tileFeature.Cell;
                        return false;
                    }

                    result.Add(tileFeature.Cell);
                    cells = result;
                    return true;
            }
        }

        private static Dictionary<int, TileFeaturePresentationBinding> BuildDirectTileFeatureBindingsById(
            StagePresentationDefinition presentation)
        {
            var bindingsById = new Dictionary<int, TileFeaturePresentationBinding>();
            if (presentation == null)
            {
                return bindingsById;
            }

            var bindings = presentation.TileFeaturePresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null ||
                    binding.TileId <= 0 ||
                    bindingsById.ContainsKey(binding.TileId))
                {
                    continue;
                }

                bindingsById.Add(binding.TileId, binding);
            }

            return bindingsById;
        }

        private static bool TryResolveEffectiveTileFeatureVisual(
            TileFeaturePresentationCatalog catalog,
            IReadOnlyDictionary<int, TileFeaturePresentationBinding> directBindings,
            StageTileFeatureDefinition tileFeature,
            out TileFeatureVisualPlacementMode placementMode,
            out TileFeatureVisualFootprintMode footprintMode,
            out GameObject visualPrefab)
        {
            placementMode = TileFeatureVisualPlacementMode.Overlay;
            footprintMode = TileFeatureVisualFootprintMode.SingleCell;
            visualPrefab = null;
            if (directBindings != null &&
                directBindings.TryGetValue(tileFeature.TileId, out var directBinding))
            {
                visualPrefab = directBinding.VisualPrefab;
                var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(tileFeature.PresentationKey);
                if (catalog != null &&
                    !string.IsNullOrEmpty(presentationKey) &&
                    catalog.TryGetEntry(presentationKey, out var keyedEntry))
                {
                    placementMode = keyedEntry.PlacementMode;
                    footprintMode = keyedEntry.FootprintMode;
                }

                return true;
            }

            if (catalog == null)
            {
                return false;
            }

            var key = TileFeaturePresentationCatalog.NormalizePresentationKey(tileFeature.PresentationKey);
            if (!string.IsNullOrEmpty(key) &&
                catalog.TryGetEntry(key, out var keyedCatalogEntry))
            {
                placementMode = keyedCatalogEntry.PlacementMode;
                footprintMode = keyedCatalogEntry.FootprintMode;
                visualPrefab = keyedCatalogEntry.VisualPrefab;
                return true;
            }

            if (catalog.TryGetDefaultEntry(tileFeature.Kind, out var defaultEntry))
            {
                placementMode = defaultEntry.PlacementMode;
                footprintMode = defaultEntry.FootprintMode;
                visualPrefab = defaultEntry.VisualPrefab;
                return true;
            }

            return false;
        }

        private static bool TryGetBoardBounds(StageDefinition gameplayDefinition, out BoardBounds boardBounds)
        {
            if (gameplayDefinition == null)
            {
                boardBounds = default;
                return false;
            }

            var board = gameplayDefinition.Board;
            if (board.MaxInclusive.x < board.MinInclusive.x ||
                board.MaxInclusive.y < board.MinInclusive.y)
            {
                boardBounds = default;
                return false;
            }

            boardBounds = new BoardBounds(board.MinInclusive, board.MaxInclusive);
            return true;
        }

        private static void ValidateTileFeaturePresentationBindings(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            var presentation = entry.PresentationDefinition;
            if (presentation == null)
            {
                return;
            }

            var presentationPath = GetAssetPath(presentation, options);
            var bindings = presentation.TileFeaturePresentationBindings;
            if (bindings.Length == 0)
            {
                return;
            }

            var gameplayTileFeatureIds = BuildGameplayTileFeatureIds(entry.GameplayDefinition);
            var boundTileIds = new HashSet<int>();
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                var fieldPrefix = $"TileFeaturePresentationBindings[{i}]";
                if (binding == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.binding-null",
                        $"StagePresentationDefinition '{presentation.name}' tile feature visual binding[{i}] is null.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (binding.TileId <= 0)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.tile-id-non-positive",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} must use a positive TileId.",
                        presentation,
                        presentationPath,
                        options.Timing);
                }
                else
                {
                    if (!boundTileIds.Add(binding.TileId))
                    {
                        report.Add(
                            StageValidationSeverity.Error,
                            "presentation.tile-feature.tile-id-duplicate",
                            $"StagePresentationDefinition '{presentation.name}' contains duplicate TileFeature visual binding for TileId {binding.TileId}.",
                            presentation,
                            presentationPath,
                            options.Timing);
                    }

                    if (!gameplayTileFeatureIds.Contains(binding.TileId))
                    {
                        report.Add(
                            StageValidationSeverity.Error,
                            "presentation.tile-feature.tile-id-missing",
                            $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} references missing StageDefinition TileFeature TileId {binding.TileId}.",
                            presentation,
                            presentationPath,
                            options.Timing);
                    }
                }

                if (binding.VisualPrefab == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.prefab-null",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} must assign a visual prefab.",
                        presentation,
                        presentationPath,
                        options.Timing);
                    continue;
                }

                if (!PrefabHasConfigurableTileFeatureVisualTarget(binding.VisualPrefab))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "presentation.tile-feature.prefab-target-missing",
                        $"StagePresentationDefinition '{presentation.name}' {fieldPrefix} prefab '{binding.VisualPrefab.name}' must provide a configurable TileFeature visual target.",
                        binding.VisualPrefab,
                        GetAssetPath(binding.VisualPrefab, options),
                        options.Timing);
                }
            }
        }

        private static HashSet<int> BuildGameplayTileFeatureIds(StageDefinition gameplayDefinition)
        {
            var tileIds = new HashSet<int>();
            if (gameplayDefinition == null)
            {
                return tileIds;
            }

            var tileFeatures = gameplayDefinition.TileFeatures;
            for (var i = 0; i < tileFeatures.Length; i++)
            {
                if (tileFeatures[i].TileId > 0)
                {
                    tileIds.Add(tileFeatures[i].TileId);
                }
            }

            return tileIds;
        }

        private static bool PrefabHasConfigurableTileFeatureVisualTarget(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            var behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                {
                    continue;
                }

                var behaviourType = behaviours[i].GetType();
                if (behaviourType.FullName == "Game.Feature.Gameplay.Host.TileFeatureVisualTargetView" ||
                    TypeImplements(behaviourType, "Game.Feature.Gameplay.Host.ITileFeatureVisualTarget") &&
                    TypeImplements(behaviourType, "Game.Feature.Gameplay.Host.ITileFeatureVisualTargetConfigurator"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TypeImplements(Type type, string interfaceFullName)
        {
            var interfaces = type.GetInterfaces();
            for (var i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].FullName == interfaceFullName)
                {
                    return true;
                }
            }

            return false;
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
                GetAssetPath(entry.GameplayDefinition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                            GetAssetPath(definition, options),
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
                            GetAssetPath(definition, options),
                            options.Timing);
                    }

                    if (!aliasedRuleIds.Add(normalizedDeprecatedId))
                    {
                        report.Add(
                            StageValidationSeverity.Error,
                            "reward.rule-id.alias-duplicate",
                            $"Deprecated reward rule id '{normalizedDeprecatedId}' is declared more than once.",
                            definition,
                            GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(definition, options),
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
                        GetAssetPath(aliasTable, options),
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
                        GetAssetPath(aliasTable, options),
                        options.Timing);
                }

                if (string.Equals(normalizedAlias, entry.CurrentStageId.Value, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.alias.self",
                        $"Alias '{normalizedAlias}' cannot point to itself.",
                        aliasTable,
                        GetAssetPath(aliasTable, options),
                        options.Timing);
                }

                if (!seenAliases.Add(normalizedAlias))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "stage-id.alias.duplicate",
                        $"Alias '{normalizedAlias}' is declared more than once.",
                        aliasTable,
                        GetAssetPath(aliasTable, options),
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
                        GetAssetPath(aliasTable, options),
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
                            GetAssetPath(progression, options),
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
                        GetAssetPath(entry.ProgressionDefinition, options),
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
                    GetAssetPath(context, options),
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
                    GetAssetPath(context, options),
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

        private static string GetAssetPath(UnityEngine.Object asset, StageCatalogValidationOptions options)
        {
            return (options?.ResolvedAssetMetadataProvider ?? NoOpMetadataProvider).GetAssetPath(asset);
        }

        private static string GetAssetGuid(UnityEngine.Object asset, StageCatalogValidationOptions options)
        {
            return (options?.ResolvedAssetMetadataProvider ?? NoOpMetadataProvider).GetAssetGuid(asset);
        }

        private sealed class NoOpAssetMetadataProvider : IStageValidationAssetMetadataProvider
        {
            public string GetAssetPath(UnityEngine.Object asset)
            {
                return string.Empty;
            }

            public string GetAssetGuid(UnityEngine.Object asset)
            {
                return string.Empty;
            }
        }

        private sealed class DefaultAssetMetadataProviderScope : IDisposable
        {
            private readonly IStageValidationAssetMetadataProvider previous;
            private bool disposed;

            public DefaultAssetMetadataProviderScope(IStageValidationAssetMetadataProvider previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                defaultMetadataProvider = previous;
                disposed = true;
            }
        }
    }
}
