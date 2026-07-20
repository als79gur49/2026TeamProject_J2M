using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    internal sealed class CurrentSceneStageLaunchRouter : IStageLaunchRouter
    {
        private readonly ISceneLoadPort _sceneLoadPort;
        private readonly string _sceneName;

        public CurrentSceneStageLaunchRouter(string sceneName, ISceneLoadPort sceneLoadPort = null)
        {
            _sceneName = sceneName ?? string.Empty;
            _sceneLoadPort = sceneLoadPort;
        }

        public bool IsLaunchInProgress =>
            _sceneLoadPort == null &&
            UnityEngine.Application.isPlaying &&
            SceneTransitionCoordinator.Instance.IsTransitionInProgress;

        public void Launch(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Stage launch router requires a valid StageNavigationRequest.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                return;
            }

            if (_sceneLoadPort != null)
            {
                try
                {
                    StageLaunchContextStore.SetCurrent(request.StageId);
                    _sceneLoadPort.LoadScene(_sceneName);
                }
                catch
                {
                    StageLaunchContextStore.TryClearCurrent(request.StageId);
                    throw;
                }

                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                SceneTransitionCoordinator.Instance.TryStartStageTransition(request, _sceneName);
            }
        }
    }
}
