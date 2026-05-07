using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Host;
using Game.Shared.AudioContracts;
using UnityEngine;

namespace Game.Feature.Stages
{
    public readonly struct TileFeaturePresentationResolvedBinding
    {
        public TileFeaturePresentationResolvedBinding(int tileId, GameObject visualPrefab)
        {
            TileId = tileId;
            VisualPrefab = visualPrefab;
        }

        public int TileId { get; }

        public GameObject VisualPrefab { get; }
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
            string resultContinueLabel)
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
                ResolveTileFeatureBindings(
                    gameplayDefinition,
                    definition.TileFeaturePresentationBindings,
                    definition.TileFeaturePresentationCatalog),
                definition.ResultTitle,
                definition.ResultSummaryText,
                definition.ResultDetailText,
                definition.ResultContinueLabel);
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
                        resolved.Add(new TileFeaturePresentationResolvedBinding(
                            directBinding.TileId,
                            directBinding.VisualPrefab));
                        continue;
                    }

                    if (TryResolveCatalogPrefab(
                            catalog,
                            tileFeature,
                            out var catalogPrefab))
                    {
                        resolved.Add(new TileFeaturePresentationResolvedBinding(
                            tileFeature.TileId,
                            catalogPrefab));
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

        private static bool TryResolveCatalogPrefab(
            TileFeaturePresentationCatalog catalog,
            StageTileFeatureDefinition tileFeature,
            out GameObject visualPrefab)
        {
            visualPrefab = null;
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
                    visualPrefab = keyedEntry.VisualPrefab;
                    return true;
                }

                UnityEngine.Debug.LogWarning(
                    $"TileFeature TileId {tileFeature.TileId} could not resolve PresentationKey '{presentationKey}' in TileFeaturePresentationCatalog '{catalog.name}'.");
            }

            if (catalog.TryGetDefaultEntry(tileFeature.Kind, out var defaultEntry) &&
                defaultEntry.VisualPrefab != null)
            {
                visualPrefab = defaultEntry.VisualPrefab;
                return true;
            }

            UnityEngine.Debug.LogWarning(
                $"TileFeature TileId {tileFeature.TileId} has no resolved visual prefab for kind '{tileFeature.Kind}'.");
            return false;
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
