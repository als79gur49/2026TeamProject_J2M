using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    internal sealed class CurrentSceneStageLaunchRouter : IStageLaunchRouter
    {
        private readonly ISceneLoadPort _sceneLoadPort;
        private readonly string _sceneName;
        private readonly Func<StageNavigationRequest, string, bool> _tryStartStageTransition;

        public CurrentSceneStageLaunchRouter(
            string sceneName,
            ISceneLoadPort sceneLoadPort = null,
            Func<StageNavigationRequest, string, bool> tryStartStageTransition = null)
        {
            _sceneName = sceneName ?? string.Empty;
            _sceneLoadPort = sceneLoadPort;
            _tryStartStageTransition = tryStartStageTransition;
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

            if (request.TransitionIntent != SceneTransitionIntent.Unknown)
            {
                SceneTransitionRoutePolicyCatalog.RequireDestination(
                    SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent),
                    SceneTransitionDestinationKind.Gameplay);
            }
            else if (_sceneLoadPort == null && _tryStartStageTransition == null)
            {
                SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent);
            }

            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                return;
            }

            if (CampaignLaunchHandoffSessionStore.Instance.TryPeek(out _))
            {
                throw new InvalidOperationException(
                    "Current-scene reload cannot run while a normal campaign handoff is pending.");
            }

            if (!CampaignPendinglessLaunchPolicy.IsAllowed(request))
            {
                throw new InvalidOperationException(
                    "Current-scene launch is restricted to committed retry/next-stage routes.");
            }

            var launchContext = StageLaunchContext.CreatePendinglessReload(request);

            if (_sceneLoadPort != null)
            {
                SceneTransitionRoutePolicyCatalog.ResolveException(
                    SceneTransitionIntent.TestInjectedSceneLoad,
                    SceneTransitionRouteClassification.TestOnly);
                var contextRegisteredByThisAttempt = false;
                try
                {
                    if (!StageLaunchContextStore.TrySetCurrent(launchContext))
                    {
                        throw new InvalidOperationException(
                            "A different stage launch operation already owns the context.");
                    }

                    contextRegisteredByThisAttempt = true;
                    if (_sceneLoadPort is ICallbackSceneLoadPort callbackPort)
                    {
                        var terminal = new StageLoadCallbackOwnership(
                            launchContext,
                            capturedHandoff: null,
                            CampaignLaunchHandoffSessionStore.Instance);
                        callbackPort.LoadScene(
                            _sceneName,
                            () => terminal.CompleteSuccess(),
                            exception => terminal.CompleteFailure(exception));
                    }
                    else
                    {
                        _sceneLoadPort.LoadScene(_sceneName);
                    }
                }
                catch
                {
                    if (contextRegisteredByThisAttempt)
                    {
                        StageLaunchContextStore.TryClear(launchContext);
                    }

                    throw;
                }

                return;
            }

            if (_tryStartStageTransition != null || UnityEngine.Application.isPlaying)
            {
                if (_tryStartStageTransition != null)
                {
                    SceneTransitionRoutePolicyCatalog.ResolveException(
                        SceneTransitionIntent.TestInjectedSceneLoad,
                        SceneTransitionRouteClassification.TestOnly);
                }

                var accepted = _tryStartStageTransition != null
                    ? _tryStartStageTransition(request, _sceneName)
                    : SceneTransitionCoordinator.Instance.TryStartStageTransition(request, _sceneName);
                if (!accepted)
                {
                    throw new InvalidOperationException(
                        "Current-scene gameplay launch was rejected before the scene transition started.");
                }
            }
        }
    }
}
