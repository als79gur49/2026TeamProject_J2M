using System;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages
{
    public interface ISceneLoadPort
    {
        void LoadScene(string sceneName);
    }

    public sealed class UnitySceneLoadPort : ISceneLoadPort
    {
        public static readonly UnitySceneLoadPort Instance = new();

        private UnitySceneLoadPort()
        {
        }

        public void LoadScene(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }

    public sealed class SceneNameStageLaunchRouter : IStageLaunchRouter
    {
        private readonly ISceneLoadPort _sceneLoadPort;
        private readonly string _sceneName;

        public SceneNameStageLaunchRouter(string sceneName, ISceneLoadPort sceneLoadPort = null)
        {
            _sceneName = sceneName ?? string.Empty;
            _sceneLoadPort = sceneLoadPort ?? UnitySceneLoadPort.Instance;
        }

        public void Launch(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Stage launch router requires a valid StageNavigationRequest.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                throw new InvalidOperationException("Stage launch router requires a configured scene name.");
            }

            StageLaunchContextStore.SetCurrent(request.StageId);
            _sceneLoadPort.LoadScene(_sceneName);
        }
    }
}
