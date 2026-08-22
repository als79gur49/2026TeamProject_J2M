using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class ConfiguredGameplayStageLaunchRouter : IStageLaunchRouter
    {
        private readonly GameplayStageLaunchRouteConfig _routeConfig;
        private readonly ISceneLoadPort _sceneLoadPort;

        public ConfiguredGameplayStageLaunchRouter(GameplayStageLaunchRouteConfig routeConfig)
            : this(routeConfig, null)
        {
        }

        internal ConfiguredGameplayStageLaunchRouter(
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

            if (request.TransitionIntent != SceneTransitionIntent.Unknown)
            {
                SceneTransitionRoutePolicyCatalog.RequireDestination(
                    SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent),
                    SceneTransitionDestinationKind.Gameplay);
            }
            else if (_sceneLoadPort == null)
            {
                SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent);
            }

            if (!_routeConfig.HasValidGameplayShellScene)
            {
                throw new InvalidOperationException("Configured gameplay launch requires a gameplay shell scene in route config.");
            }

            var continuingDirectPlayContext = request.EditorDirectPlayContext;
            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            CampaignLaunchHandoff launchHandoff = null;
            StageLaunchContext launchContext;
            if (launchHandoffStore.TryPeek(out var pendingHandoff))
            {
                if (!pendingHandoff.Matches(request))
                {
                    throw new InvalidOperationException(
                        "Configured gameplay launch request does not match the pending campaign launch handoff.");
                }

                launchHandoff = pendingHandoff;
                launchContext = StageLaunchContext.FromHandoff(pendingHandoff);
            }
            else
            {
                if (!CampaignPendinglessLaunchPolicy.IsAllowed(request))
                {
                    throw new InvalidOperationException(
                        "Configured gameplay launch without a pending handoff is restricted to committed retry/next-stage routes.");
                }

                launchContext = StageLaunchContext.CreatePendinglessReload(request);
            }

            var previousDirectPlayContext = EditorDirectPlayContextStore.GetCurrentOrNone();
            var consumedBootstrapContext =
                ConsumeInitialDirectPlayBootstrapContext(continuingDirectPlayContext);
            if (continuingDirectPlayContext.Mode == EditorDirectPlayMode.None)
            {
                EditorDirectPlayContextStore.Clear();
            }
            else
            {
                EditorDirectPlayContextStore.SetCurrent(continuingDirectPlayContext);
            }
            var directPlayOwnershipGeneration = EditorDirectPlayContextStore.OwnershipGeneration;

            if (_sceneLoadPort != null)
            {
                SceneTransitionRoutePolicyCatalog.ResolveException(
                    SceneTransitionIntent.TestInjectedSceneLoad,
                    SceneTransitionRouteClassification.TestOnly);
                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
                {
                    SceneName = _routeConfig.GameplayShellSceneName,
                    Source = request.Source,
                    RequestedStageId = request.StageId.Value,
                    LaunchStageId = request.StageId.Value,
                    EditorDirectPlayMode = continuingDirectPlayContext.Mode,
                });
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
                            launchHandoff,
                            launchHandoffStore);
                        callbackPort.LoadScene(
                            _routeConfig.GameplayShellSceneName,
                            () => terminal.CompleteSuccess(),
                            exception =>
                            {
                                if (terminal.CompleteFailure(exception))
                                {
                                    TryRestoreRejectedLaunchState(
                                        previousDirectPlayContext,
                                        continuingDirectPlayContext,
                                        consumedBootstrapContext,
                                        directPlayOwnershipGeneration);
                                }
                            });
                    }
                    else
                    {
                        _sceneLoadPort.LoadScene(_routeConfig.GameplayShellSceneName);
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

                    TryRestoreRejectedLaunchState(
                        previousDirectPlayContext,
                        continuingDirectPlayContext,
                        consumedBootstrapContext,
                        directPlayOwnershipGeneration);

                    throw;
                }

                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                try
                {
                    var accepted = SceneTransitionCoordinator.Instance.TryStartStageTransition(
                        request,
                        _routeConfig.GameplayShellSceneName,
                        launchHandoff?.Token);
                    if (!accepted)
                    {
                        throw new InvalidOperationException(
                            "Configured gameplay launch was rejected before the scene transition started.");
                    }
                }
                catch
                {
                    TryRestoreRejectedLaunchState(
                        previousDirectPlayContext,
                        continuingDirectPlayContext,
                        consumedBootstrapContext,
                        directPlayOwnershipGeneration);
                    throw;
                }

                return;
            }

            SceneTransitionRoutePolicyCatalog.ResolveException(
                SceneTransitionIntent.EditorDirectSceneLoad,
                SceneTransitionRouteClassification.EditorOnly);
            CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
            {
                SceneName = _routeConfig.GameplayShellSceneName,
                Source = request.Source,
                RequestedStageId = request.StageId.Value,
                LaunchStageId = request.StageId.Value,
                EditorDirectPlayMode = continuingDirectPlayContext.Mode,
            });
            var fallbackContextRegisteredByThisAttempt = false;
            try
            {
                if (!StageLaunchContextStore.TrySetCurrent(launchContext))
                {
                    throw new InvalidOperationException(
                        "A different stage launch operation already owns the context.");
                }

                fallbackContextRegisteredByThisAttempt = true;
                UnitySceneLoadPort.Instance.LoadScene(_routeConfig.GameplayShellSceneName);
            }
            catch
            {
                if (fallbackContextRegisteredByThisAttempt)
                {
                    StageLaunchContextStore.TryClear(launchContext);
                    if (launchHandoff != null)
                    {
                        launchHandoffStore.TryClear(launchHandoff.Token);
                    }
                }

                TryRestoreRejectedLaunchState(
                    previousDirectPlayContext,
                    continuingDirectPlayContext,
                    consumedBootstrapContext,
                    directPlayOwnershipGeneration);

                throw;
            }
        }

        private static StageLaunchContext ConsumeInitialDirectPlayBootstrapContext(
            EditorDirectPlayContext continuingDirectPlayContext)
        {
            if (continuingDirectPlayContext.Mode == EditorDirectPlayMode.None ||
                !StageLaunchContextStore.TryPeek(out var current) ||
                !current.IsEditorDirectPlayBootstrap)
            {
                return null;
            }

            if (!StageLaunchContextStore.TryConsume(current, out var consumed) ||
                !ReferenceEquals(consumed, current))
            {
                throw new InvalidOperationException(
                    "Configured gameplay launch could not consume its initial DirectPlay bootstrap context.");
            }

            return consumed;
        }

        private static void TryRestoreRejectedLaunchState(
            EditorDirectPlayContext previousContext,
            EditorDirectPlayContext attemptedContext,
            StageLaunchContext consumedBootstrapContext,
            long expectedDirectPlayOwnershipGeneration)
        {
            if (EditorDirectPlayContextStore.OwnershipGeneration !=
                expectedDirectPlayOwnershipGeneration)
            {
                return;
            }

            if (consumedBootstrapContext != null &&
                StageLaunchContextStore.TryPeekPendingEditorDirectPlayContext(out _))
            {
                return;
            }

            var currentContext = EditorDirectPlayContextStore.GetCurrentOrNone();
            if (currentContext.Mode != EditorDirectPlayMode.None &&
                !currentContext.Equals(attemptedContext))
            {
                return;
            }

            if (previousContext.Mode == EditorDirectPlayMode.None)
            {
                EditorDirectPlayContextStore.Clear();
            }
            else
            {
                EditorDirectPlayContextStore.SetCurrent(previousContext);
            }

            if (consumedBootstrapContext != null &&
                !StageLaunchContextStore.TryPeek(out _) &&
                !StageLaunchContextStore.TryPeekPendingEditorDirectPlayContext(out _))
            {
                StageLaunchContextStore.TrySetCurrent(consumedBootstrapContext);
            }
        }
    }
}
