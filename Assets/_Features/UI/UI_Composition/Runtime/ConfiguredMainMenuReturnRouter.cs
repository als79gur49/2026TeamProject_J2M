using System;
using Game.Feature.Stages;
using Game.Feature.UI.Flow;

namespace Game.Feature.UI.Composition
{
    public sealed class ConfiguredMainMenuReturnRouter : IMainMenuReturnRouter
    {
        private readonly GameplayStageLaunchRouteConfig _routeConfig;
        private readonly ISceneLoadPort _sceneLoadPort;

        public ConfiguredMainMenuReturnRouter(
            GameplayStageLaunchRouteConfig routeConfig,
            ISceneLoadPort sceneLoadPort = null)
        {
            _routeConfig = routeConfig ?? throw new ArgumentNullException(nameof(routeConfig));
            _sceneLoadPort = sceneLoadPort ?? UnitySceneLoadPort.Instance;
        }

        public void ReturnToMainMenu()
        {
            if (!_routeConfig.HasValidMainMenuScene)
            {
                throw new InvalidOperationException("Configured main menu return requires a main menu scene in route config.");
            }

            StageLaunchContextStore.Clear();
            _sceneLoadPort.LoadScene(_routeConfig.MainMenuSceneName);
        }
    }
}
