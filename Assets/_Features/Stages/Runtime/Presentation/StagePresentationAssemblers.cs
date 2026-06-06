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
            BoardPresentationProfile boardPresentationProfile,
            BoardTilePresentationCatalog boardTilePresentationCatalog,
            BoardTileStyleCatalog boardTileStyleCatalog,
            BoardTileOverlayCatalog boardTileOverlayCatalog,
            IReadOnlyList<BoardTilePresentationOverride> boardTilePresentationOverrides,
            IReadOnlyList<BoardTilePaintOverride> boardTilePaintOverrides,
            IReadOnlyList<BoardTileOverlayOverride> boardTileOverlayOverrides,
            TileFeaturePresentationCatalog tileFeaturePresentationCatalog,
            IReadOnlyList<TileFeaturePresentationResolvedBinding> tileFeatureBindings,
            StageWorldGuideCatalog worldGuideCatalog,
            IReadOnlyList<StageWorldGuideInstructionResolved> worldGuideInstructions,
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
            BoardPresentationProfile = boardPresentationProfile;
            BoardTilePresentationCatalog = boardTilePresentationCatalog;
            BoardTileStyleCatalog = boardTileStyleCatalog;
            BoardTileOverlayCatalog = boardTileOverlayCatalog;
            BoardTilePresentationOverrides = CloneReadOnlyBoardTileOverrides(boardTilePresentationOverrides);
            BoardTilePaintOverrides = CloneReadOnlyBoardTilePaintOverrides(boardTilePaintOverrides);
            BoardTileOverlayOverrides = CloneReadOnlyBoardTileOverlayOverrides(boardTileOverlayOverrides);
            TileFeaturePresentationCatalog = tileFeaturePresentationCatalog;
            TileFeatureBindings = CloneReadOnlyBindings(tileFeatureBindings);
            WorldGuideCatalog = worldGuideCatalog;
            WorldGuideInstructions = CloneReadOnlyWorldGuideInstructions(worldGuideInstructions);
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

        public BoardPresentationProfile BoardPresentationProfile { get; }

        public BoardTilePresentationCatalog BoardTilePresentationCatalog { get; }

        public BoardTileStyleCatalog BoardTileStyleCatalog { get; }

        public BoardTileOverlayCatalog BoardTileOverlayCatalog { get; }

        public IReadOnlyList<BoardTilePresentationOverride> BoardTilePresentationOverrides { get; }

        public IReadOnlyList<BoardTilePaintOverride> BoardTilePaintOverrides { get; }

        public IReadOnlyList<BoardTileOverlayOverride> BoardTileOverlayOverrides { get; }

        public TileFeaturePresentationCatalog TileFeaturePresentationCatalog { get; }

        public IReadOnlyList<TileFeaturePresentationResolvedBinding> TileFeatureBindings { get; }

        public StageWorldGuideCatalog WorldGuideCatalog { get; }

        public IReadOnlyList<StageWorldGuideInstructionResolved> WorldGuideInstructions { get; }

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

        private static IReadOnlyList<StageWorldGuideInstructionResolved> CloneReadOnlyWorldGuideInstructions(
            IReadOnlyList<StageWorldGuideInstructionResolved> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<StageWorldGuideInstructionResolved>();
            }

            var instructions = new StageWorldGuideInstructionResolved[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                instructions[i] = source[i];
            }

            return new ReadOnlyCollection<StageWorldGuideInstructionResolved>(instructions);
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

        private static IReadOnlyList<BoardTilePaintOverride> CloneReadOnlyBoardTilePaintOverrides(
            IReadOnlyList<BoardTilePaintOverride> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BoardTilePaintOverride>();
            }

            var overrides = new BoardTilePaintOverride[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                overrides[i] = entry == null
                    ? null
                    : new BoardTilePaintOverride(entry.Cell, entry.StyleKey);
            }

            return new ReadOnlyCollection<BoardTilePaintOverride>(overrides);
        }

        private static IReadOnlyList<BoardTileOverlayOverride> CloneReadOnlyBoardTileOverlayOverrides(
            IReadOnlyList<BoardTileOverlayOverride> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BoardTileOverlayOverride>();
            }

            var overrides = new BoardTileOverlayOverride[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                overrides[i] = entry == null
                    ? null
                    : new BoardTileOverlayOverride(entry.Cell, entry.OverlayKey);
            }

            return new ReadOnlyCollection<BoardTileOverlayOverride>(overrides);
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
            null,
            null,
            null,
            Array.Empty<BoardTilePresentationOverride>(),
            Array.Empty<BoardTilePaintOverride>(),
            Array.Empty<BoardTileOverlayOverride>(),
            null,
            Array.Empty<TileFeaturePresentationResolvedBinding>(),
            null,
            Array.Empty<StageWorldGuideInstructionResolved>(),
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
                StagePresentationBindingNormalizer.NormalizeEnemyBindings(definition.EnemyPresentationBindings),
                definition.StaticEntityPresentationCatalog,
                StagePresentationBindingNormalizer.NormalizeStaticEntityBindings(
                    definition.StaticEntityPresentationBindings),
                definition.BoardPresentationProfile,
                definition.BoardTilePresentationCatalog,
                definition.BoardTileStyleCatalog,
                definition.BoardTileOverlayCatalog,
                definition.BoardTilePresentationOverrides,
                definition.BoardTilePaintOverrides,
                definition.BoardTileOverlayOverrides,
                definition.TileFeaturePresentationCatalog,
                ResolveTileFeatureBindings(definition.TileFeaturePresentationBindings),
                definition.WorldGuideCatalog,
                ResolveWorldGuideInstructions(definition.WorldGuideInstructions, definition),
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
                StagePresentationBindingNormalizer.NormalizeEnemyBindings(definition.EnemyPresentationBindings),
                definition.StaticEntityPresentationCatalog,
                StagePresentationBindingNormalizer.NormalizeStaticEntityBindings(
                    definition.StaticEntityPresentationBindings),
                definition.BoardPresentationProfile,
                definition.BoardTilePresentationCatalog,
                definition.BoardTileStyleCatalog,
                definition.BoardTileOverlayCatalog,
                definition.BoardTilePresentationOverrides,
                definition.BoardTilePaintOverrides,
                definition.BoardTileOverlayOverrides,
                definition.TileFeaturePresentationCatalog,
                tileFeatureBindings,
                definition.WorldGuideCatalog,
                ResolveWorldGuideInstructions(definition.WorldGuideInstructions, definition),
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

        public static BoardTilePaintOverride[] ToAuthoringBoardTilePaintOverrides(
            IReadOnlyList<BoardTilePaintOverride> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BoardTilePaintOverride>();
            }

            var overrides = new BoardTilePaintOverride[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                overrides[i] = entry == null
                    ? null
                    : new BoardTilePaintOverride(entry.Cell, entry.StyleKey);
            }

            return overrides;
        }

        public static BoardTileOverlayOverride[] ToAuthoringBoardTileOverlayOverrides(
            IReadOnlyList<BoardTileOverlayOverride> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BoardTileOverlayOverride>();
            }

            var overrides = new BoardTileOverlayOverride[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                overrides[i] = entry == null
                    ? null
                    : new BoardTileOverlayOverride(entry.Cell, entry.OverlayKey);
            }

            return overrides;
        }

        public static StageWorldGuideInstruction[] ToAuthoringWorldGuideInstructions(
            IReadOnlyList<StageWorldGuideInstructionResolved> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<StageWorldGuideInstruction>();
            }

            var instructions = new StageWorldGuideInstruction[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                instructions[i] = new StageWorldGuideInstruction(
                    true,
                    entry.GuideKey,
                    entry.Cell,
                    entry.LocalOffset,
                    entry.HeightOffset,
                    entry.FacingMode,
                    entry.HideWhenFaceInactive);
            }

            return instructions;
        }

        private static IReadOnlyList<TileFeaturePresentationResolvedBinding> ResolveTileFeatureBindings(
            IReadOnlyList<TileFeaturePresentationBinding> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TileFeaturePresentationResolvedBinding>();
            }

            var normalized = StagePresentationBindingNormalizer.CloneTileFeatureBindingsPreserveOrder(source);
            var bindings = new TileFeaturePresentationResolvedBinding[normalized.Length];
            for (var i = 0; i < normalized.Length; i++)
            {
                var binding = normalized[i];
                bindings[i] = binding == null
                    ? default
                    : new TileFeaturePresentationResolvedBinding(binding.TileId, binding.VisualPrefab);
            }

            return new ReadOnlyCollection<TileFeaturePresentationResolvedBinding>(bindings);
        }

        private static IReadOnlyList<StageWorldGuideInstructionResolved> ResolveWorldGuideInstructions(
            IReadOnlyList<StageWorldGuideInstruction> source,
            UnityEngine.Object context)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<StageWorldGuideInstructionResolved>();
            }

            var resolved = new List<StageWorldGuideInstructionResolved>();
            for (var i = 0; i < source.Count; i++)
            {
                var instruction = source[i];
                if (instruction == null || !instruction.Enabled)
                {
                    continue;
                }

                var guideKey = instruction.GuideKey;
                if (string.IsNullOrEmpty(guideKey))
                {
                    UnityEngine.Debug.LogWarning(
                        $"Skipping StageWorldGuideInstruction[{i}] with an empty GuideKey.",
                        context);
                    continue;
                }

                resolved.Add(new StageWorldGuideInstructionResolved(
                    guideKey,
                    instruction.Cell,
                    instruction.LocalOffset,
                    instruction.HeightOffset,
                    instruction.FacingMode,
                    instruction.HideWhenFaceInactive));
            }

            return resolved.Count == 0
                ? Array.Empty<StageWorldGuideInstructionResolved>()
                : new ReadOnlyCollection<StageWorldGuideInstructionResolved>(resolved);
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
                            ResolveTileFeatureVfxStyleKey(tileFeature, catalogEntry.VfxStyleKey)));
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
                return ResolveTileFeatureVfxStyleKey(tileFeature, VfxStyleKey.Default);
            }

            var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(tileFeature.PresentationKey);
            if (string.IsNullOrEmpty(presentationKey) ||
                !catalog.TryGetEntry(presentationKey, out var entry))
            {
                return ResolveTileFeatureVfxStyleKey(tileFeature, VfxStyleKey.Default);
            }

            return ResolveTileFeatureVfxStyleKey(tileFeature, entry.VfxStyleKey);
        }

        private static VfxStyleKey ResolveTileFeatureVfxStyleKey(
            StageTileFeatureDefinition tileFeature,
            VfxStyleKey fallback)
        {
            if (tileFeature.Kind != TileFeatureKind.Destroy)
            {
                return fallback;
            }

            switch (tileFeature.ActivationRule)
            {
                case TileFeatureActivationRule.FrontFaceOnly:
                    return VfxStyleKey.Red;
                case TileFeatureActivationRule.BottomFaceOnly:
                    return VfxStyleKey.Blue;
                default:
                    return fallback;
            }
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
