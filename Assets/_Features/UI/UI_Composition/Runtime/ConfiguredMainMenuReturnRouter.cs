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

        public ConfiguredMainMenuReturnRouter(GameplayStageLaunchRouteConfig routeConfig)
            : this(routeConfig, null)
        {
        }

        internal ConfiguredMainMenuReturnRouter(
            GameplayStageLaunchRouteConfig routeConfig,
            ISceneLoadPort sceneLoadPort = null)
        {
            _routeConfig = routeConfig ?? throw new ArgumentNullException(nameof(routeConfig));
            _sceneLoadPort = sceneLoadPort;
        }

        public void ReturnToMainMenu(SceneTransitionIntent transitionIntent)
        {
            SceneTransitionRoutePolicyCatalog.RequireDestination(
                SceneTransitionRoutePolicyCatalog.ResolveProduction(transitionIntent),
                SceneTransitionDestinationKind.MainMenu);

            if (!_routeConfig.HasValidMainMenuScene)
            {
                throw new InvalidOperationException("Configured main menu return requires a main menu scene in route config.");
            }

            if (_sceneLoadPort != null)
            {
                SceneTransitionRoutePolicyCatalog.ResolveException(
                    SceneTransitionIntent.TestInjectedSceneLoad,
                    SceneTransitionRouteClassification.TestOnly);
                StageLaunchContextStore.Clear();
                _sceneLoadPort.LoadScene(_routeConfig.MainMenuSceneName);
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                if (!SceneTransitionCoordinator.Instance.TryStartMainMenuReturn(
                        _routeConfig.MainMenuSceneName,
                        transitionIntent))
                {
                    throw new InvalidOperationException(
                        $"Main menu transition {transitionIntent} was rejected because another transition owns the session.");
                }

                return;
            }

            SceneTransitionRoutePolicyCatalog.ResolveException(
                SceneTransitionIntent.EditorDirectSceneLoad,
                SceneTransitionRouteClassification.EditorOnly);
            StageLaunchContextStore.Clear();
            UnitySceneLoadPort.Instance.LoadScene(_routeConfig.MainMenuSceneName);
        }
    }
}
