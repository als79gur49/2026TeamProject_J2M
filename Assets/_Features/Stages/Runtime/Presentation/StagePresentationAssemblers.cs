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
                ResolveTileFeatureBindings(definition.TileFeaturePresentationBindings),
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
