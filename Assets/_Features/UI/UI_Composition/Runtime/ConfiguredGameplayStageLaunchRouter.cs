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
            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            CampaignLaunchHandoff launchHandoff = null;
            if (launchHandoffStore.TryPeek(out var pendingHandoff))
            {
                if (!pendingHandoff.Matches(request))
                {
                    throw new InvalidOperationException(
                        "Configured gameplay launch request does not match the pending campaign launch handoff.");
                }

                launchHandoff = pendingHandoff;
            }

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
                try
                {
                    StageLaunchContextStore.SetCurrent(request.StageId);
                    _sceneLoadPort.LoadScene(_routeConfig.GameplayShellSceneName);
                }
                catch
                {
                    StageLaunchContextStore.TryClearCurrent(request.StageId);
                    if (launchHandoff != null)
                    {
                        launchHandoffStore.TryClear(launchHandoff.Token);
                    }

                    throw;
                }

                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                var accepted = SceneTransitionCoordinator.Instance.TryStartStageTransition(
                    request.TransitionHint.HasExplicitKind
                        ? request
                        : new StageNavigationRequest(
                            request.StageId,
                            request.NavigationKind,
                            request.Source,
                            StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay)),
                    _routeConfig.GameplayShellSceneName,
                    launchHandoff?.Token);
                if (!accepted)
                {
                    throw new InvalidOperationException(
                        "Configured gameplay launch was rejected before the scene transition started.");
                }

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
            try
            {
                StageLaunchContextStore.SetCurrent(request.StageId);
                UnitySceneLoadPort.Instance.LoadScene(_routeConfig.GameplayShellSceneName);
            }
            catch
            {
                StageLaunchContextStore.TryClearCurrent(request.StageId);
                if (launchHandoff != null)
                {
                    launchHandoffStore.TryClear(launchHandoff.Token);
                }

                throw;
            }
        }
    }
}
