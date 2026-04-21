using System;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public enum StageLoadFallbackPolicy
    {
        None = 0,
        EditorDirectPlayOnly = 1,
    }

    public readonly struct StageLoadRequest
    {
        [Obsolete("Use CreateLaunchContextOnly or CreateEditorDirectPlayFallback to make fallback intent explicit.")]
        public StageLoadRequest(
            ScriptableObjectStageCatalogProvider stageCatalogProvider,
            StageId defaultStageId,
            string sceneName,
            bool allowDefaultStageIdFallback)
            : this(
                stageCatalogProvider,
                defaultStageId,
                sceneName,
                allowDefaultStageIdFallback
                    ? StageLoadFallbackPolicy.EditorDirectPlayOnly
                    : StageLoadFallbackPolicy.None)
        {
        }

        private StageLoadRequest(
            ScriptableObjectStageCatalogProvider stageCatalogProvider,
            StageId defaultStageId,
            string sceneName,
            StageLoadFallbackPolicy fallbackPolicy)
        {
            StageCatalogProvider = stageCatalogProvider;
            DefaultStageId = defaultStageId;
            SceneName = sceneName ?? string.Empty;
            FallbackPolicy = fallbackPolicy;
        }

        public static StageLoadRequest CreateLaunchContextOnly(
            ScriptableObjectStageCatalogProvider stageCatalogProvider,
            string sceneName)
        {
            return new StageLoadRequest(
                stageCatalogProvider,
                StageId.None,
                sceneName,
                StageLoadFallbackPolicy.None);
        }

        public static StageLoadRequest CreateEditorDirectPlayFallback(
            ScriptableObjectStageCatalogProvider stageCatalogProvider,
            StageId defaultStageId,
            string sceneName)
        {
            if (!defaultStageId.IsValid)
            {
                throw new ArgumentException("Editor direct-play fallback requires a valid defaultStageId.", nameof(defaultStageId));
            }

            return new StageLoadRequest(
                stageCatalogProvider,
                defaultStageId,
                sceneName,
                StageLoadFallbackPolicy.EditorDirectPlayOnly);
        }

        public ScriptableObjectStageCatalogProvider StageCatalogProvider { get; }

        public StageId DefaultStageId { get; }

        public string SceneName { get; }

        public StageLoadFallbackPolicy FallbackPolicy { get; }
    }
}
