using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Shared.AudioContracts;
using UnityEngine;

namespace Game.Feature.Stages
{
    public readonly struct TileFeaturePresentationResolvedBinding
    {
        public TileFeaturePresentationResolvedBinding(int tileId, GameObject visualPrefab)
            : this(
                tileId,
                visualPrefab,
                TileFeatureVisualPlacementMode.Overlay,
                TileFeatureVisualFootprintMode.SingleCell,
                VfxStyleKey.Default)
        {
        }

        public TileFeaturePresentationResolvedBinding(
            int tileId,
            GameObject visualPrefab,
            TileFeatureVisualPlacementMode placementMode)
            : this(tileId, visualPrefab, placementMode, TileFeatureVisualFootprintMode.SingleCell, VfxStyleKey.Default)
        {
        }

        public TileFeaturePresentationResolvedBinding(
            int tileId,
            GameObject visualPrefab,
            TileFeatureVisualPlacementMode placementMode,
            TileFeatureVisualFootprintMode footprintMode)
            : this(tileId, visualPrefab, placementMode, footprintMode, VfxStyleKey.Default)
        {
        }

        public TileFeaturePresentationResolvedBinding(
            int tileId,
            GameObject visualPrefab,
            TileFeatureVisualPlacementMode placementMode,
            TileFeatureVisualFootprintMode footprintMode,
            VfxStyleKey vfxStyleKey)
        {
            TileId = tileId;
            VisualPrefab = visualPrefab;
            PlacementMode = placementMode;
            FootprintMode = footprintMode;
            VfxStyleKey = vfxStyleKey;
        }

        public int TileId { get; }

        public GameObject VisualPrefab { get; }

        public TileFeatureVisualPlacementMode PlacementMode { get; }

        public TileFeatureVisualFootprintMode FootprintMode { get; }

        public VfxStyleKey VfxStyleKey { get; }
    }

    public sealed class StagePresentationResolvedData
    {
        public StagePresentationResolvedData(
            string displayName,
            string summaryText,
            Sprite previewSprite,
            GameObject backgroundPrefab,
            StageBgmReference bgmReference,
            EnemyPresentationCatalog enemyPresentationCatalog,
            EnemyPresentationArchetypeCatalog enemyPresentationArchetypeCatalog,
            EnemyPresentationBinding[] enemyPresentationBindings,
            StaticEntityPresentationCatalog staticEntityPresentationCatalog,
            StaticEntityPresentationBinding[] staticEntityPresentationBindings,
            BoardTilePresentationCatalog boardTilePresentationCatalog,
            IReadOnlyList<BoardTilePresentationOverride> boardTilePresentationOverrides,
            TileFeaturePresentationCatalog tileFeaturePresentationCatalog,
            IReadOnlyList<TileFeaturePresentationResolvedBinding> tileFeatureBindings,
            string resultTitle,
            string resultSummaryText,
            string resultDetailText,
            string resultContinueLabel,
            IReadOnlyList<SurfaceCell> suppressedBaseTileCells = null)
        {
            DisplayName = displayName ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            PreviewSprite = previewSprite;
            BackgroundPrefab = backgroundPrefab;
            BgmReference = bgmReference;
            EnemyPresentationCatalog = enemyPresentationCatalog;
            EnemyPresentationArchetypeCatalog = enemyPresentationArchetypeCatalog;
            EnemyPresentationBindings = enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
            StaticEntityPresentationCatalog = staticEntityPresentationCatalog;
            StaticEntityPresentationBindings = staticEntityPresentationBindings ?? Array.Empty<StaticEntityPresentationBinding>();
            BoardTilePresentationCatalog = boardTilePresentationCatalog;
            BoardTilePresentationOverrides = CloneReadOnlyBoardTileOverrides(boardTilePresentationOverrides);
            TileFeaturePresentationCatalog = tileFeaturePresentationCatalog;
            TileFeatureBindings = CloneReadOnlyBindings(tileFeatureBindings);
            SuppressedBaseTileCells = CloneReadOnlySurfaceCells(suppressedBaseTileCells);
            ResultTitle = resultTitle ?? string.Empty;
            ResultSummaryText = resultSummaryText ?? string.Empty;
            ResultDetailText = resultDetailText ?? string.Empty;
            ResultContinueLabel = resultContinueLabel ?? string.Empty;
        }

        public string DisplayName { get; }

        public string SummaryText { get; }

        public Sprite PreviewSprite { get; }

        public GameObject BackgroundPrefab { get; }

        public StageBgmReference BgmReference { get; }

        public EnemyPresentationCatalog EnemyPresentationCatalog { get; }

        public EnemyPresentationArchetypeCatalog EnemyPresentationArchetypeCatalog { get; }

        public EnemyPresentationBinding[] EnemyPresentationBindings { get; }

        public StaticEntityPresentationCatalog StaticEntityPresentationCatalog { get; }

        public StaticEntityPresentationBinding[] StaticEntityPresentationBindings { get; }

        public BoardTilePresentationCatalog BoardTilePresentationCatalog { get; }

        public IReadOnlyList<BoardTilePresentationOverride> BoardTilePresentationOverrides { get; }

        public TileFeaturePresentationCatalog TileFeaturePresentationCatalog { get; }

        public IReadOnlyList<TileFeaturePresentationResolvedBinding> TileFeatureBindings { get; }

        public IReadOnlyList<SurfaceCell> SuppressedBaseTileCells { get; }

        public string ResultTitle { get; }

        public string ResultSummaryText { get; }

        public string ResultDetailText { get; }

        public string ResultContinueLabel { get; }

        private static IReadOnlyList<TileFeaturePresentationResolvedBinding> CloneReadOnlyBindings(
            IReadOnlyList<TileFeaturePresentationResolvedBinding> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TileFeaturePresentationResolvedBinding>();
            }

            var bindings = new TileFeaturePresentationResolvedBinding[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                bindings[i] = source[i];
            }

            return new ReadOnlyCollection<TileFeaturePresentationResolvedBinding>(bindings);
        }

        private static IReadOnlyList<BoardTilePresentationOverride> CloneReadOnlyBoardTileOverrides(
            IReadOnlyList<BoardTilePresentationOverride> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BoardTilePresentationOverride>();
            }

            var overrides = new BoardTilePresentationOverride[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                overrides[i] = entry == null
                    ? null
                    : new BoardTilePresentationOverride(entry.Cell, entry.PresentationKey);
            }

            return new ReadOnlyCollection<BoardTilePresentationOverride>(overrides);
        }

        private static IReadOnlyList<SurfaceCell> CloneReadOnlySurfaceCells(
            IReadOnlyList<SurfaceCell> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<SurfaceCell>();
            }

            var cells = new SurfaceCell[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                cells[i] = source[i];
            }

            return new ReadOnlyCollection<SurfaceCell>(cells);
        }
    }

    public sealed class StageSceneCompositionData
    {
        public StageSceneCompositionData(
            StageRuntimeBuildResult gameplayBuildResult,
            StagePresentationResolvedData presentationData)
        {
            if (gameplayBuildResult == null)
            {
                throw new ArgumentNullException(nameof(gameplayBuildResult));
            }

            GameplayBuildResult = gameplayBuildResult;
            PresentationData = presentationData ?? StagePresentationAssembler.EmptyResolvedData;
        }

        public StageRuntimeBuildResult GameplayBuildResult { get; }

        public StagePresentationResolvedData PresentationData { get; }
    }

    public static class StagePresentationAssembler
    {
        public static readonly StagePresentationResolvedData EmptyResolvedData = new(
            string.Empty,
            string.Empty,
            null,
            null,
            StageBgmReference.None,
            null,
            null,
            Array.Empty<EnemyPresentationBinding>(),
            null,
            Array.Empty<StaticEntityPresentationBinding>(),
            null,
            Array.Empty<BoardTilePresentationOverride>(),
            null,
            Array.Empty<TileFeaturePresentationResolvedBinding>(),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);

        public static StagePresentationResolvedData Resolve(StagePresentationDefinition definition)
        {
            if (definition == null)
            {
                return EmptyResolvedData;
            }

            return new StagePresentationResolvedData(
                definition.DisplayName,
                definition.SummaryText,
                definition.PreviewSprite,
                definition.BackgroundPrefab,
                definition.BgmReference,
                definition.EnemyPresentationCatalog,
                definition.EnemyPresentationArchetypeCatalog,
                CloneBindings(definition.EnemyPresentationBindings),
                definition.StaticEntityPresentationCatalog,
                CloneBindings(definition.StaticEntityPresentationBindings),
                definition.BoardTilePresentationCatalog,
                definition.BoardTilePresentationOverrides,
                definition.TileFeaturePresentationCatalog,
                ResolveTileFeatureBindings(definition.TileFeaturePresentationBindings),
                definition.ResultTitle,
                definition.ResultSummaryText,
                definition.ResultDetailText,
                definition.ResultContinueLabel);
        }

        public static StagePresentationResolvedData Resolve(
            StageDefinition gameplayDefinition,
            StagePresentationDefinition definition)
        {
            if (definition == null)
            {
                return EmptyResolvedData;
            }

            var tileFeatureBindings = ResolveTileFeatureBindings(
                gameplayDefinition,
                definition.TileFeaturePresentationBindings,
                definition.TileFeaturePresentationCatalog);

            return new StagePresentationResolvedData(
                definition.DisplayName,
                definition.SummaryText,
                definition.PreviewSprite,
                definition.BackgroundPrefab,
                definition.BgmReference,
                definition.EnemyPresentationCatalog,
                definition.EnemyPresentationArchetypeCatalog,
                CloneBindings(definition.EnemyPresentationBindings),
                definition.StaticEntityPresentationCatalog,
                CloneBindings(definition.StaticEntityPresentationBindings),
                definition.BoardTilePresentationCatalog,
                definition.BoardTilePresentationOverrides,
                definition.TileFeaturePresentationCatalog,
                tileFeatureBindings,
                definition.ResultTitle,
                definition.ResultSummaryText,
                definition.ResultDetailText,
                definition.ResultContinueLabel,
                BuildSuppressedBaseTileCells(gameplayDefinition, tileFeatureBindings));
        }

        public static TileFeaturePresentationBinding[] ToAuthoringBindings(
            IReadOnlyList<TileFeaturePresentationResolvedBinding> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TileFeaturePresentationBinding>();
            }

            var bindings = new TileFeaturePresentationBinding[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                bindings[i] = new TileFeaturePresentationBinding
                {
                    TileId = source[i].TileId,
                    VisualPrefab = source[i].VisualPrefab,
                };
            }

            return bindings;
        }

        public static BoardTilePresentationOverride[] ToAuthoringBoardTilePresentationOverrides(
            IReadOnlyList<BoardTilePresentationOverride> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BoardTilePresentationOverride>();
            }

            var overrides = new BoardTilePresentationOverride[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                overrides[i] = entry == null
                    ? null
                    : new BoardTilePresentationOverride(entry.Cell, entry.PresentationKey);
            }

            return overrides;
        }

        internal static EnemyPresentationBinding[] BuildEnemyBindings(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var bindings = new List<EnemyPresentationBinding>();

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                if (spawn.Kind != StageSpawnKind.Enemy)
                {
                    continue;
                }

                var presentationId = NormalizePresentationId(spawn.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    continue;
                }

                bindings.Add(new EnemyPresentationBinding
                {
                    EntityId = spawn.EntityId,
                    PresentationId = presentationId,
                });
            }

            bindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            return bindings.ToArray();
        }

        internal static StaticEntityPresentationBinding[] BuildStaticBindings(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var bindings = new List<StaticEntityPresentationBinding>();

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                if (spawn.Kind != StageSpawnKind.Box &&
                    spawn.Kind != StageSpawnKind.Wall)
                {
                    continue;
                }

                var presentationId = NormalizePresentationId(spawn.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    continue;
                }

                bindings.Add(new StaticEntityPresentationBinding
                {
                    EntityId = spawn.EntityId,
                    PresentationId = presentationId,
                });
            }

            bindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            return bindings.ToArray();
        }

        private static EnemyPresentationBinding[] CloneBindings(EnemyPresentationBinding[] source)
        {
            return source == null || source.Length == 0
                ? Array.Empty<EnemyPresentationBinding>()
                : (EnemyPresentationBinding[])source.Clone();
        }

        private static StaticEntityPresentationBinding[] CloneBindings(StaticEntityPresentationBinding[] source)
        {
            return source == null || source.Length == 0
                ? Array.Empty<StaticEntityPresentationBinding>()
                : (StaticEntityPresentationBinding[])source.Clone();
        }

        private static IReadOnlyList<TileFeaturePresentationResolvedBinding> ResolveTileFeatureBindings(
            IReadOnlyList<TileFeaturePresentationBinding> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TileFeaturePresentationResolvedBinding>();
            }

            var bindings = new TileFeaturePresentationResolvedBinding[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var binding = source[i];
                bindings[i] = binding == null
                    ? default
                    : new TileFeaturePresentationResolvedBinding(binding.TileId, binding.VisualPrefab);
            }

            return new ReadOnlyCollection<TileFeaturePresentationResolvedBinding>(bindings);
        }

        private static IReadOnlyList<TileFeaturePresentationResolvedBinding> ResolveTileFeatureBindings(
            StageDefinition gameplayDefinition,
            IReadOnlyList<TileFeaturePresentationBinding> directBindings,
            TileFeaturePresentationCatalog catalog)
        {
            if (gameplayDefinition == null)
            {
                return ResolveTileFeatureBindings(directBindings);
            }

            var directByTileId = BuildDirectBindingsByTileId(directBindings);
            var tileFeatures = gameplayDefinition.TileFeatures;
            if ((tileFeatures == null || tileFeatures.Length == 0) &&
                directByTileId.Count == 0)
            {
                return Array.Empty<TileFeaturePresentationResolvedBinding>();
            }

            var resolved = new List<TileFeaturePresentationResolvedBinding>();
            if (tileFeatures != null)
            {
                for (var i = 0; i < tileFeatures.Length; i++)
                {
                    var tileFeature = tileFeatures[i];
                    if (tileFeature.TileId <= 0)
                    {
                        continue;
                    }

                    if (directByTileId.TryGetValue(tileFeature.TileId, out var directBinding))
                    {
                        ResolveCatalogKeyPresentationModes(
                            catalog,
                            tileFeature,
                            out var directPlacementMode,
                            out var directFootprintMode);
                        resolved.Add(new TileFeaturePresentationResolvedBinding(
                            directBinding.TileId,
                            directBinding.VisualPrefab,
                            directPlacementMode,
                            directFootprintMode,
                            ResolveCatalogKeyVfxStyle(catalog, tileFeature)));
                        continue;
                    }

                    if (TryResolveCatalogEntry(
                            catalog,
                            tileFeature,
                            out var catalogEntry))
                    {
                        resolved.Add(new TileFeaturePresentationResolvedBinding(
                            tileFeature.TileId,
                            catalogEntry.VisualPrefab,
                            catalogEntry.PlacementMode,
                            catalogEntry.FootprintMode,
                            catalogEntry.VfxStyleKey));
                    }
                }
            }

            if (resolved.Count == 0)
            {
                return Array.Empty<TileFeaturePresentationResolvedBinding>();
            }

            resolved.Sort((left, right) => left.TileId.CompareTo(right.TileId));
            return new ReadOnlyCollection<TileFeaturePresentationResolvedBinding>(resolved);
        }

        private static Dictionary<int, TileFeaturePresentationBinding> BuildDirectBindingsByTileId(
            IReadOnlyList<TileFeaturePresentationBinding> directBindings)
        {
            var directByTileId = new Dictionary<int, TileFeaturePresentationBinding>();
            if (directBindings == null)
            {
                return directByTileId;
            }

            for (var i = 0; i < directBindings.Count; i++)
            {
                var binding = directBindings[i];
                if (binding == null ||
                    binding.TileId <= 0 ||
                    directByTileId.ContainsKey(binding.TileId))
                {
                    continue;
                }

                directByTileId.Add(binding.TileId, binding);
            }

            return directByTileId;
        }

        private static bool TryResolveCatalogEntry(
            TileFeaturePresentationCatalog catalog,
            StageTileFeatureDefinition tileFeature,
            out TileFeaturePresentationCatalogEntry entry)
        {
            entry = null;
            if (catalog == null)
            {
                return false;
            }

            var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(tileFeature.PresentationKey);
            if (!string.IsNullOrEmpty(presentationKey))
            {
                if (catalog.TryGetEntry(presentationKey, out var keyedEntry) &&
                    keyedEntry.VisualPrefab != null)
                {
                    entry = keyedEntry;
                    return true;
                }

                UnityEngine.Debug.LogWarning(
                    $"TileFeature TileId {tileFeature.TileId} could not resolve PresentationKey '{presentationKey}' in TileFeaturePresentationCatalog '{catalog.name}'.");
            }

            if (catalog.TryGetDefaultEntry(tileFeature.Kind, out var defaultEntry) &&
                defaultEntry.VisualPrefab != null)
            {
                entry = defaultEntry;
                return true;
            }

            UnityEngine.Debug.LogWarning(
                $"TileFeature TileId {tileFeature.TileId} has no resolved visual prefab for kind '{tileFeature.Kind}'.");
            return false;
        }

        private static void ResolveCatalogKeyPresentationModes(
            TileFeaturePresentationCatalog catalog,
            StageTileFeatureDefinition tileFeature,
            out TileFeatureVisualPlacementMode placementMode,
            out TileFeatureVisualFootprintMode footprintMode)
        {
            placementMode = TileFeatureVisualPlacementMode.Overlay;
            footprintMode = TileFeatureVisualFootprintMode.SingleCell;
            if (catalog == null)
            {
                return;
            }

            var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(tileFeature.PresentationKey);
            if (string.IsNullOrEmpty(presentationKey))
            {
                return;
            }

            if (!catalog.TryGetEntry(presentationKey, out var entry))
            {
                return;
            }

            placementMode = entry.PlacementMode;
            footprintMode = entry.FootprintMode;
        }

        private static VfxStyleKey ResolveCatalogKeyVfxStyle(
            TileFeaturePresentationCatalog catalog,
            StageTileFeatureDefinition tileFeature)
        {
            if (catalog == null)
            {
                return VfxStyleKey.Default;
            }

            var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(tileFeature.PresentationKey);
            if (string.IsNullOrEmpty(presentationKey) ||
                !catalog.TryGetEntry(presentationKey, out var entry))
            {
                return VfxStyleKey.Default;
            }

            return entry.VfxStyleKey;
        }

        private static IReadOnlyList<SurfaceCell> BuildSuppressedBaseTileCells(
            StageDefinition gameplayDefinition,
            IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings)
        {
            if (gameplayDefinition == null ||
                bindings == null ||
                bindings.Count == 0)
            {
                return Array.Empty<SurfaceCell>();
            }

            var replaceBindingsByTileId = new Dictionary<int, TileFeaturePresentationResolvedBinding>();
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.TileId > 0 &&
                    binding.VisualPrefab != null &&
                    binding.PlacementMode == TileFeatureVisualPlacementMode.ReplaceBaseTile)
                {
                    replaceBindingsByTileId[binding.TileId] = binding;
                }
            }

            if (replaceBindingsByTileId.Count == 0)
            {
                return Array.Empty<SurfaceCell>();
            }

            var cells = new List<SurfaceCell>();
            var seenCells = new HashSet<SurfaceCell>();
            var board = gameplayDefinition.Board;
            if (board.MaxInclusive.x < board.MinInclusive.x ||
                board.MaxInclusive.y < board.MinInclusive.y)
            {
                return Array.Empty<SurfaceCell>();
            }

            var boardBounds = new BoardBounds(board.MinInclusive, board.MaxInclusive);
            var tileFeatures = gameplayDefinition.TileFeatures;
            for (var i = 0; i < tileFeatures.Length; i++)
            {
                var tileFeature = tileFeatures[i];
                if (!replaceBindingsByTileId.TryGetValue(tileFeature.TileId, out var binding))
                {
                    continue;
                }

                AddSuppressedBaseTileCells(tileFeature.Cell, binding.FootprintMode, boardBounds, seenCells, cells);
            }

            if (cells.Count == 0)
            {
                return Array.Empty<SurfaceCell>();
            }

            cells.Sort(CompareSurfaceCells);
            return new ReadOnlyCollection<SurfaceCell>(cells);
        }

        private static void AddSuppressedBaseTileCells(
            SurfaceCell center,
            TileFeatureVisualFootprintMode footprintMode,
            BoardBounds boardBounds,
            HashSet<SurfaceCell> seenCells,
            List<SurfaceCell> cells)
        {
            switch (footprintMode)
            {
                case TileFeatureVisualFootprintMode.ThreeByThreeSameFace:
                    for (var yOffset = -1; yOffset <= 1; yOffset++)
                    {
                        for (var xOffset = -1; xOffset <= 1; xOffset++)
                        {
                            var candidate = new SurfaceCell(center.face, center.x + xOffset, center.y + yOffset);
                            AddSuppressedBaseTileCell(candidate, boardBounds, seenCells, cells);
                        }
                    }

                    return;
                case TileFeatureVisualFootprintMode.SingleCell:
                default:
                    AddSuppressedBaseTileCell(center, boardBounds, seenCells, cells);
                    return;
            }
        }

        private static void AddSuppressedBaseTileCell(
            SurfaceCell cell,
            BoardBounds boardBounds,
            HashSet<SurfaceCell> seenCells,
            List<SurfaceCell> cells)
        {
            if (boardBounds.Contains(cell.PlanarPosition) &&
                seenCells.Add(cell))
            {
                cells.Add(cell);
            }
        }

        private static int CompareSurfaceCells(SurfaceCell left, SurfaceCell right)
        {
            var faceComparison = ((int)left.face).CompareTo((int)right.face);
            if (faceComparison != 0)
            {
                return faceComparison;
            }

            var xComparison = left.x.CompareTo(right.x);
            return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
        }

        private static string NormalizePresentationId(string presentationId)
        {
            return string.IsNullOrWhiteSpace(presentationId)
                ? string.Empty
                : presentationId.Trim();
        }
    }

    public static class StageSceneCompositionAssembler
    {
        public static StageSceneCompositionData Compose(
            StageRuntimeBuildResult gameplayBuildResult,
            StagePresentationResolvedData presentationData)
        {
            return new StageSceneCompositionData(gameplayBuildResult, presentationData);
        }
    }
}
