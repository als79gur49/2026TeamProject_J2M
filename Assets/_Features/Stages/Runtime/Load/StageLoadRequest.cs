using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public readonly struct StageLoadRequest
    {
        private StageLoadRequest(
            ScriptableObjectStageCatalogProvider stageCatalogProvider,
            string sceneName)
        {
            StageCatalogProvider = stageCatalogProvider;
            SceneName = sceneName ?? string.Empty;
        }

        public static StageLoadRequest CreateLaunchContextOnly(
            ScriptableObjectStageCatalogProvider stageCatalogProvider,
            string sceneName)
        {
            return new StageLoadRequest(stageCatalogProvider, sceneName);
        }

        public ScriptableObjectStageCatalogProvider StageCatalogProvider { get; }

        public string SceneName { get; }
    }
}
