using System;
using Game.Feature.Gameplay.UIAccess.DebugCommands;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    internal sealed class CurrentSceneStageLaunchRouter : IStageLaunchRouter, IDebugStageLaunchGateway
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
                StageLaunchContextStore.SetCurrent(request.StageId);
                _sceneLoadPort.LoadScene(_sceneName);
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                SceneTransitionCoordinator.Instance.TryStartStageTransition(request, _sceneName);
            }
        }

        public DebugCommandResult TryLaunch(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                return DebugCommandResult.Failed("Stage launch router requires a valid StageNavigationRequest.");
            }

            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                return DebugCommandResult.Unavailable("Debug stage launch scene is not configured.");
            }

            if (_sceneLoadPort != null)
            {
                try
                {
                    StageLaunchContextStore.SetCurrent(request.StageId);
                    _sceneLoadPort.LoadScene(_sceneName);
                    return DebugCommandResult.Success(
                        $"Routing to next stage '{request.StageId.Value}'.",
                        request.StageId,
                        request);
                }
                catch (Exception exception)
                {
                    return DebugCommandResult.Failed(exception.Message);
                }
            }

            if (!UnityEngine.Application.isPlaying)
            {
                return DebugCommandResult.Unavailable("Debug stage launch requires play mode.");
            }

            var coordinator = SceneTransitionCoordinator.Instance;
            if (coordinator.IsTransitionInProgress)
            {
                return DebugCommandResult.Unavailable("Scene transition is already in progress.");
            }

            try
            {
                if (!coordinator.TryStartStageTransition(request, _sceneName))
                {
                    return DebugCommandResult.Unavailable("Scene transition could not be started.");
                }
            }
            catch (Exception exception)
            {
                return DebugCommandResult.Failed(exception.Message);
            }

            return DebugCommandResult.Success(
                $"Routing to next stage '{request.StageId.Value}'.",
                request.StageId,
                request);
        }
    }
}
