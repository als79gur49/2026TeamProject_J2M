using System;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public readonly struct StageLoadRequest
    {
        public StageLoadRequest(
            StageLoadSourceMode sourceMode,
            ScriptableObjectStageCatalogProvider stageCatalogProvider,
            StageId defaultStageId,
            StageContentEntry serializedStageContentEntry,
            StageDefinition legacyStageDefinition,
            EnemyPresentationCatalog legacyEnemyPresentationCatalog,
            StaticEntityPresentationCatalog legacyStaticEntityPresentationCatalog,
            string sceneName,
            bool allowDefaultStageIdFallback)
        {
            SourceMode = sourceMode;
            StageCatalogProvider = stageCatalogProvider;
            DefaultStageId = defaultStageId;
            SerializedStageContentEntry = serializedStageContentEntry;
            LegacyStageDefinition = legacyStageDefinition;
            LegacyEnemyPresentationCatalog = legacyEnemyPresentationCatalog;
            LegacyStaticEntityPresentationCatalog = legacyStaticEntityPresentationCatalog;
            SceneName = sceneName ?? string.Empty;
            AllowDefaultStageIdFallback = allowDefaultStageIdFallback;
        }

        public StageLoadSourceMode SourceMode { get; }

        public ScriptableObjectStageCatalogProvider StageCatalogProvider { get; }

        public StageId DefaultStageId { get; }

        public StageContentEntry SerializedStageContentEntry { get; }

        public StageDefinition LegacyStageDefinition { get; }

        public EnemyPresentationCatalog LegacyEnemyPresentationCatalog { get; }

        public StaticEntityPresentationCatalog LegacyStaticEntityPresentationCatalog { get; }

        public string SceneName { get; }

        public bool AllowDefaultStageIdFallback { get; }

        public bool IsPlayerRuntime => UnityEngine.Application.isPlaying && !UnityEngine.Application.isEditor;
    }
}
