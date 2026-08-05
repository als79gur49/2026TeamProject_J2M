using System;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages
{
    public interface ISceneLoadPort
    {
        void LoadScene(string sceneName);
    }

    public interface ICallbackSceneLoadPort : ISceneLoadPort
    {
        void LoadScene(
            string sceneName,
            Action completed,
            Action<Exception> failed);
    }

    public sealed class UnitySceneLoadPort : ISceneLoadPort
    {
        public static readonly UnitySceneLoadPort Instance = new();

        private UnitySceneLoadPort()
        {
        }

        public void LoadScene(string sceneName)
        {
            SceneTransitionRoutePolicyCatalog.ResolveException(
                SceneTransitionIntent.EditorDirectSceneLoad,
                SceneTransitionRouteClassification.EditorOnly);
            if (UnityEngine.Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "UnitySceneLoadPort is an editor-only non-playing direct-load exception.");
            }

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

        public SceneNameStageLaunchRouter(string sceneName, ISceneLoadPort sceneLoadPort)
        {
            _sceneName = sceneName ?? string.Empty;
            _sceneLoadPort = sceneLoadPort ??
                throw new ArgumentNullException(
                    nameof(sceneLoadPort),
                    "SceneNameStageLaunchRouter is a test-only injected direct-load exception.");
        }

        public void Launch(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Stage launch router requires a valid StageNavigationRequest.", nameof(request));
            }

            SceneTransitionRoutePolicyCatalog.ResolveException(
                SceneTransitionIntent.TestInjectedSceneLoad,
                SceneTransitionRouteClassification.TestOnly);
            if (UnityEngine.Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "SceneNameStageLaunchRouter is a test-only direct-load exception.");
            }

            if (request.TransitionIntent != SceneTransitionIntent.Unknown)
            {
                SceneTransitionRoutePolicyCatalog.RequireDestination(
                    SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent),
                    SceneTransitionDestinationKind.Gameplay);
            }

            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                throw new InvalidOperationException("Stage launch router requires a configured scene name.");
            }

            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            CampaignLaunchHandoff launchHandoff = null;
            StageLaunchContext launchContext;
            if (launchHandoffStore.TryPeek(out var pendingHandoff))
            {
                if (!pendingHandoff.Matches(request))
                {
                    throw new InvalidOperationException(
                        "Stage launch request does not match the pending campaign launch handoff.");
                }

                launchHandoff = pendingHandoff;
                launchContext = StageLaunchContext.FromHandoff(pendingHandoff);
            }
            else
            {
                if (!CampaignPendinglessLaunchPolicy.IsAllowed(request))
                {
                    throw new InvalidOperationException(
                        "Stage launch without a pending handoff is restricted to committed retry/next-stage routes.");
                }

                launchContext = StageLaunchContext.CreatePendinglessReload(request);
            }

            var contextRegisteredByThisAttempt = false;
            try
            {
                if (!StageLaunchContextStore.TrySetCurrent(launchContext))
                {
                    throw new InvalidOperationException("A different stage launch operation already owns the context.");
                }

                contextRegisteredByThisAttempt = true;
                if (_sceneLoadPort is ICallbackSceneLoadPort callbackPort)
                {
                    var terminal = new StageLoadCallbackOwnership(
                        launchContext,
                        launchHandoff,
                        launchHandoffStore);
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
                    if (launchHandoff != null)
                    {
                        launchHandoffStore.TryClear(launchHandoff.Token);
                    }
                }

                throw;
            }
        }
    }

    public sealed class StageLoadCallbackOwnership
    {
        private readonly StageLaunchContext _capturedContext;
        private readonly CampaignLaunchHandoff _capturedHandoff;
        private readonly ICampaignLaunchHandoffStore _handoffStore;
        private bool _terminal;

        public StageLoadCallbackOwnership(
            StageLaunchContext capturedContext,
            CampaignLaunchHandoff capturedHandoff,
            ICampaignLaunchHandoffStore handoffStore)
        {
            _capturedContext = capturedContext ?? throw new ArgumentNullException(nameof(capturedContext));
            _capturedHandoff = capturedHandoff;
            _handoffStore = handoffStore ?? throw new ArgumentNullException(nameof(handoffStore));
        }

        public bool CompleteSuccess()
        {
            if (_terminal || !StageLaunchContextStore.IsCurrent(_capturedContext))
            {
                return false;
            }

            if (_capturedHandoff != null &&
                (!_handoffStore.TryPeek(out var currentHandoff) ||
                 !currentHandoff.Matches(_capturedHandoff)))
            {
                return false;
            }

            _terminal = true;
            return true;
        }

        public bool CompleteFailure(Exception exception)
        {
            if (_terminal || !StageLaunchContextStore.IsCurrent(_capturedContext))
            {
                return false;
            }

            if (_capturedHandoff != null &&
                (!_handoffStore.TryPeek(out var currentHandoff) ||
                 !currentHandoff.Matches(_capturedHandoff)))
            {
                return false;
            }

            _terminal = true;
            StageLaunchContextStore.TryClear(_capturedContext);
            if (_capturedHandoff != null)
            {
                _handoffStore.TryClear(_capturedHandoff.Token);
            }

            return true;
        }
    }
}
