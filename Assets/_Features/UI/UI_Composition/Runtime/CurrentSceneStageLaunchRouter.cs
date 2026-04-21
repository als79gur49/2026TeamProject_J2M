using System;
using Game.Feature.Stages;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.UI.Composition
{
    internal sealed class CurrentSceneStageLaunchRouter : IStageLaunchRouter
    {
        private readonly string sceneName;

        public CurrentSceneStageLaunchRouter(string sceneName)
        {
            this.sceneName = sceneName ?? string.Empty;
        }

        public void Launch(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Stage launch router requires a valid StageNavigationRequest.", nameof(request));
            }

            StageLaunchContextStore.SetCurrent(request.StageId);
            if (UnityEngine.Application.isPlaying && !string.IsNullOrWhiteSpace(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
