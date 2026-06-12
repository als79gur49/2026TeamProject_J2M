using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class TileFeaturePresentationBinding
    {
        public int TileId;
        public GameObject VisualPrefab;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Presentation Definition", fileName = "stage-presentation")]
    public sealed class StagePresentationDefinition : StageCompanionDefinitionBase
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private GameObject backgroundPrefab;
        [SerializeField] private EnemyPresentationCatalog enemyPresentationCatalog;
        [SerializeField] private EnemyPresentationArchetypeCatalog enemyPresentationArchetypeCatalog;
        [SerializeField] private EnemyPresentationBinding[] enemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        [SerializeField] private StaticEntityPresentationCatalog staticEntityPresentationCatalog;
        [SerializeField] private StaticEntityPresentationBinding[] staticEntityPresentationBindings = Array.Empty<StaticEntityPresentationBinding>();
        [SerializeField] private BoardPresentationProfile boardPresentationProfile;
        [SerializeField] private BoardTilePresentationCatalog boardTilePresentationCatalog;
        [SerializeField] private BoardTilePaintOverride[] boardTilePaintOverrides =
            Array.Empty<BoardTilePaintOverride>();
        [SerializeField] private TileFeaturePresentationCatalog tileFeaturePresentationCatalog;
        [SerializeField] private TileFeaturePresentationBinding[] tileFeaturePresentationBindings = Array.Empty<TileFeaturePresentationBinding>();
        [SerializeField] private StageWorldGuideCatalog worldGuideCatalog;
        [SerializeField] private StageWorldGuideInstruction[] worldGuideInstructions =
            Array.Empty<StageWorldGuideInstruction>();
        [SerializeField] private string resultTitle = "Stage Cleared";
        [SerializeField] private string resultSummaryText = string.Empty;
        [SerializeField] private string resultDetailText = string.Empty;
        [SerializeField] private string resultContinueLabel = "Continue";

        public string DisplayName => displayName ?? string.Empty;

        public GameObject BackgroundPrefab => backgroundPrefab;

        public EnemyPresentationCatalog EnemyPresentationCatalog => enemyPresentationCatalog;

        public EnemyPresentationArchetypeCatalog EnemyPresentationArchetypeCatalog => enemyPresentationArchetypeCatalog;

        public EnemyPresentationBinding[] EnemyPresentationBindings => enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();

        public StaticEntityPresentationCatalog StaticEntityPresentationCatalog => staticEntityPresentationCatalog;

        public StaticEntityPresentationBinding[] StaticEntityPresentationBindings =>
            staticEntityPresentationBindings ?? Array.Empty<StaticEntityPresentationBinding>();

        public BoardPresentationProfile BoardPresentationProfile => boardPresentationProfile;

        public BoardTilePresentationCatalog BoardTilePresentationCatalog => boardTilePresentationCatalog;

        public BoardTileStyleCatalog BoardTileStyleCatalog =>
            boardPresentationProfile != null ? boardPresentationProfile.DefaultBoardTileStyleCatalog : null;

        public IReadOnlyList<BoardTilePaintOverride> BoardTilePaintOverrides =>
            boardTilePaintOverrides ?? Array.Empty<BoardTilePaintOverride>();

        public TileFeaturePresentationCatalog TileFeaturePresentationCatalog => tileFeaturePresentationCatalog;

        public TileFeaturePresentationBinding[] TileFeaturePresentationBindings =>
            tileFeaturePresentationBindings ?? Array.Empty<TileFeaturePresentationBinding>();

        public StageWorldGuideCatalog WorldGuideCatalog => worldGuideCatalog;

        public IReadOnlyList<StageWorldGuideInstruction> WorldGuideInstructions =>
            worldGuideInstructions ?? Array.Empty<StageWorldGuideInstruction>();

        public string ResultTitle => resultTitle ?? string.Empty;

        public string ResultSummaryText => resultSummaryText ?? string.Empty;

        public string ResultDetailText => resultDetailText ?? string.Empty;

        public string ResultContinueLabel => resultContinueLabel ?? string.Empty;

        public void ApplyResolvedData(StagePresentationResolvedData value)
        {
            var resolvedData = value ?? StagePresentationAssembler.EmptyResolvedData;
            displayName = resolvedData.DisplayName;
            backgroundPrefab = resolvedData.BackgroundPrefab;
            enemyPresentationCatalog = resolvedData.EnemyPresentationCatalog;
            enemyPresentationArchetypeCatalog = resolvedData.EnemyPresentationArchetypeCatalog;
            enemyPresentationBindings = resolvedData.EnemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
            staticEntityPresentationCatalog = resolvedData.StaticEntityPresentationCatalog;
            staticEntityPresentationBindings =
                resolvedData.StaticEntityPresentationBindings ?? Array.Empty<StaticEntityPresentationBinding>();
            boardPresentationProfile = resolvedData.BoardPresentationProfile;
            boardTilePresentationCatalog = resolvedData.BoardTilePresentationCatalog;
            boardTilePaintOverrides =
                StagePresentationAssembler.ToAuthoringBoardTilePaintOverrides(
                    resolvedData.BoardTilePaintOverrides);
            tileFeaturePresentationCatalog = resolvedData.TileFeaturePresentationCatalog;
            tileFeaturePresentationBindings =
                StagePresentationAssembler.ToAuthoringBindings(resolvedData.TileFeatureBindings);
            worldGuideCatalog = resolvedData.WorldGuideCatalog;
            worldGuideInstructions =
                StagePresentationAssembler.ToAuthoringWorldGuideInstructions(
                    resolvedData.WorldGuideInstructions);
            resultTitle = resolvedData.ResultTitle;
            resultSummaryText = resolvedData.ResultSummaryText;
            resultDetailText = resolvedData.ResultDetailText;
            resultContinueLabel = resolvedData.ResultContinueLabel;
        }
    }
}
