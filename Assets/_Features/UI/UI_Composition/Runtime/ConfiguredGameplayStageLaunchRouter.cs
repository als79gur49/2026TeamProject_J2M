using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class ConfiguredGameplayStageLaunchRouter : IStageLaunchRouter
    {
        private readonly GameplayStageLaunchRouteConfig _routeConfig;
        private readonly ISceneLoadPort _sceneLoadPort;

        public ConfiguredGameplayStageLaunchRouter(
            GameplayStageLaunchRouteConfig routeConfig,
            ISceneLoadPort sceneLoadPort = null)
        {
            _routeConfig = routeConfig ?? throw new ArgumentNullException(nameof(routeConfig));
            _sceneLoadPort = sceneLoadPort;
        }

        public void Launch(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Configured gameplay launch requires a valid stage navigation request.", nameof(request));
            }

            if (!_routeConfig.HasValidGameplayShellScene)
            {
                throw new InvalidOperationException("Configured gameplay launch requires a gameplay shell scene in route config.");
            }

            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();

            if (_sceneLoadPort != null)
            {
                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
                {
                    SceneName = _routeConfig.GameplayShellSceneName,
                    Source = request.Source,
                    RequestedStageId = request.StageId.Value,
                    LaunchStageId = request.StageId.Value,
                    EditorDirectPlayMode = EditorDirectPlayContextStore.GetCurrentOrNone().Mode,
                });
                StageLaunchContextStore.SetCurrent(request.StageId);
                _sceneLoadPort.LoadScene(_routeConfig.GameplayShellSceneName);
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                StageLaunchContextStore.SetCurrent(request.StageId);
                SceneTransitionCoordinator.Instance.TryStartStageTransition(
                    request.TransitionHint.HasExplicitKind
                        ? request
                        : new StageNavigationRequest(
                            request.StageId,
                            request.NavigationKind,
                            request.Source,
                            StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay)),
                    _routeConfig.GameplayShellSceneName);
                return;
            }

            CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
            {
                SceneName = _routeConfig.GameplayShellSceneName,
                Source = request.Source,
                RequestedStageId = request.StageId.Value,
                LaunchStageId = request.StageId.Value,
                EditorDirectPlayMode = EditorDirectPlayContextStore.GetCurrentOrNone().Mode,
            });
            StageLaunchContextStore.SetCurrent(request.StageId);
            UnitySceneLoadPort.Instance.LoadScene(_routeConfig.GameplayShellSceneName);
        }
    }
}
