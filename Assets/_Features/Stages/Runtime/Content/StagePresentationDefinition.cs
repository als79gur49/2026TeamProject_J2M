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
        [Header("Stage Identity / Scene")]
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private GameObject backgroundPrefab;

        [Header("Entity Presentation")]
        [SerializeField] private EnemyPresentationCatalog enemyPresentationCatalog;
        [SerializeField] private EnemyPresentationArchetypeCatalog enemyPresentationArchetypeCatalog;
        [SerializeField] private EnemyPresentationBinding[] enemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        [SerializeField] private StaticEntityPresentationCatalog staticEntityPresentationCatalog;
        [SerializeField] private StaticEntityPresentationBinding[] staticEntityPresentationBindings = Array.Empty<StaticEntityPresentationBinding>();

        [Header("Board Presentation")]
        [SerializeField] private BoardPresentationProfile boardPresentationProfile;
        [SerializeField] private BoardTilePresentationCatalog boardTilePresentationCatalog;
        [SerializeField] private BoardTilePaintOverride[] boardTilePaintOverrides =
            Array.Empty<BoardTilePaintOverride>();

        [Header("Tile Feature Presentation")]
        [SerializeField] private TileFeaturePresentationCatalog tileFeaturePresentationCatalog;
        [SerializeField] private TileFeaturePresentationBinding[] tileFeaturePresentationBindings = Array.Empty<TileFeaturePresentationBinding>();

        [Header("World Guide")]
        [SerializeField] private StageWorldGuideCatalog worldGuideCatalog;
        [SerializeField] private StageWorldGuideInstruction[] worldGuideInstructions =
            Array.Empty<StageWorldGuideInstruction>();

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
        }
    }
}
