using System;
using Game.Feature.Stages;
using Game.Feature.UI.Flow;
using UnityEngine;

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
            _sceneLoadPort = sceneLoadPort;
        }

        public void ReturnToMainMenu()
        {
            if (!_routeConfig.HasValidMainMenuScene)
            {
                throw new InvalidOperationException("Configured main menu return requires a main menu scene in route config.");
            }

            if (_sceneLoadPort != null)
            {
                StageLaunchContextStore.Clear();
                _sceneLoadPort.LoadScene(_routeConfig.MainMenuSceneName);
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                SceneTransitionCoordinator.Instance.TryStartMainMenuReturn(_routeConfig.MainMenuSceneName);
                return;
            }

            StageLaunchContextStore.Clear();
            UnitySceneLoadPort.Instance.LoadScene(_routeConfig.MainMenuSceneName);
        }
    }
}
