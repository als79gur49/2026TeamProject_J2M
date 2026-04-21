using System;
using Game.Feature.Gameplay.Host;
using Game.Shared.AudioContracts;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Presentation Definition", fileName = "stage-presentation")]
    public sealed class StagePresentationDefinition : StageCompanionDefinitionBase
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string summaryText = string.Empty;
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private GameObject backgroundPrefab;
        [SerializeField] private StageBgmReference bgmReference = StageBgmReference.None;
        [SerializeField] private EnemyPresentationCatalog enemyPresentationCatalog;
        [SerializeField] private EnemyPresentationBinding[] enemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        [SerializeField] private StaticEntityPresentationCatalog staticEntityPresentationCatalog;
        [SerializeField] private StaticEntityPresentationBinding[] staticEntityPresentationBindings = Array.Empty<StaticEntityPresentationBinding>();
        [SerializeField] private string resultTitle = "Stage Cleared";
        [SerializeField] private string resultSummaryText = string.Empty;
        [SerializeField] private string resultDetailText = string.Empty;
        [SerializeField] private string resultContinueLabel = "Continue";

        public string DisplayName => displayName ?? string.Empty;

        public string SummaryText => summaryText ?? string.Empty;

        public Sprite PreviewSprite => previewSprite;

        public GameObject BackgroundPrefab => backgroundPrefab;

        public StageBgmReference BgmReference => bgmReference;

        public EnemyPresentationCatalog EnemyPresentationCatalog => enemyPresentationCatalog;

        public EnemyPresentationBinding[] EnemyPresentationBindings => enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();

        public StaticEntityPresentationCatalog StaticEntityPresentationCatalog => staticEntityPresentationCatalog;

        public StaticEntityPresentationBinding[] StaticEntityPresentationBindings =>
            staticEntityPresentationBindings ?? Array.Empty<StaticEntityPresentationBinding>();

        public string ResultTitle => resultTitle ?? string.Empty;

        public string ResultSummaryText => resultSummaryText ?? string.Empty;

        public string ResultDetailText => resultDetailText ?? string.Empty;

        public string ResultContinueLabel => resultContinueLabel ?? string.Empty;
    }
}
